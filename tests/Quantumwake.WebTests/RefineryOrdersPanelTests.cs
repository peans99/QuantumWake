namespace Quantumwake.WebTests;

/// <summary>
/// Refinery orders on the runs pane, and the toasts that say one is ready.
/// </summary>
/// <remarks>
/// <para>
/// Two kinds of ready, and the page must never say one in the other's words:
/// the game's log said so, or the terminal's clock - read off a screenshot -
/// ran out and the game has not said so. The second is an estimate, and from a
/// quote it is one that holds only if the pilot confirmed it.
/// </para>
/// <para>
/// The notification modes are the pilot's: the log, then the timer a minute
/// later if the log has not spoken (the default); the log only; the timer only;
/// or nothing. In every mode the same news is told once.
/// </para>
/// <para>
/// The clock is pinned at 03:12 UTC on the night measured - the quote at 03:10:04,
/// the running screen at 03:10:19 with 6 m 26 s left, so due 03:16:45 - and the
/// harness's setTimeout is swapped for one that keeps what it was asked to do.
/// </para>
/// </remarks>
public class RefineryOrdersPanelTests
{
    private const string Running = """
        {"orders":[{"id":"order:MIC:1","station":"MIC-L5 Modern Icarus Station","seenAt":"2026-10-04T03:10:04+00:00","shot":"running.jpg",
          "basis":"running","dueAt":"2026-10-04T03:16:45+00:00","method":"Pyrometric Chromalysis","cost":121,
          "inCscu":182,"outCscu":81,"completedAt":null,
          "lots":[{"read":"SILICON (RAW)","mineral":"Raw Silicon","quality":510,"quantity":142,"yield":64}]}],
         "unmatched":[],
         "measured":[{"at":"2026-10-04T03:10:04+00:00","station":"MIC-L5 Modern Icarus Station","method":"Pyrometric Chromalysis",
           "mineral":"Raw Silicon","quality":510,"inCscu":142,"outCscu":64}],
         "graceSeconds":60,"now":"2026-10-04T03:12:00+00:00"}
        """;

    private static string Completed => Running.Replace("\"completedAt\":null", "\"completedAt\":\"2026-10-04T03:16:54+00:00\"");

    private static string Quoted => Running.Replace("\"basis\":\"running\"", "\"basis\":\"quote\"");

    private static Page Opened(string picture, string? mode = null)
    {
        var page = new Page();
        page.Serve("/api/mining/refinery", picture);
        page.Do("Date.now = () => Date.parse('2026-10-04T03:12:00Z');"
            + "globalThis.__qwTimers = []; globalThis.setTimeout = (fn, ms) => { __qwTimers.push({ fn, ms }); return __qwTimers.length; };"
            + (mode is null ? "" : $"localStorage.setItem('qw.refinery.notify', '{mode}');")
            + "await loadRefineryOrders();");
        return page;
    }

    /// <summary>The timers armed for countdowns, not the minute's redraw of the list.</summary>
    private const string Armed = "__qwTimers.filter((t) => t.ms !== 60000)";

    private static int Toasts(Page page) => page.Count("__dom.node('#toasts').querySelectorAll('.toast').length");


    [Fact]
    public void A_running_order_counts_down_to_the_terminals_time()
    {
        var page = Opened(Running);

        Assert.False(page.Truth("__dom.node('#refinery-orders').hidden"));
        var text = page.NodeText("#refinery-orders-list");
        Assert.Contains("Pyrometric Chromalysis · MIC-L5 Modern Icarus Station", text);
        Assert.Contains("(4m 45s)", text);
        Assert.Contains("182 cSCU in · 81 cSCU back · cost 121 aUEC", text);
        Assert.Contains("Raw Silicon q510 142 → 64", text);
        Assert.DoesNotContain("if you confirmed", text);
    }

    [Fact]
    public void A_quote_says_it_holds_only_if_it_was_confirmed()
    {
        Assert.Contains("if you confirmed the quote", Opened(Quoted).NodeText("#refinery-orders-list"));
    }

    [Fact]
    public void A_completed_order_says_the_game_said_so()
    {
        var page = Opened(Completed);

        var text = page.NodeText("#refinery-orders-list");
        Assert.Contains("ready — the game said so at", text);
        Assert.DoesNotContain("terminal’s clock", text);
        Assert.Equal(0, page.Count(Armed + ".length"));
    }

