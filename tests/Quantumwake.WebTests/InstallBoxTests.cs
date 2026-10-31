namespace Quantumwake.WebTests;

/// <summary>
/// What the "no install" box says it searched.
/// </summary>
/// <remarks>
/// The markup claims every drive was searched. On Linux there are no drives:
/// the game sits in a Wine prefix, and a pilot told their drives were searched
/// has no idea what to type next. So the sentence and the example folder
/// follow what the server says it looked in.
/// </remarks>
public class InstallBoxTests
{
    [Fact]
    public void On_linux_the_box_names_the_prefixes_it_searched()
    {
        var page = new Page();
        page.Serve("/api/install/path",
            """{"saved":null,"detected":[],"fromLauncher":[],"windows":false,"prefixes":["/home/pilot/Games/star-citizen","/home/pilot/.wine"]}""");

        page.Do("await describeInstallSearch();");

        var text = page.NodeText("#install-search");
        Assert.Contains("/home/pilot/Games/star-citizen, /home/pilot/.wine", text);
        Assert.DoesNotContain("Every drive", text);
        Assert.Contains("StarCitizen/LIVE under drive_c", text);

        Assert.Equal(
            "/home/pilot/Games/star-citizen/drive_c/Program Files/Roberts Space Industries/StarCitizen/LIVE",
            page.Text("__dom.node('#install-path').placeholder"));
    }

    [Fact]
    public void On_linux_with_no_prefix_the_box_says_where_it_looked()
    {
        var page = new Page();
        page.Serve("/api/install/path", """{"saved":null,"detected":[],"fromLauncher":[],"windows":false,"prefixes":[]}""");

        page.Do("await describeInstallSearch();");

        var text = page.NodeText("#install-search");
        Assert.Contains("No Wine prefix was found", text);
        Assert.Contains("~/Games", text);
        Assert.StartsWith("~/Games/star-citizen/drive_c/", page.Text("__dom.node('#install-path').placeholder"));
    }

    [Fact]
    public void On_windows_the_markup_stands()
    {
        var page = new Page();
        page.Serve("/api/install/path", """{"saved":null,"detected":[],"fromLauncher":[],"windows":true,"prefixes":[]}""");
        page.Do("__dom.node('#install-search').textContent = 'as written';");

        page.Do("await describeInstallSearch();");

        Assert.Equal("as written", page.NodeText("#install-search"));
    }

    /// <summary>
    /// The French reader gets the same sentence, with the prefixes in it.
    /// </summary>
    [Fact]
    public void The_linux_sentence_is_translated()
    {
        var page = new Page("fr");
        page.Serve("/api/install/path", """{"saved":null,"detected":[],"fromLauncher":[],"windows":false,"prefixes":["/home/pilote/Games/star-citizen"]}""");

        page.Do("await describeInstallSearch();");

        var text = page.NodeText("#install-search");
        Assert.Contains("préfixes Wine /home/pilote/Games/star-citizen", text);
    }
}
