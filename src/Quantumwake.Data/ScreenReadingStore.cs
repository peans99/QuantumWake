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
/// <param name="Dismissed">
/// Set by the pilot, from the log. A reading that misread - a frame of
/// somebody else's terminal, a balance with a digit dropped - stays in the
/// log so the file is not read a second time and so the misreading can be
/// seen, but nothing believes it any more: not the wallet, not the fleet,
/// not the fittings, not the Now card.
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
    KioskReading? Kiosk = null,
    bool Dismissed = false);

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
    string? System,
    int TimesSeen = 1,
    DateTimeOffset? LastSeenAt = null);

/// <summary>A copied location the pilot chose to keep after its log entry is gone.</summary>
/// <param name="Note">
/// Why it was worth keeping, in the pilot's words. The coordinates say where;
/// nothing in the logs says why anyone was there, and a point without the
/// reason is a number that means nothing a month later.
/// </param>
/// <param name="ModifiedAt">
/// When the name, category or note last changed; null until they have. The
/// backup compares on it, the way it does for a job or a kit.
/// </param>
public sealed record PinnedLocation(
    DateTimeOffset SourceAt,
    DateTimeOffset PinnedAt,
    double X,
    double Y,
    double Z,
    double Gigametres,
    string? Believed,
    string? System,
    string? Label = null,
    string? Category = null,
    string? Note = null,
    DateTimeOffset? ModifiedAt = null) : IStamped<PinnedLocation>
{
    /// <summary>The copy it came from, to the tick - the only identity a point has.</summary>
    public string StampId => IdFor(SourceAt);

    public static string IdFor(DateTimeOffset sourceAt) => sourceAt.ToUniversalTime().ToString("o");

    public PinnedLocation Bare() => this with { ModifiedAt = null };
    public PinnedLocation Stamped(DateTimeOffset at) => this with { ModifiedAt = at };

    /// <summary>When this last changed - see <see cref="Job.ChangedAt"/>.</summary>
    public DateTimeOffset ChangedAt => ModifiedAt ?? PinnedAt;
}

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
    private readonly string _pinsPath;
    private readonly Lock _gate = new();
    private List<ScreenSighting> _sightings = [];
    private List<ClipboardSighting> _clipboard = [];
    private List<PinnedLocation> _pins = [];

    public ScreenReadingStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "screen-readings.json");
        _clipboardPath = Path.Combine(directory ?? AppPaths.Root, "screen-clipboard.json");
        _pinsPath = Path.Combine(directory ?? AppPaths.Root, "pinned-locations.json");
        Load();
    }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<ScreenSighting> All()
    {
        lock (_gate) return [.. _sightings];
    }

    /// <summary>The newest reading the pilot has not dismissed.</summary>
    public ScreenSighting? Latest
    {
        get { lock (_gate) return _sightings.FirstOrDefault(s => !s.Dismissed); }
    }

    /// <summary>Every paste, newest first.</summary>
    public IReadOnlyList<ClipboardSighting> Clipboards()
    {
        lock (_gate) return [.. _clipboard];
    }

    /// <summary>Locations deliberately kept by the pilot, newest pin first.</summary>
    public IReadOnlyList<PinnedLocation> Pinned()
    {
        lock (_gate) return [.. _pins];
    }

    /// <summary>
    /// Promotes one copied location into a durable point of interest.
    /// </summary>
    /// <remarks>
    /// The paste moment is its identity. Coordinates may repeat when a pilot
    /// checks the same spot twice, but a single copied reading should never
    /// grow a second pin because the dashboard was clicked twice.
    /// </remarks>
    public PinnedLocation? Pin(DateTimeOffset sourceAt, string? label = null, string? category = null)
    {
        lock (_gate)
        {
            var existing = _pins.FirstOrDefault(p => p.SourceAt == sourceAt);
            if (existing is not null) return existing;

            var paste = _clipboard.FirstOrDefault(p => p.At == sourceAt);
            if (paste is null) return null;

            var labelForPin = CleanLabel(label)
                ?? string.Join(" > ", new[] { paste.System, paste.Believed }.Where(v => !string.IsNullOrWhiteSpace(v)));
            if (string.IsNullOrWhiteSpace(labelForPin)) labelForPin = "Copied location";

            var pin = new PinnedLocation(paste.At, DateTimeOffset.UtcNow,
                paste.X, paste.Y, paste.Z, paste.Gigametres, paste.Believed, paste.System,
                labelForPin, CleanCategory(category) ?? "General");
            _pins.Insert(0, pin);
            SavePins();
            return pin;
        }
    }

    /// <summary>Forgets a point of interest without rewriting its source reading.</summary>
    public bool Unpin(DateTimeOffset sourceAt) => Unpin(PinnedLocation.IdFor(sourceAt));

    /// <summary>The same, by the id a backup carries it under.</summary>
    public bool Unpin(string stampId)
    {
        lock (_gate)
        {
            if (_pins.RemoveAll(p => p.StampId == stampId) == 0) return false;
            SavePins();
            return true;
        }
    }

    /// <summary>Updates the pilot's own label, category and note without moving the point.</summary>
    /// <remarks>
    /// A blank label or category keeps what was there - a name can only be
    /// replaced, never emptied. A blank note clears it, because a note can be
    /// wrong and deleting a wrong reason is a thing the pilot will want; a
    /// null note leaves it alone, so a caller that only names the point does
    /// not lose the note beside it.
    /// </remarks>
    public PinnedLocation? UpdatePin(DateTimeOffset sourceAt, string? label, string? category, string? note = null)
    {
        lock (_gate)
        {
            var at = _pins.FindIndex(p => p.SourceAt == sourceAt);
            if (at < 0) return null;

            var updated = _pins[at] with
            {
                Label = CleanLabel(label) ?? _pins[at].Label ?? "Copied location",
                Category = CleanCategory(category) ?? _pins[at].Category ?? "General",
                Note = note is null ? _pins[at].Note : CleanNote(note),
                ModifiedAt = DateTimeOffset.UtcNow,
            };
            _pins[at] = updated;
            SavePins();
            return updated;
        }
    }

    /// <summary>
    /// Puts a point back exactly as a backup carried it, replacing whatever
    /// sits on that copy. The restore is the only caller: everything else
    /// goes through <see cref="Pin"/>, which needs the paste it came from.
    /// </summary>
    public void PutPin(PinnedLocation pin)
    {
        lock (_gate)
        {
            _pins.RemoveAll(p => p.SourceAt == pin.SourceAt);
            _pins.Add(pin);
            _pins.Sort((a, b) => b.PinnedAt.CompareTo(a.PinnedAt));
            SavePins();
        }
    }

    public void AddClipboard(ClipboardSighting paste, bool mergeWithLatest = false)
    {
        lock (_gate)
        {
            // Clipboard watch deliberately asks every few seconds. The same
            // /showlocation text must refresh its "last seen" time rather
            // than consume the whole log while the pilot is still standing.
            if (mergeWithLatest && _clipboard.FirstOrDefault() is { } latest
                && latest.X == paste.X && latest.Y == paste.Y && latest.Z == paste.Z)
            {
                _clipboard[0] = latest with
                {
                    TimesSeen = Math.Max(1, latest.TimesSeen) + 1,
                    LastSeenAt = paste.At,
                    Believed = paste.Believed ?? latest.Believed,
                    System = paste.System ?? latest.System,
                };
                SaveClipboard();
                return;
            }

            _clipboard.Insert(0, paste);

            if (_clipboard.Count > Keep)
                _clipboard.RemoveRange(Keep, _clipboard.Count - Keep);

            SaveClipboard();
        }
    }

    /// <summary>The newest Fleet Manager reading, for the fleet page.</summary>
    public ScreenSighting? LatestFleet()
    {
        lock (_gate) return _sightings.FirstOrDefault(s => !s.Dismissed && s.Fleet is not null);
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
                .Where(s => !s.Dismissed)
                .Where(s => s.Wallet?.Balance is not null)
                .Select(s => new WalletBaseline(s.ShotAt, s.Wallet!.Balance!.Value, s.Shot))
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
                .Where(s => !s.Dismissed)
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

    /// <summary>
    /// Marks a reading as not to be believed, or believes it again. The
    /// reading itself is kept as read; only whether it counts changes.
    /// </summary>
    /// <returns>The reading as it now stands, or null when no such shot is in the log.</returns>
    public ScreenSighting? Dismiss(string shot, bool dismissed)
    {
        lock (_gate)
        {
            var at = _sightings.FindIndex(s => string.Equals(s.Shot, shot, StringComparison.OrdinalIgnoreCase));
            if (at < 0) return null;

            _sightings[at] = _sightings[at] with { Dismissed = dismissed };
            Save();
            return _sightings[at];
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
        LoadPins();
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

    private void LoadPins()
    {
        try
        {
            if (!File.Exists(_pinsPath)) return;

            _pins = JsonSerializer.Deserialize<List<PinnedLocation>>(File.ReadAllText(_pinsPath), Json) ?? [];
            _pins.Sort((a, b) => b.PinnedAt.CompareTo(a.PinnedAt));
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _pins = [];
        }
    }

    private void SavePins()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_pinsPath)!);
            File.WriteAllText(_pinsPath, JsonSerializer.Serialize(_pins, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Kept in memory for the session; the next pin tries again.
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

    private static string? CleanLabel(string? label) => string.IsNullOrWhiteSpace(label) ? null : label.Trim();

    private static string? CleanCategory(string? category) => string.IsNullOrWhiteSpace(category) ? null : category.Trim();

    /// <summary>Longest a note gets - a paragraph or two, not a pasted log.</summary>
    public const int NoteLength = 2000;

    private static string? CleanNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;

        var trimmed = note.Replace("\r\n", "\n").Trim();
        return trimmed.Length <= NoteLength ? trimmed : trimmed[..NoteLength];
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
