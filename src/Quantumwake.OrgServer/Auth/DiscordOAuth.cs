using System.Text.Json;

namespace Quantumwake.OrgServer.Auth;

/// <summary>Who somebody proved they are, in provider terms.</summary>
public sealed record OAuthIdentity(string Provider, string Subject, string DisplayName);

/// <summary>
/// How people sign in. An interface rather than a dev-mode flag: tests inject
/// a scripted provider, and nothing that skips real sign-in can ship in the
/// binary or be switched on with an environment variable.
/// </summary>
public interface IOAuthProvider
{
    string Name { get; }

    /// <summary>The provider page to send the browser to.</summary>
    string AuthorizeUrl(string redirectUri, string state);

    /// <summary>Turns the callback code into a proven identity, or null.</summary>
    Task<OAuthIdentity?> ExchangeAsync(string code, string redirectUri, CancellationToken token);
}

/// <summary>
/// Discord's OAuth2, hand-rolled.
/// </summary>
/// <remarks>
/// The whole exchange is one form POST and one GET, which is not worth an auth
/// library's dependency surface. Scope is <c>identify</c> and nothing else -
/// the snowflake and a display name are all this server wants to know, and
/// deliberately not the email, because a member is a game handle, not an inbox.
/// </remarks>
public sealed class DiscordOAuth(string clientId, string clientSecret) : IOAuthProvider
{
    public string Name => "Discord";

    public string AuthorizeUrl(string redirectUri, string state) =>
        "https://discord.com/oauth2/authorize?response_type=code&scope=identify"
        + $"&client_id={Uri.EscapeDataString(clientId)}"
        + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
        + $"&state={Uri.EscapeDataString(state)}";

    public async Task<OAuthIdentity?> ExchangeAsync(string code, string redirectUri, CancellationToken token)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QuantumWakeOrg");

        using var exchange = await client.PostAsync("https://discord.com/api/oauth2/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            }), token);

        if (!exchange.IsSuccessStatusCode)
            return null;

        var granted = await exchange.Content.ReadAsStringAsync(token);
        using var grant = JsonDocument.Parse(granted);
        if (!grant.RootElement.TryGetProperty("access_token", out var access))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
        request.Headers.Authorization = new("Bearer", access.GetString());
        using var response = await client.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
            return null;

        using var me = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = me.RootElement;
        var subject = root.GetProperty("id").GetString();
        if (subject is not { Length: > 0 })
            return null;

        // global_name is the display name Discord shows; username is the
        // older unique one. Either serves; a person can rename later.
        var display = root.TryGetProperty("global_name", out var g) && g.ValueKind == JsonValueKind.String
            ? g.GetString()! : root.GetProperty("username").GetString() ?? "someone";

        return new OAuthIdentity("discord", subject, display);
    }
}
