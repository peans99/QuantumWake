namespace Quantumwake.WebTests;

/// <summary>
/// Installing StarStrings while item labels are on.
/// </summary>
/// <remarks>
/// Both replace the same file, so the second one installed would otherwise
/// remove the first. The server lays the labels back over StarStrings' table,
/// and the page says so — a second thing changing on one click should not be
/// something you find out later.
/// </remarks>
public class StarStringsRelabelTests
{
    private static Page Installed(string answer)
    {
        var page = new Page();
        page.Serve("/api/starstrings", """
            {"installed":false,"release":null,"installedAt":null,"publishedAt":null,
             "latest":null,"newer":false,"files":0,"problem":null}
            """);
        page.Serve("/api/starstrings/install", answer);
        page.Serve("/api/labels", """
            {"installed":true,"installedAt":null,"layered":true,"baseSource":"StarStrings",
             "marked":12,"sold":3,"skipped":40,"annotated":7,"samples":[],"problem":null,
             "options":{"colour":false,"level":3,"facts":true}}
            """);

        page.Do("__dom.node('#starstrings-install').click();");
        return page;
    }

    private static string Alert(Page page) => page.NodeText("#starstrings-note");

    [Fact]
    public void It_says_when_the_labels_were_put_back_on_top()
    {
        var page = Installed("""
            {"release":"SC LIVE Build","installedAt":"2026-08-30T00:00:00Z","files":2,
             "relabelled":true}
            """);

        Assert.Contains("item labels were put back", Alert(page));
    }

    /// <summary>
    /// With no labels installed there is nothing to put back, and claiming
    /// otherwise would describe work that did not happen.
    /// </summary>
    [Fact]
    public void It_claims_nothing_when_there_were_no_labels()
    {
        var page = Installed("""
            {"release":"SC LIVE Build","installedAt":"2026-08-30T00:00:00Z","files":2,
             "relabelled":false}
            """);

        Assert.DoesNotContain("put back", Alert(page));
    }
}
