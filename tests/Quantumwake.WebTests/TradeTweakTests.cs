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
        page.Serve("/api/trips/t1/stops/s1/actions/a1/tweak", "{}");
        page.Serve("/api/trips/t1/stops/s2/actions/a2/tweak", "{}");
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

    /// <summary>A circuit keeps its four trades as priced lines, the return load bought where the outbound one is sold (1.2.33).</summary>
    [Fact]
    public void A_circuit_is_saved_as_priced_trades_rather_than_notes()
    {
        var page = new Page();
        var stops = page.Text("""
            JSON.stringify(circuitStops({ commodity: 'Agricium', buyAt: 'Lorville', buyAtId: 'L', buyPrice: 1234, units: 96.7,
              sellAt: 'Area18', sellAtId: 'A', sellPrice: 1656, returnCommodity: 'Waste', returnBuyAt: 'Area18', returnBuyPrice: 10,
              returnUnits: 96, returnSellAt: 'Lorville', returnSellAtId: 'L', returnSellPrice: 40 }))
            """);

        Assert.Equal("""[{"placeId":"L","place":"Lorville","actions":[{"kind":"buy","text":"Agricium","quantity":96,"unit":"SCU","price":1234}]},{"placeId":"A","place":"Area18","actions":[{"kind":"sell","text":"Agricium","quantity":96,"unit":"SCU","price":1656},{"kind":"buy","text":"Waste","quantity":96,"unit":"SCU","price":10}]},{"placeId":"L","place":"Lorville","actions":[{"kind":"sell","text":"Waste","quantity":96,"unit":"SCU","price":40}]}]""", stops);
        Assert.DoesNotContain("note", stops);
    }

    [Fact]
    public void A_circuit_whose_return_load_is_elsewhere_gets_its_own_stop()
    {
        var page = new Page();
        Assert.Equal(4, page.Count("""
            circuitStops({ commodity: 'A', buyAt: 'X', buyPrice: 1, units: 1, sellAt: 'Y', sellPrice: 2,
              returnCommodity: 'B', returnBuyAt: 'Z', returnBuyPrice: 1, returnUnits: 1, returnSellAt: 'X', returnSellPrice: 2 }).length
            """));
    }

    /// <summary>A shopping run is one priced buy per item at its counter, not a note listing them (1.2.33).</summary>
    [Fact]
    public void A_shopping_run_is_saved_as_priced_buys_per_counter()
    {
        var page = new Page();
        var stops = page.Text("""
            JSON.stringify(shoppingRunStops([
              { item: { name: 'Quantanium', needed: 12, unit: 'SCU' }, sellers: [{ terminal: 'Shubin', placeId: 'S', price: 88 }] },
              { item: { name: 'Medpen', needed: 4 }, sellers: [{ terminal: 'Shubin', placeId: 'S', price: 1500 }] },
              { item: { name: 'Ore pod', needed: 2, unit: 'cSCU' }, sellers: [{ terminal: 'Area18', placeId: 'A', price: 0 }] },
              { item: { name: 'Not chosen', needed: 1 }, sellers: [] },
            ], new Map([['Quantanium', 'Shubin'], ['Medpen', 'Shubin'], ['Ore pod', 'Area18']])))
            """);

        Assert.Equal("""[{"placeId":"S","place":"Shubin","actions":[{"kind":"buy","text":"Quantanium","quantity":12,"unit":"SCU","price":88},{"kind":"buy","text":"Medpen","quantity":4,"unit":"units","price":1500}]},{"placeId":"A","place":"Area18","actions":[{"kind":"buy","text":"Ore pod (cSCU)","quantity":2,"unit":null,"price":null}]}]""", stops);
    }

    /// <summary>A trade run keeps the ship it was planned for, with the hold it assumed (1.2.35).</summary>
    [Fact]
    public void A_trade_run_keeps_the_ship_it_was_planned_for()
    {
        var page = new Page();
        page.Serve("/api/trips", "[]");
        page.Do("""
            libraryStats = { ships: [{ name: 'Crusader C2 Hercules', className: 'CRUS_Starlifter_C2', reference: { cargoScu: 696 } }] };
            await saveTradeRoute({ commodity: 'Agricium', buyAt: 'Lorville', units: 96, buyPrice: 1234 }, 'Area18', 'A', 1656);
            """);

        const string sent = "JSON.parse(__fetch.calls.filter(c => c.url === '/api/trips' && c.body).pop().body).ship";
        Assert.Equal("Crusader C2 Hercules", page.Text($"{sent}.name"));
        Assert.Equal(696, page.Count($"{sent}.scu"));
        Assert.Equal("community", page.Text($"{sent}.scuSource"));
    }

    [Fact]
    public void The_plan_says_when_the_ship_flown_is_not_the_one_planned()
    {
        var page = new Page();
        page.Do("nowState = { inGame: true, ship: 'Drake Cutlass Black' };");
        var other = page.Text("tripShipNotice({ ship: { name: 'Crusader C2 Hercules', scu: 696, scuSource: 'community' } }).textContent");
        Assert.Contains("Planned for Crusader C2 Hercules · 696 SCU (reference data)", other);
        Assert.Contains("you are flying the Drake Cutlass Black now", other);

        page.Do("nowState = { inGame: true, ship: 'Crusader C2 Hercules' };");
        Assert.Contains("flying it now", page.Text("tripShipNotice({ ship: { name: 'Crusader C2 Hercules', scu: 696 } }).textContent"));
        Assert.True(page.Truth("tripShipNotice({ stops: [] }) === null"));
    }
}
