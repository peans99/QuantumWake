using System.Text.Json;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The six faults found reviewing backup and restore, each held down by the
/// case that showed it.
/// </summary>
/// <remarks>
/// Five of them are the same shape: the app reporting one thing while doing
/// another. A restore that says it worked over a write that failed, that says
/// nothing happened after half of it happened, that promises to leave pins
/// alone and clears them. Every one was green under the tests that existed,
/// because those tests asked what the code returned rather than what the disk
/// and the page ended up holding.
/// </remarks>
public class RestoreReviewFixTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-rfix-{Guid.NewGuid():N}");

    public RestoreReviewFixTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static readonly DateTimeOffset Old = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Newer = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private RestoreService Service() => new(
        new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
        new MiningLogStore(_root), new MapNoteStore(_root), new GoalStore(_root),
        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new LogLibrary(new SessionStore(":memory:")), new KitStore(_root), new ScreenReadingStore(_root));

    private static Job AJob(string id, string title, DateTimeOffset changed) =>
        new(id, title, "list", null, Old, false, [], ModifiedAt: changed);

    // ---- 3: view state the preview promised to leave alone ----

    /// <summary>
    /// The preview says pinned jobs are left alone either way. A backup strips
    /// Pinned on the way out, so taking the record verbatim silently unpins
    /// whatever it replaced - and the screen had just promised it would not.
    /// </summary>
    [Fact]
    public void Replacing_a_pinned_job_leaves_it_pinned()
    {
        var jobs = new JobStore(_root);
        jobs.Put(AJob("j1", "Buy armour", Old));
        jobs.TogglePin("j1");

        var service = Service();
        var file = new ExportBackup([AJob("j1", "Buy better armour", Newer)], [], [], [], [], [], []);

        service.Apply(file, service.Plan(file, "h"), "h", new RestoreChoices());

        var after = new JobStore(_root).All().Single();

        Assert.Equal("Buy better armour", after.Title);
        Assert.True(after.Pinned);
    }

    [Fact]
    public void Replacing_the_tracked_plan_leaves_it_tracked()
    {
        var trips = new TripStore(_root);

        // Adding already takes the tracking - Track() toggles, so calling it
        // here would turn it straight back off.
        var trip = trips.Add("Ore run");
        Assert.True(trips.All().Single().Tracked);

        var service = Service();
        var file = new ExportBackup([], [], [trip with { Title = "Ore run v2", ModifiedAt = Newer, Tracked = false }],
            [], [], [], []);

        // Taken explicitly: the local copy is newer, so this is a conflict and
        // the safe default is to keep it. What is under test is what happens to
        // the tracking when the overwrite is actually asked for.
        service.Apply(file, service.Plan(file, "h"), "h",
            new RestoreChoices(Take: [$"trips:{trip.Id}"]));

        var after = new TripStore(_root).All().Single();

        Assert.Equal("Ore run v2", after.Title);
        Assert.True(after.Tracked);
    }

    // ---- 4: deletions the file remembers ----

    /// <summary>
    /// A fresh machine has no tombstones, so an older backup walks every
    /// deleted record back in with nothing to say it was ever thrown away.
    /// </summary>
    [Fact]
    public void A_restore_takes_on_the_deletions_the_file_remembers()
    {
        var service = Service();
        var file = new ExportBackup([], [], [], [], [],
            [new Tombstone(TombstoneStore.Kinds.Jobs, "gone", Old)], []);

        service.Apply(file, service.Plan(file, "h"), "h", new RestoreChoices());

        var stone = Assert.Single(new TombstoneStore(_root).All());
        Assert.Equal("gone", stone.Id);
    }

    /// <summary>
    /// But never for a record that is here. It exists, no line proposed
    /// removing it, and a restore that deletes something it did not mention is
    /// the failure this feature was built to prevent.
    /// </summary>
    [Fact]
    public void A_deletion_is_not_taken_on_for_a_record_that_is_still_here()
    {
        new JobStore(_root).Put(AJob("j1", "Buy armour", Old));

        var service = Service();
        var file = new ExportBackup([], [], [], [], [],
            [new Tombstone(TombstoneStore.Kinds.Jobs, "j1", Old)], []);

        service.Apply(file, service.Plan(file, "h"), "h", new RestoreChoices());

        Assert.Empty(new TombstoneStore(_root).All());
        Assert.Single(new JobStore(_root).All());
    }

    // ---- 5: the wipe line the rest of the app counts against ----

    /// <summary>
    /// The library holds its own copy and every page counts against it, so
    /// setting only the store moves the line on the settings screen and nowhere
    /// else - every total keeps using the old cutoff.
    /// </summary>
    [Fact]
    public void A_restored_wipe_line_reaches_the_running_app()
    {
        using var sessions = new SessionStore(":memory:");
        var library = new LogLibrary(sessions);

        var service = new RestoreService(
            new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
            new MiningLogStore(_root), new MapNoteStore(_root), new GoalStore(_root),
            new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root), library,
            new KitStore(_root), new ScreenReadingStore(_root));

        var wiped = new Wipe(Newer, "Alpha 4.9", WipeScope.Money);
        var file = new ExportBackup([], [], [], [], [], [], [], Wipe: wiped);

        // A wipe line always differs from the one already set, so it arrives as
        // a conflict and is kept by default. Asked for explicitly here.
        service.Apply(file, service.Plan(file, "h"), "h", new RestoreChoices(Take: ["wipe:wipe"]));

        Assert.NotNull(library.Wipe);
        Assert.Equal(Newer, library.Wipe.At);
        Assert.Equal(WipeScope.Money, library.Wipe.Scope);
    }

    // ---- 1 and 2: a write that refuses ----

    /// <summary>
    /// A half-restore is not a smaller restore. Reporting one as a count of
    /// what happened to land first sends somebody away believing it worked.
    /// </summary>
    [Fact]
    public void A_write_that_refuses_undoes_the_rest_and_says_it_failed()
    {
        var jobs = new JobStore(_root);
        jobs.Put(AJob("j1", "Mine", Old));

        var service = Service();

        var file = new ExportBackup(
            [AJob("j1", "Theirs", Newer)], [], [], [], [], [], []);

        var plan = service.Plan(file, "h");

        // The mining store's file is held open, so its write refuses partway
        // through - after the job above has already been replaced.
        var mining = Path.Combine(_root, "mining-log.json");
        File.WriteAllText(mining, "[]");

        var withRuns = file with
        {
            MiningRuns = [new MiningRun("m1", Old, "Daymar", "Quantainium", 32, null, null, null)],
        };

        var planWithRuns = service.Plan(withRuns, "h");

        using (new FileStream(mining, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = service.Apply(withRuns, planWithRuns, "h", new RestoreChoices());

            Assert.NotNull(result);
            Assert.True(result.Failed);
            Assert.Equal(0, result.Restored);
        }

        // And the job it had already replaced is back as it was.
        Assert.Equal("Mine", new JobStore(_root).All().Single().Title);
    }

    /// <summary>
    /// The mining store swallows write failures on purpose - losing a typed
    /// haul costs the newest entry and never the file. That bargain is wrong
    /// for a restore, which would otherwise count a write that never landed.
    /// </summary>
    [Fact]
    public void A_mining_write_that_refuses_is_not_counted_as_restored()
    {
        var path = Path.Combine(_root, "mining-log.json");
        File.WriteAllText(path, "[]");

        var runs = new MiningLogStore(_root);

        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.ThrowsAny<IOException>(() =>
            runs.Put(new MiningRun("m1", Old, "Daymar", "Quantainium", 32, null, null, null)));
    }

    // ---- 6: the wire format the page reads ----

    /// <summary>
    /// The server writes enum names as they are spelled in C#; the page was
    /// reading lower case. Unchanged records became selectable and the reason
    /// column showed a raw enum name.
    /// </summary>
    /// <remarks>
    /// Asserted on the serialised text rather than on the record, because that
    /// is the only place the two sides can be seen disagreeing - both halves
    /// were internally consistent and both suites were green.
    /// </remarks>
    [Fact]
    public void The_plan_goes_over_the_wire_with_the_names_the_page_reads()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

        var plan = new RestorePlan("h",
            [new RestoreLine("jobs", "j1", "Buy armour", RestoreAction.Same, null, null),
             new RestoreLine("jobs", "j2", "Buy a gun", RestoreAction.Conflict, null, null)], 0);

        var json = JsonSerializer.Serialize(plan, options);

        Assert.Contains("\"action\":\"Same\"", json);
        Assert.Contains("\"action\":\"Conflict\"", json);
        Assert.Contains("\"takenByDefault\":false", json);
    }
}
