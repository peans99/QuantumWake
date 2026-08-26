using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Quantumwake.OrgServer;
using Quantumwake.OrgServer.Auth;
using Quantumwake.OrgServer.Store;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quantumwake.OrgServer.Tests;

/// <summary>
/// A scripted sign-in provider: whoever the test says walked in, walked in.
/// </summary>
/// <remarks>
/// This is the seam that keeps fake sign-in out of the shipped binary: it only
/// exists in this assembly, so no environment variable on a deployment can
/// reach it. Most tests never touch it - they mint accounts and tokens through
/// the store and speak plain bearer tokens.
/// </remarks>
public sealed class FakeOAuth(string key = "fake", string name = "Fake") : IOAuthProvider
{
    public string Key => key;
    public string Name => name;

    /// <summary>Code → identity; the test scripts who each code proves.</summary>
    public Dictionary<string, OAuthIdentity> People { get; } = [];

    public string AuthorizeUrl(string redirectUri, string state) =>
        $"/fake-authorize?redirect={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(state)}";

    public Task<OAuthIdentity?> ExchangeAsync(string code, string redirectUri, CancellationToken token) =>
        Task.FromResult(People.TryGetValue(code, out var identity) ? identity : null);
}

/// <summary>
/// A real org server, hosted in-process on port 0 with a temp data folder.
/// </summary>
/// <remarks>
/// The shape is the main repo's <c>ServerUnderTest</c> minus its confessed
/// flaw: there is no process-wide path static here, so fixtures run in
/// parallel and no collection lock is needed. That absence is deliberate, not
/// an oversight.
/// </remarks>
public sealed class OrgServerUnderTest : IAsyncLifetime
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-orgsrv-{Guid.NewGuid():N}");

    private WebApplication? _app;

    public FakeOAuth OAuth { get; } = new();
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _app = OrgServerHost.Build(new OrgServerOptions
        {
            DataDirectory = _directory,
            Port = 0,
            OAuth = [OAuth],
            PublicBaseUrl = null,
            // "admin" signing in through Person() is the server admin by
            // configuration; the first-account fallback is exercised by its
            // own fixture with this list empty.
            Admins = ["snowflake-admin"],
        });

        await _app.StartAsync();
        Client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public AccountStore Accounts => _app!.Services.GetRequiredService<AccountStore>();
    public OrgStore Orgs => _app!.Services.GetRequiredService<OrgStore>();
    public OrgDb Db => _app!.Services.GetRequiredService<OrgDb>();

    /// <summary>A signed-up person with a device token, minted through the store.</summary>
    public (string AccountId, string Token) Person(string name, string? handle = null)
    {
        var account = Accounts.UpsertIdentity("discord", $"snowflake-{name}", name);
        if (handle is not null)
            Accounts.SetHandle(account.Id, handle);
        var (_, token) = Accounts.MintToken(account.Id, $"{name}'s machine");
        return (account.Id, token);
    }

    public HttpRequestMessage Request(HttpMethod method, string url, string? token = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    public async Task<HttpResponseMessage> Send(HttpMethod method, string url, string? token = null, object? body = null) =>
        await Client.SendAsync(Request(method, url, token, body));

    public async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        Client?.Dispose();
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
