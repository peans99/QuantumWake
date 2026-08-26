using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Quantumwake.OrgServer.Auth;
using Quantumwake.OrgServer.Store;
using Quantumwake.OrgShared;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Quantumwake.OrgServer.Endpoints;

/// <summary>
/// Signing in with a browser, and linking a desktop app without one.
/// </summary>
/// <remarks>
/// The link flow exists because the desktop app lives on localhost and cannot
/// receive an OAuth redirect; this server owns the public URL and the provider
/// registration. The app asks for a code, the person approves it in a signed-in
/// browser, and the app polls its way to a long-lived token. The code is the
/// visible half and the device secret is the quiet half - a token is released
/// only to the holder of both.
/// </remarks>
public static class AuthEndpoints
{
    private const string StateCookie = "qw-org-state";

    public static void Map(WebApplication app)
    {
        /* ---------- browser sign-in ---------- */

        // What the pages need before they can draw a sign-in button, and the
        // one honest way for a browser to discover it is a LAN server. Open
        // deliberately: it states configuration, never who is signed in.
        app.MapGet("/api/auth/providers", (OrgServerOptions options) =>
        {
            // LAN mode offers none, whatever is configured: two ways in would
            // mean two identities for one person.
            var offered = options.LanMode ? Array.Empty<IOAuthProvider>() : options.OAuth;

            return Results.Ok(new
            {
                lanMode = options.LanMode,
                providers = offered.Select(p => new { key = p.Key, name = p.Name }).ToArray(),
            });
        }).RequireRateLimiting("api");

        app.MapGet("/auth/login", (HttpContext context, OrgServerOptions options,
            string? provider, string? @return) =>
        {
            if (options.LanMode)
            {
                return Results.Text(
                    "This server runs in LAN mode: everyone who can reach it is already signed in, "
                    + "and there is nothing to sign in to.", statusCode: 409);
            }

            if (options.PublicBaseUrl is not { Length: > 0 } baseUrl)
            {
                return Results.Text(
                    "Sign-in is not configured on this server: it has no public base URL, so the "
                    + "provider has no redirect to come back to - see the deployment notes.",
                    statusCode: 503);
            }

            // A named provider is that one or nothing - falling back to
            // another when the name is wrong would sign somebody in somewhere
            // they did not choose. Unnamed, a lone provider needs no choosing,
            // which keeps every existing /auth/login link working now that
            // there can be three.
            var oauth = provider is { Length: > 0 }
                ? options.Provider(provider)
                : options.OAuth.Count == 1 ? options.OAuth[0] : null;
            if (oauth is null)
            {
                return Results.Text(options.OAuth.Count == 0
                    ? "Sign-in is not configured on this server: no provider has credentials - "
                      + "see the deployment notes."
                    : "Choose a sign-in provider: " + string.Join(", ", options.OAuth.Select(p => p.Key)),
                    statusCode: 503);
            }

            // The state ties the callback to this browser; the provider and the
            // return path ride along so the callback knows who answered and
            // /link can resume. Only local paths are honoured - an open
            // redirect is a phishing kit. The destination goes last because it
            // is the only part that can itself contain a separator.
            var state = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
            var destination = @return is ['/', ..] && !@return.StartsWith("//") ? @return : "/";

            context.Response.Cookies.Append(StateCookie, $"{state}|{oauth.Key}|{destination}", new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                MaxAge = TimeSpan.FromMinutes(10),
            });

            return Results.Redirect(oauth.AuthorizeUrl($"{baseUrl}/auth/callback", state));
        }).RequireRateLimiting("link-start");

