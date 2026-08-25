using System.Net;

namespace Quantumwake.OrgServer.Tests;

/// <summary>
/// The wall between orgs, which matters more than any feature.
/// </summary>
/// <remarks>
/// A member of org A must get 404 - not 403 - from everything under org B,
/// because a 403 confirms the org exists and org names are not a stranger's
/// to enumerate.
/// </remarks>
public class TenancyTests(OrgServerUnderTest server) : IClassFixture<OrgServerUnderTest>
{
    [Fact]
    public async Task A_member_of_one_org_sees_nothing_of_another()
    {
        var (_, admin) = server.Person("admin");
        var (_, alice) = server.Person("alice");
        var (_, bob) = server.Person("bob");

        var a = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs", alice, new { name = "Alpha" }));
        var b = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs", bob, new { name = "Beta" }));
        var orgB = b.GetProperty("id").GetString();

        foreach (var url in new[]
        {
            $"/api/orgs/{orgB}", $"/api/orgs/{orgB}/members", $"/api/orgs/{orgB}/invites",
        })
        {
            var response = await server.Send(HttpMethod.Get, url, alice);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // Mutations too: alice cannot act inside Beta.
        Assert.Equal(HttpStatusCode.NotFound,
            (await server.Send(HttpMethod.Post, $"/api/orgs/{orgB}/leave", alice)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await server.Send(HttpMethod.Post, $"/api/orgs/{orgB}/invites", alice,
                new { expiresInDays = 7, maxUses = 0 })).StatusCode);

        // And the same requests from a member work, so the 404s above are the
        // wall and not a broken route.
        Assert.True((await server.Send(HttpMethod.Get, $"/api/orgs/{orgB}", bob)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_guessed_org_id_and_a_real_one_look_identical_to_a_stranger()
    {
        var (_, mallory) = server.Person("mallory");
        var (_, carol) = server.Person("carol");
        var real = await server.Json(await server.Send(HttpMethod.Post, "/api/orgs", carol, new { name = "Real" }));

        var guessed = await server.Send(HttpMethod.Get, "/api/orgs/000000000000", mallory);
        var existing = await server.Send(HttpMethod.Get, $"/api/orgs/{real.GetProperty("id").GetString()}", mallory);

        Assert.Equal(HttpStatusCode.NotFound, guessed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, existing.StatusCode);
        Assert.Equal(await guessed.Content.ReadAsStringAsync(), await existing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Nothing_answers_without_a_credential()
    {
        foreach (var url in new[] { "/api/me", "/api/orgs", "/api/orgs/anything", "/api/admin/orgs" })
        {
            var response = await server.Send(HttpMethod.Get, url);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task The_admin_desk_is_invisible_to_everyone_else()
    {
        var (_, dave) = server.Person("dave");

        var response = await server.Send(HttpMethod.Get, "/api/admin/orgs", dave);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
