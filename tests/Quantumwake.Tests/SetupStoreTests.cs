using System.Text.Json;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Named setups (1.2.36): Routes presets and Stream Deck choices, kept like
/// kits - their own file, through a restart, in the backup.
/// </summary>
public class SetupStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 20, 0, 0, TimeSpan.Zero);
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"qw-setups-{Guid.NewGuid():N}");

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void A_setup_is_kept_through_a_restart_and_a_name_is_replaced_not_doubled()
    {
        var store = new SetupStore(_root);
        var first = store.Save(SetupStore.RoutePreset, "Hauling", Json("""{"ship":"C2","capital":"500000"}"""))!;
        var again = store.Save(SetupStore.RoutePreset, "hauling", Json("""{"ship":"Hull C","capital":"900000"}"""))!;

        Assert.Equal(first.Id, again.Id);
        var kept = Assert.Single(new SetupStore(_root).Of(SetupStore.RoutePreset));
        Assert.Equal("Hull C", kept.Data.GetProperty("ship").GetString());

        // The same name under another kind is another setup.
        store.Save(SetupStore.Deck, "Hauling", Json("""{"model":"xl","actions":[]}"""));
        Assert.Equal(2, new SetupStore(_root).All().Count);
    }

    [Theory]
    [InlineData("wallpaper", "A", """{"x":1}""")]
    [InlineData("deck", "   ", """{"x":1}""")]
    [InlineData("deck", "A", "[1,2]")]
    public void Unknown_kinds_blank_names_and_non_objects_are_refused(string kind, string name, string json)
    {
        Assert.Null(new SetupStore(_root).Save(kind, name, Json(json)));
    }

    [Fact]
    public void Settings_too_big_to_be_a_setup_are_refused_and_a_full_kind_drops_its_oldest()
    {
        var store = new SetupStore(_root);
        Assert.Null(store.Save(SetupStore.Deck, "Huge", Json($$"""{"x":"{{new string('a', SetupStore.MaxDataBytes)}}"}""")));

        for (var i = 0; i <= SetupStore.MaxPerKind; i++) store.Save(SetupStore.RoutePreset, $"Preset {i}", Json("{}"));
        var kept = store.Of(SetupStore.RoutePreset);
        Assert.Equal(SetupStore.MaxPerKind, kept.Count);
        Assert.DoesNotContain(kept, s => s.Name == "Preset 0");
    }

    [Fact]
    public void Setups_are_in_the_backup_and_come_back_from_one()
    {
        var store = new SetupStore(_root);
        store.Save(SetupStore.Deck, "Last used", Json("""{"model":"15","source":"current","actions":["v_toggle_landing_system"]}"""));

        var backup = new BackupBuilder(
            new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
            new MiningLogStore(_root), new MapNoteStore(_root), new GoalStore(_root),
            new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
            new KitStore(_root), new ScreenReadingStore(_root), new ShardNoteStore(_root), new BuildStore(_root), store);

        var document = backup.Build(new ExportProducer("Quantumwake", "1.2.36"), Now);
        Assert.Equal(3, document.ContentVersion);
        var file = document.Backup!;
        Assert.Single(file.Setups!);
        Assert.Equal(1, backup.Preview().Setups);

        // Through the file format and back, as a real restore reads it.
        var text = JsonSerializer.Serialize(document, ExportDocument.Json);
        var read = JsonSerializer.Deserialize<ExportFile>(text, ExportDocument.Json)!.Backup!;

        var other = Path.Combine(_root, "other");
        Directory.CreateDirectory(other);
        using var sessions = new SessionStore(":memory:");
        var target = new SetupStore(other);
        var restore = new RestoreService(
            new JobStore(other), new ChecklistStore(other), new TripStore(other),
            new MiningLogStore(other), new MapNoteStore(other), new GoalStore(other),
            new WipeStore(other), new ItemLabelStore(other), new TombstoneStore(other), new LogLibrary(sessions),
            new KitStore(other), new ScreenReadingStore(other), new ShardNoteStore(other), new BuildStore(other), target);

        restore.Apply(read, restore.Plan(read, "h"), "h", new RestoreChoices());

        var landed = Assert.Single(target.All());
        Assert.Equal("Last used", landed.Name);
        Assert.Equal("v_toggle_landing_system", landed.Data.GetProperty("actions")[0].GetString());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }
}
