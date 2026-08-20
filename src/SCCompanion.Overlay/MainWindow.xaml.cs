using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
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

    public MainWindow()
    {
        InitializeComponent();

        // Park in the top-right corner of the primary screen by default.
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 24;
        Top = work.Top + 24;
    }

    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        NativeWindowStyles.ApplyOverlayStyles(this);
        NativeWindowStyles.SetClickThrough(this, _clickThrough);

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

        if (msg == WM_HOTKEY && wParam.ToInt32() == ToggleHotkeyId)
        {
            ToggleClickThrough();
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Switches between "informational" (clicks reach the game) and
    /// "interactive" (the overlay can be moved and clicked).
    /// </summary>
    private void ToggleClickThrough()
    {
        _clickThrough = !_clickThrough;
        NativeWindowStyles.SetClickThrough(this, _clickThrough);

        // Going fully opaque while interactive makes the mode obvious at a glance.
        // WebView2 exposes no settable Opacity, so this is applied to the window.
        Opacity = _clickThrough ? 0.92 : 1.0;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // Only draggable while interactive; otherwise clicks belong to the game.
        if (!_clickThrough && e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
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
