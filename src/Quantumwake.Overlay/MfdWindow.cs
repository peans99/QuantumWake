using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Brushes = System.Windows.Media.Brushes;

namespace Quantumwake.Overlay;

internal sealed class MfdWindow : Window
{
    private readonly WebView2 _browser = new();
    private readonly string _url;
    private bool _closed;
    public event Action<JsonElement>? Message;
    public event Action? Ready;

    public MfdWindow(string url, bool setup = false)
    {
        _url = url;
        Title = setup ? "Quantum Wake · MFD setup" : "Quantum Wake · MFD";
        Background = Brushes.Black;
        Width = setup ? 1100 : 480;
        Height = setup ? 800 : 480;
        if (setup) { MinWidth = 760; MinHeight = 640; }
        else
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
        }
        Content = _browser;
        SourceInitialized += (_, _) =>
        {
            if (setup) return;
            NativeWindowStyles.ApplyOverlayStyles(this);
            NativeWindowStyles.SetWindowAlpha(this, 255);
            NativeWindowStyles.SetClickThrough(this, true);
        };
        Loaded += async (_, _) =>
        {
            try
            {
                var environment = await CoreWebView2Environment.CreateAsync(null, Core.AppPaths.In("WebView2"));
                if (_closed) return;
                await _browser.EnsureCoreWebView2Async(environment);
                if (_closed) return;
                _browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _browser.CoreWebView2.NavigationStarting += (_, e) =>
                {
                    if (!string.Equals(e.Uri, _url, StringComparison.OrdinalIgnoreCase)) e.Cancel = true;
                };
                _browser.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
                _browser.CoreWebView2.WebMessageReceived += (_, e) =>
                {
                    if (!string.Equals(e.Source, _url, StringComparison.OrdinalIgnoreCase)) return;
                    try
                    {
                        using var json = JsonDocument.Parse(e.WebMessageAsJson);
                        Message?.Invoke(json.RootElement.Clone());
                    }
                    catch (JsonException) { }
                };
                _browser.NavigationCompleted += (_, e) => { if (e.IsSuccess) Ready?.Invoke(); };
                _browser.Source = new Uri(_url);
            }
            catch (Exception e) when (e is InvalidOperationException or COMException or System.IO.IOException)
            {
                if (!_closed) Content = new System.Windows.Controls.TextBlock {
                    Text = "MFD display could not load. Close it and reopen MFD setup from the tray.\n" + e.Message,
                    Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) };
            }
        };
        Closed += (_, _) => { _closed = true; _browser.Dispose(); };
    }

    public void Send(object message)
    {
        if (_closed || _browser.CoreWebView2 is null) return;
        _browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, MfdLayout.JsonOptions));
    }

    public void Place(MfdPanel panel, MfdMonitor monitor)
    {
        var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1),
                monitor.X + panel.X, monitor.Y + panel.Y, panel.Width, panel.Height, 0x10);
        }
        finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
    }

    public static MfdMonitor[] Monitors()
    {
        var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            return System.Windows.Forms.Screen.AllScreens.Select(s => new MfdMonitor(
                s.DeviceName, s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height, s.Primary)).ToArray();
        }
        finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
    }

    /// <summary>
    /// The monitor this window is sitting on, or null if it is nowhere known.
    /// </summary>
    /// <remarks>
    /// Asked of the setup window, so the backdrop can leave that monitor alone
    /// while the pilot is working on it. By the window's own centre, because a
    /// window dragged across a boundary belongs to whichever monitor holds most
    /// of it - the same answer Windows itself gives.
    /// </remarks>
    public string? MonitorId(IReadOnlyList<MfdMonitor> monitors)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect)) return null;
            int x = (rect.Left + rect.Right) / 2, y = (rect.Top + rect.Bottom) / 2;
            return monitors.FirstOrDefault(m =>
                x >= m.X && y >= m.Y && x < m.X + m.Width && y < m.Y + m.Height)?.Id;
        }
        finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect { public int Left, Top, Right, Bottom; }
}
