namespace Quantumwake.WebTests;

/// <summary>
/// Every ship name opens the ship's Reference window (0.20.33), as every
/// item name has since 0.20.32.
/// </summary>
/// <remarks>
/// The Reference pages linked a hull to its card; the pilot's own pages did
/// not. A Fleet card, a Crew row, a Sessions row and the Now page's ship card
/// all named the ship as words, so the card with the spec sheet, the shops
/// and the stock parts was reachable only by going to Reference and
/// searching for it again.
/// </remarks>
public class ShipLinksTests
{
    private static string Links(string selector) => $"__dom.node('{selector}').byClass('ref-link')";

    private static void AssertOpenedOn(Page page, string title)
    {
        Assert.False(page.Truth("__dom.node('#ref-modal').hidden"));
        Assert.Equal(title, page.NodeText("#ref-modal-title"));
        Assert.Equal("Ship", page.NodeText("#ref-modal-kicker"));
    }

    [Fact]
    public void A_fleet_card_opens_its_ship_by_class()
    {
        var page = new Page();
        page.Do("""
            excludedShips = new Set(); favouriteShips = new Set(); hangarComparison = new Set(); shipPictureStyle = 'tint';
            libraryStats = { fleetSize: 1, fleetHistory: [], ships: [
              {name:'Drake Corsair', className:'DRAK_Corsair', sorties:4, estimatedTime:'02:00:00', lastFlown:new Date().toISOString(), reference:{isSpaceship:true}}
            ]};
            renderFleet(libraryStats);
            """);

        var link = $"__dom.node('#fleet-ships').byClass('ship-name')[0].byClass('ref-link')[0]";
        Assert.Equal("Corsair", page.Text($"{link}.textContent"));
        page.Do($"{link}.click();");
        AssertOpenedOn(page, "DRAK Corsair");
    }

