namespace Quantumwake.WebTests;

/// <summary>
/// UEX's player marketplace on the Garage: who is offering a part this
/// week, said as an asking price beside the terminal price, with the way to
/// UEX - never as a market price, and never silently when the feed is off.
/// </summary>
public class GarageMarketTests
{
    private const string Garage = """
        {"known":true,"dump":"4.10.0-LIVE.12519617",
         "mine":[{"class":"AEGS_Gladius","name":"Aegis Gladius","sorties":12,"lastFlown":"2026-09-13T00:00:00+00:00"}],
         "all":[{"class":"AEGS_Gladius","name":"Aegis Gladius","manufacturer":"Aegis Dynamics","role":"Light Fighter","size":2}]}
        """;

    private const string Gladius = """
        {"known":true,"dump":"4.10.0-LIVE.12519617",
         "ship":{"class":"AEGS_Gladius","name":"Aegis Gladius","manufacturer":"Aegis Dynamics","role":"Light Fighter","career":"Combat",
                 "size":2,"crew":1,"hullMass":48552,"health":6110,"cargoScu":0,"quantumFuel":600,"hydrogenFuel":1600,
                 "flight":{"scm":226,"boost":520,"max":1193,"pitch":68,"yaw":52,"roll":200},"powerPools":{},"stockMass":55646},
         "sheet":{"class":"AEGS_Gladius","name":"Aegis Gladius","mass":55646,
                  "shields":{"em":22078,"ir":8711,"emByGroup":{}},"quantum":{"em":37164,"ir":7263,"emByGroup":{}},
                  "power":{"available":16,"usedShields":14,"usedQuantum":12,"usedByGroup":{},"overShields":false,"overQuantum":false},
                  "cooling":{"generated":68,"usedShields":36.1,"usedQuantum":30.1,"loadShields":0.531,"loadQuantum":0.443},
                  "shield":{"hp":6336,"regen":1394,"generators":2,"poolLimit":2},
                  "weapons":{"fixedDps":1944.5,"fixedSustainedDps":1186.9,"fixedAlpha":119.4,"turretDps":0,"missileDamage":21000,"missiles":6,"guns":[]},
                  "quantumDrive":{"drive":"Beacon","speed":161410900,"spoolTime":4.4,"cooldown":11.9,"range":32223415682,"fuelPerGm":18.62},
                  "armorSignals":{"em":1.13,"ir":1.13,"crossSection":1},"crossSection":{"x":6923,"y":1731,"z":8654},"parts":[],"notes":[]},
         "ports":[{"portId":"p1","hardpoint":"Hardpoint_cooler_left","group":"Cooler","minSize":1,"maxSize":1,"class":"COOL_AEGS_S01_Bracer_SCItem","name":"Bracer","stockClass":"COOL_AEGS_S01_Bracer_SCItem","changed":false,
                   "fitted":{"type":"Cooler","name":"Bracer","size":1,"grade":2,"manufacturer":"Aegis Dynamics","makerCode":"AEGS","em":1490,"ir":7260,"coolantGen":25,"powerUseMax":3}}]}
        """;

    private const string Listing = """
        {"id":172424,"title":"Glacier 3pip","operation":"sell","type":"item","itemUuid":"c-glacier",
         "price":8800000,"unit":"unit","inStock":2,"quality":null,"location":"Admin - Ruin Station","seller":"penetrator3000",
         "added":"2026-09-14T10:00:00+00:00","expires":null,"photo":null,"soldOut":false,"url":"https://uexcorp.space/marketplace/item/info/glacier-3pip-0Y9jLAxXhB/"}
        """;

    private static string Options(string listings, bool marketKnown = true) => $$"""
        {"port":{"portId":"p1","hardpoint":"Hardpoint_cooler_left","kinds":["Cooler"],"minSize":1,"maxSize":1,"fitted":"COOL_AEGS_S01_Bracer_SCItem"},
         "pricesKnown":true,"marketKnown":{{(marketKnown ? "true" : "false")}},
         "options":[
           {"part":{"class":"COOL_AEGS_S01_Bracer_SCItem","type":"Cooler","size":1,"grade":2,"name":"Bracer","manufacturer":"Aegis Dynamics","makerCode":"AEGS","em":1490,"ir":7260,"coolantGen":25,"powerUseMax":3},
            "price":null,"listings":[],"shops":[]},
           {"part":{"class":"COOL_JUST_S01_Glacier_SCItem","uuid":"c-glacier","type":"Cooler","size":1,"grade":1,"name":"Glacier","manufacturer":"Juggernaut","makerCode":"JUST","em":1490,"ir":7920,"coolantGen":38,"powerUseMax":3},
            "price":12000,"listings":[{{listings}}],"shops":[{"terminal":"Dumper's Depot","placeId":"P1","place":"Area18","system":"Stanton","price":12000}]}]}
        """;

    private static Page Opened(string market)
    {
        var page = new Page();
        page.Serve("/api/garage", Garage);
        page.Serve("/api/garage/AEGS_Gladius", Gladius);
        page.Serve("/api/garage/builds?ship=AEGS_Gladius", "[]");
        page.Serve("/api/garage/AEGS_Gladius/market", market);
        page.Do("await loadGarage();");
        return page;
    }

