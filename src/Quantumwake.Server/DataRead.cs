using Quantumwake.Core;
using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// Settings → What was read: every place the app reads from, what it found
/// there, and where that now sits on disk.
/// </summary>
/// <remarks>
/// <para>
/// The pieces all existed - the Log page knew which files were read, Settings
/// knew whether the dataset was on, Diagnostics knew the session count - but
/// a pilot asking "what does this app actually know about me, and from where"
/// had to visit five pages and add it up. This is the one answer: one block
/// per source, in the order the app trusts them, with the folder at the end
/// so the whole of it can be seen, backed up or deleted knowingly.
/// </para>
/// <para>
/// Everything here is read from the stores as they are; nothing is cached, so
/// the page is as fresh as a reload. The folder walk is a few hundred files
/// and takes milliseconds; the OCR models are the only large entries.
/// </para>
/// </remarks>
public static partial class ServerHost
{
    static void MapDataRead(WebApplication app, GameInstall? install)
    {
        app.MapGet("/api/data", (
            LogLibrary lib, GameDataStatus gameData, UexData uex, UexFeeds feeds,
            ScreenReadingStore readings, ScreenInsightService insight, ScreenSettingsStore screens,
            JobStore jobs, ChecklistStore checklists, TripStore trips, MapNoteStore notes, ShardNoteStore shards,
            BuildStore builds, KitStore kits, ImportStore imports, ControlsStore controls, WipeStore wipes,
            TextOverlayStore labels) =>
        {
            var history = ScanHistory.Build(install, lib.Store);
            var sessions = lib.Store.All();
            var lastScan = lib.Store.Scans(1).FirstOrDefault();
            var game = lib.GameCommodities;
            var community = lib.Community;
            var sightings = readings.All();
            var allJobs = jobs.All();
            var allTrips = trips.All();

            return Results.Ok(new
            {
                takenAt = DateTimeOffset.UtcNow,

                // Game.log and its backups: the record this app is built on.
                logs = new
                {
                    found = install is not null,
                    channel = install?.Channel,
                    root = install?.RootPath,
                    hasGameLog = install?.HasGameLog ?? false,
                    files = new
                    {
                        onDisk = history.OnDisk,
                        current = history.Files.Count(f => f.State == ScanHistory.Current),
                        changed = history.Files.Count(f => f.State == ScanHistory.Changed),
                        unread = history.Files.Count(f => f.State == ScanHistory.Unread),
                        gone = history.Files.Count(f => f.State == ScanHistory.Gone),
                        bytes = history.Files.Sum(f => f.Bytes ?? 0),
                    },
                    sessions = new
                    {
                        total = sessions.Count,
                        counted = sessions.Count - lib.SessionsBeforeWipe(),
                        first = sessions.Count > 0 ? sessions.Min(s => s.StartedAt) : (DateTimeOffset?)null,
                        last = sessions.Count > 0 ? sessions.Max(s => s.EndedAt) : (DateTimeOffset?)null,
                        builds = sessions
                            .Select(s => CommunityData.BuildIn(s.BuildTag))
                            .Where(b => b is not null)
                            .GroupBy(b => b!, StringComparer.Ordinal)
                            .OrderByDescending(g => g.Count())
                            .Select(g => new { build = g.Key, sessions = g.Count() }),
                    },
                    lastScan = lastScan is null ? null : new
                    {
                        startedAt = lastScan.StartedAt,
                        finishedAt = lastScan.FinishedAt,
                        files = lastScan.Files,
                        parsed = lastScan.Parsed,
                        forced = lastScan.Forced,
                    },
                    // Always sent; the page shows it only when it hides something.
                    wipe = new
                    {
                        at = wipes.Current.At,
                        patch = wipes.Current.Patch,
                        scope = wipes.Current.Scope.ToString(),
                        hidden = lib.SessionsBeforeWipe(),
                    },
                },

                // The install's own files: names, catalogues, loot tables.
                gameFiles = new
                {
                    status = gameData.Snapshot(),
                    root = install?.RootPath,
                    names = new { loaded = lib.Names.IsLoaded, items = lib.Names.ItemCount, vehicles = lib.Names.VehicleCount, places = lib.Names.PlaceCount },
                    catalogues = new
                    {
                        items = game.FactCount,
                        commodities = game.Count,
                        vehicles = game.Vehicles.Count,
                        places = game.PlaceCount,
                        recipes = game.Blueprints.Count,
                        deposits = game.Spawns.Count,
                        paints = game.Paints.Count,
                        cargoHulls = game.CargoHulls.Count,
                        crates = game.Crates.Count,
                        wikeloTrades = game.Wikelo.Trades.Count,
                        weapons = game.Armoury.Weapons.Count,
                        armour = game.Armoury.Armour.Count,
                    },
                },

                // The optional download: hull specs, part figures, commodity names.
                community = new
                {
                    enabled = community.IsEnabled,
                    fetchedAt = community.FetchedAt,
                    dump = community.Dump,
                    commodities = community.Count,
                    ships = community.Ships.Count,
                    items = community.Items.Count,
                    blueprints = community.Blueprints.Count,
                },

                // UEX: prices, and each feed with its own switch.
                uex = new
                {
                    enabled = uex.IsEnabled,
                    prices = uex.Count,
                    hasCredentials = uex.HasCredentials,
                    feeds = UexFeeds.All.Select(f => new { f.Key, f.Title, enabled = feeds.IsEnabled(f.Key), fetchedAt = feeds.FetchedAt(f.Key) }),
                },

                // Screenshots and copied locations: what the pilot handed over.
                screens = new
                {
                    canRead = insight.CanReadScreenshots,
                    watching = screens.Current.Mode == ScreenMode.Screenshots && screens.Current.WatchScreenshots,
                    readings = sightings.Count,
                    latest = readings.Latest?.ShotAt,
                    byKind = sightings
                        .GroupBy(s => s.Kind.ToString())
                        .OrderByDescending(g => g.Count())
                        .Select(g => new { kind = g.Key, count = g.Count(), last = g.Max(s => s.ShotAt) }),
                    reputation = readings.ReputationHistory().Count,
                    clipboards = readings.Clipboards().Count,
                    pinned = readings.Pinned().Count,
                },

                // What the pilot wrote: never read from anywhere, so never re-read.
                yours = new
                {
                    lists = allJobs.Count,
                    listsOpen = allJobs.Count(j => !j.Done),
                    checklists = checklists.All().Count,
                    plans = allTrips.Count,
                    plansFlying = allTrips.Count(t => t.Flying),
                    mapNotes = notes.All().Count,
                    shardNotes = shards.All().Count,
                    builds = builds.All().Count,
                    kits = kits.All().Count,
                    imports = imports.All().Count,
                    keybindingVersions = controls.Backups().Count,
                    itemLabels = labels.Current is not null,
                },

                // Where all of it sits, entry by entry, so it can be backed up
                // or removed knowingly. WebView2's cache is the tray window's,
                // not data, and would dwarf everything else.
                folder = new
                {
                    path = AppPaths.Root,
                    relocated = AppPaths.IsRelocated,
                    bytes = FolderBytes(AppPaths.Root, "WebView2"),
                    entries = FolderEntries(AppPaths.Root, "WebView2"),
                },
            });
        });
    }

