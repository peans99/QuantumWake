namespace Quantumwake.WebTests;

/// <summary>
/// From an item to its blueprint, and from the blueprint to where it comes
/// from. The rows are this install's: the CF-337 Panther's blueprint is one
/// in seven of what the NyxFoxwellHard pool pays, and Foxwell Enforcement's
/// Nyx contracts pay from it; the S-38's is on the starting list.
/// </summary>
public class BlueprintCardTests
{
    private const string Item = """
        {"className":"KLWE_LaserRepeater_S3","uex":true,
         "card":{"kind":"part","id":"KLWE_LaserRepeater_S3","name":"CF-337 Panther Repeater","facts":[]},
         "shops":[],"stockOn":[],
         "craft":{"output":"CF-337 Panther Repeater","outputClass":"KLWE_LaserRepeater_S3","craftSeconds":1680,
                  "materials":["Agricium 1.16 SCU"],"byDefault":false,"owned":false,"sources":3}}
        """;

    private const string Blueprint = """
        {"output":"CF-337 Panther Repeater","outputClass":"KLWE_LaserRepeater_S3","kind":"creation","craftSeconds":1680,
         "materials":["Agricium 1.16 SCU","Hadanite ×23"],"byDefault":false,"owned":false,"shopPrice":36308,
         "pools":[
           {"name":"REWARDS NyxFoxwellHard","share":0.1428,"sources":[
             {"kind":"contract","issuer":"Foxwell Enforcement","title":"Red Lvl. Contract: Defend Objects and Escort Employees",
              "systems":["Nyx"],"variants":1,"chance":1,"points":0}]},
           {"name":"Battaglia","share":0.056,"sources":[]}]}
        """;

    private static Page Opened()
    {
        var page = new Page();
        page.Serve("/api/reference/item?id=KLWE_LaserRepeater_S3", Item);
        page.Serve("/api/reference/blueprint?id=KLWE_LaserRepeater_S3", Blueprint);
        page.Do("await openRefDetail('item', 'KLWE_LaserRepeater_S3');");
        return page;
    }

    private const string Links = "__dom.node('#ref-modal-body').querySelectorAll('.ref-link')";

    [Fact]
    public void An_item_says_whether_you_have_its_blueprint_and_how_many_ways_there_are()
    {
        var text = Opened().NodeText("#ref-modal-body");

        Assert.Contains("Blueprint", text);
        Assert.Contains("not in your library", text);
        Assert.Contains("3 ways to get it", text);
        Assert.Contains("Where to get the blueprint", text);
    }

    [Fact]
    public void The_link_opens_the_blueprint_with_who_pays_it_out()
    {
        var page = Opened();
        page.Do($"await {Links}[{Links}.length - 1].fire('click');");

        Assert.Equal("CF-337 Panther Repeater blueprint", page.NodeText("#ref-modal-title"));
        var text = page.NodeText("#ref-modal-body");
        Assert.Contains("Foxwell Enforcement", text);
        Assert.Contains("Red Lvl. Contract", text);
        Assert.Contains("Nyx", text);
        Assert.Contains("14%", text);
        Assert.Contains("Hadanite", text);
    }

    /// <summary>A pool no contract names says so, rather than inventing a mission from its name.</summary>
    [Fact]
    public void A_pool_nothing_pays_is_said_to_be_unpaid()
    {
        var page = Opened();
        page.Do($"await {Links}[{Links}.length - 1].fire('click');");

        Assert.Contains("No contract or event tier on this build pays from this pool", page.NodeText("#ref-modal-body"));
    }

    /// <summary>
    /// Nine blueprints are on the starting whitelist. A blueprint in no pool
    /// is not one of them by default - that was the old, wrong assumption.
    /// </summary>
    [Fact]
    public void Only_the_starting_list_is_called_known_from_the_start()
    {
        var page = new Page();
        page.Serve("/api/reference/blueprint?id=behr_pistol_ballistic_01", """
            {"output":"S-38 Pistol","outputClass":"behr_pistol_ballistic_01","craftSeconds":60,"materials":[],"byDefault":true,"owned":false,"pools":[]}
            """);
        page.Serve("/api/reference/blueprint?id=orphan_01", """
            {"output":"Orphan","outputClass":"orphan_01","craftSeconds":60,"materials":[],"byDefault":false,"owned":false,"pools":[]}
            """);

        page.Do("await openRefDetail('blueprint', 'behr_pistol_ballistic_01');");
        Assert.Contains("starting list", page.NodeText("#ref-modal-body"));

        page.Do("await openRefDetail('blueprint', 'orphan_01');");
        var text = page.NodeText("#ref-modal-body");
        Assert.DoesNotContain("starts with it", text);
        Assert.Contains("No contract, event tier or starting list", text);
    }

    [Fact]
    public void A_crafting_row_opens_its_blueprint()
    {
        var page = new Page();
        page.Serve("/api/reference/blueprints", """
            [{"output":"CF-337 Panther Repeater","outputClass":"KLWE_LaserRepeater_S3","type":"WeaponGun","grade":1,"kind":"creation",
              "craftSeconds":1680,"materials":["Agricium 1.16 SCU"],"default":false,"rewardPools":["REWARDS NyxFoxwellHard"],
              "shopPrice":36308,"owned":false,"source":"install"}]
            """);
        page.Serve("/api/reference/blueprint?id=KLWE_LaserRepeater_S3", Blueprint);
        page.Do("await loadCraftingRef();");
        page.Do("await __dom.node('#crafting-table tbody').byClass('ref-link')[0].fire('click');");

        Assert.Equal("CF-337 Panther Repeater blueprint", page.NodeText("#ref-modal-title"));
    }
}
