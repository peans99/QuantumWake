using Quantumwake.Core.Events;
using Quantumwake.Core.Locations;

namespace Quantumwake.Core.State;

/// <summary>Use of one ship during a session.</summary>
/// <param name="Sorties">
/// Number of flights, counted from control-token releases. This is the reliable
/// metric and should lead in any UI.
/// </param>
/// <param name="EstimatedTime">
/// Approximate time aboard. SC 4.9 logs no seat-entry event at all - 497 of 497
/// vehicle events are <c>ClearDriver</c> - so this is inferred as the span from
/// the last known ground anchor (a location visit, spawn, or previous flight)
/// to the moment control was released, capped to avoid absurd values across
/// idle gaps. Treat it as indicative, never exact.
/// </param>
public sealed record ShipUsage(
    string Model,
    string? Manufacturer,
    TimeSpan EstimatedTime,
    int Sorties)
{
    public string DisplayName => Manufacturer is null ? Model : $"{Manufacturer} {Model.Replace('_', ' ')}";
}

/// <summary>A visit to a location during a session.</summary>
public sealed record LocationVisit(
    DateTimeOffset At,
    string RawId,
    string DisplayName,
    string? System,
    string? Body,
    LocationKind Kind);

/// <summary>A quantum jump, with origin where the log provided one.</summary>
public sealed record QuantumJump(
    DateTimeOffset At,
    string? FromId,
    string? FromName,
    string ToId,
    string ToName);

/// <summary>How a contract ended, where the log says.</summary>
public enum ContractOutcome
{
    /// <summary>Seen, but no objective state reported.</summary>
    Unknown,
    InProgress,
    Completed,
    Abandoned
}

/// <summary>
/// A crafting blueprint the player was given. The notification toast is the
/// only place the log mentions blueprints at all, and it carries a short name
/// rather than the class - so the catalogue is matched by name, loosely.
/// </summary>
public sealed record BlueprintReceipt(DateTimeOffset At, string Name);

/// <summary>
/// Where the player turned up after dying.
/// </summary>
/// <remarks>
/// Star Citizen never logs the respawn point a player sets - there is no
/// regen event of any kind, and the one spawn event that existed
/// (<c>EASpawn PerformRespawn</c>) stopped being emitted in 4.9. So this is an
/// observation rather than a reading: someone died, and the next place the
/// logs named is where they woke. Being revived where you fell is excluded,
/// since the place did not change.
/// </remarks>
public sealed record RespawnRecord(
    DateTimeOffset At,
    string Place,
    DateTimeOffset DiedAt,
    string? DiedPlace);

/// <summary>A contract seen during a session.</summary>
public sealed record ContractRecord(
    DateTimeOffset FirstSeen,
    string Raw,
    string DisplayName,
    string Issuer,
    string? System,
    string? Difficulty,
    string? Type,
    bool Accepted)
{
    /// <summary>Mission id, used to join objective state onto the contract.</summary>
    public string? MissionId { get; init; }

    public ContractOutcome Outcome { get; init; } = ContractOutcome.Unknown;

    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Objective steps the game showed in the player's journal, and how many
    /// finished. The objective ids are opaque uuids, so the steps cannot be
    /// named - but "4 of 5 done" is the difference between a near miss and a
    /// walk-away, and neither was visible before.
    /// </summary>
    public int Steps { get; init; }

    public int StepsDone { get; init; }

    /// <summary>Wall-clock time from first sighting to completion.</summary>
    public TimeSpan? TimeToComplete => CompletedAt is null ? null : CompletedAt - FirstSeen;
}

/// <summary>A confirmed kiosk purchase.</summary>
/// <param name="Confirmed">
/// True only when the server answered <c>result[Success]</c>. Unconfirmed
/// requests are kept but excluded from spend totals, since the player may have
/// cancelled or lacked the funds.
/// </param>
/// <param name="Price">
/// The order total. The kiosk logs <c>client_price</c> as the whole line, not a
/// unit price - see <see cref="ShopRequestEvent"/>.
/// </param>
public sealed record PurchaseRecord(
    DateTimeOffset At,
    string Shop,
    string Item,
    decimal Price,
    int Quantity,
    bool Confirmed)
{
    public decimal Total => Price;

    public decimal UnitPrice => Quantity > 0 ? Price / Quantity : Price;
}

/// <summary>
/// A commodity bought or sold at a kiosk.
/// </summary>
/// <remarks>
/// No server response accompanies these, so they are requests rather than
/// confirmed settlements.
/// </remarks>
/// <param name="ResourceId">
/// The game's resource id for the commodity, lower-cased. Defaults to null so
/// sessions cached before it existed still deserialize; those trades stay
/// unnamed until a rescan.
/// </param>
public sealed record CommodityTrade(
    DateTimeOffset At,
    string Shop,
    decimal Amount,
    int Quantity,
    bool IsSell,
    string? Mode,
    string? ResourceId = null);

