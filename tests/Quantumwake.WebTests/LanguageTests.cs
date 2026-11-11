namespace Quantumwake.WebTests;

/// <summary>
/// The page's language: the lookup, the plurals, and the picker.
/// </summary>
/// <remarks>
/// English is the key, so every other test in this project is also a test that
/// an English page is unchanged. French was retired in 1.2.29 and English is
/// the only language on offer, so the picker stays hidden; the picker tests
/// below serve it a second language to prove it still works for the day one
/// comes back.
/// </remarks>
public class LanguageTests
{
    private const string Languages = """
        {"current":"en","available":[
          {"code":"en","name":"English","covers":null},
          {"code":"fr","name":"Français","covers":"traduit pour l’instant : la navigation"}]}
        """;

    [Fact]
    public void An_english_page_is_its_own_text()
    {
        var page = new Page();

        Assert.Equal("Open map", page.Text("tx('Open map')"));
        Assert.Equal("2 of 5 items in hand", page.Text("tx('{have} of {total} items in hand', { have: 2, total: 5 })"));
    }

    [Theory]
    [InlineData(0, "0 logs")]
    [InlineData(1, "1 log")]
    [InlineData(2, "2 logs")]
    public void English_counts_take_english_plurals(int count, string expected)
    {
        var page = new Page();

        Assert.Equal(expected, page.Text($"txn({count}, '{{count}} log', '{{count}} logs')"));
    }

    /// <summary>English alone: no picker to show.</summary>
    [Fact]
    public void With_english_alone_the_card_is_hidden()
    {
        var page = new Page();
        page.Serve("/api/language", """{"current":"en","available":[{"code":"en","name":"English","covers":null}]}""");

        page.Do("await renderLanguageSettings();");

        Assert.True(page.Truth("__dom.node('#language-card').hidden"));
    }

    /// <summary>
    /// Each language named in itself, and a partial one saying how far it has
    /// got - shown on an English page too, because that is the page a French
    /// reader is looking at when deciding.
    /// </summary>
    [Fact]
    public void The_picker_names_each_language_and_says_what_is_partial()
    {
        var page = new Page();
        page.Serve("/api/language", Languages);

        page.Do("await renderLanguageSettings();");

        Assert.Equal("English|Français",
            page.Text("__dom.node('#language-pick').children.map(o => o.textContent).join('|')"));
        Assert.Equal("en", page.Text("__dom.node('#language-pick').value"));
        Assert.Equal("Français — traduit pour l’instant : la navigation.", page.NodeText("#language-covers"));
        Assert.False(page.Truth("__dom.node('#language-covers').hidden"));
        Assert.False(page.Truth("__dom.node('#language-card').hidden"));
    }

    /// <summary>
    /// The choice is the server's, so the overlay follows it, and the page
    /// reloads to take it rather than re-rendering in place.
    /// </summary>
    [Fact]
    public void Choosing_a_language_saves_it_and_reloads()
    {
        var page = new Page();
        page.Serve("/api/language", Languages);
        page.Serve("/api/language?code=fr", """{"current":"fr"}""");

        page.Do("await renderLanguageSettings();");
        page.Do("__dom.node('#language-pick').value = 'fr'; await __dom.node('#language-pick').onchange();");

        Assert.Contains("POST /api/language?code=fr", page.Fetched());
        Assert.Equal(1, page.Count("__reloads"));
    }
}
