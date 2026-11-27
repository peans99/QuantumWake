namespace Quantumwake.WebTests;

/// <summary>
/// Ships and ground vehicles on a shopping list (1.3.12 / 1.2.69).
/// </summary>
/// <remarks>
/// A list could hold a commodity or an item and nothing else, so "buy a Cutlass
/// at Astro Armada" was a line no seller lookup answered. The server now names
/// a line's ship and its showrooms; these are the page's half: the line opens
/// the ship's card, a ship written without an amount costs one ship, and a ship
/// name the catalogue knows is not cut at a trailing number.
/// </remarks>
public sealed class ShoppingVehicleTests
{
    private const string Lists = """
        [{"id":"a","title":"New ship","kind":"list","done":false,"pinned":false,"haveCount":0,"totalCount":2,
          "items":[{"name":"Drake Cutlass Black","needed":0,"unit":"","have":false,"got":false,"where":[],
                    "buyPrice":1400000,"buyAt":"Astro Armada","vehicle":true},
                   {"name":"SolarFlare","needed":2,"unit":"","have":false,"got":false,"where":[],"buyPrice":1000,"buyAt":"Centermass"}]}]
        """;

    private const string Catalogue = """{"commodities":[],"items":["SolarFlare"],"vehicles":["Drake Cutlass Black","Origin 300i"]}""";

    private const string Rows = "__dom.node('#jobs-list').byClass('job-card')[0].querySelectorAll('tr')";

    private static Page Shopping()
    {
        var page = new Page();
        page.Serve("/api/jobs", Lists);
        page.Serve("/api/shopping/catalogue", Catalogue);
        page.Do("atlas = []; await loadJobList();");
        return page;
    }

    [Fact]
    public void A_ship_line_opens_the_ship_card_and_an_item_line_an_item_card()
    {
        var page = Shopping();

        Assert.Equal("Open this ship", page.Text(
            $"{Rows}.find(r => r.textContent.includes('Cutlass')).byClass('ref-link')[0].title"));
        Assert.Equal("Open this item", page.Text(
            $"{Rows}.find(r => r.textContent.includes('SolarFlare')).byClass('ref-link')[0].title"));
    }

    [Fact]
    public void A_ship_written_without_an_amount_is_priced_as_one()
    {
        var page = Shopping();

        var cost = page.Text("__dom.node('#jobs-list').byClass('job-card')[0].byClass('job-cost')[0].textContent");

        // 1,400,000 for the ship and 2 x 1,000: every line priced, so "about", not "at least".
        Assert.Contains("about", cost);
        Assert.DoesNotContain("no price or amount", cost);
        Assert.Contains("1,402,000", cost);
    }

    [Fact]
    public void The_catalogue_keeps_a_ship_name_whole()
    {
        var page = new Page();
        page.Do($"catalogue = {Catalogue};");

        Assert.Equal("""{"name":"Drake Cutlass Black","needed":0,"unit":""}""",
            page.Text("JSON.stringify(parseJobItems('Drake Cutlass Black')[0])"));
        Assert.Equal("""{"name":"Origin 300i","needed":2,"unit":""}""",
            page.Text("JSON.stringify(parseJobItems('Origin 300i 2')[0])"));
    }

    [Fact]
    public void The_finder_prices_a_ship_each_not_per_SCU()
    {
        var page = new Page();

        var where = page.Text("""
            jobFindWhere({ name: 'Drake Cutlass Black', kind: 'vehicle', counters: 3, place: 'Astro Armada', price: 1400000 }, 'cheapest')
            """);

        Assert.Contains("Astro Armada", where);
        Assert.DoesNotContain("/SCU", where);
        Assert.Contains("cheapest of 3 places", where);
    }
}
