namespace Quantumwake.WebTests;

/// <summary>
/// The one detail surface, whichever view opened it.
/// </summary>
/// <remarks>
/// Every view had grown its own panel and they disagreed: the map's place card
/// knew about visits and notes, the parts row knew about size and volume, and
/// neither could say whether the thing was already yours or put it on a plan.
/// What matters here is that the shape does not change with the kind, and that
/// every line still says who is answerable for it.
/// </remarks>
public class EntityDrawerTests
{
    private const string Lorville = """
        {"kind":"place","id":"Stanton1_Lorville","name":"Lorville","subtitle":"Hurston · Stanton",
         "facts":[{"label":"Visits","value":"41 recorded, last 12 Aug 2026","source":"your logs"},
                  {"label":"Trade counter","value":"listed","source":"UEX"}],
         "holding":{"status":"18 of your things are here","detail":"P4-AR, Medpen"},
         "price":null,
         "places":[{"placeId":"Stanton1_Lorville","name":"Lorville","note":"here"},
                   {"placeId":null,"name":"TDD, Lorville","note":"sell Laranite here"}],
         "actions":["map","stop","overlay"]}
        """;

    private const string Laranite = """
        {"kind":"commodity","id":"Laranite","name":"Laranite","subtitle":"best price at TDD, Lorville",
         "facts":[{"label":"You have traded","value":"never","source":"your logs"}],
         "holding":null,
         "price":{"amount":2340,"unit":"aUEC/SCU","where":"TDD, Lorville",
                  "asOf":"2026-08-28T00:00:00Z","source":"UEX"},
         "places":[],"actions":["map","shopping","details"]}
        """;

    private static Page Opened(string kind, string id, string card)
    {
        var page = new Page();
        page.Serve($"/api/entity?kind={kind}&id={id}", card);
        page.Do($"await openEntity('{kind}', '{id}');");
        return page;
    }

    private static Page AtLorville() => Opened("place", "Stanton1_Lorville", Lorville);

    /* ---------- the same shape for every kind ---------- */

    [Fact]
    public void A_place_and_a_commodity_fill_in_the_same_panel()
    {
        Assert.Equal("Lorville", AtLorville().NodeText("#entity-name"));
        Assert.Equal("Laranite", Opened("commodity", "Laranite", Laranite).NodeText("#entity-name"));
    }

    /// <summary>
    /// The label is not decoration. This install's own logs and a community
    /// price table deserve very different amounts of trust, and a card that
    /// mixed them silently would give up the thing that makes the app worth
    /// having.
    /// </summary>
    [Fact]
    public void Every_fact_says_who_is_answerable_for_it()
    {
        var facts = AtLorville().NodeText("#entity-facts");

        Assert.Contains("41 recorded", facts);
        Assert.Contains("your logs", facts);
        Assert.Contains("UEX", facts);
    }

    /// <summary>
    /// Whether it is already yours changes every decision about it, so it sits
    /// above the price rather than below the facts.
    /// </summary>
    [Fact]
    public void What_you_already_have_is_stated()
    {
        Assert.Contains("18 of your things are here", AtLorville().NodeText("#entity-holding"));
    }

    [Fact]
    public void A_kind_with_no_answer_for_a_section_hides_it()
    {
        var page = AtLorville();

        Assert.True(page.Truth("__dom.node('#entity-price').hidden"));
        Assert.True(page.Truth("__dom.node('#entity-blurb').hidden"));
    }

    /* ---------- the price, and how old it is ---------- */

    /// <summary>
    /// The one number on this card that can cost real money if it is trusted
    /// further than it deserves. Every price here is somebody else's report of
    /// a counter that may have moved since.
    /// </summary>
    [Fact]
    public void A_price_never_appears_without_its_age()
    {
        var price = Opened("commodity", "Laranite", Laranite).NodeText("#entity-price");

        Assert.Contains("2,340 aUEC/SCU", price);
        Assert.Contains("TDD, Lorville", price);
        Assert.Contains("UEX", price);
        Assert.Contains("collected", price);
    }

    [Fact]
    public void An_age_that_cannot_be_worked_out_says_so_rather_than_guessing()
    {
        Assert.Equal("age unknown", Opened("commodity", "X", """
            {"kind":"commodity","id":"X","name":"X","subtitle":null,"facts":[],"holding":null,
             "price":{"amount":10,"unit":"aUEC","where":null,"asOf":"not a date","source":"UEX"},
             "places":[],"actions":[]}
            """).Text("ageWord('not a date')"));
    }