/// <summary>
/// One item seen in a character slot.
/// </summary>
/// <remarks>
/// <paramref name="LastSeen"/> is what makes a current kit recoverable.
/// Attachment events fire on every spawn and inventory refresh, so a slot
/// accumulates everything ever put in it - a hand slot ends up listing every
/// weapon, tool and drink the player has picked up. Only the most recent
/// sighting describes what is actually equipped.
/// </remarks>
public sealed record LoadoutItem(
    string Port,
    string ItemClass,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen);

/// <summary>
/// An item observed in a location's inventory.
/// </summary>
/// <remarks>
/// Item removals are never logged, so this is "seen at", not a live stock level.
/// </remarks>
public sealed record StashEntry(
    DateTimeOffset SeenAt,
    string LocationId,
    string LocationName,
    string ItemClass);

/// <summary>An entry on the session timeline.</summary>
public sealed record TimelineEntry(
    DateTimeOffset At,
    string Kind,
    string Text,
    string? Detail);

/// <summary>Everything known about one play session (one log file).</summary>
public sealed record SessionSummary
{
    public required string Id { get; init; }
    public required string SourceFile { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset EndedAt { get; init; }

    public string? Handle { get; init; }
    public string? Geid { get; init; }
    public string? BuildTag { get; init; }
    public string? GameVersion { get; init; }

    /// <summary>Wall-clock span of the log file.</summary>
    public TimeSpan Duration => EndedAt - StartedAt;

    /// <summary>
    /// Time attributable to the persistent universe rather than menus. This is
    /// the honest playtime figure; naive last-minus-first counts hangar idling.
    /// </summary>
    public TimeSpan InGameDuration { get; init; }

    public TimeSpan MenuDuration { get; init; }

    public IReadOnlyList<ShipUsage> Ships { get; init; } = [];
    public IReadOnlyList<LocationVisit> Locations { get; init; } = [];
    public IReadOnlyList<QuantumJump> Jumps { get; init; } = [];
    public IReadOnlyList<ContractRecord> Contracts { get; init; } = [];
    public IReadOnlyList<TimelineEntry> Timeline { get; init; } = [];
    public IReadOnlyList<PurchaseRecord> Purchases { get; init; } = [];
    public IReadOnlyList<LoadoutItem> Loadout { get; init; } = [];
    public IReadOnlyList<StashEntry> Stash { get; init; } = [];

    /// <summary>Largest owned-vehicle count seen this session, or null if not reported.</summary>
    public int? FleetSize { get; init; }

    public IReadOnlyList<CommodityTrade> Trades { get; init; } = [];

    /// <summary>Items observed entering the player's inventories.</summary>
    public IReadOnlyList<ItemPickup> Pickups { get; init; } = [];

    /// <summary>Crafting blueprints the game said were received this session.</summary>
    public IReadOnlyList<BlueprintReceipt> Blueprints { get; init; } = [];

    /// <summary>Where the player woke after dying - inferred, never stated.</summary>
    public IReadOnlyList<RespawnRecord> Respawns { get; init; } = [];

    /// <summary>Confirmed spend only.</summary>
    public decimal Spend => Purchases.Where(p => p.Confirmed).Sum(p => p.Total);

    /// <summary>Commodity sales - the only income the logs record.</summary>
    public decimal Income => Trades.Where(t => t.IsSell).Sum(t => t.Amount);

    /// <summary>Commodity purchases, kept apart from item purchases.</summary>
    public decimal CommoditySpend => Trades.Where(t => !t.IsSell).Sum(t => t.Amount);

    public decimal Net => Income - Spend - CommoditySpend;

    public int ContractsCompleted => Contracts.Count(c => c.Outcome == ContractOutcome.Completed);

    public int Incapacitations { get; init; }
    public int Deaths { get; init; }
    public int Kills { get; init; }
    public int Disconnects { get; init; }

    /// <summary>Distinct gamerules seen, with how many loading screens each had.</summary>
    public IReadOnlyDictionary<string, int> GameRules { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Most-flown ship, by sortie count.</summary>
    public string? PrimaryShip => Ships.MaxBy(s => s.Sorties)?.DisplayName;
    public string? LastLocation => Locations.Count > 0 ? Locations[^1].DisplayName : null;
}

/// <summary>
/// An item observed entering one of the player's inventories.
/// </summary>
/// <remarks>
/// A signal, not a certainty: the source event fires when the inventory UI
/// pages in an item it has not shown before, which covers looting but also
/// buying and receiving, and only while the inventory is open. Views built on
/// it say so.
/// </remarks>
public sealed record ItemPickup(DateTimeOffset At, string ItemClass);
