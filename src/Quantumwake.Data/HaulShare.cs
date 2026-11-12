namespace Quantumwake.Data;

/// <summary>One ship on a shared run.</summary>
/// <param name="Id">The crew entry's id, or <c>me</c> for the pilot's own ship.</param>
/// <param name="Name">Who flies it, as the page names them.</param>
/// <param name="Hull">The hull's name, when known.</param>
/// <param name="Scu">The hold, from the reference data; null when the hull is unknown, and then the hold is not limited.</param>
/// <param name="Start">Where the ship is now; null when nobody said, and then its first leg is not counted.</param>
public sealed record ShareShip(string Id, string Name, string? Hull, double? Scu, ResolvedPlace? Start, bool Me = false);

/// <summary>One stop on one ship's run.</summary>
/// <param name="LegGm">Straight-line distance from the previous stop (or the ship's start), body centre to body centre; null when either end has no position on file.</param>
/// <param name="AboardScu">Known cargo aboard after the stop - a floor when a leg printed no count.</param>
public sealed record ShareStop(
    string Place,
    string PlaceId,
    string? Body,
    string? System,
    double? LegGm,
    IReadOnlyList<HaulAction> Actions,
    int AboardScu);

/// <summary>One ship's share of the run, in the order it flies it.</summary>
/// <param name="Gm">The run's length, counting only the legs with both ends on file.</param>
/// <param name="GmPartial">Whether a leg had no position on file, so <see cref="Gm"/> is short of the truth.</param>
public sealed record ShareRun(ShareShip Ship, IReadOnlyList<ShareStop> Stops, double Gm, bool GmPartial, int PeakScu, int Contracts);

/// <summary>A contract that could not be put on any ship, and why.</summary>
public sealed record ShareLeftOut(string ContractTitle, string Why);

/// <summary>The work shared out between the ships.</summary>
/// <param name="LongestGm">The longest single run: what "finish soonest" makes as short as it can.</param>
public sealed record SharePlan(
    IReadOnlyList<ShareRun> Runs,
    double LongestGm,
    double TotalGm,
    bool Split,
    IReadOnlyList<ShareLeftOut> LeftOut,
    IReadOnlyList<string> Notes);

/// <summary>
/// Shares the open hauling contracts between the ships of a group, so the
/// whole run is done soonest (1.2.32).
/// </summary>
/// <remarks>
/// <para>
/// "Soonest" is the longest single ship's run, and that is what is made
/// small: a group waits on its slowest member. Distance is the only cost
/// there is to go on - straight lines between body centres from the
/// community star map, the same measure the MFD map draws. Two stops on one
/// body count as no distance, which is the honest reading of a measure that
/// cannot see a station's orbit, and a stop with no position on file is
/// counted as <see cref="UnknownGm"/> so it is neither free nor skipped. A
/// jump to another system is <see cref="CrossSystemGm"/>: there are no
/// coordinates across a jump point, only the knowledge that it is far.
/// </para>
/// <para>
/// Each contract stays with one ship unless splitting is allowed, because a
/// contract that is not shared can only be worked by whoever took it. With
/// splitting each leg can go to its own ship. Either way a leg bigger than
/// the hold that carries it is cut into hold-sized loads, each a trip of its
/// own, and no ship is ever planned to carry more than its hold. A contract
/// with a pickup already made is someone's cargo already: it stays with the
/// pilot's own ship, which is the only one the log can see loading.
/// </para>
/// <para>
/// The method is cheapest insertion - each contract, longest first, goes
/// where it adds least to the longest run - then contracts move off the
/// longest run while that makes it shorter. It is not an optimum and says
/// so; it is a fair start a group can argue with.
/// </para>
/// </remarks>
public static class HaulShare
{
    /// <summary>What a hop with no position on file is counted as, so it is neither free nor ignored: about one Stanton planet to the next.</summary>
    public const double UnknownGm = 25;

    /// <summary>What a hop to another system is counted as. No coordinates span a jump point; this only has to outweigh any hop inside one.</summary>
    public const double CrossSystemGm = 150;

    private const int MaxImprovements = 50;

