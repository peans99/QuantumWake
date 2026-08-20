using System.Diagnostics;
using SCCompanion.Core.Events;
using SCCompanion.Core.Logging;
using SCCompanion.Core.Parsing;

// SC Companion CLI - backfill and verification harness.
//
// Phase 1 has no UI by design: the parser must be proven against real logs
// before anything is built on top of it. This tool ingests an install's log
// backups and reports what it found, so the numbers can be checked against the
// ground truth recorded in docs/findings.md.

var pathArg = GetOption(args, "--path");
var install = pathArg is not null
    ? GameInstallLocator.FromPath(pathArg)
    : GameInstallLocator.Preferred();

if (install is null)
{
    Console.Error.WriteLine("No Star Citizen install found. Pass --path <StarCitizen\\LIVE>.");
    return 1;
}

var liveOnly = args.Contains("--live-only");

Console.WriteLine("SC Companion CLI  ·  by nekron");
Console.WriteLine();
Console.WriteLine($"Install : {install.RootPath}");
Console.WriteLine($"Channel : {install.Channel}");

var files = new List<string>();
if (!liveOnly)
    files.AddRange(install.BackupLogs());
if (install.HasGameLog)
    files.Add(install.GameLogPath);

if (files.Count == 0)
{
    Console.Error.WriteLine("No log files found.");
    return 1;
}

var totalBytes = files.Sum(f => new FileInfo(f).Length);
Console.WriteLine($"Files   : {files.Count} ({totalBytes / 1024.0 / 1024.0:F1} MB)");
Console.WriteLine();

var report = new Report();
var parser = new LogEventParser();
var stopwatch = Stopwatch.StartNew();

for (var i = 0; i < files.Count; i++)
{
    var file = files[i];
    Console.Write($"\r  parsing {i + 1}/{files.Count} ...");

    // A fresh parser per file: session headers are per-file state, and a
    // truncated final line in one log must not leak into the next.
    var fileParser = new LogEventParser();
    report.BeginFile(Path.GetFileName(file));

    foreach (var ev in LogFileReader.ReadEvents(file, fileParser))
        report.Add(ev);

    report.Merge(fileParser);
}

stopwatch.Stop();
Console.Write("\r".PadRight(40));
Console.WriteLine($"\rParsed in {stopwatch.Elapsed.TotalSeconds:F1}s\n");

report.Print();
return 0;

