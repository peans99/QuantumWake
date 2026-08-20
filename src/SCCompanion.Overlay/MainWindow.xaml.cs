using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace SCCompanion.Overlay;

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
    private const uint VkO = 0x4F;

    private static readonly Uri ServerRoot = new("http://127.0.0.1:31337/");

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private Process? _server;
    private bool _clickThrough = true;

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

        if (!NativeWindowStyles.RegisterGlobalHotKey(
                this,
                ToggleHotkeyId,
                NativeWindowStyles.Modifiers.Control | NativeWindowStyles.Modifiers.Alt | NativeWindowStyles.Modifiers.NoRepeat,
                VkO))
        {
            SplashText.Text = "Ctrl+Alt+O is already in use by another app; " +
                              "click-through cannot be toggled.";
        }

        await StartAsync();
    }

    private async Task StartAsync()
    {
        SplashText.Text = "LOCATING SERVER…";

        if (!await IsServerUpAsync() && !TryStartServer())
        {
            SplashText.Text = "Could not start SCCompanion.Server. Run it manually, " +
                              "then reopen the overlay.";
            return;
        }

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
        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SCCompanion",
            "WebView2");

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

    /// <summary>
    /// Launches the server alongside the overlay so the user starts one thing.
    /// </summary>
    private bool TryStartServer()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "SCCompanion.Server.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "SCCompanion.Server", "bin", "Release", "net10.0", "SCCompanion.Server.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "SCCompanion.Server", "bin", "Debug", "net10.0", "SCCompanion.Server.exe"))
        };

        var executable = candidates.FirstOrDefault(File.Exists);
        if (executable is null)
            return false;

        try
        {
            _server = Process.Start(new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(executable)!
            });

            return _server is not null;
        }
        catch
        {
            return false;
        }
    }

    private IntPtr HandleWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        const int WM_NCHITTEST = 0x0084;

        if (msg == WM_HOTKEY && wParam.ToInt32() == ToggleHotkeyId)
        {
            ToggleClickThrough();
            handled = true;
            return IntPtr.Zero;
        }

        // WebView2 covers the client area and swallows the mouse, so WPF's own
        // resize borders never see it. Claiming the outer gutter here hands the
        // edges back to the system resize loop.
        if (msg == WM_NCHITTEST && !_clickThrough)
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

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Switches between "informational" (clicks reach the game) and
    /// "interactive" (the overlay can be moved and clicked).
    /// </summary>
    private void ToggleClickThrough()
    {
        _clickThrough = !_clickThrough;
        NativeWindowStyles.SetClickThrough(this, _clickThrough);
        ApplyInteractionMode();
    }

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

        // WebView2 exposes no settable Opacity, so this is applied to the window.
        Opacity = interactive ? 1.0 : 0.92;
    }

    /// <summary>Dragging is offered from the header strip only.</summary>
    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        new OverlayGeometry(Left, Top, Width, Height).Save();

        NativeWindowStyles.UnregisterGlobalHotKey(this, ToggleHotkeyId);

        // Only stop the server if this overlay started it.
        if (_server is { HasExited: false })
        {
            try
            {
                _server.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort on shutdown.
            }
        }

        _http.Dispose();
        base.OnClosed(e);
    }
}
