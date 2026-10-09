namespace Quantumwake.WebTests;

/// <summary>
/// The Events page, the Now card and the contract rows: an event journal's
/// bars in points.
/// </summary>
/// <remarks>
/// The page's honesty is the thing under test, as on Wikelo. A total is a
/// floor and says so; an event not yet in the logs says that rather than
/// showing a row of zeros as progress; a contract the table does not know is
/// named rather than dropped; an install whose game data has not been read is
/// told so.
/// </remarks>
public class EventsTests
{
    private const string Events = """
        {"available":true,"countedFrom":null,
         "events":[
          {"id":"Iasi_ScenarioProgress","title":"RSI Discovery Event","description":"Welcome to RSI Discovery Month!\\n\\nAn annual celebration.",
           "percent":true,"firstSeen":"2026-10-09T18:00:00Z","lastSeen":"2026-10-09T19:00:00Z","completed":5,"unrecognised":0,
           "tracks":[
            {"id":"iasi_Journal_PlayerTotal","name":"Your total","overall":true,"color":null,"points":2085,
             "tiers":[{"minPoints":4500,"badge":"R_PU_IASI_OP_1","reward":"BriskAir IC-10 Cooler","reached":false},
                      {"minPoints":30000,"badge":"R_PU_IASI_OP_4","reward":"Helios Power Plant","reached":false}],
             "nextTier":4500,"toNext":2415,
             "fastest":[{"contract":"Iasi_ResourceGathering_Mining_Quantanium","title":"RSI Disc. Month: Procure Refined Quantainium","issuer":"Shubin Interstellar","points":1142,"needed":3,"done":0}]},
            {"id":"iasi_Journal_Defense","name":"Defense","overall":false,"color":"#125b7f","points":2085,
             "tiers":[{"minPoints":2000,"badge":"R_PU_IASI_DEFENSE_1","reward":"Scorpius Echo Livery and Sovereign IP-20 Power Plant","reached":true},
                      {"minPoints":4000,"badge":"R_PU_IASI_DEFENSE_2","reward":"Meteor PHB and TSB Flight Blades","reached":false}],
             "nextTier":4000,"toNext":1915,
             "fastest":[{"contract":"Iasi_Patrol_Hard","title":"RSI Disc. Month: Orange Lvl. - Neutralize Threats","issuer":"Foxwell Enforcement","points":417,"needed":5,"done":5}]}],
           "contracts":[
            {"id":"Iasi_ResourceGathering_Mining_Quantanium","title":"RSI Disc. Month: Procure Refined Quantainium","issuer":"Shubin Interstellar","points":1142,"tracks":["Your total","Collection"],"completed":0,"open":0,"lastCompleted":null},
            {"id":"Iasi_Patrol_Hard","title":"RSI Disc. Month: Orange Lvl. - Neutralize Threats","issuer":"Foxwell Enforcement","points":417,"tracks":["Your total","Defense"],"completed":5,"open":0,"lastCompleted":"2026-10-09T19:00:00Z"}]},
          {"id":"ORS_ScenarioProgress","title":"Orison Relief","description":"","percent":false,"firstSeen":null,"lastSeen":null,"completed":0,"unrecognised":0,
           "tracks":[{"id":"t","name":"Your Total","overall":true,"color":null,"points":0,
             "tiers":[{"minPoints":10800,"badge":"ORS_Pistol","reward":"ORS Pistol","reached":false}],"nextTier":10800,"toNext":10800,"fastest":[]}],
           "contracts":[]}],
         "open":[{"contract":"Iasi_Patrol_Hard","title":"RSI Disc. Month: Orange Lvl. - Neutralize Threats","points":417,"event":"RSI Discovery Event","tracks":["Your total","Defense"],"since":"2026-10-09T19:30:00Z"}]}
        """;

    private static Page Loaded(string events = Events)
    {
        var page = new Page();
        page.Serve("/api/events", events);
        page.Do("rememberEvent(null); await loadEvents();");
        return page;
    }

    [Fact]
    public void A_bar_reads_in_points_with_what_is_left_to_its_next_tier()
    {
        var body = Loaded().NodeText("#events-body");

        Assert.Contains("Defense", body);
        Assert.Contains("2,085 pts", body);
        Assert.Contains("1,915 to tier 2 (4,000)", body);
        Assert.Contains("Meteor PHB and TSB Flight Blades", body);
    }

    [Fact]
    public void The_fastest_contracts_to_the_next_tier_are_named_with_how_many_and_what_each_pays()
    {
        var body = Loaded().NodeText("#events-body");

        Assert.Contains("5 × RSI Disc. Month: Orange Lvl. - Neutralize Threats (417 each, done 5× before)", body);
        Assert.Contains("3 × RSI Disc. Month: Procure Refined Quantainium (1,142 each)", body);
    }

