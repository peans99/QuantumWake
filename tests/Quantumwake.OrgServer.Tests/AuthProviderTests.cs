using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Quantumwake.OrgServer.Auth;
using Quantumwake.OrgServer.Store;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Quantumwake.OrgServer.Tests;

/// <summary>
/// Three doors instead of one: which provider answered has to survive the
/// round trip, because the identity it proves is half of who a person is.
/// </summary>
public class AuthProviderTests : IAsyncLifetime
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-orgauth-{Guid.NewGuid():N}");

    private readonly FakeOAuth _discord = new("discord", "Discord");
    private readonly FakeOAuth _google = new("google", "Google");

    private WebApplication _app = null!;

    public async Task InitializeAsync()
    {
        _app = OrgServerHost.Build(new OrgServerOptions
        {
            DataDirectory = _directory,
            Port = 0,
            PublicBaseUrl = "https://org.example.net",
            OAuth = [_discord, _google],
            Admins = [],
        });
        await _app.StartAsync();
    }

    private Uri Base => new(_app.Urls.First());

    /// <summary>A browser: keeps cookies, and does not chase redirects itself.</summary>
    private HttpClient Browser() => new(new HttpClientHandler
    {
        CookieContainer = new CookieContainer(),
        AllowAutoRedirect = false,
    })
    { BaseAddress = Base };

    [Fact]
    public async Task The_providers_endpoint_lists_what_this_deployment_configured()
    {
        using var client = new HttpClient { BaseAddress = Base };
        var body = await client.GetStringAsync("/api/auth/providers");

        Assert.Contains("\"lanMode\":false", body);
        Assert.Contains("\"key\":\"discord\"", body);
        Assert.Contains("\"key\":\"google\"", body);
        Assert.Contains("\"name\":\"Google\"", body);
    }

    [Fact]
    public async Task With_several_configured_an_unnamed_provider_is_asked_for_rather_than_guessed()
    {
        using var client = Browser();
        var response = await client.GetAsync("/auth/login?return=/account");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("discord", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_provider_that_is_not_configured_is_refused_rather_than_substituted()
    {
        using var client = Browser();
        var response = await client.GetAsync("/auth/login?provider=facebook&return=/");

        // Falling back to another provider would sign somebody in somewhere
        // they did not choose.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_lone_provider_needs_no_choosing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"qw-orgauth1-{Guid.NewGuid():N}");
        var app = OrgServerHost.Build(new OrgServerOptions
        {
            DataDirectory = directory,
            Port = 0,
            PublicBaseUrl = "https://org.example.net",
            OAuth = [new FakeOAuth()],
        });
        try
        {
            await app.StartAsync();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { BaseAddress = new Uri(app.Urls.First()) };

            var response = await client.GetAsync("/auth/login?return=/");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/fake-authorize", response.Headers.Location!.ToString());
        }
        finally
        {
            await app.DisposeAsync();
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task The_provider_that_answered_is_the_one_the_identity_is_filed_under()
    {
        _google.People["good-code"] = new OAuthIdentity("google", "google-subject-1", "Someone");

        using var client = Browser();

        var start = await client.GetAsync("/auth/login?provider=google&return=/account");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);

        // The state is the server's; read it back off the redirect rather than
        // inventing one, which is the whole point of the check.
        var state = StateOf(start.Headers.Location!);

        var finish = await client.GetAsync($"/auth/callback?code=good-code&state={state}");

        Assert.Equal(HttpStatusCode.Redirect, finish.StatusCode);
        Assert.Equal("/account", finish.Headers.Location!.ToString());

        var me = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains("Someone", await me.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_code_from_one_provider_cannot_be_spent_at_another()
    {
        _google.People["googles-code"] = new OAuthIdentity("google", "google-subject-2", "Someone");

        using var client = Browser();

        var start = await client.GetAsync("/auth/login?provider=discord&return=/");
        var state = StateOf(start.Headers.Location!);

        // The cookie says discord, so discord is asked - and it has never
        // heard of this code.
        var finish = await client.GetAsync($"/auth/callback?code=googles-code&state={state}");

        Assert.Equal(HttpStatusCode.BadGateway, finish.StatusCode);
    }

    [Fact]
    public void An_admin_can_be_named_by_subject_alone_or_by_provider_and_subject()
    {
        var accounts = _app.Services.GetRequiredService<AccountStore>();

        var bare = accounts.UpsertIdentity("google", "bare-subject", "bare");
        var qualified = accounts.UpsertIdentity("microsoft", "qualified-subject", "qualified");
        var neither = accounts.UpsertIdentity("discord", "someone-else", "neither");

        // A fresh store, because the configured list is constructor state.
        var store = new AccountStore(
            _app.Services.GetRequiredService<OrgDb>(),
            ["bare-subject", "microsoft:qualified-subject"],
            _app.Services.GetRequiredService<ILogger<AccountStore>>());

        Assert.True(store.IsServerAdmin(bare.Id));
        Assert.True(store.IsServerAdmin(qualified.Id));
        Assert.False(store.IsServerAdmin(neither.Id));
    }

    /// <summary>
    /// The state the server minted, read back off its own redirect. Works on
    /// the string rather than Uri.Query because a provider's authorize URL is
    /// allowed to be relative, and the fake one is.
    /// </summary>
    private static string StateOf(Uri redirect) => redirect.OriginalString
        .Split('?', 2)[1].Split('&')
        .Select(pair => pair.Split('=', 2))
        .First(pair => pair[0] == "state")[1];

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }
}
