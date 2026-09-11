using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Quantumwake.Core.Events;
using Quantumwake.Core.Logging;
using Quantumwake.Core.Parsing;
using Quantumwake.Core.GameData;
using Quantumwake.Core.State;
using Quantumwake.Data;

// Quantumwake CLI - backfill and verification harness.
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

// Step 2 of docs/screen-insight.md. Takes the lines an OCR engine returned -
// a text file, one per line - and says what the catalogue thinks they are.
//
// Text in and not an image, because reading a frame and naming what was read
// are separate problems and only the first needs Windows. It is also what
// lets a bad match be reproduced by editing a file.
if (GetOption(args, "--screen") is { } screenFile)
{
    return Screen(screenFile, install.RootPath, GetOption(args, "--catalogue"), GetOption(args, "--handle"));
}

var liveOnly = args.Contains("--live-only");

// Machine-readable mode. Everything the human report would say goes to stderr
// instead, so stdout carries nothing but events and the thing stays pipeable.
var asJson = args.Contains("--events");
var only = GetOption(args, "--kind")?.Split(',', StringSplitOptions.RemoveEmptyEntries
    | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

var log = asJson ? Console.Error : Console.Out;

var json = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

log.WriteLine("Quantumwake CLI  ·  by nekron");
log.WriteLine();
log.WriteLine($"Install : {install.RootPath}");
log.WriteLine($"Channel : {install.Channel}");

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
log.WriteLine($"Files   : {files.Count} ({totalBytes / 1024.0 / 1024.0:F1} MB)");
log.WriteLine();

var report = new Report();
var parser = new LogEventParser();
var stopwatch = Stopwatch.StartNew();

for (var i = 0; i < files.Count; i++)
{
    var file = files[i];
    log.Write($"\r  parsing {i + 1}/{files.Count} ...");

    // A fresh parser per file: session headers are per-file state, and a
    // truncated final line in one log must not leak into the next.
    var fileParser = new LogEventParser();
    report.BeginFile(Path.GetFileName(file));

    foreach (var ev in LogFileReader.ReadEvents(file, fileParser))
    {
        report.Add(ev);

        // One event per line, serialised as the concrete record so each kind
        // carries its own fields rather than a lowest common denominator.
        if (asJson && (only is null || only.Contains(ev.Kind)))
            Console.Out.WriteLine(JsonSerializer.Serialize(ev, ev.GetType(), json));
    }

    report.Merge(fileParser);
}

stopwatch.Stop();
log.Write("\r".PadRight(40));
log.WriteLine($"\rParsed in {stopwatch.Elapsed.TotalSeconds:F1}s\n");

// In --events mode stdout carries events and nothing else, so the report that
// would otherwise follow them is skipped rather than mixed in.
if (!asJson) report.Print();

return 0;

static string? GetOption(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

/// <summary>Reads the probe's own output, boxes and all.</summary>
/// <remarks>
/// The probe prints each line as <c>[ left top hNN]  the text</c>, and the
/// boxes are what let a tooltip be found by where it sat rather than by the
/// order the engine happened to return it in. A file without them still works
/// and is read as one column, which is what a hand-written fixture is.
/// </remarks>
static List<ScreenTextLine> Placed(IEnumerable<string> raw)
{
    var Box = new Regex(
        @"^\[\s*(?<left>\d+)\s+(?<top>\d+)\s+h\s*(?<height>\d+)\]\s+(?<text>.*)$");

    var placed = new List<ScreenTextLine>();
    var row = 0;

    foreach (var line in raw)
    {
        var boxed = Box.Match(line);

        if (boxed.Success)
        {
            placed.Add(new ScreenTextLine(
                boxed.Groups["text"].Value,
                double.Parse(boxed.Groups["left"].Value, CultureInfo.InvariantCulture),
                double.Parse(boxed.Groups["top"].Value, CultureInfo.InvariantCulture),
                double.Parse(boxed.Groups["height"].Value, CultureInfo.InvariantCulture)));
        }
        else if (line.Trim().Length > 0)
        {
            placed.Add(new ScreenTextLine(line, 0, row * 20, 16));
        }

        row++;
    }

    return placed;
}

/// <summary>Prints what a screenshot's text was matched to, and why.</summary>
/// <remarks>
/// The catalogue is loaded straight from the install rather than through
/// <c>LogLibrary</c>: naming a thing on screen has nothing to do with what is
/// in anybody's logs, and parsing 400 MB to answer it would make the harness
/// too slow to use while iterating on the matcher.
/// </remarks>
static int Screen(string linesFile, string installRoot, string? catalogueQuery, string? handle)
{
    if (!File.Exists(linesFile))
    {
        Console.Error.WriteLine($"No such file: {linesFile}");
        return 1;
    }

    var cache = Path.Combine(
        Path.GetDirectoryName(SessionStore.DatabasePathFor(installRoot))!,
        "commodities.json");

    var game = GameCommodities.Load(installRoot, cache);

    var items = game.ItemFacts
        .Select(kv => new ItemReference(
            kv.Key, kv.Value.Name, kv.Value.Type, kv.Value.SubType,
            kv.Value.Size, kv.Value.Grade,
            kv.Value.Manufacturer is { Length: > 0 } maker ? maker : null,
            null, "install", MicroScu: kv.Value.MicroScu))
        .ToList();

    Console.WriteLine($"Catalogue : {items.Count} items from the install");

    // A plain substring dump, so the catalogue's own wording for a thing can
    // be read next to the tooltip's. The two disagree more than expected.
    if (catalogueQuery is { Length: > 0 })
    {
        Console.WriteLine();
        Console.WriteLine($"Catalogue entries matching \"{catalogueQuery}\":");

        foreach (var item in items
            .Where(i => i.Name?.Contains(catalogueQuery, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Take(20))
        {
            Console.WriteLine($"  {item.Name}");
            Console.WriteLine($"      class    {item.ClassName}");
            Console.WriteLine($"      type     {item.Type} / {item.SubType}");
            Console.WriteLine($"      maker    {item.Manufacturer ?? "(none)"}");
            Console.WriteLine($"      size {item.Size}  grade {item.Grade}  {item.MicroScu} uSCU");
        }
    }

    var lines = Placed(File.ReadAllLines(linesFile));

    // Which screen this is, and what that screen carries, before the tooltip
    // question is asked of it. The install types a ship as NOITEM_Vehicle, so
    // the ship names come from the same catalogue as the parts.
    var shipNames = items
        .Where(i => i.Type?.Contains("Vehicle", StringComparison.OrdinalIgnoreCase) == true)
        .Select(i => i.Name)
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Select(n => n!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    var commodities = game.All.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    var frame = ScreenFrames.Read(lines, items, shipNames, handle, commodities);

    Console.WriteLine();
    Console.WriteLine($"Screen    : {frame.Kind}");

    if (frame.Wallet is { } wallet)
        Console.WriteLine($"Wallet    : {(wallet.Balance is { } b ? $"{b:N0} aUEC" : wallet.Trouble)}");

    if (frame.Map is { } map)
    {
        Console.WriteLine($"System    : {map.SystemRead ?? "(not read)"}");
        Console.WriteLine($"Place     : {map.PlaceRead ?? "(not read)"}");
        Console.WriteLine($"Position  : {map.Latitude}° {map.Longitude}° {map.Gigametres} Gm");
        Console.WriteLine($"Contracts : {(map.AcceptedContracts switch { false => "none accepted", true => "some accepted", _ => "(not stated)" })}");
    }

    if (frame.Kiosk is { } kiosk)
    {
        Console.WriteLine($"Side      : {(kiosk.Buying == false ? "selling" : kiosk.Buying == true ? "buying" : "(not read)")}");
        Console.WriteLine($"Ship      : {kiosk.Ship ?? kiosk.ShipRead ?? "(not read)"}  cargo {kiosk.CargoUsed}/{kiosk.CargoCapacity} SCU");
        Console.WriteLine($"Balance   : {kiosk.BalanceRead ?? "(not read)"}  {(kiosk.Balance is { } full ? $"({full:N0} aUEC, printed in full)" : "(abbreviated, so no number is taken from it)")}");

        foreach (var row in kiosk.Rows)
        {
            Console.WriteLine($"  {row.Commodity ?? $"\"{row.Read}\""}"
                + $"  stock {row.Quantity?.ToString("N0") ?? "?"} {row.QuantityUnit ?? ""}"
                + $"  price {row.Price?.ToString("N0") ?? "?"} per {row.PriceUnit ?? "?"}"
                + $"  {row.State ?? ""}");
        }
    }

    if (frame.Contracts is { } contracts)
    {
        Console.WriteLine($"Accepted  : {contracts.Accepted} of {contracts.Capacity}");
        foreach (var card in contracts.Cards)
            Console.WriteLine($"  card     {card.Title}  [{card.Reward ?? "?"}]  {card.Issuer ?? "(issuer not read)"}");
        Console.WriteLine($"Selected  : {contracts.SelectedTitle}  reward {contracts.SelectedReward}  by {contracts.SelectedIssuer}");
        foreach (var o in contracts.Objectives) Console.WriteLine($"  objective {o}");
    }

    if (frame.Fleet is { } fleet)
    {
        foreach (var row in fleet.Ships)
            Console.WriteLine($"  ship     {row.Ship ?? "?"}  read \"{row.Read}\"  at {row.Location ?? "?"}  {row.State ?? "?"}  {row.Focus ?? "?"}  cargo {row.Cargo}" + (row.LooksLike.Count > 0 ? $"  looks like {string.Join(", ", row.LooksLike)}" : ""));
    }

    if (frame.Reputation is { } rep)
    {
        Console.WriteLine($"Org       : {rep.Organisation}  standing {rep.Standing}  rank {rep.Rank ?? "(not readable)"}");
        Console.WriteLine($"Orgs      : {string.Join(", ", rep.Organisations)}");
    }

    if (frame.Map is { } m2 && m2.PathRead is not null) Console.WriteLine($"Path      : {m2.PathRead}");

    if (frame.Loadout is { } loadout)
    {
        Console.WriteLine($"Ship      : {loadout.Ship ?? "(not named)"}  read \"{loadout.ShipRead}\""
            + (loadout.LooksLike.Count > 0 ? $"  looks like {string.Join(", ", loadout.LooksLike)}" : ""));
        Console.WriteLine($"Scope     : {loadout.Scope ?? "(none)"}");
        Console.WriteLine($"Ports     : {loadout.Fittings.Count}");

        foreach (var fitting in loadout.Fittings)
        {
            var what = fitting.NothingRead ? "(nothing read under it)"
                : fitting.IsEmpty ? "(empty, so the game says)"
                : fitting.Name is not null ? $"{fitting.Name}  [{fitting.Tier}{(fitting.Agrees.Count > 0 ? ", " + string.Join(", ", fitting.Agrees) + " agree" : "")}{(fitting.Disagrees.Count > 0 ? ", " + string.Join(", ", fitting.Disagrees) + " disagree" : "")}]"
                : $"\"{fitting.Read}\"  [unmatched]";

            Console.WriteLine($"  {fitting.Slot,-32} {what}");
        }
    }

    var result = ScreenInsight.Look(lines, items);

    // A frame with no tooltip on it still has names all over it, and that is
    // the commoner shape by a distance: six of the nine screenshots measured
    // for this were loadout screens. A tooltip whose name matched nothing gets
    // the same treatment rather than a dead end - on the component tooltip
    // measured here the name was not in the reading at all.
    if (result.Reading.Name is null || result.Candidates.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine($"Name read : {result.Reading.Name ?? "(none)"}");

        // Printed even with no name. A tooltip that gave up a manufacturer and
        // a type and no name is not nothing - it is most of an answer, and
        // hiding it would make this look like a frame with no tooltip on it.
        foreach (var (label, value) in result.Reading.Fields.OrderBy(f => f.Key, StringComparer.Ordinal))
            Console.WriteLine($"  {label,-16} {value}");

        if (result.Reading.Name is not null)
        {
            Console.WriteLine();
            Console.WriteLine($"Not certain: {result.Trouble}");
        }

        var swept = ScreenInsight.Sweep(lines, items);

        Console.WriteLine();
        Console.WriteLine($"No tooltip. Swept {lines.Count} lines, {swept.Count} named something:");

        foreach (var line in swept)
        {
            var how = line.Named
                ? "exact"
                : $"{line.Candidates.Count} x {line.Candidates[0].Tier}";

            Console.WriteLine();
            Console.WriteLine($"  \"{line.Text}\"  [{how}]");

            foreach (var candidate in line.Candidates.Take(4))
                Console.WriteLine($"      {candidate.Item.Name}  ({candidate.Item.ClassName})");
        }

        return 0;
    }

    Console.WriteLine();
    Console.WriteLine($"Name read : {result.Reading.Name}");

    foreach (var (label, value) in result.Reading.Fields.OrderBy(f => f.Key, StringComparer.Ordinal))
        Console.WriteLine($"  {label,-16} {value}");

    Console.WriteLine();
    Console.WriteLine(result.Certain
        ? "Certain."
        : $"Not certain: {result.Trouble}");

    foreach (var candidate in result.Candidates.Take(10))
    {
        Console.WriteLine();
        Console.WriteLine($"  {candidate.Item.Name}  [{candidate.Tier}]");
        Console.WriteLine($"      class    {candidate.Item.ClassName}");
        Console.WriteLine($"      type     {candidate.Item.Type} / {candidate.Item.SubType}");
        Console.WriteLine($"      maker    {candidate.Item.Manufacturer ?? "(none)"}");
        Console.WriteLine($"      {candidate.Item.MicroScu} uSCU");
        Console.WriteLine($"      agrees   {(candidate.Agrees.Count == 0 ? "(nothing)" : string.Join(", ", candidate.Agrees))}");
        Console.WriteLine($"      against  {(candidate.Disagrees.Count == 0 ? "(nothing)" : string.Join(", ", candidate.Disagrees))}");
    }

    return 0;
}

/// <summary>Aggregates parsed events into the figures worth eyeballing.</summary>
internal sealed class Report
{
    private readonly Dictionary<string, int> _locations = [];
    private readonly Dictionary<string, int> _ships = [];
    private readonly Dictionary<string, int> _gameRules = [];
    private readonly Dictionary<string, int> _contracts = [];
    private readonly Dictionary<string, int> _quantumDestinations = [];
    private readonly Dictionary<string, int> _partyMoments = [];
    private readonly Dictionary<string, int> _channelMoments = [];
    private readonly Dictionary<string, int> _berths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _partyHandles = [];
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
    private int _partyNotifications;
    private int _channelNotifications;
    private int _unmatchedKnownTags;
    private int _corpseDeaths;
    private int _shipRetrievals;
    private int _contractCompletions;
    private int _awards;
    private decimal _awarded;
    private DateTimeOffset? _lastCorpseAt;

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
            // Corpse item lines arrive in a burst per death, so group by time.
            case CorpseItemEvent corpse:
                if (_lastCorpseAt is not { } last || corpse.Timestamp - last >= TimeSpan.FromSeconds(30))
                    _corpseDeaths++;

                _lastCorpseAt = corpse.Timestamp;
                break;

            case VehicleSpawnEvent:
                _shipRetrievals++;
                break;

            case NotificationEvent notification:
                if (!_notificationIds.Add($"{_currentFile}|{notification.NotificationId}|{notification.Text}"))
                    break;

                if (notification.IsIncapacitation)
                {
                    _incapacitations++;
                    _incapacitationFiles.Add(_currentFile);
                }

                if (notification.IsContractComplete)
                    _contractCompletions++;

                if (notification.Awarded is { } awarded)
                {
                    _awards++;
                    _awarded += awarded;
                }

                // Both sides are counted rather than just the successes: the gap
                // between notifications seen and notes read is the number of
                // lines the reader declined to guess at, and that number should
                // stay small without ever being forced to zero.
                if (ShipChannel.IsChannel(notification.Text))
                {
                    _channelNotifications++;

                    if (ShipChannel.Read(notification.Timestamp, notification.Text) is { } berth)
                    {
                        var moment = berth.Moment.ToString();
                        _channelMoments[moment] = _channelMoments.GetValueOrDefault(moment) + 1;

                        var berthName = $"{berth.Ship} : {berth.Owner}";
                        _berths[berthName] = _berths.GetValueOrDefault(berthName) + 1;
                    }
                }

                if (notification.IsParty)
                {
                    _partyNotifications++;

                    if (Party.Read(notification.Timestamp, notification.Text) is { } note)
                    {
                        var moment = note.Moment.ToString();
                        _partyMoments[moment] = _partyMoments.GetValueOrDefault(moment) + 1;

                        if (note.Handle is not null)
                            _partyHandles[note.Handle] =
                                _partyHandles.GetValueOrDefault(note.Handle) + 1;
                    }
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

        Section("Contract payouts");
        Console.WriteLine($"  completions   : {_contractCompletions}");
        Console.WriteLine($"  awards stated : {_awards}");
        Console.WriteLine($"  total         : {_awarded:N0} aUEC");

        // Both numbers, always, because the gap between them is the point: the
        // game states a payout for a fraction of what it completes, and a total
        // shown on its own reads as contract income rather than a floor over
        // the few it bothered to price.
        Console.WriteLine("  -> a floor: most completions state no payout at all.");

        Section("Party");
        Console.WriteLine($"  notifications : {_partyNotifications}");
        Console.WriteLine($"  read as notes : {_partyMoments.Values.Sum()}");
        Console.WriteLine($"  players named : {_partyHandles.Count}");

        foreach (var (moment, count) in _partyMoments.OrderByDescending(p => p.Value))
            Console.WriteLine($"    {moment,-14}{count,6}");

        // The unread remainder is queue and matchmaking chatter naming nobody.
        // Printed rather than hidden: if it ever grows, the channel has gained a
        // sentence worth reading.
        var unread = _partyNotifications - _partyMoments.Values.Sum();
        if (unread > 0)
            Console.WriteLine($"  -> {unread} named nobody (join queue, broadcasts), left unread.");

        Top("Flown with", _partyHandles, 10);

        Section("Ship comms");
        Console.WriteLine($"  notifications : {_channelNotifications}");
        Console.WriteLine($"  read as notes : {_channelMoments.Values.Sum()}");
        Console.WriteLine($"  ships named   : {_berths.Count}");

        foreach (var (moment, count) in _channelMoments.OrderByDescending(p => p.Value))
            Console.WriteLine($"    {moment,-14}{count,6}");

        Top("Berths", _berths, 8);

        Section("Combat");
        Console.WriteLine($"  incapacitations   : {_incapacitations} across {_incapacitationFiles.Count} sessions");

        var deaths = _eventKinds.GetValueOrDefault("combat.death");
        var destructions = _eventKinds.GetValueOrDefault("combat.vehicle");

        Console.WriteLine($"  deaths            : {_corpseDeaths}   (from corpse item-recovery bursts)");
        Console.WriteLine($"  ship retrievals   : {_shipRetrievals}");
        Console.WriteLine($"  actor deaths      : {deaths}");
        Console.WriteLine($"  vehicle destroyed : {destructions}");

        if (deaths == 0 && destructions == 0)
        {
            Console.WriteLine("  -> no <Actor Death> or <Vehicle Destruction>, as expected on SC 4.9 and 4.10.");
            Console.WriteLine("     Deaths above come from corpse bursts, which the game still emits.");
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
