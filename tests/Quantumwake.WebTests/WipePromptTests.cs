namespace Quantumwake.WebTests;

/// <summary>
/// The offer to move the wipe line when a patch has landed since it. The app
/// brings the date; the player answers the question the logs cannot.
/// </summary>
public class WipePromptTests
{
    private const string WithPatch = """
        {
          "at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8",
          "suggested":{"patch":"Alpha 4.9","at":"2026-07-19T15:49:30+00:00"},
          "covers":["money","ships","inventory","history"],"hidden":28,"stored":149
        }
        """;

    private static Page Asking(string wipe = WithPatch)
    {
        var page = new Page();
        page.Serve("/api/wipe", wipe);
        page.Do("await checkForWipe();");
        return page;
    }

    private static bool Asked(Page page) => !page.Truth("__dom.node('#patch').hidden");

    [Fact]
    public void A_patch_since_the_wipe_is_offered_with_its_date()
    {
        var page = Asking();

        Assert.True(Asked(page));
        Assert.Contains("Alpha 4.9 arrived on", page.NodeText("#patch-title"));
        Assert.Contains("If that patch wiped", page.NodeText("#patch-detail"));
    }

    [Fact]
    public void Nothing_is_asked_when_no_patch_has_landed_since()
    {
        var page = Asking("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","suggested":null,
             "covers":["money"],"hidden":28,"stored":149}
            """);

        Assert.False(Asked(page));
    }

    /// <summary>
    /// With no line drawn at all, the warning is the other way round: the totals
    /// are counting an account that may already be gone.
    /// </summary>
    [Fact]
    public void With_no_wipe_set_it_says_what_is_being_counted()
    {
        var page = Asking("""
            {"at":null,"patch":"no wipe",
             "suggested":{"patch":"Alpha 4.9","at":"2026-07-19T15:49:30+00:00"},
             "covers":["money"],"hidden":0,"stored":149}
            """);

        Assert.Contains("counting an account you no longer have", page.NodeText("#patch-detail"));
    }

    [Fact]
    public void Saying_it_wiped_moves_the_line_to_that_patch()
    {
        var page = Asking();

        page.Do("__dom.node('#patch-wiped').fire('click');");

        var body = page.BodyOf("/api/wipe");

        Assert.Contains("2026-07-19", body);
        Assert.Contains("Alpha 4.9", body);
        Assert.True(page.Truth("__dom.node('#patch').hidden"));
    }

    [Fact]
    public void Saying_it_did_not_wipe_changes_nothing()
    {
        var page = Asking();

        page.Do("__dom.node('#patch-kept').fire('click');");

        Assert.True(page.Truth("__dom.node('#patch').hidden"));
        Assert.DoesNotContain("POST /api/wipe", page.Fetched());
    }

    /// <summary>Asked once. Either answer settles that patch for good.</summary>
    [Fact]
    public void A_patch_already_answered_is_not_asked_about_again()
    {
        var page = Asking();
        page.Do("__dom.node('#patch-kept').fire('click');");

        page.Do("await checkForWipe();");

        Assert.False(Asked(page));
    }

    [Fact]
    public void Answering_one_patch_does_not_answer_the_next()
    {
        var page = Asking();
        page.Do("__dom.node('#patch-kept').fire('click');");

        page.Serve("/api/wipe", """
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8",
             "suggested":{"patch":"Alpha 5.0","at":"2026-11-02T10:00:00+00:00"},
             "covers":["money"],"hidden":28,"stored":149}
            """);

        page.Do("await checkForWipe();");

        Assert.True(Asked(page));
        Assert.Contains("Alpha 5.0", page.NodeText("#patch-title"));
    }

    /// <summary>
    /// A wipe is a day, not a moment: stored at midnight UTC and typed into a
    /// field that means UTC. Read back in local time it shows the day before to
    /// everyone west of Greenwich, and a date that disagrees with the field
    /// beside it stops being believed.
    /// </summary>
    [Fact]
    public void The_day_shown_is_the_day_that_was_stored()
    {
        var page = Asking();

        Assert.Contains("May 15, 2026", page.NodeText("#patch-detail"));
    }

    [Fact]
    public void A_server_that_cannot_answer_asks_nothing()
    {
        var page = new Page();
        page.Do("await checkForWipe();");

        Assert.False(Asked(page));
    }
}
