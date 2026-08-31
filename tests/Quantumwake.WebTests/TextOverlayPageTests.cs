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
          "changes": [
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

    /// <summary>
    /// The list is thousands of rows on a real install, so it is searchable.
    /// </summary>
    /// <remarks>
    /// Across all three columns, because all three are things somebody looks
    /// for: the name they are holding, the kind of thing it is, and the mark
    /// itself — which is how "what does the star actually go on?" gets answered
    /// without reading four thousand rows.
    /// </remarks>
    [Fact]
    public void The_list_can_be_searched_by_name()
    {
        var page = Loaded();

        page.Do("__dom.node('#labels-search').value = 'flashlight'; renderLabelChanges();");

        var body = page.NodeText("#textoverlay-table tbody");
        Assert.Contains("FieldLite Flashlight Blue", body);
        Assert.DoesNotContain("F55 LMG", body);
    }

    [Fact]
    public void The_list_can_be_searched_by_kind_and_by_the_mark_itself()
    {
        var page = Loaded();

        page.Do("__dom.node('#labels-search').value = 'attachments'; renderLabelChanges();");
        Assert.Contains("FieldLite", page.NodeText("#textoverlay-table tbody"));

        page.Do("__dom.node('#labels-search').value = 'weapons'; renderLabelChanges();");
        Assert.Contains("F55 LMG", page.NodeText("#textoverlay-table tbody"));
    }

    /// <summary>
    /// The total is what the page is answerable for, so it is always beside the
    /// count of matches: a bare "2 rows" reads as the whole plan.
    /// </summary>
    [Fact]
    public void The_count_says_how_many_matched_and_how_many_there_are()
    {
        var page = Loaded();

        Assert.Contains("2 renames", page.NodeText("#labels-count"));

        page.Do("__dom.node('#labels-search').value = 'flashlight'; renderLabelChanges();");
        Assert.Contains("1 of 2 renames match", page.NodeText("#labels-count"));
    }

    [Fact]
    public void A_search_that_matches_nothing_says_so_rather_than_emptying_the_page()
    {
        var page = Loaded();

        page.Do("__dom.node('#labels-search').value = 'nothing like this'; renderLabelChanges();");

        Assert.Contains("Nothing matches", page.NodeText("#labels-count"));
        Assert.Contains("2 renames", page.NodeText("#labels-count"));
    }

    /// <summary>
    /// A real install rewrites some 4,000 names. Rendering them all makes
    /// typing crawl, so the render caps and the line says what it capped.
    /// </summary>
    [Fact]
    public void A_long_list_is_capped_and_says_it_capped()
    {
        var page = new Page();
        page.Do("""
            labelChanges = [];
            for (let i = 0; i < 900; i++)
              labelChanges.push({ itemClass: 'c' + i, was: 'Thing ' + i, becomes: 'Thing ' + i + ' [*]', category: 'Weapons' });
            renderLabelChanges();
            """);

        Assert.Equal(400, page.Count("__dom.node('#textoverlay-table tbody').querySelectorAll('tr').length"));
        Assert.Contains("Showing 400 of 900", page.NodeText("#labels-count"));
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
