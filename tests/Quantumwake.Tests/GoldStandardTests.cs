using System.Reflection;
using System.Text.Json;
using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// 4.10.3 deleted the Andromeda, Aquila, Phoenix Emerald and Hammerhead originals and
/// kept only their <c>_GS</c> reworks, while older logs still name the originals.
/// </summary>
public sealed class GoldStandardTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("qw-gs-").FullName;

    public void Dispose() => Directory.Delete(root, true);

    private static readonly HashSet<string> Install = new(StringComparer.OrdinalIgnoreCase)
    {
        "RSI_Constellation_Andromeda_GS",
        "RSI_Constellation_Andromeda_GS_PU_AI_CRIM",
        "AEGS_Hammerhead_GS",
        "RSI_Constellation_Taurus",
        "RSI_Constellation_Taurus_GS",
    };

    [Theory]
    [InlineData("RSI_Constellation_Andromeda", "RSI_Constellation_Andromeda_GS")]
    [InlineData("rsi_constellation_andromeda", "RSI_Constellation_Andromeda_GS")]
    [InlineData("RSI_Constellation_Andromeda_PU_AI_CRIM", "RSI_Constellation_Andromeda_GS_PU_AI_CRIM")]
    [InlineData("AEGS_Hammerhead", "AEGS_Hammerhead_GS")]
    public void A_deleted_class_finds_its_rework(string logged, string expected)
    {
        Assert.Equal(expected, GoldStandard.Successor(logged, Install.Contains), StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("RSI_Constellation_Aquila")] // no rework in this install
    [InlineData("RSI_Constellation_Andromeda_GS")] // already the rework
    [InlineData("RSI_Constellation_Andromeda_GS_Nonsense")]
    [InlineData("")]
    [InlineData(null)]
    public void Nothing_is_invented(string? logged)
    {
        Assert.Null(GoldStandard.Successor(logged, Install.Contains));
    }

    [Fact]
    public void Models_fall_back_only_when_the_class_is_gone()
    {
        ModelEntity Entity(string cls) => new(cls, $"objects/spaceships/{cls}.cga", null, null, "Vehicle", true, [], []);
        var catalogue = new GameModelCatalogue(
            new() { ["RSI_Constellation_Andromeda_GS"] = Entity("RSI_Constellation_Andromeda_GS"),
                    ["RSI_Constellation_Taurus"] = Entity("RSI_Constellation_Taurus"),
                    ["RSI_Constellation_Taurus_GS"] = Entity("RSI_Constellation_Taurus_GS") },
            []);

        Assert.Equal("RSI_Constellation_Andromeda_GS", catalogue.Find("RSI_Constellation_Andromeda")?.Class);
        // A class that still exists is used as it is, even with a rework beside it.
        Assert.Equal("RSI_Constellation_Taurus", catalogue.Find("RSI_Constellation_Taurus")?.Class);
        Assert.Null(catalogue.Find("AEGS_Hammerhead"));
    }

    [Fact]
    public void An_old_session_keeps_its_ship_size_and_hold()
    {
        var fake = Path.Combine(root, "Data.p4k");
        File.WriteAllText(fake, "");
        var version = typeof(GameCommodities).GetField("CacheVersion", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue();
        var box = new Vec3(1.25, 1.25, 1.25);
        var cache = Path.Combine(root, "commodities.json");
        File.WriteAllText(cache, JsonSerializer.Serialize(new
        {
            Stamp = $"{version}:{File.GetLastWriteTimeUtc(fake).Ticks}",
            Commodities = new Dictionary<string, string>(), Items = new Dictionary<string, string>(),
            Facts = new Dictionary<string, GameItem>(), Blueprints = Array.Empty<GameBlueprint>(),
            Spawns = Array.Empty<GameSpawn>(), Places = new Dictionary<string, GamePlace>(),
            Vehicles = new Dictionary<string, GameVehicle>
            {
                ["RSI_Constellation_Andromeda_GS"] = new("RSI_Constellation_Andromeda_GS", "RSI Constellation Mk IV Andromeda", 26, 61, 14, null, "Spaceship"),
            },
            CargoHulls = new Dictionary<string, InstallCargoHull>
            {
                ["RSI_Constellation_Andromeda_GS"] = new(true, [new CargoGrid("Grid", null, 4, 4, 6, 96, box, box, false)]),
            },
        }));

        var game = GameCommodities.Load(root, cache);

        Assert.Equal(61, game.Vehicle("RSI_Constellation_Andromeda")?.Length);
        Assert.Equal(96, game.CargoHull("RSI_Constellation_Andromeda")?.Scu);
        Assert.Null(game.Vehicle("AEGS_Hammerhead"));
    }
}
