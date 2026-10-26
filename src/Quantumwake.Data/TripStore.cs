using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>One stop on a flight plan.</summary>
/// <param name="PlaceId">
/// The engine id, so the map can draw the stop on the node it already has and
/// the live feed can recognise an arrival without matching on a name.
/// </param>
/// <param name="Note">What the stop is for - "Buy 96 SCU Agricium", "Pick up armour".</param>
public sealed record TripStop(
    string Id,
    string PlaceId,
    string Place,
    string? Note,
    bool Done,
    DateTimeOffset? DoneAt,
    IReadOnlyList<RunAction>? Actions = null);

/// <summary>The contract evidence behind a generated action, retained even after its title changes.</summary>
public sealed record RunActionLink(string? MissionId, IReadOnlyList<string>? LegIds = null)
{
    internal static RunActionLink? Clean(RunActionLink? value)
    {
        if (value is null) return null;

        var missionId = Sanitise.CleanOptional(value.MissionId, 128);
        var legs = (value.LegIds ?? [])
            .Select(id => Sanitise.CleanOptional(id, 64))
            .Where(id => id is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(32)
            .ToList();

        return missionId is null && legs.Count == 0 ? null : new RunActionLink(missionId, legs);
    }
}

/// <summary>One manual instruction at a planned stop.</summary>
/// <remarks>
/// Game.log does not carry a cargo manifest, so action lines deliberately say
/// what the pilot intends or confirms themselves; they are not inferred cargo.
/// </remarks>
/// <param name="Actual">
/// What the action turned out to be worth, when the pilot corrects it. Kept
/// beside <paramref name="Quantity"/> rather than replacing it: the estimate
/// and the outcome are the comparison, and overwriting one destroys the point
/// of recording either.
/// </param>
/// <param name="Price">
/// aUEC per unit of <paramref name="Quantity"/>, on a trade's buy or sell, so
/// the run's profit can be worked out again when either is changed.
/// </param>
/// <param name="Was">
/// The quantity and price as first planned, set the first time the pilot
/// changes either and dropped again if they put them back.
/// </param>
public sealed record RunAction(
    string Id,
    string Kind,
    string Text,
    decimal? Quantity,
    string? Unit,
    bool Done,
    DateTimeOffset? DoneAt,
    decimal? Actual = null,
    RunActionLink? Link = null,
    decimal? Price = null,
    RunActionWas? Was = null)
{
    /// <summary>
    /// The kinds a run sheet may use, with anything else read as a plain "do".
    /// </summary>
    /// <remarks>
    /// Here rather than in each caller because there are two: the page authoring
    /// a stop, and a run sheet arriving in somebody else's file. Sanitise names
    /// this hazard for exactly this shape - two copies of a rule drift, and the
    /// one that drifts is never the one being read carefully. Split, a kind
    /// added to the authoring side would silently arrive as "do" from a file.
    /// </remarks>
    public static string CleanKind(string? value) => value?.ToLowerInvariant() switch
    {
        "load" or "unload" or "buy" or "sell" or "collect" or "refuel" or "repair" => value.ToLowerInvariant(),
        _ => "do",
    };

    /// <summary>The units a quantity may carry, spelled the way the page draws them.</summary>
    public static string? CleanUnit(string? value) => Sanitise.CleanOptional(value, 16)?.ToUpperInvariant() switch
    {
        "SCU" => "SCU",
        "AUEC" => "aUEC",
        "UNIT" or "UNITS" => "units",
        _ => null,
    };

    /// <summary>
    /// A quantity arithmetic can survive; null when there is no sane one.
    /// </summary>
    /// <remarks>
    /// The comparisons reject a non-number as well as an out-of-range one, since
    /// nothing compares true against NaN.
    /// </remarks>
    public static decimal? CleanQuantity(decimal? value) =>
        value is >= 0 and <= 1_000_000 ? value : null;

    /// <summary>
    /// A unit price arithmetic can survive. Wider than a quantity: a ship
    /// component bought from a shop counter runs to millions apiece.
    /// </summary>
    public static decimal? CleanPrice(decimal? value) =>
        value is >= 0 and <= 1_000_000_000 ? value : null;
}

/// <summary>A trade action's figures as first planned, kept once the pilot changes them.</summary>
public sealed record RunActionWas(decimal? Quantity, decimal? Price)
{
    internal static RunActionWas? Clean(RunActionWas? value) =>
        value is null ? null : new RunActionWas(RunAction.CleanQuantity(value.Quantity), RunAction.CleanPrice(value.Price));
}

/// <summary>
/// A run the player intends to fly, in the order they mean to fly it.
/// </summary>
/// <param name="Tracked">
/// The one plan the Now page and the map are following. Only one at a time:
/// the point of the card is to say where to go next, and two plans have no
/// single next.
/// </param>
/// <summary>Why a run is out of the working list.</summary>
public enum Archived
{
    /// <summary>It is not.</summary>
    No,

