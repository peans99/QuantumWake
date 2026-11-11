using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Sharing the open hauling contracts between a group's ships (1.2.32), so
/// the longest single run is as short as it can be.
/// </summary>
/// <remarks>
/// A made-up Stanton laid out on round numbers, so every distance can be
/// checked by eye: Hurston at the origin, Crusader 20 Gm east, ArcCorp 30 Gm
/// north, microTech 40 Gm out on both.
/// </remarks>
public class HaulShareTests
{
    // Keyed the way the community star map keys them: as spelled, and with
    // an ordinary case-sensitive dictionary, which is what deserialising the
    // digest gives. A lower-cased lookup found nothing in the real one.
    private static readonly Dictionary<string, BodyPosition> Bodies = new()
    {
        ["Hurston"] = new(0, 0),
        ["Crusader"] = new(20e9, 0),
        ["ArcCorp"] = new(0, 30e9),
        ["microTech"] = new(40e9, 40e9),
    };

    private static readonly Dictionary<string, ResolvedPlace> Places = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lorville"] = new("lorville", "Lorville", "Hurston", "Stanton"),
        ["Everus Harbor"] = new("everus", "Everus Harbor", "Hurston", "Stanton"),
        ["Orison"] = new("orison", "Orison", "Crusader", "Stanton"),
        ["Seraphim Station"] = new("seraphim", "Seraphim Station", "Crusader", "Stanton"),
        ["Area18"] = new("area18", "Area18", "ArcCorp", "Stanton"),
        ["New Babbage"] = new("newbab", "New Babbage", "microTech", "Stanton"),
        ["Ruin Station"] = new("ruin", "Ruin Station", "Pyro VI", "Pyro"),
    };

    private static ResolvedPlace? Resolve(string name) => Places.GetValueOrDefault(name);

    private static IReadOnlyDictionary<string, BodyPosition> Positions(string system) =>
        system == "stanton" ? Bodies : new Dictionary<string, BodyPosition>();

    private static PlannedContract Contract(string title, params HaulLeg[] legs) =>
        new(title, title.ToLowerInvariant().Replace(' ', '-'), "Agricium", HaulShape.Direct, legs, "screenshot", null, null,
            legs.Length, 0, legs.Length, 0, legs.Sum(l => l.Scu ?? 0), null);

    private static HaulLeg Leg(string from, string to, int? scu, string id = "") => new(from, null, to, null, "Agricium", scu, null, id);

    private static ShareShip Ship(string id, string at, double? scu = 100, bool me = false) =>
        new(id, id, "Test hull", scu, Places[at], me);

    private static SharePlan Share(IReadOnlyList<PlannedContract> contracts, IReadOnlyList<ShareShip> ships, bool split = false) =>
        HaulShare.Share(contracts, ships, split, Resolve, Positions);

    [Fact]
    public void One_ship_picks_up_before_it_delivers_and_measures_the_run()
    {
        var plan = Share([Contract("Grain", Leg("Orison", "Area18", 20))], [Ship("me", "Lorville", me: true)]);

        var run = Assert.Single(plan.Runs);
        Assert.Equal(["Orison", "Area18"], run.Stops.Select(s => s.Place));
        Assert.Equal("load", run.Stops[0].Actions[0].Kind);
        // Hurston to Crusader is 20 Gm, Crusader to ArcCorp is the 3-4-5
        // triangle's long side: sqrt(20² + 30²).
        Assert.Equal(20, run.Stops[0].LegGm!.Value, 2);
        Assert.Equal(Math.Sqrt(20 * 20 + 30 * 30), run.Stops[1].LegGm!.Value, 2);
        Assert.Equal(20 + Math.Sqrt(1300), run.Gm, 1);
        Assert.False(run.GmPartial);
    }

    [Fact]
    public void Two_ships_each_take_the_work_near_them()
    {
        var contracts = new[]
        {
            Contract("Hurston local", Leg("Lorville", "Everus Harbor", 10)),
            Contract("microTech local", Leg("New Babbage", "New Babbage", 10)),
            Contract("Crusader hop", Leg("Orison", "Seraphim Station", 10)),
        };
        var alone = Share(contracts, [Ship("me", "Lorville", me: true)]);
        var together = Share(contracts, [Ship("me", "Lorville", me: true), Ship("mate", "New Babbage")]);

        Assert.True(together.LongestGm < alone.LongestGm, $"{together.LongestGm} should beat {alone.LongestGm}");
        var mate = together.Runs.Single(r => r.Ship.Id == "mate");
        Assert.Contains(mate.Stops, s => s.Place == "New Babbage");
        Assert.DoesNotContain(mate.Stops, s => s.Place == "Lorville");
    }

    [Fact]
    public void A_leg_bigger_than_the_hold_is_several_trips_and_the_hold_is_never_over()
    {
        var plan = Share([Contract("Bulk", Leg("Lorville", "Orison", 100))], [Ship("me", "Lorville", scu: 40, me: true)]);

        var run = Assert.Single(plan.Runs);
        var loads = run.Stops.SelectMany(s => s.Actions).Where(a => a.Kind == "load").ToList();
        Assert.Equal(3, loads.Count);
        Assert.Equal(100, loads.Sum(a => a.Scu ?? 0));
        Assert.True(run.PeakScu <= 40, $"peak {run.PeakScu}");
        Assert.All(run.Stops, s => Assert.True(s.AboardScu <= 40));
    }

    [Fact]
    public void A_whole_contract_stays_on_one_ship_unless_splitting_is_allowed()
    {
        // One pickup on Hurston, one on microTech, both delivered at home: two
        // legs at opposite ends of the system, and a ship at each end.
        var contract = Contract("Two ends", Leg("Lorville", "Everus Harbor", 10, "a"), Leg("New Babbage", "New Babbage", 10, "b"));
        var ships = new[] { Ship("me", "Lorville", me: true), Ship("mate", "New Babbage") };

        var whole = Share([contract], ships);
        Assert.Single(whole.Runs, r => r.Stops.Count > 0);

        var split = Share([contract], ships, split: true);
        Assert.Equal(2, split.Runs.Count(r => r.Stops.Count > 0));
        Assert.True(split.LongestGm < whole.LongestGm);
        Assert.True(split.Split);
    }

    [Fact]
    public void A_contract_with_a_pickup_made_stays_with_my_ship()
    {
        var started = Contract("Started", Leg("New Babbage", "New Babbage", 10)) with { PickupsDone = 1, Pickups = 2 };
        var plan = Share([started], [Ship("me", "Lorville", me: true), Ship("mate", "New Babbage")]);

        Assert.NotEmpty(plan.Runs.Single(r => r.Ship.Me).Stops);
        Assert.Empty(plan.Runs.Single(r => r.Ship.Id == "mate").Stops);
    }

    [Fact]
    public void Cargo_already_aboard_is_only_dropped()
    {
        var aboard = Contract("Aboard", Leg("Lorville", "Orison", 30)) with { PickupsDone = 1, Pickups = 1 };
        var plan = Share([aboard], [Ship("me", "Lorville", scu: 40, me: true)]);

        var stop = Assert.Single(plan.Runs.Single().Stops);
        Assert.Equal("Orison", stop.Place);
        Assert.Equal("unload", Assert.Single(stop.Actions).Kind);
        Assert.Equal(30, plan.Runs.Single().PeakScu);
    }

    [Fact]
    public void A_contract_with_an_end_not_known_is_left_out_and_says_why()
    {
        var plan = Share([Contract("Half read", new HaulLeg(null, null, "Orison", null, "Agricium", 10, null))], [Ship("me", "Lorville", me: true)]);

        var out_ = Assert.Single(plan.LeftOut);
        Assert.Equal("Half read", out_.ContractTitle);
        Assert.Contains("Contracts app", out_.Why);
        Assert.Empty(plan.Runs.Single().Stops);
    }

    [Fact]
    public void A_place_with_no_position_is_a_floor_not_a_guess()
    {
        var plan = Share([Contract("Somewhere", Leg("Lorville", "Unheard Of Outpost", 5))], [Ship("me", "Lorville", me: true)]);

        var run = plan.Runs.Single();
        Assert.True(run.GmPartial);
        Assert.Null(run.Stops.Last().LegGm);
        Assert.Contains(plan.Notes, n => n.Contains("no position on file"));
    }

    [Fact]
    public void Another_system_is_never_measured()
    {
        var plan = Share([Contract("Pyro run", Leg("Lorville", "Ruin Station", 5))], [Ship("me", "Lorville", me: true)]);

        Assert.Null(plan.Runs.Single().Stops.Last().LegGm);
        Assert.True(plan.Runs.Single().GmPartial);
    }

    [Fact]
    public void A_hold_nobody_knows_is_said_and_not_limited()
    {
        var plan = Share([Contract("Bulk", Leg("Lorville", "Orison", 500))], [Ship("me", "Lorville", scu: null, me: true)]);

        Assert.Contains(plan.Notes, n => n.Contains("hold is not known"));
        Assert.Single(plan.Runs.Single().Stops.SelectMany(s => s.Actions), a => a.Kind == "load");
    }

    [Fact]
    public void Body_distances_are_there_for_other_callers()
    {
        Assert.Equal(20, HaulShare.BodyGm(new StopWhere("Hurston", "Stanton"), new StopWhere("Crusader", "Stanton"), Positions)!.Value, 2);
        Assert.Equal(0, HaulShare.BodyGm(new StopWhere("Hurston", "Stanton"), new StopWhere("hurston", "Stanton"), Positions));
        Assert.Null(HaulShare.BodyGm(new StopWhere("Hurston", "Stanton"), new StopWhere("Pyro VI", "Pyro"), Positions));
        Assert.Null(HaulShare.BodyGm(new StopWhere(null, "Stanton"), new StopWhere("Crusader", "Stanton"), Positions));
    }

    [Fact]
    public void Put_in_order_takes_the_nearest_stop_next_when_it_knows_the_distance()
    {
        // Two stops, neither on the body the pilot is at: microTech was first
        // on the plan, but Crusader is half the distance from Hurston.
        var trip = new Trip("t", "Run", DateTimeOffset.UtcNow,
        [
            new TripStop("far", "newbab", "New Babbage", null, false, null),
            new TripStop("near", "orison", "Orison", null, false, null),
        ]);
        StopWhere Where(TripStop s) => new(Places[s.Place].Body, "Stanton");

        var order = TripOrder.Optimise(trip, Where, null, new StopWhere("Hurston", "Stanton"),
            (a, b) => HaulShare.BodyGm(a, b, Positions));
        Assert.Equal(["near", "far"], order);

        // Without distances the plan's own order stands, as before.
        Assert.Equal(["far", "near"], TripOrder.Optimise(trip, Where, null, new StopWhere("Hurston", "Stanton")));
    }
}

public class CrewStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"qw-crew-{Guid.NewGuid():N}");

    [Fact]
    public void A_wingmate_is_kept_changed_and_removed()
    {
        var store = new CrewStore(_directory);
        var mate = store.Save(null, "  Rook  ", "DRAK_Cutlass_Black")!;
        Assert.Equal("Rook", mate.Name);

        var again = new CrewStore(_directory);
        Assert.Equal("DRAK_Cutlass_Black", Assert.Single(again.All).Hull);

        again.Save(mate.Id, "Rook", "RSI_Constellation_Taurus");
        Assert.Equal("RSI_Constellation_Taurus", Assert.Single(new CrewStore(_directory).All).Hull);

        Assert.True(again.Remove(mate.Id));
        Assert.Empty(new CrewStore(_directory).All);
    }

    [Fact]
    public void A_wingmate_needs_a_name_and_the_list_has_a_ceiling()
    {
        var store = new CrewStore(_directory);
        Assert.Null(store.Save(null, "   ", null));
        for (var i = 0; i < CrewStore.Max; i++) Assert.NotNull(store.Save(null, $"Pilot {i}", null));
        Assert.Null(store.Save(null, "One too many", null));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
