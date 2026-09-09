namespace Quantumwake.WebTests;

/// <summary>
/// The second night's screens on the page: contracts, the Fleet Manager and
/// the Rep app, and the fleet page's berths.
/// </summary>
public class ScreenAppsTests
{
    private const string ContractsSighting = """
        {"shot":"ScreenShot-2026-09-08_21-48-31-84A.jpg","shotAt":"2026-09-09T01:48:31Z",
         "kind":"Contracts","summary":"5 contracts accepted","tookMs":397,
         "checks":[{"subject":"Contracts","claim":"5 accepted of 10: JUNIOR 1 STELLAR SMALL HAUL 1 TO STANTON GATEWAY, …",
                    "belief":"1 open: Junior | Stellar Small Haul | to Stanton Gateway","verdict":"differs",
                    "note":"on screen but not in the logs: JUNIOR I STELLAR SMALL HAUL 1 TO ENDGAME; the tab says 5, the logs say 1"}],
         "contracts":{"accepted":5,"capacity":10,
                      "cards":[{"title":"JUNIOR 1 STELLAR SMALL HAUL 1 TO STANTON GATEWAY","reward":"215k","issuer":"REO WINO LINEHAUL"},
                               {"title":"JUNIOR I STELLAR SMALL HAUL 1 TO ENDGAME","reward":"172k","issuer":"REO WINO LINEHAUL"}],
                      "selectedTitle":"Junior I Stellar Small Haul I to Stanton Gateway","selectedReward":215250,
                      "selectedIssuer":"Red Wind Linehaul","objectives":["Deliver 0/18 SCU of Aluminum to Stanton Gateway."]},
         "lines":[]}
        """;

    private const string FleetSighting = """
        {"shot":"ScreenShot-2026-09-08_21-52-55-ECB.jpg","shotAt":"2026-09-09T01:52:55Z",
         "kind":"Fleet","summary":"5 ships at the Fleet Manager","tookMs":286,
         "checks":[{"subject":"Fleet","claim":"5 ships listed: Drake Clipper at Port Tressler, Drake Corsair at Levski (3 names did not read)",
                    "belief":"12 ships flown","verdict":"new","note":"never flown in the logs: Drake Clipper"}],
         "fleet":{"ships":[
           {"read":"Anvil CÉX \"PiSCésÉxpéd!tiori","ship":null,"looksLike":[],"location":"Pört Tressler","state":"Stored","focus":"Pathfinder","cargo":null},
           {"read":"Drake Corsair","ship":"Drake Corsair","looksLike":[],"location":"Levski","state":"Stored","focus":"Expedition","cargo":72}
         ]},
         "lines":[]}
        """;

    private const string RepSighting = """
        {"shot":"ScreenShot-2026-09-08_21-49-08-934.jpg","shotAt":"2026-09-09T01:49:08Z",
         "kind":"Reputation","summary":"reputation with HEADHUNTERS: NEUTRAL","tookMs":180,
         "checks":[{"subject":"Reputation","claim":"HEADHUNTERS: NEUTRAL","belief":"nothing - the logs never carry reputation","verdict":"new",
                    "note":"the rank is a highlighted card among identical ones, and a highlight is not text, so it does not read"}],
         "reputation":{"organisation":"HEADHUNTERS","standing":"NEUTRAL","rank":null,"organisations":["FOXWELL ENFORCEMENT","HEADHUNTERS"]},
         "wallet":{"balance":2108600,"trouble":null},
         "lines":[]}
        """;

    private static Page Panel(string readings)
    {
        var page = new Page();

        page.Serve("/api/screen/settings", """
            {"mode":"Screenshots","watch":false,"watchScreenshots":true,
             "canReadScreenshots":true,"canReadClipboard":true,"folder":"E:\\shots"}
            """);

        page.Serve("/api/screen/readings?take=12", $$"""{"readings":[{{readings}}],"total":1}""");
        page.Do("await renderScreenPanel();");
        return page;
    }

    [Fact]
    public void A_contracts_reading_lists_the_cards_and_the_selected_contract()
    {
        var page = Panel(ContractsSighting);

        var list = page.NodeText("#screen-readings");

        Assert.Contains("5 accepted of 10", list);
        Assert.Contains("TO ENDGAME", list);
        Assert.Contains("172k", list);
        Assert.Contains("215,250 aUEC", list);
        Assert.Contains("by Red Wind Linehaul", list);
        Assert.Contains("Deliver 0/18 SCU", list);
        Assert.Contains("the tab says 5, the logs say 1", list);
    }

    [Fact]
    public void A_fleet_reading_shows_each_ship_or_what_was_read_instead()
    {
        var page = Panel(FleetSighting);

        var list = page.NodeText("#screen-readings");

        Assert.Contains("Drake Corsair", list);
        Assert.Contains("at Levski", list);
        Assert.Contains("72 SCU", list);
        Assert.Contains("read as", list);
        Assert.Contains("never flown in the logs: Drake Clipper", list);
        Assert.Contains("new", page.NodeText("#now-screen-checks"));
    }

    [Fact]
    public void A_reputation_reading_says_the_rank_does_not_read_and_shows_the_wallet()
    {
        var page = Panel(RepSighting);

        var list = page.NodeText("#screen-readings");

        Assert.Contains("HEADHUNTERS: NEUTRAL", list);
        Assert.Contains("does not read", list);
        Assert.Contains("Wallet: 2,108,600 aUEC", list);
    }

    [Fact]
    public void The_fleet_page_shows_where_each_ship_was_with_the_date()
    {
        var page = new Page();
        page.Serve("/api/screen/fittings", "[]");
        page.Serve("/api/screen/fleet", """
            {"shot":"ScreenShot-2026-09-08_21-52-55-ECB.jpg","shotAt":"2026-09-09T01:52:55Z",
             "ships":[{"read":"Drake Corsair","ship":"Drake Corsair","looksLike":[],"location":"Levski","state":"Stored","focus":"Expedition","cargo":72}]}
            """);

        page.Do("await renderFleetFittings();");

        Assert.False(page.Truth("__dom.node('#fleet-berths-title').hidden"));
        var berths = page.NodeText("#fleet-berths");
        Assert.Contains("As photographed", berths);
        Assert.Contains("Drake Corsair", berths);
        Assert.Contains("at Levski", berths);
    }

    [Fact]
    public void With_no_fleet_photographed_the_berths_stay_hidden()
    {
        var page = new Page();
        page.Serve("/api/screen/fittings", "[]");
        page.Serve("/api/screen/fleet", """{"shot":null,"shotAt":null,"ships":[]}""");

        page.Do("await renderFleetFittings();");

        Assert.True(page.Truth("__dom.node('#fleet-berths-title').hidden"));
    }
}
