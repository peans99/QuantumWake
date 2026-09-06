using System.Text.Json;
using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The seven faults found reviewing dev090, each held down by the case that
/// showed it.
/// </summary>
/// <remarks>
/// Three of them are the same shape as the last review: the app reporting one
/// thing while doing another. The other four are the cost of adding stages and
/// stores to records that already existed - a field added today is null in
/// every file written before today, and a stage derived from two dates says
/// nothing useful about a row that only ever had one of them.
/// </remarks>
public class SecondReviewFixTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-fix2-{Guid.NewGuid():N}");

    private readonly SessionStore _sessions = new(":memory:");

    public SecondReviewFixTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        _sessions.Dispose();
        Directory.Delete(_root, true);
    }

    private static readonly DateTimeOffset Noon = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private RestoreService Service() => new(
        new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
        new MiningLogStore(_root), new MapNoteStore(_root), new GoalStore(_root),
        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new LogLibrary(_sessions), new KitStore(_root));

    // ---- 1: a backup written before a store existed ----

    /// <summary>
    /// Kits arrived in 0.9.41. Every backup taken before it has no key for
    /// them, JSON leaves the list null, and the preview walked straight into
    /// it - so the files most worth restoring were the ones that could not be.
    /// </summary>
    [Fact]
    public void A_backup_from_before_a_store_existed_still_reads()
    {
        var old = """
            {"format":"quantumwake.export","formatVersion":1,"contentVersion":1,
             "exportedAt":"2026-08-01T00:00:00+00:00",
             "producer":{"app":"Quantumwake","version":"0.9.36"},
             "classes":["backup"],
             "backup":{"jobs":[],"checklists":[],"trips":[],"miningRuns":[],
                       "notes":[],"deleted":[]}}
            """;

        var (contents, hash, problem) = BackupReader.Read(old);

        Assert.Null(problem);
        Assert.NotNull(contents);
        Assert.Empty(contents.Kits);

        // And the preview walks it without falling over.
        Assert.Empty(Service().Plan(contents, hash!).Lines);
    }

    /// <summary>Every collection, not only the newest one - the next store added will be the same.</summary>
    [Fact]
    public void A_backup_missing_any_list_reads_as_empty_rather_than_null()
    {
        var sparse = """
            {"format":"quantumwake.export","formatVersion":1,"contentVersion":1,
             "exportedAt":"2026-08-01T00:00:00+00:00",
             "producer":{"app":"Quantumwake","version":"0.9.36"},
             "classes":["backup"],"backup":{}}
            """;

        var (contents, _, problem) = BackupReader.Read(sparse);

        Assert.Null(problem);
        Assert.Empty(contents!.Jobs);
        Assert.Empty(contents.Trips);
        Assert.Empty(contents.Deleted);
        Assert.Empty(contents.Kits);
    }

    // ---- 2: a rollback that leaves something behind ----

    /// <summary>
    /// Putting the photograph back does not take away what the failed run
    /// added. Reporting that as a complete rollback is the same lie the failure
    /// reporting was fixed to stop telling.
    /// </summary>
    [Fact]
    public void A_rollback_takes_back_what_the_failed_run_added()
    {
        var service = Service();

        var file = new ExportBackup(
            [new Job("newcomer", "Buy armour", "list", null, Noon, false, [], ModifiedAt: Noon)],
            [], [], [new MiningRun("m1", Noon, "Daymar", "Quantainium", 32, null, null, null)],
            [], [], []);

        var plan = service.Plan(file, "h");

        var mining = Path.Combine(_root, "mining-log.json");
        File.WriteAllText(mining, "[]");

        using (new FileStream(mining, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = service.Apply(file, plan, "h", new RestoreChoices());

            Assert.True(result!.Failed);
            Assert.True(result.RolledBack);
        }

        // The job the run had already added is gone again.
        Assert.Empty(new JobStore(_root).All());
    }

    // ---- 3: which side of an arrival a stop accounts for ----

    /// <summary>
    /// Landing ticks a stop, so DoneAt is when the player arrived - and
    /// everything they did there happened after it. Running the window up to
    /// DoneAt put every sale in the leg before the stop that earned it.
    /// </summary>
    [Fact]
    public void A_sale_after_arriving_belongs_to_the_stop_arrived_at()
    {
        var trip = new Trip("t1", "Ore run", Noon.AddDays(-1),
            [new TripStop("s1", "Stanton1", "Hurston", null, true, Noon.AddHours(1))],
            StartedAt: Noon, FinishedAt: Noon.AddHours(4));

        var sale = new LedgerEntry(
            Noon.AddHours(1).AddMinutes(15), "Cargo sold", "Agricium", "Hurston", "TDD",
            1_000, 96, true, 0);

        var review = RunReviewer.Build(trip, [sale], Noon.AddHours(5))!;

        Assert.Equal(1_000, review.Earned.Value);
        Assert.Single(review.Stops.Single().Claimed);
        Assert.Empty(review.Unclaimed);
    }

    /// <summary>Two stops still keep their own stretch, now measured forwards.</summary>
    [Fact]
    public void Each_stop_still_only_claims_its_own_stretch()
    {
        var trip = new Trip("t1", "Ore run", Noon.AddDays(-1),
            [new TripStop("s1", "Stanton1", "Hurston", null, true, Noon.AddHours(1)),
             new TripStop("s2", "Stanton2", "Crusader", null, true, Noon.AddHours(3))],
            StartedAt: Noon, FinishedAt: Noon.AddHours(5));

        var review = RunReviewer.Build(trip, [
            new LedgerEntry(Noon.AddHours(2), "Cargo sold", "Ore", "Hurston", "", 100, 1, true, 0),
            new LedgerEntry(Noon.AddHours(4), "Cargo sold", "Ore", "Crusader", "", 200, 1, true, 0),

            // At the first stop's place, but long after the run had moved on.
            new LedgerEntry(Noon.AddHours(4), "Cargo sold", "Ore", "Hurston", "", 50, 1, true, 0),
        ], Noon.AddHours(6))!;

        Assert.Equal(100, review.Stops[0].Claimed.Single().Amount);
        Assert.Equal(200, review.Stops[1].Claimed.Single().Amount);
        Assert.Single(review.Unclaimed);
    }

    // ---- 4: a kit asks for a number of things ----

    /// <summary>
    /// One medpen does not satisfy a kit that wants four, and calling it
    /// settled because the name turned up is how somebody undocks with a
    /// quarter of a kit.
    /// </summary>
    [Fact]
    public void A_kit_wanting_four_is_not_satisfied_by_one()
    {
        var stats = new LogLibrary(_sessions).Stats() with
        {
            Loadout = [new LoadoutSlot("meds", "Medical", "Medical", 1,
                [new LoadoutEntry("MedPen", 1, Noon)], Noon)],
        };

        var kit = new Kit("k1", "Bounty kit", [new KitItem("MedPen", 4)], Noon);
        var prepared = KitPreparer.Prepare(kit, stats, Noon);
        var line = Assert.Single(prepared.Lines);

        Assert.Equal(1, line.Held);
        Assert.Equal(3, line.Short);

        // And the shopping list asks for the shortfall, not for four more.
        Assert.Equal(3, Assert.Single(KitPreparer.Shopping(prepared, [])).Quantity);
    }

    [Fact]
    public void Enough_of_something_is_still_settled()
    {
        var stats = new LogLibrary(_sessions).Stats() with
        {
            Loadout = [new LoadoutSlot("meds", "Medical", "Medical", 1,
                [new LoadoutEntry("MedPen", 4, Noon)], Noon)],
        };

        var prepared = KitPreparer.Prepare(
            new Kit("k1", "Bounty kit", [new KitItem("MedPen", 4)], Noon), stats, Noon);

        Assert.Equal(KitHolding.Equipped, Assert.Single(prepared.Lines).Holding);
        Assert.Empty(KitPreparer.Shopping(prepared, []));
    }

    /// <summary>
    /// A stash entry is one sighting of a name and says nothing about how many
    /// are in the box, so there is no count to be short of.
    /// </summary>
    [Fact]
    public void A_sighting_has_no_count_to_be_short_of()
    {
        var stats = new LogLibrary(_sessions).Stats() with
        {
            Stash = [new StashLocation("p", "Port Tressler", Noon, 1,
                [new ItemGroup("Gear", [new StashItem("MedPen", Noon)])])],
        };

        var line = Assert.Single(KitPreparer.Prepare(
            new Kit("k1", "Bounty kit", [new KitItem("MedPen", 4)], Noon), stats, Noon).Lines);

        Assert.Null(line.Held);
        Assert.Null(line.Short);
        Assert.True(line.NeedsAsking);
    }

    // ---- 5: a repeat is the same route, not the same numbers ----

    [Fact]
    public void Repeating_a_run_does_not_carry_the_last_ones_outcome()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.AddStop(new TripStop("", "Stanton1", "Hurston", null, false, null));

        var stopId = trips.All().Single().Stops.Single().Id;
        trips.AddAction(trip.Id, stopId, "sell", "96 SCU Agricium", 96, "SCU");

        var actionId = trips.All().Single().Stops.Single().Actions!.Single().Id;
        trips.Correct(trip.Id, stopId, actionId, 88);

        var copy = trips.Repeat(trip.Id, Noon)!;
        var copied = copy.Stops.Single().Actions!.Single();

        Assert.Equal(96, copied.Quantity);
        Assert.Null(copied.Actual);
    }

    // ---- 7: a haul with money against it was sold ----

    /// <summary>
    /// Every haul recorded before the stages existed carries revenue and no
    /// SoldAt, and so does anything typed into the log form's own "Sold for"
    /// box. Offering to send those to a refinery is the app arguing with what
    /// it was told.
    /// </summary>
    [Fact]
    public void A_haul_with_money_against_it_reads_as_sold()
    {
        var older = new MiningRun("m1", Noon, "Daymar", "Quantainium", 32, null, 180_000, null);

        Assert.Equal(MiningStage.Sold, older.StageAt(Noon.AddDays(1)));
    }

    [Fact]
    public void A_haul_with_no_money_against_it_is_still_only_extracted()
    {
        var fresh = new MiningRun("m1", Noon, "Daymar", "Quantainium", 32, null, null, null);

        Assert.Equal(MiningStage.Extracted, fresh.StageAt(Noon.AddDays(1)));
    }
}
