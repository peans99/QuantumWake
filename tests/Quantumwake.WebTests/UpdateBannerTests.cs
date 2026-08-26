namespace Quantumwake.WebTests;

/// <summary>
/// The banner that announces a release, and what it offers.
/// </summary>
/// <remarks>
/// The button replaces the application the reader is running, so what it says
/// before being pressed matters: whether one click is even possible on this
/// build, and how much of somebody's connection it is about to spend.
/// </remarks>
public class UpdateBannerTests
{
    private static Page Announcing(string json)
    {
        var page = new Page();
        page.Serve("/api/updates/check", json);
        page.Serve("/api/updates", "{\"asked\":true,\"automatic\":false}");
        page.Do("await runUpdateCheck({ quiet: true });");
        return page;
    }

    private const string Installable = """
        {"newer":true,"current":"0.8.18","latest":"0.8.19",
         "url":"https://example/release","canInstall":true,"downloadBytes":91743987}
        """;

    [Fact]
    public void An_installable_release_offers_one_click_and_says_what_it_costs()
    {
        var actions = Announcing(Installable).NodeText("#update-actions");

        Assert.Contains("Update to 0.8.19", actions);
        Assert.Contains("87 MB", actions);
        Assert.Contains("Read the notes", actions);
    }

    /// <summary>
    /// A source build cannot replace itself, and a button that explains that
    /// only after being pressed is worse than one that was never there.
    /// </summary>
    [Fact]
    public void A_build_that_cannot_replace_itself_offers_the_release_page_instead()
    {
        var actions = Announcing("""
            {"newer":true,"current":"0.8.18","latest":"0.8.19",
             "url":"https://example/release","canInstall":false}
            """).NodeText("#update-actions");

        Assert.DoesNotContain("Update to", actions);
        Assert.Contains("Open the release page", actions);
    }

    /// <summary>A release with no size reported still offers the button.</summary>
    [Fact]
    public void An_unknown_download_size_is_left_off_rather_than_guessed()
    {
        var actions = Announcing("""
            {"newer":true,"current":"0.8.18","latest":"0.8.19",
             "url":"https://example/release","canInstall":true}
            """).NodeText("#update-actions");

        Assert.Contains("Update to 0.8.19", actions);
        Assert.DoesNotContain("MB", actions);
    }

    [Fact]
    public void Being_current_says_so_and_offers_nothing()
    {
        var page = Announcing("""
            {"newer":false,"current":"0.8.19","latest":"0.8.19","canInstall":true}
            """);

        Assert.True(page.Truth("__dom.node('#update').hidden"));
    }
}
