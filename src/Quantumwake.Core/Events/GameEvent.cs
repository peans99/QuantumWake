namespace Quantumwake.Core.Events;

/// <summary>
/// Base type for every semantic event extracted from Game.log.
/// </summary>
/// <remarks>
/// Events are deliberately small records rather than one wide type: CIG removes
/// log events patch over patch (quantum travel in 4.0.1, death scope in 4.0.2,
/// inter-system jumps in 4.1.0, combat entirely by 4.9), so a feature must be
/// able to lose one event family without taking the rest down with it.
/// </remarks>
public abstract record GameEvent(DateTimeOffset Timestamp)
{
    /// <summary>Stable name used for parser-health reporting and storage.</summary>
    public abstract string Kind { get; }
}

/// <summary>Session header: game build and version, from the top of each log file.</summary>
public sealed record SessionStartEvent(
    DateTimeOffset Timestamp,
    string? BuildTag,
    string? FileVersion) : GameEvent(Timestamp)
{
    public override string Kind => "session.start";
}

/// <summary>Account handle resolved at login. Appears once per session.</summary>
public sealed record LoginEvent(DateTimeOffset Timestamp, string Handle) : GameEvent(Timestamp)
{
    public override string Kind => "session.login";
}

/// <summary>Character record, carrying the persistent GEID.</summary>
public sealed record CharacterEvent(
    DateTimeOffset Timestamp,
    string Name,
    string Geid,
    string? AccountId,
    string? State) : GameEvent(Timestamp)
{
    public override string Kind => "session.character";
}

/// <summary>
/// A loading screen completing. The <paramref name="GameRules"/> value is the
/// cleanest signal for separating menu time from actual play.
/// </summary>
public sealed record LoadingScreenEvent(
    DateTimeOffset Timestamp,
    string Screen,
    string GameRules,
    double DurationSeconds) : GameEvent(Timestamp)
{
    public override string Kind => "session.loading";
}

/// <summary>Context establisher completion, carrying map, gamerules and shard session id.</summary>
public sealed record ContextEvent(
    DateTimeOffset Timestamp,
    string Establisher,
    string Map,
    string GameRules,
    string SessionId) : GameEvent(Timestamp)
{
    public override string Kind => "session.context";
}

/// <summary>Whether the local client took or released a vehicle's control token.</summary>
public enum SeatChange
{
    Entered,
    Left
}

/// <summary>
/// Local client taking or releasing control of a vehicle. Pairing
/// <see cref="SeatChange.Entered"/> with <see cref="SeatChange.Left"/> gives
/// time-in-seat per ship.
/// </summary>
public sealed record VehicleControlEvent(
    DateTimeOffset Timestamp,
    string VehicleId,
    string Model,
    string? Manufacturer,
    string EntityId,
    SeatChange Change) : GameEvent(Timestamp)
{
    public override string Kind => "vehicle.control";
}

/// <summary>
/// Player opened a location's inventory. The strongest discrete signal that the
/// player is physically at a given location.
/// </summary>
public sealed record LocationInventoryEvent(
    DateTimeOffset Timestamp,
    string Player,
    string LocationId) : GameEvent(Timestamp)
{
    public override string Kind => "location.inventory";
}

/// <summary>
/// A quantum route calculation. <paramref name="Origin"/> is null for the
/// shorter confirmation form, which names only the destination.
/// </summary>
public sealed record QuantumRouteEvent(
    DateTimeOffset Timestamp,
    string? Vehicle,
    string? Origin,
    string Destination) : GameEvent(Timestamp)
{
    public override string Kind => "quantum.route";
}

/// <summary>Player selected a quantum destination.</summary>
public sealed record QuantumTargetEvent(
    DateTimeOffset Timestamp,
    string? Vehicle,
    string Destination) : GameEvent(Timestamp)
{
    public override string Kind => "quantum.target";
}

/// <summary>A mission objective marker, identifying the contract behind it.</summary>
public sealed record ContractEvent(
    DateTimeOffset Timestamp,
    string MissionId,
    string? GeneratorName,
    string Contract,
    string? ContractDefinitionId) : GameEvent(Timestamp)
{
    public override string Kind => "contract.marker";
}

/// <summary>
/// A HUD notification. <paramref name="NotificationId"/> is the bracketed number
/// and is essential for deduplication: each notification fires 3-5 times with
/// differing Action values (Next, StartFade, Remove).
/// </summary>
public sealed record NotificationEvent(
    DateTimeOffset Timestamp,
    string Text,
    string NotificationId,
    string? MissionId) : GameEvent(Timestamp)
{
    public override string Kind => "hud.notification";

    /// <summary>True when this notification reports the player being downed.</summary>
    public bool IsIncapacitation =>
        Text.StartsWith("Incapacitated", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when this notification reports a contract being accepted.</summary>
    public bool IsContractAccepted =>
        Text.StartsWith("Contract Accepted", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the player used a medical bed. The game reports that the bed
    /// healed them, and separately explains how a regen location is reset -
    /// but never states where regen now is. This marks the act, nothing more.
    /// </summary>
    public bool IsMedicalBed =>
        Text.StartsWith("Medical Bed", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A ship being retrieved at a hangar or landing pad.
/// </summary>
/// <remarks>
/// The only signal that a ship was taken out. SC 4.9 logs no boarding event, so
/// without this a ship swap goes unrecorded until the player leaves the vehicle.
/// The line carries only an entity id - the model name arrives separately, on
/// quantum-navigation lines - so <see cref="VehicleIdentifiedEvent"/> is what
/// puts a name to it.
/// </remarks>
public sealed record VehicleSpawnEvent(
    DateTimeOffset Timestamp,
    string EntityId,
    string? LandingArea) : GameEvent(Timestamp)
{
    public override string Kind => "vehicle.spawn";
}

/// <summary>
/// Ties a vehicle entity id to its model, seen incidentally on another line.
/// </summary>
/// <remarks>
/// Ship names appear embedded in unrelated chatter as
/// <c>DRAK_Corsair_771478242932[771478242932]</c>. Harvesting them builds an
/// id-to-name registry, which is the only way to name a retrieved ship before
/// the player gets out of it.
/// </remarks>
public sealed record VehicleIdentifiedEvent(
    DateTimeOffset Timestamp,
    string EntityId,
    string VehicleId,
    string Model,
    string? Manufacturer) : GameEvent(Timestamp)
{
    public override string Kind => "vehicle.identified";
}

/// <summary>Local client spawned into the world.</summary>
public sealed record ClientSpawnedEvent(DateTimeOffset Timestamp) : GameEvent(Timestamp)
{
    public override string Kind => "session.spawned";
}

/// <summary>
/// Network channel disconnect. Note <c>reason="Nub destroyed"</c> is routine
/// teardown, not a crash, and should not be surfaced as an error.
/// </summary>
public sealed record DisconnectEvent(
    DateTimeOffset Timestamp,
    string Cause,
    string Reason,
    bool IsRemote) : GameEvent(Timestamp)
{
    public override string Kind => "net.disconnect";

    public bool IsRoutineTeardown =>
        Reason.Equals("Nub destroyed", StringComparison.OrdinalIgnoreCase);
}
