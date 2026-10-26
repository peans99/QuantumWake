namespace Quantumwake.Data;

/// <summary>One line of a hauling run the pilot ticked, on its way into a plan.</summary>
/// <param name="PlaceId">The map's id for the stop, empty when the atlas has none.</param>
public sealed record HaulPick(
    string PlaceId,
    string Place,
    string Kind,
    string Text,
    decimal? Quantity,
    string? Unit,
    RunActionLink? Link);

/// <summary>What merging a run into a plan did.</summary>
/// <param name="AlreadyThere">Lines the plan already carried for the same contract leg, and so left out.</param>
public sealed record MergeResult(int Added, int AlreadyThere, int NewStops);

/// <summary>Where a stop is, as far as ordering a run cares: its body and its system.</summary>
public sealed record StopWhere(string? Body, string? System);

/// <summary>
/// A plan's unfinished stops in an order that can be flown.
/// </summary>
/// <remarks>
/// <para>
/// The same rules the hauling run uses, applied to a plan the pilot has put
/// together and pulled about by hand. Each contract's pickups come before its
/// drop. After that, stops on the same body stay together, then stops in the
/// same system, then stops with something to load, then the order they were
/// already in. Nothing here knows a distance - the atlas has no coordinates
/// for a station - so it never claims a shortest route, only one that holds
/// together.
/// </para>
/// <para>
/// A stop already flown, with nothing left to do at it, stays where it is at
/// the front: it is the record of where the run went. Lines typed by hand
/// carry no contract and so ask for nothing, but a trade run's buys and sells
/// keep their order among themselves - a sale moved ahead of its purchase is
/// a sale of cargo not yet aboard.
/// </para>
/// </remarks>
public static class TripOrder
{
    private static readonly HashSet<string> Sources = new(StringComparer.OrdinalIgnoreCase) { "load", "buy", "collect" };
    private static readonly HashSet<string> Sinks = new(StringComparer.OrdinalIgnoreCase) { "unload", "sell" };

    /// <summary>The stop ids in the new order. The same ids, every one of them, so nothing can be lost by applying it.</summary>
    public static IReadOnlyList<string> Optimise(Trip trip, Func<TripStop, StopWhere> where, TripStop? start = null, StopWhere? startWhere = null)
    {
        var flown = trip.Stops.Where(s => !Trip.Outstanding(s)).ToList();
        var open = trip.Stops.Where(Trip.Outstanding).ToList();
        var index = open.Select((stop, i) => (stop, i)).ToDictionary(x => x.stop.Id, x => x.i, StringComparer.Ordinal);

        // What has to come before what: every stop loading for a contract
        // before every stop unloading for it, and the trade lines in the order
        // they were written.
        var before = open.ToDictionary(s => s.Id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        foreach (var mission in open
            .SelectMany(s => Pending(s).Select(a => a.Link?.MissionId))
            .Where(id => id is { Length: > 0 })
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var loads = open.Where(s => Pending(s).Any(a => Is(a, mission!) && Sources.Contains(a.Kind))).ToList();
            var unloads = open.Where(s => Pending(s).Any(a => Is(a, mission!) && Sinks.Contains(a.Kind))).ToList();

            foreach (var drop in unloads)
                foreach (var pick in loads.Where(p => p.Id != drop.Id))
                    before[drop.Id].Add(pick.Id);
        }

        var trade = open.Where(s => Pending(s).Any(a => a.Link?.MissionId is not { Length: > 0 }
            && (a.Kind.Equals("buy", StringComparison.OrdinalIgnoreCase) || a.Kind.Equals("sell", StringComparison.OrdinalIgnoreCase)))).ToList();
        for (var i = 1; i < trade.Count; i++)
            before[trade[i].Id].Add(trade[i - 1].Id);

        var placed = new List<TripStop>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        var last = startWhere;
        var pending = open.ToList();

        while (pending.Count > 0)
        {
            var ready = pending.Where(s => before[s.Id].All(done.Contains)).ToList();

            // Two contracts each picking up where the other drops off can
            // leave nothing ready. Then the plan's own order decides, which
            // is the order the pilot last chose.
            if (ready.Count == 0)
                ready = [pending.OrderBy(s => index[s.Id]).First()];

            var next = placed.Count == 0 && start is not null
                ? ready.FirstOrDefault(s => SamePlace(s, start))
                : null;

            next ??= ready
                .OrderByDescending(s => last?.Body is { } body && Same(where(s).Body, body))
                .ThenByDescending(s => last?.System is { } system && Same(where(s).System, system))
                .ThenByDescending(s => Pending(s).Any(a => Sources.Contains(a.Kind)))
                .ThenBy(s => index[s.Id])
                .First();

            placed.Add(next);
            done.Add(next.Id);
            pending.Remove(next);
            last = where(next);
        }

        return [.. flown.Concat(placed).Select(s => s.Id)];
    }

    private static IEnumerable<RunAction> Pending(TripStop stop) => (stop.Actions ?? []).Where(a => !a.Done);

    private static bool Is(RunAction action, string mission) =>
        string.Equals(action.Link?.MissionId, mission, StringComparison.OrdinalIgnoreCase);

    private static bool Same(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>The same place: by id when both have one, by name when either does not.</summary>
    internal static bool SamePlace(TripStop a, TripStop b) =>
        a.PlaceId.Length > 0 && b.PlaceId.Length > 0
            ? string.Equals(a.PlaceId, b.PlaceId, StringComparison.OrdinalIgnoreCase)
            : string.Equals(Compact(a.Place), Compact(b.Place), StringComparison.OrdinalIgnoreCase);

    private static string Compact(string value) => new([.. value.Where(char.IsLetterOrDigit)]);

    /// <summary>
    /// Whether two lines are for the same contract leg: the same kind, the same
    /// mission, the same legs. Only lines that carry a mission can be the same
    /// line - two hand-written "Refuel" lines are two refuels.
    /// </summary>
    public static bool SameLine(RunAction action, HaulPick pick) =>
        pick.Link?.MissionId is { Length: > 0 } mission
        && Is(action, mission)
        && string.Equals(action.Kind, pick.Kind, StringComparison.OrdinalIgnoreCase)
        && (action.Link?.LegIds ?? []).Order(StringComparer.Ordinal)
            .SequenceEqual((pick.Link.LegIds ?? []).Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
