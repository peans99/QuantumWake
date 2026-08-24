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
    DateTimeOffset? DoneAt);

/// <summary>
/// A run the player intends to fly, in the order they mean to fly it.
/// </summary>
/// <param name="Tracked">
/// The one plan the Now page and the map are following. Only one at a time:
/// the point of the card is to say where to go next, and two plans have no
/// single next.
/// </param>
public sealed record Trip(
    string Id,
    string Title,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TripStop> Stops,
    bool Tracked = false)
{
    /// <summary>Where to go now, or what remains to do at the stop just reached.</summary>
    public TripStop? Next => Stops.FirstOrDefault(s =>
        !s.Done || (s.Actions ?? []).Any(action => !action.Done));

    public bool Done => Stops.Count > 0 && Stops.All(s =>
        s.Done && (s.Actions ?? []).All(action => action.Done));
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

            var action = new RunAction(NewId(), ActionKind(kind), Sanitise.Clean(text, "Action"),
                CleanQuantity(quantity), CleanUnit(unit), Done: false, DoneAt: null);
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

    private static string ActionKind(string? value) => value?.ToLowerInvariant() switch
    {
        "load" or "unload" or "buy" or "sell" or "collect" or "refuel" or "repair" => value.ToLowerInvariant(),
        _ => "do",
    };

    private static decimal? CleanQuantity(decimal? value) =>
        value is >= 0 and <= 1_000_000 ? value : null;

    private static string? CleanUnit(string? value)
    {
        var unit = Sanitise.CleanOptional(value, 16);
        return unit?.ToUpperInvariant() switch
        {
            "SCU" => "SCU",
            "AUEC" => "aUEC",
            "UNIT" or "UNITS" => "units",
            _ => null,
        };
    }

    /// <summary>Tracks one plan and only that one. Caller holds the lock.</summary>
    private void Follow(string id)
    {
        for (var i = 0; i < _trips.Count; i++)
            _trips[i] = _trips[i] with { Tracked = _trips[i].Id == id };
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
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_trips));
    }
}
