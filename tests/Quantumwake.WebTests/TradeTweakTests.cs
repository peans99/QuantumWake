namespace Quantumwake.WebTests;

/// <summary>
/// A saved trade run, changed after the fact: the kiosk had less stock, the
/// buyer moved its price. The run sheet carries the figures, the profit is
/// worked out from it, and the first plan stays beside what it became.
/// </summary>
public class TradeTweakTests
{
    /// <summary>96 SCU of Agricium bought at 1,234 and sold at 1,656, the sale already moved to 1,600.</summary>
    private const string Plan = """
        [{"id":"t1","title":"Agricium run","createdAt":"2026-10-24T09:00:00Z","tracked":true,"archived":"No",
          "stops":[
            {"id":"s1","placeId":"Stanton1_Lorville","place":"Lorville","note":null,"done":false,
             "actions":[{"id":"a1","kind":"buy","text":"Agricium","quantity":96,"unit":"SCU","done":false,"price":1234}]},
            {"id":"s2","placeId":"RR_MIC_L1","place":"microTech L1","note":null,"done":false,
             "actions":[{"id":"a2","kind":"sell","text":"Agricium","quantity":96,"unit":"SCU","done":false,"price":1600,
                         "was":{"quantity":96,"price":1656}}]}
          ]}]
        """;

    private static Page Panel(string plan = Plan)
    {
        var page = new Page();
        page.Serve("/api/trips", plan);
        page.Do("await loadTrips(); showTripPanel();");
        return page;
    }

    [Fact]
    public void A_trade_run_is_saved_as_priced_buys_and_sells_rather_than_notes()
    {
        var page = new Page();
        page.Serve("/api/trips", "[]");
        page.Do("""
            await saveTradeRoute({ commodity: 'Agricium', buyAt: 'Lorville', buyAtId: 'Stanton1_Lorville',
              units: 96.4, buyPrice: 1234 }, 'microTech L1', 'RR_MIC_L1', 1656);
            """);

        const string sent = "JSON.parse(__fetch.calls.filter(c => c.url === '/api/trips' && c.body).pop().body)";
        Assert.Equal("buy", page.Text($"{sent}.stops[0].actions[0].kind"));
        Assert.Equal(96, page.Count($"{sent}.stops[0].actions[0].quantity"));
        Assert.Equal(1234, page.Count($"{sent}.stops[0].actions[0].price"));
        Assert.Equal("sell", page.Text($"{sent}.stops[1].actions[0].kind"));
        Assert.Equal(1656, page.Count($"{sent}.stops[1].actions[0].price"));
        Assert.True(page.Truth($"{sent}.stops[1].note === undefined"));
    }

    [Fact]
    public void The_panel_works_the_profit_out_from_the_run_sheet_and_says_what_the_change_did()
    {
        var body = Panel().NodeText("#cargo-body");

        // 96 x 1,234 = 118,464 out; 96 x 1,600 = 153,600 in.
        Assert.Contains("Buy for 118,464 aUEC · sell for 153,600 aUEC · profit +35,136 aUEC", body);
        Assert.Contains("Planned +40,512 aUEC; your changes moved it −5,376 aUEC.", body);
        Assert.Contains("at 1,600 aUEC each", body);
        Assert.Contains("planned 96 SCU at 1,656 aUEC each", body);
    }

    /// <summary>
    /// Cutting the buy short cuts the one sale that was for the whole load,
    /// so "I only got 80" is one edit, not two.
    /// </summary>
    [Fact]
    public void Cutting_the_buy_short_cuts_the_whole_load_sale_with_it()
    {
        var page = Panel();
        page.Do("""
            {
              const body = __dom.node('#cargo-body');
              body.byClass('run-action-tweak')[0].fire('click');
              const form = body.byClass('run-tweak-form')[0];
              form.children[0].value = '80';
            }
            """);
        page.Do("await __dom.node('#cargo-body').byClass('run-tweak-form')[0].children[2].fire('click');");

        Assert.Contains("\"quantity\":80", page.BodyOf("/api/trips/t1/stops/s1/actions/a1/tweak"));
        Assert.Contains("\"price\":1234", page.BodyOf("/api/trips/t1/stops/s1/actions/a1/tweak"));

        var sale = page.BodyOf("/api/trips/t1/stops/s2/actions/a2/tweak");
        Assert.Contains("\"quantity\":80", sale);
        Assert.Contains("\"price\":1600", sale);
    }

    [Fact]
    public void Selling_more_than_was_bought_is_pointed_out()
    {
        var body = Panel(Plan.Replace("\"quantity\":96,\"unit\":\"SCU\",\"done\":false,\"price\":1234", "\"quantity\":80,\"unit\":\"SCU\",\"done\":false,\"price\":1234"))
            .NodeText("#cargo-body");

        Assert.Contains("Selling 96 SCU of the 80 bought.", body);
    }

    [Fact]
    public void The_now_card_says_the_figures_of_the_next_trade()
    {
        var page = Panel();

        Assert.Contains("Buy Agricium · 96 SCU at 1,234 aUEC each", page.NodeText("#now-trip-next"));
    }

    /// <summary>A plan with nothing priced in it - a shopping trip, a rep run - has no money line.</summary>
    [Fact]
    public void A_plan_without_prices_has_no_money_line()
    {
        var page = Panel("""
            [{"id":"t1","title":"Rep run","createdAt":"2026-10-24T09:00:00Z","tracked":true,"archived":"No",
              "stops":[{"id":"s1","placeId":"","place":"Checkmate","note":"Delivery 1 of 2","done":false,
                        "actions":[{"id":"a1","kind":"collect","text":"One crate","quantity":null,"unit":null,"done":false}]}]}]
            """);

        Assert.Equal(0, page.Count("__dom.node('#cargo-body').byClass('trade-run-money').length"));
        Assert.Equal(0, page.Count("__dom.node('#cargo-body').byClass('run-action-tweak').length"));
    }
}
