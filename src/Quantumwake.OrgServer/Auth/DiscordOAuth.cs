namespace Quantumwake.OrgServer.Auth;

/// <summary>
/// Discord's OAuth2, hand-rolled.
/// </summary>
/// <remarks>
/// Scope is <c>identify</c> and nothing else - the snowflake and a display
/// name are all this server wants to know, and deliberately not the email,
/// because a member is a game handle, not an inbox. The other two providers
/// keep the same bargain.
/// </remarks>
public sealed class DiscordOAuth(string clientId, string clientSecret) : IOAuthProvider
{
    public string Key => "discord";
    public string Name => "Discord";

    public string AuthorizeUrl(string redirectUri, string state) =>
        "https://discord.com/oauth2/authorize?response_type=code&scope=identify"
        + $"&client_id={Uri.EscapeDataString(clientId)}"
        + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
        + $"&state={Uri.EscapeDataString(state)}";

    public Task<OAuthIdentity?> ExchangeAsync(string code, string redirectUri, CancellationToken token) =>
        OAuthExchange.RunAsync(Key,
            "https://discord.com/api/oauth2/token",
            "https://discord.com/api/users/@me",
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            },
            // global_name is the display name Discord shows; username is the
            // older unique one. Either serves; a person can rename later.
            me => me.Text("id") is { Length: > 0 } id
                ? (id, me.Text("global_name") ?? me.Text("username") ?? "someone")
                : null,
            token);
}
