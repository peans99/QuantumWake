using System.Text.Json;

namespace Quantumwake.Overlay;

internal sealed record MfdMonitor(string Id, int X, int Y, int Width, int Height, bool Primary);
internal sealed record MfdPanel
{
    public string Id { get; init; } = "left";
    public string Monitor { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; } = 480;
    public int Height { get; init; } = 480;
    public int Cougar { get; init; } = 1;

    /// <summary>
    /// Where the button captions sit along each edge, in pixels in from that
    /// edge; null, or a null side, for the page's own default.
    /// </summary>
    /// <remarks>
    /// The window is the frame's opening; the bezel's buttons are wherever the
    /// bezel puts them, and on a monitor smaller than the frame that is nearer
    /// the edge than the page's 14% and 12%. Without this the only way to put
    /// the first caption under the first button was to drag the window out
    /// past the opening, which put the display under the bezel.
    /// </remarks>
    public MfdInset? Inset { get; init; }

    /// <summary>
    /// The size of the monitor this panel was saved on, so it can be found
    /// again when its name no longer points at it. Null in a file saved before
    /// 0.17.8.
    /// </summary>
    /// <remarks>
    /// <see cref="Monitor"/> is Windows' display name, <c>\\.\DISPLAY2</c>, and
    /// Windows renumbers those - after a driver update, a sleep, a cable
    /// replugged. On the machine this was found on, a layout saved on the
    /// 1366×768 Cougar screen as <c>DISPLAY1</c> woke up to find <c>DISPLAY1</c>
    /// was the 3440×1440 main monitor: the frames and their black backdrop went
    /// there, over the taskbar and the tray icon that was the only way into
    /// setup. A cockpit screen is rarely the same size as anything else on the
    /// desk, which makes the size the better half of its identity.
    /// </remarks>
    public int? MonitorWidth { get; init; }

    /// <inheritdoc cref="MonitorWidth"/>
    public int? MonitorHeight { get; init; }
}

internal sealed record MfdInset(int? Left, int? Top, int? Right, int? Bottom)
{
    /// <summary>Each side clamped to the panel, or dropped; the whole thing null when every side is.</summary>
    /// <remarks>Capped at 45% of the dimension so two rows can never cross, which is what mfd-core.js does too.</remarks>
    public MfdInset? Clean(int width, int height)
    {
        static int? Side(int? value, int size) => value is { } v ? Math.Clamp(v, 0, (int)Math.Floor(size * .45)) : null;
        var cleaned = new MfdInset(Side(Left, width), Side(Top, height), Side(Right, width), Side(Bottom, height));
        return cleaned is { Left: null, Top: null, Right: null, Bottom: null } ? null : cleaned;
    }
}

internal sealed record MfdLayout
{
    public bool Enabled { get; init; }

    /// <summary>
    /// Whether to fill the monitors carrying panels with black, around the
    /// openings. On unless the pilot turns it off.
    /// </summary>
    /// <remarks>
    /// On by default because it is what the frames are for: a bezel screwed
    /// over part of a monitor leaves the rest of that monitor glowing around
    /// its edge, and in a dark cockpit that washes out the instrument inside
    /// it. Off is for anyone who put a panel in the corner of a monitor they
    /// are still using, and would rather keep the desktop than the contrast.
    /// </remarks>
    public bool Blackout { get; init; } = true;

    /// <summary>
    /// How bright the rendered page is, and how large its text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In setup rather than on the frame. They are set once when the frames go
    /// on the monitor and then almost never touched, and doing it from the face
    /// cost four of the twenty buttons - a fifth of it - for two settings. The
    /// commands stay bindable for anyone who wants them under a thumb, and a
    /// rocker is their natural home once a cockpit run says which rocker
    /// reports which number.
    /// </para>
    /// <para>
    /// Shared by both frames, like the button map and for the same reason: one
    /// device, one cockpit. Which page each frame shows is the thing that
    /// genuinely differs, and the displays still remember that themselves.
    /// </para>
    /// </remarks>
    public double Brightness { get; init; } = 1;

    /// <summary>
    /// The four levels the setup slider and the frame's own brightness button
    /// both work in. A stored value between two of them snaps to the nearer:
    /// a 65% written by an earlier build is a place no control can now return
    /// to, and a setting the pilot cannot get back is worse than one that moved
    /// five percent once.
    /// </summary>
    public static readonly double[] BrightnessLevels = [.4, .6, .8, 1];

    private static double NearestLevel(double value)
    {
        if (!double.IsFinite(value)) return 1;
        // Rounded before comparing, and ties go to the brighter: .7 sits exactly
        // between two levels, and float noise deciding which way it fell would
        // make the same file open dimmer on one machine than another. Brighter,
        // because a frame too dark to read the way back out of is the worse end.
        var best = BrightnessLevels[0];
        foreach (var level in BrightnessLevels)
            if (Math.Round(Math.Abs(level - value), 9) <= Math.Round(Math.Abs(best - value), 9)) best = level;
        return best;
    }

