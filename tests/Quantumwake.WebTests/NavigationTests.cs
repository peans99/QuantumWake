using System.Text.RegularExpressions;

namespace Quantumwake.WebTests;

public class NavigationTests
{
    [Fact]
    public void Dashboard_navigation_groups_each_view_by_its_purpose()
    {
        var markup = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html"));
        var navigation = markup[markup.IndexOf("<nav class=\"tabs\"", StringComparison.Ordinal)..];
        navigation = navigation[..navigation.IndexOf("</nav>", StringComparison.Ordinal)];

        Assert.DoesNotContain(">Jobs<span", navigation);
        Assert.DoesNotContain(">Gear<span", navigation);

        Assert.Contains("data-view=\"now\"", navigation);
        Assert.Contains("data-view=\"map\"", navigation);
        Assert.Contains("data-view=\"log\"", navigation);

        // Mining is work - refinery orders, radar, the charge - so it moved from Reference (1.2.37).
        AssertViews(navigation, "Operations", "jobs", "routes", "contracts", "checklists", "blueprints", "wikelo", "mining");
        AssertViews(navigation, "Flight", "logbook", "sessions", "servers", "places", "points", "crew", "casualties");
        AssertViews(navigation, "Hangar", "fleet", "hangar", "loadout", "stash", "garage", "armoury");
        AssertViews(navigation, "Economy", "spending", "ledger", "commodities", "market");
        AssertViews(navigation, "Reference", "ships", "components", "parts", "loot", "crafting");
        AssertViews(navigation, "Settings", "settings", "overlay", "controls", "streamdeck", "labels", "imports", "about");
    }

    [Fact]
    public void Every_navigation_destination_has_a_matching_geometric_glyph()
    {
        var markup = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html"));
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "app.js"));
        var navigation = markup[markup.IndexOf("<nav class=\"tabs\"", StringComparison.Ordinal)..];
        navigation = navigation[..navigation.IndexOf("</nav>", StringComparison.Ordinal)];
        var registry = Regex.Match(
            source,
            "const PAGE_GLYPHS = Object\\.freeze\\(\\{(?<glyphs>.*?)\\}\\);",
            RegexOptions.Singleline).Groups["glyphs"].Value;
        var glyphs = Regex.Matches(registry, "(?<view>[a-z]+)\\s*:\\s*'(?<glyph>[^']+)'")
            .ToDictionary(match => match.Groups["view"].Value, match => match.Groups["glyph"].Value);

        foreach (Match view in Regex.Matches(navigation, "data-view=\"([^\"]+)\""))
        {
            var viewName = view.Groups[1].Value;
            Assert.True(glyphs.ContainsKey(viewName), $"{viewName} has no page glyph.");
        }

        Assert.Equal(glyphs.Count, glyphs.Values.Distinct().Count());
    }

    private static void AssertViews(string navigation, string group, params string[] views)
    {
        var match = Regex.Match(
            navigation,
            $"<div class=\"tab-group\">\\s*<button type=\"button\" class=\"group-btn\"[^>]*>{group}<span.*?</div>\\s*</div>",
            RegexOptions.Singleline);

        Assert.True(match.Success, $"{group} navigation group is missing.");
        foreach (var view in views)
            Assert.Contains($"data-view=\"{view}\"", match.Value);
    }
}
