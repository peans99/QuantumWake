using SCCompanion.Core.Logging;
using SCCompanion.Core.State;

namespace SCCompanion.Data;

/// <summary>Progress from a library scan.</summary>
public sealed record ScanProgress(int Done, int Total, string CurrentFile, bool WasCached);

/// <summary>Totals across every stored session.</summary>
public sealed record LibraryStats
{
    public required int Sessions { get; init; }
    public required TimeSpan TotalTime { get; init; }
    public required TimeSpan InGameTime { get; init; }
    public required TimeSpan MenuTime { get; init; }
    public required DateTimeOffset? FirstSession { get; init; }
    public required DateTimeOffset? LastSession { get; init; }
    public required int Incapacitations { get; init; }
    public required int Disconnects { get; init; }
    public required int Kills { get; init; }

    public IReadOnlyList<ShipTotal> Ships { get; init; } = [];
    public IReadOnlyList<PlaceTotal> Locations { get; init; } = [];
    public IReadOnlyList<PlaceTotal> Destinations { get; init; } = [];
    public IReadOnlyList<FacetTotal> ContractIssuers { get; init; } = [];
    public IReadOnlyList<FacetTotal> ContractTypes { get; init; } = [];
}

/// <param name="Sorties">Flights - the reliable metric.</param>
/// <param name="EstimatedTime">Inferred time aboard; see <see cref="ShipUsage"/>.</param>
public sealed record ShipTotal(string Name, TimeSpan EstimatedTime, int Sorties, int Sessions);
public sealed record PlaceTotal(string RawId, string Name, string? System, string? Body, string Kind, int Visits);
public sealed record FacetTotal(string Name, int Count);

/// <summary>
/// Scans a Star Citizen install, keeps parsed sessions cached, and answers
/// aggregate questions about them.
/// </summary>
/// <remarks>
/// Rotated backups never change once written, so a file already ingested at the
/// same fingerprint is skipped entirely. Only the live Game.log is re-read on
/// each scan. That turns a ~30 s cold backfill into a near-instant warm start.
/// </remarks>
public sealed class LogLibrary : IDisposable
{
    private readonly SessionStore _store;
    private readonly bool _ownsStore;

    public LogLibrary(SessionStore store, bool ownsStore = false)
    {
        _store = store;
        _ownsStore = ownsStore;
    }

    public LogLibrary(string? databasePath = null)
        : this(new SessionStore(databasePath ?? SessionStore.DefaultDatabasePath), ownsStore: true)
    {
    }

    public SessionStore Store => _store;

    /// <summary>
    /// Ingests every log file for an install, skipping unchanged ones.
    /// </summary>
    /// <returns>How many files were parsed (as opposed to served from cache).</returns>
    public int Scan(GameInstall install, IProgress<ScanProgress>? progress = null, bool force = false)
    {
        var files = new List<string>(install.BackupLogs());
        if (install.HasGameLog)
            files.Add(install.GameLogPath);

        var parsed = 0;
        var pending = new List<(SessionSummary, string)>();

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var info = new FileInfo(file);
            if (!info.Exists)
                continue;

            var fingerprint = SessionStore.Fingerprint(info);
            var cached = !force && _store.IsCurrent(file, fingerprint);

            progress?.Report(new ScanProgress(i + 1, files.Count, Path.GetFileName(file), cached));

            if (cached)
                continue;

            pending.Add((BuildSession(file), fingerprint));
            parsed++;

            // Commit in batches so a long scan is not one giant transaction.
            if (pending.Count >= 25)
            {
                _store.SaveAll(pending);
                pending.Clear();
            }
        }

        if (pending.Count > 0)
            _store.SaveAll(pending);

        return parsed;
    }

    /// <summary>Parses one log file into a summary.</summary>
    public static SessionSummary BuildSession(string path)
    {
        var builder = new SessionBuilder(path);

        foreach (var ev in LogFileReader.ReadEvents(path))
            builder.Add(ev);

        return builder.Build();
    }

    public IReadOnlyList<SessionSummary> Sessions() => _store.All();

    public SessionSummary? Session(string id) => _store.Get(id);

    /// <summary>Rolls every stored session up into library-wide totals.</summary>
    public LibraryStats Stats()
    {
        var sessions = _store.All();

        if (sessions.Count == 0)
        {
            return new LibraryStats
            {
                Sessions = 0,
                TotalTime = TimeSpan.Zero,
                InGameTime = TimeSpan.Zero,
                MenuTime = TimeSpan.Zero,
                FirstSession = null,
                LastSession = null,
                Incapacitations = 0,
                Disconnects = 0,
                Kills = 0
            };
        }

        var ships = sessions
            .SelectMany(s => s.Ships.Select(ship => (Session: s.Id, Ship: ship)))
            .GroupBy(x => x.Ship.DisplayName, StringComparer.Ordinal)
            .Select(g => new ShipTotal(
                g.Key,
                TimeSpan.FromTicks(g.Sum(x => x.Ship.EstimatedTime.Ticks)),
                g.Sum(x => x.Ship.Sorties),
                g.Select(x => x.Session).Distinct().Count()))
            .OrderByDescending(s => s.Sorties)
            .ThenByDescending(s => s.EstimatedTime)
            .ToList();

        var locations = sessions
            .SelectMany(s => s.Locations)
            .GroupBy(l => l.RawId, StringComparer.Ordinal)
            .Select(g => new PlaceTotal(
                g.Key,
                g.First().DisplayName,
                g.First().System,
                g.First().Body,
                g.First().Kind.ToString(),
                g.Count()))
            .OrderByDescending(l => l.Visits)
            .ToList();

        var destinations = sessions
            .SelectMany(s => s.Jumps)
            .GroupBy(j => j.ToId, StringComparer.Ordinal)
            .Select(g => new PlaceTotal(g.Key, g.First().ToName, null, null, "Destination", g.Count()))
            .OrderByDescending(d => d.Visits)
            .ToList();

        var contracts = sessions.SelectMany(s => s.Contracts).ToList();

        return new LibraryStats
        {
            Sessions = sessions.Count,
            TotalTime = TimeSpan.FromTicks(sessions.Sum(s => s.Duration.Ticks)),
            InGameTime = TimeSpan.FromTicks(sessions.Sum(s => s.InGameDuration.Ticks)),
            MenuTime = TimeSpan.FromTicks(sessions.Sum(s => s.MenuDuration.Ticks)),
            FirstSession = sessions.Min(s => s.StartedAt),
            LastSession = sessions.Max(s => s.EndedAt),
            Incapacitations = sessions.Sum(s => s.Incapacitations),
            Disconnects = sessions.Sum(s => s.Disconnects),
            Kills = sessions.Sum(s => s.Kills),
            Ships = ships,
            Locations = locations,
            Destinations = destinations,
            ContractIssuers = Facet(contracts.Select(c => c.Issuer)),
            ContractTypes = Facet(contracts.Select(c => c.Type))
        };
    }

    private static List<FacetTotal> Facet(IEnumerable<string?> values) =>
        [.. values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FacetTotal(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)];

    public void Dispose()
    {
        if (_ownsStore)
            _store.Dispose();
    }
}