    /* ---------- the map-only parts ---------- */

    /// <summary>
    /// Notes, lore and the sold list are answers about a dot on the map rather
    /// than about the thing in general, so they belong to places only — and
    /// must be cleared rather than merely hidden, since their renderers append.
    /// </summary>
    [Fact]
    public void The_place_only_block_is_kept_for_places()
    {
        Assert.False(AtLorville().Truth("__dom.node('#entity-place-extra').hidden"));
    }

    [Fact]
    public void Opening_something_else_clears_the_last_places_notes()
    {
        var page = AtLorville();
        page.Do("__dom.node('#map-info-notes').textContent = 'Cargo elevator is round the back';");

        page.Serve("/api/entity?kind=commodity&id=Laranite", Laranite);
        page.Do("await openEntity('commodity', 'Laranite');");

        Assert.True(page.Truth("__dom.node('#entity-place-extra').hidden"));
        Assert.Equal("", page.NodeText("#map-info-notes"));
    }

    /* ---------- acting on it ---------- */

    [Fact]
    public void The_actions_offered_are_the_ones_the_kind_supports()
    {
        Assert.Equal(3, AtLorville().Count("__dom.node('#entity-actions').children.length"));
        Assert.Contains("Add as a stop", AtLorville().NodeText("#entity-actions"));
        Assert.DoesNotContain("Add to shopping", AtLorville().NodeText("#entity-actions"));
    }

    [Fact]
    public void Adding_a_place_as_a_stop_sends_that_place()
    {
        var page = AtLorville();
        page.Serve("/api/trips", "[]");

        page.Do("await runEntityAction('stop', { kind:'place', id:'Stanton1_Lorville', name:'Lorville' }, __dom.node('#entity-actions'));");

        var body = page.BodyOf("/api/trips/stops");
        Assert.Contains("\"placeId\":\"Stanton1_Lorville\"", body);
        Assert.Contains("\"place\":\"Lorville\"", body);
    }

    [Fact]
    public void Adding_a_commodity_to_the_shopping_list_sends_its_name()
    {
        var page = Opened("commodity", "Laranite", Laranite);
        page.Serve("/api/jobs/collect", """{"id":"j1","title":"Shopping list"}""");

        page.Do("await runEntityAction('shopping', { kind:'commodity', id:'Laranite', name:'Laranite' }, __dom.node('#entity-actions'));");

        Assert.Contains("\"name\":\"Laranite\"", page.BodyOf("/api/jobs/collect"));
    }

    /// <summary>
    /// Only a place has a dot of its own. Passing a commodity name to centreOn
    /// looked for a place id that was never going to exist, and the button did
    /// nothing at all.
    /// </summary>
    /// <remarks>
    /// searchCommodity rather than writing the box directly: it also drops what
    /// the cargo panel was describing. Setting the term alone recoloured the map
    /// and left the panel beside it still describing the station opened before.
    /// </remarks>
    [Fact]
    public void Showing_a_commodity_on_the_map_searches_for_it_and_drops_the_old_station()
    {
        var page = Opened("commodity", "Laranite", Laranite);
        page.Do("cargo.place = { id: 'ELSEWHERE', name: 'Somewhere else' };");

        page.Do("await runEntityAction('map', { kind:'commodity', id:'Laranite', name:'Laranite' }, __dom.node('#entity-actions'));");

        Assert.Equal("Laranite", page.Text("__dom.node('#map-search').value"));
        Assert.Equal("null", page.Text("String(cargo.place)"));
    }

    /// <summary>
    /// A component has no dot either, so it goes to the nearest thing the map
    /// can find: the cheapest seller the catalogue names.
    /// </summary>
    /// <remarks>
    /// A seller is a UEX terminal name and the atlas holds place names —
    /// "Platinum Bay, Baijini Point" against "Baijini Point" — so an exact
    /// search finds nothing at all. This asserts the map actually arrived
    /// somewhere; an earlier version only checked that a function had been
    /// called, and passed while the search was still coming up empty.
    /// </remarks>
    [Fact]
    public void Showing_a_component_on_the_map_finds_the_place_behind_the_terminal_name()
    {
        var page = new Page();
        page.Do("atlas = [{ rawId: 'BAIJINI', name: 'Baijini Point', kind: 'Station', visits: 0 }];");

        page.Do("""
            await runEntityAction('map',
              { kind:'part', id:'varipuck_s5', name:'VariPuck',
                places:[{ placeId:null, name:'Platinum Bay, Baijini Point', note:'4,000 aUEC' }] },
              __dom.node('#entity-actions'));
            """);

        Assert.Equal("BAIJINI", page.Text("cargo.place.id"));
    }

