namespace Quantumwake.WebTests;

/// <summary>
/// The run review panel: two figures, and the honesty underneath them.
/// </summary>
/// <remarks>
/// The server already refuses to attribute a movement it cannot place. What
/// these hold is that the page does not quietly undo that - by folding the
/// unclaimed list away, by showing a total without what it left out, or by
/// replacing an estimate with a correction so there is nothing left to compare.
/// </remarks>
public class RunReviewPanelTests
{
    private const string Review = """
        {"tripId":"t1","title":"Ore run",
         "startedAt":"2026-09-01T12:00:00+00:00","finishedAt":"2026-09-01T16:00:00+00:00",
         "elapsedSeconds":14400,
         "earned":{"value":288000,
           "rule":"Money in, from movements this run's stops can account for. The place is back-tracked from the last arrival before it.",
           "from":[{"at":"2026-09-01T12:30:00+00:00","kind":"Cargo sold","what":"Agricium","where":"Hurston","amount":288000,"confirmed":false}],
           "excluded":["1 movement during this run that no stop can account for - listed below rather than added in.",
                       "1 of these are what a terminal was asked for rather than what it confirmed - the game logs the request, never the answer."]},
         "spent":{"value":64000,"rule":"Money out.","from":[],"excluded":[]},
         "stops":[
           {"stopId":"s1","place":"Hurston","note":"Pick up ore","doneAt":"2026-09-01T13:00:00+00:00",
            "planned":[{"id":"a1","kind":"sell","text":"96 SCU Agricium","quantity":96,"unit":"SCU","done":true,"actual":null}],
            "claimed":[{"at":"2026-09-01T12:30:00+00:00","kind":"Cargo sold","what":"Agricium","where":"Hurston","amount":288000,"confirmed":false}]},
           {"stopId":"s2","place":"Crusader","note":null,"doneAt":null,"planned":[],"claimed":[]}],
         "unclaimed":[{"at":"2026-09-01T14:00:00+00:00","kind":"Cargo sold","what":"Waste","where":"Area18","amount":120000,"confirmed":false}]}
        """;

    private static Page Reviewed(string review = Review)
    {
        var page = new Page();
        page.Serve("/api/trips/t1/review", review);
        page.Do("await showRunReview('t1');");
        return page;
    }

    [Fact]
    public void The_two_figures_lead()
    {
        var text = Reviewed().NodeText("#cargo-body");

        Assert.Contains("Money in", text);
        Assert.Contains("288,000 aUEC", text);
        Assert.Contains("Money out", text);
        Assert.Contains("64,000 aUEC", text);
    }

    /// <summary>
    /// Money that moved during the run and cannot be placed is the most useful
    /// thing on the page when a total looks wrong, so it is listed rather than
    /// folded away or added in.
    /// </summary>
    [Fact]
    public void Money_no_stop_can_account_for_is_shown_on_the_page()
    {
        var text = Reviewed().NodeText("#cargo-body");

        Assert.Contains("not at a stop", text);
        Assert.Contains("Waste at Area18", text);
        Assert.Contains("120,000 aUEC", text);
        Assert.Contains("would be a guess", text);
    }

    /// <summary>
    /// A number nobody is questioning does not need three lines of provenance;
    /// a number somebody is questioning needs all of it. So it folds, and
    /// starts folded.
    /// </summary>
    [Fact]
    public void The_reasoning_is_there_but_folded_until_asked_for()
    {
        var page = Reviewed();

        Assert.Contains("Why this number?", page.NodeText("#cargo-body"));
        Assert.True(page.Truth("__dom.node('#cargo-body').byClass('run-why')[0].hidden"));

        page.Do("__dom.node('#cargo-body').byClass('ghost')[0].click();");

        Assert.False(page.Truth("__dom.node('#cargo-body').byClass('run-why')[0].hidden"));
    }

    /// <summary>
    /// The exclusions are the half that answers what people actually ask, which
    /// is "why is this lower than I expected".
    /// </summary>
    [Fact]
    public void A_figure_says_what_it_left_out()
    {
        var page = Reviewed();
        page.Do("__dom.node('#cargo-body').byClass('ghost')[0].click();");

        var text = page.NodeText("#cargo-body");

        Assert.Contains("no stop can account for", text);
        Assert.Contains("asked for rather than what it confirmed", text);
        Assert.Contains("back-tracked", text);
    }

    /// <summary>
    /// A stop that moved no money is ordinary, not a gap in the data — most
    /// stops are like this, and blank space reads as something missing.
    /// </summary>
    [Fact]
    public void A_stop_where_nothing_happened_says_so()
    {
        Assert.Contains("no money moved here", Reviewed().NodeText("#cargo-body"));
    }

    /// <summary>
    /// Both numbers stay on screen. Replacing the estimate with the correction
    /// leaves nothing to compare, which is the whole reason a review records
    /// either of them.
    /// </summary>
    [Fact]
    public void A_correction_sits_beside_the_estimate_rather_than_over_it()
    {
        var page = Reviewed();
        var text = page.NodeText("#cargo-body");

        Assert.Contains("planned 96 SCU", text);
        Assert.Equal(1, page.Count("__dom.node('#cargo-body').byClass('run-actual').length"));
    }

    [Fact]
    public void Recording_what_it_came_to_sends_the_amount()
    {
        var page = Reviewed();
        page.Do("const box = __dom.node('#cargo-body').byClass('run-actual')[0];"
            + " box.value = '88'; box.fire('change');");

        Assert.Contains(page.Fetched(), u => u.Contains("/actions/a1/actual?amount=88"));
    }

    /// <summary>
    /// Clearing is not correcting to zero: a stop worth nothing and a stop
    /// nobody has checked are different facts.
    /// </summary>
    [Fact]
    public void Clearing_the_box_sends_no_amount_at_all()
    {
        var page = Reviewed();
        page.Do("const box = __dom.node('#cargo-body').byClass('run-actual')[0];"
            + " box.value = ''; box.fire('change');");

        var call = Assert.Single(page.Fetched(), u => u.Contains("/actual"));

        Assert.DoesNotContain("amount=", call);
    }

    [Fact]
    public void A_plan_that_was_never_started_says_why_there_is_nothing_to_see()
    {
        var page = new Page();
        page.Fail("/api/trips/t1/review", 400, """{"problem":"never started"}""");
        page.Do("await showRunReview('t1');");

        Assert.Contains("never been started", page.NodeText("#cargo-body"));
    }
}
