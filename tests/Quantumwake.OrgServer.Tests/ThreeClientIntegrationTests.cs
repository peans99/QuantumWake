using Microsoft.Extensions.Logging.Abstractions;
using Quantumwake.Data;
using Quantumwake.OrgShared;
using Quantumwake.Server;
using System.Net.Http.Json;

namespace Quantumwake.OrgServer.Tests;

/// <summary>The shipped desktop client speaking to a real in-process org server three times.</summary>
public sealed class ThreeClientIntegrationTests(OrgServerUnderTest server) : IClassFixture<OrgServerUnderTest>
{
    [Fact]
    public async Task Three_independent_clients_join_share_read_and_remove_blueprints()
    {
        var (_, adminToken) = server.Person("admin");
        var owner = server.Person("flightlead", "FlightLead");
        var scout = server.Person("scout", "ScoutOne");
        var quartermaster = server.Person("quartermaster", "QuarterMaster");

        var created = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs", owner.Token,
            new { name = "Three Client Exercise" }));
        var orgId = created.GetProperty("id").GetString()!;
        await server.Send(HttpMethod.Post, $"/api/admin/orgs/{orgId}/activate", adminToken);
        var invite = await server.Json(await server.Send(HttpMethod.Post, $"/api/orgs/{orgId}/invites",
            owner.Token, new { expiresInDays = 1, maxUses = 2 }));
        var code = invite.GetProperty("code").GetString();
        await server.Send(HttpMethod.Post, "/api/orgs/join", scout.Token, new { code });
        await server.Send(HttpMethod.Post, "/api/orgs/join", quartermaster.Token, new { code });
        await server.Send(HttpMethod.Post, $"/api/orgs/{orgId}/modules/blueprints", owner.Token,
            new { enabled = true });

        var clients = new[]
        {
            Client(owner, "lead"), Client(scout, "scout"), Client(quartermaster, "quartermaster"),
        };
        try
        {
            foreach (var client in clients)
            {
                Assert.Null(await client.RefreshAsync());
                var (members, problem) = await client.MembersAsync();
                Assert.Null(problem);
                Assert.Equal(3, members!.Count);
                Assert.All(members, m => Assert.True(m.AppLinked));
            }

            Assert.Null(await clients[0].ShareBlueprintsAsync(
                [new OrgBlueprintUploadRow(DateTimeOffset.Parse("2026-08-24T20:00:00Z"), "Atlas Powerplant Mk I")]));
            Assert.Null(await clients[1].ShareBlueprintsAsync(
                [new OrgBlueprintUploadRow(DateTimeOffset.Parse("2026-08-24T20:05:00Z"), "Mirage Shield Mk II")]));
            Assert.Null(await clients[2].ShareBlueprintsAsync(
                [new OrgBlueprintUploadRow(DateTimeOffset.Parse("2026-08-24T20:10:00Z"), "Lancer Cooler Mk III")]));

            var (all, allProblem) = await clients[1].BlueprintsAsync();
            Assert.Null(allProblem);
            Assert.Equal(3, all!.Count);
            Assert.Equal(3, all.Select(x => x.AccountId).Distinct().Count());

            Assert.Null(await clients[1].RemoveBlueprintsAsync());
            var (remaining, _) = await clients[0].BlueprintsAsync();
            Assert.Equal(2, remaining!.Count);
            Assert.DoesNotContain(remaining, b => b.Handle == "ScoutOne");

            var audit = await server.Json(await server.Send(HttpMethod.Get,
                $"/api/orgs/{orgId}/audit", owner.Token));
            var actions = audit.EnumerateArray().Select(e => e.GetProperty("action").GetString()).ToArray();
            Assert.Contains("module.blueprints", actions);
            Assert.Contains("blueprints.shared", actions);
            Assert.Contains("blueprints.removed", actions);
        }
        finally
        {
            foreach (var (_, directory) in _created)
            {
                try { Directory.Delete(directory, true); } catch { }
            }
        }

        OrgClient Client((string AccountId, string Token) person, string name)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"qw-org-client-{name}-{Guid.NewGuid():N}");
            var link = new OrgLink(directory);
            Assert.Null(link.Configure(server.Client.BaseAddress!.ToString()));
            link.CompleteLink(person.Token, name, name);
            link.SetActiveOrg(orgId);
            var client = new OrgClient(new TestClientFactory(), link, NullLogger<OrgClient>.Instance);
            _created.Add((client, directory));
            return client;
        }
    }

    private readonly List<(OrgClient Client, string Directory)> _created = [];

    private sealed class TestClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
