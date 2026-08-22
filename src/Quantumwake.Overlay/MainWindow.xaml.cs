using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

// Hosting the tray icon means UseWindowsForms, which puts System.Drawing and
// System.Windows.Forms into the implicit usings and makes Point, Color and
// Brushes ambiguous. This window is WPF; say so once here rather than
// qualifying every use.
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace Quantumwake.Overlay;

/// <summary>
/// Transparent always-on-top shell hosting the shared web UI.
/// </summary>
/// <remarks>
/// The overlay renders nothing itself: it loads the same pages the browser does,
/// with <c>?overlay=1</c> so the stylesheet drops the chrome and paints a
/// translucent background. One UI, three hosts.
/// </remarks>
public partial class MainWindow : Window
{
    private const int ToggleHotkeyId = 0xA11;
    private const int PrevViewHotkeyId = 0xA12;
    private const int NextViewHotkeyId = 0xA13;
    private const int FullscreenHotkeyId = 0xA14;

    private const uint VkO = 0x4F;
    private const uint VkF = 0x46;
    private const uint VkLeft = 0x25;
    private const uint VkRight = 0x27;

    private static readonly Uri ServerRoot = new("http://127.0.0.1:31337/");

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
    /// <summary>
    /// Whether clicks pass straight through to the game - "pinned".
    /// </summary>
    /// <remarks>
    /// Starts off. It used to start on, which meant the overlay arrived as a
    /// pane that could not be moved, resized or closed, and the only way out
    /// was a hotkey nothing on screen mentioned. Players reported exactly that.
    /// An overlay you can grab is the sane first impression; pinning it out of
    /// the way is the deliberate act, and it has a button.
    /// </remarks>
    private bool _clickThrough;


    /// <summary>The widget-sized bounds to return to when fullscreen ends.</summary>
    private Rect _restoreBounds;
    private bool _isFullscreen;

    /// <summary>
    /// Width of the transparent gutter around the WebView2 that acts as the
    /// resize border. Must match the Grid margin in XAML.
    /// </summary>
    private const double GutterWidth = 8;

    public MainWindow()
    {
        InitializeComponent();

        var saved = OverlayGeometry.Load();

        if (saved is not null && saved.IsOnScreen())
        {
            Left = saved.Left;
            Top = saved.Top;
            Width = saved.Width;
            Height = saved.Height;
        }
        else
        {
            // Park in the top-right corner of the primary screen by default.
            var work = SystemParameters.WorkArea;
            Left = work.Right - Width - 24;
            Top = work.Top + 24;
        }
    }

    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        NativeWindowStyles.ApplyOverlayStyles(this);
        NativeWindowStyles.SetClickThrough(this, _clickThrough);
        ApplyInteractionMode();

        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        source.AddHook(HandleWindowMessage);

        const uint ctrlAlt = NativeWindowStyles.Modifiers.Control | NativeWindowStyles.Modifiers.Alt;

        // Ctrl+Alt+O still pins and unpins, for anyone who wants it, but it is
        // no longer the only way back: the pin button does it while unpinned,
        // and the tray menu does it while pinned.
        if (!NativeWindowStyles.RegisterGlobalHotKey(
                this, ToggleHotkeyId, ctrlAlt | NativeWindowStyles.Modifiers.NoRepeat, VkO))
        {
            SplashText.Text = "Ctrl+Alt+O is already in use by another app; " +
                              "pin and unpin from the header or the tray icon.";
        }

        // View switching works even while pinned, so the widget can be paged
        // through mid-flight without unpinning. Fullscreen too: a pinned
        // overlay blown up to full size is a HUD over the whole game.
        NativeWindowStyles.RegisterGlobalHotKey(this, PrevViewHotkeyId, ctrlAlt, VkLeft);
        NativeWindowStyles.RegisterGlobalHotKey(this, NextViewHotkeyId, ctrlAlt, VkRight);
        NativeWindowStyles.RegisterGlobalHotKey(
            this, FullscreenHotkeyId, ctrlAlt | NativeWindowStyles.Modifiers.NoRepeat, VkF);

