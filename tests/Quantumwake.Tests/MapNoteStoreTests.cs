using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>Personal POIs must stay attached to the atlas id, not a fragile label.</summary>
public class MapNoteStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"qw-map-notes-{Guid.NewGuid():N}");

    [Fact]
    public void A_note_keeps_its_place_tags_and_written_time_after_a_restart()
    {
        var first = new MapNoteStore(_directory);
        var note = first.Add("RR_MIC_LEO", "Port Tressler", "Cargo elevator", "Use the rear lift", ["cargo", "quiet"]);

        Assert.NotNull(note);
        var restored = Assert.Single(new MapNoteStore(_directory).All());
        Assert.Equal("RR_MIC_LEO", restored.PlaceId);
        Assert.Equal("Cargo elevator", restored.Title);
        Assert.Equal(["cargo", "quiet"], restored.Tags);
        Assert.True(restored.UpdatedAt >= restored.CreatedAt);
    }

    [Fact]
    public void A_note_without_a_map_place_is_refused()
    {
        var store = new MapNoteStore(_directory);

        Assert.Null(store.Add(null, "Port Tressler", "Cargo elevator", null, []));
        Assert.Empty(store.All());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }
}