    public double TextScale { get; init; } = 1;

    /// <summary>
    /// Minutes of no button press before the frames dim, or 0 to never dim.
    /// </summary>
    /// <remarks>
    /// Off by default, and a comfort setting rather than a protective one:
    /// these are LED panels, so nothing is burning in. It is for a lit cockpit
    /// at night, and it dims rather than blanks - a frame you can still glance
    /// at is worth more than one that has gone dark and has to be woken before
    /// it will answer.
    /// </remarks>
    public int SleepAfterMinutes { get; init; }

    public MfdPanel[] Panels { get; init; } = [];

    /// <summary>
    /// Cougar button number to command, or null for the shipped profile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by both frames rather than kept per panel: they are the same
    /// physical device, and the one thing that differs between them - which
    /// page each is showing - the displays already remember for themselves.
    /// </para>
    /// <para>
    /// A stored map is the whole answer, so a button the pilot cleared stays
    /// cleared. Only a missing map means "use the defaults", which is why this
    /// is nullable rather than an empty dictionary.
    /// </para>
    /// </remarks>
    public Dictionary<int, string>? Buttons { get; init; }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The Cougar's 20 optical buttons and its four two-way rockers.</summary>
    public const int ButtonCount = 28;

    public static MfdLayout Default(IReadOnlyList<MfdMonitor> monitors)
    {
        var m = monitors.FirstOrDefault(x => !x.Primary) ?? monitors.First();
        var size = Math.Min(480, Math.Min(m.Width / 2, m.Height));
        return new() { Panels = [
            new() { Id = "left", Monitor = m.Id, Width = size, Height = size, Cougar = 1 },
            new() { Id = "right", Monitor = m.Id, X = size, Width = size, Height = size, Cougar = 2 }
        ] };
    }

    public MfdLayout Validate(IReadOnlyList<MfdMonitor> monitors)
    {
        if (Panels is null || Panels.Length != 2 || Panels[0] is null || Panels[1] is null
            || Panels[0].Id != "left" || Panels[1].Id != "right")
            throw new ArgumentException("The layout needs a left and a right MFD.");
        if (Panels[0].Cougar == Panels[1].Cougar)
            throw new ArgumentException("Assign a different Cougar number to each MFD.");
        return this with {
            Buttons = CleanButtons(),
            // A slider cannot send a bad number, but a hand-edited file can.
            Brightness = NearestLevel(Brightness),
            TextScale = double.IsFinite(TextScale) ? Math.Clamp(TextScale, .8, 1.5) : 1,
            SleepAfterMinutes = Math.Clamp(SleepAfterMinutes, 0, 120),
            Panels = Panels.Select(p =>
        {
            if (p.Cougar is < 1 or > 8 || string.IsNullOrWhiteSpace(p.Monitor))
                throw new ArgumentException("Choose a monitor and a Cougar number for each MFD.");
            var m = Locate(p, monitors);
            // Retain a disconnected monitor's placement; never cover the game by moving it to primary.
            if (m is null) return p;
            var width = Math.Clamp(p.Width, Math.Min(220, m.Width), m.Width);
            var height = Math.Clamp(p.Height, Math.Min(220, m.Height), m.Height);
            return p with { Monitor = m.Id, Width = width, Height = height,
                X = Math.Clamp(p.X, 0, m.Width - width), Y = Math.Clamp(p.Y, 0, m.Height - height),
                Inset = p.Inset?.Clean(width, height) };
        }).ToArray() };
    }

    /// <summary>
    /// The monitor a panel belongs on, or null when it cannot be found.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name is trusted while it still points at a monitor of the size the
    /// panel was saved on. When it does not, the one monitor of that size is
    /// the panel's screen under a new name. When no monitor has that size, or
    /// several do, the answer is null and the panel stays off: guessing wrong
    /// puts an always-on-top black backdrop over the game, which is the failure
    /// this exists to stop.
    /// </para>
    /// <para>
    /// A file saved before sizes were recorded has only the name to go on, and
    /// gets it. Two monitors of the same size swapping names is invisible to
    /// this, and is the case where the name is still all there is.
    /// </para>
    /// </remarks>
    public static MfdMonitor? Locate(MfdPanel panel, IReadOnlyList<MfdMonitor> monitors)
    {
        var named = monitors.FirstOrDefault(m => m.Id == panel.Monitor);
        if (panel.MonitorWidth is not { } width || panel.MonitorHeight is not { } height) return named;
        if (named is not null && named.Width == width && named.Height == height) return named;

        var sized = monitors.Where(m => m.Width == width && m.Height == height).ToArray();
        return sized.Length == 1 ? sized[0] : null;
    }

