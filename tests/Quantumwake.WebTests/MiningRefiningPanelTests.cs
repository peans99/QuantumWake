namespace Quantumwake.WebTests;

/// <summary>
/// The mining log once a haul has stages.
/// </summary>
/// <remarks>
/// Two things the page must not do, neither of which looks broken. It must not
/// say the refinery is finished - it has only noticed that the time the pilot
/// typed has passed - and it must not send an unreadable date as a real one,
/// because a job with no expected time is supposed never to claim it is ready.
/// </remarks>
public class MiningRefiningPanelTests
{
    private const string Log = """
        [{"id":"m1","at":"2026-09-06T08:00:00+00:00","place":"Daymar","resource":"Quantainium",
          "scu":32,"quality":3,"revenue":null,"refinery":null,"stage":"Extracted","lost":null},
         {"id":"m2","at":"2026-09-05T08:00:00+00:00","place":"Yela","resource":"Taranite",
          "scu":16,"quality":null,"revenue":null,"stage":"Ready","lost":null,
          "refinery":{"place":"ArcCorp 141","method":null,"cost":4800,
                      "submittedAt":"2026-09-05T09:00:00+00:00",
                      "expectedAt":"2026-09-05T15:00:00+00:00","yield":null,"collectedAt":null}},
         {"id":"m3","at":"2026-09-04T08:00:00+00:00","place":"Aberdeen","resource":"Bexalite",
          "scu":24,"quality":null,"revenue":180000,"stage":"Sold","lost":6,
          "refinery":{"place":"Everus","method":null,"cost":null,
                      "submittedAt":"2026-09-04T09:00:00+00:00","expectedAt":null,
                      "yield":18,"collectedAt":"2026-09-04T18:00:00+00:00"}}]
        """;

    private const string Pending = """
        [{"id":"m2","place":"Yela","resource":"Taranite","scu":16,"stage":"Ready",
          "refinery":{"place":"ArcCorp 141","expectedAt":"2026-09-05T15:00:00+00:00"},
          "caveat":"The game keeps the refinery timer and logs nothing about it, so this is the time you told us to expect."}]
        """;

    /// <summary>
    /// Presses the button with this label.
    /// </summary>
    /// <remarks>
    /// By label rather than by index: every row also carries Remove, and
    /// opening a stage form inserts another button between the rows - so
    /// indices move under the test's feet for reasons that have nothing to do
    /// with what is being checked.
    /// </remarks>
    private static string Press(string label) =>
        "Array.from(__dom.node('#mining-log tbody').querySelectorAll('button'))"
        + $".find((b) => b.textContent === '{label}').click();";

    private static Page Logged(string log = Log, string pending = Pending)
    {
        var page = new Page();
        page.Serve("/api/mining/log", log);
        page.Serve("/api/mining/pending", pending);
        page.Do("await loadMiningLog(); await loadMiningPending();");
        return page;
    }

    [Fact]
    public void Each_haul_says_which_stage_it_is_at()
    {
        var text = Logged().NodeText("#mining-log tbody");

        Assert.Contains("In your hold", text);
        Assert.Contains("Due back by now", text);
        Assert.Contains("Sold", text);
    }

    /// <summary>
    /// What went in beside what came back. One column would hide the difference,
    /// which is the number the whole stage exists to produce.
    /// </summary>
    [Fact]
    public void What_came_back_is_shown_next_to_what_went_in()
    {
        var text = Logged().NodeText("#mining-log tbody");

        Assert.Contains("24", text);
        Assert.Contains("18", text);
    }

    [Fact]
    public void The_button_offers_whatever_comes_next()
    {
        var text = Logged().NodeText("#mining-log tbody");

        Assert.Contains("Send to a refinery", text);
        Assert.Contains("Collect it", text);
    }

    /// <summary>
    /// A sold haul is finished. Offering a next step on it would invite a
    /// stage the server refuses anyway, and the page should not offer what it
    /// knows will be turned down.
    /// </summary>
    [Fact]
    public void A_finished_haul_offers_nothing_more()
    {
        var page = Logged("""
            [{"id":"m3","at":"2026-09-04T08:00:00+00:00","place":"Aberdeen","resource":"Bexalite",
              "scu":24,"quality":null,"revenue":180000,"stage":"Sold","lost":6,"refinery":null}]
            """, "[]");

        var text = page.NodeText("#mining-log tbody");

        // Remove is still there; what must not be is an invitation to a stage
        // the server would refuse.
        Assert.DoesNotContain("Send to a refinery", text);
        Assert.DoesNotContain("Collect it", text);
        Assert.DoesNotContain("Record what it sold for", text);
    }

    /// <summary>
    /// The app has not been told a refinery finished. It has noticed that the
    /// pilot's own estimate has gone by, and the wording has to be that.
    /// </summary>
    [Fact]
    public void Waiting_hauls_say_the_time_came_from_you()
    {
        var page = Logged();

        Assert.False(page.Truth("__dom.node('#mining-pending').hidden"));
        Assert.Contains("the time you told us to expect", page.NodeText("#mining-pending-note"));
        Assert.Contains("you expected it by", page.NodeText("#mining-pending-list"));
    }

    [Fact]
    public void With_nothing_at_a_refinery_the_block_stays_away()
    {
        Assert.True(Logged(Log, "[]").Truth("__dom.node('#mining-pending').hidden"));
    }

    /// <summary>
    /// A date nobody can parse is sent as nothing, so the haul keeps saying
    /// "at a refinery" rather than claiming to be ready at a time invented for
    /// it.
    /// </summary>
    [Fact]
    public void An_unreadable_expected_time_is_sent_as_none()
    {
        var page = Logged();
        page.Serve("/api/mining/log/m1/submit", "{}");

        page.Do(Press("Send to a refinery"));
        page.Do("const f = __dom.node('#mining-log tbody').querySelectorAll('input');"
            + " f[0].value = 'ArcCorp 141'; f[3].value = 'whenever';"
            + Press("Save"));

        Assert.Contains("\"expectedAt\":null", page.BodyOf("/api/mining/log/m1/submit"));
        Assert.Contains("ArcCorp 141", page.BodyOf("/api/mining/log/m1/submit"));
    }

    [Fact]
    public void Collecting_sends_what_came_back()
    {
        var page = Logged();
        page.Serve("/api/mining/log/m2/collect?yield=12", "{}");

        page.Do(Press("Collect it"));
        page.Do("const f = __dom.node('#mining-log tbody').querySelectorAll('input');"
            + " f[0].value = '12';"
            + Press("Save"));

        Assert.Contains(page.Fetched(), u => u.Contains("/collect?yield=12"));
    }
}
