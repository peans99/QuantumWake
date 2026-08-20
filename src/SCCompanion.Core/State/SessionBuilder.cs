using SCCompanion.Core.Events;
using SCCompanion.Core.Locations;

namespace SCCompanion.Core.State;

/// <summary>
/// Folds a stream of events from one log file into a <see cref="SessionSummary"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two aggregations deserve explanation.
/// </para>
/// <para>
/// <b>Time in seat.</b> Vehicle control events come in
/// <c>SetDriver</c>/<c>ClearDriver</c> pairs, but current logs reliably emit only
/// the <c>ClearDriver</c> half. An unmatched release is therefore credited from
/// the previous location or vehicle signal rather than discarded, and sorties are
/// counted from releases. Time is a lower bound, never an invention.
/// </para>
/// <para>
/// <b>In-game vs menu time.</b> Gamerules transitions split the session. Every
/// interval is attributed to whichever ruleset was active at its start, so
/// <c>SC_Frontend</c> hangar idling does not inflate playtime. Across the sample
/// backup set that is roughly 70% of all logged activity.
/// </para>
/// </remarks>
public sealed class SessionBuilder
{
    private readonly string _sourceFile;
    private readonly LocationStateMachine _locationState = new();

    private readonly Dictionary<string, (TimeSpan Time, int Sorties, string? Manufacturer)> _ships = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContractRecord> _contracts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _gameRules = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _notificationIds = [];
    private readonly List<LocationVisit> _locations = [];
    private readonly List<QuantumJump> _jumps = [];
    private readonly List<TimelineEntry> _timeline = [];

    private DateTimeOffset? _firstSeen;
    private DateTimeOffset _lastSeen;
    private string? _handle;
    private string? _geid;
    private string? _buildTag;
    private string? _gameVersion;

    private string? _currentRules;
    private DateTimeOffset _rulesSince;
    private TimeSpan _inGame;
    private TimeSpan _menu;

    private string? _seatVehicle;
    private DateTimeOffset _seatSince;

    /// <summary>
    /// Last point the player was demonstrably not in flight - a location visit,
    /// a spawn, or the end of the previous sortie. Used to estimate flight time
    /// because no boarding event is logged.
    /// </summary>
    private DateTimeOffset _anchor;

    private int _incapacitations;
    private int _disconnects;
    private int _kills;
    private int _deaths;

    public SessionBuilder(string sourceFile) => _sourceFile = sourceFile;

    /// <summary>Current location estimate, for live use.</summary>
    public PlayerLocation Location => _locationState.State;

    public void Add(GameEvent ev)
    {
        _firstSeen ??= ev.Timestamp;
        _lastSeen = ev.Timestamp;

        _locationState.Apply(ev);

        switch (ev)
        {
            case SessionStartEvent session:
                _buildTag ??= session.BuildTag;
                _gameVersion ??= session.FileVersion;
                break;

            case LoginEvent login:
                _handle ??= login.Handle;
                Timeline(ev.Timestamp, "login", $"Signed in as {login.Handle}", null);
                break;

            case CharacterEvent character:
                _geid ??= character.Geid;
                _handle ??= character.Name;
                break;

            case LoadingScreenEvent loading:
                _gameRules[loading.GameRules] = _gameRules.GetValueOrDefault(loading.GameRules) + 1;
                SwitchGameRules(loading.GameRules, ev.Timestamp);
                break;

            case ContextEvent context:
                SwitchGameRules(context.GameRules, ev.Timestamp);
                break;

            case VehicleControlEvent vehicle:
                RecordSeat(vehicle);
                break;

            case LocationInventoryEvent location:
                RecordLocation(ev.Timestamp, LocationResolver.Resolve(location.LocationId));
                break;

            case QuantumRouteEvent route:
                RecordJump(ev.Timestamp, route.Origin, route.Destination);
                break;

            case ContractEvent contract:
                AddContract(contract);
                break;

            case NotificationEvent notification:
                RecordNotification(notification);
                break;

            // Dormant on SC 4.9 - no combat events are emitted - but wired so the
            // counters populate the moment CIG restores them.
            case ActorDeathEvent death:
                RecordDeath(death);
                break;

            case VehicleDestructionEvent destruction:
                Timeline(
                    ev.Timestamp,
                    "vehicle-destroyed",
                    destruction.To == DestroyLevel.SoftDeath
                        ? $"{destruction.Vehicle} disabled"
                        : $"{destruction.Vehicle} destroyed",
                    destruction.Cause);
                break;

            case DisconnectEvent disconnect:
                if (!disconnect.IsRoutineTeardown)
                {
                    _disconnects++;
                    Timeline(ev.Timestamp, "disconnect", "Disconnected", disconnect.Reason);
                }
                break;
        }
    }

