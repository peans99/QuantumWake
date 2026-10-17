using Jint;

namespace Quantumwake.WebTests;

/// <summary>
/// The Cougar MFD pages in French.
/// </summary>
/// <remarks>
/// The panel loads the dashboard's i18n script and its catalogue; the core
/// falls back to English when they are absent, which is how every other MFD
/// test runs it. The bezel captions stay the cockpit's codes - six characters
/// under a physical button - so only the readings and the page names change.
/// </remarks>
public class MfdFrenchTests
{
    private static Engine French()
    {
        var web = Path.Combine(AppContext.BaseDirectory, "web");
        var engine = new Engine();
        engine.Execute("var window = globalThis;");
        engine.Execute($"window.QW_LANG = 'fr'; window.QW_STRINGS = {File.ReadAllText(Path.Combine(web, "locales", "fr.json"))};");
        engine.Execute(File.ReadAllText(Path.Combine(web, "i18n.js")));
        engine.Execute(File.ReadAllText(Path.Combine(web, "mfd-core.js")));
        return engine;
    }

    [Fact]
    public void Nav_readings_read_in_french_and_keep_their_english_labels_for_the_glyphs()
    {
        var e = French();
        e.Execute("var r = QwMfd.rows(QwMfd.pageIds.indexOf('nav'), {}, null);");

        // The label stays English: the glyph beside it is chosen from the word.
        Assert.Equal("LOCATION", e.Evaluate("r[1][0]").AsString());
        Assert.Equal("Lieu pas encore identifié", e.Evaluate("r[1][1]").AsString());
        Assert.Equal("Chargement de votre plan suivi…", e.Evaluate("r[0][1]").AsString());
    }

    [Fact]
    public void Counts_on_the_status_page_agree_in_french()
    {
        var e = French();
        e.Execute("var r = QwMfd.rows(QwMfd.pageIds.indexOf('status'), { deaths: 1, incapacitations: 3 }, null);");

        Assert.Equal("1 mort · 3 incapacités", e.Evaluate("r.find(x => x[0] === 'THIS SESSION')[1]").AsString().Replace(' ', ' '));
    }

    [Fact]
    public void Without_a_catalogue_the_core_reads_english_as_before()
    {
        var engine = new Engine();
        engine.Execute("var window = globalThis;");
        engine.Execute(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd-core.js")));
        engine.Execute("var r = QwMfd.rows(QwMfd.pageIds.indexOf('status'), { deaths: 1, incapacitations: 3 }, null);");

        Assert.Equal("1 death · 3 incapacitations", engine.Evaluate("r.find(x => x[0] === 'THIS SESSION')[1]").AsString());
    }
}