    /// <summary>
    /// A player's ask sits under the terminal price as its own kind of route,
    /// with who, where, how many more, and the link to UEX - where the deal
    /// is made. The chip is not the terminal chip: an ask is not a price.
    /// </summary>
    [Fact]
    public void A_candidate_shows_the_cheapest_player_ask_beside_the_terminal_price()
    {
        var page = Opened("""{"enabled":true,"fetchedAt":"2026-09-15T10:00:00+00:00","total":500,"components":41,"listings":[]}""");
        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", Options(Listing + "," + Listing.Replace("172424", "172425").Replace("8800000", "9900000")));
        page.Do("await selectBenchPort('p1');");

        var glacier = "__dom.node('#garage-bench-panel').descendants().filter(n => n.classList.contains('candidate')).find(n => n.textContent.includes('Glacier'))";
        var text = page.Text($"{glacier}.textContent");

        Assert.Contains("12,000 aUEC", text);
        Assert.Contains("Player listing", text);
        Assert.Contains("8,800,000 aUEC", text);
        Assert.Contains("by penetrator3000 at Admin - Ruin Station", text);
        Assert.Contains("2 in stock", text);
        Assert.Contains("+1", text);
        Assert.DoesNotContain("9,900,000", text);

        var link = $"{glacier}.descendants().find(n => n.classList.contains('uex-link'))";
        Assert.Equal("https://uexcorp.space/marketplace/item/info/glacier-3pip-0Y9jLAxXhB/", page.Text($"{link}.href"));
        Assert.Equal("_blank", page.Text($"{link}.target"));
        Assert.Equal("noopener", page.Text($"{link}.rel"));
        Assert.True(page.Truth($"{glacier}.descendants().some(n => n.classList.contains('acquisition') && n.classList.contains('players'))"));

        // The Bracer has no ask and says nothing about players: no chip, no dash.
        var bracer = "__dom.node('#garage-bench-panel').descendants().filter(n => n.classList.contains('candidate')).find(n => n.textContent.includes('Bracer'))";
        Assert.DoesNotContain("Player listing", page.Text($"{bracer}.textContent"));
    }

    /// <summary>With the feed off, the bench says where to turn it on rather than showing nobody selling.</summary>
    [Fact]
    public void Without_the_feed_the_bench_says_so_once()
    {
        var page = Opened("""{"enabled":false,"fetchedAt":null,"total":0,"components":0,"listings":[]}""");
        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", Options("", marketKnown: false));
        page.Do("await selectBenchPort('p1');");

        Assert.Contains("player listings need the Player marketplace feed (Settings)", page.NodeText("#garage-bench-panel"));
        Assert.False(page.Truth("__dom.node('#garage-market').hidden"));
        Assert.Contains("Player listings are off", page.NodeText("#garage-market-note"));
        Assert.Equal("", page.NodeText("#garage-market-body"));
    }

    /// <summary>
    /// The panel under the bench: every part fit for this ship that a player
    /// is offering, newest first, with the part's own card and a way onto the
    /// bench at the port it would go in. The note says how much of the feed
    /// that is, and that it is a week's advertisements.
    /// </summary>
    [Fact]
    public void The_market_panel_lists_parts_fit_for_the_ship_and_opens_the_bench_at_their_port()
    {
        var page = Opened($$"""
            {"enabled":true,"fetchedAt":"2026-09-15T10:00:00+00:00","total":500,"components":41,
             "listings":[{"listing":{{Listing}},
                          "part":{"class":"COOL_JUST_S01_Glacier_SCItem","uuid":"c-glacier","type":"Cooler","size":1,"grade":1,"name":"Glacier","manufacturer":"Juggernaut","makerCode":"JUST","coolantGen":38},
                          "ports":["p1"]}]}
            """);

        Assert.Contains("GET /api/garage/AEGS_Gladius/market", page.Fetched());
        Assert.False(page.Truth("__dom.node('#garage-market').hidden"));

        var note = page.NodeText("#garage-market-note");
        Assert.Contains("1 of the newest 500 UEX marketplace listings", note);
        Assert.Contains("fit the Aegis Gladius", note);
        Assert.Contains("asking prices, not market prices", note);

        var body = page.NodeText("#garage-market-body");
        Assert.Contains("Glacier", body);
        Assert.Contains("Juggernaut", body);
        Assert.Contains("listed as “Glacier 3pip”", body);
        Assert.Contains("8,800,000 aUEC", body);
        Assert.Contains("by penetrator3000", body);
        Assert.Contains("posted", body);
        Assert.True(page.Truth("__dom.node('#garage-market-body').descendants().some(n => n.classList.contains('part-pic') && n.src === '/api/garage/picture/c-glacier')"));

        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", Options(Listing));
        page.Do("await __dom.node('#garage-market-body').descendants().find(n => n.tagName === 'button' && n.textContent === 'Bench').fire('click');");
        Assert.Contains("GET /api/garage/AEGS_Gladius/options?port=p1", page.Fetched());
        Assert.Contains("now fitted: Bracer", page.NodeText("#garage-bench-panel"));
    }

    /// <summary>An empty panel is a count, not a verdict: it says how many listings were looked through.</summary>
    [Fact]
    public void Nothing_fitting_is_said_as_a_count_of_what_was_looked_through()
    {
        var page = Opened("""{"enabled":true,"fetchedAt":"2026-09-15T10:00:00+00:00","total":500,"components":41,"listings":[]}""");

        var note = page.NodeText("#garage-market-note");
        Assert.Contains("Nobody has advertised a part that fits the Aegis Gladius in the newest 500", note);
        Assert.Contains("41 of them are ship components", note);
        Assert.Contains("not a verdict", note);
    }
}
