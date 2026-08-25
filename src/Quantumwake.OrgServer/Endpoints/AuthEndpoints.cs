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

        app.MapGet("/auth/login", (HttpContext context, OrgServerOptions options, string? @return) =>
        {
            if (options.OAuth is not { } oauth || options.PublicBaseUrl is not { Length: > 0 } baseUrl)
            {
                return Results.Text(
                    "Sign-in is not configured on this server. It needs a public base URL and "
                    + "OAuth credentials - see the deployment notes.", statusCode: 503);
            }

            // The state ties the callback to this browser; the return path
            // rides along so /link can resume. Only local paths are honoured -
            // an open redirect is a phishing kit.
            var state = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
            var destination = @return is ['/', ..] && !@return.StartsWith("//") ? @return : "/";

            context.Response.Cookies.Append(StateCookie, $"{state}|{destination}", new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10),
            });

            return Results.Redirect(oauth.AuthorizeUrl($"{baseUrl}/auth/callback", state));
        }).RequireRateLimiting("link-start");

        app.MapGet("/auth/callback", async (HttpContext context, OrgServerOptions options,
            AccountStore accounts, string? code, string? state, CancellationToken token) =>
        {
            var expected = context.Request.Cookies[StateCookie];
            context.Response.Cookies.Delete(StateCookie);

            if (options.OAuth is not { } oauth || options.PublicBaseUrl is not { Length: > 0 } baseUrl
                || code is not { Length: > 0 } || state is not { Length: > 0 }
                || expected?.Split('|', 2) is not [var wanted, var destination]
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
            // public base is the honest answer when there is one; the local
            // binding serves a self-hosted box being tried out on localhost.
            var baseUrl = options.PublicBaseUrl
                ?? $"http://{(options.Bind == "0.0.0.0" ? "127.0.0.1" : options.Bind)}:{options.Port}";

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
