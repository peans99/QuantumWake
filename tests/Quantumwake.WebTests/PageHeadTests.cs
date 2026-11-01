using System.Text.RegularExpressions;

namespace Quantumwake.WebTests;

/// <summary>
/// One page header everywhere: where the page sits in the menu, over the
/// page's menu name. Before 0.20.24 there were nine kicker styles saying nine
/// different things - CONTROL ROOM, Operational roster, Flight ledger - and
/// titles that disagreed with the menu: the Loadout page was titled Armoury,
/// the same as the Armoury page.
/// </summary>
public class PageHeadTests
{
    private static readonly string Markup =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html"));

    /// <summary>The menu: each grouped view with its group and its label.</summary>
    private static IEnumerable<(string View, string Group, string Label)> Menu()
    {
        var nav = Markup[Markup.IndexOf("<nav class=\"tabs\"", StringComparison.Ordinal)..];
        nav = nav[..nav.IndexOf("</nav>", StringComparison.Ordinal)];

        foreach (Match group in Regex.Matches(nav,
                     "class=\"group-btn\"[^>]*>(?<group>[^<]+)<span.*?</div>\\s*</div>", RegexOptions.Singleline))
            foreach (Match view in Regex.Matches(group.Value, "data-view=\"(?<view>[a-z-]+)\">(?<label>[^<]+)<"))
                yield return (view.Groups["view"].Value, group.Groups["group"].Value.Trim(), view.Groups["label"].Value.Trim());
    }

    private static string Section(string view)
    {
        var start = Markup.IndexOf($"<section id=\"view-{view}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"no section for {view}");
        var end = Markup.IndexOf("<section id=\"view-", start + 10, StringComparison.Ordinal);
        return Markup[start..(end < 0 ? Markup.Length : end)];
    }

    [Fact]
    public void Every_grouped_page_is_labelled_with_its_menu_group()
    {
        var pages = Menu().Where(p => p.View != "about").ToList();
        Assert.True(pages.Count > 30);

        foreach (var (view, group, _) in pages)
        {
            var head = Section(view);
            head = head[..head.IndexOf("</h2>", StringComparison.Ordinal)];
            Assert.True(head.Contains($"<span class=\"page-kicker\">{group}</span>"), $"{view} is not labelled {group}");
        }
    }

    /// <summary>
    /// Mining's title is the workspace in use - Prospecting, Salvage - so it
    /// is written by the page; every other title is the menu's own word.
    /// </summary>
    [Fact]
    public void Every_page_title_is_its_menu_name()
    {
        foreach (var (view, _, label) in Menu().Where(p => p.View is not "about" and not "mining"))
        {
            var h2 = Regex.Match(Section(view), "<h2[^>]*>(?<title>[^<]*)</h2>");
            Assert.True(h2.Success, $"{view} has no title");
            Assert.Equal(label, h2.Groups["title"].Value.Trim());
        }
    }

    [Fact]
    public void No_page_keeps_one_of_the_old_kickers()
    {
        foreach (var old in new[] { "fleet-kicker", "economy-kicker\">Flight", "settings-eyebrow", "armoury-eyebrow", "reference-note" })
            Assert.DoesNotContain(old, Markup);
    }
}
