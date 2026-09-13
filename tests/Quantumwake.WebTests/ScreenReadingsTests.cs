namespace Quantumwake.WebTests;

/// <summary>
/// The screenshots read as they land: the setting, the list, the Now card
/// and the fleet's photographed fittings.
/// </summary>
/// <remarks>
/// What is defended is that a reading is shown with the app's belief beside
/// it and a verdict in words, that the folder watch is offered only in the
/// mode that allows it and names the folder it follows, and that a loadout
/// on the fleet page is dated rather than presented as the loadout.
/// </remarks>
public class ScreenReadingsTests
{
    private const string MapSighting = """
        {"shot":"ScreenShot-2026-09-07_21-30-17-B52.jpg","shotAt":"2026-09-08T01:30:17Z",
         "kind":"Map","summary":"PYRO > DUDLEY & DAUGHTERS","tookMs":149,
         "checks":[
           {"subject":"Where you were","claim":"PYRO > DUDLEY & DAUGHTERS","belief":"Pyro > Dudley & Daughters","verdict":"agrees","note":null},
           {"subject":"Contracts","claim":"no accepted contracts","belief":"1 open: Bounty: Vaughn","verdict":"differs","note":"the game says none; the logs may have missed an ending"},
           {"subject":"Wallet","claim":"(did not read)","belief":"the logs never carry a balance","verdict":"unchecked","note":"the balance is printed in a face this engine does not read - the handle beside it read fine"}
         ],
         "map":{"systemRead":"PYRO","placeRead":"DUDLEY & DAUGHTERS","latitude":0,"longitude":-155.25,"gigametres":68.33,"noAcceptedContracts":true},
         "wallet":{"balance":null,"trouble":"the balance is printed in a face this engine does not read - the handle beside it read fine"},
         "lines":["HANGAR","NO ACCEPTED CONTRACTS"]}
        """;

    private const string LoadoutSighting = """
        {"shot":"ScreenShot-2026-09-07_21-08-59-905.jpg","shotAt":"2026-09-08T01:08:59Z",
         "kind":"Loadout","summary":"Drake Corsair, 2 parts named","tookMs":182,
         "checks":[{"subject":"Fitted parts","claim":"2 parts named","belief":"the Drake Corsair as it ships","verdict":"differs","note":"not stock: Genoa in Power Plant 1"}],
         "loadout":{"shipRead":"DRAKE CORSAIR","ship":"Drake Corsair","looksLike":[],
                    "scope":"Only showing ships and equipment located in Nyx.",
                    "fittings":[
                      {"slot":"Cooler 2","read":"Civ/2/C Frost-Star EX","name":"Frost-Star EX","className":"COOL_JSPN_S02_FrostStarEX_SCItem","tier":"Exact","agrees":["size","grade"],"disagrees":[],"stock":true,"nothingRead":false},
                      {"slot":"Power Plant 1","read":"Ind/2/A Genoa","name":"Genoa","className":"POWR_JUST_S02_Genoa_SCItem","tier":"Exact","agrees":["size","grade"],"disagrees":[],"stock":false,"nothingRead":false},
                      {"slot":"Turret 1","read":null,"name":null,"className":null,"tier":"None","agrees":[],"disagrees":[],"stock":null,"nothingRead":true}
                    ]},
         "lines":[]}
        """;

    private static Page Panel(string mode = "Screenshots", bool watchScreenshots = false, string readings = "")
    {
        var page = new Page();

        page.Serve("/api/screen/settings", $$"""
            {"mode":"{{mode}}","watch":false,"watchScreenshots":{{(watchScreenshots ? "true" : "false")}},
             "canReadScreenshots":true,"canReadClipboard":true,
             "folder":"E:\\rsi\\StarCitizen\\LIVE\\screenshots"}
            """);

        page.Serve("/api/screen/readings?take=50",
            $$"""{"readings":[{{readings}}],"clipboard":[],"total":1,"pastes":0}""");
        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");
        page.Do("await renderScreenPanel();");
        return page;
    }

