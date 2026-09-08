namespace Quantumwake.WebTests;

/// <summary>
/// What quality a resource comes out at, where that differs, and how long the
/// slot takes to refill.
/// </summary>
/// <remarks>
/// Everything tops out at 1000, so the ceiling says nothing; the floor says
/// most of it. Ship mining never yields below 501, hand mining below 201, and
/// ground mining or gathering can give anything at all. A place that overrides
/// the class default is marked, because that is the whole point of showing it.
/// </remarks>
public class MiningQualityTests
{
    private static Page Loaded(string body)
    {
        var page = new Page();
        page.Serve("/api/reference/resources", body);
        page.Do("await loadMiningRef();");
        return page;
    }

    private static string Row(Page page, int cell) =>
        page.Text($"__dom.node('#mining-table tbody').querySelectorAll('td')[{cell}].textContent");

    [Fact]
    public void The_floor_is_what_the_row_shows()
    {
        var page = Loaded("""
            [{"resource":"Ice","deposit":null,"minPercent":10,"maxPercent":20,"kind":"mineable",
              "location":"Daymar","system":"Stanton","group":"Mineables","groupChance":0.5,
              "share":0.2,"source":"install","respawnSeconds":3600,
              "quality":{"min":501,"max":1000,"mean":500,"spread":143,"local":false}}]
            """);

        Assert.Equal("501+", Row(page, 3));
    }

    /// <summary>
    /// A place that differs from the usual is worth a mark, or the number reads
    /// as the same number everyone else sees.
    /// </summary>
    [Fact]
    public void A_local_override_is_marked()
    {
        var page = Loaded("""
            [{"resource":"Torite","deposit":null,"minPercent":5,"maxPercent":9,"kind":"mineable",
              "location":"Rockcracker","system":"Nyx","group":"Mineables","groupChance":0.5,
              "share":0.2,"source":"install","respawnSeconds":3600,
              "quality":{"min":651,"max":1000,"mean":500,"spread":159,"local":true}}]
            """);

        Assert.Equal("651+*", Row(page, 3));
    }

    /// <summary>
    /// Respawn is not one number across the game — an hour for most presets,
    /// two for some, twenty minutes for others — so it reads as a duration
    /// rather than as seconds nobody counts in.
    /// </summary>
    [Fact]
    public void Respawn_reads_as_a_duration()
    {
        var page = Loaded("""
            [{"resource":"Ice","deposit":null,"minPercent":10,"maxPercent":20,"kind":"mineable",
              "location":"Daymar","system":"Stanton","group":"Mineables","groupChance":0.5,
              "share":0.2,"source":"install","quality":null,"respawnSeconds":7200}]
            """);

        Assert.Equal("2h", Row(page, 4));
    }

    /// <summary>
    /// Salvage has no quality of this kind, and the download has none for
    /// anything, so both read as a dash rather than as a floor of nought.
    /// </summary>
    [Fact]
    public void Nothing_known_is_a_dash()
    {
        var page = Loaded("""
            [{"resource":"Wreckage","deposit":null,"minPercent":null,"maxPercent":null,
              "kind":"salvageable","location":"Daymar","system":"Stanton","group":"Salvage",
              "groupChance":0.5,"share":0.2,"source":"dataset","quality":null,
              "respawnSeconds":null}]
            """);

        Assert.Equal("—", Row(page, 3));
        Assert.Equal("—", Row(page, 4));
    }
}
