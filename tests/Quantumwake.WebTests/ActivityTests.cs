namespace Quantumwake.WebTests;

/// <summary>
/// The chip that says what the app is busy with, and the notice that says
/// what that means for the page on screen.
/// </summary>
/// <remarks>
/// Before these, only Settings could tell a pilot a job was running. A cold
/// start reads the game files for half a minute before the log scan even
/// begins, a community download runs for a minute, and every page built on
/// either sat empty with nothing to tell that apart from a broken install.
/// </remarks>
public class ActivityTests
{
    private const string Idle = """{ "state": "ready" }""";

    private static string Activity(
        string scan = "{ running: false }", bool waiting = false, string gameData = Idle, string jobs = "[]") =>
        $"{{ scan: {scan}, scanWaiting: {(waiting ? "true" : "false")}, gameData: {gameData}, jobs: {jobs} }}";

    private const string ColdStart = """{ "state": "reading", "seconds": 12.4 }""";

    private static string Scanning(int done = 34, int total = 148, int parsed = 12) =>
        $"{{ running: true, done: {done}, total: {total}, parsed: {parsed}, file: 'Game-2026-10-12.log', " +
        $"percent: {(int)Math.Round(done * 100.0 / total)}, elapsedSeconds: 9 }}";

    private static bool Hidden(Page page, string selector) =>
        page.Truth($"__dom.node({Page.Quote(selector)}).hidden");

    private static Page On(string view, string? language = null)
    {
        var page = new Page(language);
        page.Do($"shownView = {Page.Quote(view)};");
        return page;
    }

    /// <summary>
    /// The case this was written for. The startup scan waits behind the game
    /// files, so for half a minute it is neither running nor finished, and the
    /// scan bar - the only signal there was - stayed hidden while every log
    /// page sat empty.
    /// </summary>
    [Fact]
    public void A_cold_start_says_the_logs_are_queued_behind_the_game_files()
    {
        var page = On("ledger");

        page.Do($"paintActivity({Activity(waiting: true, gameData: ColdStart)});");

        Assert.False(Hidden(page, "#activity"));
        Assert.Equal("Reading game files +1", page.NodeText("#activity-text"));

        // Ledger is built from logs, so it hears about the logs - not about a
        // game-file read it does not use.
        Assert.False(Hidden(page, "#scan"));
        Assert.Equal("Logs queued", page.NodeText("#scan-label"));
        Assert.Equal("after the game files", page.NodeText("#scan-count"));
        Assert.Equal("This page is built from your logs and fills in when they have been read.",
            page.NodeText("#scan-why"));
        Assert.True(page.Truth("__dom.node('#scan').classList.contains('indeterminate')"));
    }

    [Fact]
    public void A_page_built_from_game_files_names_the_game_files()
    {
        var page = On("parts");

        page.Do($"paintActivity({Activity(waiting: true, gameData: ColdStart)});");

        Assert.Equal("Reading game files", page.NodeText("#scan-label"));
        Assert.Equal("12s", page.NodeText("#scan-count"));
        Assert.Equal("This page is built from your game files and fills in when they have been read.",
            page.NodeText("#scan-why"));
    }

    /// <summary>
    /// A notice on every page for every job is the old always-there strip that
    /// nobody reads. Controls has nothing to do with the logs, so it keeps the
    /// chip and loses the notice.
    /// </summary>
    [Fact]
    public void A_page_that_does_not_use_the_logs_gets_the_chip_and_no_notice()
    {
        var page = On("controls");

        page.Do($"paintScan({Scanning()}, false); paintActivity({Activity(scan: Scanning())});");

        Assert.False(Hidden(page, "#activity"));
        Assert.Equal("Reading logs 34/148", page.NodeText("#activity-text"));
        Assert.True(Hidden(page, "#scan"));
    }

    [Fact]
    public void A_running_scan_keeps_its_own_bar_on_a_page_built_from_logs()
    {
        var page = On("ledger");

        page.Do($"paintScan({Scanning()}, false); paintActivity({Activity(scan: Scanning())});");

        Assert.False(Hidden(page, "#scan"));
        Assert.Equal("Parsing logs — 12 new", page.NodeText("#scan-label"));
        Assert.Equal("Game-2026-10-12.log", page.NodeText("#scan-file"));
        Assert.Equal("23%", page.Text("__dom.node('#scan-fill').style.width"));
        Assert.False(page.Truth("__dom.node('#scan').classList.contains('indeterminate')"));
        Assert.Equal("This page is built from your logs and fills in when they have been read.",
            page.NodeText("#scan-why"));
    }

    /// <summary>
    /// The automatic UEX refresh is the one fetch nobody clicked for. Market
    /// shows prices, so it says they are the previous ones until it lands; Now
    /// does not, so only the chip mentions it.
    /// </summary>
    [Theory]
    [InlineData("market", false)]
    [InlineData("now", true)]
    public void A_price_fetch_is_explained_where_prices_are_shown(string view, bool noticeHidden)
    {
        var page = On(view);

        page.Do($"paintActivity({Activity(jobs: "[{ key: 'uex', seconds: 3 }]")});");

        Assert.Equal("Fetching prices", page.NodeText("#activity-text"));
        Assert.Equal(noticeHidden, Hidden(page, "#scan"));

        if (!noticeHidden)
        {
            Assert.Equal("Fetching UEX prices", page.NodeText("#scan-label"));
            Assert.Equal("Prices on this page are the previous ones until the fetch finishes.",
                page.NodeText("#scan-why"));
        }
    }

