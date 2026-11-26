using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Which blueprints a 5.0 ship fabricator makes faster, as the PTU
/// (5.0.202.46035) states it: <c>CraftingModifierType_Time</c> 0.7 when the
/// blueprint's category is <c>FPSArmours</c>, on the Carrack's fabricator and
/// the Drake kit's.
/// </summary>
public class ShipFabricatorTests
{
    private static GameBlueprint Bp(string name, string category, int seconds = 90) =>
        new(name, name, "creation", seconds, [], [], Category: category, Record: "BP_CRAFT_" + name);

    private static readonly GameCraftSpeedup Carrack = new(
        "BaseBuilding_Interactables_ItemFabricator_Vehicle_ANVL_Carrack", 0.7, ["FPSArmours"], [],
        ["Anvil Carrack", "Anvil Carrack Expedition"]);

    private static readonly GameCraftSpeedup Drake = new(
        "BaseBuilding_Interactables_ItemFabricator_Vehicle_DRAK_kit_01", 0.7, ["FPSArmours"], [],
        ["Drake Clipper", "Drake Ironclad Assault"]);

    [Fact]
    public void Armour_is_crafted_in_seven_tenths_of_the_time_and_names_every_ship()
    {
        var helmet = GameBlueprints.WithSpeedups([Bp("helmet", "FPSArmours")], [Carrack, Drake]).Single();

        var faster = Assert.Single(helmet.Faster!);
        Assert.Equal(63, faster.CraftSeconds);
        Assert.Equal(["Anvil Carrack", "Anvil Carrack Expedition", "Drake Clipper", "Drake Ironclad Assault"], faster.Ships);
    }

    [Fact]
    public void Other_categories_are_untouched()
    {
        var gun = GameBlueprints.WithSpeedups([Bp("rifle", "FPSWeapons")], [Carrack]).Single();

        Assert.Null(gun.Faster);
    }

    [Fact]
    public void A_whitelist_names_its_blueprints_and_a_crafter_no_ship_carries_is_left_out()
    {
        var listed = new GameCraftSpeedup("Bench", 0.5, [], ["BP_CRAFT_rifle"], ["Somewhere"]);
        var nowhere = Carrack with { Ships = [] };

        Assert.Equal(45, GameBlueprints.WithSpeedups([Bp("rifle", "FPSWeapons")], [listed]).Single().Faster![0].CraftSeconds);
        Assert.Null(GameBlueprints.WithSpeedups([Bp("helmet", "FPSArmours")], [nowhere]).Single().Faster);
    }
}
