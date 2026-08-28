using Quantumwake.Core.GameData;
using Quantumwake.Core.Parsing;
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
/// <param name="PlaceId">
/// The same place as an engine id, so the map can put a receipt on the node it
/// already draws instead of matching on a display name two places can share.
/// </param>
/// <param name="Commodity">
/// What was in the boxes — resolved from the opt-in community dataset, null
/// when it is disabled or the id is unknown to it.
/// </param>
/// <param name="ResourceId">
/// The <c>resourceGUID</c> the log actually carried. <see cref="Commodity"/> is
/// this id run through a dataset that may be off, may be a different version, or
/// may not know the id; the id itself is the part the game wrote down. Kept so a
/// reader with a different catalogue can resolve a name this install could not.
/// </param>
public sealed record TradeRecord(
    DateTimeOffset At,
    bool IsSell,
    string Place,
    string PlaceId,
    int Scu,
    decimal Amount,
    decimal UnitPrice,
    string? Mode,
    string? Commodity = null,
    string? ResourceId = null);

/// <summary>An item observed entering the player's inventories.</summary>
/// <remarks>
/// A signal, not a certainty: the source event fires when the inventory UI
/// pages in an item it has not shown before, which covers looting but also
/// buying and receiving, and only while the inventory is open.
/// </remarks>
/// <param name="Category">
/// What kind of thing it is, from the same reading of the item class the Stash
/// page groups by - so a filter here and a heading there cannot disagree.
/// </param>
public sealed record PickupRecord(
    DateTimeOffset At,
    string Item,
    string ItemClass,
    string Place,
    string Category = ItemCategories.Other);

/// <summary>One contract as the logbook can tell it, newest first.</summary>
/// <param name="Steps">Journal-visible objectives, and how many finished.</param>
/// <param name="Rep">
/// The reputation the title says it pays, when a text mod has annotated it -
/// see <see cref="ContractTags"/>. Null means nobody said, which is the usual
/// case and is not the same as zero.
/// </param>
/// <param name="Blueprint">Whether the title is tagged as awarding a blueprint.</param>
public sealed record ContractLine(
    DateTimeOffset At,
    string Name,
    string Issuer,
    string? Type,
    string? System,
    string? Difficulty,
    string Outcome,
    int Steps,
    int StepsDone,
    double? Minutes,
    int? Rep = null,
    bool Blueprint = false);

/// <summary>
/// How much work this install has done for one faction.
/// </summary>
/// <remarks>
/// Not reputation: the logs carry none, and the value lives on a server this
/// app never talks to. This is the countable thing underneath it - contracts
/// taken, finished and walked away from, per issuer, over time - which is what
/// actually moves standing. <paramref name="Rep"/> is filled in only from
/// titles a text mod has annotated, and <paramref name="RepFrom"/> says how
/// many of the contracts that number came from, so it can never be mistaken
/// for a total.
/// </remarks>
public sealed record Standing(
    string Issuer,
    int Contracts,
    int Completed,
    int Abandoned,
    DateTimeOffset First,
    DateTimeOffset Last,
    int Rep,
    int RepFrom);

/// <summary>
/// Whether one kind of thing is still arriving in the logs.
/// </summary>
/// <remarks>
/// The point is the date, not the count. CIG has removed telemetry patch by
/// patch - quantum detail in 4.0.1, inter-system jumps in 4.1.0, combat
/// entirely by 4.9 - and every removal looked, from inside the app, exactly
/// like a quiet evening. A signal that stopped six weeks ago has stopped; a
/// signal nobody has ever seen may simply be one this player does not do.
/// </remarks>
/// <param name="Sessions">Sessions carrying at least one, which dates the signal.</param>
/// <param name="Note">Why it reads zero, when the reason is known.</param>
public sealed record SignalHealth(
    string Name,
    string Group,
    int Total,
    int Sessions,
    DateTimeOffset? LastSeen,
    string? Note);

/// <summary>
/// Somebody this install has flown with, as far as the logs can tell.
/// </summary>
/// <remarks>
/// The party channel announces that a player connected or dropped while you were
/// grouped with them; there is no roster event, so presence is inferred from
/// those announcements and nothing else. That makes every field here a floor
/// rather than a total: a friend who was already online when you grouped up, and
/// who stayed until you logged off, produces no notification at all and so does
/// not appear. What the numbers can be trusted to say is that these people were
/// there - never that nobody else was.
/// </remarks>
/// <param name="Sessions">Sessions in which they were named at least once.</param>
/// <summary>
/// A ship you and somebody else were both aboard.
/// </summary>
/// <param name="Owner">Whose it is - possibly you, possibly them, possibly neither.</param>
/// <param name="Times">
/// Boardings seen, not hours flown. There is no leave line for the reader, so
/// time aboard is not recoverable; a channel opens on boarding rather than on
/// flying, so a parked ship counts the same as a crossing.
/// </param>
public sealed record SharedShip(
    string Handle,
    string Ship,
    string Owner,
    int Times,
    DateTimeOffset First,
    DateTimeOffset Last);

