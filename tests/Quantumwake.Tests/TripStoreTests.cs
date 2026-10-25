using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Flight plans: the first thing in the app the player authors rather than the
/// logs producing, so the rules about what happens to a plan are worth pinning.
/// Each test gets its own directory - a plan is a file, and a shared one would
/// make these tests order-dependent.
/// </summary>
public class TripStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-trips-{Guid.NewGuid():N}");

    private TripStore NewStore() => new(_directory);

    private static TripStop Stop(string placeId, string place, string? note = null) =>
        new(string.Empty, placeId, place, note, Done: false, DoneAt: null);

    [Fact]
    public void A_new_plan_is_the_one_being_followed()
    {
        var store = NewStore();

        store.Add("First", [Stop("Stanton1_Lorville", "Lorville")]);
        var second = store.Add("Second", [Stop("GrimHEX", "GrimHEX")]);

        Assert.Equal(second.Id, store.Tracked()?.Id);
        Assert.Single(store.All(), t => t.Tracked);
    }

    [Fact]
    public void A_stop_lands_in_the_plan_being_filled()
    {
        var store = NewStore();
        store.Add("Run", [Stop("Stanton1_Lorville", "Lorville")]);

        var trip = store.AddStop(Stop("RR_MIC_L1", "microTech L1", "Sell the ore"));

        Assert.Equal(2, trip.Stops.Count);
        Assert.Equal("Sell the ore", trip.Stops[1].Note);
    }

    /// <summary>
    /// A plan written whole brings its run sheet: a rep run's crates, cleaned
    /// as one added by hand would be, and never already ticked.
    /// </summary>
    [Fact]
    public void A_plan_written_whole_keeps_its_actions_unticked()
    {
        var store = NewStore();
        var crate = new RunAction("theirs", "Collect", "  One crate ", null, "crates", Done: true, DoneAt: DateTimeOffset.UtcNow);
        var blank = new RunAction("x", "do", " ", null, null, Done: false, DoneAt: null);

        var trip = store.Add("Rep run", [Stop("GrimHEX", "GrimHEX") with { Actions = [crate, blank] }]);

        var action = Assert.Single(trip.Stops[0].Actions!);
        Assert.Equal("collect", action.Kind);
        Assert.Equal("One crate", action.Text);
        Assert.Null(action.Unit);
        Assert.False(action.Done);
        Assert.NotEqual("theirs", action.Id);
    }

    /// <summary>
    /// A tweak changes the plan and keeps its first figures once: changed
    /// twice, it still compares against the original; put back, there is
    /// nothing left to compare; repeated, the settled figures are the plan.
    /// </summary>
    [Fact]
    public void A_tweak_keeps_the_first_plan_beside_it_until_it_is_put_back()
    {
        var store = NewStore();
        var sell = new RunAction("", "sell", "Agricium", 96, "SCU", Done: false, DoneAt: null, Price: 1656);
        var trip = store.Add("Agricium run", [Stop("RR_MIC_L1", "microTech L1") with { Actions = [sell] }]);
        var (stop, action) = (trip.Stops[0].Id, trip.Stops[0].Actions![0]);

        Assert.Equal(1656, action.Price);
        Assert.Null(action.Was);

        Assert.True(store.Tweak(trip.Id, stop, action.Id, 96, 1600));
        Assert.True(store.Tweak(trip.Id, stop, action.Id, 80, 1600));

        var tweaked = store.All()[0].Stops[0].Actions![0];
        Assert.Equal((80m, 1600m), (tweaked.Quantity!.Value, tweaked.Price!.Value));
        Assert.Equal(new RunActionWas(96, 1656), tweaked.Was);

        var again = store.Repeat(trip.Id, DateTimeOffset.UtcNow)!.Stops[0].Actions![0];
        Assert.Equal((80m, 1600m), (again.Quantity!.Value, again.Price!.Value));
        Assert.Null(again.Was);

        Assert.True(store.Tweak(trip.Id, stop, action.Id, 96, 1656));
        Assert.Null(store.All().First(t => t.Id == trip.Id).Stops[0].Actions![0].Was);

        Assert.False(store.Tweak(trip.Id, stop, "nope", 1, 1));
    }

    [Fact]
    public void Adding_a_stop_with_no_plan_starts_one()
    {
        var store = NewStore();

        var trip = store.AddStop(Stop("GrimHEX", "GrimHEX"));

        Assert.Single(trip.Stops);
        Assert.True(trip.Tracked);
    }

    [Fact]
    public void Next_is_the_first_stop_not_crossed_off()
    {
        var store = NewStore();
        var trip = store.Add("Run", [
            Stop("Stanton1_Lorville", "Lorville"),
            Stop("RR_MIC_L1", "microTech L1")
        ]);

        store.ToggleStop(trip.Id, trip.Stops[0].Id);

        Assert.Equal("microTech L1", store.Tracked()?.Next?.Place);
    }

    /// <summary>The app knows where the player is, so it should not ask.</summary>
    [Fact]
    public void Arriving_crosses_off_the_stop_for_that_place()
    {
        var store = NewStore();
        var trip = store.Add("Run", [
            Stop("Stanton1_Lorville", "Lorville"),
            Stop("RR_MIC_L1", "microTech L1")
        ]);

        Assert.True(store.Arrived("RR_MIC_L1"));

        var after = store.All().Single(t => t.Id == trip.Id);
        Assert.False(after.Stops[0].Done);
        Assert.True(after.Stops[1].Done);
        Assert.NotNull(after.Stops[1].DoneAt);
    }

    /// <summary>
    /// A run that calls at one place twice is two stops, and one landing is one
    /// stop crossed off - otherwise the second visit is lost before it happens.
    /// </summary>
    [Fact]
    public void Arriving_crosses_off_one_stop_at_a_time()
    {
        var store = NewStore();
        var trip = store.Add("Round trip", [
            Stop("GrimHEX", "GrimHEX"),
            Stop("RR_MIC_L1", "microTech L1"),
            Stop("GrimHEX", "GrimHEX")
        ]);

        store.Arrived("GrimHEX");
        var after = store.All().Single(t => t.Id == trip.Id);

        Assert.True(after.Stops[0].Done);
        Assert.False(after.Stops[2].Done);
    }

    [Fact]
    public void Arriving_leaves_untracked_plans_alone()
    {
        var store = NewStore();
        var idle = store.Add("Idle", [Stop("GrimHEX", "GrimHEX")]);
        store.Add("Followed", [Stop("RR_MIC_L1", "microTech L1")]);

        Assert.False(store.Arrived("GrimHEX"));
        Assert.False(store.All().Single(t => t.Id == idle.Id).Stops[0].Done);
    }

    /// <summary>
    /// A stop from a UEX terminal often has no engine id, and those are exactly
    /// the stops the map cannot draw either - they must not also be the only
    /// ones that never cross themselves off.
    /// </summary>
    [Fact]
    public void Arriving_crosses_off_a_stop_that_has_only_a_name()
    {
        var store = NewStore();
        var trip = store.Add("Run", [Stop(string.Empty, "Port Tressler")]);

        Assert.True(store.Arrived("Port_Tressler_Engine_Id", "Port-Tressler"));
        Assert.True(store.All().Single(t => t.Id == trip.Id).Stops[0].Done);
    }

    [Fact]
    public void A_name_never_wins_over_an_id()
    {
        var store = NewStore();
        var trip = store.Add("Run", [
            Stop(string.Empty, "Lorville"),
            Stop("Stanton1_Lorville", "Lorville")
        ]);

        store.Arrived("Stanton1_Lorville", "Lorville");
        var after = store.All().Single(t => t.Id == trip.Id);

        Assert.False(after.Stops[0].Done);
        Assert.True(after.Stops[1].Done);
    }

    [Fact]
    public void A_stop_with_an_id_is_not_crossed_off_by_a_name_alone()
    {
        var store = NewStore();
        var trip = store.Add("Run", [Stop("RR_MIC_L1", "microTech L1")]);

        Assert.False(store.Arrived("Somewhere_Else", "microTech L1"));
        Assert.False(store.All().Single(t => t.Id == trip.Id).Stops[0].Done);
    }

    [Fact]
    public void Arriving_nowhere_known_changes_nothing()
    {
        var store = NewStore();
        store.Add("Run", [Stop("GrimHEX", "GrimHEX")]);

        Assert.False(store.Arrived(null));
        Assert.False(store.Arrived(""));
        Assert.False(store.Arrived("Somewhere_Else"));
    }

    [Fact]
    public void Stops_can_be_reordered_but_not_off_the_ends()
    {
        var store = NewStore();
        var trip = store.Add("Run", [
            Stop("A", "First"),
            Stop("B", "Second")
        ]);

        Assert.False(store.MoveStop(trip.Id, trip.Stops[0].Id, -1));
        Assert.True(store.MoveStop(trip.Id, trip.Stops[0].Id, 1));

        Assert.Equal("Second", store.Tracked()?.Stops[0].Place);
    }

    [Fact]
    public void Tracking_the_same_plan_twice_stops_following_it()
    {
        var store = NewStore();
        var trip = store.Add("Run", [Stop("GrimHEX", "GrimHEX")]);

        Assert.True(store.Track(trip.Id));
        Assert.Null(store.Tracked());
    }

    [Fact]
    public void Plans_survive_a_restart()
    {
        var first = NewStore();
        first.Add("Run", [Stop("GrimHEX", "GrimHEX", "Pick up armour")]);

        var reopened = NewStore();
        var trip = Assert.Single(reopened.All());

        Assert.Equal("Run", trip.Title);
        Assert.Equal("Pick up armour", trip.Stops[0].Note);
    }

    [Fact]
    public void A_stop_can_keep_checkable_manual_run_actions()
    {
        var store = NewStore();
        var trip = store.Add("Cargo run", [Stop("Stanton1_Lorville", "Lorville")]);
        var stop = Assert.Single(trip.Stops);

        Assert.True(store.AddAction(trip.Id, stop.Id, "load", "Agricium", 96, "scu"));
        var action = Assert.Single(store.All().Single().Stops.Single().Actions!);

        Assert.Equal("load", action.Kind);
        Assert.Equal("Agricium", action.Text);
        Assert.Equal(96, action.Quantity);
        Assert.Equal("SCU", action.Unit);
        Assert.True(store.ToggleAction(trip.Id, stop.Id, action.Id));
        Assert.True(store.All().Single().Stops.Single().Actions!.Single().Done);
    }

    [Fact]
    public void A_generated_action_keeps_its_contract_and_leg_identity_after_restart()
    {
        var store = NewStore();
        var trip = store.Add("Cargo run", [Stop("Stanton1_Lorville", "Lorville")]);
        var stop = Assert.Single(trip.Stops);

        Assert.True(store.AddAction(trip.Id, stop.Id, "unload", "Copper", 4, "SCU",
            new RunActionLink("mission-42", ["leg-2", "leg-3"])));

        var action = Assert.Single(NewStore().All().Single().Stops.Single().Actions!);
        Assert.Equal("mission-42", action.Link!.MissionId);
        Assert.Equal(["leg-2", "leg-3"], action.Link.LegIds);
    }

    [Fact]
    public void Arrival_keeps_the_current_stop_active_until_its_run_sheet_is_done()
    {
        var store = NewStore();
        var trip = store.Add("Cargo run", [Stop("Stanton1_Lorville", "Lorville")]);
        var stop = Assert.Single(trip.Stops);
        store.AddAction(trip.Id, stop.Id, "load", "Agricium", 96, "SCU");

        Assert.True(store.Arrived("Stanton1_Lorville"));
        var arrived = store.All().Single();
        var action = Assert.Single(arrived.Stops.Single().Actions!);
        Assert.Equal("Lorville", arrived.Next?.Place);
        Assert.False(arrived.Done);

        store.ToggleAction(arrived.Id, arrived.Stops.Single().Id, action.Id);
        Assert.True(store.All().Single().Done);
    }

    [Fact]
    public void An_unreadable_file_leaves_the_player_with_no_plans()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "trips.json"), "{ this is not json");

        Assert.Empty(NewStore().All());
    }

    /// <summary>
    /// A drag lets go once, wherever it ends: the stop lands there in one call
    /// rather than being swapped a place at a time, and the others keep their order.
    /// </summary>
    [Fact]
    public void A_dragged_stop_lands_where_it_was_let_go()
    {
        var store = NewStore();
        var trip = store.Add("Run", [
            Stop("A", "Area18"), Stop("B", "Baijini"), Stop("C", "CRU-L1"), Stop("D", "Daymar"),
        ]);
        var last = trip.Stops[3];

        Assert.True(store.PlaceStop(trip.Id, last.Id, 0));
        Assert.Equal(["D", "A", "B", "C"], store.All().Single().Stops.Select(s => s.PlaceId));

        // Past the end is the end, not a refusal: the drop target below the
        // last card has no index of its own.
        Assert.True(store.PlaceStop(trip.Id, last.Id, 99));
        Assert.Equal(["A", "B", "C", "D"], store.All().Single().Stops.Select(s => s.PlaceId));

        Assert.False(store.PlaceStop(trip.Id, "nope", 0));
    }

    /// <summary>
    /// A line carried to another stop is the same line: its id, its tick and
    /// its figures go with it, because re-typing it would lose the price.
    /// </summary>
    [Fact]
    public void A_run_sheet_line_carried_to_another_stop_keeps_what_it_was()
    {
        var store = NewStore();
        var trip = store.Add("Haul", [Stop("A", "Area18"), Stop("B", "Baijini")]);
        var (first, second) = (trip.Stops[0], trip.Stops[1]);
        store.AddAction(trip.Id, first.Id, "load", "Crate", 2, "SCU");
        store.AddAction(trip.Id, first.Id, "unload", "Crate", 2, "SCU");
        store.AddAction(trip.Id, second.Id, "refuel", "Top up", null, null);

        var drop = store.All().Single().Stops[0].Actions![1];
        store.ToggleAction(trip.Id, first.Id, drop.Id);

        Assert.True(store.MoveAction(trip.Id, first.Id, drop.Id, second.Id, 0));

        var moved = store.All().Single();
        Assert.Single(moved.Stops[0].Actions!);
        Assert.Equal(["unload", "refuel"], moved.Stops[1].Actions!.Select(a => a.Kind));

        var carried = moved.Stops[1].Actions![0];
        Assert.Equal(drop.Id, carried.Id);
        Assert.True(carried.Done);
        Assert.Equal(2, carried.Quantity);
    }

    /// <summary>
    /// The Itinerary page can have a plan open that is not the tracked one,
    /// and a stop typed there belongs to the plan on screen.
    /// </summary>
    [Fact]
    public void A_stop_added_to_a_named_plan_goes_to_that_plan_and_not_the_tracked_one()
    {
        var store = NewStore();
        var open = store.Add("Open on the page", [Stop("A", "Area18")]);
        var followed = store.Add("Tracked", [Stop("B", "Baijini")]);

        var landed = store.AddStopTo(open.Id, Stop("C", "CRU-L1", "Refuel"));

        Assert.Equal(open.Id, landed?.Id);
        Assert.Equal(["A", "C"], store.All().Single(t => t.Id == open.Id).Stops.Select(s => s.PlaceId));
        Assert.Single(store.All().Single(t => t.Id == followed.Id).Stops);
        Assert.Equal(followed.Id, store.Tracked()?.Id);
        Assert.Null(store.AddStopTo("nope", Stop("D", "Daymar")));
    }

    [Fact]
    public void A_line_moved_within_its_own_stop_is_reordered()
    {
        var store = NewStore();
        var trip = store.Add("Haul", [Stop("A", "Area18")]);
        var stop = trip.Stops[0];
        store.AddAction(trip.Id, stop.Id, "load", "First", null, null);
        store.AddAction(trip.Id, stop.Id, "load", "Second", null, null);
        store.AddAction(trip.Id, stop.Id, "load", "Third", null, null);

        var first = store.All().Single().Stops[0].Actions![0];
        Assert.True(store.MoveAction(trip.Id, stop.Id, first.Id, stop.Id, 2));

        Assert.Equal(["Second", "Third", "First"], store.All().Single().Stops[0].Actions!.Select(a => a.Text));
        Assert.False(store.MoveAction(trip.Id, stop.Id, first.Id, "nowhere", 0));
        Assert.Equal(3, store.All().Single().Stops[0].Actions!.Count);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test run over.
        }
    }
}
