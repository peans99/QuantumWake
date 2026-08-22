namespace Quantumwake.WebTests;

/// <summary>The wipe control: what it shows, and what it sends.</summary>
public class WipeSettingTests
{
    private static Page WithWipe(string json)
    {
        var page = new Page();
        page.Serve("/api/wipe", json);
        page.Do("await loadWipe();");
        return page;
    }

    [Fact]
    public void The_date_and_patch_are_shown_as_they_stand()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","hidden":18,"stored":148}
            """);

        Assert.Equal("2026-05-15", page.Text("__dom.node('#wipe-at').value"));
        Assert.Equal("Alpha 4.8", page.Text("__dom.node('#wipe-patch').value"));
    }

    /// <summary>
    /// The count is the honest part: it says the history is held back, not gone.
    /// </summary>
    [Fact]
    public void It_says_how_much_is_kept_but_not_counted()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","hidden":18,"stored":148}
            """);

        Assert.Contains("18 sessions before this are kept but not counted",
            page.NodeText("#wipe-status"));
    }

    [Fact]
    public void A_wipe_with_nothing_before_it_says_that_instead()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","hidden":0,"stored":12}
            """);

        Assert.Contains("nothing on record from before", page.NodeText("#wipe-status"));
    }

    [Fact]
    public void Counting_everything_reads_as_counting_everything()
    {
        var page = WithWipe("""{"at":null,"patch":"no wipe","hidden":0,"stored":148}""");

        Assert.Equal("", page.Text("__dom.node('#wipe-at').value"));
        Assert.Equal("", page.Text("__dom.node('#wipe-patch').value"));
        Assert.Contains("counting all 148 sessions", page.NodeText("#wipe-status"));
    }

    [Fact]
    public void Saving_sends_the_day_that_was_picked()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","hidden":18,"stored":148}
            """);

        page.Do("""
            __dom.node('#wipe-at').value = '2026-07-19';
            __dom.node('#wipe-patch').value = 'Alpha 4.9';
            __dom.node('#wipe-save').fire('click');
            """);

        var body = page.BodyOf("/api/wipe");

        Assert.Contains("2026-07-19T00:00:00Z", body);
        Assert.Contains("Alpha 4.9", body);
    }

    [Fact]
    public void Counting_everything_sends_no_date_at_all()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","hidden":18,"stored":148}
            """);

        page.Do("__dom.node('#wipe-clear').fire('click');");

        Assert.Contains("\"at\":null", page.BodyOf("/api/wipe"));
    }

    /// <summary>An empty date is a slip, not an instruction to count everything.</summary>
    [Fact]
    public void Saving_an_empty_date_asks_rather_than_guesses()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","hidden":18,"stored":148}
            """);

        page.Do("""
            __dom.node('#wipe-at').value = '';
            __dom.node('#wipe-save').fire('click');
            """);

        Assert.Contains("pick a date", page.NodeText("#wipe-status"));
        Assert.DoesNotContain("POST /api/wipe", page.Fetched());
    }
}