static string? GetOption(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

/// <summary>Aggregates parsed events into the figures worth eyeballing.</summary>
internal sealed class Report
{
    private readonly Dictionary<string, int> _locations = [];
    private readonly Dictionary<string, int> _ships = [];
    private readonly Dictionary<string, int> _gameRules = [];
    private readonly Dictionary<string, int> _contracts = [];
    private readonly Dictionary<string, int> _quantumDestinations = [];
    private readonly Dictionary<string, int> _eventKinds = [];
    private readonly HashSet<string> _handles = [];
    private readonly HashSet<string> _geids = [];
    private readonly HashSet<string> _notificationIds = [];
    private readonly HashSet<string> _incapacitationFiles = [];
    private readonly HashSet<string> _sessionIds = [];
    private readonly Dictionary<string, (int Count, string Sample)> _unmatchedByTag = [];

    private string _currentFile = "";
    private int _sessionHeaders;
    private int _incapacitations;
    private int _unmatchedKnownTags;

    public void BeginFile(string fileName) => _currentFile = fileName;

    public void Add(GameEvent ev)
    {
        _eventKinds[ev.Kind] = _eventKinds.GetValueOrDefault(ev.Kind) + 1;

        switch (ev)
        {
            case SessionStartEvent:
                _sessionHeaders++;
                break;

            case LoginEvent login:
                _handles.Add(login.Handle);
                break;

            case CharacterEvent character:
                _geids.Add($"{character.Name} ({character.Geid})");
                break;

            case LoadingScreenEvent loading:
                Bump(_gameRules, loading.GameRules);
                break;

            case ContextEvent context:
                _sessionIds.Add(context.SessionId);
                break;

            case LocationInventoryEvent location:
                Bump(_locations, location.LocationId);
                break;

            // Count each ship once per seat entry/exit pair boundary; ClearDriver
            // is the reliably present half on current logs.
            case VehicleControlEvent vehicle:
                Bump(_ships, vehicle.Manufacturer is null
                    ? vehicle.Model
                    : $"{vehicle.Manufacturer} {vehicle.Model}");
                break;

            case QuantumRouteEvent quantum:
                Bump(_quantumDestinations, quantum.Destination);
                break;

            case ContractEvent contract:
                Bump(_contracts, contract.Contract);
                break;

            // Deduplicate on the notification id: each fires 3-5 times.
            case NotificationEvent notification:
                if (!_notificationIds.Add($"{_currentFile}|{notification.NotificationId}|{notification.Text}"))
                    break;

                if (notification.IsIncapacitation)
                {
                    _incapacitations++;
                    _incapacitationFiles.Add(_currentFile);
                }
                break;
        }
    }

    public void Merge(LogEventParser parser)
    {
        _unmatchedKnownTags += parser.UnmatchedKnownTags;

        foreach (var (tag, (count, sample)) in parser.UnmatchedByTag)
        {
            if (_unmatchedByTag.TryGetValue(tag, out var existing))
                _unmatchedByTag[tag] = (existing.Count + count, existing.Sample);
            else
                _unmatchedByTag[tag] = (count, sample);
        }
    }

    public void Print()
    {
        Section("Identity");
        Console.WriteLine($"  handles : {Join(_handles)}");
        Console.WriteLine($"  chars   : {Join(_geids)}");

        Section("Sessions");
        Console.WriteLine($"  session headers : {_sessionHeaders}");
        Console.WriteLine($"  shard sessions  : {_sessionIds.Count}");
        foreach (var (rules, count) in _gameRules.OrderByDescending(p => p.Value))
            Console.WriteLine($"  {rules,-16} {count,6} loading screens");

        Top("Locations visited", _locations);
        Top("Ships flown", _ships, 10);
        Top("Quantum destinations", _quantumDestinations);
        Top("Contracts", _contracts);

        Section("Combat");
        Console.WriteLine($"  incapacitations   : {_incapacitations} across {_incapacitationFiles.Count} sessions");

        var deaths = _eventKinds.GetValueOrDefault("combat.death");
        var destructions = _eventKinds.GetValueOrDefault("combat.vehicle");

        Console.WriteLine($"  actor deaths      : {deaths}");
        Console.WriteLine($"  vehicle destroyed : {destructions}");

        if (deaths == 0 && destructions == 0)
        {
            Console.WriteLine("  -> none found, which is expected on SC 4.9: the game no longer");
            Console.WriteLine("     writes these events. See docs/findings.md.");
        }

        Section("Parser health");
        foreach (var (kind, count) in _eventKinds.OrderByDescending(p => p.Value))
            Console.WriteLine($"  {kind,-24} {count,8}");
        Console.WriteLine($"  {"! unmatched known tags",-24} {_unmatchedKnownTags,8}");

        if (_unmatchedByTag.Count > 0)
        {
            Section("Unmatched, by tag");
            foreach (var (tag, (count, sample)) in _unmatchedByTag.OrderByDescending(p => p.Value.Count))
            {
                Console.WriteLine($"  {count,6}  <{tag}>");
                Console.WriteLine($"          {sample}");
            }
        }
    }

    private static void Bump(Dictionary<string, int> map, string key) =>
        map[key] = map.GetValueOrDefault(key) + 1;

    private static string Join(IEnumerable<string> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? "(none)" : string.Join(", ", list);
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        Console.WriteLine(new string('-', title.Length));
    }

    private static void Top(string title, Dictionary<string, int> map, int take = 15)
    {
        Section($"{title} ({map.Count} distinct)");

        foreach (var (key, count) in map.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Take(take))
            Console.WriteLine($"  {count,6}  {key}");

        if (map.Count > take)
            Console.WriteLine($"  {"...",6}  and {map.Count - take} more");
    }
}
