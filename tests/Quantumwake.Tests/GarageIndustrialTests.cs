using System.Text.Json;
using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Tests;

[Collection("server")]
public class GarageIndustrialTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    // Industrial branches cut from ships.json retain the original editability,
    // nesting and size limits, including non-editable scraper modules.
    [Fact]
    public async Task Industrial_heads_reach_the_bench_and_offer_only_their_slot_sizes()
    {
        var parts = CommunityData.DigestPartStats(Fixture("garage-industrial-items.json"));
        var raw = Fixture("garage-industrial-ships.json");
        var ships = CommunityData.DigestShipStats(raw, parts);
        var slots = CommunityData.DigestShipSlots(raw);
        Assert.Contains(slots["DRAK_Golem"], s => s.Kind == "WeaponMining");
        var server = new ServerUnderTest();
        var directory = Path.Combine(server.DataDirectory, "community");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "digest.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "digest-part-stats.json"), JsonSerializer.Serialize(parts));
        File.WriteAllText(Path.Combine(directory, "digest-ship-stats.json"), JsonSerializer.Serialize(ships));
        File.WriteAllText(Path.Combine(directory, "meta.json"), """{"IndustrialPorts":true}""");
        await server.InitializeAsync();
        try
        {
            foreach (var (cls, kind, count, replacement) in new[]
            {
                ("MISC_Prospector", "WeaponMining", 1, "Mining_Laser_THCN_Helix_S1"),
                ("ARGO_MOLE", "WeaponMining", 3, "Mining_Laser_THCN_Helix_S2"),
                ("DRAK_Vulture", "SalvageHead", 2, "Salvage_Head_Salvation"),
                ("AEGS_Reclaimer", "SalvageHead", 2, "Salvage_Head_Salvation")
            })
            {
                var response = await server.Get($"/api/garage/{cls}");
                Assert.True(response.GetProperty("industrialPortsKnown").GetBoolean());
                var ports = response.GetProperty("ports").EnumerateArray().ToArray();
                Assert.Equal(count, ports.Length);
                Assert.All(ports, p => Assert.Equal(kind, p.GetProperty("group").GetString()));
                Assert.All(slots[cls], s => Assert.Equal(kind, s.Kind));
                var port = ports[0];
                var id = port.GetProperty("portId").GetString()!;
                var options = (await server.Get($"/api/garage/{cls}/options?port={id}"))
                    .GetProperty("options").EnumerateArray().Select(o => o.GetProperty("part")).ToArray();
                Assert.Contains(options, p => p.GetProperty("class").GetString() == replacement);
                Assert.All(options, p =>
                {
                    Assert.Equal(kind, p.GetProperty("type").GetString());
                    Assert.DoesNotContain("TEST", p.GetProperty("class").GetString()!, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Template", p.GetProperty("class").GetString()!, StringComparison.OrdinalIgnoreCase);
                    Assert.InRange(p.GetProperty("size").GetInt32(), port.GetProperty("minSize").GetInt32(), port.GetProperty("maxSize").GetInt32());
                });
                var fitted = await server.Posted($"/api/garage/{cls}/sheet", new { swaps = new Dictionary<string, string> { [id] = replacement } });
                Assert.Contains(fitted.GetProperty("ports").EnumerateArray(), p => p.GetProperty("class").GetString() == replacement && p.GetProperty("changed").GetBoolean());
            }
            // The Golem's head swaps like any S1 head: a pilot fitted a Helix I
            // in game (2026-11-07), and the dump marks the port editable.
            var golem = await server.Get("/api/garage/DRAK_Golem");
            var golemHead = Assert.Single(golem.GetProperty("ports").EnumerateArray());
            Assert.True(!golemHead.TryGetProperty("fixedReason", out var reason) || reason.ValueKind == JsonValueKind.Null);
            var golemPort = golemHead.GetProperty("portId").GetString()!;
            var golemOptions = (await server.Get($"/api/garage/DRAK_Golem/options?port={golemPort}"))
                .GetProperty("options").EnumerateArray().Select(o => o.GetProperty("part").GetProperty("class").GetString()).ToArray();
            Assert.Contains("Mining_Laser_THCN_Helix_S1", golemOptions);
            Assert.Contains("Mining_Laser_DRAK_Golem_S1", golemOptions);
            var helix = new { swaps = new Dictionary<string, string> { [golemPort] = "Mining_Laser_THCN_Helix_S1" } };
            var golemFitted = await server.Posted("/api/garage/DRAK_Golem/sheet", helix);
            Assert.Contains(golemFitted.GetProperty("ports").EnumerateArray(), p => p.GetProperty("class").GetString() == "Mining_Laser_THCN_Helix_S1" && p.GetProperty("changed").GetBoolean());
            var miningGolem = Quantumwake.Server.ServerHost.MiningShips(new CommunityData(directory)).Single(s => s.Class == "DRAK_Golem");
            Assert.True(Assert.Single(miningGolem.Heads).Editable);

            // The Pitman has been seen only on the Golem, so no other hull is offered it.
            var prospectorPort = (await server.Get("/api/garage/MISC_Prospector")).GetProperty("ports")[0].GetProperty("portId").GetString()!;
            var pitman = new { swaps = new Dictionary<string, string> { [prospectorPort] = "Mining_Laser_DRAK_Golem_S1" } };
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await server.Post("/api/garage/MISC_Prospector/sheet", pitman)).StatusCode);
            Assert.DoesNotContain((await server.Get($"/api/garage/MISC_Prospector/options?port={prospectorPort}")).GetProperty("options").EnumerateArray(),
                o => o.GetProperty("part").GetProperty("class").GetString() == "Mining_Laser_DRAK_Golem_S1");
        }
        finally { await server.DisposeAsync(); }
    }

    [Fact]
    public void The_golem_takes_any_size_one_head_but_the_pitman_goes_nowhere_else()
    {
        const string pitman = "Mining_Laser_DRAK_Golem_S1";
        const string helix = "Mining_Laser_THCN_Helix_S1";
        Assert.True(IndustrialFit.Allows(pitman, true, 1, helix, 1));
        Assert.False(IndustrialFit.Allows(helix, true, 1, pitman, 1));
        Assert.True(IndustrialFit.Allows(pitman, false, 1, pitman, 1));
        Assert.False(IndustrialFit.Allows("stock", null, 1, helix, 1));
        Assert.False(IndustrialFit.Allows("stock", true, 2, helix, 1));
    }

    [Theory]
    [InlineData("{}", false)]
    [InlineData("{\"IndustrialPorts\":true}", true)]
    public void Old_caches_do_not_claim_to_have_industrial_port_compatibility(string meta, bool known)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"qw-industrial-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "digest.json"), "{}");
            File.WriteAllText(Path.Combine(directory, "meta.json"), meta);
            Assert.Equal(known, new CommunityData(directory).HasIndustrialPorts);
        }
        finally { Directory.Delete(directory, true); }
    }
}
