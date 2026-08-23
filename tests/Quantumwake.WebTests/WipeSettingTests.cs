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
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","covers":["money","ships","inventory","history"],"hidden":18,"stored":148}
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
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","covers":["money","ships","inventory","history"],"hidden":18,"stored":148}
            """);

        Assert.Contains("18 sessions before this are kept but not counted",
            page.NodeText("#wipe-status"));
    }

    [Fact]
    public void A_wipe_with_nothing_before_it_says_that_instead()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","covers":["money","ships","inventory","history"],"hidden":0,"stored":12}
            """);

        Assert.Contains("nothing on record from before", page.NodeText("#wipe-status"));
    }

    [Fact]
    public void Counting_everything_reads_as_counting_everything()
    {
        var page = WithWipe("""{"at":null,"patch":"no wipe","covers":["money","ships","inventory","history"],"hidden":0,"stored":148}""");

        Assert.Equal("", page.Text("__dom.node('#wipe-at').value"));
        Assert.Equal("", page.Text("__dom.node('#wipe-patch').value"));
        Assert.Contains("counting all 148 sessions", page.NodeText("#wipe-status"));
    }

    [Fact]
    public void Saving_sends_the_day_that_was_picked()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","covers":["money","ships","inventory","history"],"hidden":18,"stored":148}
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
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","covers":["money","ships","inventory","history"],"hidden":18,"stored":148}
            """);

        page.Do("__dom.node('#wipe-clear').fire('click');");

        Assert.Contains("\"at\":null", page.BodyOf("/api/wipe"));
    }

    /// <summary>
    /// The depth is the part a single cutoff got wrong: a money wipe leaves the
    /// hangar alone, and the page has to be able to say so.
    /// </summary>
    [Fact]
    public void The_boxes_show_what_the_wipe_took()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","covers":["money"],"hidden":18,"stored":148}
            """);

        Assert.True(page.Truth("__dom.node('#wipe-money').checked"));
        Assert.False(page.Truth("__dom.node('#wipe-ships').checked"));
        Assert.False(page.Truth("__dom.node('#wipe-history').checked"));
    }

    /// <summary>
    /// "Kept but not counted" would be a lie about a partial wipe: those
    /// sessions still count towards everything it did not take.
    /// </summary>
    [Fact]
    public void A_partial_wipe_says_what_it_actually_stops_counting()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","covers":["money","inventory"],"hidden":18,"stored":148}
            """);

        Assert.Contains("18 sessions before this still count, except for money, inventory",
            page.NodeText("#wipe-status"));
    }

    [Fact]
    public void Saving_sends_the_depth_alongside_the_date()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","covers":["money","ships","inventory","history"],"hidden":18,"stored":148}
            """);

        page.Do("""
            __dom.node('#wipe-ships').checked = false;
            __dom.node('#wipe-history').checked = false;
            __dom.node('#wipe-save').fire('click');
            """);

        var body = page.BodyOf("/api/wipe");

        Assert.Contains("\"covers\":[\"money\",\"inventory\"]", body);
    }

    /// <summary>There is no second Save button to reach for.</summary>
    [Fact]
    public void Changing_a_box_saves_on_its_own()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","covers":["money","ships","inventory","history"],"hidden":18,"stored":148}
            """);

        page.Do("""
            __dom.node('#wipe-money').checked = false;
            __dom.node('#wipe-money').fire('change');
            """);

        Assert.DoesNotContain("money", page.BodyOf("/api/wipe"));
        Assert.Contains("ships", page.BodyOf("/api/wipe"));
    }

    /// <summary>An empty date is a slip, not an instruction to count everything.</summary>
    [Fact]
    public void Saving_an_empty_date_asks_rather_than_guesses()
    {
        var page = WithWipe("""
            {"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8","covers":["money","ships","inventory","history"],"hidden":18,"stored":148}
            """);

        page.Do("""
            __dom.node('#wipe-at').value = '';
            __dom.node('#wipe-save').fire('click');
            """);

        Assert.Contains("pick a date", page.NodeText("#wipe-status"));
        Assert.DoesNotContain("POST /api/wipe", page.Fetched());
    }
}
