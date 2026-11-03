using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// The Radar tab's rows: one per ore for ship rocks, one per value for every
/// kind of rock whose value does not name its ore, and nothing a pilot never meets.
/// </summary>
/// <remarks>
/// Class names and values are the install's own (4.10.2), from
/// <c>Quantumwake.Cli --signatures --all</c>.
/// </remarks>
public class RockSignatureTests
{
    private static GameRockSignature Rock(string cls, double signature, bool placed = true) =>
        new(cls, null, signature, placed);

    /// <summary>
    /// A surface Quantainium and an asteroid one read the same 3170, so they
    /// are one row that is found in both places, not two rows a ping matches.
    /// </summary>
    [Fact]
    public void A_ship_ore_is_one_row_wherever_it_is_found()
    {
        var rows = GameRockSignatures.Radar(
        [
            Rock("MineableRock_SurfaceLegendary_Quantainium", 3170),
            Rock("MineableRock_AsteroidLegendary_Quantainium", 3170),
            Rock("MineableRock_SurfaceCommon_Iron", 4270),
        ]);

        var quantainium = Assert.Single(rows, r => r.Name == "Quantainium");
        Assert.Equal(("ship", "Legendary", true, true), (quantainium.Kind, quantainium.Tier, quantainium.Surface, quantainium.Asteroid));
        Assert.Equal(["Quantainium"], quantainium.Ores);
        Assert.False(Assert.Single(rows, r => r.Name == "Iron").Asteroid);
    }

    /// <summary>
    /// An RCD variant of Savrilium is still Savrilium at 3200; the suffix must
    /// not turn it into a second ore.
    /// </summary>
    [Fact]
    public void A_sized_variant_is_the_same_ore()
    {
        var rows = GameRockSignatures.Radar(
        [
            Rock("MineableRock_AsteroidLegendary_Savrilium", 3200),
            Rock("MineableRock_AsteroidLegendary_Savrilium_RCD_large", 3200),
        ]);

        Assert.Equal("Savrilium", Assert.Single(rows).Name);
    }

    /// <summary>
    /// Every C-type reads 4700 whatever it holds, so the row is the class and
    /// lists what it can hold rather than offering one ore.
    /// </summary>
    [Fact]
    public void An_asteroid_class_is_one_row_holding_every_ore_it_can()
    {
        var rows = GameRockSignatures.Radar(
        [
            Rock("AsteroidCTypeMineableRock_Copper", 4700),
            Rock("AsteroidCTypeMineableRock_Iron", 4700),
            Rock("AsteroidSTypeMineableRock_Iron", 4720),
        ]);

        var c = Assert.Single(rows, r => r.Signature == 4700);
        Assert.Equal(("asteroid", "C-type asteroid"), (c.Kind, c.Name));
        Assert.Equal(["Copper", "Iron"], c.Ores);
        Assert.Null(c.Tier);
    }

    /// <summary>
    /// 4000 is three kinds of rock at once on 4.10.2. Each is its own row, so a
    /// ping of 8000 can say it is two of something that the figure cannot name.
    /// </summary>
    [Fact]
    public void Kinds_sharing_a_value_stay_apart()
    {
        var rows = GameRockSignatures.Radar(
        [
            Rock("MineableRock_GroundVehicle_Carinite_large", 4000),
            Rock("MineableRock_GroundVehicle_Glacosite", 4000),
            Rock("GraniteMineableRock_Copper", 4000),
            Rock("FelsicMineableRock_Iron", 4000),
            Rock("QuantaniumMineableRock", 4000),
            Rock("MineableRock_FPS_Hadanite", 3000),
        ]);

        Assert.Equal(["hand", "ground", "deposit"], rows.Select(r => r.Kind));
        Assert.Equal(["Carinite", "Glacosite"], rows[1].Ores);
        Assert.Equal(["Copper", "Iron", "Quantanium"], rows[2].Ores);
        Assert.Equal(["Felsic", "Granite", "Quantanium"], rows[2].Stones);
    }

    /// <summary>
    /// The install carries 59 rocks nothing places and a FPS template that a
    /// preset does name; neither belongs among the answers to a ping.
    /// </summary>
    [Fact]
    public void Templates_tests_and_unplaced_rocks_are_left_out()
    {
        var rows = GameRockSignatures.Radar(
        [
            Rock("MineableRock_FPS_TEMPLATE", 3000),
            Rock("MineableRock_test_Quantainium", 4000, placed: false),
            Rock("AsteroidCTypeMineableRock_Test", 4700, placed: false),
            Rock("MineableRock_AsteroidLegendary_Savrilium_RCD_small", 3200, placed: false),
            Rock("MineableRock_SurfaceRare_TEMPLATE", 4000, placed: false),
            Rock("MineableRock_FPS_Aphorite", 3000),
        ]);

        var only = Assert.Single(rows);
        Assert.Equal(["Aphorite"], only.Ores);
    }
}
