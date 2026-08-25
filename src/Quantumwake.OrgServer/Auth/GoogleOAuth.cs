namespace Quantumwake.OrgServer.Auth;

/// <summary>
/// Google, through its OpenID Connect endpoints.
/// </summary>
/// <remarks>
/// Scope is <c>openid profile</c>: the subject and a display name, and not
/// <c>email</c>, for the reason <see cref="DiscordOAuth"/> gives. The subject
/// is stable for the life of the OAuth client, so re-registering the app in
/// the Google console mints new subjects and every account signs in as
/// somebody new - which is why the credentials belong in configuration a
/// deployment keeps, not in a console somebody tidies.
/// </remarks>
public sealed class GoogleOAuth(string clientId, string clientSecret) : IOAuthProvider
{
    public string Key => "google";
    public string Name => "Google";

    public string AuthorizeUrl(string redirectUri, string state) =>
        "https://accounts.google.com/o/oauth2/v2/auth?response_type=code"
        + $"&scope={Uri.EscapeDataString("openid profile")}"
        + $"&client_id={Uri.EscapeDataString(clientId)}"
        + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
        + $"&state={Uri.EscapeDataString(state)}";

    public Task<OAuthIdentity?> ExchangeAsync(string code, string redirectUri, CancellationToken token) =>
        OAuthExchange.RunAsync(Key,
            "https://oauth2.googleapis.com/token",
            "https://openidconnect.googleapis.com/v1/userinfo",
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            },
            me => me.Text("sub") is { Length: > 0 } sub
                ? (sub, me.Text("name") ?? me.Text("given_name") ?? "someone")
                : null,
            token);
}
