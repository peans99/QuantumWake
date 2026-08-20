using SCCompanion.Core.Events;
using SCCompanion.Core.Locations;

namespace SCCompanion.Core.State;

/// <summary>How much to trust the current location estimate.</summary>
public enum LocationConfidence
{
    /// <summary>Nothing seen yet.</summary>
    None,

    /// <summary>Inferred indirectly, e.g. just spawned or just left a menu.</summary>
    Low,

    /// <summary>Derived from a quantum arrival rather than a direct signal.</summary>
    Medium,

    /// <summary>A direct location signal, such as opening a local inventory.</summary>
    High
}

/// <summary>A snapshot of where the player is believed to be.</summary>
/// <param name="Current">Last known location, or null before anything is seen.</param>
/// <param name="TravellingTo">Destination while a quantum route is active.</param>
/// <param name="Confidence">Trust level for <paramref name="Current"/>.</param>
/// <param name="InGame">False while in the frontend/menus.</param>
/// <param name="GameRules">Last seen gamerules value, e.g. <c>SC_Default</c>.</param>
public sealed record PlayerLocation(
    ResolvedLocation? Current,
    ResolvedLocation? TravellingTo,
    LocationConfidence Confidence,
    bool InGame,
    string? GameRules,
    DateTimeOffset AsOf)
{
    public static readonly PlayerLocation Unknown =
        new(null, null, LocationConfidence.None, false, null, default);

    public bool IsTravelling => TravellingTo is not null;
}

/// <summary>A change in believed location, for the timeline and map trail.</summary>
public sealed record LocationChange(
    DateTimeOffset Timestamp,
    ResolvedLocation? From,
    ResolvedLocation To,
    LocationConfidence Confidence,
    bool ViaQuantum);

/// <summary>
/// Infers the player's location from discrete log signals.
/// </summary>
/// <remarks>
/// <para>
/// Star Citizen does not log player position (see docs/findings.md), so location
/// is reconstructed rather than read. Several weak signals are fused:
/// </para>
/// <list type="bullet">
///   <item><description>a local inventory request names an exact location - the strongest signal</description></item>
///   <item><description>a quantum route names a destination, so arrival can be inferred</description></item>
///   <item><description>spawning resets confidence, since the player may be somewhere new</description></item>
///   <item><description>gamerules and loading screens separate menu time from play</description></item>
/// </list>
/// <para>
/// Because the estimate can be wrong, <see cref="LocationConfidence"/> is part of
/// the public model: the UI shows uncertainty instead of implying precision the
/// logs cannot support.
/// </para>
/// </remarks>
public sealed class LocationStateMachine
{
    private readonly List<LocationChange> _history = [];

    /// <summary>Current best estimate.</summary>
    public PlayerLocation State { get; private set; } = PlayerLocation.Unknown;

    /// <summary>Every location change seen, oldest first.</summary>
    public IReadOnlyList<LocationChange> History => _history;

    /// <summary>Raised whenever the believed location changes.</summary>
    public event Action<LocationChange>? Changed;

    /// <summary>Feeds one event into the machine.</summary>
    public void Apply(GameEvent ev)
    {
        switch (ev)
        {
            case LocationInventoryEvent location:
                // A direct, unambiguous signal - and it also confirms arrival if a
                // quantum route was in flight.
                MoveTo(
                    ev.Timestamp,
                    LocationResolver.Resolve(location.LocationId),
                    LocationConfidence.High,
                    viaQuantum: State.IsTravelling);
                break;

            case QuantumTargetEvent target:
                BeginTravel(ev.Timestamp, target.Destination);
                break;

            case QuantumRouteEvent route:
                BeginTravel(ev.Timestamp, route.Destination);
                break;

            case ClientSpawnedEvent:
                // Spawning may put the player anywhere; keep the last known
                // location but stop claiming certainty about it.
                State = State with
                {
                    TravellingTo = null,
                    Confidence = State.Current is null ? LocationConfidence.None : LocationConfidence.Low,
                    InGame = true,
                    AsOf = ev.Timestamp
                };
                break;

            case LoadingScreenEvent loading:
                ApplyGameRules(loading.GameRules, ev.Timestamp);
                break;

            case ContextEvent context:
                ApplyGameRules(context.GameRules, ev.Timestamp);
                break;
        }
    }

    /// <summary>
    /// Records a quantum destination. Some route lines fire repeatedly for the
    /// same target, so an unchanged destination is ignored.
    /// </summary>
    private void BeginTravel(DateTimeOffset timestamp, string destinationId)
    {
        var destination = LocationResolver.Resolve(destinationId);

        if (State.TravellingTo?.RawId == destination.RawId)
            return;

        State = State with { TravellingTo = destination, AsOf = timestamp };
    }

    private void MoveTo(
        DateTimeOffset timestamp,
        ResolvedLocation destination,
        LocationConfidence confidence,
        bool viaQuantum)
    {
        var unchanged = State.Current?.RawId == destination.RawId;

        if (unchanged && !State.IsTravelling)
        {
            // Same place, but a fresh direct signal - refresh confidence only.
            State = State with { Confidence = confidence, AsOf = timestamp };
            return;
        }

        var change = new LocationChange(timestamp, State.Current, destination, confidence, viaQuantum);

        State = State with
        {
            Current = destination,
            TravellingTo = null,
            Confidence = confidence,
            InGame = true,
            AsOf = timestamp
        };

        if (unchanged)
            return;

        _history.Add(change);
        Changed?.Invoke(change);
    }

    /// <summary>
    /// <c>SC_Frontend</c> means menus and the hangar, not the persistent
    /// universe. Separating the two is what keeps playtime honest - the backup
    /// set is roughly 70% frontend lines.
    /// </summary>
    private void ApplyGameRules(string gameRules, DateTimeOffset timestamp)
    {
        var inGame = !gameRules.Equals("SC_Frontend", StringComparison.OrdinalIgnoreCase);

        State = State with
        {
            GameRules = gameRules,
            InGame = inGame,
            AsOf = timestamp
        };
    }

    /// <summary>Clears all state, for reuse across files.</summary>
    public void Reset()
    {
        State = PlayerLocation.Unknown;
        _history.Clear();
    }
}
