namespace Quantumwake.WebTests;

/// <summary>
/// The log of what the app has been shown, and the toast when a reading
/// disagrees with the logs.
/// </summary>
/// <remarks>
/// Two things are defended here. A paste and a screenshot land in one list,
/// because to the pilot they are the same act. And a reading reaches the Now
/// card through the live stream rather than through this page, which is what
/// makes the card work in the widget and before anyone has opened the
/// settings.
/// </remarks>
public class ScreenLogTests
{
    private const string Paste = """
        {"at":"2026-09-09T02:10:00Z","x":-9641671346.9,"y":-11490734321.2,"z":-91805.1,
         "gigametres":14.99996,"believed":"Ruin Station","system":"Pyro"}
        """;

    private const string Orphan = """
        {"at":"2026-09-09T01:00:00Z","x":1,"y":2,"z":3,
         "gigametres":0.5,"believed":null,"system":null}
        """;

    private const string Sighting = """
        {"shot":"ScreenShot-A.jpg","shotAt":"2026-09-09T02:05:00Z","kind":"Map",
         "summary":"PYRO > RUIN STATION","tookMs":150,
         "checks":[{"subject":"Where you were","claim":"PYRO > RUIN STATION","belief":"Pyro > Ruin Station","verdict":"agrees","note":null}],
         "map":{"systemRead":"PYRO","placeRead":"RUIN STATION","latitude":0,"longitude":134.77,"gigametres":68.32,"noAcceptedContracts":false},
         "lines":[]}
        """;

    private static Page Panel(string readings = "", string clipboard = "")
    {
        var page = new Page();

        page.Serve("/api/screen/settings", """
            {"mode":"Screenshots","watch":false,"watchScreenshots":true,
             "canReadScreenshots":true,"canReadClipboard":true,"folder":"E:\\shots"}
            """);

        page.Serve("/api/screen/readings?take=50", $$"""
            {"readings":[{{readings}}],"clipboard":[{{clipboard}}],"pins":[],"total":1,"pastes":2}
            """);

        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");
        page.Do("await renderScreenPanel();");
        return page;
    }

    [Fact]
    public void A_paste_and_a_screenshot_land_in_one_list_newest_first()
    {
        var page = Panel(Sighting, $"{Paste},{Orphan}");

        var log = page.NodeText("#screen-readings");

        Assert.Contains("pasted", log);
        Assert.Contains("15.0000 Gm", log);
        Assert.Contains("PYRO > RUIN STATION", log);
        Assert.Contains("ScreenShot-A.jpg", log);
    }

    /// <summary>
    /// The reading names no place and no system. What makes it readable a week
    /// later is what the logs said at the time, so that is stored with it.
    /// </summary>
    [Fact]
    public void A_paste_carries_where_the_logs_had_you_at_the_time()
    {
        var log = Panel(Sighting, Paste).NodeText("#screen-readings");

        Assert.Contains("Pyro > Ruin Station", log);
        Assert.Contains("x -9,641,671,347", log);
    }

    [Fact]
    public void A_paste_with_no_session_behind_it_says_so_rather_than_going_blank()
    {
        var log = Panel(Sighting, Orphan).NodeText("#screen-readings");

        Assert.Contains("no session running", log);
    }

    [Fact]
    public void The_log_counts_both_halves()
    {
        var counts = Panel(Sighting, $"{Paste},{Orphan}").NodeText("#screen-log-counts");

        Assert.Contains("1 screenshot read", counts);
        Assert.Contains("2 pastes", counts);
    }

    [Fact]
    public void An_empty_log_says_what_would_land_in_it()
    {
        var log = Panel().NodeText("#screen-readings");

        Assert.Contains("Nothing read yet", log);
        Assert.Contains("pastes both land here", log);
    }

    [Fact]
    public void Clearing_the_log_empties_it()
    {
        var page = Panel(Sighting, Paste);

        page.Serve("/api/screen/readings?take=50", """{"readings":[],"clipboard":[],"pins":[],"total":0,"pastes":0}""");
        page.Do("__dom.node('#screen-log-clear').click();");

        Assert.Contains("DELETE /api/screen/readings", page.Fetched());
        Assert.Contains("Nothing read yet", page.NodeText("#screen-readings"));
    }

    [Fact]
    public void A_copied_location_can_be_pinned_as_a_point_of_interest()
    {
        var page = Panel(clipboard: Paste);
        page.Do("renderPinnedLocations([{sourceAt:'2026-09-09T02:10:00Z',pinnedAt:'2026-09-09T02:12:00Z',x:-9641671346.9,y:-11490734321.2,z:-91805.1,gigametres:14.99996,believed:'Ruin Station',system:'Pyro'}]);");
        page.Serve("/api/screen/clipboard/pin", """{"sourceAt":"2026-09-09T02:10:00Z"}""");

        Assert.Contains("Ruin Station", page.NodeText("#screen-pins"));
        page.Do("await pinClipboardLocation({at:'2026-09-09T02:10:00Z'}, __dom.node('#screen-log-refresh'));");

        Assert.Contains("POST /api/screen/clipboard/pin", page.Fetched());
        Assert.Contains("2026-09-09T02:10:00Z", page.BodyOf("/api/screen/clipboard/pin"));
    }

