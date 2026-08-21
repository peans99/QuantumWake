using System.Diagnostics;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Quantumwake.Server;

namespace Quantumwake.Overlay;

/// <summary>
/// The whole application: web server, tray icon and overlay window in one
/// process.
/// </summary>
/// <remarks>
/// <para>
/// The overlay used to launch <c>Quantumwake.Server.exe</c> as a child process.
/// Hosting it here instead is what lets the app ship as a single executable -
/// nothing has to locate a second binary beside it, and no orphaned server is
/// left running if the overlay dies badly.
/// </para>
/// <para>
/// Shutdown is explicit rather than tied to the window. Hiding or closing the
/// overlay leaves Quantum Wake in the tray with the dashboard still served,
/// which is the point for anyone on a second monitor. Only <b>Quit</b> ends the
/// process.
/// </para>
/// </remarks>
public partial class App : System.Windows.Application
{
    private const string DashboardUrl = "http://127.0.0.1:31337/";

    private WebApplication? _server;
    private TrayPresence? _tray;
    private MainWindow? _overlay;
    private Settings _settings = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settings = Settings.Load();

        _tray = new TrayPresence(_settings.ShowOverlay);
        _tray.OpenDashboardRequested += OpenDashboard;
        _tray.OverlayToggled += SetOverlayVisible;
        _tray.QuitRequested += Quit;

        await StartServerAsync(e.Args);

        _overlay = new MainWindow();

        if (_settings.ShowOverlay)
            _overlay.Show();
    }

    private async Task StartServerAsync(string[] args)
    {
        try
        {
            _server = ServerHost.Build(args);
            await _server.StartAsync();
        }
        catch (Exception ex)
        {
            // The usual cause is another copy already listening on the port, in
            // which case the dashboard still works and only this instance's
            // server is redundant. Anything else is worth saying out loud.
            _server = null;
            _tray?.Notify($"The dashboard could not start: {ex.Message}");
        }
    }

    private void OpenDashboard()
    {
        try
        {
            Process.Start(new ProcessStartInfo(DashboardUrl) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _tray?.Notify($"Could not open a browser. The dashboard is at {DashboardUrl}");
        }
    }

    /// <summary>Shows or hides the overlay, and remembers the choice.</summary>
    private void SetOverlayVisible(bool visible)
    {
        if (visible)
            (_overlay ??= new MainWindow()).Show();
        else
            _overlay?.Hide();

        _settings = _settings with { ShowOverlay = visible };
        _settings.Save();
    }

    private async void Quit()
    {
        _tray?.Dispose();

        // Close the window before stopping the server: closing is what saves the
        // overlay's geometry, and it should not race a shutting-down host.
        _overlay?.Close();

        if (_server is not null)
        {
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await _server.StopAsync(deadline.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
            {
                // Best effort on the way out.
            }
        }

        Shutdown();
    }
}
