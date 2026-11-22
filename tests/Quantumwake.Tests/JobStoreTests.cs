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

    /// <summary>Correcting an amount must not reopen a line already bought (1.2.45).</summary>
    [Fact]
    public void Editing_a_list_keeps_the_tick_on_a_line_that_keeps_its_name()
    {
        var store = new JobStore(_root);
        var job = store.Add("Hermes fit", "list", null, [new JobItem("JS-400", 1), new JobItem("SolarFlare", 2)]);
        Assert.True(store.ToggleGot(job.Id, "js-400"));

        Assert.True(store.Edit(job.Id, "Hermes refit", [new JobItem("JS-400", 2), new JobItem("FR-86", 1)]));

        var edited = store.All().Single();
        Assert.Equal("Hermes refit", edited.Title);
        Assert.Equal(["JS-400", "FR-86"], edited.Items.Select(i => i.Name));
        Assert.True(edited.Items[0].Got);
        Assert.False(edited.Items[1].Got);
    }

    [Fact]
    public void Ticking_an_unknown_line_or_list_changes_nothing()
    {
        var store = new JobStore(_root);
        var job = store.Add("Hermes fit", "list", null, [new JobItem("JS-400", 1)]);

        Assert.False(store.ToggleGot(job.Id, "FR-86"));
        Assert.False(store.ToggleGot("nope", "JS-400"));
        Assert.False(store.All().Single().Items[0].Got);
    }

    /// <summary>Now hides a done list, so its pin was a button saying "On Now" about nothing.</summary>
    [Fact]
    public void Marking_a_list_done_takes_it_off_now()
    {
        var store = new JobStore(_root);
        var job = store.Add("Hermes fit", "list", null, [new JobItem("JS-400", 1)]);
        store.TogglePin(job.Id);

        store.Toggle(job.Id);

        Assert.False(store.All().Single().Pinned);
    }

    [Fact]
    public void Collecting_into_a_named_list_puts_it_there_whatever_is_pinned()
    {
        var store = new JobStore(_root);
        var chosen = store.Add("Medical run", "list", null, []);
        var pinned = store.Add("Hermes fit", "list", null, []);
        store.TogglePin(pinned.Id);

        Assert.Equal(chosen.Id, store.Collect(new JobItem("MedGel", 1), chosen.Id).Id);
    }

    /// <summary>A Market part is rarely a blueprint material; a pinned build is the last pinned choice.</summary>
    [Fact]
    public void Collecting_prefers_a_pinned_list_to_a_pinned_build()
    {
        var store = new JobStore(_root);
        var list = store.Add("Hermes fit", "list", null, []);
        var build = store.Add("Craft Karna", "craft", null, []);
        store.TogglePin(list.Id);
        store.TogglePin(build.Id);

        Assert.Equal(list.Id, store.Collect(new JobItem("JS-400", 1)).Id);
    }

    [Fact]
    public void Wanting_more_of_a_ticked_line_reopens_it()
    {
        var store = new JobStore(_root);
        var job = store.Add("Hermes fit", "list", null, [new JobItem("JS-400", 1)]);
        store.ToggleGot(job.Id, "JS-400");

        store.Collect(new JobItem("JS-400", 1), job.Id);

        var line = store.All().Single().Items.Single();
        Assert.Equal(2, line.Needed);
        Assert.False(line.Got);
    }

    /// <summary>
    /// A run split between places (1.2.65): the weapons at one station, a part
    /// only sold at another. Each line keeps its own store, on disk.
    /// </summary>
    [Fact]
    public void Each_line_keeps_its_own_store_across_a_reload()
    {
        var store = new JobStore(_root);
        var job = store.Add("MK5 kit", "list", null,
        [
            new JobItem("Arrowhead", 1, Store: "Cousin Crow's, Orison", StoreId: "Stanton2_Orison"),
            new JobItem("Concord", 2),
        ]);

        var reloaded = new JobStore(_root).All().Single(j => j.Id == job.Id);

        Assert.Equal("Cousin Crow's, Orison", reloaded.Items[0].Store);
        Assert.Equal("Stanton2_Orison", reloaded.Items[0].StoreId);
        Assert.Null(reloaded.Items[1].Store);
    }

    [Fact]
    public void A_store_is_set_on_one_line_and_cleared_by_sending_none()
    {
        var store = new JobStore(_root);
        var job = store.Add("MK5 kit", "list", null, [new JobItem("Arrowhead", 1), new JobItem("Concord", 2)]);

        Assert.True(store.SetStore(job.Id, "concord", " HUR-L5 ", "  "));
        var set = store.All().Single().Items;
        Assert.Null(set[0].Store);
        Assert.Equal("HUR-L5", set[1].Store);
        Assert.Null(set[1].StoreId);

        Assert.True(store.SetStore(job.Id, "Concord", "", "ignored"));
        var cleared = store.All().Single().Items[1];
        Assert.Null(cleared.Store);
        Assert.Null(cleared.StoreId);

        Assert.False(store.SetStore(job.Id, "Nothing like it", "HUR-L5", null));
        Assert.False(store.SetStore("nosuchid", "Concord", "HUR-L5", null));
    }

    /// <summary>The card's text has nowhere to write a store, so a rewrite must not drop it.</summary>
    [Fact]
    public void Editing_a_list_keeps_the_store_on_a_line_that_keeps_its_name()
    {
        var store = new JobStore(_root);
        var job = store.Add("MK5 kit", "list", null, [new JobItem("Concord", 2, Store: "HUR-L5")]);

        store.Edit(job.Id, null, [new JobItem("concord", 3), new JobItem("Arrowhead", 1)]);

        var items = store.All().Single().Items;
        Assert.Equal("HUR-L5", items[0].Store);
        Assert.Equal(3, items[0].Needed);
        Assert.Null(items[1].Store);
    }

    [Fact]
    public void The_next_garage_proposal_keeps_a_chosen_store()
    {
        var store = new JobStore(_root);
        var first = store.ReplaceOpenList("RSI Hermes fit", "garage:RSI_Hermes", [new JobItem("SolarFlare", 2)]);
        store.SetStore(first.Job.Id, "SolarFlare", "Platinum Bay, Port Tressler", null);

        store.ReplaceOpenList("RSI Hermes fit", "garage:RSI_Hermes", [new JobItem("SolarFlare", 2), new JobItem("JS-400", 1)]);

        var items = store.All().Single().Items;
        Assert.Equal("Platinum Bay, Port Tressler", items[0].Store);
        Assert.Null(items[1].Store);
    }

    [Fact]
    public void Collecting_with_a_store_moves_the_line_and_without_one_leaves_it()
    {
        var store = new JobStore(_root);
        var job = store.Add("MK5 kit", "list", null, [new JobItem("Concord", 1, Store: "HUR-L5", StoreId: "L5")]);

        store.Collect(new JobItem("Concord", 1), job.Id);
        var kept = store.All().Single().Items.Single();
        Assert.Equal(("HUR-L5", "L5"), (kept.Store, kept.StoreId));

        store.Collect(new JobItem("Concord", 1, Store: "Orison"), job.Id);
        var moved = store.All().Single().Items.Single();
        Assert.Equal(3, moved.Needed);
        Assert.Equal("Orison", moved.Store);
        Assert.Null(moved.StoreId);
    }
}
