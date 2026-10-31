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
}
