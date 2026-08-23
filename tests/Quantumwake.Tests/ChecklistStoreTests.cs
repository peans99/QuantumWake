using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>Authored checklists must survive restarts and keep one clear Now target.</summary>
public class ChecklistStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"qw-checklists-{Guid.NewGuid():N}");

    private ChecklistStore NewStore() => new(_directory);

    [Fact]
    public void A_task_keeps_its_location_and_reference_attachments()
    {
        var store = NewStore();
        var list = store.Add("Pyro departure");

        store.AddItem(list.Id, "Buy a tractor beam", DateTimeOffset.Parse("2026-08-25T20:00:00Z"), "Before leaving", [
            new ChecklistAttachment("location", "Port Tressler", "Port Tressler", "RR_MIC_LEO"),
            new ChecklistAttachment("item", "MaxLift Tractor Beam", "MaxLift Tractor Beam"),
            new ChecklistAttachment("url", "Loadout guide", "https://example.test/loadout")
        ]);

        var item = Assert.Single(Assert.Single(store.All()).Items);
        Assert.Equal("Buy a tractor beam", item.Text);
        Assert.Equal("RR_MIC_LEO", item.Attachments[0].PlaceId);
        Assert.Equal("MaxLift Tractor Beam", item.Attachments[1].Label);
        Assert.Equal("https://example.test/loadout", item.Attachments[2].Target);
    }

    [Fact]
    public void Pinning_a_list_replaces_the_previous_now_list()
    {
        var store = NewStore();
        var first = store.Add("Cargo run");
        var second = store.Add("Bunker");

        Assert.True(store.TogglePin(first.Id));
        Assert.True(store.TogglePin(second.Id));

        Assert.False(store.All().Single(list => list.Id == first.Id).Pinned);
        Assert.True(store.All().Single(list => list.Id == second.Id).Pinned);
    }

    [Fact]
    public void A_checked_task_records_when_it_was_done_and_can_be_reopened()
    {
        var store = NewStore();
        var list = store.Add("Run");
        var added = store.AddItem(list.Id, "Refuel", null, null, []);
        var itemId = Assert.Single(added!.Items).Id;

        Assert.True(store.ToggleItem(list.Id, itemId));
        Assert.True(store.All().Single().Items.Single().Done);
        Assert.NotNull(store.All().Single().Items.Single().DoneAt);

        Assert.True(store.ToggleItem(list.Id, itemId));
        Assert.False(store.All().Single().Items.Single().Done);
        Assert.Null(store.All().Single().Items.Single().DoneAt);
    }

    [Fact]
    public void Lists_survive_a_restart()
    {
        var first = NewStore();
        var list = first.Add("Departure");
        first.AddItem(list.Id, "Set med bed", null, null, []);

        var restored = NewStore();
        Assert.Equal("Departure", Assert.Single(restored.All()).Title);
        Assert.Equal("Set med bed", Assert.Single(restored.All().Single().Items).Text);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }
}
