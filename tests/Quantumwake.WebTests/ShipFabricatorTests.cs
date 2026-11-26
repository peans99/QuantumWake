namespace Quantumwake.WebTests;

/// <summary>
/// 5.0's ship fabricators (1.3.7): FPS armour in 0.7 of the time, on the
/// blueprint list and in the blueprint window.
/// </summary>
/// <remarks>
/// The figures are the 5.0 PTU's (5.0.202.46035): the A23 Flight Helmet takes
/// 90 s at a standard fabricator and 63 s in the Carrack's or the Drake kit's,
/// whose ships are the Carrack, Carrack Expedition, Clipper and Ironclad Assault.
/// </remarks>
public class ShipFabricatorTests
{
    private const string Faster = """
        [{"multiplier":0.7,"craftSeconds":63,"ships":["Anvil Carrack","Anvil Carrack Expedition","Drake Clipper","Drake Ironclad Assault"]}]
        """;

    private static string Recipes(string faster) => $$"""
        [{"output":"A23 Flight Helmet Woodland","outputClass":"helmet_a23","type":"Char_Armor_Helmet","grade":1,"kind":"creation",
          "craftSeconds":90,"faster":{{faster}},"materials":["Titanium 1 SCU"],
          "default":false,"rewardPools":[],"shopPrice":null,"owned":false,"receivedAt":null,"source":"install"}]
        """;

    private const string Row = "__dom.node('#crafting-table tbody').querySelectorAll('tr')[0]";

    private static Page Listed(string faster)
    {
        var page = new Page();
        page.Serve("/api/reference/blueprints", Recipes(faster));
        page.Do("await loadCraftingRef();");
        return page;
    }

    [Fact]
    public void The_list_gives_the_ship_fabricator_time_beside_the_standard_one()
    {
        var page = Listed(Faster);

        var row = page.Text($"{Row}.textContent");
        // craftTime rounds past a minute: 90 s is "2m", 63 s "1m".
        Assert.Contains("2m", row);
        Assert.Contains("1m in a ship", row);
        Assert.Contains("Anvil Carrack", page.Text($"{Row}.querySelectorAll('.blueprint-faster')[0].title"));
    }

    [Fact]
    public void A_blueprint_no_ship_crafts_faster_shows_only_its_own_time()
    {
        var page = Listed("[]");

        Assert.Equal(0, page.Count($"{Row}.querySelectorAll('.blueprint-faster').length"));
    }

    [Fact]
    public void The_blueprint_window_names_the_ships()
    {
        var page = new Page();
        page.Serve("/api/reference/blueprint?id=helmet_a23", $$"""
            {"output":"A23 Flight Helmet Woodland","outputClass":"helmet_a23","kind":"creation","craftSeconds":90,
             "faster":{{Faster}},"materials":["Titanium 1 SCU"],"byDefault":false,"owned":false,"shopPrice":null,"pools":[]}
            """);
        page.Do("await openRefDetail('blueprint', 'helmet_a23');");

        Assert.Contains("1m in a ship's fabricator (0.7× the time) — Anvil Carrack, Anvil Carrack Expedition, Drake Clipper, Drake Ironclad Assault",
            page.NodeText("#ref-modal-body"));
    }
}
