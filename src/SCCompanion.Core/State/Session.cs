using SCCompanion.Core.Events;
using SCCompanion.Core.Locations;

namespace SCCompanion.Core.State;

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

    /// <summary>Wall-clock time from first sighting to completion.</summary>
    public TimeSpan? TimeToComplete => CompletedAt is null ? null : CompletedAt - FirstSeen;
}

/// <summary>A confirmed kiosk purchase.</summary>
/// <param name="Confirmed">
/// True only when the server answered <c>result[Success]</c>. Unconfirmed
/// requests are kept but excluded from spend totals, since the player may have
/// cancelled or lacked the funds.
/// </param>
public sealed record PurchaseRecord(
    DateTimeOffset At,
    string Shop,
    string Item,
    decimal Price,
    int Quantity,
    bool Confirmed)
{
    public decimal Total => Price * Quantity;
}

/// <summary>
/// A commodity bought or sold at a kiosk.
/// </summary>
/// <remarks>
/// No server response accompanies these, so they are requests rather than
/// confirmed settlements.
/// </remarks>
public sealed record CommodityTrade(
    DateTimeOffset At,
    string Shop,
    decimal Amount,
    int Quantity,
    bool IsSell,
    string? Mode);

/// <summary>One equipped item, at the slot it occupied.</summary>
public sealed record LoadoutItem(
    string Port,
    string ItemClass,
    DateTimeOffset FirstSeen);

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
