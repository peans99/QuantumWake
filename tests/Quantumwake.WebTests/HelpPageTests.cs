namespace Quantumwake.WebTests;

/// <summary>
/// Help is an About drill-down rather than one more top-bar tab. The about tab
/// stays active, while the fragment can still open Help directly.
/// </summary>
public class HelpPageTests
{
    [Fact]
    public void Help_is_its_own_view_and_keeps_About_active()
    {
        var page = new Page();
        page.Do("window.scrollTo = () => {}; showView('help');");

        Assert.True(page.Truth("__dom.node('#view-help').classList.contains('active')"));
        Assert.False(page.Truth("__dom.node('#view-about').classList.contains('active')"));
        Assert.True(page.Truth("__dom.node('#test-tab-about').classList.contains('active')"));

        page.Do("showView('about');");
        Assert.True(page.Truth("__dom.node('#view-about').classList.contains('active')"));
        Assert.False(page.Truth("__dom.node('#view-help').classList.contains('active')"));
    }

    [Fact]
    public void Help_markup_covers_the_current_cockpit_and_screen_features()
    {
        var markup = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html"));

        Assert.Contains("id=\"about-open-help\"", markup);
        Assert.Contains("id=\"view-help\"", markup);
        Assert.Contains("How do the Cougar MFD displays work?", markup);
        Assert.Contains("Does screenshot reading capture my screen?", markup);
        Assert.Contains("Why does cash on hand say “about”?", markup);
        Assert.Contains("Settings → Report a problem", markup);
    }
}