    [Fact]
    public void A_crew_row_opens_the_ship_you_were_both_aboard()
    {
        var page = new Page();
        page.Serve("/api/crew?days=0", "[]");
        page.Serve("/api/crew/ships?days=0", """
            [{"handle":"Velaris","ship":"Tumbril Cyclone MT","owner":"nekron","times":2,
              "first":"2026-05-10T00:00:00+00:00","last":"2026-05-10T00:00:00+00:00"}]
            """);
        page.Do("__dom.node('#crew-period').value = '0'; await loadCrew();");

        page.Do($"{Links("#crew-ships")}[0].click();");
        AssertOpenedOn(page, "Tumbril Cyclone MT");
    }

    [Fact]
    public void A_casualties_bar_opens_the_ship_you_died_in()
    {
        var page = new Page();
        page.Serve("/api/casualties?days=0", """
            {"deaths":4,"incapacitations":0,"sessionsWithDeaths":1,"estimatedFees":0,
             "byPlace":[],"byShip":[{"ship":"Drake Cutlass Black","deaths":4}],
             "bedsUsed":[],"bedKinds":[],"wokeAt":[],"fees":[]}
            """);
        page.Do("__dom.node('#casualties-period').value = '0'; await loadCasualties();");

        var link = "__dom.node('#casualties-ships').byClass('place-link')[0]";
        Assert.Equal("Open this ship", page.Text($"{link}.title"));
        page.Do($"{link}.click();");
        AssertOpenedOn(page, "Drake Cutlass Black");
    }

    /// <summary>The row opens the debrief; the ship on it opens the card and leaves the row closed.</summary>
    [Fact]
    public void A_sessions_row_ship_opens_its_card_without_opening_the_debrief()
    {
        var page = new Page();
        page.Do("""
            allSessions = [{ id:'s1', startedAt:'2026-08-20T20:00:00Z', inGame:5100, menu:300,
              primaryShip:'RSI Zeus', lastLocation:'HUR-L1', shard:'EU-1', shards:1, jumps:1, contracts:1, deaths:1, incapacitations:2 }];
            expandedSessionId = null;
            renderSessions();
            """);

        Assert.Equal("RSI Zeus", page.Text($"{Links("#sessions-table tbody")}[0].textContent"));
        page.Do($"{Links("#sessions-table tbody")}[0].click();");
        AssertOpenedOn(page, "RSI Zeus");
        Assert.True(page.Truth("expandedSessionId === null"));
    }

    [Fact]
    public void The_now_page_ship_opens_its_card()
    {
        var page = new Page();
        page.Do("renderNowShip('Drake Cutter');");

        Assert.Equal("Drake Cutter", page.NodeText("#now-ship"));
        page.Do($"{Links("#now-ship")}[0].click();");
        AssertOpenedOn(page, "Drake Cutter");
    }

    /// <summary>
    /// A trade's things open the window for what each is, as the server says:
    /// the ship card for a hull, the item card for a favour, the commodity
    /// drawer for an ore. A class the server cannot place stays words.
    /// </summary>
    [Fact]
    public void Wikelo_things_open_the_window_for_what_they_are()
    {
        var page = new Page();
        page.Serve("/api/wikelo", """
            {"available":true,"standings":[],
             "trades":[{"id":"t1","title":"Fortune ship for you","description":"","group":"vehicles","reputation":0,"minStanding":null,"retired":false,
               "rewards":[{"class":"MISC_Fortune_Collector_Industrial","name":"MISC Fortune Wikelo Special","count":1,"unit":"","kind":"ship"}],
               "requirements":[{"class":"banu_favour","name":"Wikelo Favor","count":3,"unit":"","kind":"item","have":false,"where":[]},
                               {"class":"Quantanium","name":"Quantainium","count":24,"unit":"SCU","kind":"commodity","commodity":"Quantanium","have":false,"where":[]},
                               {"class":"mystery","name":"Mystery Part","count":1,"unit":"","kind":null,"have":false,"where":[]}],
               "trackedJobId":null,"trackedDone":false}]}
            """);
        page.Do("wikeloGroup = 'vehicles'; await loadWikelo();");

        Assert.Equal(3, page.Count($"{Links("#wikelo-list")}.length"));
        Assert.Contains("1× Mystery Part", page.NodeText("#wikelo-list"));
        Assert.Equal("Open this commodity", page.Text($"{Links("#wikelo-list")}[2].title"));

        page.Do($"{Links("#wikelo-list")}[0].click();");
        AssertOpenedOn(page, "MISC Fortune Collector Industrial");

        page.Do($"{Links("#wikelo-list")}[1].click();");
        Assert.Equal("Item", page.NodeText("#ref-modal-kicker"));

        // The ore opens the drawer under the Market's spelling, not the file's.
        page.Do($"await {Links("#wikelo-list")}[2].fire('click');");
        Assert.Equal("Quantanium", page.NodeText("#commodity-name"));
    }

    /// <summary>
    /// The rig slot used to be a button, which cannot hold a link, so the
    /// fitted part was the one component name on the page that opened
    /// nothing. The slot still selects the port; the name opens the card.
    /// </summary>
    [Fact]
    public void A_garage_rig_part_opens_its_card_and_the_slot_still_selects_the_port()
    {
        var page = new Page();
        page.Serve("/api/garage", GaragePageTests.Garage);
        page.Serve("/api/garage/AEGS_Gladius", GaragePageTests.Gladius);
        page.Serve("/api/garage/builds?ship=AEGS_Gladius", "[]");
        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", """{"port":{"portId":"p1","kinds":[],"minSize":1,"maxSize":1},"options":[]}""");
        page.Do("await loadGarage();");

        var slot = "__dom.node('#garage-rig-left').descendants().find(n => n.classList.contains('garage-rig-slot'))";
        Assert.Equal("button", page.Text($"{slot}.getAttribute('role')"));

        page.Do($"{slot}.byClass('ref-link')[0].click();");
        Assert.False(page.Truth("__dom.node('#ref-modal').hidden"));
        Assert.Equal("Item", page.NodeText("#ref-modal-kicker"));
        Assert.DoesNotContain("GET /api/garage/AEGS_Gladius/options?port=p1", page.Fetched());

        page.Do($"await {slot}.fire('click');");
        Assert.Contains("GET /api/garage/AEGS_Gladius/options?port=p1", page.Fetched());
    }
}
