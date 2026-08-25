using Microsoft.AspNetCore.Builder;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quantumwake.OrgServer.Tests;

/// <summary>
/// The mode with no sign-in: everyone who can reach the port is the same
/// person, and every page has to say so.
/// </summary>
/// <remarks>
/// Hosts its own server, because LAN mode is a whole-server posture rather
/// than a request-level one - and configures a provider as well, to prove the
/// mode wins outright rather than sitting alongside a sign-in button.
/// </remarks>
public class LanModeTests : IAsyncLifetime
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-orglan-{Guid.NewGuid():N}");

    private WebApplication _app = null!;

    public async Task InitializeAsync()
    {
        _app = OrgServerHost.Build(new OrgServerOptions
        {
            DataDirectory = _directory,
            Port = 0,
            LanMode = true,
            PublicBaseUrl = "https://org.example.net",
            OAuth = [new FakeOAuth()],
            Admins = [],
        });
        await _app.StartAsync();
    }

    private HttpClient Client() => new() { BaseAddress = new Uri(_app.Urls.First()) };

    [Fact]
    public async Task Everyone_who_can_reach_it_is_the_same_account_and_that_account_is_an_admin()
    {
        using var first = Client();
        using var second = Client();

        var one = await first.GetFromJsonAsync<JsonElement>("/api/me");
        var two = await second.GetFromJsonAsync<JsonElement>("/api/me");

        var id = one.GetProperty("account").GetProperty("id").GetString();

        Assert.Equal(id, two.GetProperty("account").GetProperty("id").GetString());
        Assert.True(one.GetProperty("account").GetProperty("serverAdmin").GetBoolean());
    }

    [Fact]
    public async Task Every_page_carries_the_warning()
    {
        using var client = Client();

        foreach (var page in new[] { "/", "/link", "/account", "/admin" })
        {
            var html = await client.GetStringAsync(page);
            Assert.Contains("lan-banner", html);
            Assert.Contains("LAN mode", html);
        }
    }

    [Fact]
    public async Task A_server_that_is_not_in_lan_mode_says_nothing_of_the_sort()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"qw-orgnolan-{Guid.NewGuid():N}");
        var app = OrgServerHost.Build(new OrgServerOptions { DataDirectory = directory, Port = 0 });
        try
        {
            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

            Assert.DoesNotContain("lan-banner", await client.GetStringAsync("/"));
        }
        finally
        {
            await app.DisposeAsync();
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Sign_in_is_refused_and_no_provider_is_offered_even_though_one_is_configured()
    {
        using var client = Client();

        var login = await client.GetAsync("/auth/login?return=/");
        Assert.Equal(HttpStatusCode.Conflict, login.StatusCode);

        var providers = await client.GetStringAsync("/api/auth/providers");
        Assert.Contains("\"lanMode\":true", providers);
        Assert.Contains("\"providers\":[]", providers);
    }

    [Fact]
    public async Task A_mutation_still_has_to_come_from_this_server_s_own_pages()
    {
        using var client = Client();

        // There is no credential left to steal, but a page on another origin
        // can still make a browser POST here - and on a network where everyone
        // is an admin that is the last cross-site door worth shutting.
        var bare = await client.PostAsJsonAsync("/api/me/handle", new { handle = "drive-by" });
        Assert.Equal(HttpStatusCode.Forbidden, bare.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/me/handle")
        {
            Content = JsonContent.Create(new { handle = "nekron" }),
        };
        request.Headers.Add("X-Qw-Org", "1");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }
}
