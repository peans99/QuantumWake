using System.Net;

namespace Quantumwake.Tests;

/// <summary>
/// The requests a browser makes on another website's behalf.
/// </summary>
/// <remarks>
/// The API trusts its callers by address, and a browser lends 127.0.0.1 to any
/// page it is showing: a page the pilot visits can POST here blind, and a DNS
/// name re-resolved to loopback lets it read as well. These tests are those two
/// requests, made for real against the running server - a rebound Host and a
/// cross-site Origin - plus the neighbours that must keep working: the
/// dashboard's own posts, and clients that send no Origin at all, which is
/// every test in this suite.
/// </remarks>
[Collection("server")]
public class OriginGuardTests : IClassFixture<ServerUnderTest>
{
    private readonly ServerUnderTest _server;

    public OriginGuardTests(ServerUnderTest server) => _server = server;

    /// <summary>
    /// DNS rebinding arrives as an honest browser request whose Host still
    /// names the attacker's site - the one header the trick cannot rewrite.
    /// Reads are refused too: reading is the point of rebinding.
    /// </summary>
    [Fact]
    public async Task A_request_whose_host_names_another_site_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/setup");
        request.Headers.Host = "attacker.example";

        using var response = await _server.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// A cross-site POST is fired blind, but its side effects land; the browser
    /// names the page it was sent for, and that name is enough to refuse it.
    /// </summary>
    [Fact]
    public async Task A_write_declaring_a_foreign_origin_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/setup/done");
        request.Headers.Add("Origin", "https://attacker.example");

        using var response = await _server.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("another website", await ServerUnderTest.Refusal(response));
    }

    /// <summary>
    /// "null" is what sandboxed iframes and file: pages declare. It belongs to
    /// nobody, so it is foreign, not absent.
    /// </summary>
    [Fact]
    public async Task A_write_declaring_a_null_origin_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/setup/done");
        request.Headers.Add("Origin", "null");

        using var response = await _server.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// The dashboard's own fetches declare the loopback origin they run on,
    /// whichever loopback name the pilot opened.
    /// </summary>
    [Theory]
    [InlineData("http://127.0.0.1:31337")]
    [InlineData("http://localhost:31337")]
    public async Task The_dashboards_own_writes_pass(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/setup/done");
        request.Headers.Add("Origin", origin);

        using var response = await _server.Client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode,
            $"POST with Origin {origin} answered {(int)response.StatusCode}");
    }

    /// <summary>
    /// curl, PowerShell and the app's own process send no Origin, and anything
    /// already running code on this machine gains nothing from the API - so
    /// absence passes. Every other test in this suite posts this way.
    /// </summary>
    [Fact]
    public async Task A_write_with_no_origin_passes()
    {
        using var response = await _server.Post("/api/setup/done");

        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// A read with a foreign Origin is the browser asking, not telling: with no
    /// CORS headers served, the page never sees the answer. Refusing would
    /// only break tools that set Origin honestly.
    /// </summary>
    [Fact]
    public async Task A_read_declaring_a_foreign_origin_still_answers()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/setup");
        request.Headers.Add("Origin", "https://attacker.example");

        using var response = await _server.Client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode);
    }
}
