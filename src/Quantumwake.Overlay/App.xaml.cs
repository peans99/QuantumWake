using System.Diagnostics;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Quantumwake.Core.Logging;
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

    /// <summary>Set while shutting down, so a closing overlay is not mistaken for the user turning it off.</summary>
    private bool _quitting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Before Settings.Load, so --data moves the whole app - overlay
        // preferences and WebView2 profile included - and not just the server.
        Core.AppPaths.UseFromArguments(e.Args);

        _settings = Settings.Load();

        _tray = new TrayPresence(_settings.ShowOverlay);
        _tray.OpenDashboardRequested += OpenDashboard;
        _tray.OverlayToggled += SetOverlayVisible;
        _tray.SetInstallFolderRequested += PickInstallFolder;
        _tray.QuitRequested += Quit;

        await StartServerAsync(e.Args);

        _overlay = CreateOverlay();

        if (_settings.ShowOverlay)
            _overlay.Show();
    }

    /// <summary>
    /// Builds the overlay window and watches for it closing.
    /// </summary>
    /// <remarks>
    /// A closed WPF window cannot be shown again, so the reference has to be
    /// dropped when the user closes the widget from its own ✕ - otherwise
    /// turning the overlay back on from the dashboard or the tray would throw
    /// on a corpse. Closing is also a way of turning the overlay off, so every
    /// surface that reports its state is told.
    /// </remarks>
    private MainWindow CreateOverlay()
    {
        var window = new MainWindow();

        window.Closed += (_, _) =>
        {
            _overlay = null;

            // On the way out of the process nothing needs telling, and the
            // remembered choice must survive to the next launch.
            if (_quitting)
                return;

            _tray?.SetOverlayVisible(false);
            _server?.Services.GetRequiredService<OverlayBridge>().Report(false);

            _settings = _settings with { ShowOverlay = false };
            _settings.Save();
        };

        return window;
    }

    private async Task StartServerAsync(string[] args)
    {
        try
        {
            _server = ServerHost.Build(args);
            await _server.StartAsync();

            // Let the dashboard's Settings page show and hide the overlay. The
            // callback arrives on a request thread; everything WPF happens on
            // the dispatcher.
            _server.Services.GetRequiredService<OverlayBridge>().Attach(
                _settings.ShowOverlay,
                visible => Dispatcher.Invoke(() => SetOverlayVisible(visible)));
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

    /// <summary>
    /// The last resort when detection fails: a folder dialog, because someone
    /// whose game is in an unusual place should not have to type a path or
    /// find a config file.
    /// </summary>
    private void PickInstallFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Pick your Star Citizen folder - the one holding Game.log, "
                + @"or its parent (usually ...\StarCitizen\LIVE).",
            UseDescriptionForTitle = true,
            SelectedPath = InstallPathStore.Load() ?? ""
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var install = InstallPathStore.Save(dialog.SelectedPath);

        _tray?.Notify(install is null
            ? "No Star Citizen logs in that folder. Look for the one holding Game.log."
            : $"Found {install.Channel}. Restarting Quantum Wake to read it…");

        // The install is resolved once at startup and held by everything
        // downstream, so the honest way to apply it is to start again.
        if (install is not null)
            Restart();
    }

    private void Restart()
    {
        var exe = Environment.ProcessPath;
        if (exe is not null)
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });

        Quit();
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

    /// <summary>
    /// Shows or hides the overlay, and remembers the choice. Reached from the
    /// tray menu and from the dashboard's Settings page alike, so it keeps every
    /// surface truthful: the tray checkbox and the bridge both learn of a
    /// change the other one made.
    /// </summary>
    private void SetOverlayVisible(bool visible)
    {
        if (visible)
            (_overlay ??= CreateOverlay()).Show();
        else
            _overlay?.Hide();

        _tray?.SetOverlayVisible(visible);
        _server?.Services.GetRequiredService<OverlayBridge>().Report(visible);

        _settings = _settings with { ShowOverlay = visible };
        _settings.Save();
    }

    private async void Quit()
    {
        _quitting = true;
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
