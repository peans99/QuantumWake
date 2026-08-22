using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Quantumwake.Overlay;

/// <summary>
/// The notification-area icon, and the only visible way to control the app.
/// </summary>
/// <remarks>
/// <para>
/// Before this, the server ran as a hidden window and the overlay could be made
/// click-through: a user who wanted it to stop had no button to press and had to
/// find it in Task Manager. Everything that is not a global hotkey now hangs off
/// this menu.
/// </para>
/// <para>
/// <c>NotifyIcon</c> comes from Windows Forms, which WPF can host in the same
/// process. It costs one <c>UseWindowsForms</c> flag and no package: the
/// alternative is a NuGet dependency for a control Windows already ships.
/// </para>
/// </remarks>
internal sealed class TrayPresence : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _overlayItem;

    public event Action? OpenDashboardRequested;
    public event Action<bool>? OverlayToggled;
    public event Action? QuitRequested;

    /// <summary>The tray's answer to "it cannot find my game".</summary>
    public event Action? SetInstallFolderRequested;

    public TrayPresence(bool overlayVisible)
    {
        _overlayItem = new ToolStripMenuItem("Show overlay")
        {
            CheckOnClick = true,
            Checked = overlayVisible
        };

        _overlayItem.CheckedChanged += (_, _) => OverlayToggled?.Invoke(_overlayItem.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open dashboard", null,
            (_, _) => OpenDashboardRequested?.Invoke()));
        menu.Items.Add(_overlayItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Set Star Citizen folder…", null,
            (_, _) => SetInstallFolderRequested?.Invoke()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit", null, (_, _) => QuitRequested?.Invoke()));

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Quantum Wake",
            Visible = true,
            ContextMenuStrip = menu
        };

        // Double-click is the gesture people try first on a tray icon.
        _icon.DoubleClick += (_, _) => OpenDashboardRequested?.Invoke();
    }

    /// <summary>Reflects a change made elsewhere, such as the hotkey.</summary>
    public void SetOverlayVisible(bool visible)
    {
        if (_overlayItem.Checked != visible)
            _overlayItem.Checked = visible;
    }

    public void Notify(string message)
    {
        _icon.BalloonTipTitle = "Quantum Wake";
        _icon.BalloonTipText = message;
        _icon.ShowBalloonTip(4000);
    }

    private static Icon LoadIcon()
    {
        // Packaged beside the executable in an ordinary build and embedded in a
        // single-file one, so try the assembly first and fall back to disk.
        var stream = typeof(TrayPresence).Assembly
            .GetManifestResourceStream("Quantumwake.Overlay.app.ico");

        if (stream is not null)
        {
            using (stream)
                return new Icon(stream);
        }

        var path = Path.Combine(AppContext.BaseDirectory, "app.ico");
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