    [Fact]
    public void A_completion_with_no_screenshot_is_listed_for_what_it_is()
    {
        var page = Opened("""
            {"orders":[],"unmatched":[{"id":"done:MIC:2","at":"2026-10-04T03:16:54+00:00","station":"MIC-L5 Modern Icarus Station"}],
             "measured":[],"graceSeconds":60}
            """);

        var text = page.NodeText("#refinery-orders-list");
        Assert.Contains("An order at MIC-L5 Modern Icarus Station", text);
        Assert.Contains("Nothing is known of what was in it", text);
    }

    [Fact]
    public void With_nothing_to_show_the_block_stays_away()
    {
        Assert.True(Opened("""{"orders":[],"unmatched":[],"measured":[],"graceSeconds":60}""")
            .Truth("__dom.node('#refinery-orders').hidden"));
    }

    [Fact]
    public void The_quoted_yields_are_a_table_of_what_went_in_and_came_out()
    {
        var page = Opened(Running);

        Assert.False(page.Truth("__dom.node('#refinery-measured').hidden"));
        var row = page.Text("__dom.node('#refinery-measured-table tbody').children[0].textContent");
        Assert.Contains("Pyrometric Chromalysis", row);
        Assert.Contains("142", row);
        Assert.Contains("64", row);
        Assert.Contains("45%", row);
    }

    /// <summary>Mixed: the timer waits a minute past the countdown for the game to speak first.</summary>
    [Fact]
    public void Mixed_mode_arms_the_timer_a_minute_after_the_countdown()
    {
        var page = Opened(Running);

        // 03:12:00 to 03:16:45 is 285 s, and a minute's grace on top.
        Assert.Equal(1, page.Count(Armed + ".length"));
        Assert.Equal((285 + 60) * 1000, page.Number(Armed + "[0].ms"));
    }

    [Fact]
    public void Timer_mode_arms_it_at_the_countdown_itself()
    {
        var page = Opened(Running, "timer");
        Assert.Equal(285 * 1000, page.Number(Armed + "[0].ms"));
    }

    [Theory]
    [InlineData("log")]
    [InlineData("off")]
    public void Log_only_and_off_arm_no_timer(string mode)
    {
        Assert.Equal(0, Opened(Running, mode).Count(Armed + ".length"));
    }

    /// <summary>
    /// The order ran out before the page was opened: it is on the list as due,
    /// and announcing it now would replay history on every reload.
    /// </summary>
    [Fact]
    public void An_order_already_due_at_load_arms_nothing()
    {
        var page = Opened(Running.Replace("2026-10-04T03:16:45+00:00", "2026-10-04T03:11:00+00:00"));

        Assert.Equal(0, page.Count(Armed + ".length"));
        Assert.Contains("should be ready since", page.NodeText("#refinery-orders-list"));
        Assert.Contains("the game has not said", page.NodeText("#refinery-orders-list"));
    }

    [Fact]
    public void When_the_timer_fires_and_the_game_has_not_spoken_it_says_so()
    {
        var page = Opened(Running);
        page.Do($"await {Armed}[0].fn();");

        Assert.Equal(1, Toasts(page));
        Assert.Equal(1, page.Count("Array.from(__dom.node('#toasts').children).filter((c) => c.className === 'toast refinery-due').length"));
        Assert.Contains("Refinery order should be ready", page.NodeText("#toasts"));
        Assert.Contains("the game has not said so", page.NodeText("#toasts"));
    }

    /// <summary>The game's toast came in the minute's grace: it has been heard, and the timer keeps quiet.</summary>
    [Fact]
    public void When_the_game_spoke_first_the_timer_keeps_quiet()
    {
        var page = Opened(Running);
        page.Serve("/api/mining/refinery", Completed);
        page.Do($"await {Armed}[0].fn();");

        Assert.Equal(0, Toasts(page));
        Assert.Contains("the game said so", page.NodeText("#refinery-orders-list"));
    }

    private const string GameSaid =
        "{at:'2026-10-04T03:16:54Z',kind:'refinery',text:'Refinery order complete',detail:'MIC-L5 Modern Icarus Station'}";

    private const string Earlier = "{at:'2026-10-04T03:05:00Z',kind:'location',text:'Arrived',detail:'MIC-L5'}";

