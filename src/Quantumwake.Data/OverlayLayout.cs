using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// What the overlay widget shows: which views it offers, which Now cards it
/// carries, and how tightly it draws them.
/// </summary>
/// <param name="Density">"normal", "compact" or "tiny" - a type scale.</param>
public sealed record OverlayLayout(
    IReadOnlyList<string> Tabs,
    IReadOnlyList<string> Cards,
    string Density)
{
    /// <summary>The views worth having over a game, and the order they sit in.</summary>
    public static readonly IReadOnlyList<string> SelectableTabs =
        ["now", "jobs", "map", "commodities", "market", "loadout", "stash", "logbook", "fleet", "places"];

    /// <summary>The Now page's cards, by their data-card name.</summary>
    public static readonly IReadOnlyList<string> SelectableCards =
        ["location", "ship", "session", "handle", "feed", "stats", "job", "trade"];

    public static OverlayLayout Default => new(
        ["now", "map", "commodities", "market", "loadout", "stash"],
        [.. SelectableCards],
        "normal");
}

/// <summary>
/// Stores the overlay's layout where both halves of the app can see it.
/// </summary>
/// <remarks>
/// It has to be server-side rather than in browser storage: the overlay runs
/// in its own WebView2 profile, so a choice made in the user's browser would
/// never reach the widget it was made for.
/// </remarks>
public sealed class OverlayLayoutStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private OverlayLayout _layout = OverlayLayout.Default;

    public OverlayLayoutStore(string? directory = null)
    {
        var folder = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Quantumwake");

        _path = Path.Combine(folder, "overlay-layout.json");

        try
        {
            if (File.Exists(_path))
                _layout = JsonSerializer.Deserialize<OverlayLayout>(File.ReadAllText(_path))
                    ?? OverlayLayout.Default;
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _layout = OverlayLayout.Default;
        }
    }

    public OverlayLayout Current
    {
        get { lock (_gate) return _layout; }
    }

    /// <summary>
    /// Bumped when someone asks a running widget to reload itself. The overlay
    /// polls this alongside the layout, so anything a page load would pick up -
    /// a newly enabled dataset, a fresh build - can be pushed without hunting
    /// for the window. In memory only: a server restart is itself a change
    /// worth reloading for.
    /// </summary>
    public long ReloadToken { get; private set; }

    public long RequestReload()
    {
        lock (_gate)
            return ++ReloadToken;
    }

    /// <summary>
    /// Saves a layout, keeping only names the app knows and never leaving the
    /// widget with no tabs at all - an overlay you cannot navigate is worse
    /// than one showing too much.
    /// </summary>
    public OverlayLayout Save(OverlayLayout layout)
    {
        var tabs = layout.Tabs
            .Where(OverlayLayout.SelectableTabs.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (tabs.Count == 0)
            tabs = ["now"];

        var cards = layout.Cards
            .Where(OverlayLayout.SelectableCards.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var density = layout.Density is "compact" or "tiny" ? layout.Density : "normal";
        var cleaned = new OverlayLayout(tabs, cards, density);

        lock (_gate)
        {
            _layout = cleaned;
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(cleaned));
        }

        return cleaned;
    }
}