    [Fact]
    public void The_panel_lists_every_job_and_names_the_feed()
    {
        var page = On("settings");

        page.Do($$"""
            paintActivity({{Activity(
                scan: Scanning(),
                jobs: "[{ key: 'community', seconds: 20 }, { key: 'uex-feed', detail: 'Fuel prices', seconds: 2 }]")}});
            """);

        Assert.Equal(3, page.Count("__dom.node('#activity-list').children.length"));
        Assert.Equal("Reading logs 34/148 +2", page.NodeText("#activity-text"));

        var labels = page.Text(
            "__dom.node('#activity-list').children.map((row) => row.children[0].children[0].textContent).join('|')");
        Assert.Equal("Parsing logs — 12 new|Downloading the community dataset|Fetching the UEX feed: Fuel prices", labels);
    }

    /// <summary>
    /// The usual load: the startup scan finished before the page opened. A
    /// chip announcing "up to date" for work nobody saw would sit on every load.
    /// </summary>
    [Fact]
    public void An_idle_first_poll_shows_nothing()
    {
        var page = On("now");

        page.Do($"paintActivity({Activity()});");

        Assert.True(Hidden(page, "#activity"));
        Assert.True(Hidden(page, "#scan"));
    }

    [Fact]
    public void Work_that_finishes_says_up_to_date_for_a_moment_and_then_goes()
    {
        var page = On("now");

        page.Do($"paintActivity({Activity(scan: Scanning())}, {{ now: 1000 }});");
        page.Do($"paintActivity({Activity()}, {{ now: 2000 }});");

        Assert.False(Hidden(page, "#activity"));
        Assert.Equal("Up to date", page.NodeText("#activity-text"));
        Assert.True(page.Truth("__dom.node('#activity').classList.contains('settled')"));

        page.Do($"paintActivity({Activity()}, {{ now: 6500 }});");

        Assert.True(Hidden(page, "#activity"));
    }

    /// <summary>
    /// The notice is about the page on screen, so changing page redraws it at
    /// once rather than leaving the last page's wait up for a poll.
    /// </summary>
    [Fact]
    public void Changing_page_redraws_the_notice_for_the_new_page()
    {
        var page = On("now");
        page.Do("window.scrollTo = () => {};");

        page.Do($"paintActivity({Activity(waiting: true, gameData: ColdStart)});");
        Assert.False(Hidden(page, "#scan"));

        // About is built from nothing that is running.
        page.Do("showView('about');");
        Assert.True(Hidden(page, "#scan"));

        page.Do("showView('mining');");
        Assert.False(Hidden(page, "#scan"));
        Assert.Equal("Reading game files", page.NodeText("#scan-label"));
    }

    /// <summary>
    /// While the bar holds on "Scan complete" it belongs to paintScan: the
    /// activity pass must not hide it the moment the scan stops running.
    /// </summary>
    [Fact]
    public void The_scan_complete_hold_is_left_alone()
    {
        var page = On("ledger");

        page.Do($"paintScan({Scanning()}, false); paintActivity({Activity(scan: Scanning())});");
        page.Do($$"""
            const __finished = paintScan({ running: false, parsed: 148, elapsedSeconds: 31 }, true);
            paintActivity({{Activity()}}, { holding: __finished });
            """);

        Assert.False(Hidden(page, "#scan"));
        Assert.Equal("Scan complete", page.NodeText("#scan-label"));
        Assert.Equal("", page.NodeText("#scan-why"));
    }

    [Fact]
    public void The_panel_button_goes_to_where_the_job_is_shown()
    {
        var page = On("now");
        page.Do("window.scrollTo = () => {};");

        page.Do($"paintActivity({Activity(jobs: "[{ key: 'community', seconds: 4 }]")});");
        page.Do("__dom.node('#activity-panel').hidden = false;");
        page.Do("""
            const __open = __dom.node('#activity-list').children[0].children[2];
            __open.fire('click', { currentTarget: __open });
            """);

        Assert.Equal("Open Settings", page.Text("__dom.node('#activity-list').children[0].children[2].textContent"));
        Assert.True(Hidden(page, "#activity-panel"));
    }

    /// <summary>
    /// The page said it would fill in when the logs were read. Crew fetches on
    /// entry, so without this it kept what it got halfway through the scan.
    /// </summary>
    [Fact]
    public void A_finished_scan_refetches_the_page_on_screen()
    {
        var page = On("crew");
        page.Serve("/api/crew?days=0", "[]");

        page.Do("refreshShownView();");

        Assert.Contains("GET /api/crew?days=0", page.Fetched());
    }

    [Fact]
    public void The_wizard_does_not_call_the_history_ready_before_the_scan_has_begun()
    {
        var page = new Page();

        page.Do("paintSetupScan({ running: false }, true);");

        Assert.Equal("Reading game files first — logs next", page.NodeText("#setup-scan-label"));
        Assert.Equal("0%", page.Text("__dom.node('#setup-scan-fill').style.width"));
    }
}