/// <param name="Connected">Times they came online while partied with you.</param>
/// <param name="Dropped">Times they went offline the same way.</param>
/// <param name="LedParty">Times party lead passed to them.</param>
/// <param name="Joined">
/// Times they joined the party - a different fact from coming online, and the
/// only one of these that means somebody was not there a moment before.
/// </param>
/// <param name="Left">Times they left it, as opposed to merely dropping.</param>
public sealed record Wingman(
    string Handle,
    int Sessions,
    int Connected,
    int Dropped,
    int LedParty,
    DateTimeOffset First,
    DateTimeOffset Last,
    int Joined = 0,
    int Left = 0);

/// <summary>One commodity in the community catalogue, with this install's own trade record against it.</summary>
/// <param name="Sold">Facility keys where kiosks accept it.</param>
/// <param name="Bought">Facility keys where kiosks stock it.</param>
public sealed record MarketEntry(
    string Id,
    string Name,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Sold,
    IReadOnlyList<string> Bought,
    int MyScuSold,
    decimal MyRevenue,
    int MyTrades);

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
public sealed record LoadoutEntry(
    string Name,
    int Count,
    DateTimeOffset LastSeen,
    ItemInfo? Reference = null);

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
/// <param name="ItemClass">Engine class, kept so prices can join precisely.</param>
public sealed record StashItem(string Name, DateTimeOffset LastSeen, string? ItemClass = null);

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
                     .Select(i => new StashItem(i.Key, i.Max(x => x.SeenAt), i.First().ItemClass))
                     .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)]))
            .OrderBy(g => Rank(g.Category))];
    }
}

/// <param name="Sorties">Flights - the reliable metric.</param>
/// <param name="EstimatedTime">Inferred time aboard; see <see cref="ShipUsage"/>.</param>
/// <param name="FirstFlown">Start of the earliest session this ship appears in.</param>
/// <param name="LastFlown">Start of the most recent session this ship appears in.</param>
/// <param name="Reference">Community ship data - role, crew, claim costs - when the dataset is enabled and the name matched.</param>
/// <param name="ClassName">
/// The game's own name for this ship (<c>DRAK_Corsair</c>), which is what the
/// reference data is keyed by. <see cref="Name"/> is for reading - "Drake
/// Corsair" - and cannot be turned back into the key, because the display
/// manufacturer is a word and the class carries a code: Drake is DRAK, Anvil
/// is ANVL, and "Mk II" is Mk2. Anything asking the reference data a question
/// about this ship has to carry this along.
/// </param>
public sealed record ShipTotal(
    string Name,
    TimeSpan EstimatedTime,
    int Sorties,
    int Sessions,
    DateTimeOffset FirstFlown,
    DateTimeOffset LastFlown,
    ShipInfo? Reference = null,
    string ClassName = "");
/// <summary>When a game version was first seen in this install's logs.</summary>
public sealed record PatchArrival(string Patch, DateTimeOffset At);