    public static SharePlan Share(
        IReadOnlyList<PlannedContract> contracts,
        IReadOnlyList<ShareShip> ships,
        bool split,
        Func<string, ResolvedPlace?> resolve,
        Func<string, IReadOnlyDictionary<string, BodyPosition>> positions)
    {
        var notes = new List<string>();
        var leftOut = new List<ShareLeftOut>();
        if (ships.Count == 0)
            return new SharePlan([], 0, 0, split, [], ["No ship on the run."]);

        var hop = new Hops(positions);
        var tasks = new List<Job>();

        for (var c = 0; c < contracts.Count; c++)
        {
            var contract = contracts[c];
            var title = contract.Title;
            var started = contract.PickupsDone > 0;
            var allAboard = contract.Pickups > 0 && contract.PickupsDone >= contract.Pickups;
            var jobs = new List<Job>();
            var missing = false;

            foreach (var leg in contract.Legs)
            {
                var scu = leg.Scu is { } total ? Math.Max(0, total - (leg.ScuDone ?? 0)) : (int?)null;
                if (scu == 0) continue;

                var to = Spot(leg.Delivery, leg.DeliveryBody, resolve);
                var from = allAboard ? null : Spot(leg.Pickup, leg.PickupBody, resolve);
                if (to is null || (from is null && !allAboard)) { missing = true; continue; }

                jobs.Add(new Job(c, title, contract.MissionId, leg.Commodity ?? contract.Commodity,
                    from, to, scu ?? 0, scu is not null, leg.Id is { Length: > 0 } id ? [id] : [], started));
            }

            if (jobs.Count == 0)
            {
                leftOut.Add(new ShareLeftOut(title, missing
                    ? "Where it picks up or delivers is not known yet - photograph its card in the Contracts app."
                    : "Nothing left to move on it."));
                continue;
            }
            if (missing)
                notes.Add($"{title}: a leg whose ends are not known yet is left off - photograph its card in the Contracts app.");
            tasks.AddRange(jobs);
        }

        var me = ships.FirstOrDefault(s => s.Me) ?? ships[0];
        var runs = ships.Select(s => new Route(s, hop)).ToList();
        var mine = runs.First(r => r.Ship == me);

        // Groups: the unit that goes to one ship. A whole contract unless
        // splitting is allowed; a started contract is always whole and mine.
        var groups = new List<Group>();
        foreach (var byContract in tasks.GroupBy(t => t.Contract))
        {
            var list = byContract.ToList();
            var locked = list.Any(t => t.Started);
            if (split && !locked)
                groups.AddRange(list.Select(t => new Group([t], false)));
            else
                groups.Add(new Group(list, locked));
        }

        // Longest first: the far jobs decide the shape of the runs, and the
        // short ones fill in around them.
        foreach (var group in groups
            .OrderByDescending(g => g.Locked)
            .ThenByDescending(g => g.Jobs.Sum(j => j.From is null ? 0 : hop.Cost(j.From, j.To)))
            .ThenByDescending(g => g.Jobs.Sum(j => j.Scu)))
        {
            List<Route> candidates = group.Locked ? [mine] : runs;
            Route? best = null;
            List<Visit>? bestVisits = null;
            (double Longest, double Added) bestScore = (double.MaxValue, double.MaxValue);

            foreach (var run in candidates)
            {
                if (run.With(group) is not { } visits) continue;
                var length = run.Length(visits);
                var longest = Math.Max(length, runs.Where(r => r != run).Select(r => r.Length()).DefaultIfEmpty(0).Max());
                var score = (longest, length - run.Length());
                if (score.longest < bestScore.Longest - 1e-9
                    || (Math.Abs(score.longest - bestScore.Longest) < 1e-9 && score.Item2 < bestScore.Added))
                {
                    best = run; bestVisits = visits; bestScore = score;
                }
            }

            if (best is null)
            {
                leftOut.Add(new ShareLeftOut(group.Jobs[0].Title, "No ship on the run can carry it."));
                continue;
            }
            best.Visits = bestVisits!;
            best.Groups.Add(group);
        }

        Improve(runs);

        var shared = runs.Select(r => r.Done()).ToList();
        var longestGm = shared.Count == 0 ? 0 : shared.Max(r => r.Gm);

        if (shared.Any(r => r.GmPartial))
            notes.Add($"A hop with no position on file is left out of a run's distance and counted as {UnknownGm:0} Gm when sharing the work.");
        foreach (var ship in ships.Where(s => s.Scu is null))
            notes.Add($"{ship.Name}'s hold is not known, so nothing limits what it is given.");
        foreach (var ship in ships.Where(s => s.Start is null))
            notes.Add($"Where {ship.Name} is now was not given, so the first leg of that run is not counted.");
        if (tasks.Any(t => !t.ScuKnown))
            notes.Add("A leg whose card printed no amount counts as nothing aboard, so a hold could be fuller than shown.");

        return new SharePlan(shared, longestGm, shared.Sum(r => r.Gm), split, leftOut, notes);
    }

