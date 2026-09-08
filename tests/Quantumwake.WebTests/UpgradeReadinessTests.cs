namespace Quantumwake.WebTests;

/// <summary>
/// Saying a component has not shipped, only where the game would say it.
/// </summary>
/// <remarks>
/// The game's flightReady tag is not applied evenly. 196 of 203 weapon guns
/// carry it and not one of the 81 coolers does, so an untagged cooler means the
/// tag was never used for coolers — not that the cooler is unfinished. The
/// server sends null for those, and null must read as silence rather than as a
/// missing tag.
/// </remarks>
public class UpgradeReadinessTests
{
    private static Page Opened(string options)
    {
        var page = new Page();
        page.Do($$"""
            fillUpgradeOptions(__dom.node('#upgrade-test'), {
              kind: 'WeaponGun', size: 2, options: {{options}} });
            """);
        return page;
    }

    [Fact]
    public void A_part_the_game_has_not_marked_as_shipped_says_so()
    {
        var page = Opened("""
            [{"name":"Prototype Repeater","manufacturer":"Behring","grade":1,
              "flightReady":false,"price":1000,"shops":[{"terminal":"Dumper's Depot","placeId":"P1","place":"Area18","system":"Stanton","security":"policed","price":1000}]}]
            """);

        Assert.Contains("not flight ready", page.NodeText("#upgrade-test"));
    }

    [Fact]
    public void A_part_the_game_has_marked_is_left_alone()
    {
        var page = Opened("""
            [{"name":"Mantis GT-220","manufacturer":"Behring","grade":1,
              "flightReady":true,"price":1000,"shops":[{"terminal":"Dumper's Depot","placeId":"P1","place":"Area18","system":"Stanton","security":"policed","price":1000}]}]
            """);

        Assert.DoesNotContain("not flight ready", page.NodeText("#upgrade-test"));
    }

    /// <summary>
    /// The case that matters. A cooler is not unfinished merely because coolers
    /// are never tagged, so nothing is said about it at all.
    /// </summary>
    [Fact]
    public void A_kind_the_tag_is_never_used_on_says_nothing()
    {
        var page = Opened("""
            [{"name":"WhiteOut","manufacturer":"JuLegacy","grade":1,
              "flightReady":null,"price":1000,"shops":[{"terminal":"Dumper's Depot","placeId":"P1","place":"Area18","system":"Stanton","security":"policed","price":1000}]}]
            """);

        Assert.DoesNotContain("not flight ready", page.NodeText("#upgrade-test"));
        Assert.Contains("WhiteOut", page.NodeText("#upgrade-test"));
    }
}
