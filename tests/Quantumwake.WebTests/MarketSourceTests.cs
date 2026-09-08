namespace Quantumwake.WebTests;

/// <summary>
/// The Market page can be filled from two different places, and they do not
/// mean the same thing.
/// </summary>
/// <remarks>
/// The community dataset lists the economy simulation's own facilities. The
/// game install plus UEX lists the counters somebody has actually reported a
/// price at, which is fewer. Both are legitimate answers to "where does this
/// sell", and showing the shorter one under the longer one's wording would read
/// as an economy that had shrunk. So the caption changes with the source.
/// </remarks>
public class MarketSourceTests
{
    private const string FromInstall = """
        [{"id":"a1","name":"Agricium","groups":[],"sold":["Area18 TDD"],"bought":[],
          "myScuSold":0,"myRevenue":0,"myTrades":0,"source":"install","uex":null}]
        """;

    private const string FromDataset = """
        [{"id":"a1","name":"Agricium","groups":["Metal"],"sold":["Area18 TDD"],"bought":[],
          "myScuSold":0,"myRevenue":0,"myTrades":0,"source":"dataset","uex":null}]
        """;

    private static Page Loaded(string market)
    {
        var page = new Page();
        page.Serve("/api/market", market);
        page.Do("await loadMarket();");
        return page;
    }

    [Fact]
    public void The_install_list_is_called_a_floor_rather_than_a_roster()
    {
        var caption = Loaded(FromInstall).NodeText("#market-caption");

        Assert.Contains("your game install names", caption);
        Assert.Contains("floor", caption);
    }

    [Fact]
    public void The_dataset_list_still_says_what_it_counts()
    {
        var caption = Loaded(FromDataset).NodeText("#market-caption");

        Assert.Contains("economy simulation", caption);
        Assert.DoesNotContain("floor", caption);
    }

    /// <summary>
    /// The offer to download must not appear once the page is already full, or
    /// it reads as though nothing had loaded.
    /// </summary>
    [Fact]
    public void The_download_offer_stays_away_when_the_install_answered()
    {
        Assert.True(Loaded(FromInstall).Truth("__dom.node('#market-offer').hidden"));
    }
}
