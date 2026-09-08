namespace Quantumwake.WebTests;

/// <summary>
/// Where the crafting recipes on the page came from.
/// </summary>
/// <remarks>
/// The install and the download describe the same 1,577 shared recipes and
/// agree on the craft time for 1,575 of them. The two that differ are recipes
/// the game changed after the download was built, so saying which source is
/// answering is saying how current the page is.
/// </remarks>
public class CraftingSourceTests
{
    private static string Recipes(string source) => $$"""
        [{"output":"Metamaterial Test #146","type":"WeaponGun","grade":1,"kind":"creation",
          "craftSeconds":70,"materials":["Titanium 2 SCU","Yormandi Eye x4"],
          "default":false,"rewardPools":["InterSec ResourceGathering"],
          "shopPrice":null,"owned":false,"receivedAt":null,"source":"{{source}}"}]
        """;

    private static Page Loaded(string body)
    {
        var page = new Page();
        page.Serve("/api/reference/blueprints", body);
        page.Do("await loadCraftingRef();");
        return page;
    }

    [Fact]
    public void The_install_is_named_as_the_source()
    {
        Assert.Contains(
            "your game install", Loaded(Recipes("install")).NodeText("#crafting-source"));
    }

    [Fact]
    public void The_download_is_named_when_it_is_the_source()
    {
        Assert.Contains(
            "community dataset", Loaded(Recipes("dataset")).NodeText("#crafting-source"));
    }

    [Fact]
    public void The_row_shows_the_recipe_it_was_given()
    {
        var body = Loaded(Recipes("install")).NodeText("#crafting-table tbody");

        Assert.Contains("Metamaterial Test #146", body);
        Assert.Contains("Yormandi Eye", body);
        Assert.Contains("1 reward pool", body);
    }

    /// <summary>
    /// Some recipes take ten seconds. Rounding those to minutes printed "0m",
    /// which reads as a number the page failed to find rather than as something
    /// that is simply quick.
    /// </summary>
    [Fact]
    public void A_recipe_under_a_minute_is_not_shown_as_zero()
    {
        var body = Loaded("""
            [{"output":"Probe","type":"Misc","grade":1,"kind":"creation","craftSeconds":10,
              "materials":["Aluminum 0.2 SCU"],"default":false,"rewardPools":[],
              "shopPrice":null,"owned":false,"receivedAt":null,"source":"install"}]
            """).NodeText("#crafting-table tbody");

        Assert.Contains("10s", body);
        Assert.DoesNotContain("0m", body);
    }
}
