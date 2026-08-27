namespace Quantumwake.WebTests;

/// <summary>
/// The wipe line the first run offers, before anything has been counted.
/// </summary>
/// <remarks>
/// <para>
/// This is the most consequential field in the wizard and the easiest to press
/// past: it decides how much of somebody's history is counted at all, and it is
/// answered by a reader who has not seen the app work yet. It used to default
/// to the newest patch this install had logs from, on the reasoning that a
/// first run against years of logs is when totals look wrong. But a patch is
/// not a wipe - 4.9 and 4.10 both kept long-term persistence - so the field
/// asserted a reset that had not happened, and every session before that patch
/// stopped being counted without anybody deciding it should.
/// </para>
/// <para>
/// So it offers the last wipe there is evidence of, and names the newest patch
/// underneath as something to choose rather than something already chosen.
/// </para>
/// </remarks>
public class SetupWipeTests
{
    /// <summary>
    /// A first run: the line still sits at the shipped default, and the logs
    /// carry a patch newer than it - which is the case that used to mislead.
    /// </summary>
    private const string FreshInstall = """
        {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8",
         "suggested":{"patch":"Alpha 4.10","at":"2026-08-26T18:54:26+00:00"},
         "covers":["money","ships","inventory","history"],
         "hidden":0,"stored":149,"default":"2026-05-15T00:00:00+00:00"}
        """;

    private static Page Wizard(string wipe = FreshInstall)
    {
        var page = new Page();
        page.Serve("/api/setup", """{"done":false}""");
        page.Serve("/api/overlay", """{"available":false,"visible":false}""");
        page.Serve("/api/uex/feeds", "[]");
        page.Serve("/api/wipe", wipe);
        page.Serve("/api/setup/done", "{}");
        page.Do("await maybeShowSetup();");
        return page;
    }

    /// <summary>
    /// The date offered is the wipe, not the patch. Offering 26 August here
    /// would drop every session from the four months before it.
    /// </summary>
    [Fact]
    public void The_offered_date_is_the_last_wipe_not_the_newest_patch()
    {
        var page = Wizard();

        Assert.Equal("2026-05-15", page.Text("__dom.node('#setup-wipe').value"));
    }

    /// <summary>
    /// The newer patch is still named - somebody whose account really was reset
    /// by it needs to find that date - but as a choice, with the reason to think
    /// twice attached.
    /// </summary>
    [Fact]
    public void The_newest_patch_is_named_as_an_alternative()
    {
        var note = Wizard().NodeText("#setup-wipe-note");

        Assert.Contains("Alpha 4.8", note);
        Assert.Contains("evidence", note);
        Assert.Contains("Alpha 4.10", note);
        Assert.Contains("only if", note);
    }

    /// <summary>
    /// Keeping the offer records which wipe it was. "set at first run" says when
    /// the line was decided, which is not what the Settings page has to explain
    /// to somebody reading it months later.
    /// </summary>
    [Fact]
    public void Keeping_the_offer_records_the_patch_that_wiped()
    {
        var page = Wizard();
        page.Serve("/api/wipe", FreshInstall);
        page.Do("__dom.node('#setup-start').click();");

        Assert.Contains("\"patch\":\"Alpha 4.8\"", page.BodyOf("/api/wipe"));
        Assert.Contains("2026-05-15", page.BodyOf("/api/wipe"));
    }

    /// <summary>
    /// A date typed over the offer is nobody's known patch, and is not labelled
    /// with one.
    /// </summary>
    [Fact]
    public void A_date_chosen_by_hand_claims_no_patch()
    {
        var page = Wizard();
        page.Serve("/api/wipe", FreshInstall);
        page.Do("""
            __dom.node('#setup-wipe').value = '2026-07-19';
            __dom.node('#setup-start').click();
            """);

        Assert.Contains("\"patch\":\"set at first run\"", page.BodyOf("/api/wipe"));
        Assert.Contains("2026-07-19", page.BodyOf("/api/wipe"));
    }

    /// <summary>
    /// An install whose logs show no patch at all still gets the shipped
    /// default, rather than today - which would count nothing.
    /// </summary>
    [Fact]
    public void With_no_patch_in_the_logs_the_default_still_stands()
    {
        const string nothing = """
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","suggested":null,
             "covers":["money","ships","inventory","history"],
             "hidden":0,"stored":0,"default":"2026-05-15T00:00:00+00:00"}
            """;

        var page = Wizard(nothing);

        Assert.Equal("2026-05-15", page.Text("__dom.node('#setup-wipe').value"));
        Assert.Contains("Alpha 4.8", page.NodeText("#setup-wipe-note"));
    }
}