    private static Page Streamed(string? mode)
    {
        var page = Opened(Running, mode);
        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");

        // The game's toast lands at 03:16:54; the stream carries it after that.
        page.Do("Date.now = () => Date.parse('2026-10-04T03:17:00Z');");
        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[{Earlier}] }});");
        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[{GameSaid},{Earlier}] }});");
        return page;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("log")]
    public void The_games_own_toast_is_shown_when_the_log_is_listened_to(string? mode)
    {
        var page = Streamed(mode);
        Assert.Equal(1, page.Count("Array.from(__dom.node('#toasts').children).filter((c) => c.className === 'toast refinery').length"));
        Assert.Contains("MIC-L5 Modern Icarus Station", page.NodeText("#toasts"));
    }

    [Theory]
    [InlineData("timer")]
    [InlineData("off")]
    public void The_games_own_toast_is_held_back_when_the_pilot_asked_for_the_timer_or_nothing(string mode)
    {
        Assert.Equal(0, Toasts(Streamed(mode)));
    }

    /// <summary>The game's toast changes the orders, so the list asks for them again.</summary>
    [Fact]
    public void The_games_toast_fetches_the_orders_again()
    {
        var page = Streamed(null);
        Assert.True(page.Fetched().Count(call => call.EndsWith("/api/mining/refinery")) >= 2);
    }

    [Fact]
    public void The_choice_is_kept_for_this_viewer()
    {
        var page = Opened(Running);
        page.Do("bindRefineryNotify(); const s = __dom.node('#refinery-notify'); s.value = 'timer'; s.fire('change');");

        Assert.Equal("timer", page.Text("localStorage.getItem('qw.refinery.notify')"));
        Assert.Equal("timer", page.Text("refineryNotifyMode()"));
    }

    // ---- waiting, refining, collected ----

    private static string CollectedAt(string picture) =>
        picture.Replace("\"completedAt\":\"2026-10-04T03:16:54+00:00\"",
            "\"completedAt\":\"2026-10-04T03:16:54+00:00\",\"collectedAt\":\"2026-10-04T03:40:00+00:00\"");

    [Fact]
    public void A_ready_order_not_yet_collected_is_waiting_and_says_where()
    {
        var page = Opened(Completed);

        var text = page.NodeText("#refinery-orders-list");
        Assert.Contains("Waiting for you at MIC-L5 Modern Icarus Station", text);
        Assert.Contains("Waiting for you", text);
        Assert.Contains("Collected", text);   // the button

        // The pane's header says it too, when the pane is the one open.
        page.Do("miningPane = 'runs'; renderMiningWorkspaceHeader();");
        Assert.Contains("Waiting for you at MIC-L5 Modern Icarus Station", page.NodeText("#mining-workspace-status"));
    }

    [Fact]
    public void A_running_order_is_refining_and_cannot_be_collected_yet()
    {
        var page = Opened(Running);

        var text = page.NodeText("#refinery-orders-list");
        Assert.Contains("Nothing waiting at a refinery.", text);
        Assert.Contains("Refining", text);
        Assert.Equal(0, page.Count("__dom.node('#refinery-orders-list').querySelectorAll('button').length"));
    }

    /// <summary>Collected moves it to the history, and can be taken back: a misclick must not lose where the ore is.</summary>
    [Fact]
    public void A_collected_order_is_history_with_a_way_back()
    {
        var page = Opened(CollectedAt(Completed));

        var text = page.NodeText("#refinery-orders-list");
        Assert.Contains("Nothing waiting at a refinery.", text);
        Assert.Contains("collected", text);
        Assert.Contains("Not collected", text);

        page.Do("miningPane = 'runs'; renderMiningWorkspaceHeader();");
        Assert.DoesNotContain("Waiting for you at", page.NodeText("#mining-workspace-status"));
    }

    [Fact]
    public void Pressing_collected_tells_the_server_which_order()
    {
        var page = Opened(Completed);
        page.Serve("/api/mining/refinery/collected?id=order%3AMIC%3A1", CollectedAt(Completed));
        page.Do("Array.from(__dom.node('#refinery-orders-list').querySelectorAll('button')).find((b) => b.textContent === 'Collected').click();");

        Assert.Contains("POST /api/mining/refinery/collected?id=order%3AMIC%3A1", page.Fetched());
        Assert.Contains("Not collected", page.NodeText("#refinery-orders-list"));
        Assert.True(page.Truth("__dom.node('#now-refinery-card').hidden"));
    }

    [Fact]
    public void A_collected_order_arms_no_timer()
    {
        var page = Opened(Running.Replace("\"completedAt\":null", "\"completedAt\":null,\"collectedAt\":\"2026-10-04T03:11:00+00:00\""));
        Assert.Equal(0, page.Count(Armed + ".length"));
    }

    // ---- the Now card ----

    [Fact]
    public void The_now_card_says_where_your_ore_is_waiting()
    {
        var page = Opened(Completed);

        Assert.False(page.Truth("__dom.node('#now-refinery-card').hidden"));
        Assert.Equal("Waiting for you at MIC-L5 Modern Icarus Station", page.NodeText("#now-refinery"));
        Assert.Contains("ready — the game said so", page.NodeText("#now-refinery-list"));
        Assert.Contains("Collected", page.NodeText("#now-refinery-list"));
    }

    [Fact]
    public void The_now_card_shows_what_is_still_refining()
    {
        var page = Opened(Running);

        Assert.False(page.Truth("__dom.node('#now-refinery-card').hidden"));
        Assert.Equal("Refining at MIC-L5 Modern Icarus Station", page.NodeText("#now-refinery"));
        Assert.Contains("(4m 45s)", page.NodeText("#now-refinery-list"));
    }

    [Fact]
    public void With_everything_collected_the_now_card_stays_away()
    {
        Assert.True(Opened(CollectedAt(Completed)).Truth("__dom.node('#now-refinery-card').hidden"));
    }

    [Fact]
    public void A_lone_completion_waits_on_the_now_card_too()
    {
        var page = Opened("""
            {"orders":[],"unmatched":[{"id":"done:MIC:2","at":"2026-10-04T03:16:54+00:00","station":"MIC-L5 Modern Icarus Station"}],
             "measured":[],"graceSeconds":60}
            """);

        Assert.Equal("Waiting for you at MIC-L5 Modern Icarus Station", page.NodeText("#now-refinery"));
    }

        // ---- the Log tab ----

    [Fact]
    public void A_refinery_reading_lists_its_lots_on_the_log()
    {
        var page = new Page();
        page.Do("""
            const box = document.createElement('div');
            renderSighting(box, { kind: 'Refinery', refinery: {
              station: 'MIC-L5 Modern Icarus Station', stage: 'setup', method: 'Pyrometric Chromalysis',
              ratings: 'HIGH YIELD // LOW COST // SLOWEST', toRefine: 182, cost: 121, seconds: 395, capacityPercent: 5435,
              lots: [{ read: 'SILICON (RAW)', mineral: 'Raw Silicon', quality: 510, quantity: 142, yield: 64 }] } });
            globalThis.__qwBox = box;
            """);

        var text = page.Text("__qwBox.textContent");
        Assert.Contains("a refinery quote at MIC-L5 Modern Icarus Station", text);
        Assert.Contains("high yield // low cost // slowest", text);
        Assert.Contains("182 cSCU in", text);
        Assert.Contains("6m 35s", text);
        Assert.Contains("5,435% capacity", text);
        Assert.Contains("quality 510 · 142 cSCU · → 64 cSCU back", text);
    }

    /// <summary>A gem's SCU in thousandths, and shares that do not add up said to.</summary>
    [Fact]
    public void A_gem_scan_on_the_log_says_its_shares_do_not_add_up()
    {
        var page = new Page();
        page.Do("""
            const box = document.createElement('div');
            renderSighting(box, { kind: 'Mining', mining: {
              primary: 'Aphorite', massKg: 0.12, scu: 0.00315, shareTotal: 79.99, sharesAddUp: false,
              parts: [{ read: 'APHORITE', mineral: 'Aphorite', percent: 76.25, quality: 348 },
                      { read: 'INERT MATERIALS', mineral: 'Inert materials', percent: 3.74, quality: null }] } });
            globalThis.__qwBox = box;
            """);

        var text = page.Text("__qwBox.textContent");
        Assert.Contains("3.15m SCU", text);
        Assert.Contains("quality did not read", text);
        Assert.Contains("add up to 79.99%, not 100", text);
    }
}
