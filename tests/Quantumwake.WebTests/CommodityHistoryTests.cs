namespace Quantumwake.WebTests;

/// <summary>The commodity heading distinguishes absent chart samples from absent live prices.</summary>
public class CommodityHistoryTests
{
    [Fact]
    public void Live_counters_without_chart_samples_do_not_claim_UEX_is_off()
    {
        var page = new Page();

        page.Serve("/api/uex/market?commodity=Waste", "[]");
        page.Serve("/api/commodities?days=0", "[]");
        page.Serve("/api/uex/history?commodity=Waste", """
            { "commodity": "Waste", "sampled": 0, "terminals": 71, "series": [] }
            """);

        page.Do("""
            marketEntries = [{ name: 'Waste', groups: ['Waste'], sold: [], bought: [] }];
            renderCommodityCounters = () => {};
            renderCommodityReceipts = async () => {};
            await openCommodity('Waste');
            """);

        var heading = page.NodeText("#commodity-sub");
        Assert.Contains("Live UEX quotes are available at 71 counters", heading);
        Assert.Contains("counter tables above are still the current report", heading);
        Assert.DoesNotContain("UEX is off", heading);
    }

    [Fact]
    public void No_live_counters_says_that_without_guessing_why()
    {
        var page = new Page();

        page.Serve("/api/uex/market?commodity=Waste", "[]");
        page.Serve("/api/commodities?days=0", "[]");
        page.Serve("/api/uex/history?commodity=Waste", """
            { "commodity": "Waste", "sampled": 0, "terminals": 0, "series": [] }
            """);

        page.Do("""
            marketEntries = [{ name: 'Waste', groups: ['Waste'], sold: [], bought: [] }];
            renderCommodityCounters = () => {};
            renderCommodityReceipts = async () => {};
            await openCommodity('Waste');
            """);

        Assert.Equal("No UEX market counters are currently reported for this commodity.",
            page.NodeText("#commodity-sub"));
    }
}
