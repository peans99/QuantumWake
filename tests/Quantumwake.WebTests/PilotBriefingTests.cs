namespace Quantumwake.WebTests;

/// <summary>The Now-page launch checklist stays small, but its data is actionable.</summary>
public class PilotBriefingTests
{
    private const string Briefing = """
        {
          "locationId":"Area18","location":"Area18","tripId":"t1","tripTitle":"Supply run",
          "stops":[{"id":"s1","placeId":"GrimHEX","place":"GrimHEX","note":"Pick up armour"}],
          "shopping":[{"jobId":"j1","jobTitle":"Hangar list","name":"MedPen","needed":4,"unit":"","terminal":"Cubby Blast, Area18","price":1000,"kind":"item"}],
          "trade":[{"commodity":"Laranite","buyHere":25,"sellThere":31,"sellTerminal":"TDD, Lorville","marginPerScu":6}],
          "services":[{"name":"Shops","status":"items listed","dataEnabled":true},{"name":"Repair","status":"not reported","dataEnabled":false}],
          "stash":[{"name":"P4-AR","category":"Weapons","lastSeen":"2026-08-23T10:00:00Z"}]
        }
        """;

    private static Page AtArea18()
    {
        var page = new Page();
        page.Serve("/api/briefing", Briefing);
        page.Serve("/api/trips", "[]");
        page.Do("renderNow({ connected:true, inGame:true, locationId:'Area18', location:'Area18', confidence:'High', recentEvents:[] });");
        return page;
    }

    [Fact]
    public void It_joins_the_next_stop_shopping_trade_services_and_stash_at_the_live_place()
    {
        var page = AtArea18();

        Assert.False(page.Truth("__dom.node('#now-briefing-card').hidden"));
        Assert.Contains("Supply run", page.NodeText("#briefing-sub"));
        Assert.Contains("GrimHEX", page.NodeText("#briefing-stops"));
        Assert.Contains("MedPen", page.NodeText("#briefing-shopping"));
        Assert.Contains("Laranite", page.NodeText("#briefing-trade"));
        Assert.Contains("Shops: items listed", page.NodeText("#briefing-services"));
        Assert.Contains("P4-AR", page.NodeText("#briefing-stash"));
        Assert.Contains("cargo in your hold is not recorded", page.NodeText("#briefing-trade"));
    }

    [Fact]
    public void Adding_here_to_the_plan_sends_the_live_place()
    {
        var page = AtArea18();

        page.Do("await addBriefingStop();");

        var body = page.BodyOf("/api/trips/stops");
        Assert.Contains("\"placeId\":\"Area18\"", body);
        Assert.Contains("\"place\":\"Area18\"", body);
    }

    [Fact]
    public void Now_card_collapse_is_saved_by_card_name()
    {
        var page = new Page();

        page.Do("initNowCardCollapsers(); __dom.node('#now-briefing-card').byClass('now-collapse')[0].fire('click');");

        Assert.True(page.Truth("__dom.node('#now-briefing-card').classList.contains('collapsed')"));
        Assert.Contains("briefing", page.Text("localStorage.getItem('qw-now-collapsed-cards')"));
    }

    [Fact]
    public void A_now_card_can_be_hidden_and_restored_from_the_hidden_cards_tray()
    {
        var page = new Page();

        page.Do("initNowCardCollapsers(); __dom.node('#now-briefing-card').byClass('now-hide')[0].fire('click');");

        Assert.True(page.Truth("__dom.node('#now-briefing-card').classList.contains('user-hidden')"));
        Assert.Contains("Show briefing", page.NodeText("#now-hidden-card-list"));
        Assert.Contains("briefing", page.Text("localStorage.getItem('qw-now-hidden-cards')"));

        page.Do("__dom.node('#now-hidden-card-list').byClass('ghost')[0].fire('click');");

        Assert.False(page.Truth("__dom.node('#now-briefing-card').classList.contains('user-hidden')"));
    }

    /// <summary>
    /// Pinning has to say when it did not happen.
    /// </summary>
    /// <remarks>
    /// The bare server has no overlay to show and answers 409, which a browser
    /// treats as a perfectly good response — so for as long as this card has
    /// existed the button reported success and did nothing. The release note
    /// for the fix claimed the briefing said so before the briefing did.
    /// </remarks>
    [Fact]
    public void Pinning_the_briefing_says_so_when_there_is_no_overlay()
    {
        var page = AtArea18();
        page.Fail("/api/overlay?visible=true", 409, """{"message":"No overlay in this process."}""");

        page.Do("await pinBriefingToOverlay();");

        Assert.Equal("no overlay", page.NodeText("#briefing-overlay"));
    }

    [Fact]
    public void Pinning_the_briefing_asks_the_way_the_endpoint_reads_it()
    {
        var page = AtArea18();
        page.Serve("/api/overlay?visible=true", """{"available":true,"visible":true}""");

        page.Do("await pinBriefingToOverlay();");

        Assert.Contains("POST /api/overlay?visible=true", page.Fetched());
        Assert.Equal("✓ pinned", page.NodeText("#briefing-overlay"));
    }

    [Fact]
    public void A_service_filter_keeps_only_matching_places_and_marks_the_place_card()
    {
        var page = new Page();

        // The place card is the shared drawer now, so opening one asks the
        // server what is known about the place before the map-only parts of it
        // are filled in.
        page.Serve("/api/entity?kind=place&id=clinic", """
            {"kind":"place","id":"clinic","name":"Seraphim","subtitle":null,
             "facts":[],"holding":null,"price":null,"places":[],"actions":["map"]}
            """);

        page.Do("""
            atlas = [
              { rawId: 'clinic', name: 'Seraphim', kind: 'Station', visits: 1, system: '', body: '', lastVisit: null },
              { rawId: 'shop', name: 'Area18', kind: 'City', visits: 1, system: '', body: '', lastVisit: null }
            ];
            mapServicesByPlace.set('clinic', ['clinic']);
            mapServicesByPlace.set('shop', ['shop']);
            selectMapService('clinic');
            await showMapInfo(atlas[0]);
            """);

        Assert.Equal("clinic", page.Text("mapServiceFilter"));
        Assert.Equal(1, page.Count("__dom.node('#starmap').byClass('map-node').length"));
        Assert.Contains("✚", page.NodeText("#map-info-services"));
        Assert.Contains("Clinic", page.NodeText("#map-info-services"));
    }

    [Fact]
    public void A_briefing_that_cannot_be_fetched_is_not_left_describing_the_last_place()
    {
        var page = AtArea18();
        Assert.Contains("GrimHEX", page.NodeText("#briefing-stops"));

        // Travel, and let the fetch for the new place fail.
        page.Do("delete __fetch.routes['/api/briefing'];");
        page.Do("renderNow({ connected:true, inGame:true, locationId:'Lorville', location:'Lorville', confidence:'High', recentEvents:[] });");

        // Area18's stops and stash must not stay on screen labelled Lorville.
        Assert.True(page.Truth("__dom.node('#now-briefing-card').hidden"));

        // And the next render tries again, rather than waiting for another move.
        var asked = page.Fetched().Count(url => url == "GET /api/briefing");
        page.Do("renderNow({ connected:true, inGame:true, locationId:'Lorville', location:'Lorville', confidence:'High', recentEvents:[] });");
        Assert.Equal(asked + 1, page.Fetched().Count(url => url == "GET /api/briefing"));
    }
}
