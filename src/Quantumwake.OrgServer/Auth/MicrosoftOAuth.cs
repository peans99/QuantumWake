namespace Quantumwake.OrgServer.Auth;

/// <summary>
/// Microsoft identities, personal and work, through the v2.0 endpoints.
/// </summary>
/// <remarks>
/// <para>
/// The tenant defaults to <c>common</c>, which accepts both a personal
/// Microsoft account and a work or school one - the right default for an org
/// whose members are not colleagues. A deployment that is a company can name
/// its own tenant id and stop being a door for the rest of the world.
/// </para>
/// <para>
/// Unlike the other two, the v2.0 token endpoint wants the scope repeated in
/// the exchange as well as the authorize call, and rejects the request without
/// it.
/// </para>
/// </remarks>
public sealed class MicrosoftOAuth(string clientId, string clientSecret, string tenant = "common")
    : IOAuthProvider
{
    private const string Scope = "openid profile";

    public string Key => "microsoft";
    public string Name => "Microsoft";

    public string AuthorizeUrl(string redirectUri, string state) =>
        $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/authorize"
        + "?response_type=code"
        + $"&scope={Uri.EscapeDataString(Scope)}"
        + $"&client_id={Uri.EscapeDataString(clientId)}"
        + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
        + $"&state={Uri.EscapeDataString(state)}";

    public Task<OAuthIdentity?> ExchangeAsync(string code, string redirectUri, CancellationToken token) =>
        OAuthExchange.RunAsync(Key,
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/token",
            "https://graph.microsoft.com/oidc/userinfo",
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "authorization_code",
                ["scope"] = Scope,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            },
            me => me.Text("sub") is { Length: > 0 } sub
                ? (sub, me.Text("name") ?? me.Text("givenname") ?? "someone")
                : null,
            token);
}
