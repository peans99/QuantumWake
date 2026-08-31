namespace Quantumwake.WebTests;

/// <summary>
/// Filling the install-backed catalogues once the game files have been read.
/// </summary>
/// <remarks>
/// Parts, Mining and Crafting are fetched once, when the page loads. On a cold
/// install that is half a minute before there is anything to fetch, so they came
/// back empty and stayed empty until somebody reloaded the browser - the app
/// looked like it held no data rather than like it was still reading. The poll
/// that watches the read finish is the only thing in a position to notice, so
/// filling them is its job.
/// </remarks>
public class GameDataRefillTests
{
    private const string Reading = """
        {"state":"reading","problem":null,"seconds":6,"counts":{}}
        """;

    private const string Ready = """
        {"state":"ready","problem":null,"seconds":28.4,
         "counts":{"commodities":342,"items":26028,"recipes":1606,
                   "deposits":783,"places":1388,"spawnplaces":49}}
        """;

    private static Page Waiting()
    {
        var page = new Page();
        page.Serve("/api/reference/items", "[]");
        page.Serve("/api/reference/resources", "[]");
        page.Serve("/api/reference/blueprints", "[]");
        page.Serve("/api/map", """{"nodes":[],"destinations":[],"positions":{}}""");
        page.Serve("/api/map/services", "[]");
        page.Serve("/api/map/amenities", "[]");
        page.Serve("/api/gamedata", Reading);
        page.Do("await loadGameData();");
        return page;
    }

    [Fact]
    public void The_catalogues_are_fetched_again_when_the_read_finishes()
    {
        var page = Waiting();
        Assert.DoesNotContain("GET /api/reference/items", page.Fetched());

        page.Serve("/api/gamedata", Ready);
        page.Do("await loadGameData();");

        Assert.Contains("GET /api/reference/items", page.Fetched());
        Assert.Contains("GET /api/reference/resources", page.Fetched());
        Assert.Contains("GET /api/reference/blueprints", page.Fetched());
    }

    /// <summary>
    /// Only on the transition. A poll that refetched three catalogues every time
    /// it confirmed the same answer would be worse than the bug.
    /// </summary>
    [Fact]
    public void A_read_that_was_already_finished_refetches_nothing()
    {
        var page = new Page();
        page.Serve("/api/gamedata", Ready);
        page.Do("await loadGameData();");
        page.Do("await loadGameData();");

        Assert.DoesNotContain("GET /api/reference/items", page.Fetched());
    }

    /// <summary>
    /// The Settings copy compares the two sources by quoting install figures.
    /// Three of them were typed into the page and had gone stale, so they are
    /// filled from the same counts the tiles use.
    /// </summary>
    [Fact]
    public void The_quoted_install_figures_come_from_the_counts()
    {
        var page = new Page();
        page.Serve("/api/gamedata", Ready);
        page.Do("await loadGameData();");

        Assert.Equal("26,028", page.NodeText("#settings-install-items"));
        Assert.Equal("783", page.NodeText("#settings-install-deposits"));
        Assert.Equal("49", page.NodeText("#settings-install-spawnplaces"));
    }
    /// <summary>
    /// These three read from the install now. Telling someone to enable a 110 MB
    /// download because a table they own has not finished being read is both
    /// wrong and expensive, and it was still the message on all three.
    /// </summary>
    [Theory]
    [InlineData("renderPartsRef()", "#parts-table tbody")]
    [InlineData("renderMiningRef()", "#mining-table tbody")]
    [InlineData("renderCraftingRef()", "#crafting-table tbody")]
    public void An_empty_catalogue_that_is_still_reading_says_so(string render, string body)
    {
        var page = Waiting();
        page.Do(render + ";");

        var text = page.NodeText(body);
        Assert.Contains("Still reading", text);
        Assert.DoesNotContain("community dataset", text);
    }
    /// <summary>
    /// The map reads the install too - its places come from the game's own
    /// gazetteer and the amenities filter is built entirely from it - and it was
    /// left out of the first pass at this, so a cold start showed a thin map
    /// until the browser was reloaded.
    /// </summary>
    [Fact]
    public void The_map_is_filled_in_when_the_read_finishes()
    {
        var page = Waiting();
        Assert.DoesNotContain("GET /api/map/amenities", page.Fetched());

        page.Serve("/api/gamedata", Ready);
        page.Do("await loadGameData();");

        Assert.Contains("GET /api/map", page.Fetched());
        Assert.Contains("GET /api/map/amenities", page.Fetched());
    }
}
