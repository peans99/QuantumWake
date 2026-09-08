namespace Quantumwake.WebTests;

/// <summary>
/// Where to go, and the record of where you went.
/// </summary>
/// <remarks>
/// Rich and valuable are different questions. A place can be full of ore nobody
/// pays for, or hold a trace of something precious, so both are shown rather
/// than folded into one score. And the log below them is typed rather than
/// read: the game records no mining at all, so it must never be added to the
/// figures that came from logs.
/// </remarks>
public class MiningPlacesTests
{
    private const string Places = """
        [{"place":"Hurston","system":"Stanton","ore":72.9,"quality":{"min":501,"local":false},
          "perRock":186144,"ores":8,"respawn":3600,
          "best":[{"resource":"Hadanite","worth":450000},{"resource":"Dolivine","worth":90000}]},
         {"place":"Pyro I","system":"Pyro","ore":74.2,"quality":{"min":501,"local":true},
          "perRock":116451,"ores":4,"respawn":3600,
          "best":[{"resource":"Aphorite","worth":300000}]}]
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/mining/places", Places);
        page.Do("await loadMiningPlaces();");
        return page;
    }

    private static string Cell(Page page, int index) =>
        page.Text($"__dom.node('#mining-places tbody').querySelectorAll('td')[{index}].textContent");

    [Fact]
    public void Places_are_ranked_with_how_rich_they_are()
    {
        var page = Loaded();

        Assert.Contains("Hurston", Cell(page, 0));
        Assert.Equal("73%", Cell(page, 2));
    }

    /// <summary>
    /// A place that beats the usual quality floor is the interesting one, and
    /// is marked the same way the deposit table marks it.
    /// </summary>
    [Fact]
    public void A_place_that_beats_the_usual_floor_is_starred()
    {
        var page = Loaded();

        Assert.Equal("501+", Cell(page, 3));
        Assert.Equal("501+*", Cell(page, 11));
    }

    /// <summary>
    /// The install describes fewer places than the download, and saying so is
    /// the difference between "the best place" and "the best place I can see".
    /// </summary>
    [Fact]
    public void It_says_it_is_only_part_of_the_map()
    {
        Assert.Contains("rather than all of it", Loaded().NodeText("#mining-places-note"));
    }
}
