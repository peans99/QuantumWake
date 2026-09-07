using Quantumwake.Core.State;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// The serious half of the third review: six places where the app said
/// something that was not so.
/// </summary>
/// <remarks>
/// Two of these are worse than bugs. The kit feature was built on a reading of
/// LibraryStats.Loadout that was wrong - it is the latest occupant of each port
/// across the whole library, not what is worn now - so "equipped is settled"
/// was a claim the data could not support. And a search hit from the reader's
/// own logs opened a drawer that closed itself, which is the group most likely
/// to be clicked.
/// </remarks>
public class ThirdReviewFixTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-fix3-{Guid.NewGuid():N}");

    private readonly SessionStore _sessions = new(":memory:");

    public ThirdReviewFixTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        _sessions.Dispose();
        Directory.Delete(_root, true);
    }

    private static readonly DateTimeOffset Noon = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    // ---- 2: a hit from your own logs opens the same drawer as the catalogue ----

    /// <summary>
    /// Saves a session carrying one stashed item, which is how Stats() - and
    /// so Search - comes to know about it.
    /// </summary>
    private void Stashed(string itemClass)
    {
        _sessions.Save(
            new SessionSummary
            {
                Id = "s1",
                SourceFile = "s1.log",
                StartedAt = Noon.AddHours(-1),
                EndedAt = Noon,
                Handle = "nekron",
                Stash = [new StashEntry(Noon, "place-id", "Port Tressler", itemClass)],
            },
            "fingerprint:s1");
    }

    /// <summary>
    /// Hits from one group only. The catalogue group answers from whatever
    /// install this runs on, so a test that swept every group would pass or
    /// fail on the machine rather than on the code.
    /// </summary>
    private IReadOnlyList<SearchHit> Hits(string q, string source) =>
        Search.Run(q, new LogLibrary(_sessions),
                new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
                new KitStore(_root), new MapNoteStore(_root))
            .Groups.Single(group => group.Source == source).Hits;

    /// <summary>
    /// The stash reports what a locker lists and the catalogue reports a class.
    /// A drawer that understood only one of them closed silently on the other,
    /// and it was the reader's own group that failed - the one most likely to
    /// be clicked.
    /// </summary>
    [Fact]
    public void A_hit_from_your_own_logs_carries_the_class_the_stash_knows()
    {
        Stashed("medpen_01");

        var hit = Assert.Single(Hits("medpen", Search.Sources.Yours));

        Assert.Equal("part", hit.Kind);
        Assert.Equal("medpen_01", hit.Id);
        Assert.Contains("seen in storage", hit.Why);
    }

    // ---- 3: a share file is not a backup from the future ----

    /// <summary>
    /// ContentVersion counts the share format, which is already past the backup
    /// format's number - so a share written by this very build failed the
    /// version check and told the reader to update an app that was current.
    /// </summary>
    [Fact]
    public void A_share_file_from_this_build_is_named_as_a_share()
    {
        var share = new ExportFile(
            ExportDocument.Format, ExportDocument.FormatVersion, ExportDocument.ContentVersion,
            DateTimeOffset.UtcNow, new ExportProducer("Quantumwake", "0.9.50"),
            [ExportDocument.Authored],
            Authored: new ExportAuthored(null, null, [], [], []));

        var (contents, _, problem) = BackupReader.Read(
            System.Text.Json.JsonSerializer.Serialize(share, ExportDocument.Json));

        Assert.Null(contents);
        Assert.Contains("shared export", problem!.Message);
        Assert.DoesNotContain("Update Quantum Wake", problem.Message);
    }

    /// <summary>A backup that really is from the future still says so.</summary>
    [Fact]
    public void A_backup_from_a_newer_build_still_asks_for_an_update()
    {
        var future = new ExportFile(
            ExportDocument.Format, ExportDocument.FormatVersion, BackupBuilder.Version + 1,
            DateTimeOffset.UtcNow, new ExportProducer("Quantumwake", "9.9.9"),
            [ExportDocument.Backup],
            Backup: new ExportBackup([], [], [], [], [], [], []));

        var (_, _, problem) = BackupReader.Read(
            System.Text.Json.JsonSerializer.Serialize(future, ExportDocument.Json));

        Assert.Contains("Update Quantum Wake", problem!.Message);
    }

    // ---- 4: a stop must not land on a run that is already filed ----

    /// <summary>
    /// Trip.Done is false whenever any stop is outstanding, so a run finished
    /// with one stop unticked still looked like the obvious home for a new
    /// stop - and filed runs are drawn with no stop list, so it landed
    /// somewhere invisible while the call answered success.
    /// </summary>
    [Fact]
    public void A_new_stop_never_lands_on_a_filed_run()
    {
        var trips = new TripStore(_root);

        var finished = trips.Add("Ore run");
        trips.AddStop(new TripStop("", "Stanton1", "Hurston", null, false, null));
        trips.Start(finished.Id, Noon);
        trips.Finish(finished.Id, Noon.AddHours(1));

        // Nothing is tracked and the only run is filed, so this makes a fresh
        // plan rather than reopening a finished one.
        var landed = trips.AddStop(new TripStop("", "Stanton2", "Crusader", null, false, null));

        Assert.NotEqual(finished.Id, landed.Id);
        Assert.Equal(Archived.No, landed.Archived);
        Assert.Single(trips.All().Single(t => t.Id == finished.Id).Stops);
    }

    // ---- 9: the loadout is a sighting too ----

    /// <summary>
    /// LibraryStats.Loadout is the latest occupant of each port across the
    /// whole library, not what is worn now. A helmet nothing has replaced since
    /// March reported as equipped, needed no confirmation, and was dropped from
    /// the shopping list - the exact failure the three-valued answer exists to
    /// prevent.
    /// </summary>
    [Fact]
    public void An_old_loadout_sighting_is_asked_about_like_any_other()
    {
        var stats = new LogLibrary(_sessions).Stats() with
        {
            Loadout = [new LoadoutSlot("armour", "Armour", "Armour", 1,
                [new LoadoutEntry("Pembroke helmet", 1, Noon.AddDays(-KitPreparer.StaleAfterDays - 1))],
                Noon)],
        };

        var kit = new Kit("k1", "Bounty kit", [new KitItem("Pembroke helmet")], Noon);
        var line = Assert.Single(KitPreparer.Prepare(kit, stats, Noon).Lines);

        Assert.Equal(KitHolding.Stale, line.Holding);
        Assert.True(line.NeedsAsking);
    }

    [Fact]
    public void Something_seen_on_you_recently_is_still_settled()
    {
        var stats = new LogLibrary(_sessions).Stats() with
        {
            Loadout = [new LoadoutSlot("armour", "Armour", "Armour", 1,
                [new LoadoutEntry("Pembroke helmet", 1, Noon.AddHours(-2))], Noon)],
        };

        var kit = new Kit("k1", "Bounty kit", [new KitItem("Pembroke helmet")], Noon);
        var line = Assert.Single(KitPreparer.Prepare(kit, stats, Noon).Lines);

        Assert.Equal(KitHolding.Equipped, line.Holding);
        Assert.False(line.NeedsAsking);
    }

    /// <summary>
    /// And the page says why it is asking, so the questions read as care rather
    /// than fussiness.
    /// </summary>
    [Fact]
    public void The_rule_now_names_the_loadout_as_well_as_the_stash()
    {
        var prepared = KitPreparer.Prepare(
            new Kit("k1", "Bounty kit", [], Noon), new LogLibrary(_sessions).Stats(), Noon);

        Assert.Contains("last thing seen in each slot", prepared.Rule);
    }

    /// <summary>Being short still beats being old: a shortfall is a fact, not a question.</summary>
    [Fact]
    public void Not_enough_of_something_is_still_simply_missing()
    {
        var stats = new LogLibrary(_sessions).Stats() with
        {
            Loadout = [new LoadoutSlot("meds", "Medical", "Medical", 1,
                [new LoadoutEntry("MedPen", 1, Noon.AddDays(-400))], Noon)],
        };

        var line = Assert.Single(KitPreparer.Prepare(
            new Kit("k1", "Bounty kit", [new KitItem("MedPen", 4)], Noon), stats, Noon).Lines);

        Assert.Equal(KitHolding.Missing, line.Holding);
        Assert.False(line.NeedsAsking);
    }
}
