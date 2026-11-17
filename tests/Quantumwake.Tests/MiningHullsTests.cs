using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>Whether a hull carries a mining head, from the two descriptions of it (1.2.60).</summary>
public sealed class MiningHullsTests
{
    private static ShipSlot Slot(string kind) => new("p", "h", kind, 1, null, 0, null);

    private static FitPort Port(string? type, params string[] accepts) =>
        new("p", "h", null, type, false, 1, 1, accepts, []);

    private static ShipBase Hull(params FitPort[] loadout) => new(
        "X", "X", null, null, null, 1, 1, false, 0, 0, 0, new Vec3(0, 0, 0),
        new FlightStats(0, 0, 0, 0, 0, 0), 0, 0, 0, new Dictionary<string, int>(), null!, loadout);

    [Fact]
    public void A_changeable_mining_port_is_a_miner() =>
        Assert.True(MiningHulls.CanMine(null, [Slot("Shield"), Slot("WeaponMining")]));

    [Fact]
    public void A_mining_arm_only_the_fuller_loadout_shows_is_a_miner_as_on_the_ROC() =>
        Assert.True(MiningHulls.CanMine(Hull(new FitPort("a", "h", null, "Turret", false, 1, 1, [],
            [Port(null, "WeaponMining")])), [Slot("Shield")]));

    [Fact]
    public void A_known_hull_with_no_mining_port_is_not_as_the_Pisces() =>
        Assert.False(MiningHulls.CanMine(Hull(Port("WeaponGun")), [Slot("WeaponGun")]));

    [Fact]
    public void A_hull_neither_knows_is_unknown_as_the_Hermes() =>
        Assert.Null(MiningHulls.CanMine(null, []));
}
