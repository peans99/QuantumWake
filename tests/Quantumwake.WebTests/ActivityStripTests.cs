namespace Quantumwake.WebTests;

/// <summary>
/// The strip that says what the app is doing.
/// </summary>
/// <remarks>
/// It used to watch the opening log scan and then return, so the app looked
/// idle for the rest of the session however hard it was working — a game patch
/// sends it off to read 110 MB for half a minute, and a manual re-read of every
/// log said "rescanning…" and nothing else for thirty seconds.
/// </remarks>
public class ActivityStripTests
{
    private const string Scan = """
        { "key":"scan", "label":"Reading your logs", "detail":"Game.log.41",
          "percent":37, "seconds":12, "count":"59 of 159" }
        """;

    private const string GameFiles = """
        { "key":"gamedata", "label":"Reading your game files", "detail":null,
          "percent":null, "seconds":18, "count":null }
        """;

    private static Page Showing(string jobs)
    {
        var page = new Page();
        page.Do($"showActivity([{jobs}]);");
        return page;
    }

    [Fact]
    public void A_job_that_knows_its_progress_shows_the_bar_and_the_count()
    {
        var page = Showing(Scan);

        Assert.False(page.Truth("__dom.node('#scan').hidden"));
        Assert.Equal("Reading your logs", page.NodeText("#scan-label"));
        Assert.Equal("37%", page.Text("__dom.node('#scan-fill').style.width"));
        Assert.Contains("59 of 159", page.NodeText("#scan-count"));
        Assert.Equal("Game.log.41", page.NodeText("#scan-file"));
    }

    /// <summary>
    /// Two of these four jobs genuinely cannot say how far along they are. An
    /// indeterminate bar is honest about the difference; a guessed one that
    /// sticks at 90% teaches people to ignore bars.
    /// </summary>
    [Fact]
    public void A_job_that_cannot_know_its_progress_says_so_rather_than_guessing()
    {
        var page = Showing(GameFiles);

        Assert.True(page.Truth("__dom.node('#scan-fill').classList.contains('working')"));
        Assert.Equal("Reading your game files", page.NodeText("#scan-label"));
        Assert.Contains("18s", page.NodeText("#scan-count"));
    }

    [Fact]
    public void A_job_that_does_know_is_not_left_sweeping()
    {
        var page = Showing(GameFiles);
        Assert.True(page.Truth("__dom.node('#scan-fill').classList.contains('working')"));

        page.Do($"showActivity([{Scan}]);");

        Assert.False(page.Truth("__dom.node('#scan-fill').classList.contains('working')"));
    }

    /// <summary>
    /// One line at a time — a stack of bars appearing and collapsing is harder
    /// to read than a sentence — but nothing is hidden, so the rest are counted.
    /// </summary>
    [Fact]
    public void Several_jobs_at_once_show_the_oldest_and_count_the_rest()
    {
        var page = Showing($"{GameFiles}, {Scan}");

        Assert.Contains("Reading your game files", page.NodeText("#scan-label"));
        Assert.Contains("+1 more", page.NodeText("#scan-label"));
    }

    /// <summary>
    /// A full bar and a word, so work that ends in a blink still reads as
    /// having ended rather than as having been imagined.
    /// </summary>
    [Fact]
    public void Ending_fills_the_bar_and_stops_it_sweeping()
    {
        var page = Showing(GameFiles);

        page.Do("showActivityDone();");

        Assert.Equal("Done", page.NodeText("#scan-label"));
        Assert.Equal("100%", page.Text("__dom.node('#scan-fill').style.width"));
        Assert.False(page.Truth("__dom.node('#scan-fill').classList.contains('working')"));
    }

    [Fact]
    public void When_the_work_ends_the_strip_goes_away()
    {
        var page = Showing(Scan);
        Assert.False(page.Truth("__dom.node('#scan').hidden"));

        page.Do("hideActivity();");

        Assert.True(page.Truth("__dom.node('#scan').hidden"));
    }

    /// <summary>
    /// The whole point of the work that just ended was to change what the views
    /// would say, so a re-read that leaves the old numbers on screen is a
    /// re-read nobody can tell happened.
    /// </summary>
    [Fact]
    public void Finishing_reloads_what_the_work_was_for()
    {
        var page = new Page();
        page.Serve("/api/stats", "{}");
        page.Serve("/api/sessions", "[]");

        page.Do("await reloadAfterActivity();");

        Assert.Contains(page.Fetched(), url => url.Contains("/api/stats"));
    }
}
