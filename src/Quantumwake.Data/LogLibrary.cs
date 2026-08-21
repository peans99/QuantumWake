using Quantumwake.Core.GameData;
using Quantumwake.Core.Locations;
using Quantumwake.Core.Logging;
using Quantumwake.Core.State;

namespace Quantumwake.Data;

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

    /// <summary>When the kit shown in <see cref="Loadout"/> was worn.</summary>
    public DateTimeOffset? LoadoutAsOf { get; init; }
    public IReadOnlyList<StashLocation> Stash { get; init; } = [];
}

/// <param name="Total">Confirmed spend on this item across all sessions.</param>
public sealed record SpendTotal(string Name, decimal Total, int Quantity);

/// <summary>One cargo trade.</summary>
/// <param name="UnitPrice">aUEC per SCU, worked out from amount and quantity.</param>
/// <param name="Place">
/// Where the sale happened, back-tracked from the last arrival before it. Cargo
/// terminals all share a single kiosk id, so their own name says nothing.
/// </param>
/// <param name="Commodity">
/// What was in the boxes — resolved from the opt-in community dataset, null
/// when it is disabled or the id is unknown to it.
/// </param>
public sealed record TradeRecord(
    DateTimeOffset At,
    bool IsSell,
    string Place,
    int Scu,
    decimal Amount,
    decimal UnitPrice,
    string? Mode,
    string? Commodity = null);

/// <summary>One money movement.</summary>
/// <param name="Amount">Negative for money out, positive for money in.</param>
/// <param name="Confirmed">
/// Item purchases are confirmed by a server response; commodity trades are not,
/// so those are amounts requested at the kiosk.
/// </param>
/// <param name="Running">Cumulative net at this point, oldest movement first.</param>
/// <param name="Where">
/// Where the player was standing, back-tracked from the log. The kiosk id is no
/// use for this - every cargo terminal in the game reports itself as
/// <c>SCShop_Admin_lt_base_g</c> - so the place comes from the most recent
/// arrival before the transaction instead.
/// </param>
/// <param name="Shop">The vendor, resolved to its brand where the game names one.</param>
public sealed record LedgerEntry(
    DateTimeOffset At,
    string Kind,
    string What,
    string Where,
    string Shop,
    decimal Amount,
    int Quantity,
    bool Confirmed,
    decimal Running);

public sealed record FleetPoint(DateTimeOffset At, int Vehicles);

/// <summary>What occupies one character slot.</summary>
/// <param name="Category">Grouping for display, from <see cref="LoadoutCategories"/>.</param>
/// <param name="Label">Readable slot name.</param>
/// <param name="Current">Most recently seen occupant - the kit actually in use.</param>
/// <param name="CurrentSeen">When that occupant was last seen.</param>
/// <param name="History">
/// Everything else the slot has held, most recent first. Useful as background,
/// but it is a record of churn rather than a loadout: a hand slot accumulates
/// every weapon, tool and drink ever picked up.
/// </param>
/// <param name="SlotCount">How many ports in this family, e.g. 9 magazine slots.</param>
/// <param name="Items">What currently occupies them, with how many hold each.</param>
/// <remarks>
/// Deliberately carries no history. Everything a slot has ever held is a record
/// of churn, not a loadout, and showing it alongside the current occupant only
/// made the page harder to read.
/// </remarks>
public sealed record LoadoutSlot(
    string Port,
    string Category,
    string Label,
    int SlotCount,
    IReadOnlyList<LoadoutEntry> Items,
    DateTimeOffset? CurrentSeen);

