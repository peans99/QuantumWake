namespace Quantumwake.WebTests;

/// <summary>
/// The commodity page's "Seen at the kiosk" table (1.2.22): a price read off
/// the screen beside UEX's, and a misread shown as one.
/// </summary>
public class CommoditySeenTests
{
    private const string Url = "/api/kiosk-prices?commodity=Ship%20Ammunition%20-%20Size%203";

    private static Page Rendered(string report)
    {
        var page = new Page();
        page.Serve(Url, report);
        page.Do("""
            openCommodityName = 'Ship Ammunition - Size 3';
            await renderCommoditySeen('Ship Ammunition - Size 3');
            """);
        return page;
    }

    [Fact]
    public void A_misread_price_is_struck_through_and_says_so()
    {
        var page = Rendered("""
            {"seen":[
              {"at":"2026-09-11T00:53:54Z","shot":"s","commodity":"Ship Ammunition - Size 3","buying":true,"price":384,
               "place":"Checkmate","terminal":"Checkmate","uex":7384,"uexAt":"2026-11-03T10:00:00Z","verdict":"misread"},
              {"at":"2026-09-10T00:53:54Z","shot":"t","commodity":"Ship Ammunition - Size 3","buying":true,"price":7500,
               "place":"Checkmate","terminal":"Checkmate","uex":7384,"verdict":"differs"}
            ],"olderReader":0}
            """);

        var text = page.Text("__dom.node('#commodity-seen-table').querySelector('tbody').textContent");
        Assert.Contains("probably misread", text);
        Assert.Contains("Asking", text);
        Assert.Contains("differs from UEX (+2%)", text);
        Assert.Equal(1, page.Count("__dom.node('#commodity-seen-table').querySelector('tbody').querySelectorAll('.struck').length"));
        Assert.True(page.Count("__dom.node('#commodity-seen-note').hidden ? 1 : 0") == 1);
    }

    [Fact]
    public void Readings_from_the_earlier_reader_are_counted_with_the_way_to_bring_them_in()
    {
        var page = Rendered("""{"seen":[],"olderReader":3}""");

        Assert.Contains("No kiosk showing this one has been read.", page.Text("__dom.node('#commodity-seen-table').querySelector('tbody').textContent"));
        Assert.Contains("3 older kiosk readings are left out", page.NodeText("#commodity-seen-note"));
        Assert.Contains("Read them again from the Log page", page.NodeText("#commodity-seen-note"));
    }
}
