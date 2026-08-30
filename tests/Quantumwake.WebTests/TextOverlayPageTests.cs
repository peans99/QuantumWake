namespace Quantumwake.WebTests;

/// <summary>
/// The Text overlay page. It offers to write into someone's game folder, so
/// what it shows before that click is the whole point.
/// </summary>
public class TextOverlayPageTests
{
    private const string Plan = """
        {
          "installed": false, "installedAt": null, "layered": false,
          "baseSource": "the game",
          "marked": 41, "sold": 96, "skipped": 812,
          "samples": [
            {"itemClass":"gmni_lmg_ballistic_01","was":"F55 LMG","becomes":"F55 LMG *","category":"Weapons"},
            {"itemClass":"nvtc_ubarrel_flsh_s1_02","was":"FieldLite Flashlight Blue","becomes":"FieldLite Flashlight Blue *","category":"Attachments"}
          ],
          "problem": null
        }
        """;

    private static Page Loaded(string plan = Plan)
    {
        var page = new Page();
        page.Serve("/api/labels", plan);
        page.Do("await loadTextOverlay();");
        return page;
    }

    [Fact]
    public void It_shows_what_would_change_before_anything_is_written()
    {
        var page = Loaded();

        Assert.Contains("41", page.NodeText("#textoverlay-summary"));
        Assert.Contains("F55 LMG *", page.NodeText("#textoverlay-table tbody"));
        Assert.Contains("Nothing is written until you install", page.NodeText("#textoverlay-status"));
    }

    /// <summary>
    /// Which file it builds on decides whether another mod survives, so the page
    /// says so rather than leaving it to be discovered afterwards.
    /// </summary>
    [Fact]
    public void It_says_when_it_is_layered_over_another_mod()
    {
        var layered = Plan.Replace("\"baseSource\": \"the game\"", "\"baseSource\": \"StarStrings\"");

        Assert.Contains("StarStrings", Loaded(layered).NodeText("#textoverlay-source"));
        Assert.Contains("the game's own text", Loaded().NodeText("#textoverlay-source"));
    }

    /// <summary>Remove only appears when there is something to remove.</summary>
    [Fact]
    public void Remove_is_hidden_until_it_is_installed()
    {
        Assert.True(Loaded().Truth("__dom.node('#textoverlay-remove').hidden"));

        var installed = Plan
            .Replace("\"installed\": false", "\"installed\": true")
            .Replace("\"installedAt\": null", "\"installedAt\": \"2026-08-28T10:00:00Z\"");

        Assert.False(Loaded(installed).Truth("__dom.node('#textoverlay-remove').hidden"));
    }

    /// <summary>
    /// With no game folder there is nothing to build against, and the button must
    /// not invite a click that can only fail.
    /// </summary>
    [Fact]
    public void A_problem_disables_the_install_button_and_says_why()
    {
        var broken = Plan.Replace("\"problem\": null",
            "\"problem\": \"No game install was found, so there is nothing to build against.\"");

        var page = Loaded(broken);

        Assert.True(page.Truth("__dom.node('#textoverlay-install').disabled"));
        Assert.Contains("No game install was found", page.NodeText("#textoverlay-status"));
    }

    /// <summary>
    /// Installing is a POST and only on a click. Nothing about looking at the
    /// page may write to the game folder.
    /// </summary>
    [Fact]
    public void Looking_at_the_page_writes_nothing()
    {
        var page = new Page();
        page.Serve("/api/labels", Plan);
        page.Do("window.__posted = []; const f = window.fetch;"
            + " window.fetch = (u, o) => { if (o && o.method === 'POST') window.__posted.push(u);"
            + " return f(u, o); }; await loadTextOverlay();");

        Assert.Equal("", page.Text("window.__posted.join(',')"));
    }
}