    /// <summary>
    /// And a seller the atlas cannot place leaves the map where it was, rather
    /// than centring on whatever happened to match loosely enough.
    /// </summary>
    [Fact]
    public void A_seller_the_map_cannot_name_moves_nothing()
    {
        var page = new Page();
        page.Do("atlas = [{ rawId: 'BAIJINI', name: 'Baijini Point', kind: 'Station', visits: 0 }];");

        page.Do("""
            await runEntityAction('map',
              { kind:'part', id:'x', name:'X',
                places:[{ placeId:null, name:'Nowhere In Particular', note:'' }] },
              __dom.node('#entity-actions'));
            """);

        Assert.Equal("null", page.Text("String(cargo.place)"));
    }

    /* ---------- pinning ---------- */

    /// <summary>
    /// The endpoint binds visible from the query, so a JSON body was ignored
    /// and answered 400 — which fetch does not throw for, so the button said it
    /// had worked every single time.
    /// </summary>
    [Fact]
    public void Pinning_to_the_overlay_asks_the_way_the_endpoint_reads_it()
    {
        var page = AtLorville();
        page.Serve("/api/overlay?visible=true", """{"available":true,"visible":true}""");

        page.Do("await runEntityAction('overlay', { kind:'place', id:'x', name:'X' }, __dom.node('#entity-actions'));");

        Assert.Contains("POST /api/overlay?visible=true", page.Fetched());
    }

    [Fact]
    public void A_refused_pin_says_so_rather_than_claiming_success()
    {
        var page = AtLorville();
        page.Fail("/api/overlay?visible=true", 409, """{"message":"No overlay in this process."}""");

        page.Do("await runEntityAction('overlay', { kind:'place', id:'x', name:'X' }, __dom.node('#entity-actions'));");

        Assert.Equal("failed", page.NodeText("#entity-actions"));
    }

    /* ---------- opening and closing ---------- */

    [Fact]
    public void Clicking_the_same_thing_again_closes_it()
    {
        var page = AtLorville();
        Assert.False(page.Truth("__dom.node('#entity-drawer').hidden"));

        page.Do("await openEntity('place', 'Stanton1_Lorville');");

        Assert.True(page.Truth("__dom.node('#entity-drawer').hidden"));
    }

    /// <summary>
    /// Two clicks are two requests, and the first can land second — which had
    /// the panel describing whatever was clicked before.
    /// </summary>
    /// <remarks>
    /// The ticket is what is tested rather than the interleaving: the fetch
    /// stub answers in the order it is asked, so an out-of-order landing cannot
    /// be staged here at all. A first attempt tried and passed against the
    /// unfixed page, which is worse than no test.
    /// </remarks>
    [Fact]
    public void Only_the_newest_request_is_still_wanted()
    {
        var page = new Page();

        page.Do("first = entityTicket(); second = entityTicket();");

        Assert.False(page.Truth("first()"));
        Assert.True(page.Truth("second()"));
    }

    /// <summary>
    /// Closing counts as moving on: a reply still in flight must not reopen a
    /// drawer the pilot has just dismissed.
    /// </summary>
    [Fact]
    public void Closing_makes_a_reply_in_flight_unwanted()
    {
        var page = new Page();

        page.Do("pending = entityTicket();");
        Assert.True(page.Truth("pending()"));

        page.Do("closeEntity();");

        Assert.False(page.Truth("pending()"));
    }

    /// <summary>
    /// Nothing known is not an error worth a dialog — but leaving the previous
    /// card up would have the panel answering for the wrong thing.
    /// </summary>
    [Fact]
    public void A_thing_nothing_is_known_about_closes_the_panel_rather_than_lying()
    {
        var page = AtLorville();

        page.Fail("/api/entity?kind=place&id=nowhere", 404, """{"problem":"Nothing is known about that."}""");
        page.Do("await openEntity('place', 'nowhere');");

        Assert.True(page.Truth("__dom.node('#entity-drawer').hidden"));
    }
}
