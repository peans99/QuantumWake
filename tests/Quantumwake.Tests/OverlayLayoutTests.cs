using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// What the overlay shows. The interesting case is upgrade: a layout saved
/// before a card existed must not read as "the user switched it off".
/// </summary>
public class OverlayLayoutTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-overlay-{Guid.NewGuid():N}");

    private string LayoutPath => Path.Combine(_directory, "overlay-layout.json");

    private void WriteLayout(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(LayoutPath, json);
    }

    [Fact]
    public void A_card_added_since_the_layout_was_saved_arrives_switched_on()
    {
        // No "known" list: written before the app recorded what it offered.
        WriteLayout("""{"Tabs":["now"],"Cards":["location","ship"],"Density":"normal"}""");

        var cards = new OverlayLayoutStore(_directory).Current.Cards;

        Assert.Contains("location", cards);
        Assert.Contains("trip", cards);
        Assert.DoesNotContain("feed", cards);
    }

    [Fact]
    public void A_card_switched_off_stays_off()
    {
        var store = new OverlayLayoutStore(_directory);
        store.Save(new OverlayLayout(["now"], ["location", "ship"], "normal"));

        var reopened = new OverlayLayoutStore(_directory).Current;

        Assert.Equal(["location", "ship"], reopened.Cards);
    }

    [Fact]
    public void Saving_records_what_the_app_offered()
    {
        var store = new OverlayLayoutStore(_directory);
        var saved = store.Save(new OverlayLayout(["now"], ["location"], "normal"));

        Assert.Equal(OverlayLayout.SelectableCards, saved.Known);
    }

    [Fact]
    public void Unknown_names_are_dropped_and_the_widget_keeps_a_tab()
    {
        var store = new OverlayLayoutStore(_directory);

        var saved = store.Save(new OverlayLayout(["nonsense"], ["location", "nonsense"], "silly"));

        Assert.Equal(["now"], saved.Tabs);
        Assert.Equal(["location"], saved.Cards);
        Assert.Equal("normal", saved.Density);
    }

    [Fact]
    public void A_corrupt_layout_falls_back_to_the_default()
    {
        WriteLayout("{ not json");

        Assert.Equal(OverlayLayout.Default.Cards, new OverlayLayoutStore(_directory).Current.Cards);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test run over.
        }
    }
}
