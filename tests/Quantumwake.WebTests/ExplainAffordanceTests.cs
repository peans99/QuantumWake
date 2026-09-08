namespace Quantumwake.WebTests;

/// <summary>
/// The "why?" control on a figure.
/// </summary>
/// <remarks>
/// The server already refuses the two dangerous answers - a blank explanation
/// for something it cannot explain, and an empty record list presented as an
/// answer. What the page can still get wrong is putting the control on figures
/// that have no explanation, rewording the rule so two builds say different
/// things, and asking the server before anybody has asked it anything.
/// </remarks>
public class ExplainAffordanceTests
{
    private const string Entries = """
        [{"at":"2026-08-20T09:00:00+00:00","kind":"Cargo sold","what":"Agricium","where":"Port Tressler",
          "shop":"TDD","amount":288000,"confirmed":true,"running":288000}]
        """;

    private const string Explained = """
        {"figure":"ledger.in","label":"Money in","value":288000,"source":"your logs",
         "rule":"Every movement of money your logs recorded, into your account.",
         "records":[{"at":"2026-08-20T09:00:00+00:00","what":"Agricium","where":"Port Tressler",
                     "amount":288000,"confirmed":true}],
         "excluded":["1 of these were requested rather than confirmed."],
         "fromRecords":true}
        """;

    private static Page Ledger(string explained = Explained)
    {
        var page = new Page();
        page.Serve("/api/ledger?days=0", Entries);
        page.Serve("/api/explain?figure=ledger.in&days=0", explained);
        page.Do("__dom.node('#ledger-period').value = '0'; await loadLedger();");
        return page;
    }

    /// <summary>
    /// A control on every tile would promise an explanation for numbers nobody
    /// has written one down for.
    /// </summary>
    [Fact]
    public void Only_the_figures_that_can_be_explained_carry_the_control()
    {
        var page = Ledger();

        // Money in, money out and net - but not the movement count.
        Assert.Equal(3, page.Count("__dom.node('#ledger-summary').byClass('why').length"));
    }

    /// <summary>
    /// Folded until asked for, and not fetched until then either: a figure
    /// nobody is questioning should cost nothing.
    /// </summary>
    [Fact]
    public void Nothing_is_asked_of_the_server_until_somebody_asks()
    {
        var page = Ledger();

        Assert.True(page.Truth("__dom.node('#ledger-summary').byClass('why-panel')[0].hidden"));
        Assert.DoesNotContain(page.Fetched(), u => u.Contains("/api/explain"));
    }

    [Fact]
    public void Pressing_it_shows_the_rule_and_what_was_left_out()
    {
        var page = Ledger();
        page.Do("await __dom.node('#ledger-summary').byClass('why')[0].fire('click');");

        var text = page.NodeText("#ledger-summary");

        Assert.Contains("Every movement of money your logs recorded", text);
        Assert.Contains("requested rather than confirmed", text);
        Assert.Contains("Agricium at Port Tressler", text);
    }

    [Fact]
    public void Pressing_it_again_folds_it_away()
    {
        var page = Ledger();
        page.Do("const b = __dom.node('#ledger-summary').byClass('why')[0];"
            + " await b.fire('click'); await b.fire('click');");

        Assert.True(page.Truth("__dom.node('#ledger-summary').byClass('why-panel')[0].hidden"));
    }

    /// <summary>
    /// A figure with a rule and no records answers with the rule and says there
    /// are none. A blank space under the exclusions reads as data that failed
    /// to load.
    /// </summary>
    [Fact]
    public void A_figure_with_no_records_says_so_rather_than_showing_a_gap()
    {
        var page = Ledger("""
            {"figure":"ledger.in","label":"Money in","value":0,"source":"your logs",
             "rule":"Inferred from corpse item-recovery bursts.",
             "records":[],"excluded":["This figure has no underlying records."],
             "fromRecords":false}
            """);

        page.Do("await __dom.node('#ledger-summary').byClass('why')[0].fire('click');");

        Assert.Contains("only the rule above", page.NodeText("#ledger-summary"));
    }

    /// <summary>
    /// The server owns the wording. A page that reworded a rule would be a
    /// second copy of it, drifting in exactly the direction that makes a caveat
    /// stop being true.
    /// </summary>
    [Fact]
    public void The_wording_comes_from_the_server_untouched()
    {
        var page = Ledger("""
            {"figure":"ledger.in","label":"Money in","value":1,"source":"your logs",
             "rule":"A very particular sentence, worded once.",
             "records":[],"excluded":[],"fromRecords":false}
            """);

        page.Do("await __dom.node('#ledger-summary').byClass('why')[0].fire('click');");

        Assert.Contains("A very particular sentence, worded once.", page.NodeText("#ledger-summary"));
    }

    /// <summary>
    /// A figure this build cannot explain says so. Silence would read as
    /// "there is nothing behind this number".
    /// </summary>
    [Fact]
    public void A_figure_the_server_cannot_explain_says_it_does_not_know()
    {
        var page = new Page();
        page.Serve("/api/ledger?days=0", Entries);
        page.Fail("/api/explain?figure=ledger.in&days=0", 404, """{"problem":"unknown"}""");
        page.Do("__dom.node('#ledger-period').value = '0'; await loadLedger();");
        page.Do("await __dom.node('#ledger-summary').byClass('why')[0].fire('click');");

        Assert.Contains("knows how to explain that one yet", page.NodeText("#ledger-summary"));
    }
}
