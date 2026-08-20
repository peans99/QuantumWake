using SCCompanion.Core.Events;
using SCCompanion.Core.Locations;

namespace SCCompanion.Core.State;

/// <summary>Time spent in one ship during a session.</summary>
public sealed record ShipUsage(
    string Model,
    string? Manufacturer,
    TimeSpan TimeInSeat,
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

/// <summary>A contract seen during a session.</summary>
public sealed record ContractRecord(
    DateTimeOffset FirstSeen,
    string Raw,
    string DisplayName,
    string Issuer,
    string? System,
    string? Difficulty,
    string? Type,
    bool Accepted);

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

    public int Incapacitations { get; init; }
    public int Deaths { get; init; }
    public int Kills { get; init; }
    public int Disconnects { get; init; }

    /// <summary>Distinct gamerules seen, with how many loading screens each had.</summary>
    public IReadOnlyDictionary<string, int> GameRules { get; init; } =
        new Dictionary<string, int>();

    public string? PrimaryShip => Ships.MaxBy(s => s.TimeInSeat)?.DisplayName;
    public string? LastLocation => Locations.Count > 0 ? Locations[^1].DisplayName : null;
}