    /// <summary>The pilot finished it, or filed it themselves.</summary>
    You,

    /// <summary>It went quiet and the sweep filed it. Reversible - see TripStore.Resume.</summary>
    Quiet
}

public sealed record Trip(
    string Id,
    string Title,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TripStop> Stops,
    bool Tracked = false,
    DateTimeOffset? ModifiedAt = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? FinishedAt = null,
    Archived Archived = Archived.No,
    DateTimeOffset? ArchivedAt = null) : IStamped<Trip>
{
    public string StampId => Id;
    /// <remarks>
    /// Tracked comes off as well as the stamp. It is this machine's view state
    /// and never travels in a backup, so a change to it is not a change a
    /// restore could ever see - and counting it would make every trip look
    /// edited the moment a new plan took the tracking from it.
    /// </remarks>
    public Trip Bare() => this with { ModifiedAt = null, Tracked = false };
    public Trip Stamped(DateTimeOffset at) => this with { ModifiedAt = at };

    /// <summary>When this last changed - see <see cref="Job.ChangedAt"/>.</summary>
    public DateTimeOffset ChangedAt => ModifiedAt ?? CreatedAt;

    /// <summary>
    /// A stop that still wants something: not yet reached, or reached with run
    /// work outstanding.
    /// </summary>
    /// <remarks>
    /// Named and shared because three places ask this question - Next below,
    /// the Now briefing, and the page - and the briefing was left behind when
    /// the rule grew its second half. Landing ticks the stop, so anything
    /// selecting on Done alone drops it exactly when its run sheet applies.
    /// </remarks>
    public static bool Outstanding(TripStop stop) =>
        !stop.Done || (stop.Actions ?? []).Any(action => !action.Done);

    /// <summary>Where to go now, or what remains to do at the stop just reached.</summary>
    public TripStop? Next => Stops.FirstOrDefault(Outstanding);

    public bool Done => Stops.Count > 0 && !Stops.Any(Outstanding);

    /// <summary>Begun and not yet ended.</summary>
    public bool Flying => StartedAt is not null && FinishedAt is null && Archived == Archived.No;

    /// <summary>
    /// How long the run took, or has taken so far.
    /// </summary>
    /// <remarks>
    /// Measured from StartedAt, never from CreatedAt. A plan written last week
    /// and flown tonight is a two-hour run, and reporting a week of it is the
    /// whole reason this field exists.
    /// </remarks>
    public TimeSpan? Elapsed(DateTimeOffset now) =>
        StartedAt is { } began ? (FinishedAt ?? now) - began : null;
}

/// <summary>
/// The player's flight plans, kept in a file beside the caches.
/// </summary>
/// <remarks>
/// Authored, not observed - the same reasoning as <see cref="JobStore"/>: a
/// plan is the user's own work, so it lives in its own file, survives a cache
/// wipe, and is never touched by a rescan.
/// </remarks>
public sealed class TripStore
{
    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Marks what actually changed, so no mutator has to remember to.</summary>
    private readonly ChangeStamp<Trip> _stamp = new(r => JsonSerializer.Serialize(r));
    private List<Trip> _trips = [];

