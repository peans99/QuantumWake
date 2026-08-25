using Quantumwake.OrgServer.Store;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quantumwake.OrgServer.Tests;

/// <summary>
/// The device-link flow: the only door a stranger on the internet can knock on,
/// and the one that mints long-lived keys.
/// </summary>
public class LinkFlowTests(OrgServerUnderTest server) : IClassFixture<OrgServerUnderTest>
{
    private async Task<(string Code, string Secret)> Started()
    {
        var response = await server.Client.PostAsJsonAsync("/api/link/start",
            new { clientName = "TEST-PC (Quantum Wake 0.9.0)" });
        var body = await server.Json(response);
        return (body.GetProperty("code").GetString()!, body.GetProperty("deviceSecret").GetString()!);
    }

    private async Task<JsonElement> Polled(string code, string secret)
    {
        var response = await server.Client.PostAsJsonAsync("/api/link/poll",
            new { code, deviceSecret = secret });
        return await server.Json(response);
    }

    [Fact]
    public async Task With_no_public_base_the_verify_url_is_the_address_the_app_reached()
    {
        // The fixture configures no PublicBaseUrl, which is the normal case for
        // a LAN server: nothing needs one because there is no OAuth redirect.
        // The binding is not a usable answer there - 127.0.0.1 is wrong for
        // everyone not sitting at the server, and inside a container it is the
        // port before the mapping.
        var response = await server.Client.PostAsJsonAsync("/api/link/start",
            new { clientName = "TEST-PC" });
        var verify = (await server.Json(response)).GetProperty("verifyUrl").GetString()!;

        Assert.StartsWith(server.Client.BaseAddress!.ToString().TrimEnd('/'), verify);
    }

    [Fact]
    public async Task An_unapproved_code_polls_as_pending_and_yields_nothing()
    {
        var (code, secret) = await Started();

        var poll = await Polled(code, secret);

        Assert.Equal("pending", poll.GetProperty("status").GetString());
        Assert.False(poll.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task The_code_alone_is_worthless_without_the_device_secret()
    {
        var (code, secret) = await Started();
        var account = server.Accounts.UpsertIdentity("discord", "snowflake-peeker", "peeker");
        server.Accounts.DecideLink(code, account.Id, approved: true, DateTimeOffset.UtcNow);

        // Someone who glimpsed the code in a browser races the client for the
        // token - and gets the same answer as a wrong guess.
        var stolen = await Polled(code, "not-the-secret");
        Assert.Equal("expired", stolen.GetProperty("status").GetString());
        Assert.False(stolen.TryGetProperty("token", out _));

        // The rightful holder still collects: the wrong-secret poll did not
        // burn the approval.
        var honest = await Polled(code, secret);
        Assert.Equal("approved", honest.GetProperty("status").GetString());
    }

    [Fact]
    public async Task An_approved_code_releases_its_token_exactly_once()
    {
        var (code, secret) = await Started();
        var account = server.Accounts.UpsertIdentity("discord", "snowflake-approver", "approver");
        server.Accounts.DecideLink(code, account.Id, approved: true, DateTimeOffset.UtcNow);

        var first = await Polled(code, secret);
        Assert.Equal("approved", first.GetProperty("status").GetString());
        var token = first.GetProperty("token").GetString();
        Assert.StartsWith("qwo_", token);
        Assert.Equal("approver", first.GetProperty("account").GetProperty("displayName").GetString());

        // The token works...
        var me = await server.Send(HttpMethod.Get, "/api/me", token);
        Assert.True(me.IsSuccessStatusCode);

        // ...and the code is spent: a second poll, same secret, gets nothing.
        var second = await Polled(code, secret);
        Assert.Equal("expired", second.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_denied_code_says_so_and_mints_nothing()
    {
        var (code, secret) = await Started();
        var account = server.Accounts.UpsertIdentity("discord", "snowflake-denier", "denier");
        server.Accounts.DecideLink(code, account.Id, approved: false, DateTimeOffset.UtcNow);

        var poll = await Polled(code, secret);

        Assert.Equal("denied", poll.GetProperty("status").GetString());
        Assert.False(poll.TryGetProperty("token", out _));
    }

    [Fact]
    public void A_code_past_its_ten_minutes_is_dead_even_when_approved()
    {
        var start = server.Accounts.StartLink("late", "http://x", DateTimeOffset.UtcNow.AddMinutes(-30));
        var account = server.Accounts.UpsertIdentity("discord", "snowflake-late", "late");
        server.Accounts.DecideLink(start.Code, account.Id, approved: true, DateTimeOffset.UtcNow.AddMinutes(-25));

        var poll = server.Accounts.PollLink(start.Code, start.DeviceSecret, DateTimeOffset.UtcNow);

        Assert.Equal("expired", poll.Status);
        Assert.Null(poll.Token);
    }

    [Fact]
    public void An_expired_code_cannot_be_approved_either()
    {
        var start = server.Accounts.StartLink("slow", "http://x", DateTimeOffset.UtcNow.AddMinutes(-30));
        var account = server.Accounts.UpsertIdentity("discord", "snowflake-slow", "slow");

        Assert.False(server.Accounts.DecideLink(start.Code, account.Id, approved: true, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Garbage_in_the_poll_is_the_same_expired_as_everything_else()
    {
        var poll = await Polled("XXXX-XXXX", "nope");
        Assert.Equal("expired", poll.GetProperty("status").GetString());
    }
}
