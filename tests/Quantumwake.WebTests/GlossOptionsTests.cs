namespace Quantumwake.WebTests;

/// <summary>
/// The Gloss page's marking choices.
/// </summary>
/// <remarks>
/// Changing a mark deliberately does not rewrite the game's file. Writing into
/// somebody's game folder on a checkbox is not a thing to do quietly, so the
/// page saves the choice and says to install again.
/// </remarks>
public class GlossOptionsTests
{
    private const string Status = """
        {"installed":false,"installedAt":null,"layered":false,"baseSource":"the game",
         "marked":12,"sold":3,"skipped":40,"annotated":7,"samples":[],"problem":null,
         "options":{"colour":true,"level":4,"facts":false}}
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/gloss", Status);
        page.Do("await loadTextOverlay();");
        return page;
    }

    [Fact]
    public void The_stored_choices_are_what_the_page_shows()
    {
        var page = Loaded();

        Assert.True(page.Truth("__dom.node('#gloss-colour').checked"));
        Assert.False(page.Truth("__dom.node('#gloss-facts').checked"));
        Assert.Equal("4", page.Text("__dom.node('#gloss-level').value"));
    }

    /// <summary>
    /// The level only means anything while colour is on. Disabled rather than
    /// hidden: a control that vanishes reads as a bug, one that greys out reads
    /// as a consequence.
    /// </summary>
    [Fact]
    public void The_level_is_disabled_when_colour_is_off()
    {
        var page = new Page();
        page.Serve("/api/gloss", Status.Replace("\"colour\":true", "\"colour\":false"));
        page.Do("await loadTextOverlay();");

        Assert.True(page.Truth("__dom.node('#gloss-level').disabled"));
    }

    [Fact]
    public void Saving_a_choice_posts_it_and_says_it_is_not_installed_yet()
    {
        var page = Loaded();
        page.Serve("/api/gloss/options", """{"colour":false,"level":4,"facts":false}""");

        page.Do("__dom.node('#gloss-colour').checked = false; await saveGlossOptions();");

        Assert.Contains("\"colour\":false", page.BodyOf("/api/gloss/options"));
        Assert.Contains("Install again", page.NodeText("#gloss-options-note"));
    }

    /// <summary>
    /// How much the size and class marks touch is worth a tile of its own: it is
    /// the part of the file the sold mark never explains.
    /// </summary>
    [Fact]
    public void The_summary_counts_what_the_facts_marked()
    {
        Assert.Contains("Size or class marked", Loaded().NodeText("#textoverlay-summary"));
    }
}
