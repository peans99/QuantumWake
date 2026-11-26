using System.Text.Json;
using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>The claim rules as the item-recovery record states them.</summary>
public class GameClaimsTests
{
    // The 5.0 PTU's shape, cut down: a starter group, the capital group, a
    // neutral group, and the haulers' group that also lists the Carrack.
    private static GameClaimRules Ptu() => new(
        new(StringComparer.OrdinalIgnoreCase) { ["ANVL_Carrack"] = new(48.3, 16.1, 19595) },
        [
            new(0.8, 0.8, 0.8, ["ANVL_C8_Pisces"]),
            new(0.8, 1, 0.8, ["ANVL_Carrack", "AEGS_Idris_P"]),
            new(1, 1, 1, ["ORIG_890Jump"]),
            new(0.6, 1, 0.6, ["CRUS_Starlifter_C2", "ANVL_Carrack"]),
        ],
        new ClaimRepeat(5, 2.4, 1.75, 1.5));

    [Fact]
    public void A_hull_in_two_groups_gets_both_back()
    {
        var groups = Ptu().GroupsOf("anvl_carrack");

        Assert.Equal([0.8, 0.6], groups.Select(g => g.Cooldown));
    }

    [Fact]
    public void A_group_of_ones_changes_nothing_and_is_left_out()
    {
        Assert.Empty(Ptu().GroupsOf("ORIG_890Jump"));
    }

    [Fact]
    public void LIVE_chain_of_one_is_no_repeat_rule()
    {
        Assert.False(new ClaimRepeat(1, 1, 1, 1).Active);
        Assert.True(Ptu().Repeat!.Active);
    }

    [Fact]
    public void The_rules_survive_the_cache()
    {
        var back = JsonSerializer.Deserialize<GameClaimRules>(JsonSerializer.Serialize(Ptu()))!;

        Assert.Equal(16.1, back.Waits["ANVL_Carrack"].ExpeditedMinutes);
        Assert.Equal(2, back.GroupsOf("ANVL_Carrack").Count);
        Assert.Equal(5, back.Repeat!.Chain);
    }
}