        await StartAsync();
    }

    private async Task StartAsync()
    {
        // The server runs inside this process now - App starts it before the
        // window appears - so this only waits for it to finish binding.
        SplashText.Text = "AWAITING LINK…";

        if (!await WaitForServerAsync(TimeSpan.FromSeconds(40)))
        {
            SplashText.Text = "The server did not respond on 127.0.0.1:31337.";
            return;
        }

        await ShowDashboardAsync();
    }

    private async Task ShowDashboardAsync()
    {
        // Keep the WebView2 user-data folder out of Program Files.
        var userData = Quantumwake.Core.AppPaths.In("WebView2");

        Directory.CreateDirectory(userData);

        var environment = await CoreWebView2Environment.CreateAsync(null, userData);
        await Browser.EnsureCoreWebView2Async(environment);

        var settings = Browser.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;

        Browser.Source = new Uri(ServerRoot, "?overlay=1");
        Browser.NavigationCompleted += (_, _) => Splash.Visibility = Visibility.Collapsed;
    }

    private async Task<bool> IsServerUpAsync()
    {
        try
        {
            using var response = await _http.GetAsync(new Uri(ServerRoot, "api/install"));
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> WaitForServerAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await IsServerUpAsync())
                return true;

            await Task.Delay(500);
        }

        return false;
    }

    private IntPtr HandleWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        const int WM_NCHITTEST = 0x0084;

        if (msg == WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case ToggleHotkeyId:
                    ToggleClickThrough();
                    handled = true;
                    return IntPtr.Zero;

                case PrevViewHotkeyId:
                    CycleView(-1);
                    handled = true;
                    return IntPtr.Zero;

                case NextViewHotkeyId:
                    CycleView(1);
                    handled = true;
                    return IntPtr.Zero;

                case FullscreenHotkeyId:
                    ToggleFullscreen();
                    handled = true;
                    return IntPtr.Zero;
            }
        }

        // WebView2 covers the client area and swallows the mouse, so WPF's own
        // resize borders never see it. Claiming the outer gutter here hands the
        // edges back to the system resize loop.
        if (msg == WM_NCHITTEST && !_clickThrough && !_isFullscreen)
        {
            var hit = HitTestGutter(lParam);
            if (hit != HitTest.Client)
            {
                handled = true;
                return new IntPtr((int)hit);
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>Standard Win32 hit-test results for window edges.</summary>
    private enum HitTest
    {
        Client = 1,
        Left = 10,
        Right = 11,
        Top = 12,
        TopLeft = 13,
        TopRight = 14,
        Bottom = 15,
        BottomLeft = 16,
        BottomRight = 17
    }

    /// <summary>Maps a screen point to a resize edge, or Client if it is inside.</summary>
    private HitTest HitTestGutter(IntPtr lParam)
    {
        // Screen coordinates are packed as two signed shorts; they go negative on
        // monitors left of or above the primary one.
        var packed = lParam.ToInt64();
        var screenX = (short)(packed & 0xFFFF);
        var screenY = (short)((packed >> 16) & 0xFFFF);

        Point point;
        try
        {
            point = PointFromScreen(new Point(screenX, screenY));
        }
        catch (InvalidOperationException)
        {
            // No presentation source yet, during teardown.
            return HitTest.Client;
        }

        var onLeft = point.X <= GutterWidth;
        var onRight = point.X >= ActualWidth - GutterWidth;
        var onTop = point.Y <= GutterWidth;
        var onBottom = point.Y >= ActualHeight - GutterWidth;

        return (onLeft, onRight, onTop, onBottom) switch
        {
            (true, _, true, _) => HitTest.TopLeft,
            (_, true, true, _) => HitTest.TopRight,
            (true, _, _, true) => HitTest.BottomLeft,
            (_, true, _, true) => HitTest.BottomRight,
            (true, _, _, _) => HitTest.Left,
            (_, true, _, _) => HitTest.Right,
            (_, _, true, _) => HitTest.Top,
            (_, _, _, true) => HitTest.Bottom,
            _ => HitTest.Client
        };
    }

    /// <summary>
    /// Pages the hosted dashboard forward or back. Driven from the shell rather
    /// than the page so it works while click-through is on and the WebView2
    /// receives no input at all.
    /// </summary>
    private async void CycleView(int delta)
    {
        if (Browser.CoreWebView2 is null)
            return;

        try
        {
            await Browser.ExecuteScriptAsync($"window.scCycleView && window.scCycleView({delta})");
        }
        catch (InvalidOperationException)
        {
            // The browser is still initialising; the hotkey is a no-op until then.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void PrevButton_Click(object sender, RoutedEventArgs e) => CycleView(-1);
    private void NextButton_Click(object sender, RoutedEventArgs e) => CycleView(1);
    private void FullscreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    /// <summary>
    /// Grows the widget to cover the monitor it is on, and back. The page is
    /// told, because at full size the six-tab whitelist lifts and the widget
    /// briefly is the whole dashboard; the compact bounds come back on exit
    /// and are the only geometry ever persisted.
    /// </summary>
    private async void ToggleFullscreen()
    {
        _isFullscreen = !_isFullscreen;

        if (_isFullscreen)
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);

            // The monitor under the window, in WPF units. Screen reports device
            // pixels; the presentation source carries the DPI transform.
            var hwnd = new WindowInteropHelper(this).Handle;
            var bounds = System.Windows.Forms.Screen.FromHandle(hwnd).Bounds;
            var transform = ((HwndSource)PresentationSource.FromVisual(this)!)
                .CompositionTarget.TransformFromDevice;

            var topLeft = transform.Transform(new Point(bounds.Left, bounds.Top));
            var bottomRight = transform.Transform(new Point(bounds.Right, bounds.Bottom));

            Left = topLeft.X;
            Top = topLeft.Y;
            Width = bottomRight.X - topLeft.X;
            Height = bottomRight.Y - topLeft.Y;
        }
        else
        {
            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;
        }

        FullscreenButton.ToolTip = _isFullscreen
            ? "Back to widget size (Ctrl+Alt+F)"
            : "Fullscreen (Ctrl+Alt+F)";

        if (Browser.CoreWebView2 is not null)
        {
            try
            {
                await Browser.ExecuteScriptAsync(
                    $"window.scOverlayExpanded && window.scOverlayExpanded({(_isFullscreen ? "true" : "false")})");
            }
            catch (InvalidOperationException)
            {
                // Still initialising; the page will simply start compact.
            }
        }
    }

    /// <summary>
    /// Switches between "informational" (clicks reach the game) and
    /// "interactive" (the overlay can be moved and clicked).
    /// </summary>
    private void ToggleClickThrough() => SetPinned(!_clickThrough);

    /// <summary>
    /// Pins the overlay out of the way, or takes it back.
    /// </summary>
    /// <remarks>
    /// A pinned window cannot be clicked at all - that is what pinning means -
    /// so the way back can never be a button on it. The tray menu carries it,
    /// and is told each time so its tick matches what the screen is doing.
    /// </remarks>
    public void SetPinned(bool pinned)
    {
        if (_clickThrough == pinned)
            return;

        _clickThrough = pinned;
        NativeWindowStyles.SetClickThrough(this, _clickThrough);
        ApplyInteractionMode();
        PinnedChanged?.Invoke(pinned);
    }

    /// <summary>Raised when the overlay is pinned or unpinned, however it happened.</summary>
    public event Action<bool>? PinnedChanged;

    public bool IsPinned => _clickThrough;

    private void PinButton_Click(object sender, RoutedEventArgs e) => SetPinned(true);

    /// <summary>
    /// Makes the current mode unmistakable. In pass-through the overlay is a
    /// clean readout; interactive gains a header, a lit border and a corner grip
    /// so the move and resize affordances are visible rather than guessed at.
    /// </summary>
    private void ApplyInteractionMode()
    {
        var interactive = !_clickThrough;

        HeaderBar.Visibility = interactive ? Visibility.Visible : Visibility.Collapsed;
        Grip.Visibility = interactive ? Visibility.Visible : Visibility.Collapsed;

        Frame.BorderBrush = interactive
            ? new SolidColorBrush(Color.FromRgb(0x35, 0xC8, 0xF0))
            : Brushes.Transparent;

        // Translucency is applied at the HWND level. WPF's Window.Opacity needs
        // AllowsTransparency, which cannot be used with WebView2.
        NativeWindowStyles.SetWindowAlpha(this, interactive ? (byte)255 : (byte)235);

        // WebView2 only routes mouse input once the window holds focus, so the
        // switch to interactive has to actually claim it.
        if (interactive)
        {
            Activate();
            Browser.Focus();
        }
    }

    /// <summary>Dragging is offered from the header strip only.</summary>
    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // A fullscreen overlay has nowhere to be dragged to.
        if (!_isFullscreen && e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Closing while fullscreen must not turn the saved widget geometry
        // into a monitor-sized window next launch.
        var saved = _isFullscreen
            ? _restoreBounds
            : new Rect(Left, Top, Width, Height);

        new OverlayGeometry(saved.Left, saved.Top, saved.Width, saved.Height).Save();

        NativeWindowStyles.UnregisterGlobalHotKey(this, ToggleHotkeyId);
        NativeWindowStyles.UnregisterGlobalHotKey(this, PrevViewHotkeyId);
        NativeWindowStyles.UnregisterGlobalHotKey(this, NextViewHotkeyId);
        NativeWindowStyles.UnregisterGlobalHotKey(this, FullscreenHotkeyId);

        _http.Dispose();
        base.OnClosed(e);
    }
}