    [Fact]
    public void A_tier_reached_is_ticked_on_the_bar_and_in_the_list()
    {
        var page = Loaded();

        Assert.Equal(1, page.Count("__dom.node('#events-body').byClass('event-tick').filter(n => n.classList.contains('reached')).length"));
        Assert.Contains("✓Tier 1 · 2,000", page.Text("__dom.node('#events-body').byClass('event-tiers').map(n => n.textContent).join('|')"));
    }

    /// <summary>
    /// The game's blurb carries "\n" written out as two characters; it is read
    /// as a line break, not printed.
    /// </summary>
    [Fact]
    public void The_description_is_read_not_printed_with_the_game_markup()
    {
        var body = Loaded().NodeText("#events-body");

        Assert.Contains("Welcome to RSI Discovery Month!\n\nAn annual celebration.", body);
        Assert.DoesNotContain("\\n", body);
    }

    [Fact]
    public void A_contract_in_the_journal_now_says_what_finishing_it_adds()
    {
        var body = Loaded().NodeText("#events-body");

        Assert.Contains("In your journal now", body);
        Assert.Contains("+417 · RSI Disc. Month: Orange Lvl. - Neutralize Threats — counts toward Your total, Defense", body);
    }

    /// <summary>
    /// The overall bar takes every contract, so naming it on every row is
    /// noise: a row names the bar that sets it apart.
    /// </summary>
    [Fact]
    public void A_contract_row_names_the_bar_that_sets_it_apart()
    {
        var table = Loaded().Text("__dom.node('#events-body').byClass('event-contracts')[0].textContent");

        Assert.Contains("Neutralize ThreatsFoxwell EnforcementDefense4175", table);
        Assert.Contains("Shubin InterstellarCollection1,142—", table);
    }

    [Fact]
    public void The_journals_percent_is_explained_once_rather_than_imitated()
    {
        Assert.Contains("the journal shows these bars in percent; here they are in points", Loaded().NodeText("#events-body"));
    }

    [Fact]
    public void An_event_not_in_the_logs_says_so_rather_than_showing_zero_as_progress()
    {
        var page = Loaded();
        page.Do("rememberEvent('ORS_ScenarioProgress'); renderEvents();");

        var body = page.NodeText("#events-body");
        Assert.Contains("None of its contracts are in your logs yet", body);
        Assert.Contains("ORS Pistol", body);
    }

    [Fact]
    public void Every_event_in_the_patch_can_be_picked_and_the_played_one_is_marked()
    {
        var page = Loaded();

        var picker = page.NodeText("#events-picker");
        Assert.Contains("RSI Discovery Event●", picker);
        Assert.Contains("Orison Relief", picker);
        Assert.DoesNotContain("Orison Relief●", picker);
        Assert.Equal("2 in this patch", page.NodeText("#events-count"));
    }

    [Fact]
    public void Contracts_that_look_like_the_events_but_are_not_in_its_table_are_named()
    {
        var page = Loaded(Events.Replace("\"completed\":5,\"unrecognised\":0", "\"completed\":5,\"unrecognised\":2"));

        Assert.Contains("2 contracts in your logs look like this event's but are not in the installed game's points table", page.NodeText("#events-body"));
    }

    [Fact]
    public void Without_game_data_the_page_says_the_files_have_not_been_read()
    {
        var page = Loaded("""{"available":false,"countedFrom":null,"events":[],"open":[]}""");

        Assert.Contains("The game files have not been read yet", page.NodeText("#events-body"));
    }

    [Fact]
    public void The_now_card_shows_the_event_being_played_and_what_the_open_contract_adds()
    {
        var page = Loaded();

        Assert.False(page.Truth("__dom.node('#now-event-card').hidden"));
        Assert.Equal("RSI Discovery Event", page.NodeText("#now-event-label"));
        Assert.Equal("2,085 pts · 2,415 to tier 1 (4,500)", page.NodeText("#now-event"));

        var list = page.NodeText("#now-event-list");
        Assert.Contains("+417 · RSI Disc. Month: Orange Lvl. - Neutralize Threats", list);
        Assert.Contains("Defense2,085 / 4,000", list);
    }

    /// <summary>
    /// A month-old event is not the one being played: the card stands down
    /// rather than leading the Now page with something the pilot has left.
    /// </summary>
    [Fact]
    public void The_now_card_hides_when_no_event_has_been_played_lately()
    {
        var stale = Events
            .Replace("\"lastSeen\":\"2026-10-09T19:00:00Z\"", "\"lastSeen\":\"2025-01-01T00:00:00Z\"")
            .Replace("\"open\":[{", "\"openWas\":[{");

        Assert.True(Loaded(stale).Truth("__dom.node('#now-event-card').hidden"));
    }

