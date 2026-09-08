using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// A run with a beginning and an end, and what happens to one left going.
/// </summary>
/// <remarks>
/// Two things here are easy to get subtly wrong and hard to notice. Elapsed
/// time measured from the wrong date reports a plan written last week as a
/// week-long flight, which is a number somebody would believe. And a sweep that
/// files a run somebody is still flying turns a fortnight off into lost work -
/// which is why archiving is reversible and why resuming keeps the start.
/// </remarks>
public class RunLifecycleTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-runs-{Guid.NewGuid():N}");

    public RunLifecycleTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static readonly DateTimeOffset Noon = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_plan_is_not_a_run_until_it_is_started()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");

        Assert.Null(trip.StartedAt);
        Assert.False(trip.Flying);
        Assert.Null(trip.Elapsed(Noon));
    }

    /// <summary>
    /// The whole reason StartedAt exists. A plan authored a week before it is
    /// flown must not report a week of elapsed time.
    /// </summary>
    [Fact]
    public void Elapsed_time_runs_from_the_start_not_from_the_writing()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");

        trips.Start(trip.Id, Noon);
        var flying = trips.All().Single();

        Assert.Equal(TimeSpan.FromHours(2), flying.Elapsed(Noon.AddHours(2)));
    }

    [Fact]
    public void Finishing_stops_the_clock_and_files_it()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.Start(trip.Id, Noon);
        trips.Finish(trip.Id, Noon.AddHours(3));

        var done = trips.All().Single();

        Assert.Equal(Archived.You, done.Archived);
        Assert.False(done.Flying);
        Assert.Equal(TimeSpan.FromHours(3), done.Elapsed(Noon.AddDays(9)));
    }

    [Fact]
    public void Starting_twice_keeps_the_first_start()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");

        Assert.True(trips.Start(trip.Id, Noon));
        Assert.False(trips.Start(trip.Id, Noon.AddHours(4)));
        Assert.Equal(Noon, trips.All().Single().StartedAt);
    }

    /// <summary>
    /// A run finished without being started still took time, and the only date
    /// available is when the plan was written. Better than a null the review
    /// would have nothing to say about.
    /// </summary>
    [Fact]
    public void Finishing_something_never_started_still_has_a_span()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");

        trips.Finish(trip.Id, Noon);

        Assert.NotNull(trips.All().Single().Elapsed(Noon));
    }

    // ---- the quiet sweep ----

    [Fact]
    public void A_run_left_going_is_filed_on_its_own()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.Start(trip.Id, Noon);

        Assert.Equal(1, trips.SweepQuiet(TimeSpan.FromDays(10), DateTimeOffset.UtcNow.AddDays(11)));
        Assert.Equal(Archived.Quiet, trips.All().Single().Archived);
    }

    /// <summary>
    /// An unstarted plan is a backlog item, not an abandoned flight. Filing
    /// those would empty the list somebody keeps their intentions in.
    /// </summary>
    [Fact]
    public void A_plan_that_was_never_flown_is_left_alone()
    {
        var trips = new TripStore(_root);
        trips.Add("Some day");

        Assert.Equal(0, trips.SweepQuiet(TimeSpan.FromDays(10), DateTimeOffset.UtcNow.AddDays(400)));
        Assert.Equal(Archived.No, trips.All().Single().Archived);
    }

    /// <summary>
    /// The clock is when the run was last worked on, not when it began - so a
    /// long run somebody is still ticking stops on survives.
    /// </summary>
    [Fact]
    public void A_run_still_being_worked_on_survives()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.Start(trip.Id, DateTimeOffset.UtcNow.AddDays(-30));
        trips.AddStop(new TripStop("s1", "Stanton1", "Hurston", null, false, null));

        Assert.Equal(0, trips.SweepQuiet(TimeSpan.FromDays(10), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Turning_the_sweep_off_files_nothing()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.Start(trip.Id, Noon);

        Assert.Equal(0, trips.SweepQuiet(TimeSpan.Zero, DateTimeOffset.UtcNow.AddYears(2)));
    }

    /// <summary>
    /// The half that makes the sweep safe: a fortnight off costs a click, not a
    /// run. The start survives, or a two-week flight reads as a two-minute one.
    /// </summary>
    [Fact]
    public void Resuming_a_filed_run_keeps_when_it_began()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.Start(trip.Id, Noon);
        trips.SweepQuiet(TimeSpan.FromDays(10), DateTimeOffset.UtcNow.AddDays(11));

        Assert.True(trips.Resume(trip.Id));

        var back = trips.All().Single();

        Assert.Equal(Archived.No, back.Archived);
        Assert.Equal(Noon, back.StartedAt);
        Assert.True(back.Flying);
    }

    /// <summary>
    /// Resuming has to reset the clock too, or the sweep files it again on the
    /// next read and the button appears to do nothing.
    /// </summary>
    [Fact]
    public void A_resumed_run_is_not_filed_again_immediately()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.Start(trip.Id, Noon);
        trips.SweepQuiet(TimeSpan.FromDays(10), DateTimeOffset.UtcNow.AddDays(11));
        trips.Resume(trip.Id);

        Assert.Equal(0, trips.SweepQuiet(TimeSpan.FromDays(10), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_finished_run_is_not_swept_again()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.Start(trip.Id, Noon);
        trips.Finish(trip.Id, Noon.AddHours(1));

        Assert.Equal(0, trips.SweepQuiet(TimeSpan.FromDays(10), DateTimeOffset.UtcNow.AddYears(1)));
        Assert.Equal(Archived.You, trips.All().Single().Archived);
    }

    // ---- repeating ----

    [Fact]
    public void Repeating_a_run_copies_the_route_and_not_the_history()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.AddStop(new TripStop("s1", "Stanton1", "Hurston", "Pick up ore", false, null));
        trips.AddAction(trip.Id, "s1", "load", "96 SCU Agricium", 96, "SCU");
        trips.ToggleStop(trip.Id, "s1");
        trips.Start(trip.Id, Noon);
        trips.Finish(trip.Id, Noon.AddHours(2));

        var copy = trips.Repeat(trip.Id, Noon.AddDays(1));

        Assert.NotNull(copy);
        Assert.Equal("Ore run", copy.Title);
        Assert.NotEqual(trip.Id, copy.Id);
        Assert.Null(copy.StartedAt);
        Assert.Equal(Archived.No, copy.Archived);
        Assert.All(copy.Stops, stop => Assert.False(stop.Done));
        Assert.All(copy.Stops, stop => Assert.All(stop.Actions ?? [], a => Assert.False(a.Done)));
    }

    /// <summary>
    /// Ids have to be fresh, or ticking a stop on the copy ticks it on the run
    /// it came from.
    /// </summary>
    [Fact]
    public void A_repeated_run_shares_no_ids_with_its_source()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.AddStop(new TripStop("s1", "Stanton1", "Hurston", null, false, null));

        var copy = trips.Repeat(trip.Id, Noon)!;

        Assert.DoesNotContain(copy.Stops, stop => stop.Id == "s1");
    }

    // ---- the setting ----

    [Fact]
    public void The_patience_setting_has_a_stated_default_and_survives_a_restart()
    {
        Assert.Equal(RunSettings.DefaultDays, new RunSettingsStore(_root).Current.ArchiveAfterDays);

        new RunSettingsStore(_root).Save(3);

        Assert.Equal(3, new RunSettingsStore(_root).Current.ArchiveAfterDays);
    }

    [Fact]
    public void An_absurd_patience_is_brought_back_into_range()
    {
        Assert.Equal(0, RunSettings.Clean(-5).ArchiveAfterDays);
        Assert.Equal(RunSettings.MaxDays, RunSettings.Clean(9999).ArchiveAfterDays);
        Assert.Equal(RunSettings.DefaultDays, RunSettings.Clean(null).ArchiveAfterDays);
    }
}
