using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Points of interest in the backup, and back out of it.
/// </summary>
/// <remarks>
/// The plan for points said "in the backup from the first commit", and the
/// first commit did not do it - so the name, category and note on every pin
/// lived only in one file in one folder for two releases. These tests are the
/// rule that keeps the next store from doing the same: a pin goes into the
/// file, comes back whole, and is compared on the same stamp as a job or a kit.
/// </remarks>
public class PinBackupTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-pin-backup-{Guid.NewGuid():N}");

    private static readonly DateTimeOffset At = new(2026, 9, 9, 2, 10, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = At.AddDays(1);

    public PinBackupTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private BackupBuilder Builder() => new(
        new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
        new MiningLogStore(_root), new MapNoteStore(_root), new GoalStore(_root),
        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new KitStore(_root), new ScreenReadingStore(_root));

    private RestoreService Service() => new(
        new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
        new MiningLogStore(_root), new MapNoteStore(_root), new GoalStore(_root),
        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new LogLibrary(new SessionStore(":memory:")), new KitStore(_root), new ScreenReadingStore(_root));

    private static PinnedLocation APin(DateTimeOffset changed, string? note = "Quantanium on the north face") =>
        new(At, At.AddMinutes(2), -9641671346.9, -11490734321.2, -91805.1, 14.99996,
            "Ruin Station", "Pyro", "Ruin mining shelf", "Mining", note, ModifiedAt: changed);

    private static ExportBackup Backup(params PinnedLocation[] pins) =>
        new([], [], [], [], [], [], [], null, null, null, pins);

    [Fact]
    public void Pins_are_in_the_backup_and_the_readings_are_not()
    {
        var store = new ScreenReadingStore(_root);
        store.AddClipboard(new ClipboardSighting(At, 1, 2, 3, 0.1, "Ruin Station", "Pyro"));
        store.Pin(At, "Ruin mining shelf", "Mining");
        store.UpdatePin(At, null, null, "Two rocks left.");

        var backup = Builder().Build(new ExportProducer("Quantumwake", "0.10.38"), Later).Backup!;

        var pin = Assert.Single(backup.Pins!);
        Assert.Equal("Ruin mining shelf", pin.Label);
        Assert.Equal("Two rocks left.", pin.Note);
        Assert.NotNull(pin.ModifiedAt);
        Assert.Equal(1, Builder().Preview().Pins);
    }

    /// <summary>A backup written before points existed has no key for them, and reads as none.</summary>
    [Fact]
    public void A_backup_from_before_points_existed_reads_as_having_none()
    {
        var file = Builder().Build(new ExportProducer("Quantumwake", "0.10.38"), Later);
        var old = file with { Backup = file.Backup! with { Pins = null } };
        var text = System.Text.Json.JsonSerializer.Serialize(old, ExportDocument.Json);
        Assert.DoesNotContain("\"pins\"", text);

        var (contents, _, problem) = BackupReader.Read(text);

        Assert.Null(problem);
        Assert.NotNull(contents!.Pins);
        Assert.Empty(contents.Pins);
    }

    [Fact]
    public void A_pin_not_here_comes_back_with_its_note()
    {
        var service = Service();
        var file = Backup(APin(Later));
        var plan = service.Plan(file, "hash");

        var line = Assert.Single(plan.Lines);
        Assert.Equal(RestoreAction.Add, line.Action);
        Assert.Equal("Ruin mining shelf", line.Label);

        var result = service.Apply(file, plan, "hash", new RestoreChoices());

        Assert.Equal(1, result?.Restored);
        var pin = Assert.Single(new ScreenReadingStore(_root).Pinned());
        Assert.Equal("Quantanium on the north face", pin.Note);
        Assert.Equal(At, pin.SourceAt);
    }

    /// <summary>
    /// The failure the tombstones exist for: a point removed on purpose last
    /// week must not walk back in from a month-old backup unasked.
    /// </summary>
    [Fact]
    public void A_pin_removed_on_purpose_is_not_handed_back()
    {
        new TombstoneStore(_root).Record(TombstoneStore.Kinds.Pins, PinnedLocation.IdFor(At));

        var line = Assert.Single(Service().Plan(Backup(APin(Later)), "hash").Lines);

        Assert.Equal(RestoreAction.Deleted, line.Action);
        Assert.False(line.TakenByDefault);
    }

    [Fact]
    public void A_newer_note_here_is_kept_and_an_older_one_is_replaced()
    {
        var store = new ScreenReadingStore(_root);
        store.PutPin(APin(Later, "the note typed most recently"));

        var older = Assert.Single(Service().Plan(Backup(APin(At, "an older note")), "hash").Lines);
        Assert.Equal(RestoreAction.Conflict, older.Action);

        store.PutPin(APin(At, "an older note"));
        var newer = Assert.Single(Service().Plan(Backup(APin(Later, "the note typed most recently")), "hash").Lines);
        Assert.Equal(RestoreAction.Replace, newer.Action);
    }

    [Fact]
    public void The_same_pin_on_both_sides_is_not_a_change()
    {
        new ScreenReadingStore(_root).PutPin(APin(At));

        var line = Assert.Single(Service().Plan(Backup(APin(Later)), "hash").Lines);

        // Only the stamp differs, and the stamp is not the work.
        Assert.Equal(RestoreAction.Same, line.Action);
    }
}
