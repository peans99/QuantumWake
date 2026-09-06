using System.Text.Json;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Restoring a backup: what it says it will do, and that it does only that.
/// </summary>
/// <remarks>
/// This is the one feature in the app that writes over work somebody did. So
/// the tests worth having are not "does it restore" - they are the four ways it
/// could quietly destroy something: handing back records that were deleted on
/// purpose, overwriting newer work with older, doing something the preview did
/// not mention, and applying a plan that was approved for a different file.
/// </remarks>
public class RestoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-restore-{Guid.NewGuid():N}");

    public RestoreTests() => Directory.CreateDirectory(_root);
    public void Dispose() { _sessions.Dispose(); Directory.Delete(_root, true); }

    private static readonly DateTimeOffset Old = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Newer = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly SessionStore _sessions = new(":memory:");

    private RestoreService Service() => new(
        new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
        new MiningLogStore(_root), new MapNoteStore(_root), new GoalStore(_root),
        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new LogLibrary(_sessions), new KitStore(_root));

    private static ExportBackup Backup(params Job[] jobs) =>
        new(jobs, [], [], [], [], [], [], null, null, null);

    private static Job AJob(string id, string title, DateTimeOffset changed) =>
        new(id, title, "list", null, Old, false, [], ModifiedAt: changed);

    [Fact]
    public void Something_not_here_comes_back()
    {
        var plan = Service().Plan(Backup(AJob("j1", "Buy armour", Newer)), "hash");

        var line = Assert.Single(plan.Lines);
        Assert.Equal(RestoreAction.Add, line.Action);
        Assert.Equal("Buy armour", line.Label);
        Assert.True(line.TakenByDefault);
    }

    /// <summary>
    /// The failure this whole feature exists to prevent: a month-old backup
    /// quietly handing back the plans somebody threw away last week.
    /// </summary>
    [Fact]
    public void Something_deleted_on_purpose_is_not_handed_back()
    {
        new TombstoneStore(_root).Record(TombstoneStore.Kinds.Jobs, "j1");

        var line = Assert.Single(Service().Plan(Backup(AJob("j1", "Buy armour", Newer)), "hash").Lines);

        Assert.Equal(RestoreAction.Deleted, line.Action);
        Assert.False(line.TakenByDefault);
    }

    [Fact]
    public void An_older_copy_here_is_replaced()
    {
        new JobStore(_root).Put(AJob("j1", "Buy armour", Old));

        var line = Assert.Single(Service().Plan(Backup(AJob("j1", "Buy better armour", Newer)), "hash").Lines);

        Assert.Equal(RestoreAction.Replace, line.Action);
        Assert.True(line.TakenByDefault);
    }

    /// <summary>
    /// Newer work here is kept unless somebody says otherwise. There is no
    /// common ancestor to merge from, so the only honest default is the one
    /// that destroys nothing.
    /// </summary>
    [Fact]
    public void Newer_work_here_is_kept_by_default()
    {
        new JobStore(_root).Put(AJob("j1", "Buy armour and a gun", Newer));

        var line = Assert.Single(Service().Plan(Backup(AJob("j1", "Buy armour", Old)), "hash").Lines);

        Assert.Equal(RestoreAction.Conflict, line.Action);
        Assert.False(line.TakenByDefault);
        Assert.Equal(Newer, line.Yours);
        Assert.Equal(Old, line.Theirs);
    }

    [Fact]
    public void An_identical_copy_is_not_a_change()
    {
        new JobStore(_root).Put(AJob("j1", "Buy armour", Old));

        var line = Assert.Single(Service().Plan(Backup(AJob("j1", "Buy armour", Old)), "hash").Lines);

        Assert.Equal(RestoreAction.Same, line.Action);
        Assert.False(line.TakenByDefault);
    }

    /// <summary>
    /// Pinning here and pinning in the file are not a disagreement about
    /// content, so a record that differs only by view state is unchanged.
    /// </summary>
    [Fact]
    public void A_difference_of_pins_alone_is_not_a_change()
    {
        new JobStore(_root).Put(AJob("j1", "Buy armour", Old) with { Pinned = true });

        var line = Assert.Single(Service().Plan(Backup(AJob("j1", "Buy armour", Old)), "hash").Lines);

        Assert.Equal(RestoreAction.Same, line.Action);
    }

    [Fact]
    public void Applying_a_plan_restores_what_it_said_it_would()
    {
        var service = Service();
        var file = Backup(AJob("j1", "Buy armour", Newer));
        var plan = service.Plan(file, "hash");

        var result = service.Apply(file, plan, "hash", new RestoreChoices());

        Assert.NotNull(result);
        Assert.Equal(1, result.Restored);
        Assert.Equal("Buy armour", new JobStore(_root).All().Single().Title);
    }

    /// <summary>
    /// The preview is only a promise if the thing it described is the thing
    /// that runs.
    /// </summary>
    [Fact]
    public void A_plan_approved_for_another_file_is_refused()
    {
        var service = Service();
        var file = Backup(AJob("j1", "Buy armour", Newer));
        var plan = service.Plan(file, "hash-of-the-file-they-saw");

        Assert.Null(service.Apply(file, plan, "hash-of-a-different-file", new RestoreChoices()));
        Assert.Empty(new JobStore(_root).All());
    }

    /// <summary>A conflict runs only when the reader actually asks for it.</summary>
    [Fact]
    public void A_conflict_runs_only_when_it_is_chosen()
    {
        new JobStore(_root).Put(AJob("j1", "Mine", Newer));

        var service = Service();
        var file = Backup(AJob("j1", "Theirs", Old));
        var plan = service.Plan(file, "hash");

        service.Apply(file, plan, "hash", new RestoreChoices());
        Assert.Equal("Mine", new JobStore(_root).All().Single().Title);

        service.Apply(file, plan, "hash", new RestoreChoices(Take: ["jobs:j1"]));
        Assert.Equal("Theirs", new JobStore(_root).All().Single().Title);
    }

    [Fact]
    public void Something_taken_by_default_can_be_left_out()
    {
        var service = Service();
        var file = Backup(AJob("j1", "Buy armour", Newer));
        var plan = service.Plan(file, "hash");

        var result = service.Apply(file, plan, "hash", new RestoreChoices(Leave: ["jobs:j1"]));

        Assert.Equal(0, result!.Restored);
        Assert.Equal(1, result.Skipped);
        Assert.Empty(new JobStore(_root).All());
    }

    /// <summary>
    /// Taking a deleted record back has to clear its tombstone, or the next
    /// preview offers to remove it again - and a preview that keeps asking the
    /// same question is one people stop reading.
    /// </summary>
    [Fact]
    public void Taking_a_deleted_record_back_forgets_the_deletion()
    {
        new TombstoneStore(_root).Record(TombstoneStore.Kinds.Jobs, "j1");

        var service = Service();
        var file = Backup(AJob("j1", "Buy armour", Newer));
        var plan = service.Plan(file, "hash");

        service.Apply(file, plan, "hash", new RestoreChoices(Take: ["jobs:j1"]));

        Assert.Empty(new TombstoneStore(_root).All());
        Assert.Single(new JobStore(_root).All());
    }

    /// <summary>
    /// A restored record keeps the change time it was backed up with. Stamped
    /// "now" it would look freshly edited and win the next conflict against the
    /// machine it came from.
    /// </summary>
    [Fact]
    public void A_restored_record_keeps_the_time_it_was_backed_up_with()
    {
        var service = Service();
        var file = Backup(AJob("j1", "Buy armour", Newer));

        service.Apply(file, service.Plan(file, "hash"), "hash", new RestoreChoices());

        Assert.Equal(Newer, new JobStore(_root).All().Single().ModifiedAt);
    }

    [Fact]
    public void The_preview_says_how_much_view_state_it_is_leaving_alone()
    {
        var jobs = new JobStore(_root);
        var job = jobs.Add("Buy armour", "list", null, []);
        jobs.TogglePin(job.Id);

        Assert.Equal(1, Service().Plan(Backup(), "hash").Ignored);
    }

    // ---- reading the file itself ----

    [Fact]
    public void A_share_export_is_not_mistaken_for_a_backup()
    {
        var share = new ExportFile(ExportDocument.Format, ExportDocument.FormatVersion, 1,
            DateTimeOffset.UtcNow, new ExportProducer("Quantumwake", "0.9.34"),
            [ExportDocument.Authored], Authored: new ExportAuthored(null, null, [], [], []));

        var (contents, _, problem) = BackupReader.Read(JsonSerializer.Serialize(share, ExportDocument.Json));

        Assert.Null(contents);
        Assert.Contains("shared export", problem!.Message);
    }

    [Fact]
    public void A_backup_from_a_newer_build_is_refused_whole()
    {
        var future = new ExportFile(ExportDocument.Format, ExportDocument.FormatVersion,
            BackupBuilder.Version + 1, DateTimeOffset.UtcNow,
            new ExportProducer("Quantumwake", "9.9.9"), [ExportDocument.Backup],
            Backup: new ExportBackup([], [], [], [], [], [], []));

        var (contents, _, problem) = BackupReader.Read(JsonSerializer.Serialize(future, ExportDocument.Json));

        Assert.Null(contents);
        Assert.Contains("does not know how to put back", problem!.Message);
    }

    [Fact]
    public void Two_different_files_do_not_share_a_hash()
    {
        Assert.NotEqual(RestorePlan.HashOf("{\"a\":1}"), RestorePlan.HashOf("{\"a\":2}"));
        Assert.Equal(RestorePlan.HashOf("{\"a\":1}"), RestorePlan.HashOf("{\"a\":1}"));
    }
}