    [Fact]
    public void A_saved_point_can_be_removed_from_the_log()
    {
        var page = Panel();
        page.Serve("/api/screen/pins?at=2026-09-09T02%3A10%3A00Z", """{"removed":true}""");

        page.Do("await unpinLocation({sourceAt:'2026-09-09T02:10:00Z'}, __dom.node('#screen-log-refresh'));");

        Assert.Contains("DELETE /api/screen/pins?at=2026-09-09T02%3A10%3A00Z", page.Fetched());
    }

    [Fact]
    public void A_pinned_point_can_be_named_and_categorised()
    {
        var page = Panel();
        page.Serve("/api/screen/pins", """{"label":"Ruin mining shelf","category":"Mining"}""");

        page.Do("await savePinnedLocation({sourceAt:'2026-09-09T02:10:00Z'}, 'Ruin mining shelf', 'Mining', __dom.node('#screen-log-refresh'));");

        Assert.Contains("PUT /api/screen/pins", page.Fetched());
        Assert.Contains("Ruin mining shelf", page.BodyOf("/api/screen/pins"));
        Assert.Contains("Mining", page.BodyOf("/api/screen/pins"));
    }

    [Fact]
    public void A_repeated_clipboard_location_says_it_was_seen_again()
    {
        var paste = Paste.Replace("\"system\":\"Pyro\"", "\"system\":\"Pyro\",\"timesSeen\":4,\"lastSeenAt\":\"2026-09-09T02:13:00Z\"");

        var log = Panel(clipboard: paste).NodeText("#screen-readings");

        Assert.Contains("seen 4 times", log);
        Assert.Contains("last", log);
    }

    [Fact]
    public void An_unread_mobiglas_screen_goes_to_the_review_queue_with_its_text()
    {
        const string unknown = """
            {"shot":"ScreenShot-unread.jpg","shotAt":"2026-09-09T02:05:00Z","kind":"MobiGlas",
             "summary":"a mobiGlas screen this app cannot read yet","tookMs":150,"checks":[],
             "lines":["Unmapped app","Useful captured line"]}
            """;

        var page = Panel(readings: unknown);

        Assert.False(page.Truth("__dom.node('#screen-review').hidden"));
        Assert.Contains("ScreenShot-unread.jpg", page.NodeText("#screen-review-list"));
        Assert.Contains("Useful captured line", page.NodeText("#screen-review-list"));
        Assert.Contains("Open screenshot", page.NodeText("#screen-review-list"));
    }

    // ---- the stream, and the toast ----

    private static void Frame(Page page, string events, string screen = "null") =>
        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[{events}], screen:{screen} }});");

    private const string Differs =
        "{at:'2026-09-09T02:05:00Z',kind:'screen-differs',text:'Where you were: PYRO > RUIN STATION',"
        + "detail:'the footer reads as Ruin Station'}";

    /// <summary>
    /// Something already in the window, so the first frame has an anchor. The
    /// stream opens with history and the client deliberately toasts none of
    /// it, so a test that opens on an empty feed proves nothing.
    /// </summary>
    private const string Arrival =
        "{at:'2026-09-09T02:00:00Z',kind:'location',text:'Arrived',detail:'Ruin Station'}";

    /// <summary>
    /// A disagreement is the whole point of the feature, and the one thing
    /// worth interrupting for.
    /// </summary>
    [Fact]
    public void A_reading_that_differs_raises_a_toast()
    {
        var page = Panel();

        Frame(page, Arrival);
        Frame(page, $"{Differs},{Arrival}");

        Assert.Equal(1, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
        Assert.Contains("Where you were", page.NodeText("#toasts"));
    }

    /// <summary>
    /// A pilot photographing a loadout takes several frames in a row. A toast
    /// apiece would teach them to ignore the toasts.
    /// </summary>
    [Fact]
    public void A_reading_that_agrees_raises_none()
    {
        var page = Panel();

        Frame(page, Arrival);
        Frame(page, $"{{at:'2026-09-09T02:06:00Z',kind:'screen',text:'PYRO > RUIN STATION',detail:null}},{Arrival}");

        Assert.Equal(0, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
    }

    [Fact]
    public void The_card_is_fed_by_the_stream_and_not_by_this_page()
    {
        var page = Panel();

        Assert.True(page.Truth("__dom.node('#now-screen-card').hidden"));

        Frame(page, "", Sighting);

        Assert.False(page.Truth("__dom.node('#now-screen-card').hidden"));
        Assert.Contains("PYRO > RUIN STATION", page.NodeText("#now-screen-summary"));
    }

    [Fact]
    public void The_hub_leads_with_a_screen_disagreement_and_a_link_to_review_it()
    {
        var page = Panel();
        Frame(page, "", "{checks:[{verdict:'differs'}]}");

        Assert.False(page.Truth("__dom.node('#now-focus').hidden"));
        Assert.Contains("Screen needs review", page.NodeText("#now-focus-title"));
        Assert.Equal("Review", page.NodeText("#now-focus-open"));
    }
}
