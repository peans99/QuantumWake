using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// A run's plan against what the logs recorded while it ran.
/// </summary>
/// <remarks>
/// Everything here rests on an inference: a receipt names a kiosk and never a
/// place, so the ledger back-tracks the position from the last arrival before
/// it. That makes the interesting tests the ones about restraint - a stop that
/// was never ticked, a sale somewhere the run never went, money that moved
/// before the run began. Getting any of those wrong produces a total that looks
/// authoritative and is invented.
///
/// Every fixture here puts its sales AFTER the stop's DoneAt, because landing
/// is what ticks a stop: the pilot arrives and then trades. These originally
/// read the other way and passed, which is how the window ran backwards through
/// two releases - the tests agreed with the code about a thing both had wrong.
/// </remarks>
public class RunReviewTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static LedgerEntry Sale(DateTimeOffset at, string where, decimal amount, bool confirmed = false) =>
        new(at, "Cargo sold", "Agricium · 96 SCU", where, "TDD", amount, 96, confirmed, 0);

    private static LedgerEntry Payout(DateTimeOffset at, string where, decimal amount) =>
        new(at, "Contract paid", "Cargo haul", where, "", amount, 0, true, 0);

    private static TripStop Stop(string id, string place, DateTimeOffset? doneAt) =>
        new(id, place.Replace(" ", ""), place, null, doneAt is not null, doneAt);

    private static Trip Run(params TripStop[] stops) =>
        new("t1", "Ore run", Noon.AddDays(-3), stops, StartedAt: Noon, FinishedAt: Noon.AddHours(4));

    [Fact]
    public void A_plan_never_started_has_nothing_to_review()
    {
        var plan = new Trip("t1", "Ore run", Noon, [Stop("s1", "Hurston", null)]);

        Assert.Null(RunReviewer.Build(plan, [], Noon.AddHours(1)));
    }

    [Fact]
    public void A_sale_at_a_stop_is_claimed_by_it()
    {
        var review = RunReviewer.Build(
            Run(Stop("s1", "Hurston", Noon.AddHours(1))),
            [Sale(Noon.AddHours(1).AddMinutes(30), "Hurston", 288_000)],
            Noon.AddHours(5))!;

        var claim = Assert.Single(review.Stops.Single().Claimed);

        Assert.Equal(288_000, claim.Amount);
        Assert.Equal(288_000, review.Earned.Value);
        Assert.Empty(review.Unclaimed);
    }

    /// <summary>
    /// The game can write the arrival and receipt in the same second. Arrival
    /// opens the stop's window, so equality belongs to the stop rather than to
    /// the unclaimed list.
    /// </summary>
    [Fact]
    public void A_sale_on_the_same_second_as_arrival_belongs_to_that_stop()
    {
        var review = RunReviewer.Build(
            Run(Stop("s1", "Hurston", Noon.AddHours(1))),
            [Sale(Noon.AddHours(1), "Hurston", 288_000)],
            Noon.AddHours(5))!;

        Assert.Equal(288_000, review.Earned.Value);
        Assert.Single(review.Stops.Single().Claimed);
        Assert.Empty(review.Unclaimed);
    }

    /// <summary>
    /// Money that moved before the run began belongs to whatever came before
    /// it. Counting it would make every run look better than it was.
    /// </summary>
    [Fact]
    public void Money_that_moved_before_the_run_is_not_part_of_it()
    {
        var review = RunReviewer.Build(
            Run(Stop("s1", "Hurston", Noon.AddHours(1))),
            [Sale(Noon.AddHours(-2), "Hurston", 500_000)],
            Noon.AddHours(5))!;

        Assert.Empty(review.Stops.Single().Claimed);
        Assert.Empty(review.Unclaimed);
        Assert.Equal(0, review.Earned.Value);
    }

    /// <summary>
    /// A sale somewhere the run never stopped is real and is shown - just not
    /// attached to a stop that cannot account for it.
    /// </summary>
    [Fact]
    public void Money_that_no_stop_can_account_for_is_listed_rather_than_hidden()
    {
        var review = RunReviewer.Build(
            Run(Stop("s1", "Hurston", Noon.AddHours(1))),
            [Sale(Noon.AddHours(2), "Area18", 120_000)],
            Noon.AddHours(5))!;

        Assert.Empty(review.Stops.Single().Claimed);
        Assert.Single(review.Unclaimed);
        Assert.Equal(0, review.Earned.Value);
        Assert.Contains(review.Earned.Excluded, e => e.Contains("no stop can account for"));
    }

    /// <summary>
    /// A station visited twice in one run keeps its two visits apart, because
    /// the windows run end to end and the time says which one. Without that the
    /// app would be guessing between two stops with the same name.
    /// </summary>
    [Fact]
    public void One_station_visited_twice_keeps_its_visits_apart()
    {
        var review = RunReviewer.Build(
            Run(Stop("s1", "Hurston", Noon.AddHours(1)), Stop("s2", "Hurston", Noon.AddHours(3))),
            [Sale(Noon.AddHours(1).AddMinutes(30), "Hurston", 90_000),
             Sale(Noon.AddHours(3).AddMinutes(30), "Hurston", 40_000)],
            Noon.AddHours(5))!;

        Assert.Equal(90_000, review.Stops[0].Claimed.Single().Amount);
        Assert.Equal(40_000, review.Stops[1].Claimed.Single().Amount);
        Assert.Empty(review.Unclaimed);
    }

    /// <summary>
    /// A stop nobody ticked has no time, so the app has no idea when the player
    /// was there. Claiming by place alone would take a sale from a station
    /// visited twice in one run.
    /// </summary>
    [Fact]
    public void A_stop_that_was_never_ticked_claims_nothing()
    {
        var review = RunReviewer.Build(
            Run(Stop("s1", "Hurston", null)),
            [Sale(Noon.AddHours(2), "Hurston", 288_000)],
            Noon.AddHours(5))!;

        Assert.Empty(review.Stops.Single().Claimed);
        Assert.Single(review.Unclaimed);
    }

    [Fact]
    public void Each_stop_claims_only_its_own_stretch_of_the_run()
    {
        var review = RunReviewer.Build(
            Run(Stop("s1", "Hurston", Noon.AddHours(1)), Stop("s2", "Crusader", Noon.AddHours(3))),
            [Sale(Noon.AddHours(1).AddMinutes(30), "Hurston", 100_000),
             Sale(Noon.AddHours(3).AddMinutes(30), "Crusader", 200_000),

             // At the first stop's place, but after the run had moved on.
             Sale(Noon.AddHours(3).AddMinutes(45), "Hurston", 50_000)],
            Noon.AddHours(5))!;

        Assert.Equal(100_000, review.Stops[0].Claimed.Single().Amount);
        Assert.Equal(200_000, review.Stops[1].Claimed.Single().Amount);
        Assert.Single(review.Unclaimed);
        Assert.Equal(300_000, review.Earned.Value);
    }

    [Fact]
    public void Money_out_is_counted_apart_from_money_in()
    {
        var review = RunReviewer.Build(
            Run(Stop("s1", "Hurston", Noon.AddHours(1))),
            [Sale(Noon.AddHours(1).AddMinutes(10), "Hurston", 288_000),
             Sale(Noon.AddHours(1).AddMinutes(20), "Hurston", -64_000)],
            Noon.AddHours(5))!;

        Assert.Equal(288_000, review.Earned.Value);
        Assert.Equal(64_000, review.Spent.Value);
    }

    /// <summary>
    /// A trade is what the terminal was asked for, never what it confirmed, and
    /// a figure built mostly out of requests has to say so.
    /// </summary>
    [Fact]
    public void A_figure_says_how_much_of_it_was_only_requested()
    {
        var review = RunReviewer.Build(
            Run(Stop("s1", "Hurston", Noon.AddHours(1))),
            [Sale(Noon.AddHours(1).AddMinutes(10), "Hurston", 288_000, confirmed: false),
             Payout(Noon.AddHours(1).AddMinutes(20), "Hurston", 50_250)],
            Noon.AddHours(5))!;

        Assert.Equal(338_250, review.Earned.Value);
        Assert.Contains(review.Earned.Excluded, e => e.Contains("asked for rather than what it"));
    }

    /// <summary>
    /// Every figure carries the inference it rests on, rather than leaving each
    /// page to word it again and word it differently.
    /// </summary>
    [Fact]
    public void Every_figure_states_the_rule_that_made_it()
    {
        var review = RunReviewer.Build(Run(Stop("s1", "Hurston", Noon.AddHours(1))), [], Noon.AddHours(5))!;

        Assert.Contains("back-tracked", review.Earned.Rule);
        Assert.Contains("back-tracked", review.Spent.Rule);
    }

    [Fact]
    public void A_running_review_measures_up_to_now()
    {
        var flying = new Trip("t1", "Ore run", Noon.AddDays(-1),
            [Stop("s1", "Hurston", Noon.AddMinutes(30))], StartedAt: Noon);

        var review = RunReviewer.Build(flying, [Sale(Noon.AddMinutes(40), "Hurston", 10_000)], Noon.AddHours(2))!;

        Assert.Null(review.FinishedAt);
        Assert.Equal(7200, review.ElapsedSeconds);
        Assert.Equal(10_000, review.Earned.Value);
    }

    // ---- corrections ----

    [Fact]
    public void A_correction_sits_beside_the_estimate_rather_than_replacing_it()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qw-corr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var trips = new TripStore(root);
            var trip = trips.Add("Ore run");
            // The store gives a stop its own id, so it has to be read back
            // rather than assumed from what was handed in.
            trips.AddStop(new TripStop("", "Stanton1", "Hurston", null, false, null));
            var stopId = trips.All().Single().Stops.Single().Id;

            trips.AddAction(trip.Id, stopId, "sell", "96 SCU Agricium", 96, "SCU");

            var action = trips.All().Single().Stops.Single().Actions!.Single();

            Assert.True(trips.Correct(trip.Id, stopId, action.Id, 88));

            var corrected = trips.All().Single().Stops.Single().Actions!.Single();

            Assert.Equal(96, corrected.Quantity);
            Assert.Equal(88, corrected.Actual);

            // Clearing is not the same as correcting to zero.
            Assert.True(trips.Correct(trip.Id, stopId, action.Id, null));
            Assert.Null(trips.All().Single().Stops.Single().Actions!.Single().Actual);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
