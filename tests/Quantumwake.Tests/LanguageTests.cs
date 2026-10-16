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

    [Fact]
    public void A_choice_survives_a_restart()
    {
        new LanguageStore(_directory).Save("fr");

        Assert.Equal("fr", new LanguageStore(_directory).Current);
    }

    /// <summary>
    /// The code becomes part of a file path when the catalogue is read, so
    /// anything not on the list is English rather than trusted.
    /// </summary>
    [Theory]
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
        Assert.Equal("fr", new LanguageStore(_directory).Save(" FR "));
    }

    /// <summary>
    /// A partial language says so. French was partial through 0.18.16 and is
    /// complete from 0.18.17 - what stays English is game text, on purpose -
    /// so neither carries a note now; a language added half-done must.
    /// </summary>
    [Fact]
    public void A_finished_language_carries_no_coverage_note()
    {
        Assert.Null(LanguageStore.Available.Single(l => l.Code == "en").Covers);
        Assert.Null(LanguageStore.Available.Single(l => l.Code == "fr").Covers);
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

    /// <summary>
    /// The whole catalogue inline, so the page's first paint is already French.
    /// Put back to English after, because the server is shared with every other
    /// test in the collection.
    /// </summary>
    [Fact]
    public async Task A_french_page_is_handed_the_french_catalogue()
    {
        try
        {
            var saved = await _server.Posted("/api/language?code=fr");
            Assert.Equal("fr", saved.GetProperty("current").GetString());

            var script = await Script();

            Assert.Contains("window.QW_LANG = \"fr\"", script);
            Assert.Contains("\"Ouvrir la carte\"", script);

            var listed = await _server.Get("/api/language");
            Assert.Equal("fr", listed.GetProperty("current").GetString());
            Assert.Contains(listed.GetProperty("available").EnumerateArray(),
                l => l.GetProperty("name").GetString() == "Français");
        }
        finally
        {
            await _server.Post("/api/language?code=en");
        }
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