    [Fact]
    public void An_open_event_contract_leads_the_focus_strip_with_what_it_pays()
    {
        var page = new Page();
        page.Do("""
            renderNowFocus({ contracts: [ { name: 'RSI Disc. Month: Orange Lvl. - Neutralize Threats', hauling: false,
              eventPoints: 417, event: 'RSI Discovery Event', eventTracks: ['Your total', 'Defense'] } ] });
            """);

        Assert.Equal("Event contract", page.NodeText("#now-focus-title"));
        Assert.Equal("RSI Disc. Month: Orange Lvl. - Neutralize Threats · +417 pts to Your total, Defense", page.NodeText("#now-focus-detail"));
        Assert.Equal("Event progress", page.NodeText("#now-focus-open"));
    }

    [Fact]
    public void A_contract_row_carries_what_it_pays_into_an_event()
    {
        var page = new Page();
        page.Serve("/api/contracts?days=0", """
            [{ "at": "2026-10-09T18:00:00Z", "name": "RSI Disc. Month: Orange Lvl. - Neutralize Threats",
               "issuer": "Foxwell Enforcement", "type": "Patrol", "difficulty": "Hard",
               "system": "Stanton", "outcome": "Completed", "steps": 0, "stepsDone": 0,
               "eventPoints": 417, "event": "RSI Discovery Event", "eventTracks": ["Your total", "Defense"] }]
            """);

        page.Do("await loadContractList();");

        var text = page.NodeText("#contracts-table tbody");
        Assert.Contains("417 pts", text);
        Assert.DoesNotContain("—417", text);
        Assert.Contains("RSI Discovery Event: counts toward Your total, Defense",
            page.Text("__dom.node('#contracts-table tbody').byClass('tag-event')[0].title"));
    }

    /// <summary>
    /// An event the pilot means to play, or wants in view between sessions,
    /// is one play alone never puts on the Now page - so a choice puts it
    /// there, even with nothing played for a year.
    /// </summary>
    [Fact]
    public void A_tracked_event_is_on_the_now_card_whatever_was_played()
    {
        var stale = Events
            .Replace("\"lastSeen\":\"2026-10-09T19:00:00Z\"", "\"lastSeen\":\"2025-01-01T00:00:00Z\"")
            .Replace("\"open\":[{", "\"openWas\":[{")
            .Replace("\"countedFrom\":null,", "\"countedFrom\":null,\"tracked\":\"ORS_ScenarioProgress\",");

        var page = Loaded(stale);

        Assert.False(page.Truth("__dom.node('#now-event-card').hidden"));
        Assert.Equal("Orison Relief · tracked", page.NodeText("#now-event-label"));
        Assert.Equal("0 pts · 10,800 to tier 1 (10,800)", page.NodeText("#now-event"));
    }

    /// <summary>
    /// A choice beats play: with a Discovery contract open in the journal,
    /// the card still shows the event the pilot pinned.
    /// </summary>
    [Fact]
    public void A_tracked_event_beats_the_one_an_open_contract_pays_into()
    {
        var page = Loaded(Events.Replace("\"countedFrom\":null,", "\"countedFrom\":null,\"tracked\":\"ORS_ScenarioProgress\","));

        Assert.Equal("Orison Relief · tracked", page.NodeText("#now-event-label"));
    }

    /// <summary>
    /// A tracked event the installed game no longer lists - a patch retired
    /// it - is no choice at all, and the card goes back to following play.
    /// </summary>
    [Fact]
    public void A_tracked_event_the_game_no_longer_lists_is_ignored()
    {
        var page = Loaded(Events.Replace("\"countedFrom\":null,", "\"countedFrom\":null,\"tracked\":\"Gone_ScenarioProgress\","));

        Assert.Equal("RSI Discovery Event", page.NodeText("#now-event-label"));
    }

    [Fact]
    public void Track_on_now_asks_the_server_to_keep_this_event()
    {
        var page = Loaded();
        page.Serve("/api/events/track?id=Iasi_ScenarioProgress", """{"tracked":"Iasi_ScenarioProgress"}""");

        Assert.Contains("Track on Now", page.NodeText("#events-body"));
        page.Do("await trackEvent('Iasi_ScenarioProgress');");

        Assert.Contains("POST /api/events/track?id=Iasi_ScenarioProgress", page.Fetched());
        Assert.Contains("Tracked on Now — stop", page.NodeText("#events-body"));
        Assert.Equal("RSI Discovery Event · tracked", page.NodeText("#now-event-label"));
    }

    [Fact]
    public void Stop_lets_the_card_follow_play_again()
    {
        var page = Loaded(Events.Replace("\"countedFrom\":null,", "\"countedFrom\":null,\"tracked\":\"Iasi_ScenarioProgress\","));
        page.Serve("/api/events/track", """{"tracked":null}""");

        page.Do("await trackEvent(null);");

        Assert.Contains("POST /api/events/track", page.Fetched());
        Assert.Contains("Track on Now", page.NodeText("#events-body"));
        Assert.DoesNotContain("Tracked on Now", page.NodeText("#events-body"));
        Assert.Equal("RSI Discovery Event", page.NodeText("#now-event-label"));
    }
}