        app.MapGet("/auth/callback", async (HttpContext context, OrgServerOptions options,
            AccountStore accounts, string? code, string? state, CancellationToken token) =>
        {
            var expected = context.Request.Cookies[StateCookie];
            context.Response.Cookies.Delete(StateCookie);

            if (options.LanMode || options.PublicBaseUrl is not { Length: > 0 } baseUrl
                || code is not { Length: > 0 } || state is not { Length: > 0 }
                || expected?.Split('|', 3) is not [var wanted, var providerKey, var destination]
                || options.Provider(providerKey) is not { } oauth
                || !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(wanted), System.Text.Encoding.UTF8.GetBytes(state)))
            {
                return Results.Text("That sign-in attempt cannot be finished. Start again.", statusCode: 400);
            }

            var identity = await oauth.ExchangeAsync(code, $"{baseUrl}/auth/callback", token);
            if (identity is null)
                return Results.Text($"{oauth.Name} did not confirm the sign-in. Try again.", statusCode: 502);

            var account = accounts.UpsertIdentity(identity.Provider, identity.Subject, identity.DisplayName);

            await context.SignInAsync("cookie", new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("account", account.Id)], CookieAuthenticationDefaults.AuthenticationScheme)));

            return Results.Redirect(destination is ['/', ..] ? destination : "/");
        }).RequireRateLimiting("link-start");

        app.MapPost("/auth/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync("cookie");
            return Results.Ok();
        });

        /* ---------- linking a desktop app ---------- */

        app.MapPost("/api/link/start", (HttpContext context, OrgServerOptions options,
            AccountStore accounts, OrgLinkStartRequest request) =>
        {
            // The verify URL must be reachable from the person's browser. The
            // public base is the honest answer when there is one; failing that,
            // the Host this very request arrived on - because the app reached
            // the server at the address its owner typed into Settings, which is
            // by definition an address that works from their machine.
            //
            // The binding is NOT a usable fallback: it is 127.0.0.1 for anyone
            // not sitting at the server, and inside a container it is the port
            // before the mapping, so it was wrong for every LAN server and
            // every container that had no public base configured.
            var baseUrl = options.PublicBaseUrl
                ?? (context.Request.Host.HasValue
                    ? $"{context.Request.Scheme}://{context.Request.Host}"
                    : $"http://{(options.Bind == "0.0.0.0" ? "127.0.0.1" : options.Bind)}:{options.Port}");

            return Results.Ok(accounts.StartLink(request.ClientName, baseUrl, DateTimeOffset.UtcNow));
        }).RequireRateLimiting("link-start");

        app.MapPost("/api/link/poll", (AccountStore accounts, OrgLinkPollRequest request) =>
            Results.Ok(accounts.PollLink(request.Code, request.DeviceSecret, DateTimeOffset.UtcNow)))
            .RequireRateLimiting("link-poll");

        // The approve page's questions and buttons. Browser-only: the token
        // holder has nothing to approve, and an unauthenticated caller being
        // able to read client names would make the code an oracle.
        app.MapGet("/api/link/{code}", (HttpContext context, OrgActors actors, AccountStore accounts, string code) =>
        {
            var (actor, refusal) = actors.ResolveBrowser(context);
            if (actor is null)
                return refusal!;

            var link = accounts.GetLink(code);
            if (link is null || link.ExpiresAt <= DateTimeOffset.UtcNow)
                return Results.NotFound(new OrgProblem("That link code has expired. Start again from the app."));

            return Results.Ok(new
            {
                clientName = link.ClientName,
                status = link.Status,
                you = actor.Account.DisplayName,
            });
        });

        app.MapPost("/api/link/{code}/approve", (HttpContext context, OrgActors actors, AccountStore accounts, string code) =>
            Decide(context, actors, accounts, code, approved: true));

        app.MapPost("/api/link/{code}/deny", (HttpContext context, OrgActors actors, AccountStore accounts, string code) =>
            Decide(context, actors, accounts, code, approved: false));
    }

    private static IResult Decide(HttpContext context, OrgActors actors, AccountStore accounts,
        string code, bool approved)
    {
        var (actor, refusal) = actors.ResolveBrowser(context);
        if (actor is null)
            return refusal!;

        return accounts.DecideLink(code, actor.Account.Id, approved, DateTimeOffset.UtcNow)
            ? Results.Ok()
            : Results.NotFound(new OrgProblem("That link code is not waiting for a decision."));
    }
}
