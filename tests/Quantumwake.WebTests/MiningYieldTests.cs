namespace Quantumwake.WebTests;

/// <summary>
/// How much of a rock an ore actually is.
/// </summary>
/// <remarks>
/// The page has always said how likely a rock is to appear and never how much
/// of it is worth anything. The install says both, and says the second as a
/// range because that is what it is — the game gives a rock a band and rolls
/// within it, so a single figure would read as a promise.
/// </remarks>
public class MiningYieldTests
{
    private static Page Loaded(string body)
    {
        var page = new Page();
        page.Serve("/api/reference/resources", body);
        page.Do("await loadMiningRef();");
        return page;
    }

    [Fact]
    public void A_band_is_shown_as_a_band()
    {
        var page = Loaded("""
            [{"resource":"Ice","deposit":"Ice Deposit","minPercent":9.7,"maxPercent":84.3,
              "kind":"mineable","location":"Daymar","system":"Stanton","group":"Mineables",
              "groupChance":0.5,"share":0.2,"source":"install"}]
            """);

        Assert.Contains("9.7–84%", page.NodeText("#mining-table tbody"));
    }

    /// <summary>
    /// A band with the same ends is one number, not a range from itself.
    /// </summary>
    [Fact]
    public void A_fixed_share_is_shown_as_one_number()
    {
        var page = Loaded("""
            [{"resource":"Iron","deposit":null,"minPercent":12,"maxPercent":12,
              "kind":"mineable","location":"Daymar","system":"Stanton","group":"Mineables",
              "groupChance":0.5,"share":0.2,"source":"install"}]
            """);

        var body = page.NodeText("#mining-table tbody");

        Assert.Contains("12%", body);
        Assert.DoesNotContain("12–12%", body);
    }

    /// <summary>
    /// Salvage has no ore share, and the download has none for anything. Both
    /// must read as a dash rather than as nought per cent.
    /// </summary>
    [Fact]
    public void Nothing_known_is_a_dash_rather_than_zero()
    {
        var page = Loaded("""
            [{"resource":"Wreckage","deposit":null,"minPercent":null,"maxPercent":null,
              "kind":"salvageable","location":"Daymar","system":"Stanton","group":"Salvage",
              "groupChance":0.5,"share":0.2,"source":"dataset"}]
            """);

        // The third cell, checked on its own: a share of 50% elsewhere in the
        // row contains "0%" and would satisfy a looser assertion.
        Assert.Equal(
            "—",
            page.Text("__dom.node('#mining-table tbody').querySelectorAll('td')[2].textContent"));
    }
}
