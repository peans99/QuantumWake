using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>Which language the dashboard is read in, and how the page is handed it.</summary>
public class LanguageStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"qw-language-{Guid.NewGuid():N}");

    /// <summary>
    /// English until somebody says otherwise: a French Windows that updates
    /// keeps the app it had rather than a half-translated one.
    /// </summary>
    [Fact]
    public void A_fresh_install_reads_in_english()
    {
        Assert.Equal("en", new LanguageStore(_directory).Current);
    }

    /// <summary>
    /// French was offered from 0.18 to 1.2.28 and retired in 1.2.29. An
    /// install that chose it has <c>fr</c> saved; it reads as English now,
    /// with nothing for the pilot to undo.
    /// </summary>
    [Fact]
    public void French_chosen_before_it_was_retired_reads_as_english()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "language.json"), """{"Code":"fr"}""");

        Assert.Equal("en", new LanguageStore(_directory).Current);
    }

    /// <summary>
    /// The code becomes part of a file path when the catalogue is read, so
    /// anything not on the list is English rather than trusted.
    /// </summary>
    [Theory]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("../../secrets")]
    [InlineData("")]
    [InlineData(null)]
    public void A_code_not_on_the_list_is_english(string? code)
    {
        Assert.Equal("en", new LanguageStore(_directory).Save(code));
    }

    [Fact]
    public void A_code_is_matched_however_it_is_cased()
    {
        Assert.Equal("en", new LanguageStore(_directory).Save(" EN "));
    }

    /// <summary>
    /// English is the only language on offer, so Settings has no picker to
    /// show. A language added later, half-done, must carry a coverage note.
    /// </summary>
    [Fact]
    public void English_is_the_one_language_and_carries_no_coverage_note()
    {
        var only = Assert.Single(LanguageStore.Available);
        Assert.Equal("en", only.Code);
        Assert.Null(only.Covers);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}

/// <summary>The language over HTTP: the setting, and the script that hands the page its catalogue.</summary>
[Collection("server")]
public class LanguageEndpointTests : IClassFixture<ServerUnderTest>
{
    private readonly ServerUnderTest _server;

    public LanguageEndpointTests(ServerUnderTest server) => _server = server;

    private Task<string> Script() => _server.Client.GetStringAsync("/api/i18n.js");

    [Fact]
    public async Task An_english_page_is_handed_no_catalogue()
    {
        await _server.Post("/api/language?code=en");

        var script = await Script();

        Assert.Contains("window.QW_LANG = \"en\"", script);
        Assert.DoesNotContain("QW_STRINGS", script);
    }

    /// <summary>Asking for French now is asking for English: there is no catalogue to hand over.</summary>
    [Fact]
    public async Task Asking_for_french_is_answered_in_english()
    {
        var saved = await _server.Posted("/api/language?code=fr");
        Assert.Equal("en", saved.GetProperty("current").GetString());

        var script = await Script();
        Assert.Contains("window.QW_LANG = \"en\"", script);
        Assert.DoesNotContain("QW_STRINGS", script);
    }

    /// <summary>
    /// The page is read in a browser that asks again whenever it likes; a
    /// cached script would keep the old language after a change.
    /// </summary>
    [Fact]
    public async Task The_script_is_never_cached()
    {
        var response = await _server.Client.GetAsync("/api/i18n.js");

        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
    }
}
