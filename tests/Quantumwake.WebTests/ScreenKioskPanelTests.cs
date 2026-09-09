namespace Quantumwake.WebTests;

/// <summary>
/// A kiosk reading on the page.
/// </summary>
/// <remarks>
/// The unit stays welded to the price everywhere it is shown. A price with
/// the unit stripped off is the one number on this screen that could cost
/// somebody real money.
/// </remarks>
public class ScreenKioskPanelTests
{
    private const string KioskSighting = """
        {"shot":"kiosk.jpg","shotAt":"2026-09-09T02:00:00Z","kind":"Kiosk",
         "summary":"a kiosk buying, 3 commodities listed","tookMs":407,
         "checks":[{"subject":"Kiosk",
                    "claim":"3 commodities listed: Hephaestanite at 2,197 per UNITS, Corundum at 296 per SCU",
                    "belief":"nothing - the logs never carry a shop's price","verdict":"new",
                    "note":"this kiosk prices in UNITS and SCU on the same screen, so the two are kept apart and never converted"}],
         "kiosk":{"buying":true,"shipRead":"usc HULL A","ship":null,"cargoUsed":0,"cargoCapacity":64,
                  "balanceRead":"¤1,583M AUEC",
                  "rows":[
                    {"read":"HEPHAESTANITE","commodity":"Hephaestanite","state":"MAX INVENTORY","quantity":5000,"quantityUnit":"SCU","price":2197,"priceUnit":"UNITS"},
                    {"read":"CORUNDUM","commodity":"Corundum","state":"MAX INVENTORY","quantity":6000,"quantityUnit":"SCU","price":296,"priceUnit":"SCU"},
                    {"read":"TITANIUM","commodity":"Titanium","state":"MAX INVENTORY","quantity":6000,"quantityUnit":"SCU","price":null,"priceUnit":null}
                  ]},
         "lines":["COMMODITIES"]}
        """;

    private static Page Panel()
    {
        var page = new Page();

        page.Serve("/api/screen/settings", """
            {"mode":"Screenshots","watch":false,"watchScreenshots":true,
             "canReadScreenshots":true,"canReadClipboard":true,"folder":"E:\\shots"}
            """);

        page.Serve("/api/screen/readings?take=12", $$"""{"readings":[{{KioskSighting}}],"total":1}""");
        page.Do("await renderScreenPanel();");
        return page;
    }

    [Fact]
    public void A_kiosk_reading_lists_each_commodity_with_its_price_and_stock()
    {
        var list = Panel().NodeText("#screen-readings");

        Assert.Contains("a kiosk buying", list);
        Assert.Contains("Hephaestanite", list);
        Assert.Contains("Corundum", list);
        Assert.Contains("6,000 SCU in stock", list);
        Assert.Contains("0 / 64 SCU", list);
    }

    /// <summary>
    /// Two commodities on one screen priced in different quantities, and the
    /// page shows each with the unit it was printed in.
    /// </summary>
    [Fact]
    public void The_unit_is_never_shown_apart_from_the_price()
    {
        var list = Panel().NodeText("#screen-readings");

        Assert.Contains("2,197 per UNITS", list);
        Assert.Contains("296 per SCU", list);
        Assert.Contains("never converted", list);
    }

    [Fact]
    public void A_commodity_whose_price_did_not_read_shows_none()
    {
        var list = Panel().NodeText("#screen-readings");

        Assert.Contains("Titanium", list);
        Assert.DoesNotContain("per null", list);
        Assert.DoesNotContain("undefined", list);
    }

    [Fact]
    public void The_abbreviated_balance_is_shown_as_abbreviated()
    {
        var list = Panel().NodeText("#screen-readings");

        Assert.Contains("1,583M", list);
        Assert.Contains("abbreviated", list);
    }

    [Fact]
    public void The_Now_card_carries_the_kiosk_as_a_new_finding()
    {
        var page = Panel();

        Assert.False(page.Truth("__dom.node('#now-screen-card').hidden"));
        Assert.Contains("a kiosk buying", page.NodeText("#now-screen-summary"));
        Assert.Contains("new", page.NodeText("#now-screen-checks"));
    }
}