    public TripStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "trips.json");
        Load();
    }

    public IReadOnlyList<Trip> All()
    {
        lock (_gate)
            return [.. _trips];
    }

    /// <summary>The plan the Now page and the map are following, if any.</summary>
    public Trip? Tracked()
    {
        lock (_gate)
            return _trips.FirstOrDefault(t => t.Tracked);
    }

    public Trip Add(string? title, IEnumerable<TripStop>? stops = null)
    {
        var trip = new Trip(
            NewId(),
            string.IsNullOrWhiteSpace(title) ? "Flight plan" : title.Trim(),
            DateTimeOffset.UtcNow,
            [.. (stops ?? []).Select(Fresh)]);

        lock (_gate)
        {
            _trips.Insert(0, trip);

            // A new plan is what the player is thinking about, so it takes the
            // tracking from whatever held it. Anything else means adding stops
            // to a plan the map is not showing.
            Follow(trip.Id);
            Save();
        }

        return _trips[0];
    }

    /// <summary>
    /// Adds one stop to the plan the player is filling: the tracked one, else
    /// the newest unfinished one, else a fresh plan. Returns where it landed,
    /// so the page can say so.
    /// </summary>
    public Trip AddStop(TripStop stop)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Tracked);

            // Archived as well as unfinished. Trip.Done is false whenever any
            // stop is outstanding, so a run finished with one stop unticked
            // still looked like the obvious place to put a new stop - and
            // filed runs are drawn without a stop list, so the stop landed
            // somewhere invisible while the call answered success.
            if (index < 0)
                index = _trips.FindIndex(t => t.Archived == Archived.No && !t.Done);

            if (index < 0)
            {
                var created = new Trip(NewId(), "Flight plan", DateTimeOffset.UtcNow, [Fresh(stop)]);
                _trips.Insert(0, created);
                Follow(created.Id);
                Save();
                return _trips[0];
            }

            _trips[index] = _trips[index] with { Stops = [.. _trips[index].Stops, Fresh(stop)] };
            Save();
            return _trips[index];
        }
    }

    /// <summary>
    /// Adds a stop to one named plan. The Itinerary page can have any plan
    /// open, and a stop typed there going to whichever plan is tracked would
    /// land somewhere the pilot is not looking. Null when the plan is unknown.
    /// </summary>
    public Trip? AddStopTo(string tripId, TripStop stop)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == tripId);
            if (index < 0)
                return null;

            _trips[index] = _trips[index] with { Stops = [.. _trips[index].Stops, Fresh(stop)] };
            Save();
            return _trips[index];
        }
    }

    /// <summary>
    /// Puts the lines of a hauling run the pilot ticked into a plan: each at
    /// an unreached stop for its place when the plan has one, else at a new
    /// stop on the end. Null when the plan is unknown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A line the plan already carries for the same contract leg is left out.
    /// Merging the same run twice, or a run into the plan it was made from,
    /// otherwise doubles every line - two sets of the same contracts on one
    /// plan, which is what the pilot was looking at when this was asked for.
    /// </para>
    /// <para>
    /// A pickup goes to the first unreached stop for its place and a drop to
    /// the last, so a place the plan calls at twice keeps the drop on the
    /// later visit, after the cargo is aboard.
    /// </para>
    /// </remarks>
    public MergeResult? Merge(string tripId, IReadOnlyList<HaulPick> picks)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == tripId);
            if (index < 0)
                return null;

            var stops = _trips[index].Stops.ToList();
            int added = 0, already = 0, created = 0;

            foreach (var pick in picks.Where(p => !string.IsNullOrWhiteSpace(p.Text)))
            {
                if (stops.Any(stop => (stop.Actions ?? []).Any(action => TripOrder.SameLine(action, pick))))
                {
                    already++;
                    continue;
                }

                var place = new TripStop(string.Empty, pick.PlaceId ?? string.Empty, pick.Place, null, false, null);
                var matches = stops.Select((stop, i) => (stop, i))
                    .Where(x => !x.stop.Done && TripOrder.SamePlace(x.stop, place))
                    .Select(x => x.i)
                    .ToList();

                var at = matches.Count == 0 ? -1
                    : RunAction.CleanKind(pick.Kind) is "unload" or "sell" ? matches[^1] : matches[0];

                if (at < 0)
                {
                    stops.Add(Fresh(place));
                    at = stops.Count - 1;
                    created++;
                }

                if ((stops[at].Actions ?? []).Count >= MaxActions)
                    continue;

                var action = new RunAction(NewId(), RunAction.CleanKind(pick.Kind), Sanitise.Clean(pick.Text, "Action"),
                    RunAction.CleanQuantity(pick.Quantity), RunAction.CleanUnit(pick.Unit), Done: false, DoneAt: null,
                    Link: RunActionLink.Clean(pick.Link));
                stops[at] = stops[at] with { Actions = [.. (stops[at].Actions ?? []), action] };
                added++;
            }

            if (added > 0)
            {
                _trips[index] = _trips[index] with { Stops = stops };
                Save();
            }

            return new MergeResult(added, already, created);
        }
    }

    /// <summary>
    /// Puts a plan's stops in the given order. Refused unless the ids are
    /// exactly the plan's own, so a reorder computed against a plan that has
    /// since changed cannot drop or duplicate a stop.
    /// </summary>
    public bool Reorder(string tripId, IReadOnlyList<string> stopIds)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == tripId);
            if (index < 0)
                return false;

            var stops = _trips[index].Stops;
            if (stopIds.Count != stops.Count
                || !stopIds.Order(StringComparer.Ordinal).SequenceEqual(stops.Select(s => s.Id).Order(StringComparer.Ordinal), StringComparer.Ordinal))
                return false;

            var byId = stops.ToDictionary(s => s.Id, StringComparer.Ordinal);
            var ordered = stopIds.Select(id => byId[id]).ToList();
            if (ordered.SequenceEqual(stops))
                return true;

            _trips[index] = _trips[index] with { Stops = ordered };
            Save();
            return true;
        }
    }

    public bool ToggleStop(string tripId, string stopId)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == tripId);
            if (index < 0)
                return false;

            var stops = _trips[index].Stops.ToList();
            var at = stops.FindIndex(s => s.Id == stopId);
            if (at < 0)
                return false;

            var done = !stops[at].Done;
            stops[at] = stops[at] with { Done = done, DoneAt = done ? DateTimeOffset.UtcNow : null };

            _trips[index] = _trips[index] with { Stops = stops };
            Save();
            return true;
        }
    }

    /// <summary>Adds a manual load, unload, collection, or service instruction to one stop.</summary>
    public bool AddAction(string tripId, string stopId, string? kind, string? text,
        decimal? quantity, string? unit, RunActionLink? link = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        lock (_gate)
        {
            var tripIndex = _trips.FindIndex(t => t.Id == tripId);
            if (tripIndex < 0) return false;

            var stops = _trips[tripIndex].Stops.ToList();
            var stopIndex = stops.FindIndex(stop => stop.Id == stopId);
            if (stopIndex < 0) return false;

            var action = new RunAction(NewId(), RunAction.CleanKind(kind), Sanitise.Clean(text, "Action"),
                RunAction.CleanQuantity(quantity), RunAction.CleanUnit(unit), Done: false, DoneAt: null,
                Link: RunActionLink.Clean(link));
            stops[stopIndex] = stops[stopIndex] with { Actions = [.. (stops[stopIndex].Actions ?? []), action] };
            _trips[tripIndex] = _trips[tripIndex] with { Stops = stops };
            Save();
            return true;
        }
    }

    public bool ToggleAction(string tripId, string stopId, string actionId)
    {
        lock (_gate)
        {
            var tripIndex = _trips.FindIndex(t => t.Id == tripId);
            if (tripIndex < 0) return false;

            var stops = _trips[tripIndex].Stops.ToList();
            var stopIndex = stops.FindIndex(stop => stop.Id == stopId);
            if (stopIndex < 0) return false;

            var actions = (stops[stopIndex].Actions ?? []).ToList();
            var actionIndex = actions.FindIndex(action => action.Id == actionId);
            if (actionIndex < 0) return false;

            var done = !actions[actionIndex].Done;
            actions[actionIndex] = actions[actionIndex] with { Done = done, DoneAt = done ? DateTimeOffset.UtcNow : null };
            stops[stopIndex] = stops[stopIndex] with { Actions = actions };
            _trips[tripIndex] = _trips[tripIndex] with { Stops = stops };
            Save();
            return true;
        }
    }

    public bool RemoveAction(string tripId, string stopId, string actionId)
    {
        lock (_gate)
        {
            var tripIndex = _trips.FindIndex(t => t.Id == tripId);
            if (tripIndex < 0) return false;

            var stops = _trips[tripIndex].Stops.ToList();
            var stopIndex = stops.FindIndex(stop => stop.Id == stopId);
            if (stopIndex < 0) return false;

            var actions = (stops[stopIndex].Actions ?? []).Where(action => action.Id != actionId).ToList();
            if (actions.Count == (stops[stopIndex].Actions ?? []).Count) return false;

            stops[stopIndex] = stops[stopIndex] with { Actions = actions };
            _trips[tripIndex] = _trips[tripIndex] with { Stops = stops };
            Save();
            return true;
        }
    }

    /// <summary>Moves a stop up or down the running order.</summary>
    public bool MoveStop(string tripId, string stopId, int delta)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == tripId);
            if (index < 0)
                return false;

            var stops = _trips[index].Stops.ToList();
            var at = stops.FindIndex(s => s.Id == stopId);
            var to = at + delta;

            if (at < 0 || to < 0 || to >= stops.Count)
                return false;

            (stops[at], stops[to]) = (stops[to], stops[at]);
            _trips[index] = _trips[index] with { Stops = stops };
            Save();
            return true;
        }
    }

    /// <summary>
    /// Puts a stop at a position in the running order, counted after it has
    /// been lifted out - where a drag lets go, which is one call however far it
    /// travelled. A position past either end is the end.
    /// </summary>
    public bool PlaceStop(string tripId, string stopId, int to)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == tripId);
            if (index < 0)
                return false;

            var stops = _trips[index].Stops.ToList();
            var at = stops.FindIndex(s => s.Id == stopId);
            if (at < 0)
                return false;

            var stop = stops[at];
            stops.RemoveAt(at);
            stops.Insert(Math.Clamp(to, 0, stops.Count), stop);

            _trips[index] = _trips[index] with { Stops = stops };
            Save();
            return true;
        }
    }

    /// <summary>
    /// Carries one run-sheet line to another stop of the same plan, at a
    /// position in that stop's list. A drop collected at the wrong stop is one
    /// line, and deleting it to type it again loses its price, its planned
    /// figures and the contract it came from.
    /// </summary>
    public bool MoveAction(string tripId, string stopId, string actionId, string toStopId, int to)
    {
        lock (_gate)
        {
            var tripIndex = _trips.FindIndex(t => t.Id == tripId);
            if (tripIndex < 0) return false;

            var stops = _trips[tripIndex].Stops.ToList();
            var from = stops.FindIndex(stop => stop.Id == stopId);
            var into = stops.FindIndex(stop => stop.Id == toStopId);
            if (from < 0 || into < 0) return false;

            var source = (stops[from].Actions ?? []).ToList();
            var at = source.FindIndex(action => action.Id == actionId);
            if (at < 0) return false;

            var action = source[at];
            source.RemoveAt(at);
            stops[from] = stops[from] with { Actions = source };

            // Read after the removal, so a move within one stop sees the list
            // with the line already lifted out, as PlaceStop does.
            var target = (stops[into].Actions ?? []).ToList();
            target.Insert(Math.Clamp(to, 0, target.Count), action);
            stops[into] = stops[into] with { Actions = target };

            _trips[tripIndex] = _trips[tripIndex] with { Stops = stops };
            Save();
            return true;
        }
    }

    public bool RemoveStop(string tripId, string stopId)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == tripId);
            if (index < 0)
                return false;

            var stops = _trips[index].Stops.Where(s => s.Id != stopId).ToList();
            if (stops.Count == _trips[index].Stops.Count)
                return false;

            _trips[index] = _trips[index] with { Stops = stops };
            Save();
            return true;
        }
    }

    /// <summary>Follows a plan, or stops following it. False when the id is unknown.</summary>
    public bool Track(string id)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == id);
            if (index < 0)
                return false;

            if (_trips[index].Tracked)
                _trips[index] = _trips[index] with { Tracked = false };
            else
                Follow(id);

            Save();
            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            var removed = _trips.RemoveAll(t => t.Id == id) > 0;
            if (removed)
                Save();

            return removed;
        }
    }

    /// <summary>
    /// Crosses off the stop the player has just arrived at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The app already knows where the player is, so making them tick a box for
    /// somewhere they are standing is asking for data it has. Only the tracked
    /// plan is touched, and only its next unfinished stop for that place: a plan
    /// that visits Lorville twice means two separate stops, and arriving once
    /// should not tick both.
    /// </para>
    /// <para>
    /// A stop can still be ticked and unticked by hand. Unticking one the player
    /// is standing at would be undone on the next arrival, but not before -
    /// arrivals fire on entering a place, not continuously.
    /// </para>
    /// <para>
    /// The id is the reliable half of this and the name is the fallback: a stop
    /// added from a UEX terminal carries no engine id when the two naming
    /// schemes could not be reconciled, and those stops would otherwise be the
    /// only ones that never cross themselves off - which is precisely backwards,
    /// since they are the ones the map cannot draw either. A name is only
    /// consulted for a stop with no id, so an id never loses to one.
    /// </para>
    /// <para>
    /// A stop whose every line belongs to a contract that is no longer open is
    /// left alone. A hauling plan written on 23 Sep was still tracked a month
    /// later, and landing at Gaslight on 24 Oct for a new Carbon haul ticked
    /// the old plan's Gaslight stop - work for contracts the game had dropped
    /// weeks before, crossed off as though it had been done.
    /// </para>
    /// </remarks>
    /// <param name="missionOpen">
    /// Whether a mission is open in the session being played. Null when that
    /// is not known, and then every stop ticks as it always did.
    /// </param>
    public bool Arrived(string? placeId, string? placeName = null, Func<string, bool>? missionOpen = null)
    {
        if (string.IsNullOrWhiteSpace(placeId) && string.IsNullOrWhiteSpace(placeName))
            return false;

        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Tracked);
            if (index < 0)
                return false;

            var stops = _trips[index].Stops.ToList();

            bool Live(TripStop stop) => missionOpen is null || !OnlyEnded(stop, missionOpen);

            var at = stops.FindIndex(s => !s.Done && Same(s.PlaceId, placeId) && Live(s));

            if (at < 0)
                at = stops.FindIndex(s =>
                    !s.Done && string.IsNullOrWhiteSpace(s.PlaceId) && Same(s.Place, placeName) && Live(s));

            if (at < 0)
                return false;

            stops[at] = stops[at] with { Done = true, DoneAt = DateTimeOffset.UtcNow };
            _trips[index] = _trips[index] with { Stops = stops };
            Save();
            return true;
        }
    }

    /// <summary>
    /// The contracts a plan still has unticked lines for that are not open now.
    /// </summary>
    /// <remarks>
    /// Only lines that carry a mission id can be asked about; a line typed by
    /// hand belongs to no contract and is never called ended. Ticked lines do
    /// not count: a contract handed in is not open either, and its finished
    /// drop is not stale.
    /// </remarks>
    public static IReadOnlyList<string> EndedMissions(Trip trip, Func<string, bool> missionOpen) =>
        [.. trip.Stops
            .SelectMany(stop => stop.Actions ?? [])
            .Where(action => !action.Done)
            .Select(action => action.Link?.MissionId)
            .Where(id => id is { Length: > 0 } && !missionOpen(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Whether every line at a stop is for a contract no longer open. A stop with no lines is not.</summary>
    private static bool OnlyEnded(TripStop stop, Func<string, bool> missionOpen)
    {
        var actions = stop.Actions ?? [];
        return actions.Count > 0
            && actions.All(action => action.Link?.MissionId is { Length: > 0 } id && !missionOpen(id));
    }

    /// <summary>
    /// Takes the unticked lines for ended contracts off a plan, and the stops
    /// that held nothing else. Returns how many lines went, or null when the
    /// plan is unknown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ticked line stays: a contract handed in is no longer open either, and
    /// its ticked drop is the record of the work, not something left undone.
    /// A stop that never had lines is the pilot's own and is left alone.
    /// </para>
    /// <para>
    /// A stop emptied of everything goes even when it was crossed off. The
    /// Gaslight stop of the 23 Sep plan was ticked by a landing for another
    /// contract, so it recorded nothing; kept, it sat in front of the new
    /// Gaslight stop the merged run added and read as a visit already made.
    /// </para>
    /// </remarks>
    public int? RemoveEnded(string tripId, Func<string, bool> missionOpen)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == tripId);
            if (index < 0)
                return null;

            var removed = 0;
            var stops = new List<TripStop>();

            foreach (var stop in _trips[index].Stops)
            {
                var actions = stop.Actions ?? [];
                var kept = actions
                    .Where(action => action.Done || action.Link?.MissionId is not { Length: > 0 } id || missionOpen(id))
                    .ToList();
                removed += actions.Count - kept.Count;

                if (actions.Count > 0 && kept.Count == 0)
                    continue;

                stops.Add(kept.Count == actions.Count ? stop : stop with { Actions = kept });
            }

            if (removed == 0)
                return 0;

            _trips[index] = _trips[index] with { Stops = stops };
            Save();
            return removed;
        }
    }

    /// <summary>Two names for one place, ignoring case, spacing and punctuation.</summary>
    /// <remarks>
    /// "Port Tressler" against "Port-Tressler" is the same landing, and a stop
    /// that fails to cross itself off over a hyphen is worse than no automation.
    /// </remarks>
    private static bool Same(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        return string.Equals(Compact(left), Compact(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Compact(string value) =>
        new([.. value.Where(char.IsLetterOrDigit)]);

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <remarks>
    /// A plan written whole - a rep run's pickups and drops, a trade's buy and
    /// sell - arrives with its run sheet, so the actions come along under the
    /// same rules <see cref="AddAction"/> applies one at a time. Never ticked:
    /// a new plan has not been flown.
    /// </remarks>
    private static TripStop Fresh(TripStop stop) => new(
        NewId(),
        stop.PlaceId ?? string.Empty,
        string.IsNullOrWhiteSpace(stop.Place) ? "Unknown place" : stop.Place.Trim(),
        string.IsNullOrWhiteSpace(stop.Note) ? null : stop.Note.Trim(),
        Done: false,
        DoneAt: null,
        Actions: [.. (stop.Actions ?? [])
            .Where(action => action is not null && !string.IsNullOrWhiteSpace(action.Text))
            .Take(MaxActions)
            .Select(action => new RunAction(NewId(), RunAction.CleanKind(action.Kind),
                Sanitise.Clean(action.Text, "Action"), RunAction.CleanQuantity(action.Quantity),
                RunAction.CleanUnit(action.Unit), Done: false, DoneAt: null,
                Link: RunActionLink.Clean(action.Link), Price: RunAction.CleanPrice(action.Price)))]);

    /// <summary>The same ceiling a run sheet arriving in a file is held to.</summary>
    private const int MaxActions = ImportReader.MaxRunActions;

    /// <summary>Tracks one plan and only that one. Caller holds the lock.</summary>
    private void Follow(string id)
    {
        for (var i = 0; i < _trips.Count; i++)
            _trips[i] = _trips[i] with { Tracked = _trips[i].Id == id };
    }

    /// <summary>
    /// Marks a run as begun, now.
    /// </summary>
    /// <remarks>
    /// Explicit rather than inferred from the first ticked stop, because only
    /// the pilot knows when they set off - and a plan can be edited for a week
    /// before it is flown. Starting a run that is already going leaves its
    /// original start alone.
    /// </remarks>
    public bool Start(string id, DateTimeOffset now)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == id);
            if (index < 0 || _trips[index].StartedAt is not null) return false;

            _trips[index] = _trips[index] with { StartedAt = now, FinishedAt = null };
            Save();
            return true;
        }
    }

    /// <summary>
    /// Ends a run and files it, so the working list holds only what is live.
    /// </summary>
    public bool Finish(string id, DateTimeOffset now)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == id);
            if (index < 0 || _trips[index].FinishedAt is not null) return false;

            _trips[index] = _trips[index] with
            {
                // A run finished without ever being started still took some
                // time, and pretending otherwise would leave the review with
                // nothing to measure. The best guess available is when the plan
                // was written, and it is marked as a guess by being equal.
                StartedAt = _trips[index].StartedAt ?? _trips[index].CreatedAt,
                FinishedAt = now,
                Archived = Archived.You,
                ArchivedAt = now,
                Tracked = false,
            };

            Save();
            return true;
        }
    }

    /// <summary>
    /// Puts a filed run back in the working list, keeping when it began.
    /// </summary>
    /// <remarks>
    /// The half that makes the quiet sweep safe. Without this, taking a
    /// fortnight off turns a real run into a lost one; with it, the sweep only
    /// ever costs somebody a click. StartedAt survives on purpose - the run did
    /// begin when it began, and rewriting that to now would report a two-week
    /// flight as a two-minute one.
    /// </remarks>
    public bool Resume(string id)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Id == id);
            if (index < 0 || _trips[index].Archived == Archived.No) return false;

            _trips[index] = _trips[index] with
            {
                Archived = Archived.No,
                ArchivedAt = null,
                FinishedAt = null,
            };

            // Saving stamps ModifiedAt, which is the clock the sweep reads - so
            // resuming resets it and the run is not filed again tomorrow.
            Save();
            return true;
        }
    }

    /// <summary>
    /// Copies a run into a fresh one, ready to fly again.
    /// </summary>
    /// <remarks>
    /// Everything done is cleared: a repeat is the same route, not the same
    /// history. The copy takes the tracking, because somebody who asked to
    /// repeat a run is about to fly it.
    /// </remarks>
    public Trip? Repeat(string id, DateTimeOffset now)
    {
        lock (_gate)
        {
            var source = _trips.FirstOrDefault(t => t.Id == id);
            if (source is null) return null;

            var copy = new Trip(
                NewId(),
                source.Title,
                now,
                [.. source.Stops.Select(stop => stop with
                {
                    Id = NewId(),
                    Done = false,
                    DoneAt = null,
                    Actions = [.. (stop.Actions ?? []).Select(action => action with
                    {
                        Id = NewId(),
                        Done = false,
                        DoneAt = null,

                        // The estimate is the plan and comes along; the
                        // correction was the last run's outcome, and carrying
                        // it would present that as this run's result before it
                        // has been flown.
                        Actual = null,

                        // A tweak was the plan meeting the last run; what it
                        // settled on is this run's plan, with nothing to
                        // compare it against yet.
                        Was = null,
                    })],
                })]);

            _trips.Insert(0, copy);
            Follow(copy.Id);
            Save();

            return _trips[0];
        }
    }

    /// <summary>
    /// Files runs that have gone quiet, and says how many it filed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only runs that were actually begun. An unstarted plan is a backlog item
    /// rather than an abandoned flight, and filing those would empty the list
    /// somebody keeps their intentions in.
    /// </para>
    /// <para>
    /// The clock is <see cref="Trip.ChangedAt"/> rather than a field of its own.
    /// That is already "when the content last changed", which is what working on
    /// a run does - ticking a stop, adding an action, renaming it - and a second
    /// date would be one more thing that can disagree with the first.
    /// </para>
    /// </remarks>
    public int SweepQuiet(TimeSpan idle, DateTimeOffset now)
    {
        if (idle <= TimeSpan.Zero) return 0;

        lock (_gate)
        {
            var filed = 0;

            for (var i = 0; i < _trips.Count; i++)
            {
                if (!_trips[i].Flying || now - _trips[i].ChangedAt < idle) continue;

                _trips[i] = _trips[i] with
                {
                    Archived = Archived.Quiet,
                    ArchivedAt = now,
                    Tracked = false,
                };

                filed++;
            }

            if (filed > 0) Save();

            return filed;
        }
    }

    /// <summary>
    /// Records what an action actually came to, leaving the estimate alone.
    /// </summary>
    /// <remarks>
    /// Null clears a correction rather than setting it to zero - a stop that
    /// turned out to be worth nothing and a stop nobody has checked yet are
    /// different facts, and only one of them is a number.
    /// </remarks>
    public bool Correct(string tripId, string stopId, string actionId, decimal? actual)
    {
        lock (_gate)
        {
            var tripIndex = _trips.FindIndex(t => t.Id == tripId);
            if (tripIndex < 0) return false;

            var stops = _trips[tripIndex].Stops.ToList();
            var stopIndex = stops.FindIndex(s => s.Id == stopId);
            if (stopIndex < 0) return false;

            var actions = (stops[stopIndex].Actions ?? []).ToList();
            var actionIndex = actions.FindIndex(a => a.Id == actionId);
            if (actionIndex < 0) return false;

            actions[actionIndex] = actions[actionIndex] with
            {
                Actual = actual is null ? null : RunAction.CleanQuantity(actual),
            };

            stops[stopIndex] = stops[stopIndex] with { Actions = actions };
            _trips[tripIndex] = _trips[tripIndex] with { Stops = stops };

            Save();
            return true;
        }
    }

    /// <summary>
    /// Changes what a planned action is for - the SCU of a buy, the price of a
    /// sale - keeping what it was first planned at beside it.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Correct"/>, which records an outcome after the fact,
    /// this changes the plan: the kiosk had less stock, the buyer moved its
    /// price, the hold was smaller than the plan assumed. The first figures
    /// are kept once, on the first change, so a run tweaked three times still
    /// compares against what was planned rather than the second guess; putting
    /// them back drops the comparison, because there is nothing left to compare.
    /// </remarks>
    public bool Tweak(string tripId, string stopId, string actionId, decimal? quantity, decimal? price)
    {
        lock (_gate)
        {
            var tripIndex = _trips.FindIndex(t => t.Id == tripId);
            if (tripIndex < 0) return false;

            var stops = _trips[tripIndex].Stops.ToList();
            var stopIndex = stops.FindIndex(s => s.Id == stopId);
            if (stopIndex < 0) return false;

            var actions = (stops[stopIndex].Actions ?? []).ToList();
            var actionIndex = actions.FindIndex(a => a.Id == actionId);
            if (actionIndex < 0) return false;

            var before = actions[actionIndex];
            var was = before.Was ?? new RunActionWas(before.Quantity, before.Price);
            var now = new RunActionWas(RunAction.CleanQuantity(quantity), RunAction.CleanPrice(price));

            actions[actionIndex] = before with
            {
                Quantity = now.Quantity,
                Price = now.Price,
                Was = now == was ? null : was,
            };

            stops[stopIndex] = stops[stopIndex] with { Actions = actions };
            _trips[tripIndex] = _trips[tripIndex] with { Stops = stops };

            Save();
            return true;
        }
    }

    /// <summary>
    /// Puts a record back exactly as given, replacing any with the same id.
    /// </summary>
    /// <remarks>
    /// For restoring a backup, and nothing else. Every other way in makes its
    /// own record so the store owns the id and the dates; this one deliberately
    /// does not, because a restore has to reproduce what was backed up rather
    /// than author something new that resembles it.
    /// </remarks>
    public void Put(Trip trip)
    {
        lock (_gate)
        {
            var index = _trips.FindIndex(x => x.Id == trip.Id);

            // View state is this machine's and the preview promises to leave it
            // alone, so a replacement keeps the pin or the tracking it lands on.
            // The file never carried them - a backup strips both on the way out -
            // so taking the record verbatim silently unpins whatever it replaced.
            if (index >= 0) _trips[index] = trip with { Tracked = _trips[index].Tracked };
            else _trips.Add(trip);

            // The record keeps the change time it was backed up with.
            _stamp.Adopt(trip);
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _trips = JsonSerializer.Deserialize<List<Trip>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // A corrupt file must not stop the app; the user starts with none.
            _trips = [];
        }

        _stamp.Loaded(_trips);
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _stamp.Apply(_trips, DateTimeOffset.UtcNow);
            File.WriteAllText(_path, JsonSerializer.Serialize(_trips));
    }
}
