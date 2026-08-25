using System.Net;

namespace Quantumwake.OrgServer.Tests;

/// <summary>
/// The gate between registering an org and having one: a server admin says so.
/// </summary>
public class ApprovalTests(OrgServerUnderTest server) : IClassFixture<OrgServerUnderTest>
{
    [Fact]
    public async Task An_ordinary_registration_waits_for_the_admin()
    {
        var (_, alice) = server.Person("alice");

        var org = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs", alice, new { name = "Waiting Room" }));

        Assert.Equal("pending", org.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_admins_own_org_is_active_from_birth()
    {
        var (_, admin) = server.Person("admin");

        var org = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs", admin, new { name = "Home Fleet" }));

        Assert.Equal("active", org.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_pending_org_cannot_hand_out_invites()
    {
        var (_, bob) = server.Person("bob");
        var org = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs", bob, new { name = "Eager" }));

        var response = await server.Send(HttpMethod.Post,
            $"/api/orgs/{org.GetProperty("id").GetString()}/invites", bob, new { expiresInDays = 7, maxUses = 0 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Approval_opens_the_doors_end_to_end()
    {
        var (_, admin) = server.Person("admin");
        var (_, carol) = server.Person("carol");
        var (_, dave) = server.Person("dave");

        var org = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs", carol, new { name = "Soon" }));
        var orgId = org.GetProperty("id").GetString();

        // The admin sees it queued...
        var queue = await server.Json(await server.Send(HttpMethod.Get, "/api/admin/orgs", admin));
        Assert.Contains(queue.EnumerateArray(), o => o.GetProperty("id").GetString() == orgId);

        // ...approves it...
        Assert.True((await server.Send(HttpMethod.Post, $"/api/admin/orgs/{orgId}/activate", admin)).IsSuccessStatusCode);

        // ...and now an invite can be minted and used.
        var invite = await server.Json(await server.Send(HttpMethod.Post,
            $"/api/orgs/{orgId}/invites", carol, new { expiresInDays = 7, maxUses = 0 }));
        var joined = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs/join", dave,
            new { code = invite.GetProperty("code").GetString() }));

        Assert.Equal(orgId, joined.GetProperty("id").GetString());
        Assert.Equal("member", joined.GetProperty("role").GetString());
    }

    [Fact]
    public async Task An_invite_into_a_pending_org_does_not_exist_yet()
    {
        // Codes can only be minted once active, so the joinable failure is a
        // stale or made-up code - and it must not confirm anything.
        var (_, eve) = server.Person("eve");

        var response = await server.Send(HttpMethod.Post, "/api/orgs/join", eve, new { code = "AAAAA-AAAAA" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_the_admin_can_moderate()
    {
        var (_, frank) = server.Person("frank");
        var org = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs", frank, new { name = "Selfmade" }));

        var response = await server.Send(HttpMethod.Post,
            $"/api/admin/orgs/{org.GetProperty("id").GetString()}/activate", frank);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
