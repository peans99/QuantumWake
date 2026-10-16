namespace Quantumwake.WebTests;

/// <summary>
/// The Settings pages beyond General in French: Controls, Overlay, Item
/// labels, shared files and About.
/// </summary>
/// <remarks>
/// Action names, action groups and their categories are the game's own
/// keybinding text and stay as it writes them; so do stick product names.
/// The page's words around them translate - the control names, the activation
/// modes, the counts.
/// </remarks>
public class SettingsPagesFrenchTests
{
    [Fact]
    public void The_button_table_reads_in_french_with_the_games_action_names_kept()
    {
        var page = new Page("fr");
        page.Serve("/api/controls", ControlsPageTests.Model);
        page.Serve("/api/controls/backups", "[]");
        page.Do("showControlsPane('devices'); await loadControls(); controlsDevice = 'js4'; await renderControlsDevice();");

        var table = page.NodeText("#controls-buttons tbody");
        Assert.Contains("Bouton 4", table);
        Assert.Contains("appui long", table);
        Assert.Contains("axe y", table);
        Assert.Contains("Eject", table);
        Assert.Contains("Flight - Movement · FLIGHT", table);

        page.Do("controlsDevice = 'js2'; await renderControlsDevice();");
        var throttle = page.Text("__dom.node('#controls-buttons tbody').children.map(tr => tr.children.slice(0, 3).map(td => td.textContent).join(' ')).join('|')");
        Assert.Contains("bouton 5 + Afterburner", throttle);
        // "hold" alone is a cargo hold in the catalogue; the mode keys apart.
        Assert.Contains("maintien", throttle);
        Assert.DoesNotContain("soute", throttle);
    }

    [Fact]
    public void A_shared_file_card_reads_in_french_with_its_names_kept()
    {
        var page = new Page("fr");
        page.Serve("/api/imports", ImportPageTests.OneBatch);
        page.Do("await loadImports();");

        var text = page.NodeText("#imports-list");
        Assert.Contains("importé le", text);
        Assert.Contains("par Quantum Wake 0.8.4", text);
        Assert.Contains("7 plans", text);
        Assert.Contains("Retirer les plans", text);
        Assert.Contains("nekron-share.json", text);
        Assert.Contains("Tuesday hauling", text);
    }

    [Fact]
    public void The_item_label_count_reads_in_french()
    {
        var page = new Page("fr");
        page.Do("""
            labelChanges = [
              { was: 'Arden-CL Backpack', category: 'Armour', becomes: 'Arden-CL Backpack [L]' },
              { was: 'Pyro RYT Multi-Tool', category: 'Tools', becomes: 'Pyro RYT Multi-Tool *' }];
            renderLabelChanges();
            """);

        Assert.Equal("2 renommages.", page.NodeText("#labels-count"));
        var table = page.NodeText("#textoverlay-table tbody");
        Assert.Contains("Armure", table);
        Assert.Contains("Arden-CL Backpack [L]", table);
    }

    [Fact]
    public void A_hat_reads_as_one_phrase_so_left_is_never_somebody_leaving()
    {
        var page = new Page("fr");
        Assert.Equal("Chapeau 1 gauche", page.Text("controlsControlLabel('hat1_left')"));
        Assert.Equal("chapeau 2 haut", page.Text("controlsInputWords('js1_hat2_up')"));
    }

    /// <summary>
    /// A game set to French reads its own text file, so labels written into
    /// the English one would say "installed" and never show. The page says so.
    /// </summary>
    [Theory]
    [InlineData(null, "english", "Nothing is written until you install.", "is set to")]
    [InlineData(null, "french_(france)", "Star Citizen is set to french_(france), so it reads that language's text file", null)]
    [InlineData("fr", "french_(france)", "Star Citizen est réglé sur french_(france), il lit donc le fichier de texte de cette langue", null)]
    public void The_labels_say_when_the_game_reads_another_language(string? language, string game, string expected, string? absent)
    {
        var page = new Page(language);
        page.Serve("/api/labels", $$"""
            {"installed":false,"layered":false,"baseSource":"game","marked":0,"sold":0,"skipped":0,"changes":[],
             "problem":null,"annotated":0,"options":null,"filled":0,"gameLanguage":"{{game}}"}
            """);
        page.Do("await loadTextOverlay();");

        var status = page.NodeText("#textoverlay-status").Replace(' ', ' ').Replace(' ', ' ');
        Assert.Contains(expected, status);
        if (absent is not null) Assert.DoesNotContain(absent, status);
    }
}