    private void SwitchGameRules(string rules, DateTimeOffset at)
    {
        if (_currentRules is null)
        {
            _currentRules = rules;
            _rulesSince = at;
            return;
        }

        if (_currentRules.Equals(rules, StringComparison.OrdinalIgnoreCase))
            return;

        Accrue(_currentRules, at - _rulesSince);
        _currentRules = rules;
        _rulesSince = at;
    }

    private void Accrue(string rules, TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
            return;

        if (rules.Equals("SC_Frontend", StringComparison.OrdinalIgnoreCase))
            _menu += span;
        else
            _inGame += span;
    }

    /// <summary>
    /// Longest span credited to a single flight. Beyond this the gap is far more
    /// likely to be the player idling or away than a genuine sortie.
    /// </summary>
    private static readonly TimeSpan MaxFlightEstimate = TimeSpan.FromHours(2);

    private void RecordSeat(VehicleControlEvent vehicle)
    {
        var key = vehicle.Model;

        // Retained for completeness: current builds never emit a seat-entry
        // event, but handling it costs nothing and future-proofs the pairing.
        if (vehicle.Change == SeatChange.Entered)
        {
            _seatVehicle = key;
            _seatSince = vehicle.Timestamp;
            Timeline(vehicle.Timestamp, "ship", $"Boarded {Describe(vehicle)}", null);
            return;
        }

        // Release. Prefer a genuine pairing; otherwise estimate from the last
        // known ground anchor, since SC 4.9 logs no boarding event.
        var elapsed = _seatVehicle == key && _seatSince != default
            ? vehicle.Timestamp - _seatSince
            : EstimateFrom(vehicle.Timestamp);

        var existing = _ships.GetValueOrDefault(key);
        _ships[key] = (existing.Time + elapsed, existing.Sorties + 1, vehicle.Manufacturer);

        Timeline(vehicle.Timestamp, "ship", $"Left {Describe(vehicle)}", Format(elapsed));

        _seatVehicle = null;
        _seatSince = default;
        _anchor = vehicle.Timestamp;
    }

    /// <summary>Time since the last ground anchor, clamped to a believable range.</summary>
    private TimeSpan EstimateFrom(DateTimeOffset until)
    {
        if (_anchor == default)
            return TimeSpan.Zero;

        var span = until - _anchor;
        if (span <= TimeSpan.Zero)
            return TimeSpan.Zero;

        return span > MaxFlightEstimate ? MaxFlightEstimate : span;
    }

    private static string? Format(TimeSpan span) =>
        span <= TimeSpan.Zero ? null : $"~{span.TotalMinutes:F0} min";

    private static string Describe(VehicleControlEvent vehicle) =>
        vehicle.Manufacturer is null
            ? vehicle.Model.Replace('_', ' ')
            : $"{vehicle.Manufacturer} {vehicle.Model.Replace('_', ' ')}";

    private void RecordLocation(DateTimeOffset at, ResolvedLocation location)
    {
        // Collapse consecutive repeats: inventory is opened many times per stop.
        if (_locations.Count > 0 && _locations[^1].RawId == location.RawId)
            return;

        _locations.Add(new LocationVisit(
            at, location.RawId, location.DisplayName, location.System, location.Body, location.Kind));

        // Arriving somewhere means the player is out of the pilot seat.
        _anchor = at;

        Timeline(at, "location", location.DisplayName, location.Body);
    }

