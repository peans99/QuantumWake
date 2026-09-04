namespace Quantumwake.WebTests;

/// <summary>
/// The bar that says the logs are being read.
/// </summary>
/// <remarks>
/// A cold backfill is 400 MB across ~148 files and takes about half a minute,
/// and the forced rescan on the Settings page re-reads all of it. Without a bar
/// both are indistinguishable from an app that has hung, which is the whole
/// reason this exists — so the transitions are worth asserting.
///
/// The loop itself is paced by <c>setTimeout</c>, which is inert in the stub;
/// what it does each poll lives in <c>paintScan</c> and is driven directly here.
/// </remarks>
public class ScanProgressTests
{
    private static string Status(
        bool running, int done = 0, int total = 0, int parsed = 0,
        string? file = null, int percent = 0, int elapsed = 0) =>
        $$"""
          {
            running: {{(running ? "true" : "false")}},
            done: {{done}}, total: {{total}}, parsed: {{parsed}},
            file: {{(file is null ? "null" : Page.Quote(file))}},
            percent: {{percent}}, elapsedSeconds: {{elapsed}}
          }
          """;

    private static bool Hidden(Page page) => page.Truth("__dom.node('#scan').hidden");

    [Fact]
    public void A_running_scan_shows_the_bar_and_the_file_it_is_on()
    {
        var page = new Page();

        var status = Status(running: true, done: 34, total: 148, parsed: 12,
            file: "Game-2026-08-30.log", percent: 23, elapsed: 9);

        page.Do($"paintScan({status}, false);");

        Assert.False(Hidden(page));
        Assert.Equal("Parsing logs — 12 new", page.NodeText("#scan-label"));
        Assert.Equal("34 / 148 · 9s", page.NodeText("#scan-count"));
        Assert.Equal("Game-2026-08-30.log", page.NodeText("#scan-file"));
        Assert.Equal("23%", page.Text("__dom.node('#scan-fill').style.width"));
    }

    /// <summary>
    /// A warm start reads nothing: every file matches its fingerprint. Saying
    /// "parsing" there would claim work that is not happening.
    /// </summary>
    [Fact]
    public void Nothing_parsed_yet_says_checking_rather_than_parsing()
    {
        var page = new Page();

        page.Do($"paintScan({Status(running: true, done: 5, total: 148)}, false);");

        Assert.Equal("Checking logs…", page.NodeText("#scan-label"));
    }

    /// <summary>
    /// The common case by far: the server scans at startup and the browser
    /// arrives afterwards. An idle server has not finished a scan the page ever
    /// saw, and announcing one would put "scan complete" on every single load.
    /// </summary>
    [Fact]
    public void An_idle_server_is_not_a_finished_scan()
    {
        var page = new Page();

        Assert.False(page.Truth($"paintScan({Status(running: false)}, false)"));
        Assert.True(Hidden(page));
        Assert.Equal("", page.NodeText("#scan-label"));
    }

    [Fact]
    public void A_scan_that_was_running_and_stops_reports_what_it_read()
    {
        var page = new Page();

        Assert.True(page.Truth(
            $"paintScan({Status(running: false, parsed: 148, elapsed: 31)}, true)"));

        Assert.Equal("Scan complete", page.NodeText("#scan-label"));
        Assert.Equal("148 parsed · 31s", page.NodeText("#scan-count"));
        Assert.Equal("", page.NodeText("#scan-file"));
        Assert.Equal("100%", page.Text("__dom.node('#scan-fill').style.width"));
    }

    /// <summary>
    /// The regression this was written for: the watcher used to <c>return</c>
    /// once it had drawn a finish, so the forced rescan on the Settings page —
    /// a full re-read of every log, and the slowest scan there is — ran with no
    /// bar at all. The loop now hands <c>sawRunning</c> back each poll and keeps
    /// going, so a second scan is drawn like the first.
    /// </summary>
    [Fact]
    public void A_second_scan_is_still_watched_after_the_first_one_finished()
    {
        var page = new Page();

        // The boot scan: runs, then finishes.
        page.Do($"paintScan({Status(running: true, done: 148, total: 148, parsed: 148)}, false);");
        page.Do($"paintScan({Status(running: false, parsed: 148, elapsed: 31)}, true);");

        // The loop retires the bar itself once the finish has been read.
        page.Do("__dom.node('#scan').hidden = true;");

        // A forced rescan later in the same page life.
        var rescan = Status(running: true, done: 3, total: 148, parsed: 3,
            file: "Game.log", percent: 2, elapsed: 1);

        page.Do($"paintScan({rescan}, false);");

        Assert.False(Hidden(page));
        Assert.Equal("Parsing logs — 3 new", page.NodeText("#scan-label"));
        Assert.Equal("3 / 148 · 1s", page.NodeText("#scan-count"));
    }

