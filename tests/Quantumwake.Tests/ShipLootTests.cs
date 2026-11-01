using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Which ships the game flies as NPCs, and who flies them, read from the class
/// names this install uses.
/// </summary>
public class ShipLootTests
{
    [Theory]
    [InlineData("AEGS_Gladius_PU_AI_CRIM_IK", true)]
    [InlineData("MISC_Starfarer_PU_AI_CIV", true)]
    [InlineData("RSI_Polaris_AI_Template", true)]
    [InlineData("DRAK_Caterpillar_Pirate", true)]
    [InlineData("Orbital_Sentry_PU_Criminal", true)]
    // Arena Commander's pirates: nothing to take from them.
    [InlineData("AEGS_Gladius_EA_AI_PIR", false)]
    // The ship a pilot buys.
    [InlineData("AEGS_Gladius", false)]
    // "AI" inside a word is not the marker.
    [InlineData("XNAA_SanTokYai", false)]
    public void Npc_ships_are_told_apart_by_name(string entityClass, bool npc)
    {
        Assert.Equal(npc, GameShipLoot.IsNpcShip(entityClass));
    }

    [Theory]
    [InlineData("AEGS_Gladius_PU_AI_CRIM", "Criminal")]
    [InlineData("MISC_Starfarer_PU_AI_CIV_DEF", "Civilian")]
    [InlineData("MISC_Starfarer_PU_AI_Super_CIV", "Civilian · Elite")]
    [InlineData("DRAK_Cutlass_Black_PU_AI_NT_QIG", "Nine Tails")]
    [InlineData("AEGS_Gladius_PU_AI_UEE", "UEE Navy")]
    [InlineData("AEGS_Gladius_PU_AI_SEC_HurstonDynamics", "Security")]
    [InlineData("ANVL_Centurion_AI_NineTails", "Nine Tails")]
    [InlineData("RSI_Polaris_AI_Template", "NPC")]
    [InlineData("DRAK_Caterpillar_Pirate", "Pirate")]
    [InlineData("Orbital_Sentry_PU_Criminal", "Criminal")]
    // How a variant is set up is not who flies it.
    [InlineData("AEGS_Gladius_PU_AI_CRIM_IK", "Criminal")]
    [InlineData("XNAA_SanTokYai_AI_DefendShip", "NPC")]
    public void Who_flies_a_ship_is_read_from_its_name(string entityClass, string crew)
    {
        Assert.Equal(crew, GameShipLoot.Crew(entityClass));
    }

    private static GameLootData Fitted() => new()
    {
        Stock = new()
        {
            ["AEGS_Gladius"] = [new PartFit("KLWE_LaserRepeater_S3", 2), new PartFit("COOL_Bracer", 2)],
            ["AEGS_Gladius_Valiant"] = [new PartFit("KLWE_LaserRepeater_S3", 2)],
        },
        Ships = new()
        {
            ["KLWE_LaserRepeater_S3"] = [new ShipFit("AEGS_Gladius_PU_AI_CRIM", 2), new ShipFit("AEGS_Gladius_Valiant_PU_AI_UEE", 2)],
            ["BEHR_Missile_S2"] = [new ShipFit("AEGS_Gladius_PU_AI_CRIM", 4)],
        },
    };

    /// <summary>
    /// A part is followed both ways: from the hull to what it comes with, and
    /// from the part to the hulls that come with it.
    /// </summary>
    [Fact]
    public void Stock_parts_read_from_the_hull_and_from_the_part()
    {
        var loot = Fitted();

        Assert.Equal(["COOL_Bracer", "KLWE_LaserRepeater_S3"], loot.PartsOf("aegs_gladius").Select(p => p.Part).Order());
        Assert.Equal(["AEGS_Gladius", "AEGS_Gladius_Valiant"], loot.StockOn("KLWE_LaserRepeater_S3").Select(s => s.Ship));
        Assert.Equal(4, loot.PartsOf("AEGS_Gladius_PU_AI_CRIM").Single(p => p.Part == "BEHR_Missile_S2").Count);
    }

    /// <summary>
    /// The Valiant's NPC version is the Valiant's, not the Gladius's, though
    /// its class starts with AEGS_Gladius_ too.
    /// </summary>
    [Fact]
    public void An_npc_version_belongs_to_the_longest_hull_its_class_extends()
    {
        var loot = Fitted();

        Assert.Equal(["AEGS_Gladius_PU_AI_CRIM"], loot.NpcVersionsOf("AEGS_Gladius"));
        Assert.Equal(["AEGS_Gladius_Valiant_PU_AI_UEE"], loot.NpcVersionsOf("AEGS_Gladius_Valiant"));
    }
}
