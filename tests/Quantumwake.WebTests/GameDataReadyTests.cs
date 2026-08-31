namespace Quantumwake.WebTests;

/// <summary>
/// Telling "not read yet" apart from "could not be read".
/// </summary>
/// <remarks>
/// The first read of the game files takes about half a minute, and for that
/// half minute every page backed by them is empty. Several of those pages
/// answered the ambiguity by suggesting a 110 MB download, which fixes a wait
/// by downloading something.
/// </remarks>
public class GameDataReadyTests
{
    private static Page Loaded(string state)
    {
        var page = new Page();
        page.Serve("/api/gamedata", state);
        page.Do("await loadGameData();");
        return page;
    }

    [Fact]
    public void A_finished_read_shows_what_it_found()
    {
        var page = Loaded("""
            {"state":"ready","problem":null,"seconds":28.4,
             "counts":{"commodities":342,"items":26028,"recipes":1606,
                       "deposits":1321,"places":1344}}
            """);

        Assert.Equal("ready", page.NodeText("#gamedata-state"));
        Assert.Contains("26,028", page.NodeText("#gamedata-counts"));
        Assert.Contains("1,606", page.NodeText("#gamedata-counts"));
    }

    /// <summary>
    /// A page with nothing to show must say it is waiting, not that something
    /// is wrong and certainly not that a download would help.
    /// </summary>
    [Fact]
    public void A_read_in_progress_says_so_rather_than_blaming_anything()
    {
        var page = Loaded("""
            {"state":"reading","problem":null,"seconds":6,"counts":{}}
            """);

        Assert.Contains("reading", page.NodeText("#gamedata-state"));
        Assert.Contains("Still reading", page.Text("gameDataExcuse()"));
    }

    [Fact]
    public void A_failure_says_what_went_wrong()
    {
        var page = Loaded("""
            {"state":"failed","problem":"The game archive was read but produced nothing.",
             "seconds":3,"counts":{}}
            """);

        Assert.Contains("could not be read", page.NodeText("#gamedata-state"));
        Assert.Contains("produced nothing", page.NodeText("#gamedata-problem"));
    }

    /// <summary>
    /// No install never resolves itself, so it must not read as a wait.
    /// </summary>
    [Fact]
    public void No_install_is_not_a_wait()
    {
        var page = Loaded("""
            {"state":"noinstall","problem":"No Star Citizen install was found.",
             "seconds":null,"counts":{}}
            """);

        Assert.Equal("No game install was found.", page.Text("gameDataExcuse()"));
    }
}