    /// <summary>
    /// The first-run wizard is a fixed opaque sheet over the whole page, so it
    /// carries its own copy of the bar. It counted files and called them events,
    /// which on this install would have read "148 events" for 400 MB of logs.
    /// </summary>
    [Fact]
    public void The_wizards_bar_counts_the_files_it_actually_parsed()
    {
        var page = new Page();

        var status = Status(running: true, done: 34, total: 148, parsed: 12, percent: 23);

        page.Do($"paintSetupScan({status});");

        Assert.Equal("Reading logs — 12 new", page.NodeText("#setup-scan-label"));
        Assert.Equal("34 / 148 files", page.NodeText("#setup-scan-count"));
        Assert.Equal("23%", page.Text("__dom.node('#setup-scan-fill').style.width"));
    }

    /// <summary>
    /// The line beside the Settings button, which is the only progress visible
    /// from where the rescan is actually started — the page's own bar is at the
    /// top of the document, well off-screen by the time you reach this button.
    /// </summary>
    [Fact]
    public void The_settings_rescan_counts_logs_beside_its_own_button()
    {
        var page = new Page();

        Assert.Equal("112 / 160 logs · 8s", page.Text(
            $"rescanLine({Status(running: true, done: 112, total: 160, elapsed: 8)})"));

        // Before the server has said how many there are, a count would be "0 / 0".
        Assert.Equal("rescanning…", page.Text($"rescanLine({Status(running: true)})"));
        Assert.Equal("rescanning…", page.Text($"rescanLine({Status(running: false)})"));
    }

    /// <summary>
    /// The button still does the thing, and says what came of it.
    /// </summary>
    [Fact]
    public void The_settings_rescan_reports_what_the_full_re_read_found()
    {
        var page = new Page();

        page.Serve("/api/scan?force=true", """{ "parsed": 160, "sessions": 160 }""");
        page.Serve("/api/stats", "{}");
        page.Serve("/api/sessions", "[]");
        page.Do("__dom.node('#settings-rescan').fire('click', { currentTarget: __dom.node('#settings-rescan') });");

        Assert.Contains("POST /api/scan?force=true", page.Fetched());
        Assert.Equal("160 sessions from a full re-read", page.NodeText("#settings-rescan-status"));
        Assert.False(page.Truth("__dom.node('#settings-rescan').disabled"));
    }

    /// <summary>
    /// The loop itself, with the clock let run.
    /// </summary>
    /// <remarks>
    /// The stub's timers are inert, so this hands the page one that fires at
    /// once and runs out after a bounded number of waits — a loop that never
    /// ends still ends the test. The scripted polls are a scan, its finish, and
    /// a rescan after it; the old watcher returned at the finish and the last
    /// one was drawn on nothing.
    /// </remarks>
    [Fact]
    public void The_watcher_is_still_there_for_the_scan_after_the_first_one()
    {
        var page = new Page();

        page.Do("""
            __waits = 0;
            globalThis.setTimeout = (fn) => {
              if (++__waits > 20) throw new Error('watched long enough');
              fn();
              return 0;
            };
            """);

        page.Do($$"""
            __polls = [
              {{Status(running: true, done: 40, total: 148, parsed: 40, percent: 27)}},
              {{Status(running: false, parsed: 148, elapsed: 31)}},
              {{Status(running: false)}},
              {{Status(running: true, done: 3, total: 148, parsed: 3, percent: 2)}}
            ];

            Object.defineProperty(__fetch.routes, '/api/scan/status', {
              configurable: true,
              get: () => (__polls.length > 1 ? __polls.shift() : __polls[0]),
            });
            """);

        page.Do("watchScan().catch(() => { /* the timer runs out; that is the end */ });");

        Assert.False(Hidden(page));
        Assert.Equal("Parsing logs — 3 new", page.NodeText("#scan-label"));

        // The finish reloaded the views before the second scan began.
        Assert.Contains("GET /api/sessions", page.Fetched());
    }

    [Fact]
    public void The_wizard_says_the_history_is_ready_once_the_scan_stops()
    {
        var page = new Page();

        page.Do($"paintSetupScan({Status(running: false, parsed: 148)});");

        Assert.Equal("Logs read — history ready", page.NodeText("#setup-scan-label"));
        Assert.Equal("", page.NodeText("#setup-scan-count"));
        Assert.Equal("100%", page.Text("__dom.node('#setup-scan-fill').style.width"));
    }
}