public sealed record PlaceTotal(

    string RawId,
    string Name,
    string? System,
    string? Body,
    string Kind,
    int Visits,
    DateTimeOffset? LastVisit = null);
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

    private readonly Dictionary<string, (int Count, string Sample)> _unread = new(StringComparer.Ordinal);

    /// <summary>
    /// What the parser could not read, over the files this run actually parsed.
    /// </summary>
    /// <remarks>
    /// Empty after a start that read everything from cache, which is the honest
    /// answer rather than a stale one: nothing was parsed, so nothing was found
    /// unreadable. A forced rescan fills it.
    /// </remarks>
    public ParserHealth Health(IEnumerable<string> known, bool samples = false)
    {
        lock (_unread)
            return Diagnostics.Health(_unread.Values.Sum(v => v.Count), _unread, known, samples);
    }


    /// <summary>
    /// The line a wipe draws, and how deep it goes.
    /// </summary>
    /// <remarks>
    /// Null means count everything. Set from <see cref="WipeStore"/> at startup
    /// and whenever the player changes it, so one assignment moves every total
    /// the wipe actually touched - and leaves the rest alone.
    /// </remarks>
    public Wipe? Wipe { get; set; }

    /// <summary>The date, when there is one to apply.</summary>
    private DateTimeOffset? WipedAt =>
        Wipe is { At: var at } && at > DateTimeOffset.MinValue ? at : null;

    /// <summary>
    /// Every session that counts towards one kind of total, newest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single door onto the store, because a total that reaches past a wipe
    /// is answering about an account the player no longer has. One filter here
    /// rather than at each question means a view added later cannot forget it.
    /// </para>
    /// <para>
    /// Which totals it applies to is the player's own answer, because wipes
    /// come at different depths: a patch that resets aUEC and leaves the hangar
    /// alone should not blank the fleet, and one that clears inventories should
    /// not blank the ledger. Asking for the wrong category is the one way to
    /// get this wrong, so every caller names what it is counting.
    /// </para>
    /// <para>
    /// Nothing is deleted either way - the sessions are still stored, still
    /// parsed, and come back the moment the date moves.
    /// </para>
    /// </remarks>
    private IReadOnlyList<SessionSummary> Counted(WipeScope counting) =>
        Narrow(_store.All(), counting);

    /// <summary>The same rule applied to a list already in hand.</summary>
    private IReadOnlyList<SessionSummary> Narrow(
        IReadOnlyList<SessionSummary> sessions, WipeScope counting) =>
        WipedAt is { } since && Wipe!.Scope.HasFlag(counting)
            ? [.. sessions.Where(s => s.StartedAt >= since)]
            : sessions;

    /// <summary>
    /// When each game version first appears in the logs, oldest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A wipe cannot be read out of the logs - nothing says "your account was
    /// reset". What can be read is when a patch arrived, and wipes arrive with
    /// patches, so the app offers the date and lets the player say whether it
    /// wiped. That is the honest shape of this: evidence for the question, not
    /// an answer invented to look clever.
    /// </para>
    /// <para>
    /// Grouped by major.minor, because 4.9.188 and 4.9.190 are the same patch to
    /// a player and only the first of them is a date worth offering. Reads every
    /// stored session rather than the counted ones: the whole point is to notice
    /// a patch that arrived after the line currently drawn.
    /// </para>
    /// </remarks>
    public IReadOnlyList<PatchArrival> PatchArrivals() =>
    [
        .. _store.All()
            .Where(s => !string.IsNullOrWhiteSpace(s.GameVersion))
            .Select(s => (Patch: MajorMinor(s.GameVersion!), s.StartedAt))
            .Where(x => x.Patch is not null)
            .GroupBy(x => x.Patch!, StringComparer.Ordinal)
            .Select(g => new PatchArrival(g.Key, g.Min(x => x.StartedAt)))
            .OrderBy(a => a.At)
    ];

    /// <summary>"4.9.188.23497" to "4.9", or null when it is not a version.</summary>
    private static string? MajorMinor(string version)
    {
        var parts = version.Split('.');

        return parts.Length >= 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _)
            ? $"{parts[0]}.{parts[1]}"
            : null;
    }

    /// <summary>
    /// The newest patch that arrived after the line currently drawn, if any.
    /// </summary>
    /// <remarks>
    /// Offered, never applied: only the player knows whether that patch wiped.
    /// A patch on the same day as the current wipe is the wipe already recorded,
    /// so it is not offered back.
    /// </remarks>
    public PatchArrival? PatchSinceWipe()
    {
        var since = WipedAt ?? DateTimeOffset.MinValue;

        return PatchArrivals()
            .Where(a => a.At > since.AddHours(12))
            .OrderByDescending(a => a.At)
            .FirstOrDefault();
    }

    /// <summary>How many stored sessions started before the wipe.</summary>
    public int SessionsBeforeWipe() =>
        WipedAt is { } since ? _store.All().Count(s => s.StartedAt < since) : 0;

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

            pending.Add((BuildSession(file, _unread), fingerprint));
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
    public static SessionSummary BuildSession(string path) => BuildSession(path, null);

    /// <summary>
    /// Parses one log file, and reports what it could not read.
    /// </summary>
    /// <param name="unread">
    /// Collects the tags this file defeated the parser with. A fresh parser per
    /// file rather than one shared across the scan: it carries a half-read
    /// session header between lines, and that state belongs to the file it came
    /// from.
    /// </param>
    public static SessionSummary BuildSession(
        string path, Dictionary<string, (int Count, string Sample)>? unread)
    {
        var builder = new SessionBuilder(path);
        var parser = new LogEventParser();

        foreach (var ev in LogFileReader.ReadEvents(path, parser))
            builder.Add(ev);

        if (unread is not null)
        {
            lock (unread)
                unread.Merge(parser);
        }

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
        var sessions = Counted(WipeScope.Money);

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
    private static string PlaceAt(SessionSummary session, DateTimeOffset at) =>
        PlaceRefAt(session, at).Name;

    /// <summary>
    /// The same back-track as <see cref="PlaceAt"/>, keeping the engine id.
    /// </summary>
    /// <remarks>
    /// The map draws its nodes from engine ids, so a receipt carrying only a
    /// display name cannot be placed on one exactly. Carrying the id through
    /// costs nothing and makes the join certain.
    /// </remarks>
    private static (string Id, string Name) PlaceRefAt(SessionSummary session, DateTimeOffset at)
    {
        DateTimeOffset? bestAt = null;
        string? bestId = null;
        string? best = null;

        for (var i = session.Locations.Count - 1; i >= 0; i--)
        {
            if (session.Locations[i].At <= at)
            {
                bestAt = session.Locations[i].At;
                bestId = session.Locations[i].RawId;
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
                bestId = jump.ToId;
                best = jump.ToName;
            }

            break;
        }

        return (bestId ?? string.Empty, best ?? "Unknown");
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

    private TerminalPlaces? _terminalPlaces;

    /// <summary>
    /// Terminal-name to map-place lookup, built from the atlas on first use.
    /// </summary>
    /// <remarks>
    /// Held here because the atlas is here: every caller that needs the join -
    /// the price shading, the flight plan, the trade advisor - then agrees with
    /// every other one, which is the whole point of doing it in a single place.
    /// Rebuilt when the atlas grows, since a place first visited today is a
    /// place a terminal can now be matched to.
    /// </remarks>
    public TerminalPlaces Terminals
    {
        get
        {
            var atlas = Atlas();

            if (_terminalPlaces is null || _terminalCount != atlas.Count)
            {
                _terminalPlaces = new TerminalPlaces(atlas);
                _terminalCount = atlas.Count;
            }

            return _terminalPlaces;
        }
    }

    private int _terminalCount = -1;

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
        var sessions = Counted(WipeScope.Money);

        if (days > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            sessions = [.. sessions.Where(s => s.StartedAt >= cutoff)];
        }

        return [.. sessions
            .SelectMany(s => s.Trades.Select(t =>
            {
                var place = PlaceRefAt(s, t.At);

                return new TradeRecord(
                    t.At,
                    t.IsSell,
                    place.Name,
                    place.Id,
                    t.Quantity,
                    t.Amount,
                    t.Quantity > 0 ? t.Amount / t.Quantity : 0,
                    t.Mode,
                    Community.Commodity(t.ResourceId),
                    t.ResourceId);
            }))
            .OrderByDescending(t => t.At)];
    }

    /// <summary>
    /// The handle this install last played under, or null when no session names one.
    /// </summary>
    /// <remarks>
    /// The newest rather than the most frequent: a pilot who renamed wants the
    /// name they answer to now, and the older one is still in the logs for
    /// <see cref="Wingmen"/> to keep out of their own friends list.
    /// </remarks>
    public string? Handle() =>
        Counted(WipeScope.History)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => s.Handle)
            .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

    /// <summary>Cargo trades whose own timestamp falls inside the last <paramref name="days"/> days.</summary>
    /// <remarks>
    /// <see cref="Trades(int)"/> takes its window off <see cref="SessionSummary.StartedAt"/>,
    /// which is the right filter for "sessions from this week" and the wrong one
    /// for "trades from this week": a session that began eight days ago and ran
    /// past midnight is dropped whole, taking trades made well inside the window
    /// with it. So fetch two days wider than asked and filter on the trade.
    ///
    /// Two rather than one because a single day only covers a session shorter
    /// than 24 hours, and this is a filter over summaries already in memory - the
    /// wider fetch costs nothing, while the narrower one loses rows in silence.
    /// </remarks>
    public IReadOnlyList<TradeRecord> TradesWithin(int days)
    {
        if (days <= 0)
            return Trades(0);

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        return [.. Trades(days + 2).Where(t => t.At >= cutoff)];
    }

    public IReadOnlyList<SessionSummary> Sessions() => Counted(WipeScope.History);

    public SessionSummary? Session(string id) => _store.Get(id);

    /// <summary>
    /// When each item class was first seen in the player's inventories, newest
    /// first, with the place back-tracked the same way trades are.
    /// </summary>
    /// <remarks>
    /// Deduplicated across the whole history: a class that has ever been seen
    /// before is not news again. The source event is a listing rather than a
    /// transfer, so this is an acquisition <i>signal</i> — first sighting is
    /// roughly when the item entered the player's life, whether looted, bought
    /// or received — and the view says so.
    /// </remarks>
    /// <summary>
    /// Every contract seen, newest first, with the objective progress the game
    /// pushed for it. Contracts are keyed per session, so the same repeatable
    /// mission taken twice is two lines - which is what a logbook wants.
    /// </summary>
    public IReadOnlyList<ContractLine> Contracts(int days = 0)
    {
        var cutoff = days > 0 ? DateTimeOffset.UtcNow.AddDays(-days) : DateTimeOffset.MinValue;

        return
        [
            .. Counted(WipeScope.History)
                .SelectMany(s => s.Contracts)
                .Where(c => c.FirstSeen >= cutoff)
                .OrderByDescending(c => c.FirstSeen)
                .Select(c => new ContractLine(
                    c.FirstSeen,

                    // The annotations come off the name and become fields: a
                    // contract should still read as its own title.
                    ContractTags.Clean(c.DisplayName),
                    c.Issuer,
                    c.Type,
                    c.System,
                    c.Difficulty,
                    c.Outcome.ToString(),
                    c.Steps,
                    c.StepsDone,
                    c.TimeToComplete?.TotalMinutes,
                    ContractTags.RepFrom(c.DisplayName),
                    ContractTags.AwardsBlueprint(c.DisplayName)))
        ];
    }

    /// <summary>
    /// Work done per faction, most first.
    /// </summary>
    /// <remarks>
    /// The honest answer to "how is my standing with these people": every
    /// contract of theirs this install has taken, how many were finished, and
    /// when. Reputation itself is never logged - the client opens a channel to
    /// a reputation service and the numbers stay on the far side of it - so the
    /// only thing that can be counted is the work, and the only rep that can be
    /// shown is what a text mod wrote on a title.
    /// </remarks>
    public IReadOnlyList<Standing> Standings(int days = 0)
    {
        var cutoff = days > 0 ? DateTimeOffset.UtcNow.AddDays(-days) : DateTimeOffset.MinValue;

        return
        [
            .. Counted(WipeScope.History)
                .SelectMany(s => s.Contracts)
                .Where(c => c.FirstSeen >= cutoff && !string.IsNullOrWhiteSpace(c.Issuer))
                .GroupBy(c => ContractTags.IssuerKey(c.Issuer))
                .Select(g =>
                {
                    var rep = g.Select(c => ContractTags.RepFrom(c.DisplayName))
                        .Where(r => r is not null)
                        .Select(r => r!.Value)
                        .ToList();

                    return new Standing(
                        // The fullest spelling the logs used, so a row reads as
                        // "Bounty Hunters Guild" rather than "BHG".
                        g.Select(c => c.Issuer)
                            .OrderByDescending(name => name.Length)
                            .First(),
                        g.Count(),
                        g.Count(c => c.Outcome == ContractOutcome.Completed),
                        g.Count(c => c.Outcome == ContractOutcome.Abandoned),
                        g.Min(c => c.FirstSeen),
                        g.Max(c => c.FirstSeen),
                        rep.Sum(),
                        rep.Count);
                })
                .OrderByDescending(s => s.Completed)
                .ThenByDescending(s => s.Contracts)
        ];
    }

    /// <summary>
    /// What the logs are still carrying, and when each thing last arrived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from stored sessions rather than from the parser, which matters
    /// more than it sounds: a scan skips every unchanged backup, so parser
    /// counters describe whatever happened to be re-read rather than the
    /// install. Summaries are the whole history, and they are what the pages
    /// draw from - so this answers "is the app still seeing this", which is the
    /// question, rather than "did the parser match something just now".
    /// </para>
    /// <para>
    /// Deliberately unscoped by wipe. A wipe ends an account, not the client's
    /// willingness to log a thing, and drawing the line here would make a
    /// removed event and a fresh start look identical.
    /// </para>
    /// </remarks>
    public IReadOnlyList<SignalHealth> Signals()
    {
        var sessions = _store.All();

        SignalHealth Count(
            string name, string group, Func<SessionSummary, int> howMany, string? note = null)
        {
            var seen = sessions.Where(s => howMany(s) > 0).ToList();

            return new SignalHealth(
                name, group,
                sessions.Sum(howMany),
                seen.Count,
                seen.Count > 0 ? seen.Max(s => s.StartedAt) : null,
                note);
        }

        return
        [
            Count("Sessions", "Flight", _ => 1),
            Count("Places visited", "Flight", s => s.Locations.Count),
            Count("Ships flown", "Flight", s => s.Ships.Count),
            Count("Quantum jumps", "Flight", s => s.Jumps.Count),
            Count("Contracts", "Flight", s => s.Contracts.Count),
            Count("Party mentions", "Flight", s => s.PartyNotes.Count,
                "Only fires when somebody joins or drops while you are grouped."),

            Count("Item purchases", "Economy", s => s.Purchases.Count),
            Count("Commodity sales", "Economy", s => s.Trades.Count(t => t.IsSell)),
            Count("Commodity purchases", "Economy", s => s.Trades.Count(t => !t.IsSell)),

            Count("Loadout attachments", "Gear", s => s.Loadout.Count),
            Count("Stash listings", "Gear", s => s.Stash.Count),
            Count("Items first seen", "Gear", s => s.Pickups.Count),
            Count("Blueprints", "Gear", s => s.Blueprints.Count),

            Count("Beds used", "Casualties", s => s.MedicalBeds.Count),
            Count("Incapacitations", "Casualties", s => s.Incapacitations),
            Count("Deaths", "Casualties", s => s.Deaths,
                "Inferred from corpse item-recovery bursts: 4.9 and 4.10 log no death event."),
            Count("Kills", "Casualties", s => s.Kills,
                "Not logged at all since 4.9. The parser is written and dormant, "
                + "and this fills in by itself if CIG restore the events."),
        ];
    }

    /// <summary>
    /// Everyone the party channel has named, most-flown-with first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ranked by sessions rather than by notifications, because a notification
    /// count measures somebody's connection quality more than your time
    /// together: one player who dropped and rejoined nine times in an evening
    /// would otherwise outrank nine people who each flew a whole night with you.
    /// </para>
    /// <para>
    /// Your own handles are dropped. You appear in your own logs whenever party
    /// lead passes to you, and a list of the people you fly with should not have
    /// you at the top of it.
    /// </para>
    /// <para>
    /// Scoped to <see cref="WipeScope.History"/>: who you flew with is play
    /// history, and a wipe takes an account's possessions rather than the memory
    /// of an evening.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Wingman> Wingmen(int days = 0)
    {
        var cutoff = days > 0 ? DateTimeOffset.UtcNow.AddDays(-days) : DateTimeOffset.MinValue;
        var sessions = Counted(WipeScope.History);

        // Every handle this install has played under, so a rename does not put
        // an older self in the list beside your friends.
        var mine = sessions
            .Select(s => s.Handle)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        return
        [
            .. sessions
                .SelectMany(s => s.PartyNotes.Select(note => (Session: s.Id, Note: note)))
                .Where(x => x.Note.Handle is not null
                            && x.Note.At >= cutoff
                            && !mine.Contains(x.Note.Handle))
                .GroupBy(x => x.Note.Handle!, StringComparer.OrdinalIgnoreCase)
                .Select(g => new Wingman(
                    // The most recent spelling: handles are grouped without
                    // regard to case, and the newest is likeliest to be how they
                    // write it now.
                    g.OrderByDescending(x => x.Note.At).First().Note.Handle!,
                    g.Select(x => x.Session).Distinct().Count(),
                    g.Count(x => x.Note.Moment == PartyMoment.Connected),
                    g.Count(x => x.Note.Moment == PartyMoment.Disconnected),
                    g.Count(x => x.Note.Moment == PartyMoment.BecameLeader),
                    g.Min(x => x.Note.At),
                    g.Max(x => x.Note.At),
                    g.Count(x => x.Note.Moment == PartyMoment.Joined),
                    g.Count(x => x.Note.Moment == PartyMoment.Left)))
                .OrderByDescending(w => w.Sessions)
                .ThenByDescending(w => w.Connected)
                .ThenBy(w => w.Handle, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>
    /// Blueprints the player has been given, earliest sighting first. The game
    /// announces them once and never mentions them again, so this is the whole
    /// record of what can be crafted.
    /// </summary>
    /// <summary>
    /// The ships you and other people were aboard together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built from the ship comms channels, which are the only lines that put a
    /// person inside a vehicle. A pairing is recorded when the reader and
    /// somebody else were both in the same channel: either they boarded a ship
    /// while the reader was in it, or the reader boarded one they own.
    /// </para>
    /// <para>
    /// A floor, like everything else here. The reader sees only channels they
    /// were in themselves, boarding is not flying, and nothing records how long
    /// anybody stayed - so this answers "we were both in this ship" and refuses
    /// the question it looks like it answers, which is how much you flew
    /// together.
    /// </para>
    /// </remarks>
    public IReadOnlyList<SharedShip> SharedShips(int days = 0)
    {
        var cutoff = days > 0 ? DateTimeOffset.UtcNow.AddDays(-days) : DateTimeOffset.MinValue;
        var sessions = Counted(WipeScope.History);

        var mine = sessions
            .Select(s => s.Handle)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        var pairings = new List<(string Handle, string Ship, string Owner, DateTimeOffset At)>();

        foreach (var note in sessions.SelectMany(s => s.ChannelNotes).Where(n => n.At >= cutoff))
        {
            switch (note.Moment)
            {
                // Somebody came aboard a channel the reader was already in.
                case ChannelMoment.TheyBoarded when note.Handle is { } who && !mine.Contains(who):
                    pairings.Add((who, note.Ship, note.Owner, note.At));
                    break;

                // The reader boarded a ship somebody else owns. Their name is on
                // the berth even when no arrival line ever named them.
                case ChannelMoment.YouBoarded when !mine.Contains(note.Owner):
                    pairings.Add((note.Owner, note.Ship, note.Owner, note.At));
                    break;
            }
        }

        return
        [
            .. pairings
                // One key rather than a tuple comparer: handles and ship names
                // are both matched without regard to case, and the newest
                // spelling of each is taken from the group below.
                .GroupBy(p => $"{p.Handle}|{p.Ship}|{p.Owner}".ToLowerInvariant())
                .Select(g => new SharedShip(
                    g.OrderByDescending(p => p.At).First().Handle,
                    g.OrderByDescending(p => p.At).First().Ship,
                    g.OrderByDescending(p => p.At).First().Owner,
                    g.Count(),
                    g.Min(p => p.At),
                    g.Max(p => p.At)))
                .OrderByDescending(s => s.Times)
                .ThenByDescending(s => s.Last)
        ];
    }

    public IReadOnlyList<BlueprintReceipt> Blueprints()
    {
        return
        [
            .. Counted(WipeScope.Inventory)
                .SelectMany(s => s.Blueprints)
                .GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new BlueprintReceipt(g.Min(b => b.At), g.Key))
                .OrderBy(b => b.At)
        ];
    }

    /// <summary>
    /// What is stored where.
    /// </summary>
    /// <param name="everSeen">
    /// False keeps only the newest listing per place, which is the honest
    /// answer to "what is there now" - removals are never logged, so older
    /// listings would show things taken away long ago. True unions every
    /// listing instead, which is the honest answer to "what have I left
    /// lying around", and matters because a listing is only ever a page: a
    /// glance at one tab of an inventory replaces a full browse and the place
    /// appears to have emptied.
    /// </param>
    public IReadOnlyList<StashLocation> Stash(bool everSeen = false) =>
        StashView(Counted(WipeScope.Inventory), everSeen);

    private List<StashLocation> StashView(IReadOnlyList<SessionSummary> sessions, bool everSeen) =>
    [
        .. sessions
            .SelectMany(s => s.Stash)
            .GroupBy(e => e.LocationId, StringComparer.Ordinal)
            .Select(g =>
            {
                var latest = g.Max(e => e.SeenAt);

                var items = g
                    .Where(e => everSeen || e.SeenAt == latest)
                    .Select(e => (e.ItemClass, e.SeenAt))

                    // Newest sighting wins, so an item's date is when it was
                    // last actually seen rather than whichever row sorted first.
                    .GroupBy(e => e.ItemClass, StringComparer.OrdinalIgnoreCase)
                    .Select(i => i.MaxBy(e => e.SeenAt))
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
    ];

    /// <summary>
    /// Where the player has woken after dying, newest first. Inferred from the
    /// first place seen after each death, since the game logs no respawn point.
    /// </summary>
    /// <summary>
    /// Beds used, newest first, each labelled with which kind it looks like.
    /// </summary>
    /// <remarks>
    /// The game prints one line for every bed - the clinic bed after a fight
    /// and the hab bed you wake up in at login - so the kind is inferred from
    /// what surrounds it and is honest about being a guess. Regen is only set
    /// at a real medical bed, so a "wake" is not evidence of anything.
    /// </remarks>

    public IReadOnlyList<MedicalBedVisit> MedicalBeds(int days = 0)
    {
        var cutoff = days > 0 ? DateTimeOffset.UtcNow.AddDays(-days) : DateTimeOffset.MinValue;

        // Through the same door as every other question: a bed used on an
        // account that has since been wiped is not a hint about this one.
        return
        [
            .. Counted(WipeScope.History)
                .SelectMany(s => s.MedicalBeds)
                .Where(b => b.At >= cutoff)
                .OrderByDescending(b => b.At)
        ];
    }

    public IReadOnlyList<RespawnRecord> Respawns(int days = 0)
    {
        var cutoff = days > 0 ? DateTimeOffset.UtcNow.AddDays(-days) : DateTimeOffset.MinValue;

        return
        [
            .. Counted(WipeScope.History)
                .SelectMany(s => s.Respawns)
                .Where(r => r.At >= cutoff)
                .OrderByDescending(r => r.At)
        ];
    }

    public IReadOnlyList<PickupRecord> Pickups(int days = 0)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var firsts = new List<PickupRecord>();

        // Oldest first, so the first sighting wins and later ones are noise.
        foreach (var session in Counted(WipeScope.Inventory).OrderBy(s => s.StartedAt))
        {
            foreach (var pickup in session.Pickups.OrderBy(p => p.At))
            {
                if (!seen.Add(pickup.ItemClass))
                    continue;

                firsts.Add(new PickupRecord(
                    pickup.At,
                    Names.Item(pickup.ItemClass),
                    pickup.ItemClass,
                    PlaceAt(session, pickup.At),
                    ItemCategories.Of(pickup.ItemClass)));
            }
        }

        if (days > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            firsts = [.. firsts.Where(p => p.At >= cutoff)];
        }

        return [.. firsts.OrderByDescending(p => p.At)];
    }

    /// <summary>
    /// The community commodity catalogue joined onto the player's own trades:
    /// every commodity the dataset knows, with this install's volume and
    /// revenue against each. Empty when the dataset is disabled.
    /// </summary>
    public IReadOnlyList<MarketEntry> Market()
    {
        if (!Community.IsEnabled)
            return [];

        var trades = Counted(WipeScope.Money)
            .SelectMany(s => s.Trades)
            .Where(t => t.ResourceId is not null)
            .GroupBy(t => t.ResourceId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return [.. Community.All
            .Select(pair =>
            {
                var mine = trades.GetValueOrDefault(pair.Key);

                return new MarketEntry(
                    pair.Key,
                    pair.Value.Name,
                    pair.Value.Groups,
                    pair.Value.Sold,
                    pair.Value.Bought,
                    mine?.Where(t => t.IsSell).Sum(t => t.Quantity) ?? 0,
                    mine?.Where(t => t.IsSell).Sum(t => t.Amount) ?? 0m,
                    mine?.Count ?? 0);
            })
            .OrderByDescending(e => e.MyRevenue)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)];
    }

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
        var sessions = Counted(WipeScope.History);

        if (days > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            sessions = [.. sessions.Where(s => s.StartedAt >= cutoff)];
        }

        // This one method answers for four different kinds of total, and a
        // wipe need not have taken all four. Each aggregate below counts from
        // its own list: after a money-only wipe the ledger starts again while
        // the fleet, the stashes and the places keep their whole history.
        var spending = Narrow(sessions, WipeScope.Money);
        var hangar = Narrow(sessions, WipeScope.Ships);
        var holdings = Narrow(sessions, WipeScope.Inventory);

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

        var ships = hangar
            .SelectMany(s => s.Ships.Select(ship => (Session: s.Id, s.StartedAt, Ship: ship)))
            .GroupBy(x => ShipName(x.Ship), StringComparer.Ordinal)
            .Select(g => new ShipTotal(
                g.Key,
                TimeSpan.FromTicks(g.Sum(x => x.Ship.EstimatedTime.Ticks)),
                g.Sum(x => x.Ship.Sorties),
                g.Select(x => x.Session).Distinct().Count(),
                g.Min(x => x.StartedAt),
                g.Max(x => x.StartedAt),

                // The raw log tokens ARE the class name (DRAK_Corsair), so try
                // those first; the localised display name only matches when CIG
                // named the class after it.
                g.Select(x => Community.Ship($"{x.Ship.Manufacturer}_{x.Ship.Model}"))
                    .FirstOrDefault(r => r is not null)
                 ?? Community.Ship(g.Key),

                // Kept so later questions - what fits this ship, what does it
                // cost to claim - can be asked of the reference data at all.
                g.Select(x => $"{x.Ship.Manufacturer}_{x.Ship.Model}")
                    .FirstOrDefault(name => Community.Ship(name) is not null)
                 ?? $"{g.First().Ship.Manufacturer}_{g.First().Ship.Model}"))

            // "Unmanned" variants (Cutlass Black Unmanned Salvage and kin) are
            // mission derelicts the player boarded, not ships they own; they
            // must not count as fleet anywhere.
            .Where(s => !s.Name.Contains("Unmanned", StringComparison.OrdinalIgnoreCase))
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
                g.Count(),
                g.Max(x => x.At)))
            .OrderByDescending(l => l.Visits)
            .ToList();

        var destinations = sessions
            .SelectMany(s => s.Jumps)
            .GroupBy(j => j.ToId, StringComparer.Ordinal)
            .Select(g => new PlaceTotal(g.Key, g.First().ToName, null, null, "Destination", g.Count()))
            .OrderByDescending(d => d.Visits)
            .ToList();

        var contracts = sessions.SelectMany(s => s.Contracts).ToList();
        var purchases = spending.SelectMany(s => s.Purchases).Where(p => p.Confirmed).ToList();

        // Grouped by display name rather than class, so the same weapon bought
        // in two colourways adds up as one line instead of two mystery ids.
        var items = purchases
            .GroupBy(p => Names.Item(p.Item), StringComparer.OrdinalIgnoreCase)
            .Select(g => new SpendTotal(g.Key, g.Sum(p => p.Total), g.Sum(p => p.Quantity)))
            .OrderByDescending(i => i.Total)
            .ToList();

        var trades = spending.SelectMany(s => s.Trades).ToList();
        var income = trades.Where(t => t.IsSell).Sum(t => t.Amount);
        var commoditySpend = trades.Where(t => !t.IsSell).Sum(t => t.Amount);

        // Grouped by where the sale happened, not by kiosk. Every commodity
        // terminal in the game shares one shop id, so grouping on that produced
        // a single bar labelled "Admin lt base g" holding every sale ever made.
        var tradeShops = spending
            .SelectMany(s => s.Trades.Where(t => t.IsSell).Select(t => (Place: PlaceAt(s, t.At), t.Amount, t.Quantity)))
            .GroupBy(t => t.Place, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SpendTotal(g.Key, g.Sum(t => t.Amount), g.Sum(t => t.Quantity)))
            .OrderByDescending(s => s.Total)
            .ToList();

        var fleetHistory = hangar
            .Where(s => s.FleetSize is > 0)
            .OrderBy(s => s.StartedAt)
            .Select(s => new FleetPoint(s.StartedAt, s.FleetSize!.Value))
            .ToList();

        // Last equipped per slot, across the whole library. Restricting to a
        // single session looked tidier but lost real gear: the newest session
        // recorded no arms, legs or core, so those slots vanished entirely.
        var allWorn = holdings
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
                        Names.Item(i.Key), i.Count(), i.Max(l => l.LastSeen),
                        Community.Item(i.Key)))
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
        var stash = StashView(holdings, everSeen: false);

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
