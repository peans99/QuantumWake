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

    // ---- commerce ----

    /// <summary>Confirmed spend across every session.</summary>
    public decimal Spend { get; init; }

    public int PurchaseCount { get; init; }
    public IReadOnlyList<FacetTotal> Shops { get; init; } = [];
    public IReadOnlyList<SpendTotal> Items { get; init; } = [];

    /// <summary>Commodity sales - the only income the logs record.</summary>
    public decimal Income { get; init; }

    public decimal CommoditySpend { get; init; }
    public int TradeCount { get; init; }

    /// <summary>Income less all outgoings.</summary>
    public decimal Net { get; init; }

    public IReadOnlyList<SpendTotal> TradeShops { get; init; } = [];

    // ---- contracts ----

    public int ContractsCompleted { get; init; }
    public int ContractsAbandoned { get; init; }
    public int ContractsSeen { get; init; }

    // ---- fleet, loadout, stash ----

    /// <summary>Largest owned-vehicle count ever reported.</summary>
    public int? FleetSize { get; init; }

    /// <summary>Owned-vehicle count over time, for the fleet chart.</summary>
    public IReadOnlyList<FleetPoint> FleetHistory { get; init; } = [];

    public IReadOnlyList<LoadoutSlot> Loadout { get; init; } = [];
    public IReadOnlyList<StashLocation> Stash { get; init; } = [];
}

/// <param name="Total">Confirmed spend on this item across all sessions.</param>
public sealed record SpendTotal(string Name, decimal Total, int Quantity);

public sealed record FleetPoint(DateTimeOffset At, int Vehicles);

/// <summary>Items seen in one slot, most frequent first.</summary>
/// <param name="Category">Grouping for display, from <see cref="LoadoutCategories"/>.</param>
/// <param name="Label">Readable slot name.</param>
public sealed record LoadoutSlot(
    string Port,
    string Category,
    string Label,
    IReadOnlyList<FacetTotal> Items);

/// <summary>
/// Groups character item ports into something a person would recognise.
/// </summary>
/// <remarks>
/// Ports are engine-side names - <c>wep_stocked_3</c>, <c>helmethook_attach</c>,
/// <c>Eyedetail_ItemPort</c> - and there are 57 of them on a single character.
/// Listed flat they are unreadable, so they are bucketed by what the slot is
/// for. Matching is by prefix and keyword rather than an exhaustive list, so
/// ports added in future patches still land somewhere sensible.
/// </remarks>
public static class LoadoutCategories
{
    public const string Armour = "Armour";
    public const string Weapons = "Weapons";
    public const string Attachments = "Weapon attachments";
    public const string Throwables = "Throwables";
    public const string Medical = "Medical";
    public const string Utility = "Utility";
    public const string Carried = "Carried";
    public const string Appearance = "Appearance";
    public const string Other = "Other";

    /// <summary>Display order, most useful first. Appearance is cosmetic, so last.</summary>
    public static readonly IReadOnlyList<string> Order =
    [
        Weapons, Attachments, Throwables, Armour, Medical, Utility, Carried, Appearance, Other
    ];

    /// <summary>Sort key for a category; unknown ones fall to the end.</summary>
    public static int Rank(string category)
    {
        for (var i = 0; i < Order.Count; i++)
        {
            if (Order[i].Equals(category, StringComparison.Ordinal))
                return i;
        }

        return Order.Count;
    }

    public static string Of(string port)
    {
        if (string.IsNullOrWhiteSpace(port))
            return Other;

        // Appearance ports are the character model itself, not equipment.
        if (port.EndsWith("_ItemPort", StringComparison.OrdinalIgnoreCase))
            return Appearance;

        if (Has(port, "grenade")) return Throwables;
        if (Has(port, "medpen") || Has(port, "oxypen")) return Medical;

        // Checked before "weapon" so magazines and optics do not land there.
        if (Has(port, "magazine") || Has(port, "optics") || Has(port, "barrel") || Has(port, "module"))
            return Attachments;

        if (Has(port, "weapon") || port.StartsWith("wep_", StringComparison.OrdinalIgnoreCase))
            return Weapons;

        if (Has(port, "armor") || Has(port, "armour") || Has(port, "helmet")
            || Has(port, "backpack") || Has(port, "necksock")
            || port.Equals("Core", StringComparison.OrdinalIgnoreCase)
            || port.Equals("Extra", StringComparison.OrdinalIgnoreCase))
        {
            return Armour;
        }

        if (Has(port, "inventory_pocket") || port.StartsWith("$slot", StringComparison.OrdinalIgnoreCase))
            return Carried;

        if (Has(port, "utility") || Has(port, "mobiglas") || Has(port, "radar") || Has(port, "lens"))
            return Utility;

        return Other;
    }

