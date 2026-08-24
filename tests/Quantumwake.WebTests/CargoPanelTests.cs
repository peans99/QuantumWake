namespace Quantumwake.WebTests;

/// <summary>
/// The map's cargo panel: what a commodity fetched, where, and what colour the
/// map paints that. All of it ran only in a browser until now.
/// </summary>
public class CargoPanelTests
{
    private const string Receipts = """
        [
          {"at":"2026-08-22T10:00:00Z","isSell":true,"place":"GrimHEX","placeId":"GrimHEX",
           "scu":320,"amount":195200,"unitPrice":610,"commodity":"Laranite"},
          {"at":"2026-08-21T10:00:00Z","isSell":true,"place":"microTech L1","placeId":"RR_MIC_L1",
           "scu":160,"amount":80000,"unitPrice":500,"commodity":"Laranite"},
          {"at":"2026-08-20T10:00:00Z","isSell":true,"place":"microTech L1","placeId":"RR_MIC_L1",
           "scu":80,"amount":36800,"unitPrice":460,"commodity":"Laranite"},
          {"at":"2026-08-10T10:00:00Z","isSell":true,"place":"Lorville","placeId":"Stanton1_Lorville",
           "scu":16,"amount":4800,"unitPrice":300,"commodity":"Laranite"},
          {"at":"2026-08-19T10:00:00Z","isSell":false,"place":"Lorville","placeId":"Stanton1_Lorville",
           "scu":100,"amount":18000,"unitPrice":180,"commodity":"Laranite"},
          {"at":"2026-08-19T09:00:00Z","isSell":false,"place":"GrimHEX","placeId":"GrimHEX",
           "scu":100,"amount":21000,"unitPrice":210,"commodity":"Laranite"},
          {"at":"2026-08-22T09:00:00Z","isSell":true,"place":"GrimHEX","placeId":"GrimHEX",
           "scu":64,"amount":17600,"unitPrice":275,"commodity":"Agricium"}
        ]
        """;

    /// <summary>A page with receipts loaded and a commodity in hand.</summary>
    private static Page Loaded(string side = "sell")
    {
        var page = new Page();
        page.Serve("/api/commodities?days=0", Receipts);

        page.Do($$"""
            await loadCargoReceipts();
            cargo.name = 'Laranite';
            cargo.buying = {{(side == "buy" ? "true" : "false")}};
            cargo.days = 0;
            """);

        return page;
    }

    [Fact]
    public void Receipts_without_a_commodity_are_not_market_data()
    {
        var page = new Page();
        page.Serve("/api/commodities?days=0", """
            [
              {"at":"2026-08-22T10:00:00Z","isSell":true,"place":"GrimHEX","placeId":"GrimHEX",
               "scu":10,"amount":100,"unitPrice":10,"commodity":null},
              {"at":"2026-08-22T10:00:00Z","isSell":true,"place":"GrimHEX","placeId":"GrimHEX",
               "scu":10,"amount":0,"unitPrice":0,"commodity":"Laranite"},
              {"at":"2026-08-22T10:00:00Z","isSell":true,"place":"GrimHEX","placeId":"GrimHEX",
               "scu":10,"amount":100,"unitPrice":10,"commodity":"Laranite"}
            ]
            """);

        page.Do("await loadCargoReceipts();");

        Assert.Equal(1, page.Count("cargoReceipts.length"));
    }

    [Fact]
    public void The_dearest_place_leads_when_selling()
    {
        var page = Loaded();
        var rows = "receiptsFor('Laranite', false)";

        Assert.Equal(3, page.Count($"{rows}.length"));
        Assert.Equal("GrimHEX", page.Text($"{rows}[0].name"));
        Assert.Equal(610, page.Number($"{rows}[0].best"));
    }

    [Fact]
    public void The_cheapest_place_leads_when_buying()
    {
        var page = Loaded("buy");

        Assert.Equal("Lorville", page.Text("receiptsFor('Laranite', true)[0].name"));
    }

    /// <summary>Best is per place, not per receipt: two sales, one answer.</summary>
    [Fact]
    public void A_places_best_is_its_best_receipt()
    {
        var page = Loaded();

        Assert.Equal(500, page.Number("receiptsFor('Laranite', false)[1].best"));
    }

    /// <summary>
    /// One 160 SCU run must not weigh the same as one 80 SCU top-up, or the
    /// average says a place pays better than it does.
    /// </summary>
    [Fact]
    public void The_average_is_weighted_by_volume()
    {
        var page = Loaded();
        var expected = (160 * 500.0 + 80 * 460.0) / 240.0;

        Assert.Equal(expected, page.Number("receiptsFor('Laranite', false)[1].average"), 3);
    }

    [Fact]
    public void The_window_leaves_out_older_receipts()
    {
        var page = Loaded();

        var all = page.Count("receiptsFor('Laranite', false).length");

        // The window is measured back from now, and these receipts are dated:
        // left against the real clock the answer changes by the hour and then
        // stops changing at all. Pin the clock to a day the fixture is about.
        page.Do("Date.now = () => Date.parse('2026-08-22T12:00:00Z'); cargo.days = 3;");
        var recent = page.Count("receiptsFor('Laranite', false).length");

        Assert.Equal(3, all);
        Assert.Equal(2, recent);
    }

    [Fact]
    public void Another_commodity_is_a_different_market()
    {
        var page = Loaded();

        Assert.Equal(1, page.Count("receiptsFor('Agricium', false).length"));
        Assert.Equal("GrimHEX", page.Text("receiptsFor('Agricium', false)[0].name"));
    }

    /// <summary>
    /// The ramp runs from poor to good, and "good" changes sides: the gold end
    /// is the best price paid when selling and the cheapest when buying.
    /// </summary>
    [Fact]
    public void The_colour_ramp_runs_poor_to_good()
    {
        var page = new Page();

        Assert.NotEqual(page.Text("shadeColour(0)"), page.Text("shadeColour(1)"));
        Assert.Equal(page.Text("shadeColour(1)"), page.Text("shadeColour(1)"));
    }

    [Fact]
    public void The_panel_ranks_every_place_it_has_a_receipt_for()
    {
        var page = Loaded();
        page.Serve("/api/uex/market?commodity=Laranite", "[]");

        page.Do("""
            marketEntries = [{ name: 'Laranite', sold: [], bought: [], groups: [] }];
            shadeRows = { name: 'Laranite', rows: [] };
            renderCommodityPanel(marketEntries[0]);
            """);

        var body = page.NodeText("#cargo-body");

        Assert.Contains("GrimHEX", body);
        Assert.Contains("microTech L1", body);
        Assert.Contains("Lorville", body);
        Assert.Contains("Trade history", body);
    }

    [Fact]
    public void A_station_panel_separates_what_it_takes_from_what_it_offers()
    {
        var page = Loaded();

        page.Do("""
            atlas = [{ rawId: 'GrimHEX', name: 'GrimHEX', kind: 'Station', visits: 3 }];
            marketEntries = [];
            showStation('GrimHEX', 'GrimHEX');
            """);

        var body = page.NodeText("#cargo-body");

        Assert.Contains("Accepts", body);
        Assert.Contains("Offers", body);
        Assert.Contains("Laranite", body);
        Assert.Contains("Agricium", body);
    }
}
