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
            var m = monitors.FirstOrDefault(m => m.Id == p.Monitor);
            // Retain a disconnected monitor's placement; never cover the game by moving it to primary.
            if (m is null) return p;
            var width = Math.Clamp(p.Width, Math.Min(220, m.Width), m.Width);
            var height = Math.Clamp(p.Height, Math.Min(220, m.Height), m.Height);
            return p with { Width = width, Height = height,
                X = Math.Clamp(p.X, 0, m.Width - width), Y = Math.Clamp(p.Y, 0, m.Height - height) };
        }).ToArray() };
    }

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
}