    /// <summary>The layout with each panel remembering the size of the monitor it is on, as it is saved.</summary>
    public MfdLayout Stamped(IReadOnlyList<MfdMonitor> monitors) => this with
    {
        Panels = Panels.Select(p => monitors.FirstOrDefault(m => m.Id == p.Monitor) is { } m
            ? p with { MonitorWidth = m.Width, MonitorHeight = m.Height }
            : p).ToArray()
    };

    /// <summary>
    /// What to tell the pilot when the frames did not land where they were
    /// saved, or null when they did.
    /// </summary>
    /// <remarks>
    /// Said once, at start, through the tray. Every case names the shortcut,
    /// because the case that needs it most - frames on the wrong screen - is
    /// the one where the tray may be hard to reach.
    /// </remarks>
    public static string? Explain(MfdLayout saved, IReadOnlyList<MfdMonitor> monitors)
    {
        if (!saved.Enabled || saved.Panels is not { Length: > 0 } panels) return null;

        var placed = panels.Select(p => (Panel: p, Monitor: Locate(p, monitors))).ToArray();

        if (placed.FirstOrDefault(x => x.Monitor is null && x.Panel.MonitorWidth is not null) is { Panel: { } lost })
            return $"The MFD screen ({lost.MonitorWidth}×{lost.MonitorHeight}) was not found, so the MFDs are off. "
                + "Reconnect it, or open MFD setup (Ctrl+Alt+M) to place them again.";

        if (placed.Any(x => x.Monitor is not null && x.Monitor.Id != x.Panel.Monitor))
            return "Windows renumbered your displays since the MFD layout was saved. The MFDs found their "
                + "screen again by its size. If they are not behind the frames, open MFD setup (Ctrl+Alt+M).";

        // A file from before sizes were kept, pointing at the main monitor
        // while others exist: exactly what a renumbering does to one, and
        // nothing here can tell it apart from a deliberate choice. One save in
        // setup records the size and ends the question.
        if (monitors.Count > 1
            && placed.Any(x => x.Panel.MonitorWidth is null && x.Monitor is { Primary: true }))
            return "The MFDs are on your main screen. If your frames are on another one, Windows has renumbered "
                + "your displays: open MFD setup (Ctrl+Alt+M), place them and save once, and the layout will "
                + "find its screen by size from then on.";

        return null;
    }

    /// <summary>
    /// Whether a monitor may be blacked out behind the frames.
    /// </summary>
    /// <remarks>
    /// Never the main one. It holds the game and the taskbar, so a backdrop
    /// there is never what a cockpit wants - and it is the one place a backdrop
    /// hides the tray icon, the way into setup.
    /// </remarks>
    public static bool MayBlackOut(MfdMonitor monitor) => !monitor.Primary;

    /// <summary>
    /// The binding map with anything unusable dropped. Null stays null, which
    /// is the only thing that reads as "use the shipped profile".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A map that cleans down to nothing stays an empty map rather than
    /// becoming null: a pilot who unassigns every button gets a blank frame and
    /// can see every dropdown in setup saying so. Handing the defaults back
    /// instead would be the app quietly overruling them, and there would be
    /// nothing on screen to explain it.
    /// </para>
    /// <para>
    /// Shape only. What the command names mean is the display's business, and
    /// <c>QwMfd.buttons</c> already ignores one it does not know, so repeating
    /// the vocabulary here would only give it somewhere to drift: a command
    /// added to the page would be refused by a file this never heard about.
    /// </para>
    /// </remarks>
    private Dictionary<int, string>? CleanButtons() =>
        Buttons?
            .Where(b => b.Key is >= 1 and <= ButtonCount
                && !string.IsNullOrWhiteSpace(b.Value) && b.Value.Length <= 32)
            .ToDictionary(b => b.Key, b => b.Value);
}

internal sealed class CougarEdges
{
    private uint? _previous;
    public int[] Read(uint buttons)
    {
        // Seed with the current state after connection: a held button is not a new press.
        var pressed = _previous.HasValue ? buttons & ~_previous.Value : 0;
        _previous = buttons;
        return Enumerable.Range(1, 28).Where(n => (pressed & (1u << (n - 1))) != 0).ToArray();
    }
    public void Disconnect() => _previous = null;

    /// <summary>The HID button usages a report holds down, as the mask <see cref="Read"/> takes.</summary>
    public static uint Buttons(ReadOnlySpan<ushort> usages)
    {
        var mask = 0u;
        foreach (var usage in usages)
            if (usage is >= 1 and <= 32) mask |= 1u << (usage - 1);
        return mask;
    }
}
