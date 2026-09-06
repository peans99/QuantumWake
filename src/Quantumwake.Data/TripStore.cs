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

/// <summary>One manual instruction at a planned stop.</summary>
/// <remarks>
/// Game.log does not carry a cargo manifest, so action lines deliberately say
/// what the pilot intends or confirms themselves; they are not inferred cargo.
/// </remarks>
public sealed record RunAction(
    string Id,
    string Kind,
    string Text,
    decimal? Quantity,
    string? Unit,
    bool Done,
    DateTimeOffset? DoneAt)
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

            if (index < 0)
                index = _trips.FindIndex(t => !t.Done);

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
        decimal? quantity, string? unit)
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
                RunAction.CleanQuantity(quantity), RunAction.CleanUnit(unit), Done: false, DoneAt: null);
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
    /// </remarks>
    public bool Arrived(string? placeId, string? placeName = null)
    {
        if (string.IsNullOrWhiteSpace(placeId) && string.IsNullOrWhiteSpace(placeName))
            return false;

        lock (_gate)
        {
            var index = _trips.FindIndex(t => t.Tracked);
            if (index < 0)
                return false;

            var stops = _trips[index].Stops.ToList();

            var at = stops.FindIndex(s => !s.Done && Same(s.PlaceId, placeId));

            if (at < 0)
                at = stops.FindIndex(s =>
                    !s.Done && string.IsNullOrWhiteSpace(s.PlaceId) && Same(s.Place, placeName));

            if (at < 0)
                return false;

            stops[at] = stops[at] with { Done = true, DoneAt = DateTimeOffset.UtcNow };
            _trips[index] = _trips[index] with { Stops = stops };
            Save();
            return true;
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

    private static TripStop Fresh(TripStop stop) => new(
        NewId(),
        stop.PlaceId ?? string.Empty,
        string.IsNullOrWhiteSpace(stop.Place) ? "Unknown place" : stop.Place.Trim(),
        string.IsNullOrWhiteSpace(stop.Note) ? null : stop.Note.Trim(),
        Done: false,
        DoneAt: null,
        Actions: []);

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
