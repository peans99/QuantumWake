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
    private readonly ToolStripMenuItem _pinItem;
    private readonly ToolStripMenuItem _updateItem;

    /// <summary>What the current balloon should do if it is clicked, if anything.</summary>
    private Action? _balloonAction;

    public event Action? OpenDashboardRequested;
    public event Action<bool>? OverlayToggled;

    /// <summary>
    /// Pin the overlay out of the way, or take it back.
    /// </summary>
    /// <remarks>
    /// The only control that works in both directions: a pinned overlay passes
    /// every click to the game, so it cannot carry its own way back.
    /// </remarks>
    public event Action<bool>? OverlayPinned;

    public event Action? QuitRequested;

    /// <summary>The tray's answer to "it cannot find my game".</summary>
    public event Action? SetInstallFolderRequested;

    /// <summary>
    /// Asked for from here as well as from the dashboard, because the dashboard
    /// is a browser tab somebody has to go and open first - and the answer to
    /// "is there a new one?" is worth less the more work it takes to ask.
    /// </summary>
    public event Action? CheckForUpdatesRequested;

    public TrayPresence(bool overlayVisible)
    {
        _overlayItem = new ToolStripMenuItem("Show overlay")
        {
            CheckOnClick = true,
            Checked = overlayVisible
        };

        _overlayItem.CheckedChanged += (_, _) => OverlayToggled?.Invoke(_overlayItem.Checked);

        _pinItem = new ToolStripMenuItem("Pin overlay (clicks reach the game)")
        {
            CheckOnClick = true,
            Enabled = overlayVisible
        };

        _pinItem.CheckedChanged += (_, _) => OverlayPinned?.Invoke(_pinItem.Checked);

        _updateItem = new ToolStripMenuItem("Check for updates", null,
            (_, _) => CheckForUpdatesRequested?.Invoke());

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open dashboard", null,
            (_, _) => OpenDashboardRequested?.Invoke()));
        menu.Items.Add(_overlayItem);
        menu.Items.Add(_pinItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Set Star Citizen folder…", null,
            (_, _) => SetInstallFolderRequested?.Invoke()));
        menu.Items.Add(_updateItem);
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

        // One subscription for the life of the icon; which balloon is being
        // answered is a field, because subscribing per balloon would stack
        // handlers and open a window for every check ever run.
        _icon.BalloonTipClicked += (_, _) =>
        {
            var act = _balloonAction;
            _balloonAction = null;
            act?.Invoke();
        };
    }

    /// <summary>Reflects a change made elsewhere, such as the hotkey.</summary>
    public void SetOverlayVisible(bool visible)
    {
        if (_overlayItem.Checked != visible)
            _overlayItem.Checked = visible;
    }

    /// <summary>
    /// Reflects a pin that happened elsewhere - the header button, or the hotkey.
    /// </summary>
    /// <remarks>
    /// The tick has to match the screen. This is the control someone reaches for
    /// when the overlay has stopped answering the mouse, and a tick that
    /// disagrees with what they are looking at is worse than no tick at all.
    /// </remarks>
    public void SetOverlayPinned(bool pinned)
    {
        if (_pinItem.Checked != pinned)
            _pinItem.Checked = pinned;
    }

    /// <summary>Pinning means nothing while the overlay is not on screen.</summary>
    public void SetPinAvailable(bool available) => _pinItem.Enabled = available;

    /// <summary>
    /// A balloon, optionally worth clicking.
    /// </summary>
    /// <param name="onClick">
    /// What a click on the balloon should do. Windows gives no way to say a
    /// notification is clickable, so only pass one where the message itself
    /// says what clicking would do.
    /// </param>
    public void Notify(string message, Action? onClick = null)
    {
        _balloonAction = onClick;
        _icon.BalloonTipTitle = "Quantum Wake";
        _icon.BalloonTipText = message;
        _icon.ShowBalloonTip(4000);
    }

    /// <summary>
    /// Greys the item out while a check is in flight. A menu that still offers
    /// the thing it is already doing gets pressed again, and two checks racing
    /// would report twice.
    /// </summary>
    public void SetCheckingForUpdates(bool checking)
    {
        _updateItem.Enabled = !checking;
        _updateItem.Text = checking ? "Checking…" : "Check for updates";
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
