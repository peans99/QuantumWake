namespace Quantumwake.WebTests;

/// <summary>
/// What looted gear is worth. The number shown is the median across every
/// terminal stocking it, not the cheapest, because the two differ tenfold on
/// at least one item this install has picked up.
/// </summary>
public class LootPriceTests
{
    private const string Pickups = """
        [{"at":"2026-08-20T09:00:00+00:00","item":"MaxLift Tractor Beam","itemClass":"maxlift_01",
          "place":"Orison","category":"Attachments","price":19175},
         {"at":"2026-08-19T09:00:00+00:00","item":"Bantam Hat Orange","itemClass":"987_hat_01",
          "place":"Orison","category":"Clothing","price":null}]
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/loot?days=0", Pickups);
        page.Do("__dom.node('#loot-period').value = '0'; await loadLoot();");
        return page;
    }

    [Fact]
    public void It_shows_what_an_item_typically_costs()
    {
        Assert.Contains("19,175 aUEC", Loaded().NodeText("#loot-table tbody"));
    }

    /// <summary>
    /// An unstocked item is not a worthless one. A blank cell reads as nothing
    /// and a zero as free, so the gap gets a dash and a reason instead.
    /// </summary>
    [Fact]
    public void An_item_nothing_stocks_shows_a_dash_rather_than_a_zero()
    {
        var page = Loaded();

        Assert.Contains("—", page.NodeText("#loot-table tbody"));
        Assert.DoesNotContain("0 aUEC", page.NodeText("#loot-table tbody"));
    }

    /// <summary>
    /// The summary says how much of the table carries a price, so a page that
    /// is mostly dashes is explained rather than looking broken.
    /// </summary>
    [Fact]
    public void The_summary_says_how_many_carry_a_price()
    {
        Assert.Contains("1 of 2", Loaded().NodeText("#loot-summary"));
    }
}
