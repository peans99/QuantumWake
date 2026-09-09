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
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

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
        return this with { Panels = Panels.Select(p =>
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
