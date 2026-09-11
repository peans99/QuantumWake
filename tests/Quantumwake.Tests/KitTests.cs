using Quantumwake.Data;
using Quantumwake.Core.State;

namespace Quantumwake.Tests;

/// <summary>
/// Saved kits, and the one fact that shapes the whole feature.
/// </summary>
/// <remarks>
/// The game logs an item being seen in a container and never one being taken
/// out or used up, so anything ever stashed looks present for ever. A kit that
/// treated a sighting as a holding would report a full loadout to somebody
/// standing in an empty hangar - and would look completely correct doing it.
/// Everything here is about refusing to make that claim.
/// </remarks>
public class KitTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-kits-{Guid.NewGuid():N}");

    public KitTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A real empty stats object with only the two lists a kit reads.
    /// </summary>
    /// <remarks>
    /// Built from an empty library rather than hand-rolled: LibraryStats has a
    /// dozen required members that have nothing to do with kits, and listing
    /// them here would be a second thing to update every time one is added.
    /// </remarks>
    private LibraryStats Stats(
        IReadOnlyList<LoadoutSlot>? loadout = null, IReadOnlyList<StashLocation>? stash = null)
    {
        using var sessions = new SessionStore(":memory:");

        return new LogLibrary(sessions).Stats() with
        {
            Loadout = loadout ?? [],
            Stash = stash ?? [],
        };
    }

    private static LoadoutSlot Wearing(string name, DateTimeOffset seen) =>
        new("armour", "Armour", "Armour", 1, [new LoadoutEntry(name, 1, seen)], seen);

    private static StashLocation Stored(string name, string place, DateTimeOffset seen) =>
        new("place-id", place, seen, 1, [new ItemGroup("Gear", [new StashItem(name, seen)])]);

    private static Kit AKit(params KitItem[] items) =>
        new("k1", "Bounty kit", items, Now.AddDays(-60));

    [Fact]
    public void Something_you_are_wearing_is_not_a_question()
    {
        var prepared = KitPreparer.Prepare(
            AKit(new KitItem("Pembroke helmet")),
            Stats(loadout: [Wearing("Pembroke helmet", Now.AddHours(-2))]),
            Now);

        var line = Assert.Single(prepared.Lines);

        Assert.Equal(KitHolding.Equipped, line.Holding);
        Assert.False(line.NeedsAsking);
        Assert.Equal(0, prepared.ToConfirm);
    }

    /// <summary>
    /// The heart of it. A stashed item is a sighting, and no amount of
    /// recency turns a sighting into a stock level.
    /// </summary>
    [Fact]
    public void Something_seen_in_a_stash_is_always_a_question()
    {
        var prepared = KitPreparer.Prepare(
            AKit(new KitItem("MedPen")),
            Stats(stash: [Stored("MedPen", "Port Tressler", Now.AddHours(-3))]),
            Now);

        var line = Assert.Single(prepared.Lines);

        Assert.Equal(KitHolding.Seen, line.Holding);
        Assert.True(line.NeedsAsking);
        Assert.Equal("Port Tressler", line.Where);
    }

    /// <summary>
    /// Age changes how loudly the page doubts a sighting, never whether it asks.
    /// A stash checked this morning and one checked last spring must not read
    /// identically.
    /// </summary>
    [Fact]
    public void An_old_sighting_is_marked_stale_and_still_asked_about()
    {
        var prepared = KitPreparer.Prepare(
            AKit(new KitItem("MedPen")),
            Stats(stash: [Stored("MedPen", "Port Tressler", Now.AddDays(-KitPreparer.StaleAfterDays - 1))]),
            Now);

        var line = Assert.Single(prepared.Lines);

        Assert.Equal(KitHolding.Stale, line.Holding);
        Assert.True(line.NeedsAsking);
    }

    /// <summary>
    /// "Never seen" is the one negative the logs support. It is not "you do not
    /// have it" - it is "nothing here has ever seen it" - and it is the only
    /// line that needs no asking and still goes on the list.
    /// </summary>
    [Fact]
    public void Something_never_seen_anywhere_is_simply_missing()
    {
        var prepared = KitPreparer.Prepare(AKit(new KitItem("Railgun")), Stats(), Now);

        var line = Assert.Single(prepared.Lines);

        Assert.Equal(KitHolding.Missing, line.Holding);
        Assert.False(line.NeedsAsking);
        Assert.Equal(1, prepared.Missing);
    }

    /// <summary>
    /// A loadout is what the game said was on the character; a stash entry is a
    /// sighting in a container it may have left since. Worn wins.
    /// </summary>
    [Fact]
    public void Worn_beats_stored_when_both_have_seen_it()
    {
        var prepared = KitPreparer.Prepare(
            AKit(new KitItem("Pembroke helmet")),
            Stats(loadout: [Wearing("Pembroke helmet", Now.AddHours(-1))],
                  stash: [Stored("Pembroke helmet", "Port Tressler", Now.AddDays(-2))]),
            Now);

        Assert.Equal(KitHolding.Equipped, Assert.Single(prepared.Lines).Holding);
    }

    [Fact]
    public void The_newest_sighting_is_the_one_worth_asking_about()
    {
        var prepared = KitPreparer.Prepare(
            AKit(new KitItem("MedPen")),
            Stats(stash: [
                Stored("MedPen", "Old locker", Now.AddDays(-90)),
                Stored("MedPen", "New locker", Now.AddDays(-1))]),
            Now);

        var line = Assert.Single(prepared.Lines);

        Assert.Equal("New locker", line.Where);
        Assert.Equal(KitHolding.Seen, line.Holding);
    }

    [Fact]
    public void Every_preparation_states_the_rule_it_rests_on()
    {
        var prepared = KitPreparer.Prepare(AKit(new KitItem("MedPen")), Stats(), Now);

        Assert.Contains("never one being taken out", prepared.Rule);
    }

    // ---- what to shop for ----

    [Fact]
    public void Shopping_takes_what_was_never_seen()
    {
        var prepared = KitPreparer.Prepare(AKit(new KitItem("Railgun")), Stats(), Now);

        Assert.Single(KitPreparer.Shopping(prepared, []));
    }

    /// <summary>
    /// A sighting nobody has spoken about is left as held. The cost of that
    /// being wrong is a wasted trip; the cost of the other direction is buying
    /// something you already own.
    /// </summary>
    [Fact]
    public void A_sighting_nobody_answered_about_is_left_alone()
    {
        var prepared = KitPreparer.Prepare(
            AKit(new KitItem("MedPen")),
            Stats(stash: [Stored("MedPen", "Port Tressler", Now.AddDays(-2))]),
            Now);

        Assert.Empty(KitPreparer.Shopping(prepared, []));
    }

    [Fact]
    public void Saying_a_sighting_is_gone_puts_it_on_the_list()
    {
        var prepared = KitPreparer.Prepare(
            AKit(new KitItem("MedPen")),
            Stats(stash: [Stored("MedPen", "Port Tressler", Now.AddDays(-2))]),
            Now);

        Assert.Single(KitPreparer.Shopping(prepared, ["MedPen"]));
    }

    /// <summary>
    /// A kit is a core of a few things plus preferences, and a list that cannot
    /// tell them apart stops being read the first time it makes a fuss over a
    /// spare undersuit.
    /// </summary>
    [Fact]
    public void An_optional_line_says_it_is_optional()
    {
        var prepared = KitPreparer.Prepare(
            AKit(new KitItem("Spare undersuit", 1, Optional: true)), Stats(), Now);

        Assert.True(Assert.Single(prepared.Lines).Optional);
    }

    // ---- the store ----

    [Fact]
    public void A_kit_survives_a_restart_and_carries_a_change_time()
    {
        new KitStore(_root).Add("Bounty kit", [new KitItem("MedPen", 4)]);

        var stored = new KitStore(_root).All().Single();

        Assert.Equal("Bounty kit", stored.Name);
        Assert.Equal(4, stored.Items.Single().Quantity);
        Assert.NotNull(stored.ModifiedAt);
    }

    [Fact]
    public void An_absurd_quantity_is_brought_into_range_rather_than_refused()
    {
        var kit = new KitStore(_root).Add("Bounty kit", [new KitItem("MedPen", 9999)]);

        Assert.Equal(999, kit.Items.Single().Quantity);
    }

    [Fact]
    public void A_line_with_no_name_is_dropped()
    {
        var kit = new KitStore(_root).Add("Bounty kit", [new KitItem("  "), new KitItem("MedPen")]);

        Assert.Equal("MedPen", kit.Items.Single().Name);
    }

    /// <summary>
    /// A store added after the backup existed has to join it, or a backup
    /// quietly stops being complete - which is the one thing it promises.
    /// </summary>
    [Fact]
    public void Kits_are_in_the_backup()
    {
        new KitStore(_root).Add("Bounty kit", [new KitItem("MedPen")]);

        var backup = new BackupBuilder(
            new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
            new MiningLogStore(_root), new MapNoteStore(_root), new GoalStore(_root),
            new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
            new KitStore(_root), new ScreenReadingStore(_root));

        Assert.Single(backup.Build(new ExportProducer("Quantumwake", "0.9.41"), Now).Backup!.Kits);
        Assert.Equal(1, backup.Preview().Kits);
    }
}
