namespace Quantumwake.WebTests;

/// <summary>
/// The Settings block for the community dataset, once a game patch has moved
/// past it.
/// </summary>
/// <remarks>
/// <para>
/// The dataset is a copy of files that describe one build of the game, and a
/// patch is exactly the event that makes it wrong: 4.10 adds ships, items and
/// commodities the 4.9 dump has never heard of, and every one of them then
/// renders as a bare id. Nothing said so - the block showed a count and the day
/// it was fetched, neither of which answers "is this current?" - and there was
/// no control to act on the answer either, because Download hides itself once
/// the data is downloaded.
/// </para>
/// <para>
/// So these pin three things: the dump is named, the block says when your logs
/// have moved past it, and the button that fixes it is present exactly when it
/// would do something.
/// </para>
/// </remarks>
public class CommunityDatasetTests
{
    private const string Behind = """
        {"enabled":true,"commodities":203,"fetchedAt":"2026-08-22T21:44:00Z",
         "dump":"4.9.0-LIVE.12344265","playing":"12519617",
         "behind":true,"source":"x"}
        """;

    private const string Current = """
        {"enabled":true,"commodities":203,"fetchedAt":"2026-08-27T15:04:00Z",
         "dump":"4.10.0-LIVE.12519617","playing":"12519617",
         "behind":false,"source":"x"}
        """;

    private const string Off = """
        {"enabled":false,"commodities":0,"fetchedAt":null,"dump":null,
         "playing":null,"behind":false,"source":"x"}
        """;

    /// <summary>
    /// Settings reads several endpoints; the others answer emptily so the
    /// community block is what is under test.
    /// </summary>
    private static Page Rendered(string community)
    {
        var page = new Page();
        page.Serve("/api/community", community);
        page.Serve("/api/overlay", """{"available":false,"visible":false}""");
        page.Serve("/api/uex", """{"enabled":false,"prices":0,"fetchedAt":null,"hasCredentials":false}""");
        page.Do("await renderSettings();");
        return page;
    }

    /// <summary>
    /// The build it was made from, beside the day it was fetched. The fetch date
    /// alone is the misleading half: it says when this machine asked, and a dump
    /// for a patch lands days after the patch itself.
    /// </summary>
    [Fact]
    public void The_status_names_the_dump_it_was_made_from()
    {
        var line = Rendered(Current).NodeText("#settings-community-status");

        Assert.Contains("203 commodities", line);
        Assert.Contains("4.10.0-LIVE.12519617", line);
        Assert.Contains("fetched", line);
    }

    /// <summary>
    /// A cache written before the dump was recorded has no answer to give, and
    /// says so rather than leaving the reader to assume it is current.
    /// </summary>
    [Fact]
    public void A_cache_that_does_not_know_its_dump_says_so()
    {
        const string unknown = """
            {"enabled":true,"commodities":203,"fetchedAt":"2026-08-22T21:44:00Z",
             "dump":null,"playing":"12519617",
             "behind":false,"source":"x"}
            """;

        Assert.Contains("dump unknown", Rendered(unknown).NodeText("#settings-community-status"));
    }

    /// <summary>
    /// Both numbers named, and what it costs said plainly: the point is not that
    /// a version differs but that things in the game have no name here.
    /// </summary>
    [Fact]
    public void Logs_past_the_dump_are_reported_with_both_builds()
    {
        var page = Rendered(Behind);

        Assert.False(page.Truth("__dom.node('#settings-community-behind').hidden"));

        var line = page.NodeText("#settings-community-behind");
        Assert.Contains("12519617", line);
        Assert.Contains("4.9.0-LIVE.12344265", line);
        Assert.Contains("no name here", line);
    }

    /// <summary>
    /// Same build on both sides is the ordinary case, and an ordinary case
    /// deserves silence rather than a line saying nothing is wrong.
    /// </summary>
    [Fact]
    public void A_dump_that_matches_the_logs_says_nothing()
    {
        var page = Rendered(Current);

        Assert.True(page.Truth("__dom.node('#settings-community-behind').hidden"));
    }

    /// <summary>
    /// Refresh appears only once there is something to refresh, and Download
    /// only while there is not - so the block always offers exactly one way
    /// forward.
    /// </summary>
    [Fact]
    public void Refresh_is_offered_when_the_data_is_there_and_download_when_it_is_not()
    {
        var enabled = Rendered(Current);

        Assert.False(enabled.Truth("__dom.node('#settings-community-refresh').hidden"));
        Assert.True(enabled.Truth("__dom.node('#settings-community-enable').hidden"));

        var off = Rendered(Off);

        Assert.True(off.Truth("__dom.node('#settings-community-refresh').hidden"));
        Assert.False(off.Truth("__dom.node('#settings-community-enable').hidden"));
    }

    /// <summary>
    /// Refresh posts the same enable call, which re-downloads and overwrites
    /// every digest. Nothing is deleted first: a failed refresh has to leave the
    /// working copy in place.
    /// </summary>
    [Fact]
    public void Refresh_asks_for_the_files_again_without_deleting_first()
    {
        var page = Rendered(Current);
        page.Serve("/api/community/enable", """{"enabled":true,"commodities":211}""");
        page.Do("__dom.node('#settings-community-refresh').click();");

        Assert.Contains(page.Fetched(), url => url == "POST /api/community/enable");
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("/api/community/disable"));
    }

    /// <summary>
    /// Eleven files are fetched, and the failure of any one used to read as the
    /// same sentence. The cause is in the problem's detail, so it is shown.
    /// </summary>
    [Fact]
    public void A_failed_download_reports_what_actually_failed()
    {
        var page = Rendered(Current);
        page.Fail("/api/community/enable", 502,
            """{"title":"The community dataset could not be fetched.","detail":"ships.json: 404"}""");

        page.Do("__dom.node('#settings-community-refresh').click();");

        var status = page.NodeText("#settings-community-status");
        Assert.Contains("ships.json: 404", status);
    }
}
