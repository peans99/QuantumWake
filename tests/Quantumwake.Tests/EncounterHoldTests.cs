using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Which hold an encounter ship's cargo list fills, and how the game files an
/// encounter. The hulls are 4.10.2's: the install gives the Cutlass Black 46
/// SCU of grids and no NPC Cutlass its own, and the Avenger Titan 8 while the
/// Stalker built on the same frame has none.
/// </summary>
public class EncounterHoldTests
{
    private static InstallCargoHull Hull(double scu) =>
        new(true, scu > 0 ? [new CargoGrid("grid", null, 0, 0, 0, scu, new Vec3(0, 0, 0), new Vec3(1, 1, 1), false)] : []);

    private static readonly Dictionary<string, InstallCargoHull> Hulls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DRAK_Cutlass"] = Hull(12),
        ["DRAK_Cutlass_Black"] = Hull(46),
        ["AEGS_Avenger_Titan"] = Hull(8),
        ["MISC_Starfarer_PU_AI_CIV"] = Hull(291),
    };

    [Fact]
    public void An_npc_ship_takes_the_longest_hull_it_is_built_on()
    {
        Assert.Equal(("DRAK_Cutlass_Black", 46.0), GameEncounters.Hold("DRAK_Cutlass_Black_PU_AI_NT", Hulls));
    }

    [Fact]
    public void Its_own_grids_win_over_the_hull()
    {
        Assert.Equal(("MISC_Starfarer_PU_AI_CIV", 291.0), GameEncounters.Hold("MISC_Starfarer_PU_AI_CIV", Hulls));
    }

    [Fact]
    public void A_hull_that_only_shares_a_maker_and_frame_is_not_its_hold()
    {
        // AEGS_Avenger_Stalker is not AEGS_Avenger_Titan: the prefix has to be
        // the whole hull class followed by an underscore.
        Assert.Null(GameEncounters.Hold("AEGS_Avenger_Stalker_PU_AI_CRIM", Hulls));
        Assert.Null(GameEncounters.Hold(null, Hulls));
    }

    [Theory]
    [InlineData("libs/foundry/records/aiwavecollection/pu/mission/ai_bounty/bounty_wave_hard_stanton2.xml", "pu/mission/ai_bounty")]
    [InlineData("libs/foundry/records/aiwavecollection/ea/ea_vanduulswarm.xml", "ea")]
    [InlineData("libs/foundry/records/aiwavecollection/pu/aiwavecollection.crusader_missionmanager_test_waves.xml", "pu")]
    public void The_folder_is_the_games_filing_below_aiwavecollection(string file, string folder)
    {
        Assert.Equal(folder, GameEncounters.Folder(file));
    }

    [Fact]
    public void Arena_Commander_is_the_ea_folder()
    {
        Assert.True(new GameEncounter("EA_VanduulSwarm", "ea", [], []).ArenaCommander);
        Assert.False(new GameEncounter("Bounty_Wave_Hard_Stanton2", "pu/mission/ai_bounty", [], []).ArenaCommander);
    }
}
