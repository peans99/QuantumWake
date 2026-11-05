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

// The garage check needs no install at all: it reads the two community dump
// files and says whether the sheet reproduces their own totals, ship by ship.
if (GetOption(args, "--garage-check") is { } dumpDir)
    return GarageCheck(dumpDir);

// The screen reader run over a screenshot, or a folder of them, the way the
// backups are run through the parser: what each frame read, how long it took,
// and the lines written out so --screen and a diff can be asked about them.
// Needs no install; needs the models, which it fetches on first use.
if (GetOption(args, "--ocr") is { } ocrTarget)
    return await Ocr(ocrTarget, GetOption(args, "--out"), args.Contains("--show"), GetOption(args, "--rec"));

var pathArg = GetOption(args, "--path");
var install = pathArg is not null
    ? GameInstallLocator.FromPath(pathArg)
    : GameInstallLocator.Preferred();

if (install is null)
{
    Console.Error.WriteLine(OperatingSystem.IsWindows()
        ? "No Star Citizen install found. Pass --path <StarCitizen\\LIVE>."
        : "No Star Citizen install found in any Wine prefix. Pass --path <prefix>, or the LIVE folder under its drive_c.");
    return 1;
}

// The table behind docs/garage.md's maker-logo claim: every non-paint
// SCItemManufacturer record's Code and Logo, whether the archive holds the
// texture, and how a split mip chain is cut. Run it again after a patch.
if (args.Contains("--maker-logos"))
{
    var p4k = new P4kArchive(P4kArchive.PathFor(install.RootPath));
    var blob = p4k.TryRead(@"Data\Game2.dcb");
    if (blob is null) { Console.Error.WriteLine("no Game2.dcb"); return 1; }
    var core = new DataCore(blob);
    var folder = p4k.List(@"Data\UI\SharedAssets\ManufacturerLogos\");
    Console.WriteLine($"{folder.Count} entries under ManufacturerLogos");
    var byName = folder.ToDictionary(e => System.IO.Path.GetFileName(e.Path), e => e.Size, StringComparer.OrdinalIgnoreCase);
    var n = 0;
    foreach (var record in core.Records())
    {
        if (!record.Name.StartsWith("SCItemManufacturer.", StringComparison.OrdinalIgnoreCase) || record.Name.Contains("Paint_", StringComparison.OrdinalIgnoreCase)) continue;
        var at = core.InstanceAt(record, record.VariantIndex);
        var code = core.StringAt(at, record.StructIndex, "Code");
        var logo = core.StringAt(at, record.StructIndex, "Logo");
        var file = logo is { Length: > 0 } ? System.IO.Path.GetFileNameWithoutExtension(logo) + ".dds" : null;
        var has = file is not null && byName.TryGetValue(file, out var size)
            ? $"archive {size}" + (size < 1024 ? " parts " + string.Join(",", folder.Where(e => e.Path.Contains(file + ".", StringComparison.OrdinalIgnoreCase)).Select(e => System.IO.Path.GetExtension(e.Path) + "=" + e.Size)) : "")
            : "archive -";
        Console.WriteLine($"{code,-6} {record.Name,-40} {logo ?? "-",-70} {has}");
        n++;
    }
    Console.WriteLine($"{n} makers");
    return 0;
}


// Every screen reader run over a folder of screenshots, end to end: the same
// ReadShotAsync the dashboard calls, second looks and all, with the ONNX
// engine, and one line per frame saying what it was taken for. Run on Windows
// as well and diffed, this is the measurement docs/linux.md asks for before
// the engine is trusted with the readers.
if (GetOption(args, "--ocr-check") is { } checkFolder)
    return await OcrCheck(checkFolder, install.RootPath, GetOption(args, "--rec"));

// Step 2 of docs/screen-insight.md. Takes the lines an OCR engine returned -
// a text file, one per line - and says what the catalogue thinks they are.
//
// Text in and not an image, because reading a frame and naming what was read
// are separate problems and only the first needs Windows. It is also what
// lets a bad match be reproduced by editing a file.
if (GetOption(args, "--screen") is { } screenFile)
{
    return Screen(screenFile, install.RootPath, GetOption(args, "--catalogue"));
}

// The reputation ladders as the install has them, and every Rep app reading
// the app has kept, placed on its ladder. The table behind the Reputation
// page's estimates. --data names the app's folder to read from, and the
// game-data cache is kept there too: point it at a copy, because opening the
// readings writes the reputation history beside them.
if (args.Contains("--reputation"))
{
    var data = GetOption(args, "--data") ?? Quantumwake.Core.AppPaths.Root;
    var game = GameCommodities.Load(install.RootPath, Path.Combine(data, "commodities.json"));
    var catalogue = game.Reputation;

    Console.WriteLine($"{catalogue.Scopes.Count} ladders");
    foreach (var scope in catalogue.Scopes)
    {
        Console.WriteLine($"  {scope.Id,-44} {scope.Name ?? $"({scope.NameKey})"}  ceiling {scope.Ceiling:N0}");
        Console.WriteLine("      " + string.Join(" | ", scope.Ranks.Select(r => $"{r.Name} {r.MinReputation:N0}" + (r.Perk is null ? "" : " +"))));
    }

    Console.WriteLine();
    var history = new ScreenReadingStore(data).ReputationHistory();
    Console.WriteLine($"{history.Count} Rep readings");

    foreach (var r in history.OrderBy(r => r.ShotAt))
    {
        var reading = r.Reading;
        var scope = ReputationLadders.Match(reading, catalogue);
        var place = ReputationLadders.Place(reading, catalogue);
        Console.WriteLine($"  {r.ShotAt:yyyy-MM-dd HH:mm} {reading.Organisation,-28} {reading.Rank ?? "(no rank)",-22} "
            + (reading.Progress is { } p ? $"{p,6:P1}" : "      ")
            + $"  ladder {scope?.Id ?? "-",-40}"
            + (place is null ? "" : $"  ~{place.Estimate:N0} ±{place.Resolution:N0}  [{place.Floor:N0}..{place.Next?.ToString("N0") ?? "top"}]"));
    }

    return 0;
}

// The mining model as the install has it: the constants, every mineral's
// resistance and instability, every laser's power and modifiers, the modules
// and the gadgets. The table behind docs/mining.md; run it again after a
// patch and against the community tools, which read the same files.
if (args.Contains("--mining"))
{
    var cache = Path.Combine(Path.GetDirectoryName(SessionStore.DatabasePathFor(install.RootPath))!, "commodities.json");
    var mining = GameCommodities.Load(install.RootPath, cache).Mining;

    if (mining.Constants is { } k)
    {
        Console.WriteLine($"constants: capacity {k.PowerCapacityPerMass}/kg, decay {k.DecayPerMass}/kg/s, window {k.OptimalWindowSize} (max {k.OptimalWindowMaxSize}, factor {k.OptimalWindowFactor}), "
            + $"resistance curve {k.ResistanceCurveFactor}, thinness curve {k.OptimalWindowThinnessCurveFactor}, fill {k.ControlledBreakingFillRate}/s (danger {k.DangerBreakingFillRate}/s), "
            + $"absorbable below {k.AbsorbableVolumeThreshold}, {k.CentiScuPerVolume} cSCU per volume");
    }
    else Console.WriteLine("constants: none read");

    Console.WriteLine($"\n{mining.Minerals.Count} minerals");
    foreach (var m in mining.Minerals)
        Console.WriteLine($"  {m.Name,-24} {m.Method,-6} resistance {m.Resistance,6:0.00}  instability {m.Instability,6:0}  window {m.WindowMidpoint:0.00}±{m.WindowRandomness:0.00} thin {m.WindowThinness,5:0.0}  explosion ×{m.ExplosionMultiplier,-5:0}  cluster {m.ClusterFactor:0.00}  [{m.Class}]");

    static string Mods(MiningModifiers x) => string.Join(" ", new[]
    {
        x.Resistance != 0 ? $"res {x.Resistance:+0.#;-0.#}%" : null,
        x.Instability != 0 ? $"inst {x.Instability:+0.#;-0.#}%" : null,
        x.WindowSize != 0 ? $"window {x.WindowSize:+0.#;-0.#}%" : null,
        x.WindowRate != 0 ? $"rate {x.WindowRate:+0.#;-0.#}%" : null,
        x.CatastrophicRate != 0 ? $"overcharge {x.CatastrophicRate:+0.#;-0.#}%" : null,
        x.ShatterDamage != 0 ? $"shatter {x.ShatterDamage:+0.#;-0.#}%" : null,
        x.ClusterFactor != 0 ? $"cluster {x.ClusterFactor:+0.#;-0.#}%" : null,
    }.Where(s => s is not null));

    Console.WriteLine($"\n{mining.Lasers.Count} lasers");
    foreach (var l in mining.Lasers)
        Console.WriteLine($"  S{l.Size} {l.Name,-28} power {l.Power,6:0}  extraction {l.ExtractionPower,5:0}  slots {l.Slots}  filter {l.FilterModifier,3:0}%  throttle min {l.ThrottleMinimum:0.00}  {Mods(l.Modifiers)}  [{l.Class}]");

    Console.WriteLine($"\n{mining.Modules.Count} modules");
    foreach (var m in mining.Modules)
        Console.WriteLine($"  {(m.Active ? "active " : "passive")} {m.Name,-22} power ×{m.PowerMultiplier:0.00}  extraction ×{m.ExtractionMultiplier:0.00}  filter {m.FilterModifier,4:0.#}%  {(m.Active ? $"{m.Lifetime:0}s × {m.Charges}  " : "")}{Mods(m.Modifiers)}  [{m.Class}]");

    Console.WriteLine($"\n{mining.Gadgets.Count} gadgets");
    foreach (var g in mining.Gadgets)
        Console.WriteLine($"  {g.Name,-12} {Mods(g.Modifiers)}  [{g.Class}]");

    Console.WriteLine($"\n{mining.Compositions.Count} compositions");
    foreach (var c in mining.Compositions.OrderBy(c => c.Class, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"  {c.Class,-36} {c.Name,-24} min {c.MinimumDistinctElements} distinct: {string.Join(", ", c.Parts.Select(p => $"{p.Element} {p.MinPercent:0}-{p.MaxPercent:0}% p{p.Probability:0.00}"))}");

    return 0;
}

// The personal armoury as the install has it: every gun with its damage,
// rate, magazine and fire modes, every piece of armour with its resistances
// and the figures that actually differ between pieces. The table behind
// docs/armoury.md; run it again after a patch and against the community
// tables, which read the same files. --armoury=all lists the finishes too.
// Cargo fit: the crates as the install sizes them, every hull's grids as the
// community digest places them, the check that the grids sum to the dump's
// own capacity, and the question that started it. The table behind
// docs/cargo-fit.md; run it again after a patch or a dataset refresh.
// Salvage as the install has it: the constants, every scraper module and
// head, and each hull's controller - what it scrapes to, what it
// disintegrates to and how fast. What is NOT here, and why, is in
// docs/salvage.md: a hull's own yield needs its surface area and volume,
// which are geometry the DataCore does not hold.
// Every action the game can bind, with its defaults and, if the profile
// rebinds it, what this install has on it. --keys=md writes the reference
// document (docs/keybindings-<version>.md is one of these).
if (args.Any(a => a.StartsWith("--keys", StringComparison.OrdinalIgnoreCase)))
{
    var markdown = args.Contains("--keys=md", StringComparer.OrdinalIgnoreCase);
    var cache = Path.Combine(Path.GetDirectoryName(SessionStore.DatabasePathFor(install.RootPath))!, "commodities.json");
    var game = GameCommodities.Load(install.RootPath, cache);
    var catalogue = game.Controls.Catalogue;
    var profilePath = Path.Combine(install.RootPath, "user", "client", "0", "Profiles", "default", "actionmaps.xml");
    var profile = File.Exists(profilePath) ? Quantumwake.Core.Controls.ControlProfile.Parse(File.ReadAllBytes(profilePath)) : null;
    var mine = (profile?.Bindings ?? []).ToLookup(b => (b.ActionMap, b.Action));
    var sticks = (profile?.Joysticks ?? []).ToDictionary(d => d.Key, d => d.Product ?? d.Key);
    string Mine(Quantumwake.Core.Controls.ControlBinding b) =>
        b.Input.Kind == Quantumwake.Core.Controls.InputKind.None ? $"{b.Input.DeviceKey} cleared"
        : b.Input.IsJoystick ? $"{sticks.GetValueOrDefault(b.Input.DeviceKey, b.Input.DeviceKey)} {b.Input.Label}"
        : b.Input.Label;
    static string Dash(string s) => s.Length == 0 ? "—" : s;
    static string Cell(string s) => s.Replace("|", "\\|");

    if (markdown)
    {
        // The archive does not say its version; the live log's session does.
        var version = install.HasGameLog && LogLibrary.BuildSession(install.GameLogPath).GameVersion is { Length: > 0 } v
            ? $"Alpha {v}" : "this install";
        Console.WriteLine($"# Star Citizen keybindings, {version}");
        Console.WriteLine();
        Console.WriteLine($"Every action the game can bind - {catalogue.Actions.Count} in {catalogue.ActionMaps.Count} groups - read from");
        Console.WriteLine("`Data\\Libs\\Config\\defaultProfile.xml` in this install's archive and labelled from its");
        Console.WriteLine("own strings, with the default input per device kind and how the action fires.");
        Console.WriteLine("A dash is bindable but unbound by default. Generated on " + DateTime.Now.ToString("yyyy-MM-dd") + " by");
        Console.WriteLine("`dotnet run --project src\\Quantumwake.Cli -c Release -- --keys=md > docs\\keybindings.md`;");
        Console.WriteLine("regenerate after a patch and diff. `--keys` alone prints the same with this install's");
        Console.WriteLine("own bindings beside each action.");
        Console.WriteLine();
        foreach (var map in catalogue.ActionMaps)
        {
            var actions = catalogue.Actions.Where(a => a.ActionMap == map.Name).ToList();
            if (actions.Count == 0) continue;
            Console.WriteLine($"## {map.Label}{(map.Category.Length > 0 ? $" · {map.Category}" : "")}");
            Console.WriteLine();
            Console.WriteLine($"`{map.Name}` · {actions.Count} actions");
            Console.WriteLine();
            Console.WriteLine("| Action | Id | Keyboard | Mouse | Gamepad | Joystick | Fires |");
            Console.WriteLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var a in actions)
                Console.WriteLine($"| {Cell(a.Label)} | `{a.Name}` | {Cell(Dash(a.Keyboard))} | {Cell(Dash(a.Mouse))} | {Cell(Dash(a.Gamepad))} | {Cell(Dash(a.Joystick))} | {Dash(a.ActivationMode)} |");
            Console.WriteLine();
        }
        return 0;
    }

    Console.WriteLine($"{catalogue.Actions.Count} actions in {catalogue.ActionMaps.Count} groups; profile {(profile is null ? "none" : $"{profile.Bindings.Count} rebinds on {sticks.Count} sticks")}");
    foreach (var map in catalogue.ActionMaps)
    {
        var actions = catalogue.Actions.Where(a => a.ActionMap == map.Name).ToList();
        if (actions.Count == 0) continue;
        Console.WriteLine($"\n{map.Label}{(map.Category.Length > 0 ? $" [{map.Category}]" : "")}  ({map.Name})");
        foreach (var a in actions)
        {
            var yours = mine[(map.Name, a.Name)].Select(Mine).ToList();
            Console.WriteLine($"  {a.Label,-46} kb {Dash(a.Keyboard),-14} js {Dash(a.Joystick),-10} gp {Dash(a.Gamepad),-12} {a.ActivationMode,-18}{(yours.Count > 0 ? "  YOURS: " + string.Join(", ", yours) : "")}");
        }
    }
    return 0;
}

if (args.Contains("--salvage", StringComparer.OrdinalIgnoreCase))
{
    var cache = Path.Combine(Path.GetDirectoryName(SessionStore.DatabasePathFor(install.RootPath))!, "commodities.json");
    var salvage = GameCommodities.Load(install.RootPath, cache).Salvage;

    Console.WriteLine(salvage.Constants is { } k
        ? $"constants: hull thickness {k.HullThicknessMetres * 1000:0.#} mm, ammo-to-material factor {k.AmmoToMaterialFactor}"
        : "constants: none read");

    Console.WriteLine($"\n{salvage.Heads.Count} heads");
    foreach (var h in salvage.Heads) Console.WriteLine($"  {h.Name,-28} {h.Slots} module slots  {h.Manufacturer}  [{h.Class}]");

    Console.WriteLine($"\n{salvage.Modules.Count} scraper modules");
    foreach (var m in salvage.Modules) Console.WriteLine($"  {m.Name,-28} speed {m.Speed,5:0.###}  radius {m.Radius,4:0.##} m  efficiency {m.Efficiency:P0}  {m.Manufacturer}  [{m.Class}]");

    Console.WriteLine($"\n{salvage.Ships.Count} salvage hulls");
    foreach (var s in salvage.Ships)
        Console.WriteLine($"  {s.Ship,-24} scrapes to {s.ScrapesTo ?? "-",-6} disintegrates {s.ScuPerCubicMetre:0.#####} SCU/m³ of {s.DisintegratesTo ?? "-",-28} heads {s.Heads}  {s.BoxSecondsPerScu:0.#} s/SCU to box");

    return 0;
}

if (args.Contains("--cargo", StringComparer.OrdinalIgnoreCase))
{
    var cache = Path.Combine(Path.GetDirectoryName(SessionStore.DatabasePathFor(install.RootPath))!, "commodities.json");
    var crates = GameCommodities.Load(install.RootPath, cache).Crates;
    Console.WriteLine($"{crates.Count} crate sizes read from the install" + (crates.Count == 0 ? " - the built-in table stands in" : ""));
    foreach (var c in crates.Count > 0 ? crates : CargoFit.StandardCrates)
        Console.WriteLine($"  {c.Scu,3} SCU  {c.X:0.##} x {c.Y:0.##} x {c.Z:0.##} m  ({c.X / CargoFit.CellMetres:0} x {c.Y / CargoFit.CellMetres:0} x {c.Z / CargoFit.CellMetres:0} cells)  {c.Mass:N0} kg full");

    var community = new CommunityData();
    if (!community.HasCargoGrids) { Console.WriteLine("\nThe community digest has no cargo grids - refresh the reference data first."); return 1; }
    var withGrids = community.GarageShips.Values.Where(s => s.CargoGrids is { Count: > 0 }).ToList();
    var agree = withGrids.Count(s => Math.Abs(s.CargoGrids!.Sum(g => g.Scu) - s.CargoScu) < 0.01);
    var offLattice = withGrids.SelectMany(s => s.CargoGrids!).Count(g => !CargoFit.OnLattice(g));
    Console.WriteLine($"\n{withGrids.Count} hulls with grids of {community.GarageShips.Count}; the grids sum to the dump's capacity on {agree}; {offLattice} grids off the 1.25 m lattice");
    foreach (var s in withGrids.Where(s => Math.Abs(s.CargoGrids!.Sum(g => g.Scu) - s.CargoScu) >= 0.01))
        Console.WriteLine($"  ! {s.Class}: grids {s.CargoGrids!.Sum(g => g.Scu)} vs {s.CargoScu}");
    foreach (var s in withGrids.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"  {s.Name,-34} {s.CargoScu,6:0} SCU  " + string.Join("; ", s.CargoGrids!.GroupBy(g => g.Class).Select(g => $"{g.Count()}x {g.Key} {g.First().X:0.##}x{g.First().Y:0.##}x{g.First().Z:0.##} m, up to {g.First().MaxBox.X:0.##}x{g.First().MaxBox.Y:0.##}x{g.First().MaxBox.Z:0.##}")));

    if (community.GarageShip("RSI_Hermes") is { CargoGrids: { Count: > 0 } } hermes)
    {
        var fit = CargoFit.Pack(hermes.CargoGrids, new CargoLoad(new Dictionary<int, int> { [32] = 4, [16] = 2 }), crates.Count > 0 ? crates : null);
        Console.WriteLine($"\n4 x 32 + 2 x 16 in the Hermes: {(fit.Fits ? "fits" : "no packing found")}, {fit.PlacedScu} of {fit.LoadScu} SCU placed in {fit.CapacityScu}");
        foreach (var g in fit.Grids) Console.WriteLine($"  {g.Grid.Class} {g.Cells.W}x{g.Cells.L}x{g.Cells.H}: " + string.Join(", ", g.Placed.Select(p => $"{p.Scu}@({p.X},{p.Y},{p.Z}) {p.DX}x{p.DY}x{p.DZ}")) + (g.RuleIgnored ? "  [one-cell rule ignored]" : ""));
    }
    return 0;
}

// Event progress, the evidence behind docs/events.md: every campaign the
// install describes, its bars and tiers, what each contract pays, and what
// this install's logs have earned against it. Run it after a patch - an event
// arriving or leaving shows here first.
if (args.Contains("--events", StringComparer.OrdinalIgnoreCase))
{
    var cache = Path.Combine(Path.GetDirectoryName(SessionStore.DatabasePathFor(install.RootPath))!, "commodities.json");
    var catalogue = GameCommodities.Load(install.RootPath, cache).Scenarios;
    var all = new List<ContractRecord>();

    foreach (var file in install.BackupLogs().Concat(install.HasGameLog ? [install.GameLogPath] : []))
    {
        var builder = new SessionBuilder(Path.GetFileName(file));
        foreach (var ev in LogFileReader.ReadEvents(file, new LogEventParser())) builder.Add(ev);
        all.AddRange(builder.Build().Contracts);
    }

    Console.WriteLine($"{catalogue.Scenarios.Count} campaigns, {catalogue.Contracts.Count} paying contracts; {all.Count} contracts in the logs");

    foreach (var status in EventProgress.Build(catalogue, all))
    {
        Console.WriteLine($"\n== {status.Title}  [{status.Id}]{(status.Percent ? "  (journal shows %)" : "")}");
        Console.WriteLine(status.FirstSeen is { } first
            ? $"   seen {first:yyyy-MM-dd} .. {status.LastSeen:yyyy-MM-dd}, {status.Completed} completed" + (status.Unrecognised > 0 ? $", {status.Unrecognised} unrecognised" : "")
            : "   not in these logs");

        foreach (var track in status.Tracks)
        {
            Console.WriteLine($"   {track.Name,-14} {track.Points,7:N0} pts  tiers " + string.Join(" / ", track.Tiers.Select(t => (t.Reached ? "*" : "") + t.MinPoints.ToString("N0"))));
            foreach (var tier in track.Tiers) Console.WriteLine($"      {tier.MinPoints,7:N0}  {tier.Reward}");
            foreach (var s in track.Fastest) Console.WriteLine($"      next: {s.Needed} x {s.Title} ({s.Points})");
        }

        foreach (var c in status.Contracts)
            Console.WriteLine($"   {c.Points,6:N0}  {c.Id,-46} {string.Join("+", c.Tracks.Skip(1)),-12} {c.Completed,3} done  {c.Title}");
    }

    return 0;
}

// Each planet and moon's baseline against every armour set's temperature
// range, by the rule the Armoury's Climate tab uses: a set is judged by the
// narrowest of its pieces and counts only when it covers the body (a core or
// an undersuit). The numbers behind docs/armoury.md's climate section.
if (args.Contains("--climate", StringComparer.OrdinalIgnoreCase))
{
    var data = GetOption(args, "--data") ?? Quantumwake.Core.AppPaths.Root;
    var armoury = GameCommodities.Load(install.RootPath, Path.Combine(data, "commodities.json")).Armoury;
    var climate = armoury.Climate ?? GameClimateData.Empty;

    var families = armoury.Armour
        .Where(p => p.Slot != "Backpack" && (p.TemperatureMin != 0 || p.TemperatureMax != 0))
        .GroupBy(p => p.Family)
        .Select(g => (Family: g.Key, Min: g.Max(p => p.TemperatureMin), Max: g.Min(p => p.TemperatureMax),
            Body: g.Any(p => p.Slot is "Core" or "Undersuit")))
        .ToList();
    var sets = families.Where(f => f.Body).ToList();

    Console.WriteLine($"{climate.Bodies.Count} bodies; unprotected {climate.BodyMin} to {climate.BodyMax} °C; visor frosts at {climate.VisorFrost} °C");
    Console.WriteLine($"{sets.Count} sets that cover the body, {families.Count - sets.Count} other families (helmets, arms, legs alone)");
    Console.WriteLine($"hottest-rated set {sets.MaxBy(s => s.Max).Family} to {sets.Max(s => s.Max)} °C; coldest {sets.MinBy(s => s.Min).Family} to {sets.Min(s => s.Min)} °C\n");

    foreach (var b in climate.Bodies)
    {
        var rated = sets.Where(s => s.Min <= b.Celsius && b.Celsius <= s.Max).ToList();
        var bare = climate.BodyMin <= b.Celsius && b.Celsius <= climate.BodyMax;
        Console.WriteLine($"  {b.Name,-11} {(b.Parent is null ? "" : "(" + b.Parent + ")"),-12} {b.Celsius,7:F1} °C  p {b.Pressure,5}  h {b.Humidity,4}  "
            + $"{(bare ? "comfortable" : "needs a suit"),-12}  sets {rated.Count,3}"
            + (rated.Count is > 0 and <= 5 ? "  " + string.Join(", ", rated.Select(s => s.Family)) : ""));
    }

    return 0;
}

// The ship-part figures the item labels write into descriptions, held up
// against the community dataset's own - a source the reader never uses - so a
// field read from the wrong place shows up as a disagreement rather than as a
// plausible number. Run with a scratch --data: it rebuilds the cache.
if (args.Contains("--ship-parts", StringComparer.OrdinalIgnoreCase))
{
    var data = GetOption(args, "--data") ?? Quantumwake.Core.AppPaths.Root;
    var parts = GameCommodities.Load(install.RootPath, Path.Combine(data, "commodities.json")).Armoury.ShipParts ?? [];
    Console.WriteLine($"{parts.Count} ship parts with figures: " + string.Join(", ", parts.Values.GroupBy(p => p.Type).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}")));

    var digest = Path.Combine(GetOption(args, "--community") ?? Path.Combine(Quantumwake.Core.AppPaths.Root, "community"), "digest-part-stats.json");
    if (!File.Exists(digest)) { Console.WriteLine("no community digest to check against"); return 0; }
    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(digest));

    var checks = new Dictionary<string, (int Agree, int Differ, List<string> Examples)>();
    void Check(string field, string cls, double? ours, double theirs)
    {
        if (ours is null || theirs <= 0) return;
        var entry = checks.TryGetValue(field, out var e) ? e : (Agree: 0, Differ: 0, Examples: new List<string>());
        if (Math.Abs(ours.Value - theirs) <= Math.Max(0.06, theirs * 0.001)) entry.Agree++;
        else { entry.Differ++; if (entry.Examples.Count < 40) entry.Examples.Add($"{cls}: {ours} vs {theirs}  [{parts[cls].FireKind} x{parts[cls].FireModes}]"); }
        checks[field] = entry;
    }
    static double Num(System.Text.Json.JsonElement e, string name) =>
        e.ValueKind == System.Text.Json.JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number ? v.GetDouble() : 0;

    foreach (var entry in doc.RootElement.EnumerateObject())
    {
        var e = entry.Value;
        var cls = e.TryGetProperty("Class", out var c) ? c.GetString() ?? entry.Name : entry.Name;
        if (!parts.TryGetValue(cls, out var p)) continue;
        if (e.TryGetProperty("Shield", out var sh)) { Check("shield HP", cls, p.ShieldHp, Num(sh, "Hp")); Check("shield regen", cls, p.ShieldRegen, Num(sh, "Regen")); Check("damaged delay", cls, p.DamagedDelay, Num(sh, "DamagedDelay")); Check("downed delay", cls, p.DownedDelay, Num(sh, "DownedDelay")); }
        if (p.Type == "PowerPlant") Check("power output", cls, p.PowerOutput, Num(e, "PowerGen"));
        if (p.Type == "Cooler") Check("cooling output", cls, p.CoolingOutput, Num(e, "CoolantGen"));
        if (e.TryGetProperty("Quantum", out var q)) { Check("quantum speed", cls, p.QuantumSpeed, Num(q, "Speed")); Check("spool", cls, p.SpoolTime, Num(q, "SpoolTime")); Check("quantum cooldown", cls, p.QuantumCooldown, Num(q, "Cooldown")); }
        if (e.TryGetProperty("Weapon", out var w)) { Check("gun alpha", cls, p.Alpha, Num(w, "Alpha")); Check("gun rpm", cls, p.RoundsPerMinute, Num(w, "RateOfFire")); Check("gun dps", cls, p.Dps, Num(w, "Dps")); Check("gun speed", cls, p.ProjectileSpeed, Num(w, "AmmoSpeed")); Check("gun range", cls, p.Range, Num(w, "Range")); }
        if (e.TryGetProperty("Missile", out var m)) { Check("missile damage", cls, p.MissileDamage, Num(m, "Damage")); Check("missile speed", cls, p.MissileSpeed, Num(m, "Speed")); Check("lock time", cls, p.LockTime, Num(m, "LockTime")); }
    }
    foreach (var (field, (agree, differ, examples)) in checks.OrderBy(k => k.Key))
    {
        Console.WriteLine($"  {field,-16} {agree,5} agree  {differ,4} differ");
        foreach (var x in examples) Console.WriteLine($"      {x}");
    }
    return 0;
}

// Every mineable rock's radar signature, grouped by value, and which counts of
// which rocks a ping could not tell apart. The numbers behind docs/mining.md's
// radar section.
if (args.Contains("--signatures", StringComparer.OrdinalIgnoreCase))
{
    var data = GetOption(args, "--data") ?? Quantumwake.Core.AppPaths.Root;
    var mining = GameCommodities.Load(install.RootPath, Path.Combine(data, "commodities.json")).Mining;
    var rocks = mining.Signatures ?? [];
    Console.WriteLine($"{rocks.Count} mineable entities with a signature, {rocks.Count(r => r.Placed)} placed by a preset; {rocks.Select(r => r.Signature).Distinct().Count()} distinct values");
    if (args.Contains("--all", StringComparer.OrdinalIgnoreCase))
    {
        foreach (var r in rocks) Console.WriteLine($"  {r.Signature,7}  {(r.Placed ? "placed" : "      ")}  {r.Class}  {r.Composition}");
        return 0;
    }
    var radar = GameRockSignatures.Radar(rocks);
    Console.WriteLine($"{radar.Count} radar rows, {radar.Select(r => r.Signature).Distinct().Count()} values a pilot can meet");
    foreach (var r in radar)
    {
        var where = r.Surface && r.Asteroid ? "both" : r.Surface ? "surface" : "asteroid";
        var stones = r.Stones.Count > 0 ? $"  stones: {string.Join(", ", r.Stones)}" : "";
        Console.WriteLine($"  {r.Signature,7}  {r.Kind,-8} {r.Name,-24} {r.Tier,-10} {where,-8} {string.Join(", ", r.Ores)}{stones}");
    }

    // Counts of different rocks that read the same: a ping no reading can split.
    var ones = radar.Select(r => (r.Signature, r.Name)).Distinct().ToList();
    var sums = ones.SelectMany(o => Enumerable.Range(1, 12).Select(n => (Total: o.Signature * n, Read: $"{n}x {o.Name}")))
        .GroupBy(x => x.Total).Where(g => g.Count() > 1).OrderBy(g => g.Key).ToList();
    Console.WriteLine($"{sums.Count} totals up to 12 rocks that two readings share; the first ten:");
    foreach (var g in sums.Take(10)) Console.WriteLine($"  {g.Key,7}  {string.Join(" = ", g.Select(x => x.Read))}");
    return 0;
}

// Every contract's details as the Missions page shows them - contractor,
// title, type, where, CrimeStat, gates - and how many of the logged contracts
// they cover. The numbers behind docs/missions.md's catalogue section.
if (args.Contains("--mission-catalogue", StringComparer.OrdinalIgnoreCase))
{
    var data = GetOption(args, "--data") ?? Quantumwake.Core.AppPaths.Root;
    var details = GameCommodities.Load(install.RootPath, Path.Combine(data, "commodities.json")).Reputation.Details ?? GameMissionCatalogue.Empty;
    var all = details.Missions;
    var released = all.Where(m => !m.Unreleased).ToList();
    string Pct(int n) => released.Count == 0 ? "0%" : $"{100.0 * n / released.Count:F0}%";

    Console.WriteLine($"{all.Count} contracts; {all.Count - released.Count} unreleased; {released.Count} released; {details.Texts.Count} text keys");
    Console.WriteLine($"of the released: title {Pct(released.Count(m => m.TitleKey is not null))}, briefing {Pct(released.Count(m => m.DescriptionKey is not null))}, "
        + $"contractor {Pct(released.Count(m => m.ContractorKey is not null))}, type {Pct(released.Count(m => m.TypeKey is not null))}, "
        + $"system {Pct(released.Count(m => m.Systems.Count > 0))}, location {Pct(released.Count(m => m.Locations.Count > 0))}, "
        + $"CrimeStat {Pct(released.Count(m => m.MaxCrimeStat is not null))}, gates {Pct(released.Count(m => m.Gates.Count > 0))}, "
        + $"series {Pct(released.Count(m => m.RequiresCompleted is not null))}, minutes {Pct(released.Count(m => m.Minutes is not null))}, "
        + $"difficulty {Pct(released.Count(m => m.Difficulty is not null))}, illegal {released.Count(m => m.Illegal)}, buy-in {released.Count(m => m.BuyIn is not null)}");

    Console.WriteLine("types: " + string.Join(", ", released.GroupBy(m => details.Text(m.TypeKey) ?? "(none)").OrderByDescending(g => g.Count()).Take(20).Select(g => $"{g.Key} {g.Count()}")));
    Console.WriteLine("areas: " + string.Join("; ", details.Areas.OrderBy(a => a.Key).Select(a => $"{a.Key} = {a.Value.Name}")));
    Console.WriteLine("systems: " + string.Join(", ", released.SelectMany(m => m.Systems.DefaultIfEmpty("(none)")).GroupBy(s => s).OrderByDescending(g => g.Count()).Take(20).Select(g => $"{g.Key} {g.Count()}")));
    Console.WriteLine("contractors: " + string.Join(", ", released.GroupBy(m => details.Text(m.ContractorKey) ?? "(none)").OrderByDescending(g => g.Count()).Take(25).Select(g => $"{g.Key} {g.Count()}")));

    var contracts = new List<ContractRecord>();
    foreach (var file in install.BackupLogs().Concat(install.HasGameLog ? [install.GameLogPath] : []))
    {
        var builder = new SessionBuilder(Path.GetFileName(file));
        foreach (var ev in LogFileReader.ReadEvents(file, new LogEventParser())) builder.Add(ev);
        contracts.AddRange(builder.Build().Contracts);
    }
    var byId = all.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
    var withId = contracts.Where(c => c.DefinitionId is not null).ToList();
    var found = withId.Where(c => byId.ContainsKey(c.DefinitionId!)).ToList();
    var sameName = found.Count(c => string.Equals(byId[c.DefinitionId!].Name, c.Raw, StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"\n{contracts.Count} logged contracts; {withId.Count} with a definition id; {found.Count} found; debug name agrees on {sameName}");
    Console.WriteLine($"logged contracts the files give a title for: {found.Count(c => byId[c.DefinitionId!].TitleKey is not null)}; distinct definitions flown {found.Select(c => c.DefinitionId).Distinct().Count()}");
    foreach (var c in found.GroupBy(c => c.DefinitionId).Take(6).Select(g => g.First()))
    {
        var m = byId[c.DefinitionId!];
        Console.WriteLine($"  {m.Name,-50} {details.Text(m.ContractorKey) ?? "-",-28} {details.Text(m.TypeKey) ?? "-",-12} [{string.Join(",", m.Systems)}] crime {m.MinCrimeStat}-{m.MaxCrimeStat} {m.Minutes} min");
        Console.WriteLine($"     files: {details.Text(m.TitleKey)}");
        Console.WriteLine($"     log:   {c.Title}");
    }

    return 0;
}

// The NPC encounters the install describes: which mission scripts name them,
// how the game files the rest, and what their ships carry at the hold sizes
// the install gives. The numbers behind docs/encounters.md.
if (args.Contains("--encounters", StringComparer.OrdinalIgnoreCase))
{
    var data = GetOption(args, "--data") ?? Quantumwake.Core.AppPaths.Root;
    var game = GameCommodities.Load(install.RootPath, Path.Combine(data, "commodities.json"));
    var found = game.Loot.Encounters;
    var all = found.Encounters;
    var ships = all.SelectMany(e => e.Waves.SelectMany(w => w.Ships.Select(s => (Encounter: e, Ship: s)))).ToList();
    var carrying = ships.Where(x => x.Ship.Cargo.Count > 0).ToList();
    var unused = found.Unused().ToList();

    Console.WriteLine($"{all.Count} encounters; {all.Count(e => e.ArenaCommander)} Arena Commander; {found.ScriptsRead} mission scripts read");
    Console.WriteLine($"named by a mission script: {all.Count(e => e.Scripts.Count > 0)} ({all.Count(e => e.Scripts.Count > 0 && e.ArenaCommander)} of them Arena Commander); named by none: {all.Count(e => e.Scripts.Count == 0)}");
    Console.WriteLine($"{ships.Count} ship entries; {ships.Count(x => x.Ship.Ship is null)} name no ship class; {carrying.Count} in {carrying.Select(x => x.Encounter.Name).Distinct().Count()} encounters carry cargo");
    Console.WriteLine($"{found.Manifests.Count} cargo manifests; {found.Manifests.Count - unused.Count} carried; {unused.Count} carried by nothing\n");

    Console.WriteLine("by mission script:");
    foreach (var g in all.Where(e => e.Scripts.Count > 0).GroupBy(e => string.Join("/", e.Scripts[0].Split('/').Take(3))).OrderByDescending(g => g.Count()))
        Console.WriteLine($"  {g.Count(),3}  {g.Key}");
    Console.WriteLine("\nnamed by no script, by the game's folder:");
    foreach (var g in all.Where(e => e.Scripts.Count == 0).GroupBy(e => e.Folder).OrderByDescending(g => g.Count()))
        Console.WriteLine($"  {g.Count(),3}  {g.Key}");

    Console.WriteLine("\nlargest loads (hold x fullest fill):");
    var loads = carrying
        .Select(x => (x.Ship.Ship, Hold: GameEncounters.Hold(x.Ship.Ship, game.CargoHulls), Manifest: found.Manifests.GetValueOrDefault(x.Ship.Cargo[0]), x.Encounter.Name))
        .Where(x => x.Manifest is not null)
        .GroupBy(x => (x.Ship, x.Manifest!.Name))
        .Select(g => (g.Key.Ship, g.First().Hold, g.First().Manifest, Encounters: g.Select(x => x.Name).Distinct().Count()))
        .ToList();
    foreach (var l in loads.OrderByDescending(l => (l.Hold?.Scu ?? 0) * l.Manifest!.MaxFill).Take(12))
        Console.WriteLine($"  {l.Ship ?? "(no class)",-38} hold {(l.Hold?.Scu.ToString() ?? "?"),5} SCU  {l.Manifest!.MinFill:P0}-{l.Manifest.MaxFill:P0}  up to {(l.Hold is { } h ? Math.Floor(h.Scu * l.Manifest.MaxFill).ToString() : "?"),4} SCU {string.Join("/", l.Manifest.Goods.Select(g => g.Commodity))}  in {l.Encounters}");
    Console.WriteLine($"  carrying ship kinds with no hold in the install: {loads.Where(l => l.Hold is null).Select(l => l.Ship ?? "(no class)").Distinct().Count()} of {loads.Select(l => l.Ship ?? "(no class)").Distinct().Count()}");

    Console.WriteLine("\ncarried by nothing:");
    foreach (var g in unused.GroupBy(m => m.Name.Split('_')[0]))
        Console.WriteLine($"  {g.Count(),3}  {g.Key}: {g.Min(m => m.MinFill):P0}-{g.Max(m => m.MaxFill):P0} {string.Join("/", g.SelectMany(m => m.Goods.Select(x => x.Commodity)).Distinct().Take(6))}");

    return 0;
}

// What every logged contract pays in rep by the installed game, joined on the
// marker's contractDefinitionId, and checked against the StarStrings tags in
// the titles - which the files never saw. The numbers behind docs/missions.md;
// run it again after a patch, since a patch can move every id.
if (args.Contains("--missions", StringComparer.OrdinalIgnoreCase))
{
    var data = GetOption(args, "--data") ?? Quantumwake.Core.AppPaths.Root;
    var catalogue = GameCommodities.Load(install.RootPath, Path.Combine(data, "commodities.json")).Reputation;
    var missions = catalogue.Missions ?? new Dictionary<string, GameMissionPay>();
    var contracts = new List<ContractRecord>();
    var receipts = new List<(BlueprintReceipt Receipt, ContractRecord? After)>();

    foreach (var file in install.BackupLogs().Concat(install.HasGameLog ? [install.GameLogPath] : []))
    {
        var builder = new SessionBuilder(Path.GetFileName(file));
        foreach (var ev in LogFileReader.ReadEvents(file, new LogEventParser())) builder.Add(ev);
        var session = builder.Build();
        contracts.AddRange(session.Contracts);

        // A blueprint lands within a second of the completion that paid it.
        foreach (var receipt in session.Blueprints)
            receipts.Add((receipt, session.Contracts
                .Where(c => c.CompletedAt is { } done && receipt.At - done >= TimeSpan.FromSeconds(-5) && receipt.At - done <= TimeSpan.FromSeconds(30))
                .OrderByDescending(c => c.CompletedAt)
                .FirstOrDefault()));
    }

    var known = contracts.Where(c => catalogue.Mission(c.DefinitionId) is not null).ToList();
    var paying = known.Where(c => catalogue.Mission(c.DefinitionId)!.OnCompletion is > 0).ToList();

    Console.WriteLine($"{missions.Count} contracts in the install; {missions.Values.Count(m => m.Rep.Count > 0)} move a ladder, "
        + $"{missions.Values.Count(m => m.Blueprint is > 0)} can award a blueprint, {missions.Values.Count(m => m.Reward is not null)} pay a fixed sum");
    Console.WriteLine($"{contracts.Count} logged contracts; {contracts.Count(c => c.DefinitionId is not null)} carry a definition id; "
        + $"{known.Count} the install knows; {paying.Count} pay rep on completion");

    // The check: a single-figure tag the mod wrote against the game's figure.
    var tagged = known.Where(c => ContractTags.RepFrom(c.Title) is not null).ToList();
    var agree = tagged.Count(c => ContractTags.RepFrom(c.Title) == catalogue.Mission(c.DefinitionId)!.OnCompletion);
    Console.WriteLine($"\nStarStrings single-figure tags: {tagged.Count}; the install agrees on {agree}");
    foreach (var c in tagged.Where(c => ContractTags.RepFrom(c.Title) != catalogue.Mission(c.DefinitionId)!.OnCompletion))
        Console.WriteLine($"  ! {c.Raw}: tag {ContractTags.RepFrom(c.Title)}, game {catalogue.Mission(c.DefinitionId)!.OnCompletion}");

    var laddered = known.Where(c => ContractTags.LadderFrom(c.Title) is not null).ToList();
    var onLadder = laddered.Count(c => ContractTags.LadderFrom(c.Title)!.Contains(catalogue.Mission(c.DefinitionId)!.OnCompletion ?? -1));
    Console.WriteLine($"StarStrings ladder tags: {laddered.Count}; the game's figure is one of the ladder's on {onLadder}");

    var blueprintTags = known.Count(c => ContractTags.AwardsBlueprint(c.Title));
    var blueprintBoth = known.Count(c => ContractTags.AwardsBlueprint(c.Title) && catalogue.Mission(c.DefinitionId)!.Blueprint is > 0);
    Console.WriteLine($"[BP] tags: {blueprintTags}; the install has a blueprint result on {blueprintBoth}");

    // The ground truth for blueprints is the toast that says one arrived.
    var paired = receipts.Where(r => r.After is not null).ToList();
    Console.WriteLine($"Received Blueprint toasts: {receipts.Count}; after a completion {paired.Count}; "
        + $"the install predicted {paired.Count(r => catalogue.Mission(r.After!.DefinitionId)?.Blueprint is > 0)}, "
        + $"a [BP] tag {paired.Count(r => ContractTags.AwardsBlueprint(r.After!.Title))}");
    var tagOnly = contracts.Where(c => c.Outcome == ContractOutcome.Completed && ContractTags.AwardsBlueprint(c.Title)
        && catalogue.Mission(c.DefinitionId)?.Blueprint is not > 0).ToList();
    Console.WriteLine($"  completed with a [BP] tag the install does not back: {tagOnly.Count}; followed by a blueprint: "
        + $"{tagOnly.Count(c => paired.Any(r => r.After == c))}");

    // Hauling titles: what the overlay would write into the game's own text,
    // set beside what StarStrings wrote for the same key - the mod chose its
    // wording from the key's name, this from the contracts under the key.
    var haulTitles = catalogue.HaulTitles ?? new Dictionary<string, string>();
    var vanillaRaw = new P4kArchive(P4kArchive.PathFor(install.RootPath)).TryRead(@"Data\Localization\english\global.ini");
    if (vanillaRaw is not null)
    {
        var vanilla = GameText.WithoutBom(System.Text.Encoding.UTF8.GetString(vanillaRaw));
        var plan = TextOverlay.Build(vanilla, _ => true, null, new TextOverlayOptions(Facts: false), haulTitles);
        var ours = plan.Changes.Where(c => c.Category == TextOverlay.HaulTitleCategory)
            .ToDictionary(c => c.ItemClass.EndsWith(",P") ? c.ItemClass[..^2] : c.ItemClass, c => c.Becomes, StringComparer.OrdinalIgnoreCase);

        var loose = Path.Combine(install.RootPath, @"Data\Localization\english\global.ini");
        var mod = File.Exists(loose)
            ? File.ReadLines(loose).Select(l => l.TrimStart('﻿')).Where(l => l.Contains('='))
                .Select(l => (Key: l[..l.IndexOf('=')], Value: l[(l.IndexOf('=') + 1)..]))
                .Where(x => x.Value.Contains("~mission(Location") || x.Value.Contains("~mission(Destination"))
                .GroupBy(x => x.Key.EndsWith(",P") ? x.Key[..^2] : x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase)
            : [];

        static string Ends(string v) =>
            v.Contains("~mission(Location|name) > ~mission(Destination|name)") ? "A > B"
            : v.Contains("to ~mission(Destination") ? "to" : v.Contains("from ~mission(Location") ? "from" : "-";

        Console.WriteLine($"\nhauling title keys: {haulTitles.Count} ({string.Join(", ", haulTitles.Values.GroupBy(v => v).Select(g => $"{g.Key} {g.Count()}"))}); "
            + $"the overlay rewrites {plan.Routed} lines of the game's own text");
        var shared = ours.Keys.Where(mod.ContainsKey).ToList();
        Console.WriteLine($"  keys StarStrings also placed: {shared.Count}; same ends on {shared.Count(k => Ends(ours[k]) == Ends(mod[k]))}");
        foreach (var k in shared.Where(k => Ends(ours[k]) != Ends(mod[k])))
            Console.WriteLine($"    {k}: ours {Ends(ours[k])}, the mod's {Ends(mod[k])}");
        foreach (var k in ours.Keys.Where(k => !mod.ContainsKey(k)))
            Console.WriteLine($"    only ours: {k} ({Ends(ours[k])})");
    }

    Console.WriteLine("\nrep on completion, by issuer:");
    foreach (var g in paying.GroupBy(c => (c.Issuer, Rep: catalogue.Mission(c.DefinitionId)!.OnCompletion)).OrderBy(g => g.Key.Issuer).ThenBy(g => g.Key.Rep))
        Console.WriteLine($"  {g.Count(),4}  {g.Key.Issuer,-30} {g.Key.Rep,7:N0}");

    Console.WriteLine("\nlogged but paying no rep, by issuer:");
    foreach (var g in known.Except(paying).GroupBy(c => c.Issuer).OrderByDescending(g => g.Count()))
        Console.WriteLine($"  {g.Count(),4}  {g.Key}");

    foreach (var c in contracts.Where(c => c.DefinitionId is not null && catalogue.Mission(c.DefinitionId) is null).GroupBy(c => c.Raw).Take(20))
        Console.WriteLine($"  ? not in the install: {c.Key} x{c.Count()}");

    return 0;
}

// Hauling as the logs have it: every hauling contract folded out of the
// backups with what its title and archetype say about the route, and the
// totals that decide how much a screenshot still has to add. The evidence
// behind docs/hauling.md; run it again after the mod or the game changes a
// title format.
if (args.Contains("--hauling", StringComparer.OrdinalIgnoreCase))
{
    var hauls = new List<ContractRecord>();

    foreach (var file in install.BackupLogs().Concat(install.HasGameLog ? [install.GameLogPath] : []))
    {
        var builder = new SessionBuilder(Path.GetFileName(file));
        foreach (var ev in LogFileReader.ReadEvents(file, new LogEventParser())) builder.Add(ev);
        hauls.AddRange(builder.Build().Contracts.Where(c => HaulingContract.IsHauling(c.Raw)));
    }

    var titled = hauls.Count(c => c.Title is not null);
    var routes = hauls.Select(c => HaulingContract.RouteFromTitle(c.Title)).ToList();
    var shapes = hauls.Select(c => HaulingContract.FromArchetype(c.Raw)!).ToList();

    Console.WriteLine($"{hauls.Count} hauling contracts; {titled} with the toast's title, {hauls.Count - titled} marker only");
    Console.WriteLine($"  delivery named : {routes.Count(r => r?.Delivery is not null)}");
    Console.WriteLine($"  pickup named   : {routes.Count(r => r?.Pickup is not null)}");
    Console.WriteLine($"  both named     : {routes.Count(r => r is { Pickup: not null, Delivery: not null })}");
    Console.WriteLine($"  neither        : {routes.Count(r => r is null)}");
    Console.WriteLine($"  shape known    : {shapes.Count(s => s.Shape != HaulShape.Unknown)}   cargo named: {shapes.Count(s => s.Commodity is not null)}");
    Console.WriteLine($"  pickup steps   : {hauls.Sum(c => c.Pickups)} ({hauls.Sum(c => c.PickupsDone)} done)   drop-off steps: {hauls.Sum(c => c.Deliveries)} ({hauls.Sum(c => c.DeliveriesDone)} done)");

    Console.WriteLine("\nplaces named, by end:");
    foreach (var g in routes.Where(r => r is not null).SelectMany(r => new[] { ("to", r!.Delivery), ("from", r.Pickup) })
        .Where(x => x.Item2 is not null).GroupBy(x => x).OrderByDescending(g => g.Count()))
        Console.WriteLine($"  {g.Count(),4}  {g.Key.Item1,-4} {g.Key.Item2}");

    Console.WriteLine("\nshapes:");
    foreach (var g in shapes.GroupBy(s => (s.Shape, s.Pickups, s.Deliveries)).OrderByDescending(g => g.Count()))
        Console.WriteLine($"  {g.Count(),4}  {g.Key.Shape,-14} {g.Key.Pickups?.ToString() ?? "?"} -> {g.Key.Deliveries?.ToString() ?? "?"}");

    Console.WriteLine("\ncargo:");
    foreach (var g in shapes.GroupBy(s => s.Commodity ?? "(not spelled)").OrderByDescending(g => g.Count()))
        Console.WriteLine($"  {g.Count(),4}  {g.Key}");

    return 0;
}

// Loot as the install has it: every table resolved to items, which entries
// only a mission can fill, everything that rolls a table (slot presets and
// NPC bodies) and how much of what these logs saw in containers the tables
// can produce at all. The numbers behind docs/loot.md. --loot=<class> lists
// every container and body that can roll that one item, through its own
// constraints.
if (args.Any(a => a.StartsWith("--loot", StringComparison.OrdinalIgnoreCase)))
{
    var p4k = new P4kArchive(P4kArchive.PathFor(install.RootPath));
    var blob = p4k.TryRead(@"Data\Game2.dcb");
    if (blob is null) { Console.Error.WriteLine("no Game2.dcb"); return 1; }
    var core = new DataCore(blob);
    var loot = new GameLoot(core);
    var lootClass = args.First(a => a.StartsWith("--loot", StringComparison.OrdinalIgnoreCase)).Split('=', 2).ElementAtOrDefault(1);

    var tables = loot.Tables().Select(t => loot.Resolve(t)).OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
    var reachable = tables.SelectMany(t => t.Items).Select(i => i.ItemClass).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var sources = loot.Sources().ToList();
    var rolls = sources.GroupBy(s => s.RollKey).ToDictionary(g => g.Key, g => loot.Resolve(g.First()));

    var placesWatch = Stopwatch.StartNew();
    var english = GameCommodities.EnglishText(System.Text.Encoding.UTF8.GetString(p4k.TryRead(GameCommodities.LocalisationEntry) ?? []));
    var itemFacts = GameItems.Read(core, english);
    var digest = GameLootData.Read(core, p4k, english, c => itemFacts.TryGetValue(c, out var f) ? f.Type : null);
    placesWatch.Stop();

    if (lootClass is { Length: > 0 })
    {
        var rows = sources
            .Select(s => (Source: s, Odds: rolls[s.RollKey]?.Items
                .Where(i => i.ItemClass.Equals(lootClass, StringComparison.OrdinalIgnoreCase)).Sum(i => i.PerRoll) ?? 0))
            .Where(r => r.Odds > 0)
            .OrderByDescending(r => r.Odds * r.Source.SlotChance);

        foreach (var (s, odds) in rows)
            Console.WriteLine($"{odds,9:P3}/roll  slot {s.SlotChance,6:P1}  {(s.RespawnSeconds is { } r ? $"{r / 60}min" : "-"),6}  {s.Kind,-4} {s.Preset} / {s.Group} / {s.Container}  [{s.Table}]"
                + (s.Kind == "slot" ? $"  {digest.PlacesOf(s.Preset).Count} places" : ""));

        var placed = digest.PlacesFor(lootClass);
        Console.WriteLine($"\n{placed.Count} places, {placed.Count(p => p.Name is not null)} on the star map:");
        foreach (var p in placed.Take(40))
            Console.WriteLine($"  {p.Best.PerRoll * p.Best.Source.SlotChance,9:P3}  {p.Name ?? "(unmarked)",-40} {p.Body,-14} {p.Spots,3} sets  {p.Best.Source.Preset} / {p.Best.Source.Container}  [{p.Id}]");

        var flown = digest.ShipsFor(lootClass);
        Console.WriteLine($"\n{flown.Count} NPC ships fly with it fitted:");
        foreach (var s in flown)
            Console.WriteLine($"  x{s.Count}  {s.Ship,-50} {GameShipLoot.Crew(s.Ship)}");

        var worn = digest.NpcsFor(lootClass);
        Console.WriteLine($"\n{worn.Count} NPC classes wear or carry it (share if outfits are picked evenly):");
        foreach (var n in worn.Take(40))
            Console.WriteLine($"  {n.Share,7:P1}  {LootLabels.Npc(n.Npc),-50} {n.Npc}");
        return 0;
    }

    Console.WriteLine($"{tables.Count} loot tables, {reachable.Count} distinct items reachable");
    Console.WriteLine($"{sources.Count} sources: {sources.Count(s => s.Kind == "slot")} container slots in "
        + $"{sources.Where(s => s.Kind == "slot").Select(s => s.Preset).Distinct().Count()} presets, "
        + $"{sources.Count(s => s.Kind == "body")} NPC and corpse classes; {rolls.Count} distinct table-and-constraint rolls");
    foreach (var t in tables)
        Console.WriteLine($"  {t.Name,-50} {t.Items.Select(i => i.ItemClass).Distinct().Count(),5} items"
            + (t.Unfilled.Count > 0 ? $"  mission-only or empty: {string.Join("; ", t.Unfilled)}" : ""));

    // Checked against something the resolver never read: every class these
    // logs listed out of a Container-scope inventory. That is crates, bodies
    // and the pilot's own packs mixed together, so a miss is a question to
    // look at rather than an error.
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // And the places, against where the pilot had last set course before
    // opening a container: the logs never say where a crate stood, but a
    // quantum destination within the last two hours is where he went.
    var visits = new HashSet<(string File, string Item, string Destination)>();
    var seenAnywhere = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var file in install.BackupLogs().Concat(install.HasGameLog ? [install.GameLogPath] : []))
    {
        (DateTimeOffset At, string Id)? lastCourse = null;
        foreach (var ev in LogFileReader.ReadEvents(file, new LogEventParser()))
        {
            switch (ev)
            {
                case QuantumRouteEvent route: lastCourse = (route.Timestamp, route.Destination); break;
                case QuantumTargetEvent target: lastCourse = (target.Timestamp, target.Destination); break;
                case InventoryItemEvent { ScopeType: "Container" } item:
                    seen.Add(item.ItemClass);
                    seenAnywhere.Add(item.ItemClass);
                    if (lastCourse is { } c && item.Timestamp - c.At < TimeSpan.FromHours(2))
                        visits.Add((file, item.ItemClass, c.Id));
                    break;
                case InventoryItemEvent other:
                    seenAnywhere.Add(other.ItemClass);
                    break;
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Seen in a container in these logs: {seen.Count} classes, {seen.Count(reachable.Contains)} reachable from a table");
    foreach (var miss in seen.Where(c => !reachable.Contains(c)).Order(StringComparer.Ordinal))
        Console.WriteLine($"  in no table: {miss}");

    var slotPresets = sources.Where(s => s.Kind == "slot").Select(s => s.Preset).Distinct().ToList();
    var placeCount = digest.Places.Values.SelectMany(v => v).Select(p => (p.Name, p.Body)).Distinct().ToList();
    Console.WriteLine();
    Console.WriteLine($"Map files read in {placesWatch.Elapsed.TotalSeconds:F1}s (with the table read): {slotPresets.Count(digest.Places.ContainsKey)} of {slotPresets.Count} slot presets placed, "
        + $"in {placeCount.Count(p => p.Name is not null)} named places and unmarked spots on {placeCount.Where(p => p.Name is null).Select(p => p.Body).Distinct().Count()} bodies");
    Console.WriteLine($"  items with a named place: {reachable.Count(i => digest.PlacesFor(i).Any(p => p.Name is not null))} of {reachable.Count}; "
        + $"of the logged classes: {seen.Count(i => digest.PlacesFor(i).Any(p => p.Name is not null))} of {seen.Count}");
    foreach (var p in slotPresets.Where(p => !digest.Places.ContainsKey(p)).Order(StringComparer.Ordinal))
        Console.WriteLine($"  set down in no map file: {p}");

    // A destination is the place itself, or somewhere on its body: an id
    // carries its body as a prefix - Stanton1_UGF_HDRSO-Olliff is on Stanton1 -
    // except the orbital markers, which spell it OOC_Stanton_2b_Daymar.
    static string BodyOf(string id) =>
        Regex.Match(id, @"^OOC_([A-Za-z]+)_(\d+[a-z]?)_", RegexOptions.IgnoreCase) is { Success: true } m
            ? m.Groups[1].Value + m.Groups[2].Value
            : id.Split('_')[0];
    int atPlace = 0, onBody = 0, elsewhere = 0;
    var misses = new List<string>();
    foreach (var (_, item, destination) in visits.Distinct())
    {
        var places = digest.PlacesFor(item);
        if (places.Count == 0) continue;
        if (places.Any(p => string.Equals(p.Id, destination, StringComparison.OrdinalIgnoreCase))) atPlace++;
        else if (places.Any(p => p.Id is { } id && BodyOf(id).Equals(BodyOf(destination), StringComparison.OrdinalIgnoreCase))) onBody++;
        else { elsewhere++; misses.Add($"{item} after a course to {destination}"); }
    }
    Console.WriteLine($"Container pickups with a course set in the two hours before, for items with places: "
        + $"{atPlace + onBody + elsewhere}; at a listed place {atPlace}, on a listed place's body {onBody}, neither {elsewhere}");
    foreach (var m in misses) Console.WriteLine($"  neither: {m}");

    foreach (var g in visits.GroupBy(v => v.Destination).OrderByDescending(g => g.Count()))
        Console.WriteLine($"  {g.Count(),4} pickups after a course to {g.Key}: "
            + $"{g.Count(v => digest.PlacesFor(v.Item).Any(p => string.Equals(p.Id, g.Key, StringComparison.OrdinalIgnoreCase)))} listed there");
    // Ship parts: the loadouts against what the logs listed in any inventory.
    var npcShips = digest.Ships.Values.SelectMany(v => v).Select(s => s.Ship).Distinct().Count();
    Console.WriteLine($"\nNPC ships: {npcShips} carry {digest.Ships.Count} kinds of part worth taking "
        + $"({digest.Ships.Values.Sum(v => v.Count)} ship-and-part pairs)");
    foreach (var g in digest.Ships.GroupBy(kv => itemFacts.TryGetValue(kv.Key, out var f) ? f.Type : "?").OrderByDescending(g => g.Count()))
        Console.WriteLine($"  {g.Count(),5} {g.Key}");
    var loggedParts = seenAnywhere.Where(c => itemFacts.TryGetValue(c, out var f) && GameShipLoot.Kept.Contains(f.Type)).ToList();
    Console.WriteLine($"Ship parts these logs listed in any inventory: {loggedParts.Count}; fitted to some NPC ship: {loggedParts.Count(c => digest.ShipsFor(c).Count > 0)}");
    foreach (var c in loggedParts.Where(c => digest.ShipsFor(c).Count == 0).Order(StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"  on no NPC ship: {c} ({itemFacts[c].Type})");

    // NPC outfits, against the same container listings: a body is a
    // container in the logs, so what NPCs wear should turn up there - and
    // explain some of what the tables only give at a thousandth of a percent.
    var npcKinds = digest.Npcs.Values.SelectMany(v => v).Select(n => n.Npc).Distinct().ToList();
    Console.WriteLine($"\nNPC outfits: {digest.Npcs.Count} kinds of item worn or carried by {npcKinds.Count} NPC classes "
        + $"({npcKinds.Select(LootLabels.Npc).Distinct().Count()} kinds once gender and number are dropped); {digest.Npcs.Values.Sum(v => v.Count)} pairs");
    var wornSeen = seen.Where(c => digest.NpcsFor(c).Count > 0).ToList();
    Console.WriteLine($"Seen in a container in these logs: {seen.Count}; worn by some NPC {wornSeen.Count}; "
        + $"from a table {seen.Count(reachable.Contains)}; from either {seen.Count(c => reachable.Contains(c) || digest.NpcsFor(c).Count > 0)}");
    foreach (var c in seen.Where(c => !reachable.Contains(c) && digest.NpcsFor(c).Count == 0).Order(StringComparer.Ordinal))
        Console.WriteLine($"  from neither: {c}");
    var armourSeen = seen.Where(c => itemFacts.TryGetValue(c, out var f) && f.Type.StartsWith("Char_Armor", StringComparison.Ordinal)).ToList();
    Console.WriteLine($"Armour among them: {armourSeen.Count}; worn by some NPC {armourSeen.Count(c => digest.NpcsFor(c).Count > 0)}; "
        + $"best table roll under 0.1% for {armourSeen.Count(c => digest.For(c).Select(h => h.PerRoll).DefaultIfEmpty(0).Max() < 0.001)}");
    return 0;
}

if (args.Any(a => a.StartsWith("--armoury", StringComparison.OrdinalIgnoreCase)))
{
    var all = args.Contains("--armoury=all", StringComparer.OrdinalIgnoreCase);
    // --data points the cache at a copy, so a re-read after a reader change
    // does not land in the real data folder.
    var cache = Path.Combine(GetOption(args, "--data") ?? Path.GetDirectoryName(SessionStore.DatabasePathFor(install.RootPath))!, "commodities.json");
    var armoury = GameCommodities.Load(install.RootPath, cache).Armoury;

    static string Kinds(DamageKinds d) => string.Join("+", new[]
    {
        d.Physical > 0 ? $"{d.Physical:0.##} phys" : null, d.Energy > 0 ? $"{d.Energy:0.##} energy" : null,
        d.Distortion > 0 ? $"{d.Distortion:0.##} dist" : null, d.Thermal > 0 ? $"{d.Thermal:0.##} therm" : null,
        d.Biochemical > 0 ? $"{d.Biochemical:0.##} bio" : null, d.Stun > 0 ? $"{d.Stun:0.##} stun" : null,
    }.Where(s => s is not null));

    static string Mode(FireMode m) => m.Kind switch
    {
        "Beam" => $"{m.Name} {Kinds(m.BeamDamagePerSecond ?? DamageKinds.None)}/s to {m.BeamFullRange:0}m (none past {m.BeamZeroRange:0}m) {m.BeamAmmoPerSecond:0.#} rounds/s",
        "Charge" => $"{m.Name} {m.RoundsPerMinute:0} rpm, ×{m.ChargeDamageMultiplier:0.##} after {m.ChargeSeconds:0.##}s for ×{m.ChargeAmmoMultiplier:0.##} rounds",
        "Burst" => $"{m.Name} {m.BurstShots}×{m.Pellets} at {m.RoundsPerMinute:0} rpm, {m.BurstCooldown:0.##}s between (sustained {Armoury.SustainedRoundsPerMinute(m):0})",
        _ => $"{m.Name} {m.RoundsPerMinute:0} rpm" + (m.Pellets > 1 ? $" ×{m.Pellets} pellets" : "") + (m.AmmoPerShot != 1 ? $" {m.AmmoPerShot} rounds/shot" : ""),
    } + (m.Condition.Length > 0 ? $" when {m.Condition}" : "");

    var guns = armoury.Weapons.Where(w => all || w.BaseClass is null).ToList();
    Console.WriteLine($"{armoury.Weapons.Count} personal weapons, {armoury.Weapons.Count(w => w.BaseClass is null)} of them plain and the rest finishes of one");
    foreach (var w in guns)
    {
        var finishes = armoury.Weapons.Count(o => o.BaseClass == w.Class);
        Console.WriteLine($"  {w.Name,-38} {w.Kind,-16} {w.Weight,-6} S{w.Size} {w.Mass,5:0.##}kg  hit {Kinds(w.Damage),-22} {w.ProjectileSpeed,5:0} m/s for {w.ProjectileLifetime:0.#}s"
            + (w.DropStart > 0 ? $"  drops {w.DropPerMetre:0.###}/m past {w.DropStart:0}m to {w.DropFloor:0.##}" : "")
            + $"  mag {w.Magazine}"
            + (w.Explosion is { } x ? $"  blast {Kinds(x.Damage)} over {x.Radius:0.#}-{x.OuterRadius:0.#}m" : "")
            + (finishes > 0 ? $"  +{finishes} finishes" : "") + (w.BaseClass is not null ? $"  finish of {w.BaseClass}" : "")
            + $"  [{w.Class}] {w.Manufacturer}");
        foreach (var m in w.Modes)
            Console.WriteLine($"      {Mode(m),-80} {Armoury.DamagePerSecond(w, m),7:0} dps  {Armoury.DamagePerMagazine(w, m),7:0}/mag  empties in {Armoury.SecondsToEmpty(w, m),5:0.#}s");
    }

    // Every gun's attachment slots and what goes in each, by the files' own
    // rule (Armoury.Fits): the numbers behind docs/armoury.md's fitting section.
    if (args.Contains("--armoury=ports", StringComparer.OrdinalIgnoreCase))
    {
        var plainPieces = armoury.Attachments.Where(a => a.BaseClass is null).ToList();
        var plainGuns = armoury.Weapons.Where(w => w.BaseClass is null).ToList();
        Console.WriteLine($"\n{plainGuns.Count(w => w.Ports.Count > 0)} of {plainGuns.Count} plain guns have attachment slots; {plainGuns.Sum(w => w.Ports.Count)} slots in all");
        foreach (var slot in new[] { "Sight", "Barrel", "Underbarrel" })
            Console.WriteLine($"  {slot,-11} on {plainGuns.Count(w => w.Ports.Any(p => p.Slot == slot)),3} guns; fixed on {plainGuns.Count(w => w.Ports.Any(p => p.Slot == slot && p.Fixed)),2}; shipped filled on {plainGuns.Count(w => w.Ports.Any(p => p.Slot == slot && p.Default is not null)),2}");
        Console.WriteLine($"  pieces asking a tag of the port: {plainPieces.Count(a => a.RequiredTags.Count > 0)}; pieces that fit no gun: {plainPieces.Count(a => !plainGuns.Any(w => w.Ports.Any(p => Armoury.Fits(p, a))))}");
        foreach (var a in plainPieces.Where(a => !plainGuns.Any(w => w.Ports.Any(p => Armoury.Fits(p, a)))))
            Console.WriteLine($"      fits nothing: {a.Name} [{a.Class}] {a.Kind} S{a.Size} tags {string.Join(" ", a.Tags)}");
        foreach (var w in plainGuns)
        {
            Console.WriteLine($"  {w.Name,-38} [{w.Class}]");
            foreach (var p in w.Ports)
            {
                var fits = plainPieces.Where(a => Armoury.Fits(p, a)).ToList();
                Console.WriteLine($"      {p.Slot,-11} {p.Name,-22} S{p.MinSize}-{p.MaxSize} needs [{string.Join(" ", p.RequiredTags)}] offers [{string.Join(" ", p.PortTags)}]"
                    + (p.Fixed ? " FIXED" : "") + (p.Default is { } d ? $" ships with {d}" : "") + $"  {fits.Count} fit");
            }
        }
        var p4ar = plainGuns.FirstOrDefault(w => w.Class == "behr_rifle_ballistic_01");
        var tacit = plainPieces.FirstOrDefault(a => a.Name.StartsWith("Tacit Suppressor", StringComparison.OrdinalIgnoreCase) && p4ar?.Ports.Any(p => Armoury.Fits(p, a)) == true);
        if (p4ar is not null && tacit is not null)
        {
            var fitted = Armoury.Fitted(p4ar, tacit.Effect);
            var m = p4ar.Modes[0]; var fm = fitted.Modes[0];
            Console.WriteLine($"  P4-AR with {tacit.Name}: {Armoury.DamagePerSecond(p4ar, m):0.#} -> {Armoury.DamagePerSecond(fitted, fm):0.#} dps, {Armoury.DamagePerMagazine(p4ar, m):0} -> {Armoury.DamagePerMagazine(fitted, fm):0}/mag");
        }
    }

    var knives = armoury.Melee.Where(k => all || k.BaseClass is null).ToList();
    Console.WriteLine($"\n{armoury.Melee.Count} knives, {armoury.Melee.Count(k => k.BaseClass is null)} plain, on {armoury.Melee.Select(k => k.Config).Distinct().Count()} melee config(s): {string.Join(", ", armoury.Melee.GroupBy(k => k.Config).Select(g => $"{g.Key} ×{g.Count()}"))}");
    foreach (var k in knives)
        Console.WriteLine($"  {k.Name,-38} slash {Kinds(k.Slash),-14} stab {Kinds(k.Stab),-14} impulse {k.Impulse:0.#} {k.Mass:0.##}kg"
            + (armoury.Melee.Count(o => o.BaseClass == k.Class) is var f && f > 0 ? $"  +{f} finishes" : "") + (k.BaseClass is not null ? $"  finish of {k.BaseClass}" : "")
            + $"  [{k.Class}] {k.Manufacturer}");

    Console.WriteLine($"\n{armoury.Throwables.Count} grenades");
    foreach (var g in armoury.Throwables)
        Console.WriteLine($"  {g.Name,-38} {g.Trigger}{(g.FuseSeconds > 0 ? $" {g.FuseSeconds:0.#}s" : "")}"
            + (g.Blast is { } b ? $"  blast {Kinds(b.Damage)} over {b.Radius:0.#}-{b.OuterRadius:0.#}m pressure {g.Pressure:0}" : "  no blast")
            + (g.Hazard is { } h ? $"  then {Kinds(h.PerHit)} every {h.PeriodSeconds:0.##}s within {h.Radius:0.##}m" : "")
            + $"  {g.Mass:0.##}kg [{g.Class}] {g.Manufacturer}");

    static string Effect(AttachmentEffect e) => string.Join("  ", new[]
    {
        e.Zoom > 0 ? $"zoom ×{e.Zoom:0.#}" + (e.SecondZoom > 0 ? $"/×{e.SecondZoom:0.#}" : "") : null,
        e.ZeroingMax > 0 ? $"zero to {e.ZeroingMax:0}m by {e.ZeroingStep:0}" : null,
        e.ZoomTime != 1 ? $"ads time ×{e.ZoomTime:0.##}" : null,
        e.Damage != 1 ? $"damage ×{e.Damage:0.###}" : null,
        e.FireRate != 1 ? $"rate ×{e.FireRate:0.###}" : null,
        e.Spread != 1 ? $"spread ×{e.Spread:0.###}" : null,
        e.RecoilStrength != 1 ? $"recoil ×{e.RecoilStrength:0.###}" : null,
        e.RecoilTime != 1 ? $"recoil time ×{e.RecoilTime:0.###}" : null,
        e.Sound != 1 ? $"sound ×{e.Sound:0.###}" : null,
        e.Heat != 1 ? $"heat ×{e.Heat:0.###}" : null,
        e.AmmoCost != 1 ? $"ammo ×{e.AmmoCost:0.###}" : null,
        e.ChargeTime != 1 ? $"charge ×{e.ChargeTime:0.###}" : null,
        e.ProjectileSpeed != 1 ? $"speed ×{e.ProjectileSpeed:0.###}" : null,
        e.Pellets != 0 ? $"pellets {e.Pellets:+0;-0}" : null,
        e.BurstShots != 0 ? $"burst {e.BurstShots:+0;-0}" : null,
    }.Where(s => s is not null));

    var attachments = armoury.Attachments.Where(a => all || a.BaseClass is null).ToList();
    Console.WriteLine($"\n{armoury.Attachments.Count} attachments, {armoury.Attachments.Count(a => a.BaseClass is null)} plain: {string.Join(", ", armoury.Attachments.Where(a => a.BaseClass is null).GroupBy(a => a.Kind).Select(g => $"{g.Count()} {g.Key.ToLowerInvariant()}"))}");
    foreach (var a in attachments)
        Console.WriteLine($"  {a.Name,-38} {a.Kind,-11} {a.Family,-13} S{a.Size} {a.Mass,4:0.##}kg  {(a.Effect.IsNone ? "changes nothing the files put a number on" : Effect(a.Effect))}"
            + (armoury.Attachments.Count(o => o.BaseClass == a.Class) is var f && f > 0 ? $"  +{f} finishes" : "") + (a.BaseClass is not null ? $"  finish of {a.BaseClass}" : "")
            + $"  [{a.Class}] {a.Manufacturer}");

    Console.WriteLine($"\n{armoury.Armour.Count} pieces of armour");
    foreach (var group in armoury.Armour.GroupBy(a => a.Resistances?.Macro ?? "(none)").OrderBy(g => g.Key))
    {
        var r = group.First().Resistances;
        Console.WriteLine(r is null
            ? $"  {group.Key}: {group.Count()} pieces, no resistance block"
            : $"  {group.Key}: {group.Count()} pieces at ×{r.Physical:0.###} phys ×{r.Energy:0.###} energy ×{r.Distortion:0.###} dist ×{r.Thermal:0.###} therm ×{r.Biochemical:0.###} bio ×{r.Stun:0.###} stun, impact ×{r.Impact:0.###}");
    }
    Console.WriteLine();
    foreach (var slot in armoury.Armour.GroupBy(a => a.Slot))
    {
        Console.WriteLine($"  {slot.Key}: {slot.Count()} pieces in {slot.Select(a => a.Family).Distinct(StringComparer.OrdinalIgnoreCase).Count()} sets");
        foreach (var family in slot.GroupBy(a => (a.Family, a.Weight, a.Resistances?.Macro, a.TemperatureMin, a.TemperatureMax, a.RadiationCapacity, a.CapacityMicroScu, a.EmSignature, a.IrSignature, a.Mass)).OrderBy(g => g.Key.Weight).ThenBy(g => g.Key.Family))
        {
            var a = family.First();
            Console.WriteLine($"    {a.Family,-34} {a.Weight,-6} {family.Count(),3}× {a.Resistances?.Macro ?? "-",-20} {a.TemperatureMin,4:0}..{a.TemperatureMax,-4:0}C rad {a.RadiationCapacity,6:0} -{a.RadiationDissipation:0.#}/s"
                + (a.GForceResistance != 0 ? $" g {a.GForceResistance:+0.##;-0.##}" : "")
                + (a.CapacityMicroScu > 0 ? $" carries {a.CapacityMicroScu / 1_000_000.0:0.###} SCU" : "")
                + $" em {a.EmSignature:0.#} ir {a.IrSignature:0.#} {a.Mass:0.#}kg" + (a.MotionPenalty > 0 ? $" broken -{a.MotionPenalty:P0} move" : "")
                + $"  {a.Manufacturer}" + (all ? "  " + string.Join(" | ", family.Select(p => p.Name)) : ""));
        }
    }

    return 0;
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
/// <summary>
/// The stock-fit comparison from docs/garage.md over the whole dump: computes
/// every spaceship's sheet from its parts and compares with the totals the
/// dump wrote beside them, which the sheet never reads.
/// </summary>
/// <param name="dumpDir">A folder holding ships.json and ship-items.json from scunpacked-data.</param>
static int GarageCheck(string dumpDir)
{
    var shipsPath = Path.Combine(dumpDir, "ships.json");
    var itemsPath = Path.Combine(dumpDir, "ship-items.json");

    if (!File.Exists(shipsPath) || !File.Exists(itemsPath))
    {
        Console.Error.WriteLine($"Expected ships.json and ship-items.json in {dumpDir}.");
        return 2;
    }

    var parts = CommunityData.DigestPartStats(File.ReadAllText(itemsPath));
    var ships = CommunityData.DigestShipStats(File.ReadAllText(shipsPath), parts);

    var counted = ships.Values.Where(s => s.IsSpaceship && s.Dataset.EmShields > 0).ToList();
    var tally = new Dictionary<string, (int Ok, int Tried)>();
    var misses = new List<string>();

    void Check(string figure, string ship, double got, double want, double tolerance)
    {
        if (want <= 0) return;
        var (ok, tried) = tally.GetValueOrDefault(figure);
        var hit = Math.Abs(got - want) / want <= tolerance;
        tally[figure] = (ok + (hit ? 1 : 0), tried + 1);
        if (!hit && misses.Count < 40) misses.Add($"  {figure,-14} {ship,-44} {got,14:0.#} vs {want,14:0.#}");
    }

    foreach (var ship in counted)
    {
        var sheet = ShipSheet.Compute(ship, parts);
        var d = ship.Dataset;

        Check("EM shields", ship.Name, sheet.Shields.Em, d.EmShields, 0.01);
        Check("EM quantum", ship.Name, sheet.Quantum.Em, d.EmQuantum, 0.01);
        Check("IR shields", ship.Name, sheet.Shields.Ir, d.IrShields, 0.01);
        Check("IR quantum", ship.Name, sheet.Quantum.Ir, d.IrQuantum, 0.01);
        Check("power seg", ship.Name, sheet.Power.Available, d.PowerSegments, 0);
        Check("cooling seg", ship.Name, sheet.Cooling.Generated, d.CoolingSegments, 0.01);
        Check("shield hp", ship.Name, sheet.Shield.Hp, d.ShieldHp, 0.01);
        Check("fixed dps", ship.Name, sheet.Weapons.FixedDps, d.FixedDps, 0.02);
        if (sheet.QuantumDrive is { } q) Check("qt range", ship.Name, q.Range, d.QuantumRange, 0.01);
        Check("mass", ship.Name, sheet.Mass, d.MassTotal, 0.02);
    }

    Console.WriteLine($"Garage check: {counted.Count} spaceships with a stock fit, {parts.Count} parts");
    Console.WriteLine();
    foreach (var (figure, (ok, tried)) in tally.OrderBy(t => t.Key))
        Console.WriteLine($"  {figure,-14} {ok,4} of {tried,-4} {(ok == tried ? "" : $"  ({tried - ok} miss)")}");

    if (misses.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Misses (first 40):");
        foreach (var line in misses) Console.WriteLine(line);
    }

    // Fixed-vs-turret is the one split the dump itself does not settle - see
    // ShipSheet.IsTurret - so it is reported but does not fail the check.
    return tally.Where(t => t.Key != "fixed dps").All(t => t.Value.Ok == t.Value.Tried) ? 0 : 1;
}

static async Task<int> OcrCheck(string folder, string installRoot, string? recogniser)
{
    var files = Directory.Exists(folder)
        ? Directory.GetFiles(folder, "*.jpg").Concat(Directory.GetFiles(folder, "*.png")).OrderBy(f => f).ToArray()
        : File.Exists(folder) ? [folder] : [];

    if (files.Length == 0)
    {
        Console.Error.WriteLine($"No screenshot at {folder}");
        return 1;
    }

    using var reader = new Quantumwake.Ocr.Onnx.OnnxScreenReader(
        recogniser == "ch" ? Quantumwake.Ocr.Onnx.OnnxModels.MultilingualRecogniser : null);

    // The install's own names, as the dashboard has them; the readings go to
    // a folder that is thrown away, so nothing here lands in anyone's history.
    using var library = new LogLibrary(SessionStore.DatabasePathFor(installRoot));
    library.LoadNames(installRoot);
    var scratch = Directory.CreateTempSubdirectory("qw-ocr-check-").FullName;
    var insight = new ScreenInsightService(library, new ScreenReadingStore(scratch), reader);

    Console.WriteLine($"Models  : {Quantumwake.Ocr.Onnx.OnnxModels.Folder} ({(reader.Ready ? "present" : "to fetch")})");
    Console.WriteLine($"Frames  : {files.Length}");

    var kinds = new SortedDictionary<string, int>();

    try
    {
        foreach (var file in files)
        {
            ScreenSighting sighting;

            try
            {
                sighting = await insight.ReadShotAsync(file);
            }
            catch (ScreenEngineUnavailableException e)
            {
                // Not a measurement: every frame would have come out Unknown.
                Console.Error.WriteLine($"No frame read: {e.Message} ({e.InnerException?.Message})");
                return 1;
            }

            kinds[sighting.Kind.ToString()] = kinds.GetValueOrDefault(sighting.Kind.ToString()) + 1;

            var extras = new List<string>();
            if (sighting.Wallet is not null) extras.Add("wallet");
            if (sighting.Contracts is not null) extras.Add("contracts");
            if (sighting.Reputation is not null) extras.Add("rep");
            if (sighting.Mining is not null) extras.Add("mining");
            if (sighting.Refinery is not null) extras.Add("refinery");
            if (sighting.Kiosk is not null) extras.Add("kiosk");
            if (sighting.Fleet is not null) extras.Add("fleet");
            if (sighting.Loadout is not null) extras.Add("loadout");
            if (sighting.Map is not null) extras.Add("map");
            if (sighting.Item is not null) extras.Add("item");

            Console.WriteLine($"{Path.GetFileName(file),-44} {sighting.TookMs,6} ms {sighting.Kind,-10} {sighting.Summary}"
                + (extras.Count > 0 ? $"  [{string.Join(",", extras)}]" : ""));
        }

        Console.WriteLine();
        Console.WriteLine("Frames by kind");
        Console.WriteLine("--------------");
        foreach (var (kind, count) in kinds)
            Console.WriteLine($"  {count,4}  {kind}");

        return 0;
    }
    finally
    {
        Directory.Delete(scratch, recursive: true);
    }
}

static async Task<int> Ocr(string target, string? outDir, bool show, string? recogniser)
{
    var files = Directory.Exists(target)
        ? Directory.GetFiles(target, "*.jpg").Concat(Directory.GetFiles(target, "*.png")).OrderBy(f => f).ToArray()
        : File.Exists(target) ? [target] : [];

    if (files.Length == 0)
    {
        Console.Error.WriteLine($"No screenshot at {target}");
        return 1;
    }

    long lastPercent = -1;
    using var reader = new Quantumwake.Ocr.Onnx.OnnxScreenReader(
        recogniser == "ch" ? Quantumwake.Ocr.Onnx.OnnxModels.MultilingualRecogniser : null)
    {
        Progress = new Progress<(Quantumwake.Ocr.Onnx.OnnxModel Model, long Received)>(p =>
        {
            // Once per percent, not once per buffer: a log of this is read, not watched.
            var percent = p.Received * 100 / p.Model.Bytes;
            if (percent != lastPercent)
                Console.Error.WriteLine($"  fetching {p.Model.FileName} {percent,3}%");
            lastPercent = percent;
        }),
    };

    Console.WriteLine($"Models  : {Quantumwake.Ocr.Onnx.OnnxModels.Folder} ({(reader.Ready ? "present" : "to fetch")})");
    Console.WriteLine($"Frames  : {files.Length}");

    if (outDir is not null) Directory.CreateDirectory(outDir);

    var total = System.Diagnostics.Stopwatch.StartNew();
    long lineCount = 0;

    foreach (var file in files)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var lines = await reader.ReadAsync(file);
        watch.Stop();
        lineCount += lines.Count;

        Console.WriteLine($"{Path.GetFileName(file),-44} {watch.ElapsedMilliseconds,6} ms {lines.Count,4} lines");

        if (show)
            foreach (var line in lines)
                Console.WriteLine($"    {line.Left,6:0} {line.Top,6:0} {line.Height,4:0}  {line.Text}");

        if (outDir is not null)
            await File.WriteAllLinesAsync(
                Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + ".txt"),
                // The probe's own format, so --screen reads these files back.
                lines.Select(l => $"[{l.Left,5:0} {l.Top,5:0} h{l.Height:0}]  {l.Text}"));
    }

    Console.WriteLine($"Total   : {files.Length} frames, {lineCount} lines, {total.Elapsed.TotalSeconds:0.0} s");
    return 0;
}

static int Screen(string linesFile, string installRoot, string? catalogueQuery)
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
    var frame = ScreenFrames.Read(lines, items, shipNames, commodities);

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
        foreach (var s in contracts.Steps ?? [])
            Console.WriteLine($"  {new string(' ', s.Depth * 2)}step {s.Kind,-8} {s.Commodity ?? "-",-22} {(s.Kind == "collect" ? "from" : "to"),-4} {s.Place ?? "-"}{(s.Body is null ? "" : $" ({s.Body})")}{(s.Total is null ? "" : $"  {s.Done}/{s.Total} SCU")}");
        foreach (var site in contracts.PickupSites ?? [])
            Console.WriteLine($"  pickup site {site.Place}{(site.Body is null ? "" : $" on {site.Body}")}");
        var (legs, deliveries) = HaulLegs.From(contracts);
        foreach (var leg in legs)
            Console.WriteLine($"  leg  {leg.Pickup ?? "?"}{(leg.PickupBody is null ? "" : $" ({leg.PickupBody})")} -> {leg.Delivery ?? "?"}{(leg.DeliveryBody is null ? "" : $" ({leg.DeliveryBody})")}  {leg.Commodity ?? "?"}  {(leg.Scu is null ? "share not printed" : $"{leg.ScuDone}/{leg.Scu} SCU")}");
        foreach (var d in deliveries)
            Console.WriteLine($"  total {d.ScuDone}/{d.Scu} SCU of {d.Commodity ?? "?"} to {d.Place}");
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
    private readonly Dictionary<string, int> _shards = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _shardRegions = new(StringComparer.Ordinal);
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

            case ShardJoinEvent join:
                Bump(_shards, join.Shard);
                Bump(_shardRegions, ShardName.TryParse(join.Shard, out var shard) ? shard.Region : "(unparsed)");
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
        Console.WriteLine($"  client sessions : {_sessionIds.Count}");
        Console.WriteLine($"  shard joins     : {_shards.Values.Sum()} on {_shards.Count} distinct shards");
        foreach (var (region, count) in _shardRegions.OrderByDescending(p => p.Value))
            Console.WriteLine($"  {region,-24} {count,6} joins");
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
