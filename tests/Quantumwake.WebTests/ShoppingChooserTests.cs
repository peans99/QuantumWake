namespace Quantumwake.WebTests;

/// <summary>
/// Turning a shopping list into a run: which seller is offered, which is
/// chosen for you, and what the resulting plan carries.
/// </summary>
public class ShoppingChooserTests
{
    /// <summary>Three sellers of one good, cheapest short of what is wanted.</summary>
    private const string LaraniteSellers = """
        [
          {"terminal":"ArcCorp 056","placeId":"","buy":7047,"sell":0,"buyScu":330,"sellScu":0},
          {"terminal":"ArcCorp 045","placeId":"Stanton3_Area18","buy":7400,"sell":0,"buyScu":1050,"sellScu":0},
          {"terminal":"Rustville","placeId":"","buy":9000,"sell":0,"buyScu":4000,"sellScu":0}
        ]
        """;

    private const string AgriciumSellers = """
        [{"terminal":"Endgame","placeId":"","buy":6840,"sell":0,"buyScu":67,"sellScu":0}]
        """;

    private static Page WithSellers()
    {
        var page = new Page();
        page.Serve("/api/uex/market?commodity=Laranite", LaraniteSellers);
        page.Serve("/api/uex/market?commodity=Agricium", AgriciumSellers);
        page.Serve("/api/trips", "[]");
        page.Do("atlas = []; await loadTrips();");
        return page;
    }

    /// <param name="needed">SCU wanted, which decides what counts as short.</param>
    private static void OpenChooser(Page page, double needed = 10, bool includeUnknown = true)
    {
        var unknown = includeUnknown
            ? ", { name: 'Nonexistent Widget', needed: 1, unit: '', have: false, buyAt: null }"
            : "";

        page.Do($$"""
            const job = {
              id: 'j1',
              title: 'Restock',
              items: [
                { name: 'Laranite', needed: {{needed}}, unit: 'SCU', have: false, buyAt: 'ArcCorp 056', buyPrice: 7047 },
                { name: 'Agricium', needed: 5, unit: 'SCU', have: false, buyAt: 'Endgame', buyPrice: 6840 },
                { name: 'Cargo already aboard', needed: 1, unit: 'SCU', have: true, buyAt: 'Endgame' }
                {{unknown}}
              ],
            };

            await planShoppingTrip(job, __dom.node('#test-card'));
            """);
    }

    private const string Rows = "__dom.node('#test-card').byClass('chooser-row')";

    [Fact]
    public void A_row_per_missing_thing_and_none_for_what_is_in_hand()
    {
        var page = WithSellers();
        OpenChooser(page);

        Assert.Equal(3, page.Count($"{Rows}.length"));
        Assert.Contains("Laranite", page.NodeText("#test-card"));
        Assert.DoesNotContain("Cargo already aboard", page.NodeText("#test-card"));
    }

    /// <summary>The select for one row of the chooser.</summary>
    private static string SelectIn(int row) =>
        $"{Rows}[{row}].children.filter(c => c.tagName === 'select')[0]";

    [Fact]
    public void Every_seller_is_offered_cheapest_first_with_a_way_to_skip()
    {
        var page = WithSellers();
        OpenChooser(page);

        var select = SelectIn(0);

        Assert.Equal(4, page.Count($"{select}.options.length"));
        Assert.Contains("ArcCorp 056", page.Text($"{select}.options[0].textContent"));
        Assert.Contains("ArcCorp 045", page.Text($"{select}.options[1].textContent"));
        Assert.Contains("Rustville", page.Text($"{select}.options[2].textContent"));
        Assert.Equal("Leave this one off", page.Text($"{select}.options[3].textContent"));
    }

    /// <summary>
    /// Flying to the cheapest source of 500 SCU to find 330 is a wasted landing
    /// the page could see coming.
    /// </summary>
    [Fact]
    public void A_seller_too_small_for_the_order_is_labelled_short()
    {
        var page = WithSellers();
        OpenChooser(page, needed: 500);

        var select = $"{SelectIn(0)}";

        Assert.Contains("short", page.Text($"{select}.options[0].textContent"));
        Assert.DoesNotContain("short", page.Text($"{select}.options[1].textContent"));
    }

    [Fact]
    public void The_default_is_the_cheapest_that_can_fill_the_order()
    {
        var page = WithSellers();
        OpenChooser(page, needed: 500);

        Assert.Equal("ArcCorp 045",
            page.Text($"{SelectIn(0)}.value"));
    }

    [Fact]
    public void The_cheapest_stands_when_it_can_serve_you()
    {
        var page = WithSellers();
        OpenChooser(page, needed: 10);

        Assert.Equal("ArcCorp 056",
            page.Text($"{SelectIn(0)}.value"));
    }

    [Fact]
    public void Something_with_no_seller_is_shown_rather_than_dropped()
    {
        var page = WithSellers();
        OpenChooser(page);

        Assert.Contains("no known seller", page.NodeText("#test-card"));
    }

    [Fact]
    public void The_stop_count_follows_the_choices()
    {
        var page = WithSellers();
        OpenChooser(page);

        var count = "__dom.node('#test-card').byClass('chooser-foot')[0].children[0].textContent";
        Assert.Equal("2 stops", page.Text(count));

        page.Do($"""
            const select = {SelectIn(0)};
            select.value = '';
            select.fire('change');
            """);

        Assert.Equal("1 stop", page.Text(count));
    }

    /// <summary>
    /// A trip is a sequence of places, so two things bought at one terminal is
    /// one landing carrying both.
    /// </summary>
    [Fact]
    public void One_stop_per_terminal_carrying_what_to_buy_there()
    {
        var page = WithSellers();
        page.Serve("/api/uex/market?commodity=Agricium", """
            [{"terminal":"ArcCorp 056","placeId":"","buy":6840,"sell":0,"buyScu":900,"sellScu":0}]
            """);

        OpenChooser(page, includeUnknown: false);
        page.Do("__dom.node('#test-card').byClass('chooser-foot')[0].byClass('ghost')[0].fire('click');");

        var body = page.BodyOf("/api/trips");

        Assert.Contains("Restock run", body);
        Assert.Contains("Laranite 10 SCU, Agricium 5 SCU", body);
        Assert.Equal(1, page.Count("JSON.parse(__fetch.calls.filter(c => c.url === '/api/trips' && c.body).pop().body).stops.length"));
    }

    [Fact]
    public void A_stop_carries_what_it_will_cost_and_where_the_map_draws_it()
    {
        var page = WithSellers();
        OpenChooser(page, needed: 500);
        page.Do("__dom.node('#test-card').byClass('chooser-foot')[0].byClass('ghost')[0].fire('click');");

        var body = page.BodyOf("/api/trips");

        // 500 SCU at ArcCorp 045's 7,400, and the map id the server resolved.
        Assert.Contains("3,700,000", body);
        Assert.Contains("Stanton3_Area18", body);
    }

    [Fact]
    public void Choosing_nothing_plans_nothing()
    {
        var page = WithSellers();
        OpenChooser(page);

        page.Do($$"""
            for (const row of {{Rows}}) {
              const select = row.children.filter(c => c.tagName === 'select')[0];
              if (!select) continue;
              select.value = '';
              select.fire('change');
            }

            __dom.node('#test-card').byClass('chooser-foot')[0].byClass('ghost')[0].fire('click');
            """);

        Assert.DoesNotContain("POST /api/trips", page.Fetched());
    }
}
