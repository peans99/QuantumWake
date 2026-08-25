using System.Text.Json;

namespace Quantumwake.OrgServer.Auth;

/// <summary>Who somebody proved they are, in provider terms.</summary>
public sealed record OAuthIdentity(string Provider, string Subject, string DisplayName);

/// <summary>
/// How people sign in. An interface rather than a dev-mode flag: tests inject
/// a scripted provider, and nothing that skips real sign-in can ship in the
/// binary or be switched on with an environment variable.
/// </summary>
/// <remarks>
/// A person is a provider identity, so a second provider is new rows in
/// <c>identities</c> rather than a migration - and the same human signing in
/// with Google having previously used Discord is two accounts, honestly, since
/// nothing this server is allowed to ask for could prove they are the same
/// person. Linking them is an account-page feature, not a guess.
/// </remarks>
public interface IOAuthProvider
{
    /// <summary>
    /// The stable key: the <c>?provider=</c> segment and the value written to
    /// the identities table. Lowercase, and never changed once shipped - it is
    /// half of the primary key that identifies a person.
    /// </summary>
    string Key { get; }

    /// <summary>What the sign-in button says.</summary>
    string Name { get; }

    /// <summary>The provider page to send the browser to.</summary>
    string AuthorizeUrl(string redirectUri, string state);

    /// <summary>Turns the callback code into a proven identity, or null.</summary>
    Task<OAuthIdentity?> ExchangeAsync(string code, string redirectUri, CancellationToken token);
}

/// <summary>
/// The half of OAuth2 that every provider here shares: swap the code for an
/// access token, then ask the provider who it belongs to.
/// </summary>
/// <remarks>
/// Three providers doing this by hand is still not worth an auth library's
/// dependency surface, but three copies of it would be. What differs between
/// them is two URLs, the form, and how the answer spells a display name -
/// which is exactly what this takes as arguments.
/// </remarks>
internal static class OAuthExchange
{
    public static async Task<OAuthIdentity?> RunAsync(
        string provider,
        string tokenUrl,
        string userInfoUrl,
        Dictionary<string, string> form,
        Func<JsonElement, (string Subject, string Display)?> read,
        CancellationToken token)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QuantumWakeOrg");

        using var exchange = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(form), token);
        if (!exchange.IsSuccessStatusCode)
            return null;

        using var grant = JsonDocument.Parse(await exchange.Content.ReadAsStringAsync(token));
        if (!grant.RootElement.TryGetProperty("access_token", out var access))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
        request.Headers.Authorization = new("Bearer", access.GetString());
        using var response = await client.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
            return null;

        using var me = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if (read(me.RootElement) is not { } who || who.Subject is not { Length: > 0 })
            return null;

        return new OAuthIdentity(provider, who.Subject, who.Display);
    }

    /// <summary>A string property, or null when it is absent or not one.</summary>
    public static string? Text(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}
