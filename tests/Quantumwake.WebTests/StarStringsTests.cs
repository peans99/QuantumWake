namespace Quantumwake.WebTests;

/// <summary>
/// The StarStrings card in Settings, and above all what it says about a build
/// that has fallen behind the game.
/// </summary>
/// <remarks>
/// A stale text mod is not a cosmetic problem: each release is cut against one
/// game build, and text a later patch adds is not in a file written before it,
/// so it reads blank across the game. "A newer build is out" did not say that,
/// and the person reading it has no way to connect their missing menus to a
/// card in someone else's dashboard. These tests hold the line that the page
/// names the consequence and both ways out of it.
/// </remarks>
public class StarStringsTests
{
    private const string Installed = """
        {"repository":"https://github.com/MrKraken/StarStrings",
         "gameRoot":"C:\Games\StarCitizen\LIVE",
         "installed":true,"displaced":false,
         "release":"SC LIVE Build (release-2026-07-01-a11c0de)",
         "installedAt":"2026-07-02T10:00:00Z","files":[]}
        """;

    private const string Stale = """
        {"repository":"https://github.com/MrKraken/StarStrings",
         "gameRoot":"C:\Games\StarCitizen\LIVE",
         "installed":true,"displaced":false,
         "release":"SC LIVE Build (release-2026-07-01-a11c0de)",
         "installedAt":"2026-07-02T10:00:00Z","files":[],
         "latest":{"name":"SC LIVE Build (release-2026-08-26-b83d58b)",
                   "publishedAt":"2026-08-26T19:03:37Z","url":"x"},
         "newer":true}
        """;

    private const string Current = """
        {"repository":"https://github.com/MrKraken/StarStrings",
         "gameRoot":"C:\Games\StarCitizen\LIVE",
         "installed":true,"displaced":false,
         "release":"SC LIVE Build (release-2026-08-26-b83d58b)",
         "installedAt":"2026-08-27T10:00:00Z","files":[],
         "latest":{"name":"SC LIVE Build (release-2026-08-26-b83d58b)",
                   "publishedAt":"2026-08-26T19:03:37Z","url":"x"},
         "newer":false}
        """;

    private const string NotInstalled = """
        {"repository":"https://github.com/MrKraken/StarStrings",
         "gameRoot":"C:\Games\StarCitizen\LIVE",
         "installed":false,"displaced":false,"files":[]}
        """;

    private static Page Loaded(string state, string? checkedState = null)
    {
        var page = new Page();
        page.Serve("/api/starstrings", state);
        page.Serve("/api/starstrings?check=true", checkedState ?? state);

        // The card writes its alert line into the status element's parent, and
        // the stub does not parse the markup, so the status starts orphaned.
        page.Do("document.createElement('div').append(__dom.node('#starstrings-status'));");

        page.Do("await loadStarStrings(true);");
        return page;
    }

    private static string Status(Page page) => page.NodeText("#starstrings-status");

    [Fact]
    public void A_build_left_behind_says_what_it_costs()
    {
        var status = Status(Loaded(Stale));

        Assert.Contains("A newer build is out", status);
        Assert.Contains("goes missing in-game", status);
    }

    /// <summary>
    /// Updating is one way out and removing is the other, and somebody whose
    /// text has already gone needs the second one named.
    /// </summary>
    [Fact]
    public void It_names_both_ways_out()
    {
        var status = Status(Loaded(Stale));

        Assert.Contains("install it", status);
        Assert.Contains("Remove", status);
    }

    [Fact]
    public void A_current_build_is_not_warned_about()
    {
        var status = Status(Loaded(Current));

        Assert.Contains("you have it", status);
        Assert.DoesNotContain("missing", status);
    }

    [Fact]
    public void Nothing_installed_is_said_plainly()
    {
        var status = Status(Loaded(NotInstalled));

        Assert.Contains("Not installed.", status);
        Assert.DoesNotContain("missing", status);
    }

    /// <summary>
    /// The moment the mod goes in is the only one where the person is certain
    /// to be reading, so it is where the patch habit gets asked for.
    /// </summary>
    [Fact]
    public void Installing_asks_for_a_check_after_every_patch()
    {
        var page = new Page();
        page.Serve("/api/starstrings", NotInstalled);
        page.Serve("/api/starstrings?check=true", Installed);
        page.Serve("/api/starstrings/install", """{"release":"SC LIVE Build","files":2}""");

        page.Do("document.createElement('div').append(__dom.node('#starstrings-status'));");
        page.Do("initStarStrings();");
        page.Do("__dom.node('#starstrings-install').fire('click');");

        Assert.Contains("POST /api/starstrings/install", page.Fetched());

        var alert = page.Text(
            "__dom.node('#starstrings-status').parentElement.querySelector('.trip-alert').textContent");

        Assert.Contains("Restart Star Citizen", alert);
        Assert.Contains("check for a new build after every game patch", alert);
    }
}
