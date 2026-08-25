using System.Net;
using System.Text.Json;

namespace Quantumwake.OrgServer.Tests;

/// <summary>
/// Who may do what inside an org, and what leaving actually removes.
/// </summary>
public class RoleAndDeletionTests(OrgServerUnderTest server) : IClassFixture<OrgServerUnderTest>
{
    /// <summary>An active org with an owner and two members, built the long way round.</summary>
    private async Task<(string OrgId, (string Id, string Token) Owner, (string Id, string Token) M1, (string Id, string Token) M2)>
        Trio(string name)
    {
        var (_, admin) = server.Person("admin");
        var owner = server.Person($"{name}-owner");
        var m1 = server.Person($"{name}-one");
        var m2 = server.Person($"{name}-two");

        var org = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs", owner.Token, new { name }));
        var orgId = org.GetProperty("id").GetString()!;
        await server.Send(HttpMethod.Post, $"/api/admin/orgs/{orgId}/activate", admin);

        var invite = await server.Json(await server.Send(HttpMethod.Post,
            $"/api/orgs/{orgId}/invites", owner.Token, new { expiresInDays = 7, maxUses = 0 }));
        var code = invite.GetProperty("code").GetString();
        await server.Send(HttpMethod.Post, "/api/orgs/join", m1.Token, new { code });
        await server.Send(HttpMethod.Post, "/api/orgs/join", m2.Token, new { code });

        return (orgId, (owner.AccountId, owner.Token), (m1.AccountId, m1.Token), (m2.AccountId, m2.Token));
    }

    [Fact]
    public async Task A_member_can_read_but_not_run_the_org()
    {
        var (orgId, _, m1, m2) = await Trio("Readers");

        Assert.True((await server.Send(HttpMethod.Get, $"/api/orgs/{orgId}/members", m1.Token)).IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await server.Send(HttpMethod.Get, $"/api/orgs/{orgId}/invites", m1.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await server.Send(HttpMethod.Delete, $"/api/orgs/{orgId}/members/{m2.Id}", m1.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await server.Send(HttpMethod.Post, $"/api/orgs/{orgId}/members/{m2.Id}/role", m1.Token,
                new { role = "manager" })).StatusCode);
    }

    [Fact]
    public async Task A_manager_removes_members_but_not_other_managers()
    {
        var (orgId, owner, m1, m2) = await Trio("Ranks");

        // Owner promotes m1; m1 can now kick m2 - but could not kick another manager.
        Assert.True((await server.Send(HttpMethod.Post, $"/api/orgs/{orgId}/members/{m1.Id}/role", owner.Token,
            new { role = "manager" })).IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await server.Send(HttpMethod.Delete, $"/api/orgs/{orgId}/members/{owner.Id}", m1.Token)).StatusCode);
        Assert.True((await server.Send(HttpMethod.Delete, $"/api/orgs/{orgId}/members/{m2.Id}", m1.Token)).IsSuccessStatusCode);

        // The kicked member is gone from the org and the org from them.
        var members = await server.Json(await server.Send(HttpMethod.Get, $"/api/orgs/{orgId}/members", owner.Token));
        Assert.Equal(2, members.GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound,
            (await server.Send(HttpMethod.Get, $"/api/orgs/{orgId}", m2.Token)).StatusCode);
    }

    [Fact]
    public async Task Ownership_transfers_rather_than_multiplying()
    {
        var (orgId, owner, m1, _) = await Trio("Handover");

        Assert.True((await server.Send(HttpMethod.Post, $"/api/orgs/{orgId}/members/{m1.Id}/role", owner.Token,
            new { role = "owner" })).IsSuccessStatusCode);

        var members = await server.Json(await server.Send(HttpMethod.Get, $"/api/orgs/{orgId}/members", m1.Token));
        var roles = members.EnumerateArray().Select(m => m.GetProperty("role").GetString()).ToList();
        Assert.Single(roles, r => r == "owner");
        Assert.Contains("manager", roles);
    }

    [Fact]
    public async Task A_sole_owner_with_members_cannot_walk_away()
    {
        var (orgId, owner, _, _) = await Trio("Anchored");

        var response = await server.Send(HttpMethod.Post, $"/api/orgs/{orgId}/leave", owner.Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await server.Json(response);
        Assert.Contains("ownership", problem.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Forget_me_takes_the_account_its_memberships_and_its_keys()
    {
        var (orgId, owner, m1, _) = await Trio("Erasure");

        Assert.True((await server.Send(HttpMethod.Delete, "/api/me", m1.Token)).IsSuccessStatusCode);

        // The token died with the account...
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await server.Send(HttpMethod.Get, "/api/me", m1.Token)).StatusCode);

        // ...and the org no longer lists them.
        var members = await server.Json(await server.Send(HttpMethod.Get, $"/api/orgs/{orgId}/members", owner.Token));
        Assert.DoesNotContain(members.EnumerateArray(),
            m => m.GetProperty("displayName").GetString() == "Erasure-one");
    }

    [Fact]
    public async Task Forget_me_refuses_while_an_org_would_be_left_ownerless()
    {
        var (_, owner, _, _) = await Trio("Tethered");

        var response = await server.Send(HttpMethod.Delete, "/api/me", owner.Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await server.Json(response);
        Assert.Contains("Tethered", problem.GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_revoked_device_key_stops_working_immediately()
    {
        var person = server.Person("revoked");
        var tokens = await server.Json(await server.Send(HttpMethod.Get, "/api/me/tokens", person.Token));
        var id = tokens.EnumerateArray().First().GetProperty("id").GetString();

        Assert.True((await server.Send(HttpMethod.Delete, $"/api/me/tokens/{id}", person.Token)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await server.Send(HttpMethod.Get, "/api/me", person.Token)).StatusCode);
    }
}
