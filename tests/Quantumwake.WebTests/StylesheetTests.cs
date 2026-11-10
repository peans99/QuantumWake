namespace Quantumwake.WebTests;

/// <summary>
/// The stylesheet's shape, since nothing else checks it.
/// </summary>
/// <remarks>
/// <c>node --check</c> guards the script and every WebTest exercises it; the
/// stylesheet had no guard at all, and one selector list left with a stray
/// brace rendered the whole dashboard unstyled - every rule after it ignored,
/// the page still "working", and nothing red anywhere until a screenshot.
/// Braces and comments balancing is not proof the CSS is right, but it is the
/// failure that actually happened.
/// </remarks>
public class StylesheetTests
{
    private static string Css(string name = "app.css") => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", name));

    [Theory]
    [InlineData("app.css")]
    [InlineData("console.css")]
    public void Braces_balance_and_never_go_negative(string name)
    {
        var css = Css(name);
        var depth = 0;
        var line = 1;

        foreach (var ch in css)
        {
            if (ch == '\n') line++;
            if (ch == '{') depth++;
            if (ch == '}') depth--;
            Assert.True(depth >= 0, $"a closing brace with nothing open at line {line}");
        }

        Assert.Equal(0, depth);
    }

    /// <summary>
    /// A rule that sets <c>display</c> outranks the <c>hidden</c> attribute,
    /// so each of these - which the script shows and hides by that attribute -
    /// needs its own <c>[hidden]</c> rule or it never goes away. The command
    /// palette covered the dashboard on every load that way; the rest were
    /// found by hooking the <c>hidden</c> setter in a real browser and asking
    /// which elements stayed displayed: an empty focus strip, a gap where no
    /// ship logo is, the crack calculator beside its own "not ready" note, the
    /// goal form still open under a goal already set.
    /// </summary>
    [Theory]
    [InlineData("command-palette")]
    [InlineData("now-focus")]
    [InlineData("now-ship-logo")]
    [InlineData("statline")]
    [InlineData("mining-salvage-brief")]
    [InlineData("crack")]
    [InlineData("controls-device")]
    [InlineData("part-tags")]
    [InlineData("map-info-services")]
    [InlineData("map-info-amenities")]
    [InlineData("goal-form")]
    public void An_element_the_script_hides_by_attribute_is_not_kept_on_screen_by_its_display_rule(string name)
    {
        var css = Css();

        Assert.Matches(new System.Text.RegularExpressions.Regex(
            $@"^\s*\.{name}\s*\{{[^}}]*display\s*:\s*(grid|flex)", System.Text.RegularExpressions.RegexOptions.Multiline), css);
        Assert.Matches($@"\.{name}\[hidden\][^{{]*\{{\s*display\s*:\s*none", css);
    }

    /// <summary>
    /// The page-heading diamond overrides the plain heading's tick, which is
    /// absolutely placed, filled and centred by a translate. Leaving any of
    /// that to inheritance drew the diamond solid, low and over the title's
    /// first letter on every page, so the override restates all three.
    /// </summary>
    [Fact]
    public void The_page_heading_diamond_is_hollow_centred_and_clear_of_the_title()
    {
        var rule = System.Text.RegularExpressions.Regex.Match(Css(), @"\.section-bar h2::before\s*\{(?<body>[^}]*)\}").Groups["body"].Value;

        Assert.Contains("position: absolute", rule);
        Assert.Contains("background: transparent", rule);
        Assert.Contains("translateY(-50%) rotate(45deg)", rule);
        Assert.Matches(@"\.section-bar h2\s*\{\s*padding-left:\s*1[6-9]px", Css());
    }

    [Theory]
    [InlineData("app.css")]
    [InlineData("console.css")]
    public void Comments_open_and_close_in_pairs(string name)
    {
        var css = Css(name);
        Assert.Equal(CountOf(css, "/*"), CountOf(css, "*/"));
    }

    /// <summary>A selector list that ends in a comma has lost the rule it was leading to.</summary>
    [Theory]
    [InlineData("app.css")]
    [InlineData("console.css")]
    public void No_selector_list_dangles(string name)
    {
        var lines = Css(name).Split('\n');
        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (!lines[i].TrimEnd().EndsWith(',')) continue;
            var next = lines[i + 1].Trim();
            Assert.False(next.Length == 0 || next.StartsWith("/*") || next.EndsWith(':'), $"selector list dangles at line {i + 1}");
        }
    }

    /// <summary>
    /// console.css restyles the header and nothing else. 1.2.18 used it to
    /// restyle every page and was taken back in 1.2.19 because the old pages
    /// were preferred; page styles belong in app.css.
    /// </summary>
    [Fact]
    public void Console_stylesheet_only_touches_the_header()
    {
        var header = new[] { ".topbar", ".brand", ".status", ".search-box", ".tabs", "#tabs", ".group-menu" };
        var underIt = new[] { ".flight-telemetry", ".context-rail", ".deck-summary" };
        var css = System.Text.RegularExpressions.Regex.Replace(Css("console.css"), @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match rule in System.Text.RegularExpressions.Regex.Matches(css, @"([^{}]+)\{[^{}]*\}"))
        {
            foreach (var selector in rule.Groups[1].Value.Split(','))
            {
                var s = selector.Trim();
                if (s == "body:not(.overlay)") continue;
                Assert.StartsWith("body:not(.overlay) ", s);
                Assert.True(header.Concat(underIt).Any(s.Contains), $"console.css styles something outside the header: {s}");
            }
        }
    }

    private static int CountOf(string text, string token)
    {
        var count = 0;
        for (var at = text.IndexOf(token, StringComparison.Ordinal); at >= 0; at = text.IndexOf(token, at + token.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
