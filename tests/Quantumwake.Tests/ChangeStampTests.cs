using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// When an authored record last changed, which a restore needs to settle a
/// conflict and which nothing recorded before now.
/// </summary>
/// <remarks>
/// The rule lives in one place on purpose. Stamping by hand meant 27 call sites
/// across five stores, and a timestamp that is right most of the time is worse
/// than none: it resolves conflicts the wrong way with nothing on screen
/// looking broken. These tests hold the two properties that make it safe —
/// loading is not a change, and neither is saving something identical.
/// </remarks>
public class ChangeStampTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-stamp-{Guid.NewGuid():N}");

    public ChangeStampTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void A_new_record_is_stamped_when_it_is_written()
    {
        var trip = new TripStore(_root).Add("Ore run");

        var stored = new TripStore(_root).All().Single();

        Assert.NotNull(stored.ModifiedAt);
        Assert.Equal(stored.ModifiedAt, stored.ChangedAt);
        Assert.Equal(trip.Id, stored.Id);
    }

    /// <summary>
    /// Reading a file is not editing it. Without a baseline taken at load, the
    /// first save after every restart would stamp the whole store as changed,
    /// and a backup would always look older than the machine that read it.
    /// </summary>
    /// <remarks>
    /// Compared disk-to-disk rather than against the in-memory record: a
    /// timestamp loses precision through JSON, so the two differ by less than a
    /// tick even when nothing has been restamped.
    /// </remarks>
    [Fact]
    public void Loading_a_store_does_not_count_as_changing_it()
    {
        new TripStore(_root).Add("Ore run");
        var written = new TripStore(_root).All().Single().ModifiedAt;

        // A second store loads the same file and writes an unrelated record.
        var second = new TripStore(_root);
        second.Add("Second run");

        var untouched = new TripStore(_root).All().Single(t => t.Title == "Ore run");

        Assert.Equal(written, untouched.ModifiedAt);
    }

    /// <summary>
    /// Toggling a stop and toggling it back leaves the trip as it was found, so
    /// it must not end up looking newer than a backup that agrees with it.
    /// </summary>
    /// <remarks>
    /// Driven through <see cref="ChangeStamp{T}"/> with explicit times rather
    /// than through a store: the clock here is coarser than two consecutive
    /// saves, so a store-level version of this passes and fails by timing.
    /// </remarks>
    [Fact]
    public void A_change_undone_leaves_the_record_where_it_started()
    {
        var made = DateTimeOffset.UtcNow;
        var stamp = new ChangeStamp<Job>(j => System.Text.Json.JsonSerializer.Serialize(j));
        var jobs = new List<Job> { new("j1", "Buy armour", "list", null, made, false, []) };

        stamp.Loaded(jobs);

        jobs[0] = jobs[0] with { Done = true };
        Assert.True(stamp.Apply(jobs, made.AddMinutes(1)));
        var moved = jobs[0].ModifiedAt;

        jobs[0] = jobs[0] with { Done = false };
        Assert.True(stamp.Apply(jobs, made.AddMinutes(2)));
        var undone = jobs[0].ModifiedAt;

        // Back to the loaded content, so the next save is not a change at all.
        Assert.False(stamp.Apply(jobs, made.AddMinutes(3)));
        Assert.Equal(undone, jobs[0].ModifiedAt);
        Assert.NotEqual(moved, undone);
    }

    /// <summary>
    /// An id reused after a delete is a new record, and must not inherit the
    /// old one's comparison and go unstamped.
    /// </summary>
    [Fact]
    public void An_id_reused_after_a_delete_is_treated_as_new()
    {
        var made = DateTimeOffset.UtcNow;
        var stamp = new ChangeStamp<Job>(j => System.Text.Json.JsonSerializer.Serialize(j));
        var jobs = new List<Job> { new("j1", "Buy armour", "list", null, made, false, []) };

        stamp.Loaded(jobs);
        jobs.Clear();
        stamp.Apply(jobs, made.AddMinutes(1));

        jobs.Add(new Job("j1", "Buy armour", "list", null, made, false, []));

        Assert.True(stamp.Apply(jobs, made.AddMinutes(2)));
        Assert.Equal(made.AddMinutes(2), jobs[0].ModifiedAt);
    }

    [Fact]
    public void Editing_one_record_does_not_stamp_its_neighbours()
    {
        var jobs = new JobStore(_root);
        var first = jobs.Add("Craft a Hornet", "craft", null, []);
        var second = jobs.Add("Buy armour", "list", null, []);

        var untouchedBefore = jobs.All().Single(j => j.Id == first.Id).ModifiedAt;

        jobs.Toggle(second.Id);

        Assert.Equal(untouchedBefore, jobs.All().Single(j => j.Id == first.Id).ModifiedAt);
        Assert.NotEqual(untouchedBefore, jobs.All().Single(j => j.Id == second.Id).ModifiedAt);
    }

    /// <summary>
    /// Pinning is this machine's view state and never travels in a backup, so
    /// it must not make a record look edited. Otherwise pinning a job here
    /// would win a restore conflict against the same job edited elsewhere.
    /// </summary>
    [Fact]
    public void Pinning_something_is_not_editing_it()
    {
        var jobs = new JobStore(_root);
        var job = jobs.Add("Craft a Hornet", "craft", null, []);
        var before = jobs.All().Single().ModifiedAt;

        jobs.TogglePin(job.Id);

        Assert.True(jobs.All().Single().Pinned);
        Assert.Equal(before, jobs.All().Single().ModifiedAt);
    }

    /// <summary>
    /// A record written before this existed has no stamp, and must read as old
    /// rather than as now — a restore comparing the two would otherwise treat
    /// every legacy record as the newer side of a conflict.
    /// </summary>
    [Fact]
    public void A_record_with_no_stamp_falls_back_to_when_it_was_written()
    {
        var made = DateTimeOffset.UtcNow.AddDays(-30);
        var job = new Job("j1", "Old job", "list", null, made, false, []);

        Assert.Null(job.ModifiedAt);
        Assert.Equal(made, job.ChangedAt);
    }

    /// <summary>
    /// A haul's date is when it was mined, which is not when the row was last
    /// corrected.
    /// </summary>
    [Fact]
    public void A_mining_run_separates_when_it_happened_from_when_it_was_edited()
    {
        var mined = DateTimeOffset.UtcNow.AddDays(-3);
        var run = new MiningRun("m1", mined, "Daymar", "Quantainium", 32, 3, null, null);

        Assert.Equal(mined, run.ChangedAt);
        Assert.Equal(mined, run.Stamped(DateTimeOffset.UtcNow).At);
        Assert.NotEqual(mined, run.Stamped(DateTimeOffset.UtcNow).ChangedAt);
    }

    /// <summary>
    /// Notes already carried an edit time, so the rule rides on that rather
    /// than adding a second date that could disagree with it.
    /// </summary>
    [Fact]
    public void A_note_keeps_using_the_edit_time_it_already_had()
    {
        var notes = new MapNoteStore(_root);
        var note = notes.Add("Stanton1", "Hurston", "Good rocks", null, ["mining"]);

        var stored = new MapNoteStore(_root).All().Single();

        Assert.Equal(stored.UpdatedAt, stored.ChangedAt);
        Assert.NotEqual(default, stored.UpdatedAt);
        Assert.Equal(note!.Id, stored.Id);
    }
}