    /// <summary>Moves groups off the longest run while that makes it shorter.</summary>
    private static void Improve(List<Route> runs)
    {
        if (runs.Count < 2) return;

        for (var round = 0; round < MaxImprovements; round++)
        {
            var longest = runs.OrderByDescending(r => r.Length()).First();
            var before = longest.Length();
            var moved = false;

            foreach (var group in longest.Groups.Where(g => !g.Locked).ToList())
            {
                var without = longest.Without(group);
                var shorter = longest.Length(without);

                foreach (var other in runs.Where(r => r != longest))
                {
                    if (other.With(group) is not { } visits) continue;
                    var after = Math.Max(shorter, Math.Max(other.Length(visits),
                        runs.Where(r => r != longest && r != other).Select(r => r.Length()).DefaultIfEmpty(0).Max()));
                    if (after >= before - 1e-6) continue;

                    longest.Visits = without; longest.Groups.Remove(group);
                    other.Visits = visits; other.Groups.Add(group);
                    moved = true;
                    break;
                }
                if (moved) break;
            }

            if (!moved) return;
        }
    }

    private static Place? Spot(string? name, string? body, Func<string, ResolvedPlace?> resolve)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var place = resolve(name);
        return place is null
            ? new Place(name.Trim(), "", body, null)
            : new Place(place.Name, place.Id, place.Body ?? body, place.System);
    }

    /// <summary>Body centre to body centre, in Gm; null when either has no position on file or they are in different systems.</summary>
    public static double? BodyGm(StopWhere a, StopWhere b, Func<string, IReadOnlyDictionary<string, BodyPosition>> positions) =>
        new Hops(positions).Bodies(a.Body, a.System, b.Body, b.System);

    internal static Place? From(ResolvedPlace? start) =>
        start is null ? null : new Place(start.Name, start.Id, start.Body, start.System);

    internal sealed record Place(string Name, string Id, string? Body, string? System)
    {
        public bool Same(Place other) =>
            Id.Length > 0 && other.Id.Length > 0
                ? string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase)
                : string.Equals(Compact(Name), Compact(other.Name), StringComparison.OrdinalIgnoreCase);

        private static string Compact(string value) => new([.. value.Where(char.IsLetterOrDigit)]);
    }

    /// <summary>Distances between places: body centre to body centre, in Gm.</summary>
    internal sealed class Hops(Func<string, IReadOnlyDictionary<string, BodyPosition>> positions)
    {
        /// <summary>The measured distance, or null when either end has no position on file or they are in different systems.</summary>
        public double? Gm(Place a, Place b) => a.Same(b) ? 0 : Bodies(a.Body, a.System, b.Body, b.System);

        public double? Bodies(string? bodyA, string? systemA, string? bodyB, string? systemB)
        {
            var a = (Body: bodyA, System: systemA);
            var b = (Body: bodyB, System: systemB);
            if (a.System is { } sa && b.System is { } sb && !string.Equals(sa, sb, StringComparison.OrdinalIgnoreCase)) return null;
            if (a.Body is null || b.Body is null) return null;
            if (string.Equals(a.Body, b.Body, StringComparison.OrdinalIgnoreCase)) return 0;
            var system = a.System ?? b.System;
            if (system is null) return null;
            var bodies = Bodies(system);
            if (!bodies.TryGetValue(a.Body, out var pa) || !bodies.TryGetValue(b.Body, out var pb)) return null;
            return Math.Sqrt((pa.X - pb.X) * (pa.X - pb.X) + (pa.Y - pb.Y) * (pa.Y - pb.Y)) / 1e9;
        }

        // The star map keys bodies as it spells them - "microTech", "Pyro IV"
        // - and the atlas and the contract text do not always agree on case.
        // An exact lookup found none of 202 atlas places on this install.
        private readonly Dictionary<string, Dictionary<string, BodyPosition>> _systems = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, BodyPosition> Bodies(string system)
        {
            if (!_systems.TryGetValue(system, out var bodies))
            {
                bodies = new Dictionary<string, BodyPosition>(StringComparer.OrdinalIgnoreCase);
                foreach (var (name, at) in positions(system.ToLowerInvariant())) bodies.TryAdd(name, at);
                _systems[system] = bodies;
            }
            return bodies;
        }

        /// <summary>What a hop counts as when sharing the work: the distance, or a stand-in when there is none.</summary>
        public double Cost(Place a, Place b) => Gm(a, b) ?? (CrossSystem(a, b) ? CrossSystemGm : UnknownGm);

        private static bool CrossSystem(Place a, Place b) =>
            a.System is { } sa && b.System is { } sb && !string.Equals(sa, sb, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One leg of a contract, or a hold-sized load of one.</summary>
    /// <param name="From">The pickup; null when the cargo is already aboard.</param>
    private sealed record Job(int Contract, string Title, string? MissionId, string? Commodity,
        Place? From, Place To, int Scu, bool ScuKnown, IReadOnlyList<string> LegIds, bool Started);

    private sealed record Group(IReadOnlyList<Job> Jobs, bool Locked);

    private sealed record Visit(Job Job, bool Load)
    {
        public Place Where => Load ? Job.From! : Job.To;
    }

    private sealed class Route(ShareShip ship, Hops hop)
    {
        public ShareShip Ship { get; } = ship;
        public List<Visit> Visits { get; set; } = [];
        public List<Group> Groups { get; } = [];
        private readonly Place? _start = From(ship.Start);

        public double Length() => Length(Visits);

        public double Length(IReadOnlyList<Visit> visits)
        {
            var total = 0.0;
            var at = _start;
            foreach (var visit in visits)
            {
                if (at is not null) total += hop.Cost(at, visit.Where);
                at = visit.Where;
            }
            return total;
        }

        /// <summary>The route with the group's jobs inserted where each adds least and the hold allows; null when it cannot carry them.</summary>
        public List<Visit>? With(Group group)
        {
            var visits = Visits.ToList();
            foreach (var job in group.Jobs)
                foreach (var load in Loads(job))
                    if (Insert(visits, load) is { } next) visits = next;
                    else return null;
            return visits;
        }

        public List<Visit> Without(Group group) =>
            [.. Visits.Where(v => !group.Jobs.Any(j => SameJob(v.Job, j)))];

        private static bool SameJob(Job load, Job job) =>
            load.Contract == job.Contract && load.To == job.To && load.From == job.From && load.LegIds.SequenceEqual(job.LegIds);

        /// <summary>The job in hold-sized pieces: a leg bigger than the hold is several trips.</summary>
        private IEnumerable<Job> Loads(Job job)
        {
            if (Ship.Scu is not > 0 || job.Scu <= Ship.Scu || job.From is null)
            {
                yield return job;
                yield break;
            }
            var hold = (int)Math.Floor(Ship.Scu.Value);
            if (hold <= 0) { yield return job; yield break; }
            for (var left = job.Scu; left > 0; left -= hold)
                yield return job with { Scu = Math.Min(hold, left) };
        }

        private List<Visit>? Insert(List<Visit> visits, Job job)
        {
            List<Visit>? best = null;
            var bestLength = double.MaxValue;

            if (job.From is null)
            {
                for (var j = 0; j <= visits.Count; j++)
                {
                    var trial = visits.ToList();
                    trial.Insert(j, new Visit(job, false));
                    Consider(trial);
                }
                return best;
            }

            for (var i = 0; i <= visits.Count; i++)
                for (var j = i + 1; j <= visits.Count + 1; j++)
                {
                    var trial = visits.ToList();
                    trial.Insert(i, new Visit(job, true));
                    trial.Insert(j, new Visit(job, false));
                    Consider(trial);
                }
            return best;

            void Consider(List<Visit> trial)
            {
                if (!Fits(trial)) return;
                var length = Length(trial);
                if (length < bestLength - 1e-9) { best = trial; bestLength = length; }
            }
        }

        /// <summary>Whether the hold is never over: cargo already aboard counts from the start.</summary>
        private bool Fits(IReadOnlyList<Visit> visits)
        {
            if (Ship.Scu is not > 0) return true;
            var aboard = visits.Where(v => !v.Load && v.Job.From is null).Sum(v => v.Job.Scu);
            if (aboard > Ship.Scu + 1e-9) return false;
            foreach (var visit in visits)
            {
                aboard += visit.Load ? visit.Job.Scu : -visit.Job.Scu;
                if (aboard > Ship.Scu + 1e-9) return false;
            }
            return true;
        }

        /// <summary>The route as stops: visits in a row at one place are one stop.</summary>
        public ShareRun Done()
        {
            var stops = new List<ShareStop>();
            var at = _start;
            var gm = 0.0;
            var partial = false;
            var aboard = Visits.Where(v => !v.Load && v.Job.From is null).Sum(v => v.Job.Scu);
            var peak = aboard;
            var i = 0;

            while (i < Visits.Count)
            {
                var place = Visits[i].Where;
                var actions = new List<HaulAction>();
                while (i < Visits.Count && Visits[i].Where.Same(place))
                {
                    var v = Visits[i];
                    aboard += v.Load ? v.Job.Scu : -v.Job.Scu;
                    actions.Add(new HaulAction(v.Load ? "load" : "unload", v.Job.Commodity, v.Job.ScuKnown ? v.Job.Scu : null,
                        v.Job.Contract, v.Job.Title, MissionId: v.Job.MissionId, LegIds: v.Job.LegIds));
                    i++;
                }
                peak = Math.Max(peak, aboard);

                double? leg = at is null ? null : hop.Gm(at, place);
                if (at is not null && leg is null) partial = true;
                gm += leg ?? 0;
                stops.Add(new ShareStop(place.Name, place.Id, place.Body, place.System, leg, actions, Math.Max(0, aboard)));
                at = place;
            }

            return new ShareRun(Ship, stops, Math.Round(gm, 2), partial, peak,
                Visits.Select(v => v.Job.Contract).Distinct().Count());
        }
    }
}
