using Quantumwake.Data;

namespace Quantumwake.Tests;

public sealed class JobStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"qw-jobs-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void Replacing_an_open_list_keeps_one_current_fit()
    {
        var store = new JobStore(_root);
        var first = store.ReplaceOpenList("RSI Hermes fit", "garage:RSI_Hermes",
            [new JobItem("SolarFlare", 2)], "Terra", "P1");
        var second = store.ReplaceOpenList("RSI Hermes fit", "garage:RSI_Hermes",
            [new JobItem("SolarFlare", 2), new JobItem("JS-400", 1)], "Area18", "P2");

        var list = Assert.Single(store.All());
        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Job.Id, list.Id);
        Assert.Equal(["SolarFlare", "JS-400"], list.Items.Select(item => item.Name));
        Assert.Equal("Area18", list.Destination);
    }

    [Fact]
    public void Replacing_a_fit_consolidates_the_old_automatic_part_list()
    {
        var store = new JobStore(_root);
        store.Add("RSI Hermes · SolarFlare", "list", "garage:RSI_Hermes", [new JobItem("SolarFlare", 2)]);

        var saved = store.ReplaceOpenList("RSI Hermes fit", "garage:RSI_Hermes",
            [new JobItem("SolarFlare", 2)], "Terra", "P1",
            new HashSet<string>(StringComparer.Ordinal)
            {
                "RSI Hermes · SolarFlare",
                "RSI Hermes - SolarFlare",
            });

        var list = Assert.Single(store.All());
        Assert.True(saved.Created);
        Assert.Single(saved.ConsolidatedIds);
        Assert.Equal("RSI Hermes fit", list.Title);
    }
    /// <summary>
    /// A destination the pilot pointed the list at on Shopping is theirs. The
    /// next rewrite of the list keeps it and says so, rather than writing the
    /// new proposal over it; a proposed destination is still replaced.
    /// </summary>
    [Fact]
    public void A_chosen_destination_survives_the_lists_next_rewrite()
    {
        var store = new JobStore(_root);
        var first = store.ReplaceOpenList("RSI Hermes fit", "garage:RSI_Hermes", [new JobItem("SolarFlare", 2)], "Terra", "P1");
        Assert.False(first.DestinationKept);

        // Proposed, not chosen: the next proposal takes its place.
        var proposed = store.ReplaceOpenList("RSI Hermes fit", "garage:RSI_Hermes", [new JobItem("SolarFlare", 2)], "Area18", "P2");
        Assert.Equal("Area18", proposed.Job.Destination);
        Assert.False(proposed.DestinationKept);

        Assert.True(store.SetDestination(first.Job.Id, "Orison", "P3"));

        var rewritten = store.ReplaceOpenList("RSI Hermes fit", "garage:RSI_Hermes", [new JobItem("SolarFlare", 2), new JobItem("JS-400", 1)], "Lorville", "P4");
        Assert.True(rewritten.DestinationKept);
        Assert.Equal("Orison", rewritten.Job.Destination);
        Assert.Equal("P3", rewritten.Job.DestinationId);
        Assert.Equal(2, rewritten.Job.Items.Count);

        // Clearing the destination hands the choice back.
        Assert.True(store.SetDestination(first.Job.Id, null, null));
        var again = store.ReplaceOpenList("RSI Hermes fit", "garage:RSI_Hermes", [new JobItem("SolarFlare", 2)], "Lorville", "P4");
        Assert.False(again.DestinationKept);
        Assert.Equal("Lorville", again.Job.Destination);

        Assert.Equal(first.Job.Id, store.OpenList("garage:RSI_Hermes", "RSI Hermes fit")!.Id);
        Assert.Null(store.OpenList("garage:RSI_Hermes", "Quiet fit"));
    }

    /// <summary>
    /// One run often buys for more than one list, so pinning a second keeps
    /// the first (1.2.43); before, the second silently unpinned it.
    /// </summary>
    [Fact]
    public void Several_lists_can_be_pinned_and_each_unpins_alone()
    {
        var store = new JobStore(_root);
        var first = store.Add("Hermes fit", "list", null, [new JobItem("SolarFlare", 1)]);
        var second = store.Add("Medical run", "list", null, [new JobItem("MedGel", 2)]);

        Assert.True(store.TogglePin(first.Id));
        Assert.True(store.TogglePin(second.Id));
        Assert.All(store.All(), job => Assert.True(job.Pinned));

        Assert.True(store.TogglePin(first.Id));
        Assert.False(store.All().Single(job => job.Id == first.Id).Pinned);
        Assert.True(store.All().Single(job => job.Id == second.Id).Pinned);
    }

    /// <summary>With several pinned, "+ list" lands in the top one on Shopping, the newest.</summary>
    [Fact]
    public void Collecting_with_several_pinned_lands_in_the_top_one()
    {
        var store = new JobStore(_root);
        var older = store.Add("Hermes fit", "list", null, [new JobItem("SolarFlare", 1)]);
        var newer = store.Add("Medical run", "list", null, [new JobItem("MedGel", 2)]);
        store.Add("Unpinned", "list", null, []);
        store.TogglePin(older.Id);
        store.TogglePin(newer.Id);

        var landed = store.Collect(new JobItem("Hemozal", 1));

        Assert.Equal(newer.Id, landed.Id);
        Assert.Contains(store.All().Single(job => job.Id == newer.Id).Items, item => item.Name == "Hemozal");
    }
}
