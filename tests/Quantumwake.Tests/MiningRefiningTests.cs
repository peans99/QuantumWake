using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// A haul from the rock to the money, and the wall it stays on one side of.
/// </summary>
/// <remarks>
/// Nothing here can be observed. Game.log records no extraction, no rock
/// scanned, no refinery job and no collection - the only trace mining leaves is
/// ore turning up in a sale that was never a purchase. So every stage is a
/// typed claim, and the tests that matter are the ones keeping those claims
/// from being added to figures the app can actually stand behind.
/// </remarks>
public class MiningRefiningTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-refine-{Guid.NewGuid():N}");

    public MiningRefiningTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static readonly DateTimeOffset Noon = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private MiningRun Haul(MiningLogStore runs) =>
        runs.Add("Daymar", "Quantainium", 32, 3, null, null)!;

    [Fact]
    public void A_fresh_haul_is_only_extracted()
    {
        var runs = new MiningLogStore(_root);

        Assert.Equal(MiningStage.Extracted, Haul(runs).StageAt(Noon));
    }

    [Fact]
    public void Submitting_puts_it_at_a_refinery()
    {
        var runs = new MiningLogStore(_root);
        var run = Haul(runs);

        Assert.True(runs.Submit(run.Id, "ArcCorp 141", "Dinyx Solventation", 4_800, Noon.AddHours(6), Noon));

        var submitted = runs.All().Single();

        Assert.Equal(MiningStage.Submitted, submitted.StageAt(Noon));
        Assert.Equal("Dinyx Solventation", submitted.Refinery!.Method);
        Assert.Equal(4_800, submitted.Refinery.Cost);
    }

    /// <summary>
    /// Ready is about now rather than about the record. Nothing writes down the
    /// moment a refinery finishes, so the only thing the app can do is compare
    /// the pilot's own expectation against the clock.
    /// </summary>
    [Fact]
    public void A_job_becomes_ready_when_the_expected_moment_passes()
    {
        var runs = new MiningLogStore(_root);
        var run = Haul(runs);
        runs.Submit(run.Id, "ArcCorp 141", null, null, Noon.AddHours(6), Noon);

        Assert.Equal(MiningStage.Submitted, runs.All().Single().StageAt(Noon.AddHours(5)));
        Assert.Equal(MiningStage.Ready, runs.All().Single().StageAt(Noon.AddHours(7)));
    }

    /// <summary>
    /// A job with no expected time never claims to be ready. The app has no
    /// idea, and saying "ready" would be inventing one.
    /// </summary>
    [Fact]
    public void A_job_with_no_expected_time_never_says_it_is_ready()
    {
        var runs = new MiningLogStore(_root);
        var run = Haul(runs);
        runs.Submit(run.Id, "ArcCorp 141", null, null, null, Noon);

        Assert.Equal(MiningStage.Submitted, runs.All().Single().StageAt(Noon.AddYears(1)));
    }

    /// <summary>
    /// Less back than went in is normal, and the difference is the number worth
    /// knowing - so both are kept rather than one overwriting the other.
    /// </summary>
    [Fact]
    public void Collecting_keeps_what_went_in_beside_what_came_back()
    {
        var runs = new MiningLogStore(_root);
        var run = Haul(runs);
        runs.Submit(run.Id, "ArcCorp 141", null, null, Noon.AddHours(6), Noon);

        Assert.True(runs.Collect(run.Id, 24, Noon.AddHours(7)));

        var collected = runs.All().Single();

        Assert.Equal(MiningStage.Collected, collected.StageAt(Noon.AddHours(8)));
        Assert.Equal(32, collected.Scu);
        Assert.Equal(24, collected.Refinery!.Yield);
        Assert.Equal(8, collected.Lost);
    }

    /// <summary>
    /// Nothing lost and nothing known yet are different facts, and only one of
    /// them is a number.
    /// </summary>
    [Fact]
    public void Loss_is_unknown_rather_than_zero_before_collection()
    {
        var runs = new MiningLogStore(_root);
        var run = Haul(runs);
        runs.Submit(run.Id, "ArcCorp 141", null, null, Noon.AddHours(6), Noon);

        Assert.Null(runs.All().Single().Lost);
    }

    [Fact]
    public void Selling_records_what_it_made()
    {
        var runs = new MiningLogStore(_root);
        var run = Haul(runs);
        runs.Submit(run.Id, "ArcCorp 141", null, null, Noon.AddHours(6), Noon);
        runs.Collect(run.Id, 24, Noon.AddHours(7));

        Assert.True(runs.Sell(run.Id, 180_000, Noon.AddHours(8)));

        var sold = runs.All().Single();

        Assert.Equal(MiningStage.Sold, sold.StageAt(Noon.AddHours(9)));
        Assert.Equal(180_000, sold.Revenue);
    }

    // ---- stages run one way ----

    [Fact]
    public void A_haul_at_a_refinery_cannot_be_submitted_again()
    {
        var runs = new MiningLogStore(_root);
        var run = Haul(runs);
        runs.Submit(run.Id, "ArcCorp 141", null, null, Noon.AddHours(6), Noon);

        Assert.False(runs.Submit(run.Id, "Everus", null, null, null, Noon.AddHours(1)));
        Assert.Equal("ArcCorp 141", runs.All().Single().Refinery!.Place);
    }

    /// <summary>
    /// Re-submitting a collected haul would quietly discard the yield that came
    /// back the first time.
    /// </summary>
    [Fact]
    public void A_collected_haul_cannot_go_back_to_the_refinery()
    {
        var runs = new MiningLogStore(_root);
        var run = Haul(runs);
        runs.Submit(run.Id, "ArcCorp 141", null, null, Noon.AddHours(6), Noon);
        runs.Collect(run.Id, 24, Noon.AddHours(7));

        Assert.False(runs.Submit(run.Id, "Everus", null, null, null, Noon.AddHours(8)));
        Assert.Equal(24, runs.All().Single().Refinery!.Yield);
    }

    [Fact]
    public void Something_never_submitted_cannot_be_collected_or_sold()
    {
        var runs = new MiningLogStore(_root);
        var run = Haul(runs);

        Assert.False(runs.Collect(run.Id, 24, Noon));
        Assert.False(runs.Sell(run.Id, 100_000, Noon));
    }

    // ---- what is owed right now ----

    [Fact]
    public void Pending_lists_only_what_is_at_a_refinery_soonest_first()
    {
        var runs = new MiningLogStore(_root);

        var waiting = Haul(runs);
        runs.Submit(waiting.Id, "Late refinery", null, null, Noon.AddHours(9), Noon);

        var sooner = runs.Add("Yela", "Taranite", 16, null, null, null)!;
        runs.Submit(sooner.Id, "Soon refinery", null, null, Noon.AddHours(2), Noon);

        var done = runs.Add("Aberdeen", "Bexalite", 8, null, null, null)!;
        runs.Submit(done.Id, "Old refinery", null, null, Noon.AddHours(1), Noon);
        runs.Collect(done.Id, 8, Noon.AddHours(2));

        var pending = runs.Pending(Noon.AddHours(3));

        Assert.Equal(2, pending.Count);
        Assert.Equal("Soon refinery", pending[0].Refinery!.Place);
        Assert.DoesNotContain(pending, r => r.Id == done.Id);
    }

    /// <summary>
    /// The wall. Mining revenue is typed, and the ledger is what the logs saw -
    /// adding one to the other would turn a memory into a receipt.
    /// </summary>
    [Fact]
    public void Mining_revenue_never_reaches_the_ledger()
    {
        using var sessions = new SessionStore(":memory:");
        var library = new LogLibrary(sessions);

        var runs = new MiningLogStore(_root);
        var run = Haul(runs);
        runs.Submit(run.Id, "ArcCorp 141", null, null, Noon.AddHours(6), Noon);
        runs.Collect(run.Id, 24, Noon.AddHours(7));
        runs.Sell(run.Id, 180_000, Noon.AddHours(8));

        Assert.Empty(library.Ledger());
    }

    [Fact]
    public void The_lifecycle_survives_a_restart()
    {
        var run = Haul(new MiningLogStore(_root));

        new MiningLogStore(_root).Submit(run.Id, "ArcCorp 141", "Ferron Exchange", 4_800, Noon.AddHours(6), Noon);
        new MiningLogStore(_root).Collect(run.Id, 24, Noon.AddHours(7));

        var reloaded = new MiningLogStore(_root).All().Single();

        Assert.Equal("Ferron Exchange", reloaded.Refinery!.Method);
        Assert.Equal(24, reloaded.Refinery.Yield);
        Assert.Equal(MiningStage.Collected, reloaded.StageAt(Noon.AddHours(8)));
    }
}
