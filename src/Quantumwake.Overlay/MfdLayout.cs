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
        return this with { Buttons = CleanButtons(), Panels = Panels.Select(p =>
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
