using Quantumwake.Core.Events;
using Quantumwake.Core.Locations;

namespace Quantumwake.Core.State;

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
    private readonly List<PurchaseRecord> _purchases = [];
    private readonly List<CommodityTrade> _trades = [];
    private readonly List<ItemPickup> _pickups = [];
    private readonly HashSet<string> _pickupClasses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LoadoutItem> _loadoutSeen = new(StringComparer.Ordinal);

    /// <summary>Mission id to contract key, so objective state can find its contract.</summary>
    private readonly Dictionary<string, string> _contractsByMission = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ObjectiveState> _objectiveStates = new(StringComparer.Ordinal);

    private readonly HashSet<string> _blueprints = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BlueprintReceipt> _blueprintReceipts = [];

    private readonly List<RespawnRecord> _respawns = [];
    private readonly List<MedicalBedVisit> _medicalBeds = [];
    private readonly List<PartyNote> _partyNotes = [];
    private readonly List<ChannelNote> _channelNotes = [];

    /// <summary>Set by a death or incapacitation, cleared by the location that answers it.</summary>
    private DateTimeOffset? _awaitingRespawn;
    private string? _diedAt;
    private string _respawnCause = "death";

    /// <summary>
    /// How long after going down an arrival still counts as waking up there.
    /// Long enough for a loading screen and the walk out of a medical bay,
    /// short enough that the next place flown to is not mistaken for a regen.
    /// </summary>
    private static readonly TimeSpan RespawnWindow = TimeSpan.FromMinutes(10);

    /// <summary>Notes that the player went down, for the next place to answer.</summary>
    private void ArmRespawn(DateTimeOffset at, string cause)
    {
        _awaitingRespawn = at;
        _respawnCause = cause;
        _diedAt = _locationState.State.Current?.DisplayName;
    }

    /// <summary>Mission id to its journal-visible objectives and their states.</summary>
    private readonly Dictionary<string, Dictionary<string, ObjectiveState>> _objectiveSteps =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Latest inventory listing per scope key. Replaced wholesale on each query,
    /// so it always holds current contents rather than an accumulation.
    /// </summary>
    private readonly Dictionary<string, (DateTimeOffset At, List<string> Items)> _listings = new(StringComparer.Ordinal);

    /// <summary>Opaque inventory scope key to the location id it belongs to.</summary>
    private readonly Dictionary<string, string> _locationKeys = new(StringComparer.Ordinal);

    private ShopRequestEvent? _pendingPurchase;
    private string? _lastLocationId;
    private int? _fleetSize;

    /// <summary>Index of a jump whose destination was a category, awaiting arrival.</summary>
    private int? _pendingGenericJump;

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

    /// <summary>When something last happened to the player's body, if it has.</summary>
    private DateTimeOffset? _lastCasualty;

    /// <summary>When the player last left the menus for the world.</summary>
    private DateTimeOffset? _enteredGame;

    /// <summary>
    /// Waking up at login, treatment after a casualty, or a bed used to heal.
    /// </summary>
    /// <remarks>
    /// Three minutes because a login lands the player in a bed and the toast
    /// follows the spawn immediately; fifteen because crawling to a bed after a
    /// fight takes a while, and anything longer starts collecting beds that
    /// have nothing to do with the death.
    /// </remarks>
    private string BedKind(DateTimeOffset at)
    {
        if (_lastCasualty is { } hurt && at - hurt <= TimeSpan.FromMinutes(15))
            return "after-death";

        var arrived = _enteredGame ?? _firstSeen;

        if (arrived is { } spawned && at - spawned <= TimeSpan.FromMinutes(3) && _lastCasualty is null)
            return "wake";

        return "heal";
    }


    /// <summary>Timestamp of the last corpse-item line, for burst grouping.</summary>
    private DateTimeOffset? _lastCorpseAt;
    private readonly HashSet<string> _corpseItems = [];

    // Vehicle entity id registries. Retrieval lines carry only an id, so the
    // display name has to be joined in from incidental sightings elsewhere.
    private readonly Dictionary<string, string> _vehicleNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Model, string? Manufacturer)> _vehicleModels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VehicleSpawnEvent> _pendingSpawns = new(StringComparer.Ordinal);
    private readonly HashSet<string> _spawnedVehicles = [];
    private readonly HashSet<string> _creditedSpawns = [];

    private string? _currentVehicleId;

    /// <summary>Ship most recently retrieved, once its name is known.</summary>
    public string? CurrentShip =>
        _currentVehicleId is not null && _vehicleNames.TryGetValue(_currentVehicleId, out var name)
            ? name
            : null;

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

            case VehicleIdentifiedEvent identified:
                _vehicleNames[identified.EntityId] = Describe(identified.Manufacturer, identified.Model);
                _vehicleModels[identified.EntityId] = (identified.Model, identified.Manufacturer);
                ResolvePendingSpawns();
                break;

            case VehicleSpawnEvent spawn:
                RecordSpawn(spawn);
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

            case ShopRequestEvent request:
                // Held until the server confirms; an unanswered request is not spend.
                _pendingPurchase = request;
                break;

            case ShopFlowResponseEvent response:
                ResolvePurchase(response);
                break;

            case CommodityTradeEvent trade:
                _trades.Add(new CommodityTrade(
                    trade.Timestamp, PrettyShop(trade.ShopName), trade.Amount,
                    trade.Quantity, trade.IsSell, trade.TransactionMode, trade.ResourceId));

                Timeline(
                    trade.Timestamp,
                    trade.IsSell ? "sold" : "bought",
                    $"{(trade.IsSell ? "Sold" : "Bought")} {trade.Quantity} SCU",
                    $"{trade.Amount:N0} aUEC · {PrettyShop(trade.ShopName)}");
                break;


            case MissionObjectiveEvent objective:
                ApplyObjectiveState(objective);
                break;

            case AttachmentEvent attachment:
                RecordAttachment(attachment);
                break;

            // The scope key is opaque, but the query follows the named location
            // request, so the two can be bound together.
            case InventoryQueryEvent query when query.IsLocation:
                if (_lastLocationId is not null)
                    _locationKeys[query.ScopeKey] = _lastLocationId;

                // A query lists only the page currently on screen - the lines are
                // literally tagged "End Page" - so listings are unioned across
                // the session rather than replacing each other. One session's
                // union is the best available picture of a location's contents.
                if (_listings.TryGetValue(query.ScopeKey, out var open))
                    _listings[query.ScopeKey] = (query.Timestamp, open.Items);
                else
                    _listings[query.ScopeKey] = (query.Timestamp, []);
                break;

            case InventoryItemEvent item:
                // First sighting per item class this session. The event is a
                // listing, not a transfer - browsing a full stash pages in
                // everything it holds - so only the first appearance carries
                // any acquisition signal, and the Loot view dedupes again
                // across sessions to when a class was first seen at all.
                if (_pickupClasses.Add(item.ItemClass))
                    _pickups.Add(new ItemPickup(item.Timestamp, item.ItemClass));

                if (item.IsLocation)
                    RecordStashItem(item);
                break;

            case FleetQueryEvent fleet:
                // Take the largest seen: the count grows as ships are bought.
                _fleetSize = Math.Max(_fleetSize ?? 0, fleet.Vehicles);
                break;

            case NotificationEvent notification:
                RecordNotification(notification);
                break;

            // Dormant on SC 4.9 - no combat events are emitted - but wired so the
            // counters populate the moment CIG restores them.
            case ActorDeathEvent death:
                RecordDeath(death);
                break;

            case CorpseItemEvent corpse:
                RecordCorpse(corpse);
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

        // Leaving the menus is the moment the player is standing in the world -
        // and for anyone who logged out in a bed, the moment they wake up in
        // one. Measuring from the log start instead would count the launcher
        // and the loading screen, which is why a wake-up looked like a trip to
        // a clinic nine minutes in.
        if (_currentRules.Equals("SC_Frontend", StringComparison.OrdinalIgnoreCase))
            _enteredGame = at;

        _currentRules = rules;
        _rulesSince = at;
    }

    private void Accrue(string rules, TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
            return;

        if (IsMenu(rules))
            _menu += span;
        else
            _inGame += span;
    }

    /// <summary>SC_Frontend is the menu shell; everything else is being in the game.</summary>
    private static bool IsMenu(string rules) =>
        rules.Equals("SC_Frontend", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Playtime including the stretch still open, without banking it.
    /// </summary>
    /// <remarks>
    /// <see cref="Build"/> used to close the open interval by calling
    /// <see cref="Accrue"/>, which is harmless when a file is consumed once and
    /// built once - and ruinous live, where every event and every broadcast
    /// rebuilds. Nothing advanced <c>_rulesSince</c>, so each rebuild added the
    /// whole span again on top of the last: an hour-long session archived on
    /// rotation with a playtime measured in weeks. Returning the total instead
    /// of accumulating it makes Build safe to call as often as anyone likes.
    /// </remarks>
    private (TimeSpan InGame, TimeSpan Menu) Playtime()
    {
        if (_currentRules is null)
            return (_inGame, _menu);

        var open = _lastSeen - _rulesSince;

        if (open <= TimeSpan.Zero)
            return (_inGame, _menu);

        return IsMenu(_currentRules)
            ? (_inGame, _menu + open)
            : (_inGame + open, _menu);
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

    /// <summary>
    /// Records a ship being retrieved. The name may not be known yet - the spawn
    /// line carries only an entity id - so unnamed spawns are parked and resolved
    /// when a sighting names them.
    /// </summary>
    private void RecordSpawn(VehicleSpawnEvent spawn)
    {
        // The same retrieval logs a "spawning" then a "spawned" line.
        if (!_spawnedVehicles.Add(spawn.EntityId))
            return;

        _currentVehicleId = spawn.EntityId;

        if (_vehicleNames.TryGetValue(spawn.EntityId, out var name))
        {
            Timeline(spawn.Timestamp, "ship", $"Retrieved {name}", spawn.LandingArea);
            CreditSpawnedShip(spawn.EntityId);
        }
        else
        {
            _pendingSpawns[spawn.EntityId] = spawn;
        }
    }

    /// <summary>Names any retrieval that was waiting on an identification.</summary>
    private void ResolvePendingSpawns()
    {
        if (_pendingSpawns.Count == 0)
            return;

        foreach (var entityId in _pendingSpawns.Keys.ToList())
        {
            if (!_vehicleNames.TryGetValue(entityId, out var name))
                continue;

            var spawn = _pendingSpawns[entityId];
            _pendingSpawns.Remove(entityId);

            Timeline(spawn.Timestamp, "ship", $"Retrieved {name}", spawn.LandingArea);
            CreditSpawnedShip(entityId);
        }
    }

    /// <summary>
    /// Counts a retrieval as a sortie even if the player never disembarks, so a
    /// ship swap shows up immediately rather than only on exit.
    /// </summary>
    private void CreditSpawnedShip(string entityId)
    {
        if (!_vehicleModels.TryGetValue(entityId, out var vehicle))
            return;

        if (!_creditedSpawns.Add(entityId))
            return;

        var existing = _ships.GetValueOrDefault(vehicle.Model);
        _ships[vehicle.Model] = (existing.Time, existing.Sorties + 1, vehicle.Manufacturer);
    }

    private static string Describe(string? manufacturer, string model) =>
        manufacturer is null ? model.Replace('_', ' ') : $"{manufacturer} {model.Replace('_', ' ')}";

    private static string Describe(VehicleControlEvent vehicle) =>
        vehicle.Manufacturer is null
            ? vehicle.Model.Replace('_', ' ')
            : $"{vehicle.Manufacturer} {vehicle.Model.Replace('_', ' ')}";

    private void RecordLocation(DateTimeOffset at, ResolvedLocation location)
    {
        // Remembered even when the visit itself is a repeat, because the
        // inventory scope query that follows needs it to bind its key.
        _lastLocationId = location.RawId;

        // The first place seen after going down: where the player woke up.
        // Somewhere else entirely means they respawned there; the same place
        // means they were revived where they fell, which is not a respawn and
        // must not be recorded as one. Beyond the window the arrival has
        // nothing to do with it, and answering anyway would invent a regen
        // point out of wherever they happened to fly next.
        if (_awaitingRespawn is { } wentDownAt)
        {
            if (at - wentDownAt > RespawnWindow)
            {
                _awaitingRespawn = null;
            }
            else if (!string.Equals(location.DisplayName, _diedAt, StringComparison.OrdinalIgnoreCase))
            {
                _awaitingRespawn = null;

                _respawns.Add(new RespawnRecord(
                    at, location.DisplayName, wentDownAt, _diedAt, _respawnCause));

                Timeline(at, "respawn", "Woke up", location.DisplayName);
            }
        }

        ResolveGenericJump(location);

        // Collapse consecutive repeats: inventory is opened many times per stop.
        if (_locations.Count > 0 && _locations[^1].RawId == location.RawId)
            return;

        _locations.Add(new LocationVisit(
            at, location.RawId, location.DisplayName, location.System, location.Body, location.Kind));

        // Arriving somewhere means the player is out of the pilot seat.
        _anchor = at;

        Timeline(at, "location", location.DisplayName, location.Body);
    }

    /// <summary>
    /// Rewrites a jump whose destination was a category to the place actually
    /// reached, so "Rest Stop" becomes "microTech LEO Rest Stop".
    /// </summary>
    private void ResolveGenericJump(ResolvedLocation arrival)
    {
        if (_pendingGenericJump is not { } index || index >= _jumps.Count)
            return;

        _pendingGenericJump = null;

        // Arriving back where the jump started means we never went anywhere.
        var jump = _jumps[index];
        if (jump.FromId == arrival.RawId)
            return;

        _jumps[index] = jump with { ToId = arrival.RawId, ToName = arrival.DisplayName };

        // Keep the timeline consistent with the corrected destination.
        for (var i = _timeline.Count - 1; i >= 0; i--)
        {
            if (_timeline[i].Kind != "quantum")
                continue;

            _timeline[i] = _timeline[i] with { Text = $"Quantum to {arrival.DisplayName}" };
            break;
        }
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

        // "ObjectContainer_RestStop" and friends name a category, not a place.
        // Remember the jump so the actual arrival can replace it.
        _pendingGenericJump = LocationResolver.IsAmbiguous(destination.RawId)
            ? _jumps.Count - 1
            : null;

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
            _lastCasualty = notification.Timestamp;

            // Being incapacitated and waking somewhere else is a respawn, and
            // the commoner kind: a corpse-recovery burst only follows a full
            // death, so arming on death alone missed most of them.
            ArmRespawn(notification.Timestamp, "incapacitated");

            Timeline(notification.Timestamp, "incapacitated", "Incapacitated", null);
            return;
        }

        // The party channel is the only place another player is ever named, so
        // these are kept whole rather than counted: who, when, and what happened.
        // Lines naming nobody - a join queue opening, a broadcast, the one
        // garbled string in this corpus - read as null and stop here.
        // Boarding is a different fact from grouping, and the two channels share
        // two of their titles - so this is asked first, on the body, and returns
        // rather than falling through to a reader that would decline it anyway.
        if (ShipChannel.IsChannel(notification.Text))
        {
            if (ShipChannel.Read(notification.Timestamp, notification.Text) is { } berth)
            {
                _channelNotes.Add(berth);

                // Only other people reach the timeline. "You have joined channel"
                // fires on every ship the reader boards - 410 times here - and a
                // feed saying you got into your own Cyclone buries the flight.
                if (berth.Moment is ChannelMoment.TheyBoarded)
                    Timeline(berth.At, "party", $"{berth.Handle} came aboard", berth.Ship);
            }

            return;
        }

        if (notification.IsParty)
        {
            if (Party.Read(notification.Timestamp, notification.Text) is { } note)
            {
                _partyNotes.Add(note);

                // Lead changing hands does not reach the timeline. It is real,
                // but it happens in flurries while a party re-forms, and a feed
                // saying "X is now leader" five times in a minute buries the
                // flight it is meant to describe.
                //
                // Joining and leaving are worded apart from coming online and
                // dropping on purpose: somebody who logs out and back in did not
                // leave, and a feed that says the same thing for both makes a
                // friend with a poor connection look like one who walked off.
                var said = note.Moment switch
                {
                    PartyMoment.Connected => $"{note.Handle} came online",
                    PartyMoment.Disconnected => $"{note.Handle} dropped",
                    PartyMoment.Joined => $"{note.Handle} joined the party",
                    PartyMoment.Left => $"{note.Handle} left the party",
                    _ => null,
                };

                if (said is not null)
                    Timeline(note.At, "party", said, "in your party");
            }

            return;
        }

        if (notification.IsContractAccepted)
        {
            var title = notification.Text["Contract Accepted:".Length..].Trim(' ', ':');
            Timeline(notification.Timestamp, "contract", "Contract accepted", title);
            return;
        }

        // A medical bed is where regen gets set, so using one at a known place
        // is the strongest hint the logs offer about where a player will wake -
        // stronger than waiting for the next death, though still only a hint,
        // since a bed also just heals. Repeat toasts at the same place inside
        // an hour are one visit.
        //
        // The game prints this same line for the hab bed you wake up in at
        // login, which is why the kind is worked out from what surrounds it
        // rather than from the text - see MedicalBedVisit.Kind.
        if (notification.IsMedicalBed)
        {
            var place = _locationState.State.Current?.DisplayName;

            var repeat = place is not null
                && _medicalBeds.Count > 0
                && string.Equals(_medicalBeds[^1].Place, place, StringComparison.OrdinalIgnoreCase)
                && notification.Timestamp - _medicalBeds[^1].At < TimeSpan.FromHours(1);

            if (place is not null && !repeat)
            {
                var kind = BedKind(notification.Timestamp);

                _medicalBeds.Add(new MedicalBedVisit(notification.Timestamp, place, kind));

                Timeline(notification.Timestamp, "medbed", kind switch
                {
                    "wake" => "Woke up here",
                    "after-death" => "Treated at a medical bed",
                    _ => "Used a bed",
                }, place);
            }

            return;
        }

        // Blueprints arrive only as this notification - nothing else in the log
        // says which recipes a player holds, so the toast IS the record.
        const string blueprintPrefix = "Received Blueprint:";

        if (notification.Text.StartsWith(blueprintPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = notification.Text[blueprintPrefix.Length..].Trim(' ', ':');

            if (name.Length > 0 && _blueprints.Add(name))
            {
                _blueprintReceipts.Add(new BlueprintReceipt(notification.Timestamp, name));
                Timeline(notification.Timestamp, "blueprint", "Blueprint received", name);
            }
        }
    }

    /// <summary>
    /// Matches a server response to the request it answers. Requests and
    /// responses arrive in order at the same kiosk, so the most recent pending
    /// request is the one being answered.
    /// </summary>
    private void ResolvePurchase(ShopFlowResponseEvent response)
    {
        if (_pendingPurchase is null)
            return;

        // Ignore responses for a different kiosk; the pairing would be a guess.
        if (!string.IsNullOrEmpty(response.KioskId)
            && !response.KioskId.Equals(_pendingPurchase.KioskId, StringComparison.Ordinal))
        {
            return;
        }

        // Intermediate states such as BuyRequestProcessing precede the outcome.
        if (!response.Succeeded)
            return;

        var request = _pendingPurchase;
        _pendingPurchase = null;

        var record = new PurchaseRecord(
            request.Timestamp,
            PrettyShop(request.ShopName),
            request.ItemName,
            request.Price,
            request.Quantity,
            Confirmed: true);

        _purchases.Add(record);

        Timeline(
            response.Timestamp,
            response.IsSelling ? "sold" : "bought",
            $"{(response.IsSelling ? "Sold" : "Bought")} {request.ItemName}",
            $"{record.Total:N0} aUEC · {record.Shop}");
    }

    /// <summary><c>SCShop_OmegaPro_NewBabbage</c> becomes <c>Omega Pro, New Babbage</c>.</summary>
    private static string PrettyShop(string raw)
    {
        var trimmed = raw.StartsWith("SCShop_", StringComparison.OrdinalIgnoreCase)
            ? raw["SCShop_".Length..]
            : raw;

        return trimmed.Replace('_', ' ').Replace('-', ' ').Trim();
    }

    /// <summary>
    /// Applies objective state to the contract that owns it. A contract counts as
    /// completed once any of its objectives completes, and abandoned only if
    /// nothing completed and something was withdrawn.
    /// </summary>
    private void ApplyObjectiveState(MissionObjectiveEvent objective)
    {
        _objectiveStates[objective.MissionId] = objective.State;

        // Journal-visible steps only, kept per objective id because each one is
        // upserted repeatedly as its state changes.
        if (objective.ShownInLog)
        {
            if (!_objectiveSteps.TryGetValue(objective.MissionId, out var steps))
                _objectiveSteps[objective.MissionId] =
                    steps = new Dictionary<string, ObjectiveState>(StringComparer.Ordinal);

            steps[objective.ObjectiveId] = objective.State;
        }

        var key = _contractsByMission.GetValueOrDefault(objective.MissionId);
        if (key is null || !_contracts.TryGetValue(key, out var contract))
            return;

        var outcome = objective.State switch
        {
            ObjectiveState.Completed => ContractOutcome.Completed,
            ObjectiveState.Withdrawn => ContractOutcome.Abandoned,
            ObjectiveState.Failed => ContractOutcome.Abandoned,
            ObjectiveState.InProgress => ContractOutcome.InProgress,
            _ => ContractOutcome.Unknown
        };

        // Step counts are recorded whatever the outcome: a contract abandoned
        // four steps in is a different story from one dropped at the first.
        var progressed = contract with
        {
            Steps = StepCount(objective.MissionId),
            StepsDone = StepsDoneCount(objective.MissionId)
        };

        // Completion is terminal: a later in-progress objective must not undo it.
        if (contract.Outcome == ContractOutcome.Completed && outcome != ContractOutcome.Completed)
        {
            _contracts[key] = progressed;
            return;
        }

        _contracts[key] = progressed with
        {
            Outcome = outcome,
            CompletedAt = outcome == ContractOutcome.Completed ? objective.Timestamp : contract.CompletedAt
        };

        if (outcome == ContractOutcome.Completed)
            Timeline(objective.Timestamp, "contract-done", "Contract completed", contract.DisplayName);
    }

    private int StepCount(string missionId) =>
        _objectiveSteps.TryGetValue(missionId, out var steps) ? steps.Count : 0;

    private int StepsDoneCount(string missionId) =>
        _objectiveSteps.TryGetValue(missionId, out var steps)
            ? steps.Values.Count(s => s == ObjectiveState.Completed)
            : 0;

    /// <summary>
    /// Attributes an item to the location whose inventory was being browsed.
    /// </summary>
    /// <remarks>
    /// Listings repeat as the player pages through an inventory, so each
    /// (location, item) pair is counted once per session. Removals are never
    /// logged, so this records what was <i>seen</i> at a place, not a live stock
    /// level.
    /// </remarks>
    private void RecordStashItem(InventoryItemEvent item)
    {
        if (!_listings.TryGetValue(item.ScopeKey, out var listing))
            return;

        // Paging through an inventory repeats entries; one row per item is enough.
        if (!listing.Items.Contains(item.ItemClass, StringComparer.OrdinalIgnoreCase))
            listing.Items.Add(item.ItemClass);
    }

    /// <summary>
    /// Turns the final listing of each location into stash entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every entry from one session shares that session's last query time for the
    /// location, which lets the library keep only the newest session per place.
    /// Merging sessions would be wrong: item removals are never logged, so an
    /// all-time union shows things taken away long ago.
    /// </para>
    /// <para>
    /// This is still an approximation. Listings are paged, so it reflects what
    /// was actually browsed on the last visit, not guaranteed full contents.
    /// </para>
    /// </remarks>
    private List<StashEntry> BuildStash()
    {
        var byLocation = new Dictionary<string, (DateTimeOffset At, List<string> Items)>(StringComparer.Ordinal);

        foreach (var (scopeKey, listing) in _listings)
        {
            var locationId = _locationKeys.GetValueOrDefault(scopeKey);
            if (locationId is null || listing.Items.Count == 0)
                continue;

            if (!byLocation.TryGetValue(locationId, out var merged))
                merged = (listing.At, []);

            foreach (var itemClass in listing.Items)
            {
                if (!merged.Items.Contains(itemClass, StringComparer.OrdinalIgnoreCase))
                    merged.Items.Add(itemClass);
            }

            byLocation[locationId] = (listing.At > merged.At ? listing.At : merged.At, merged.Items);
        }

        var entries = new List<StashEntry>();

        foreach (var (locationId, merged) in byLocation)
        {
            var location = LocationResolver.Resolve(locationId);

            foreach (var itemClass in merged.Items)
                entries.Add(new StashEntry(merged.At, locationId, location.DisplayName, itemClass));
        }

        return entries;
    }

    /// <summary>
    /// Records equipped items. These fire on every spawn and inventory refresh,
    /// so only the first sighting of each port/item pair is kept.
    /// </summary>
    private void RecordAttachment(AttachmentEvent attachment)
    {
        var key = $"{attachment.Port}|{attachment.ItemClass}";

        // Keep both ends of the sighting window: the first tells us when the item
        // appeared, the last is what identifies the kit actually in use.
        if (_loadoutSeen.TryGetValue(key, out var existing))
        {
            _loadoutSeen[key] = existing with { LastSeen = attachment.Timestamp };
            return;
        }

        _loadoutSeen[key] = new LoadoutItem(
            attachment.Port, attachment.ItemClass, attachment.Timestamp, attachment.Timestamp);
    }

    /// <summary>
    /// Gap between corpse-item bursts that separates one death from the next.
    /// Observed bursts of 19-40 lines complete in well under a second, while
    /// real deaths in a session were minutes apart.
    /// </summary>
    private static readonly TimeSpan DeathBurstGap = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Counts a death from the burst of corpse-item lines it produces.
    /// </summary>
    /// <remarks>
    /// One line per carried item, so the burst is grouped by time. This is the
    /// only death signal SC 4.9 still emits reliably - see
    /// <see cref="CorpseItemEvent"/>.
    /// </remarks>
    private void RecordCorpse(CorpseItemEvent corpse)
    {
        _corpseItems.Add(corpse.ItemClass);

        if (_lastCorpseAt is { } last && corpse.Timestamp - last < DeathBurstGap)
        {
            _lastCorpseAt = corpse.Timestamp;
            return;
        }

        _lastCorpseAt = corpse.Timestamp;
        _deaths++;
        _lastCasualty = corpse.Timestamp;

        // Where the player wakes is the closest the logs come to naming their
        // respawn point: the game never records the choice, so the next place
        // seen after dying is the answer by observation.
        ArmRespawn(corpse.Timestamp, "death");

        Timeline(corpse.Timestamp, "death", "Died", _locationState.State.Current?.DisplayName);
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

    /// <summary>
    /// Builds the summary. Reads state without changing it, so the live feed can
    /// rebuild on every event.
    /// </summary>
    public SessionSummary Build()
    {
        var started = _firstSeen ?? default;

        // The stretch still open is closed into the returned totals, never
        // banked - see Playtime.
        var (inGame, menu) = Playtime();

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
            InGameDuration = inGame,
            MenuDuration = menu,
            Ships = ships,
            Locations = _locations,
            Jumps = _jumps,
            Contracts = [.. _contracts.Values.OrderBy(c => c.FirstSeen)],
            Timeline = [.. _timeline.OrderBy(t => t.At)],
            Purchases = _purchases,
            Trades = _trades,
            Pickups = _pickups,
            Blueprints = _blueprintReceipts,
            Respawns = _respawns,
            MedicalBeds = _medicalBeds,
            PartyNotes = _partyNotes,
            ChannelNotes = _channelNotes,
            Loadout = [.. _loadoutSeen.Values.OrderBy(l => l.Port, StringComparer.Ordinal)],
            Stash = BuildStash(),
            FleetSize = _fleetSize,
            Incapacitations = _incapacitations,

            // Deaths come from corpse-item bursts, which SC 4.9 still emits.
            // Kills stay zero: no event identifies a killer any more.
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

        // Objective state arrives keyed by mission id, sometimes before the
        // contract itself, so apply anything already seen for this mission.
        var known = _objectiveStates.GetValueOrDefault(contract.MissionId, ObjectiveState.Unknown);

        _contracts[contract.Contract] = new ContractRecord(
            contract.Timestamp,
            parsed.Raw,
            parsed.DisplayName,
            parsed.Issuer,
            parsed.System,
            parsed.Difficulty,
            parsed.Type,
            Accepted: false)
        {
            MissionId = contract.MissionId,
            Outcome = known switch
            {
                ObjectiveState.Completed => ContractOutcome.Completed,
                ObjectiveState.Withdrawn or ObjectiveState.Failed => ContractOutcome.Abandoned,
                ObjectiveState.InProgress => ContractOutcome.InProgress,
                _ => ContractOutcome.Unknown
            },
            Steps = StepCount(contract.MissionId),
            StepsDone = StepsDoneCount(contract.MissionId)
        };

        _contractsByMission[contract.MissionId] = contract.Contract;
    }
}
