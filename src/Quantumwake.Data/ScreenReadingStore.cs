using System.Text.Json;
using System.Text.Json.Serialization;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>One screenshot, read, and everything that was made of it.</summary>
/// <param name="Shot">The file, by name only. The name carries the moment it was taken.</param>
/// <param name="ShotAt">When it was taken, from the file rather than from when it was read.</param>
/// <param name="Summary">One line for a list: "Drake Corsair, 9 parts named" or "PYRO > DUDLEY &amp; DAUGHTERS".</param>
/// <param name="Item">The tooltip's item, on the frames that had one; the older scan shape, kept so the panel keeps working.</param>
/// <param name="Lines">
/// Every line the engine returned, kept verbatim. For a screen this app has no
/// reader for it is the whole point: the text of a kiosk or a contract list
/// sits here waiting for the reader to be written from it, which is how each
/// of the readers that exist was written.
/// </param>
public sealed record ScreenSighting(
    string Shot,
    DateTimeOffset ShotAt,
    ScreenKind Kind,
    string Summary,
    IReadOnlyList<ScreenCheck> Checks,
    ScreenScan? Item,
    LoadoutReading? Loadout,
    MapReading? Map,
    WalletReading? Wallet,
    IReadOnlyList<string> Lines,
    long TookMs,
    ContractsReading? Contracts = null,
    FleetReading? Fleet = null,
    ReputationReading? Reputation = null,
    KioskReading? Kiosk = null);

/// <summary>One <c>/showlocation</c> reading the pilot pasted.</summary>
/// <param name="At">When it was parsed, which for a paste is the only time there is.</param>
/// <param name="Believed">
/// Where the logs put the pilot at that moment, so the reading can be read
/// back later beside what the app thought. The reading itself names no place
/// and no system, which is the whole reason this field is here.
/// </param>
public sealed record ClipboardSighting(
    DateTimeOffset At,
    double X,
    double Y,
    double Z,
    double Gigametres,
    string? Believed,
    string? System);

/// <summary>
/// Remembers what the screenshots said, and what was pasted.
/// </summary>
/// <remarks>
/// <para>
/// Its own file beside the other authored data, newest first, and bounded:
/// a pilot who screenshots every kiosk does not need a year of them, and the
/// bound is what lets the whole thing be read into a page at once.
/// </para>
/// <para>
/// A reading is dated by its screenshot, never by when it was read. A shot
/// taken last week and read today says what was true last week, and the
/// fittings drawn from it are worded that way on the page.
/// </para>
/// </remarks>
public sealed class ScreenReadingStore
{
    /// <summary>Enough to hold a long session's worth and a few weeks of habit.</summary>
    public const int Keep = 300;

    private readonly string _path;
    private readonly string _clipboardPath;
    private readonly Lock _gate = new();
    private List<ScreenSighting> _sightings = [];
    private List<ClipboardSighting> _clipboard = [];

    public ScreenReadingStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "screen-readings.json");
        _clipboardPath = Path.Combine(directory ?? AppPaths.Root, "screen-clipboard.json");
        Load();
    }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<ScreenSighting> All()
    {
        lock (_gate) return [.. _sightings];
    }

    public ScreenSighting? Latest
    {
        get { lock (_gate) return _sightings.FirstOrDefault(); }
    }

    /// <summary>Every paste, newest first.</summary>
    public IReadOnlyList<ClipboardSighting> Clipboards()
    {
        lock (_gate) return [.. _clipboard];
    }

    public void AddClipboard(ClipboardSighting paste)
    {
        lock (_gate)
        {
            _clipboard.Insert(0, paste);

            if (_clipboard.Count > Keep)
                _clipboard.RemoveRange(Keep, _clipboard.Count - Keep);

            SaveClipboard();
        }
    }

    /// <summary>The newest Fleet Manager reading, for the fleet page.</summary>
    public ScreenSighting? LatestFleet()
    {
        lock (_gate) return _sightings.FirstOrDefault(s => s.Fleet is not null);
    }

    /// <summary>Whether this file has been read already, so a folder scan does not read it twice.</summary>
    public bool Has(string shot)
    {
        lock (_gate) return _sightings.Any(s => string.Equals(s.Shot, shot, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The last wallet figure that actually read, for the next to be checked against.</summary>
    public WalletBaseline? LastWallet()
    {
        lock (_gate)
        {
            return _sightings
                .Where(s => s.Wallet?.Balance is not null)
                .Select(s => new WalletBaseline(s.ShotAt, s.Wallet!.Balance!.Value))
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// The newest loadout read for each ship that read, so the fleet can show
    /// what it was last photographed carrying.
    /// </summary>
    public IReadOnlyList<ScreenSighting> LatestLoadouts()
    {
        lock (_gate)
        {
            return [.. _sightings
                .Where(s => s.Loadout?.Ship is not null)
                .GroupBy(s => s.Loadout!.Ship!, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(s => s.ShotAt).First())
                .OrderByDescending(s => s.ShotAt)];
        }
    }

    public void Add(ScreenSighting sighting)
    {
        lock (_gate)
        {
            _sightings.RemoveAll(s => string.Equals(s.Shot, sighting.Shot, StringComparison.OrdinalIgnoreCase));
            _sightings.Insert(0, sighting);

            if (_sightings.Count > Keep)
                _sightings.RemoveRange(Keep, _sightings.Count - Keep);

            Save();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _sightings.Clear();
            _clipboard.Clear();
            Save();
            SaveClipboard();
        }
    }

    private void Load()
    {
        LoadSightings();
        LoadClipboard();
    }

    private void LoadSightings()
    {
        try
        {
            if (!File.Exists(_path)) return;

            _sightings = JsonSerializer.Deserialize<List<ScreenSighting>>(File.ReadAllText(_path), Json) ?? [];
            _sightings.Sort((a, b) => b.ShotAt.CompareTo(a.ShotAt));
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // A file that will not read is a file that gets rewritten by the
            // next screenshot. Losing the history beats refusing to start.
            _sightings = [];
        }
    }

    /// <summary>
    /// The pastes, from their own file.
    /// </summary>
    /// <remarks>
    /// Its own file and its own method. A paste and a screenshot have nothing
    /// in common but the panel they end up on, and neither an unreadable file
    /// nor an absent one should take the other with it - which is exactly what
    /// happened when this shared a method and an early return with the
    /// screenshots.
    /// </remarks>
    private void LoadClipboard()
    {
        try
        {
            if (!File.Exists(_clipboardPath)) return;

            _clipboard = JsonSerializer.Deserialize<List<ClipboardSighting>>(File.ReadAllText(_clipboardPath), Json) ?? [];
            _clipboard.Sort((a, b) => b.At.CompareTo(a.At));
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _clipboard = [];
        }
    }

    private void SaveClipboard()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_clipboardPath)!);
            File.WriteAllText(_clipboardPath, JsonSerializer.Serialize(_clipboard, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Kept in memory for the session; the next paste tries again.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_sightings, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Kept in memory for the session; the next add tries again.
        }
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
