namespace Quantumwake.WebTests;

/// <summary>
/// A list's run chosen for the nearest counters, the cheapest, or the fewest
/// stops (1.2.60), with each counter's distance from the pilot; and a line the
/// stash probably holds in another colour, said on the card and in the chooser.
/// </summary>
public sealed class ShoppingPreferTests
{
    // Measured from New Babbage: the cheapest core is two moons away, the
    // nearest is on microTech; the arms are only sold here.
    private const string CoreSellers = """
        {"name":"ORC-mkV Core Grey","kind":"item","from":{"name":"New Babbage","id":"Stanton4_NewBabbage","source":"live"},"sellers":[
          {"kind":"item","terminal":"Tammany and Sons","placeId":"Stanton1_Lorville","place":"Lorville","body":"Hurston","system":"Stanton","security":"monitored","price":3040,"scu":0,"here":false,"gm":41.2},
          {"kind":"item","terminal":"FPS Armor Tressler","placeId":"Stanton4_PortTressler","place":"Port Tressler","body":"microTech","system":"Stanton","security":"monitored","price":3300,"scu":0,"here":false,"gm":0},
          {"kind":"item","terminal":"Platinum Bay New Babbage","placeId":"Stanton4_NewBabbage","place":"New Babbage","body":"microTech","system":"Stanton","security":"monitored","price":3500,"scu":0,"here":true,"gm":0}
        ]}
        """;

    private const string ArmsSellers = """
        {"name":"ORC-mkV Arms Grey","kind":"item","from":{"name":"New Babbage","id":"Stanton4_NewBabbage","source":"live"},"sellers":[
          {"kind":"item","terminal":"Platinum Bay New Babbage","placeId":"Stanton4_NewBabbage","place":"New Babbage","body":"microTech","system":"Stanton","security":"monitored","price":1100,"scu":0,"here":true,"gm":0}
        ]}
        """;

    private static Page Chooser()
    {
        var page = new Page();
        page.Serve("/api/shopping/sellers?name=ORC-mkV%20Core%20Grey", CoreSellers);
        page.Serve("/api/shopping/sellers?name=ORC-mkV%20Arms%20Grey", ArmsSellers);
        page.Serve("/api/trips", "[]");
        page.Do("""
            atlas = []; await loadTrips();
            await planShoppingTrip({ id: 'adi', title: 'ADI uniforms', items: [
              { name: 'ORC-mkV Core Grey', needed: 1, unit: '', have: false, where: [],
                alike: [{ name: 'ORC-mkV Core Red', places: ['Terminus LEO Rest Stop'] }] },
              { name: 'ORC-mkV Arms Grey', needed: 1, unit: '', have: false, where: [], alike: [] },
            ] }, __dom.node('#test-card'));
            """);
        return page;
    }

    private const string Rows = "__dom.node('#test-card').byClass('chooser-row')";

    private static string SelectIn(int row) => $"{Rows}[{row}].children.filter(c => c.tagName === 'select')[0]";

    private static string Prefer(string label) =>
        $"__dom.node('#test-card').byClass('chooser-prefer')[0].children.find(b => b.textContent === {Page.Quote(label)})";

    [Fact]
    public void Fewest_stops_is_still_the_default_and_says_where_distances_are_from()
    {
        var page = Chooser();

        // New Babbage sells both: one landing.
        Assert.Equal("Platinum Bay New Babbage", page.Text($"{SelectIn(0)}.value"));
        Assert.Contains("Distances from where you are: New Babbage.", page.NodeText("#test-card"));
        Assert.Contains("Platinum Bay New Babbage · 3,500 aUEC · where you are", page.Text($"{SelectIn(0)}.options[2].textContent"));
        Assert.Contains("FPS Armor Tressler · 3,300 aUEC · on microTech", page.Text($"{SelectIn(0)}.options[1].textContent"));
        Assert.Contains("Tammany and Sons · 3,040 aUEC · 41.2 Gm", page.Text($"{SelectIn(0)}.options[0].textContent"));
    }

    [Fact]
    public void Cheapest_takes_each_line_to_its_cheapest_counter_wherever_it_is()
    {
        var page = Chooser();

        page.Do($"{Prefer("Cheapest")}.fire('click');");

        Assert.Equal("Tammany and Sons", page.Text($"{SelectIn(0)}.value"));
        Assert.Contains("each at the cheapest counter", page.NodeText("#test-card"));
    }

    [Fact]
    public void Nearest_puts_here_first_and_lists_the_counters_nearest_first()
    {
        var page = Chooser();

        page.Do($"{Prefer("Cheapest")}.fire('click');");
        page.Do($"{Prefer("Nearest")}.fire('click');");

        Assert.Equal("Platinum Bay New Babbage", page.Text($"{SelectIn(0)}.value"));
        Assert.Contains("Platinum Bay New Babbage", page.Text($"{SelectIn(0)}.options[0].textContent"));
        Assert.Contains("FPS Armor Tressler", page.Text($"{SelectIn(0)}.options[1].textContent"));
        Assert.Equal("true", page.Text($"{Prefer("Nearest")}.getAttribute('aria-pressed')"));
    }

    [Fact]
    public void A_look_alike_in_the_stash_is_said_under_the_line()
    {
        var page = Chooser();

        Assert.Contains("You have ORC-mkV Core Red at", page.Text($"{Rows}[0].textContent"));
        Assert.Contains("Terminus LEO Rest Stop", page.Text($"{Rows}[0].textContent"));
        Assert.DoesNotContain("You have", page.Text($"{Rows}[1].textContent"));
    }

    [Fact]
    public void The_card_says_a_look_alike_beside_where_to_buy_and_does_not_tick_it()
    {
        var page = new Page();
        page.Serve("/api/jobs", """
            [{"id":"adi","title":"ADI uniforms","kind":"list","done":false,"pinned":false,"haveCount":0,"totalCount":1,
              "items":[{"name":"ORC-mkV Core Grey","needed":1,"unit":"","have":false,"got":false,"where":[],
                        "alike":[{"name":"ORC-mkV Core Red","places":["Terminus LEO Rest Stop","Port Tressler"]}],
                        "buyPrice":3040,"buyAt":"Tammany and Sons"}]}]
            """);
        page.Do("atlas = []; await loadJobList();");

        var text = page.NodeText("#jobs-list");
        Assert.Contains("buy at", text);
        Assert.Contains("You have ORC-mkV Core Red at", text);
        Assert.Contains("Terminus LEO Rest Stop +1", text);
    }
}