    /// <summary>Every top-level file and folder, with its size, largest first.</summary>
    static IReadOnlyList<object> FolderEntries(string root, params string[] skip)
    {
        if (!Directory.Exists(root)) return [];
        var rows = new List<(string Name, string Kind, long Bytes, DateTimeOffset Modified, int Files)>();
        foreach (var path in Directory.EnumerateFileSystemEntries(root))
        {
            var name = Path.GetFileName(path);
            if (skip.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            if (Directory.Exists(path))
            {
                var files = SafeFiles(path).ToList();
                rows.Add((name, "folder", files.Sum(f => f.Length),
                    files.Count > 0 ? new DateTimeOffset(files.Max(f => f.LastWriteTimeUtc), TimeSpan.Zero) : new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero),
                    files.Count));
            }
            else
            {
                var info = new FileInfo(path);
                rows.Add((name, "file", info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), 1));
            }
        }
        return [.. rows.OrderByDescending(r => r.Bytes).Select(r => (object)new { r.Name, r.Kind, r.Bytes, r.Modified, r.Files })];
    }

    static long FolderBytes(string root, params string[] skip) =>
        Directory.Exists(root)
            ? Directory.EnumerateFileSystemEntries(root)
                .Where(p => !skip.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
                .Sum(p => Directory.Exists(p) ? SafeFiles(p).Sum(f => f.Length) : new FileInfo(p).Length)
            : 0;

    /// <summary>The files under a folder; a file that vanishes mid-walk or refuses to be stat'ed is skipped, not fatal.</summary>
    static IEnumerable<FileInfo> SafeFiles(string folder)
    {
        IEnumerable<string> paths;
        try { paths = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories); }
        catch (IOException) { yield break; }
        catch (UnauthorizedAccessException) { yield break; }
        foreach (var path in paths)
        {
            FileInfo? info = null;
            try { info = new FileInfo(path); _ = info.Length; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (info is not null) yield return info;
        }
    }
}
