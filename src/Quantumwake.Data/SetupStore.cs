using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// A named setup the pilot keeps (1.2.36): a Routes preset, a Stream Deck
/// choice. The settings themselves are the page's own shape, kept as given.
/// </summary>
/// <param name="Kind">Which page it belongs to: <see cref="SetupStore.RoutePreset"/> or <see cref="SetupStore.Deck"/>.</param>
/// <param name="Data">The page's settings, as JSON; the store keeps it and never reads into it.</param>
public sealed record SavedSetup(
    string Id,
    string Kind,
    string Name,
    JsonElement Data,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt = null) : IStamped<SavedSetup>
{
    public string StampId => Id;
    public SavedSetup Bare() => this with { ModifiedAt = null };
    public SavedSetup Stamped(DateTimeOffset at) => this with { ModifiedAt = at };

    /// <summary>When this last changed - see <see cref="Job.ChangedAt"/>.</summary>
    public DateTimeOffset ChangedAt => ModifiedAt ?? CreatedAt;
}

/// <summary>
/// The pilot's named setups, kept the way kits and builds are (1.2.36).
/// </summary>
/// <remarks>
/// <para>
/// Route presets lived in the browser's storage, so the overlay, which has a
/// WebView2 profile of its own, never saw one made in a browser, and no
/// backup carried them; Stream Deck choices were not kept at all and reset on
/// every reload. Kits and builds already had the right behaviour - their own
/// file, never touched by a rescan, in the backup - and a setup is the same
/// kind of thing: something typed, worth not losing.
/// </para>
/// <para>
/// One store for every page's setups rather than one per page: the pages
/// differ only in what the settings are, which is the page's business, and
/// the store holds them as given. A name is unique within its kind, so saving
/// under a name already used replaces that setup, as the presets always did.
/// </para>
/// </remarks>
public sealed class SetupStore
{
    public const string RoutePreset = "route-preset";
    public const string Deck = "deck";

    /// <summary>The kinds a page may keep; anything else is refused rather than stored for nobody.</summary>
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string>(StringComparer.Ordinal) { RoutePreset, Deck };

    /// <summary>Per kind: a pick list, not an archive.</summary>
    public const int MaxPerKind = 24;

    /// <summary>A setup is a handful of fields; anything bigger is not one.</summary>
    public const int MaxDataBytes = 16_384;

    private readonly string _path;
    private readonly Lock _gate = new();
    private readonly ChangeStamp<SavedSetup> _stamp = new(r => JsonSerializer.Serialize(r));
    private List<SavedSetup> _setups = [];

    public SetupStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "setups.json");
        Load();
    }

    public IReadOnlyList<SavedSetup> All()
    {
        lock (_gate) return [.. _setups];
    }

    public IReadOnlyList<SavedSetup> Of(string kind)
    {
        lock (_gate) return [.. _setups.Where(s => s.Kind == kind)];
    }

    /// <summary>
    /// Keeps a setup under a name, replacing the one of that kind already
    /// called that. Null for an unknown kind, an empty name, settings that
    /// are not a JSON object, or settings too big to be a setup.
    /// </summary>
    public SavedSetup? Save(string? kind, string? name, JsonElement data)
    {
        if (kind is null || !Kinds.Contains(kind)) return null;
        var clean = Sanitise.Clean(name, string.Empty, 60);
        if (clean.Length == 0 || data.ValueKind != JsonValueKind.Object) return null;
        if (data.GetRawText().Length > MaxDataBytes) return null;

        lock (_gate)
        {
            var at = _setups.FindIndex(s => s.Kind == kind && string.Equals(s.Name, clean, StringComparison.OrdinalIgnoreCase));
            SavedSetup setup;
            if (at >= 0)
            {
                setup = _setups[at] with { Name = clean, Data = data.Clone() };
                _setups[at] = setup;
            }
            else
            {
                if (_setups.Count(s => s.Kind == kind) >= MaxPerKind)
                {
                    // The oldest of the kind makes room, as the browser-kept
                    // presets did with their last twelve.
                    var oldest = _setups.Where(s => s.Kind == kind).OrderBy(s => s.ChangedAt).First();
                    _setups.Remove(oldest);
                }
                setup = new SavedSetup(Guid.NewGuid().ToString("N")[..10], kind, clean, data.Clone(), DateTimeOffset.UtcNow);
                _setups.Add(setup);
            }
            Save();
            return _setups.First(s => s.Id == setup.Id);
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            if (_setups.RemoveAll(s => s.Id == id) == 0) return false;
            Save();
            return true;
        }
    }

    /// <summary>
    /// Puts a record back exactly as given, replacing any with the same id.
    /// For restoring a backup only - see <see cref="MapNoteStore.Put"/>.
    /// </summary>
    public void Put(SavedSetup setup)
    {
        lock (_gate)
        {
            var index = _setups.FindIndex(s => s.Id == setup.Id);
            if (index >= 0) _setups[index] = setup;
            else _setups.Add(setup);
            _stamp.Adopt(setup);
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _setups = (JsonSerializer.Deserialize<List<SavedSetup>>(File.ReadAllText(_path)) ?? [])
                    .Where(s => !string.IsNullOrWhiteSpace(s.Id) && Kinds.Contains(s.Kind ?? ""))
                    .ToList();
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // Conveniences, not work: a damaged file leaves the pages with
            // their defaults rather than refusing to load.
            _setups = [];
        }

        _stamp.Loaded(_setups);
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _stamp.Apply(_setups, DateTimeOffset.UtcNow);
        File.WriteAllText(_path, JsonSerializer.Serialize(_setups));
    }
}
