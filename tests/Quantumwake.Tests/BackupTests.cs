using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The backup file, and the two things underneath it that make a restore
/// possible: knowing when a record changed, and knowing what was deleted.
/// </summary>
/// <remarks>
/// What is being defended here is not the file format. It is that a backup is
/// <em>complete</em> — the existing share export carries three of the ten
/// authored stores, which is right for sending to somebody else and wrong for
/// getting a machine back. A store added later and forgotten here would fail
/// silently, and the person who finds out is the one who has already lost the
/// folder.
/// </remarks>
public class BackupTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-backup-{Guid.NewGuid():N}");

    public BackupTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private BackupBuilder Builder() => new(
        new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
        new MiningLogStore(_root), new MapNoteStore(_root), new GoalStore(_root),
        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new KitStore(_root));

    private static ExportProducer Producer() => new("Quantumwake", "0.9.33");

    [Fact]
    public void A_backup_carries_every_authored_store()
    {
        new JobStore(_root).Add("Craft a Hornet", "craft", null, [new JobItem("Tungsten", 40)]);
        new ChecklistStore(_root).Add("Before undock");
        new TripStore(_root).Add("Ore run");
        new MiningLogStore(_root).Add("Daymar", "Quantainium", 32, 3, null, null);
        new MapNoteStore(_root).Add("Stanton1", "Hurston", "Good rocks", null, ["mining"]);
        new GoalStore(_root).Save(new Goal("Corsair", 24_000_000, DateTimeOffset.UtcNow));

        var backup = Builder().Build(Producer(), DateTimeOffset.UtcNow).Backup;

        Assert.NotNull(backup);
        Assert.Single(backup.Jobs);
        Assert.Single(backup.Checklists);
        Assert.Single(backup.Trips);
        Assert.Single(backup.MiningRuns);
        Assert.Single(backup.Notes);
        Assert.NotNull(backup.Goal);
    }

    /// <summary>
    /// The share export drops these deliberately and so does this — but for the
    /// same reason, not a different one: they are this machine's view state,
    /// and a restored file must not fight the reader's own pin.
    /// </summary>
    [Fact]
    public void This_machines_view_state_stays_behind()
    {
        var jobs = new JobStore(_root);
        var job = jobs.Add("Craft a Hornet", "craft", null, []);
        jobs.TogglePin(job.Id);

        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run");
        trips.Track(trip.Id);

        var backup = Builder().Build(Producer(), DateTimeOffset.UtcNow).Backup!;

        Assert.False(backup.Jobs[0].Pinned);
        Assert.False(backup.Trips[0].Tracked);
    }

    /// <summary>
    /// Nothing observed belongs in the file. Sessions and receipts come back by
    /// reading the logs again, and a copy here would be a second version that
    /// can disagree with the game.
    /// </summary>
    [Fact]
    public void A_backup_claims_only_to_hold_authored_work()
    {
        var file = Builder().Build(Producer(), DateTimeOffset.UtcNow);

        Assert.Equal([ExportDocument.Backup], file.Classes);
        Assert.Null(file.Receipts);
        Assert.Null(file.Blueprints);
    }

    [Fact]
    public void The_preview_counts_what_the_file_would_hold()
    {
        new TripStore(_root).Add("Ore run");
        new TripStore(_root).Add("Second run");

        var counts = Builder().Preview();

        Assert.Equal(2, counts.Trips);
        Assert.False(counts.Goal);
    }

    /// <summary>
    /// A deletion is a fact worth carrying. Without it a restore hands back
    /// everything you got rid of, and cannot even tell you it is doing so.
    /// </summary>
    [Fact]
    public void What_was_deleted_travels_with_the_backup()
    {
        var deleted = new TombstoneStore(_root);
        deleted.Record(TombstoneStore.Kinds.Trips, "trip-1");

        var stone = Assert.Single(Builder().Build(Producer(), DateTimeOffset.UtcNow).Backup!.Deleted);

        Assert.Equal(TombstoneStore.Kinds.Trips, stone.Store);
        Assert.Equal("trip-1", stone.Id);
    }

    /// <summary>Ids repeat across stores, so a tombstone is only meaningful with one.</summary>
    [Fact]
    public void Tombstones_are_scoped_to_their_store()
    {
        var deleted = new TombstoneStore(_root);
        deleted.Record(TombstoneStore.Kinds.Trips, "same-id");
        deleted.Record(TombstoneStore.Kinds.Jobs, "same-id");

        Assert.Equal(2, deleted.All().Count);
    }

    /// <summary>
    /// Deleting the same thing twice must not make it look newer than a backup
    /// that still carries it.
    /// </summary>
    [Fact]
    public void Deleting_twice_keeps_the_first_time()
    {
        var deleted = new TombstoneStore(_root);
        deleted.Record(TombstoneStore.Kinds.Jobs, "job-1");
        var first = deleted.All()[0].At;

        deleted.Record(TombstoneStore.Kinds.Jobs, "job-1");

        Assert.Single(deleted.All());
        Assert.Equal(first, deleted.All()[0].At);
    }

    [Fact]
    public void Taking_a_record_back_forgets_that_it_was_deleted()
    {
        var deleted = new TombstoneStore(_root);
        deleted.Record(TombstoneStore.Kinds.Jobs, "job-1");
        deleted.Forget(TombstoneStore.Kinds.Jobs, "job-1");

        Assert.Empty(deleted.All());
    }

    [Fact]
    public void Tombstones_survive_a_restart()
    {
        new TombstoneStore(_root).Record(TombstoneStore.Kinds.Notes, "note-1");

        Assert.Single(new TombstoneStore(_root).All());
    }
}