    /// <summary>
    /// The card is fed by the live stream, not by the settings page, which is
    /// what makes it work in the widget and before anyone opens the settings.
    /// </summary>
    private static void Frame(Page page, string? screen) =>
        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[], screen:{screen ?? "null"} }});");

    [Fact]
    public void The_folder_watch_is_offered_only_with_screenshots_allowed()
    {
        Assert.True(Panel("CopyOnly").Truth("__dom.node('#screen-watch-folder-label').hidden"));
        Assert.False(Panel("Screenshots").Truth("__dom.node('#screen-watch-folder-label').hidden"));
    }

    /// <summary>
    /// The pilot is agreeing to a folder being followed, and should be able
    /// to see which one, and that nothing already in it is read.
    /// </summary>
    [Fact]
    public void Watching_names_the_folder_it_follows_and_what_it_leaves_alone()
    {
        var page = Panel(watchScreenshots: true);

        var folder = page.NodeText("#screen-folder");

        Assert.False(page.Truth("__dom.node('#screen-folder').hidden"));
        Assert.Contains(@"E:\rsi\StarCitizen\LIVE\screenshots", folder);
        Assert.Contains("Nothing already there", folder);
        Assert.Contains("every screenshot", page.NodeText("#screen-mode-status"));
    }

    [Fact]
    public void Ticking_the_folder_watch_saves_it_with_the_rest_of_the_setting()
    {
        var page = Panel();
        page.Serve("/api/screen/settings?mode=Screenshots&watch=false&watchScreenshots=true",
            """{"mode":"Screenshots","watch":false,"watchScreenshots":true}""");

        page.Do("__dom.node('#screen-watch-folder').checked = true;");
        page.Do("await saveScreenSettings('Screenshots', false, true);");

        Assert.Contains("POST /api/screen/settings?mode=Screenshots&watch=false&watchScreenshots=true", page.Fetched());
        Assert.False(page.Truth("__dom.node('#screen-folder').hidden"));
    }

    [Fact]
    public void A_reading_shows_the_claim_the_belief_and_the_verdict_in_words()
    {
        var page = Panel(readings: MapSighting);

        var list = page.NodeText("#screen-readings");

        Assert.Contains("PYRO > DUDLEY & DAUGHTERS", list);
        Assert.Contains("logs: Pyro > Dudley & Daughters", list);
        Assert.Contains("agrees", list);
        Assert.Contains("Contracts: no accepted contracts", list);
        Assert.Contains("1 open: Bounty: Vaughn", list);
        Assert.Contains("differs", list);
        Assert.Contains("not checked", list);
        Assert.Contains("does not read", list);
    }

    [Fact]
    public void The_Now_card_carries_the_newest_reading_and_counts_the_disagreements()
    {
        var page = Panel(readings: MapSighting);
        Frame(page, MapSighting);

        Assert.False(page.Truth("__dom.node('#now-screen-card').hidden"));
        Assert.Equal("PYRO > DUDLEY & DAUGHTERS", page.NodeText("#now-screen-summary"));
        Assert.Contains("1 thing the screen and the logs disagree on", page.NodeText("#now-screen-note"));
        Assert.Equal(3, page.Count("__dom.node('#now-screen-checks').querySelectorAll('li').length"));
    }

    [Fact]
    public void With_nothing_read_the_Now_card_stays_hidden()
    {
        var page = Panel(readings: "");
        Frame(page, null);

        Assert.True(page.Truth("__dom.node('#now-screen-card').hidden"));
        Assert.Contains("Nothing read yet", page.NodeText("#screen-readings"));
    }

    [Fact]
    public void A_loadout_reading_lists_each_port_and_says_which_parts_are_not_stock()
    {
        var page = Panel(readings: LoadoutSighting);

        var list = page.NodeText("#screen-readings");

        Assert.Contains("Drake Corsair", list);
        Assert.Contains("Only showing ships and equipment located in Nyx.", list);
        Assert.Contains("Power Plant 1", list);
        Assert.Contains("Genoa", list);
        Assert.Contains("not stock", list);
        Assert.Contains("Turret 1", list);
        Assert.Contains("nothing read under it", list);
    }

    /// <summary>
    /// The scan button now returns the whole reading, checks and all, and the
    /// tooltip half of it is shown the way it always was.
    /// </summary>
    [Fact]
    public void A_scan_shows_the_reading_and_its_checks()
    {
        var page = Panel();
        page.Serve("/api/screen/scan", MapSighting);

        page.Do("await scanScreenshot();");

        var result = page.NodeText("#screen-result");

        Assert.Contains("PYRO > DUDLEY & DAUGHTERS", result);
        Assert.Contains("-155.25", result);
        Assert.Contains("Where you were", result);
        Assert.Contains("149 ms", page.NodeText("#screen-status"));
        Assert.Contains("map", page.NodeText("#screen-status"));
    }

    [Fact]
    public void A_scan_that_could_not_run_says_why()
    {
        var page = Panel();
        page.Serve("/api/screen/scan", """
            {"shot":"","shotAt":"2026-09-08T01:30:17Z","kind":"Unknown",
             "summary":"no screenshots yet - press Print Screen in the game and try again",
             "checks":[],"lines":[],"tookMs":0}
            """);

        page.Do("await scanScreenshot();");

        Assert.Contains("Print Screen", page.NodeText("#screen-status"));
    }

    /// <summary>
    /// A screenshot is a moment and not a state. The fleet page says when.
    /// </summary>
    [Fact]
    public void The_fleet_page_shows_what_a_ship_was_last_photographed_carrying_with_the_date()
    {
        var page = new Page();
        page.Serve("/api/screen/fittings", """
            [{"shot":"ScreenShot-2026-09-07_21-08-59-905.jpg","shotAt":"2026-09-08T01:08:59Z",
              "ship":"Drake Corsair","scope":"Only showing ships and equipment located in Nyx.",
              "fittings":[
                {"slot":"Cooler 2","read":"Civ/2/C Frost-Star EX","name":"Frost-Star EX","tier":"Exact","agrees":[],"disagrees":[],"stock":true,"nothingRead":false},
                {"slot":"Power Plant 1","read":"Ind/2/A Genoa","name":"Genoa","tier":"Exact","agrees":[],"disagrees":[],"stock":false,"nothingRead":false}
              ]}]
            """);

        page.Do("await renderFleetFittings();");

        var grid = page.NodeText("#fleet-fittings");

        Assert.False(page.Truth("__dom.node('#fleet-fittings-title').hidden"));
        Assert.Contains("Drake Corsair", grid);
        Assert.Contains("as photographed", grid);
        Assert.Contains("2026", grid);
        Assert.Contains("Frost-Star EX", grid);
        Assert.Contains("not stock", grid);
    }

    [Fact]
    public void With_no_loadout_photographed_the_fleet_section_stays_hidden()
    {
        var page = new Page();
        page.Serve("/api/screen/fittings", "[]");

        page.Do("await renderFleetFittings();");

        Assert.True(page.Truth("__dom.node('#fleet-fittings-title').hidden"));
    }

    /// <summary>
    /// Opening the page has to draw them, which is the part that was missing.
    /// </summary>
    /// <remarks>
    /// Both fleet sections above were built, documented and covered by the two
    /// tests before this one - and no one had ever seen either, because the
    /// tests called the render directly and nothing else called it at all.
    /// <c>showView</c> had an arm for a dozen views and none for the fleet, so
    /// the headings stayed hidden and the page looked as though it had nothing
    /// to say. A test that only calls the renderer cannot notice that; this one
    /// opens the view the way a pilot does.
    /// </remarks>
    [Fact]
    public void Opening_the_fleet_view_draws_the_photographed_sections()
    {
        var page = new Page();
        page.Serve("/api/screen/fittings", """
            [{"shot":"ScreenShot-2026-09-07_21-08-59-905.jpg","shotAt":"2026-09-08T01:08:59Z",
              "ship":"Drake Corsair","scope":"Only showing ships and equipment located in Nyx.",
              "fittings":[
                {"slot":"Cooler 2","read":"Civ/2/C Frost-Star EX","name":"Frost-Star EX","tier":"Exact","agrees":[],"disagrees":[],"stock":true,"nothingRead":false}
              ]}]
            """);

        page.Do("window.scrollTo = () => {}; showView('fleet');");

        Assert.Contains("Drake Corsair", page.NodeText("#fleet-fittings"));
        Assert.False(page.Truth("__dom.node('#fleet-fittings-title').hidden"));
    }
}