/// <param name="Count">Number of slots in the family holding this item.</param>
public sealed record LoadoutEntry(string Name, int Count, DateTimeOffset LastSeen);

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

    /// <summary>
    /// Collapses numbered sibling ports onto one family.
    /// </summary>
    /// <remarks>
    /// <c>magazine_attach</c>, <c>magazine_attach_1</c> … <c>magazine_attach_8</c>
    /// are nine copies of the same thing. Treating them separately produced nine
    /// near-identical rows; the family is what a player thinks of as "magazines".
    /// </remarks>
    public static string Family(string port) =>
        string.IsNullOrEmpty(port) ? port : TrailingNumberRegex.Replace(port, string.Empty);

    private static readonly System.Text.RegularExpressions.Regex TrailingNumberRegex =
        new(@"_\d+$", System.Text.RegularExpressions.RegexOptions.Compiled);

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

    /// <summary>
    /// True for slots a player would call equipment.
    /// </summary>
    /// <remarks>
    /// A character carries 57 attachment ports and most are not gear. Eleven are
    /// the character model itself - teeth, eyelashes, eyebrows, scalp, head, body
    /// mesh - and several more are fixtures everyone has and nobody chooses: the
    /// three mobiGlas ports, the default radar lens, the built-in visor, the
    /// necksocks. Listing them buries the handful of slots that answer "what am I
    /// carrying".
    /// </remarks>
    public static bool IsEquipment(string port)
    {
        if (string.IsNullOrWhiteSpace(port))
            return false;

        // The character model, not equipment.
        if (port.EndsWith("_ItemPort", StringComparison.OrdinalIgnoreCase))
            return false;

        // Hands hold things transiently - a tool, a drink, a helmet being taken
        // off - which is handling, not equipment. They churned harder than any
        // other slot: 88 distinct items in the right hand alone.
        if (Has(port, "weapon_attach_hand"))
            return false;

        // Fixtures with exactly one possible occupant.
        return !Has(port, "mobiglas")
            && !Has(port, "necksock")
            && !port.Equals("radar", StringComparison.OrdinalIgnoreCase)
            && !port.Equals("helmet_visor", StringComparison.OrdinalIgnoreCase)
            && !port.Equals("Lens_ItemPort", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>One item in a stash, with when it was last seen there.</summary>
public sealed record StashItem(string Name, DateTimeOffset LastSeen);

/// <summary>Items of one kind, within a stash or elsewhere.</summary>
public sealed record ItemGroup(string Category, IReadOnlyList<StashItem> Items);

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

    /// <summary>
    /// Buckets and orders a set of item classes.
    /// </summary>
    /// <param name="display">
    /// Turns a class into its display name. Classification always uses the raw
    /// class, since the engine name is what carries the type information -
    /// "P4-AR Boneyard Rifle" does not contain the word "rifle" reliably, but
    /// <c>behr_rifle_ballistic_01</c> does.
    /// </param>
    public static IReadOnlyList<ItemGroup> Group(
        IEnumerable<(string ItemClass, DateTimeOffset SeenAt)> items,
        Func<string, string>? display = null)
    {
        display ??= x => x;

        return [.. items
            .GroupBy(x => Of(x.ItemClass), StringComparer.Ordinal)
            .Select(g => new ItemGroup(
                g.Key,
                [.. g.GroupBy(x => display(x.ItemClass), StringComparer.OrdinalIgnoreCase)
                     .Select(i => new StashItem(i.Key, i.Max(x => x.SeenAt)))
                     .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)]))
            .OrderBy(g => Rank(g.Category))];
    }
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

    /// <summary>
    /// Engine id to display name, read from the game's own localisation table.
    /// Empty when Data.p4k is unavailable, in which case raw ids are shown.
    /// </summary>
    public GameNames Names { get; private set; } = GameNames.Empty;

    /// <summary>
    /// The opt-in community dataset, following the same pattern as
    /// <see cref="Names"/>: empty means every lookup quietly returns null and
    /// the views say nothing rather than guessing.
    /// </summary>
    public CommunityData Community { get; set; } = new();

    /// <summary>
    /// Loads display names for an install. Safe to skip - every lookup falls
    /// back to the raw identifier.
    /// </summary>
    public void LoadNames(string installRoot)
    {
        var cache = Path.Combine(
            Path.GetDirectoryName(SessionStore.DatabasePathFor(installRoot))!,
            "names.json");

        Names = GameNames.Load(installRoot, cache);

        // Let the resolver prefer the game's own place names, and drop anything
        // resolved before they were available.
        LocationResolver.NameLookup = Names.Place;
        LocationResolver.ClearCache();
    }

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

    /// <summary>
    /// Every money movement, newest first, with a running net.
    /// </summary>
    /// <remarks>
    /// Item purchases and commodity trades are the only transactions the logs
    /// record. There is no wallet event, so this is a movement ledger rather than
    /// a balance: it says what went out and came in, not what is left.
    /// </remarks>
    public IReadOnlyList<LedgerEntry> Ledger(int days = 0)
    {
        var sessions = _store.All();

        if (days > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            sessions = [.. sessions.Where(s => s.StartedAt >= cutoff)];
        }

        var movements = new List<(DateTimeOffset At, string Kind, string What, string Where, string Shop, decimal Amount, int Quantity, bool Confirmed)>();

        foreach (var session in sessions)
        {
            foreach (var purchase in session.Purchases)
            {
                movements.Add((purchase.At, "Item bought", Names.Item(purchase.Item),
                    PlaceAt(session, purchase.At), ShopLabel(purchase.Shop),
                    -purchase.Total, purchase.Quantity, purchase.Confirmed));
            }

            foreach (var trade in session.Trades)
            {
                // "Waste · 304 SCU" with the community dataset, "304 SCU" without.
                var what = Community.Commodity(trade.ResourceId) is { } commodity
                    ? $"{commodity} · {trade.Quantity} SCU"
                    : $"{trade.Quantity} SCU";

                movements.Add((
                    trade.At,
                    trade.IsSell ? "Cargo sold" : "Cargo bought",
                    what,
                    PlaceAt(session, trade.At),
                    ShopLabel(trade.Shop),
                    trade.IsSell ? trade.Amount : -trade.Amount,
                    trade.Quantity,

                    // Commodity trades carry no server confirmation.
                    false));
            }
        }

        // Running total is computed oldest-first, then the list is reversed so the
        // newest movement leads.
        var ordered = movements.OrderBy(m => m.At).ToList();
        var entries = new List<LedgerEntry>(ordered.Count);
        decimal running = 0;

        foreach (var m in ordered)
        {
            running += m.Amount;
            entries.Add(new LedgerEntry(
                m.At, m.Kind, m.What, m.Where, m.Shop, m.Amount, m.Quantity, m.Confirmed, running));
        }

        entries.Reverse();
        return entries;
    }

    /// <summary>
    /// Where the player was at a given moment in a session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Transactions name a kiosk, not a place, and the cargo terminals all share
    /// one id - <c>SCShop_Admin_lt_base_g</c> - so a ledger built on shop names
    /// reads "Admin lt base g" for every sale ever made. The player's position is
    /// recoverable anyway: arrivals and quantum jumps are both logged with
    /// timestamps, so the last one before the transaction says where it happened.
    /// </para>
    /// <para>
    /// Jumps count as well as arrivals because a sale can follow a jump before
    /// anything asks for a location inventory. Whichever signal is more recent
    /// wins. Both lists are already in order, so this walks backwards and stops
    /// at the first hit.
    /// </para>
    /// </remarks>
    private static string PlaceAt(SessionSummary session, DateTimeOffset at)
    {
        DateTimeOffset? bestAt = null;
        string? best = null;

        for (var i = session.Locations.Count - 1; i >= 0; i--)
        {
            if (session.Locations[i].At <= at)
            {
                bestAt = session.Locations[i].At;
                best = session.Locations[i].DisplayName;
                break;
            }
        }

        for (var i = session.Jumps.Count - 1; i >= 0; i--)
        {
            var jump = session.Jumps[i];
            if (jump.At > at)
                continue;

            if (bestAt is null || jump.At > bestAt)
            {
                best = jump.ToName;
            }

            break;
        }

        return best ?? "Unknown";
    }

    /// <summary>
    /// Every place in the game, with how many times each has been visited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Visit history alone draws a map of nowhere-else: the places already seen,
    /// floating with no context. The game's own localisation table names roughly
    /// 1,300 locations, and running each id back through the resolver puts a
    /// system, body and kind on it, which is everything the map needs to lay one
    /// out. Places never visited come back with zero visits so the UI can dim
    /// them or hide them behind a toggle.
    /// </para>
    /// <para>
    /// Only places the resolver can put a real category on are added. The table
    /// is mostly interiors - <c>Pyro1_L2_03_Entrance</c> and its 800-odd
    /// siblings are elevator landings inside one building, and drawing them
    /// buries the 240 places that are actually somewhere you fly to. A handful
    /// of keys hold marketing copy rather than a name, so anything with a line
    /// break or a paragraph's worth of text is skipped as well.
    /// </para>
    /// <para>
    /// Visited places are never dropped, whatever their id looks like. Somewhere
    /// the player has actually stood earns its dot.
    /// </para>
    /// </remarks>
    public IReadOnlyList<PlaceTotal> Atlas()
    {
        var visited = Stats().Locations.ToDictionary(p => p.RawId, StringComparer.OrdinalIgnoreCase);
        var atlas = new List<PlaceTotal>(visited.Values);

        foreach (var id in Names.PlaceIds)
        {
            if (visited.ContainsKey(id))
                continue;

            var place = LocationResolver.Resolve(id);

            if (!place.IsResolved || place.System is null || place.Kind == LocationKind.Unknown)
                continue;

            if (place.DisplayName.Length > 44 || place.DisplayName.Contains('\\'))
                continue;

            atlas.Add(new PlaceTotal(
                id, place.DisplayName, place.System, place.Body, place.Kind.ToString(), 0));
        }

        return atlas;
    }

    /// <summary>The vendor's brand name where the game publishes one.</summary>
    /// <remarks>
    /// Commodity kiosks have no brand - every one of them logs as
    /// <c>SCShop_Admin_lt_base_g</c> - so they get a plain description instead of
    /// the mangled id.
    /// </remarks>
    private string ShopLabel(string shop)
    {
        if (Names.Shop(shop) is { } branded)
            return branded;

        return shop.StartsWith("Admin", StringComparison.OrdinalIgnoreCase)
            ? "Cargo terminal"
            : shop;
    }

    /// <summary>
    /// Cargo trades, newest first, with unit price worked out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What was in the boxes is not recoverable. The log names a commodity only
    /// by <c>resourceGUID</c> - <c>b999ef65-35be-45bf-908a-5eac6e06ba12</c> for a
    /// 320 SCU sale - and never repeats that id anywhere a name is attached.
    /// </para>
    /// <para>
    /// The DataCore was the obvious place to look and it is a dead end. All four
    /// ids traded across the backups were searched through the whole 330 MB of
    /// <c>Game2.dcb</c> - as text, and as bytes in both guid orderings - and none
    /// of them appears. The file does hold the commodity catalogue
    /// (<c>records/entities/commodities/minerals/dolivine.xml</c> and friends)
    /// and 24,442 guid strings, so the ids in the log belong to some other
    /// numbering: most likely the shop inventory tables, which ship encrypted.
    /// </para>
    /// <para>
    /// Volume, price, unit price and place are all present and exact, so the view
    /// reports those and stays quiet about the cargo itself.
    /// </para>
    /// </remarks>
    public IReadOnlyList<TradeRecord> Trades(int days = 0)
    {
        var sessions = _store.All();

        if (days > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            sessions = [.. sessions.Where(s => s.StartedAt >= cutoff)];
        }

        return [.. sessions
            .SelectMany(s => s.Trades.Select(t => new TradeRecord(
                t.At,
                t.IsSell,
                PlaceAt(s, t.At),
                t.Quantity,
                t.Amount,
                t.Quantity > 0 ? t.Amount / t.Quantity : 0,
                t.Mode,
                Community.Commodity(t.ResourceId))))
            .OrderByDescending(t => t.At)];
    }

    public IReadOnlyList<SessionSummary> Sessions() => _store.All();

    public SessionSummary? Session(string id) => _store.Get(id);

    /// <summary>
    /// Rolls stored sessions up into library-wide totals.
    /// </summary>
    /// <param name="days">
    /// Only include sessions started within this many days. Zero means all time.
    /// Filtering here rather than in the browser keeps the payload small and the
    /// arithmetic in one place.
    /// </param>
    public LibraryStats Stats(int days = 0)
    {
        var sessions = _store.All();

        if (days > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            sessions = [.. sessions.Where(s => s.StartedAt >= cutoff)];
        }

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
            .GroupBy(x => ShipName(x.Ship), StringComparer.Ordinal)
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

        // Grouped by display name rather than class, so the same weapon bought
        // in two colourways adds up as one line instead of two mystery ids.
        var items = purchases
            .GroupBy(p => Names.Item(p.Item), StringComparer.OrdinalIgnoreCase)
            .Select(g => new SpendTotal(g.Key, g.Sum(p => p.Total), g.Sum(p => p.Quantity)))
            .OrderByDescending(i => i.Total)
            .ToList();

        var trades = sessions.SelectMany(s => s.Trades).ToList();
        var income = trades.Where(t => t.IsSell).Sum(t => t.Amount);
        var commoditySpend = trades.Where(t => !t.IsSell).Sum(t => t.Amount);

        // Grouped by where the sale happened, not by kiosk. Every commodity
        // terminal in the game shares one shop id, so grouping on that produced
        // a single bar labelled "Admin lt base g" holding every sale ever made.
        var tradeShops = sessions
            .SelectMany(s => s.Trades.Where(t => t.IsSell).Select(t => (Place: PlaceAt(s, t.At), t.Amount, t.Quantity)))
            .GroupBy(t => t.Place, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SpendTotal(g.Key, g.Sum(t => t.Amount), g.Sum(t => t.Quantity)))
            .OrderByDescending(s => s.Total)
            .ToList();

        var fleetHistory = sessions
            .Where(s => s.FleetSize is > 0)
            .OrderBy(s => s.StartedAt)
            .Select(s => new FleetPoint(s.StartedAt, s.FleetSize!.Value))
            .ToList();

        // Last equipped per slot, across the whole library. Restricting to a
        // single session looked tidier but lost real gear: the newest session
        // recorded no arms, legs or core, so those slots vanished entirely.
        var allWorn = sessions
            .SelectMany(s => s.Loadout)
            .Where(l => LoadoutCategories.IsEquipment(l.Port))
            .ToList();

        // Merge numbered siblings. A character has nine magazine ports and four
        // grenade ports; listing each as its own row is what made the page
        // unreadable. One "Magazines" row saying what is in them is the answer.
        var loadout = allWorn
            .GroupBy(l => LoadoutCategories.Family(l.Port), StringComparer.Ordinal)
            .Select(g =>
            {
                var slotCount = g.Select(l => l.Port).Distinct(StringComparer.Ordinal).Count();

                // Per port, only its latest occupant counts as equipped.
                var equipped = g
                    .GroupBy(l => l.Port, StringComparer.Ordinal)
                    .Select(p => p.MaxBy(l => l.LastSeen)!)
                    .ToList();

                var items = equipped
                    .GroupBy(l => l.ItemClass, StringComparer.OrdinalIgnoreCase)
                    .Select(i => new LoadoutEntry(
                        Names.Item(i.Key), i.Count(), i.Max(l => l.LastSeen)))
                    .OrderByDescending(i => i.Count)
                    .ThenByDescending(i => i.LastSeen)
                    .ToList();

                return new LoadoutSlot(
                    g.Key,
                    LoadoutCategories.Of(g.Key),
                    LoadoutCategories.Label(g.Key),
                    slotCount,
                    items,
                    items.Count > 0 ? items.Max(i => i.LastSeen) : null);
            })
            // Category order first, then most recently used slot.
            .OrderBy(s => LoadoutCategories.Rank(s.Category))
            .ThenByDescending(s => s.CurrentSeen)
            .ToList();

        // Items are only ever added to the log, never removed, so this is the
        // union of everything seen at each place rather than current contents.
        var stash = sessions
            .SelectMany(s => s.Stash)
            .GroupBy(e => e.LocationId, StringComparer.Ordinal)
            .Select(g =>
            {
                // Only the newest listing describes what is there now. Item
                // removals are never logged, so merging older listings would
                // show things taken away long ago.
                var latest = g.Max(e => e.SeenAt);

                var items = g
                    .Where(e => e.SeenAt == latest)
                    .Select(e => (e.ItemClass, e.SeenAt))
                    .DistinctBy(e => e.ItemClass, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var groups = ItemCategories.Group(items, Names.Item);

                return new StashLocation(
                    g.Key,
                    g.First().LocationName,
                    latest,
                    groups.Sum(x => x.Items.Count),
                    groups);
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
            Shops = Facet(purchases.Select(p => ShopLabel(p.Shop))),
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

    /// <summary>
    /// Prefers the game's own vehicle name over the one built from the log id,
    /// so "DRAK Corsair" reads as "Drake Corsair".
    /// </summary>
    private string ShipName(ShipUsage ship)
    {
        if (ship.Manufacturer is null)
            return Names.Item(ship.Model) is var item && item != ship.Model ? item : ship.DisplayName;

        var vehicleId = $"{ship.Manufacturer}_{ship.Model}";
        var resolved = Names.Vehicle(vehicleId);

        // Vehicle() tidies underscores when it finds nothing, which is not an
        // improvement over the name we already had.
        return resolved.Equals(vehicleId.Replace('_', ' '), StringComparison.Ordinal)
            ? ship.DisplayName
            : resolved;
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