    /// <summary>Turns a port name into something readable.</summary>
    public static string Label(string port)
    {
        var text = port
            .Replace("_ItemPort", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_attach", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimStart('$')
            .Replace('_', ' ')
            .Trim();

        if (text.Length == 0)
            return port;

        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static bool Has(string port, string token) =>
        port.Contains(token, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Items of one kind, within a stash or elsewhere.</summary>
public sealed record ItemGroup(string Category, IReadOnlyList<string> Items);

/// <summary>Items seen stored at one location, grouped by kind.</summary>
public sealed record StashLocation(
    string LocationId,
    string Name,
    DateTimeOffset LastSeen,
    int ItemCount,
    IReadOnlyList<ItemGroup> Groups);

/// <summary>
/// Sorts item class names into recognisable kinds.
/// </summary>
/// <remarks>
/// Item classes are engine names built from a manufacturer prefix and a
/// descriptor - <c>behr_rifle_ballistic_01_white02</c>,
/// <c>crlf_consumable_healing_01</c>, <c>arma_barrel_supp_s1</c>. The descriptor
/// is consistent enough to classify on, which turns a flat wall of forty item
/// codes into something scannable. Order matters: magazines and barrels are
/// matched before weapons, or every <c>_mag</c> would read as a rifle.
/// </remarks>
public static class ItemCategories
{
    public const string Weapons = "Weapons";
    public const string Attachments = "Attachments";
    public const string Ammo = "Ammo";
    public const string Throwables = "Throwables";
    public const string Armour = "Armour";
    public const string Medical = "Medical";
    public const string Consumables = "Food & drink";
    public const string Tools = "Tools";
    public const string Containers = "Containers";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> Order =
    [
        Weapons, Ammo, Attachments, Throwables, Armour, Medical, Tools, Consumables, Containers, Other
    ];

    /// <summary>Sort key for a category; unknown ones fall to the end.</summary>
    public static int Rank(string category)
    {
        for (var i = 0; i < Order.Count; i++)
        {
            if (Order[i].Equals(category, StringComparison.Ordinal))
                return i;
        }

        return Order.Count;
    }

    public static string Of(string itemClass)
    {
        if (string.IsNullOrWhiteSpace(itemClass))
            return Other;

        var c = itemClass;

        if (Has(c, "_mag") || Has(c, "ammobox") || Has(c, "ammo")) return Ammo;
        if (Has(c, "gren")) return Throwables;

        if (Has(c, "barrel") || Has(c, "optics") || Has(c, "supp") || Has(c, "scope")
            || Has(c, "iron_sight") || Has(c, "underbarrel") || Has(c, "_stab_"))
        {
            return Attachments;
        }

        if (Has(c, "rifle") || Has(c, "pistol") || Has(c, "smg") || Has(c, "lmg")
            || Has(c, "sniper") || Has(c, "shotgun") || Has(c, "cannon") || Has(c, "melee"))
        {
            return Weapons;
        }

        if (Has(c, "medgun") || Has(c, "healing") || Has(c, "medpen") || Has(c, "oxypen")
            || Has(c, "vial") || Has(c, "consumable_"))
        {
            return Medical;
        }

        if (Has(c, "armor") || Has(c, "armour") || Has(c, "undersuit") || Has(c, "helmet")
            || Has(c, "backpack") || Has(c, "flightsuit") || Has(c, "necksock") || Has(c, "legs")
            || Has(c, "arms") || Has(c, "torso") || Has(c, "_core_"))
        {
            return Armour;
        }

        if (Has(c, "multitool") || Has(c, "tractor") || Has(c, "utility") || Has(c, "light")
            || Has(c, "salvage") || Has(c, "mining"))
        {
            return Tools;
        }

        if (Has(c, "drink") || Has(c, "food") || Has(c, "bottle") || Has(c, "_can_")
            || Has(c, "mug") || Has(c, "snack"))
        {
            return Consumables;
        }

        if (Has(c, "container") || Has(c, "carryable") || Has(c, "scu") || Has(c, "box"))
            return Containers;

        return Other;
    }

    private static bool Has(string value, string token) =>
        value.Contains(token, StringComparison.OrdinalIgnoreCase);

    /// <summary>Buckets and orders a set of item classes.</summary>
    public static IReadOnlyList<ItemGroup> Group(IEnumerable<string> items) =>
        [.. items
            .GroupBy(Of, StringComparer.Ordinal)
            .Select(g => new ItemGroup(g.Key, [.. g.Distinct(StringComparer.OrdinalIgnoreCase).Order()]))
            .OrderBy(g => Rank(g.Category))];
}

/// <param name="Sorties">Flights - the reliable metric.</param>
/// <param name="EstimatedTime">Inferred time aboard; see <see cref="ShipUsage"/>.</param>
/// <param name="FirstFlown">Start of the earliest session this ship appears in.</param>
/// <param name="LastFlown">Start of the most recent session this ship appears in.</param>
public sealed record ShipTotal(
    string Name,
    TimeSpan EstimatedTime,
    int Sorties,
    int Sessions,
    DateTimeOffset FirstFlown,
    DateTimeOffset LastFlown);
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
            .SelectMany(s => s.Ships.Select(ship => (Session: s.Id, s.StartedAt, Ship: ship)))
            .GroupBy(x => x.Ship.DisplayName, StringComparer.Ordinal)
            .Select(g => new ShipTotal(
                g.Key,
                TimeSpan.FromTicks(g.Sum(x => x.Ship.EstimatedTime.Ticks)),
                g.Sum(x => x.Ship.Sorties),
                g.Select(x => x.Session).Distinct().Count(),
                g.Min(x => x.StartedAt),
                g.Max(x => x.StartedAt)))
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
        var purchases = sessions.SelectMany(s => s.Purchases).Where(p => p.Confirmed).ToList();

        var items = purchases
            .GroupBy(p => p.Item, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SpendTotal(g.Key, g.Sum(p => p.Total), g.Sum(p => p.Quantity)))
            .OrderByDescending(i => i.Total)
            .ToList();

        var trades = sessions.SelectMany(s => s.Trades).ToList();
        var income = trades.Where(t => t.IsSell).Sum(t => t.Amount);
        var commoditySpend = trades.Where(t => !t.IsSell).Sum(t => t.Amount);

        var tradeShops = trades
            .Where(t => t.IsSell)
            .GroupBy(t => t.Shop, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SpendTotal(g.Key, g.Sum(t => t.Amount), g.Sum(t => t.Quantity)))
            .OrderByDescending(s => s.Total)
            .ToList();

        var fleetHistory = sessions
            .Where(s => s.FleetSize is > 0)
            .OrderBy(s => s.StartedAt)
            .Select(s => new FleetPoint(s.StartedAt, s.FleetSize!.Value))
            .ToList();

        var loadout = sessions
            .SelectMany(s => s.Loadout)
            .GroupBy(l => l.Port, StringComparer.Ordinal)
            .Select(g => new LoadoutSlot(
                g.Key,
                LoadoutCategories.Of(g.Key),
                LoadoutCategories.Label(g.Key),
                [.. g.GroupBy(l => l.ItemClass, StringComparer.OrdinalIgnoreCase)
                     .Select(i => new FacetTotal(i.Key, i.Count()))
                     .OrderByDescending(i => i.Count)]))
            // Category order first, then busiest slot within each category.
            .OrderBy(s => LoadoutCategories.Rank(s.Category))
            .ThenByDescending(s => s.Items.Count)
            .ToList();

        // Items are only ever added to the log, never removed, so this is the
        // union of everything seen at each place rather than current contents.
        var stash = sessions
            .SelectMany(s => s.Stash)
            .GroupBy(e => e.LocationId, StringComparer.Ordinal)
            .Select(g =>
            {
                var items = g.Select(e => e.ItemClass)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new StashLocation(
                    g.Key,
                    g.First().LocationName,
                    g.Max(e => e.SeenAt),
                    items.Count,
                    ItemCategories.Group(items));
            })
            .OrderByDescending(l => l.ItemCount)
            .ToList();

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
            ContractTypes = Facet(contracts.Select(c => c.Type)),

            Spend = purchases.Sum(p => p.Total),
            PurchaseCount = purchases.Count,
            Shops = Facet(purchases.Select(p => p.Shop)),
            Items = items,

            Income = income,
            CommoditySpend = commoditySpend,
            TradeCount = trades.Count,
            Net = income - purchases.Sum(p => p.Total) - commoditySpend,
            TradeShops = tradeShops,

            ContractsSeen = contracts.Count,
            ContractsCompleted = contracts.Count(c => c.Outcome == ContractOutcome.Completed),
            ContractsAbandoned = contracts.Count(c => c.Outcome == ContractOutcome.Abandoned),

            FleetSize = fleetHistory.Count > 0 ? fleetHistory.Max(f => f.Vehicles) : null,
            FleetHistory = fleetHistory,
            Loadout = loadout,
            Stash = stash
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
