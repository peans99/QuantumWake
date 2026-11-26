namespace Quantumwake.WebTests;

/// <summary>
/// A ship window's claim line: the install's own wait and fee, and the 5.0
/// claim factors said as factors rather than folded into minutes nobody checked.
/// </summary>
public class ShipClaimTests
{
    [Fact]
    public void The_install_wait_leads_and_the_fee_follows()
    {
        var page = new Page();

        var text = page.Text("""
            shipClaimText({ standardMinutes: 9.99, expeditedMinutes: 3.33, expediteFee: 6251, groups: [], repeat: null }, null)
            """);

        Assert.Contains("~10m standard", text);
        Assert.Contains("~3.3m expedited", text);
        Assert.Contains("6,251", text);
    }

    [Fact]
    public void Without_the_install_block_the_community_fee_still_shows()
    {
        var page = new Page();

        var text = page.Text("shipClaimText(null, { expeditedCost: 1806 })");

        Assert.Contains("1,806", text);
        Assert.DoesNotContain("standard", text);
    }

    [Fact]
    public void A_hull_in_two_groups_is_not_given_one_of_them()
    {
        var page = new Page();

        // The 5.0 PTU lists the Carrack in a 0.8 group and a 0.6 one.
        var note = page.Text("""
            shipClaimNote({ standardMinutes: 48.3, groups: [
              { cooldown: 0.8, cost: 1, delivery: 0.8 },
              { cooldown: 0.6, cost: 1, delivery: 0.6 }],
              repeat: { chain: 5, cooldown: 2.4, cost: 1.75, delivery: 1.5 } })
            """);

        Assert.Contains("2 claim groups", note);
        Assert.Contains("wait ×0.8", note);
        Assert.Contains("wait ×0.6", note);
        Assert.DoesNotContain("cost ×1", note);
        Assert.Contains("chain up to 5", note);
        Assert.Contains("×2.4 wait", note);
        Assert.Contains("not checked", note);
    }

    [Fact]
    public void A_hull_with_no_factors_gets_no_note()
    {
        var page = new Page();

        Assert.Equal("null", page.Text("String(shipClaimNote({ standardMinutes: 35, groups: [], repeat: null }))"));
    }
}