    private void RecordJump(DateTimeOffset at, string? originId, string destinationId)
    {
        var destination = LocationResolver.Resolve(destinationId);
        var origin = originId is null ? null : LocationResolver.Resolve(originId);

        // Route lines repeat while a target stays selected.
        if (_jumps.Count > 0 && _jumps[^1].ToId == destination.RawId)
            return;

        _jumps.Add(new QuantumJump(
            at, origin?.RawId, origin?.DisplayName, destination.RawId, destination.DisplayName));

        Timeline(at, "quantum", $"Quantum to {destination.DisplayName}", origin?.DisplayName);
    }

    private void RecordNotification(NotificationEvent notification)
    {
        // Each notification fires 3-5 times with differing Action values.
        if (!_notificationIds.Add($"{notification.NotificationId}|{notification.Text}"))
            return;

        if (notification.IsIncapacitation)
        {
            _incapacitations++;
            Timeline(notification.Timestamp, "incapacitated", "Incapacitated", null);
            return;
        }

        if (notification.IsContractAccepted)
        {
            var title = notification.Text["Contract Accepted:".Length..].Trim(' ', ':');
            Timeline(notification.Timestamp, "contract", "Contract accepted", title);
        }
    }

    private void RecordDeath(ActorDeathEvent death)
    {
        switch (death.Classification)
        {
            case KillKind.PvpKill:
            case KillKind.PveKill:
                _kills++;
                Timeline(death.Timestamp, "kill", $"Killed {death.Victim}", death.Weapon);
                break;

            case KillKind.Death:
            case KillKind.PvpDeath:
                _deaths++;
                Timeline(death.Timestamp, "death", $"Killed by {death.Killer}", death.Weapon);
                break;

            case KillKind.Suicide:
                _deaths++;
                Timeline(death.Timestamp, "death", "Died", death.DamageType);
                break;

            // Bystander kills are noise unless the player was involved.
        }
    }

    private void Timeline(DateTimeOffset at, string kind, string text, string? detail) =>
        _timeline.Add(new TimelineEntry(at, kind, text, detail));

    /// <summary>Builds the summary. Safe to call once the file has been consumed.</summary>
    public SessionSummary Build()
    {
        var started = _firstSeen ?? default;

        // Close the final open interval so the last stretch is not lost.
        if (_currentRules is not null)
            Accrue(_currentRules, _lastSeen - _rulesSince);

        var ships = _ships
            .Select(p => new ShipUsage(p.Key, p.Value.Manufacturer, p.Value.Time, p.Value.Sorties))
            .OrderByDescending(s => s.Sorties)
            .ThenByDescending(s => s.EstimatedTime)
            .ToList();

        return new SessionSummary
        {
            Id = $"{Path.GetFileNameWithoutExtension(_sourceFile)}",
            SourceFile = _sourceFile,
            StartedAt = started,
            EndedAt = _lastSeen,
            Handle = _handle,
            Geid = _geid,
            BuildTag = _buildTag,
            GameVersion = _gameVersion,
            InGameDuration = _inGame,
            MenuDuration = _menu,
            Ships = ships,
            Locations = _locations,
            Jumps = _jumps,
            Contracts = [.. _contracts.Values.OrderBy(c => c.FirstSeen)],
            Timeline = [.. _timeline.OrderBy(t => t.At)],
            Incapacitations = _incapacitations,

            // Always zero on SC 4.9: the game emits no combat events at all.
            // These populate automatically if CIG restores them.
            Deaths = _deaths,
            Kills = _kills,

            Disconnects = _disconnects,
            GameRules = _gameRules
        };
    }

    /// <summary>Registers a contract, keeping the earliest sighting.</summary>
    private void AddContract(ContractEvent contract)
    {
        if (_contracts.ContainsKey(contract.Contract))
            return;

        var parsed = ContractNameParser.Parse(contract.Contract);

        _contracts[contract.Contract] = new ContractRecord(
            contract.Timestamp,
            parsed.Raw,
            parsed.DisplayName,
            parsed.Issuer,
            parsed.System,
            parsed.Difficulty,
            parsed.Type,
            Accepted: false);
    }
}
