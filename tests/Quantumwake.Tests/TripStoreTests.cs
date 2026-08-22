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
    public void An_unreadable_file_leaves_the_player_with_no_plans()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "trips.json"), "{ this is not json");

        Assert.Empty(NewStore().All());
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
