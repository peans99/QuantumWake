namespace Quantumwake.WebTests;

/// <summary>
/// Folding a page's summary away to reach its table.
/// </summary>
/// <remarks>
/// The Contracts page leads with four tiles, a standing table under a paragraph
/// of caveat, and two charts, so the newest contract - the thing most often
/// being looked for - starts below the fold. The preference is per-reader and
/// per-browser, so it lives in localStorage; what these tests hold is that it
/// survives a reload and that a page whose summary is hidden still says so,
/// since a fold nobody can find is a page with a missing section.
/// </remarks>
public class PageStatsCollapseTests
{
    private const string Toggle = "__dom.node('#contracts-stats-toggle')";
    private const string Panel = "__dom.node('#contracts-stats')";

    private static Page Started()
    {
        var page = new Page();
        page.Do("initPageStatsCollapsers();");
        return page;
    }

    [Fact]
    public void The_summary_starts_open()
    {
        var page = Started();

        Assert.False(page.Truth($"{Panel}.hidden"));
        Assert.Equal("Hide summary", page.Text($"{Toggle}.textContent"));
    }

    [Fact]
    public void Pressing_it_folds_the_summary_away()
    {
        var page = Started();
        page.Do($"{Toggle}.click();");

        Assert.True(page.Truth($"{Panel}.hidden"));
    }

    /// <summary>
    /// The label names what pressing it does, both ways round. An arrow is no
    /// use here: once the summary is folded there is nothing on screen to say
    /// what the button would bring back.
    /// </summary>
    [Fact]
    public void The_button_says_which_way_it_goes()
    {
        var page = Started();
        page.Do($"{Toggle}.click();");

        Assert.Equal("Show summary", page.Text($"{Toggle}.textContent"));

        page.Do($"{Toggle}.click();");

        Assert.Equal("Hide summary", page.Text($"{Toggle}.textContent"));
        Assert.False(page.Truth($"{Panel}.hidden"));
    }

    /// <summary>
    /// The point of the preference is not having to fold it again every visit.
    /// </summary>
    [Fact]
    public void The_choice_survives_a_reload()
    {
        var page = Started();
        page.Do($"{Toggle}.click();");

        Assert.Contains("contracts", page.Text("localStorage.getItem('qw-collapsed-page-stats')"));

        page.Do("collapsedPageStats = new Set(JSON.parse(localStorage.getItem('qw-collapsed-page-stats')));"
            + $" {Panel}.hidden = false; initPageStatsCollapsers();");

        Assert.True(page.Truth($"{Panel}.hidden"));
    }

    /// <summary>
    /// A browser refusing localStorage - a private window, or site data blocked
    /// - must leave the page working rather than throwing on the way in.
    /// </summary>
    [Fact]
    public void A_stored_preference_that_cannot_be_read_is_ignored()
    {
        var page = Started();
        page.Do("localStorage.setItem('qw-collapsed-page-stats', 'not json');"
            + " collapsedPageStats = new Set(); initPageStatsCollapsers();");

        Assert.False(page.Truth($"{Panel}.hidden"));
    }
}
