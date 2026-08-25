using Quantumwake.OrgServer.Store;
using Quantumwake.OrgShared;
using System.Security.Claims;

namespace Quantumwake.OrgServer.Auth;

/// <summary>The proven caller of one request.</summary>
public sealed record Actor(AccountRow Account, bool ViaCookie);

/// <summary>
/// Turns a request into an account, or into the refusal that says why not.
/// </summary>
/// <remarks>
/// <para>
/// Two credentials exist and every API endpoint accepts either: a bearer token
/// (the desktop app's) and a browser cookie (the link-approve and admin
/// pages'). Resolution is a helper called at the top of each endpoint rather
/// than an authentication framework, for the same reason LanGuard is a static
/// method: a rule that fits in a sentence should be readable in one place.
/// </para>
/// <para>
/// The cross-site check: a cookie-authenticated mutation must carry the
/// <c>X-Qw-Org</c> header. A hostile page on another origin can make the
/// browser send the cookie, but it cannot attach a custom header without a
/// CORS preflight, and this server grants no CORS - so the header is the
/// proof the request came from this server's own pages. Bearer tokens need no
/// such proof; no other origin holds one.
/// </para>
/// </remarks>
public sealed class OrgActors(AccountStore accounts)
{
    public const string CsrfHeader = "X-Qw-Org";

    /// <summary>The caller, or the response that turns them away.</summary>
    public (Actor? Actor, IResult? Refusal) Resolve(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var account = accounts.ResolveToken(header["Bearer ".Length..].Trim());
            return account is null
                ? (null, Results.Json(new OrgProblem("That token is not valid any more. Link this app again."),
                    OrgWire.Json, statusCode: 401))
                : (new Actor(account, ViaCookie: false), null);
        }

        var id = context.User.FindFirstValue("account");
        if (id is null || accounts.Get(id) is not { } fromCookie)
            return (null, Results.Json(new OrgProblem("Sign in first."), OrgWire.Json, statusCode: 401));

        if (!HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method)
            && !context.Request.Headers.ContainsKey(CsrfHeader))
        {
            return (null, Results.Json(new OrgProblem("This request did not come from the server's own pages."),
                OrgWire.Json, statusCode: 403));
        }

        return (new Actor(fromCookie, ViaCookie: true), null);
    }

    /// <summary>Same, but only the browser will do - token holders are turned away.</summary>
    public (Actor? Actor, IResult? Refusal) ResolveBrowser(HttpContext context)
    {
        var (actor, refusal) = Resolve(context);
        if (actor is { ViaCookie: false })
            return (null, Results.Json(new OrgProblem("This is a browser-only door."), OrgWire.Json, statusCode: 403));
        return (actor, refusal);
    }

    public bool IsServerAdmin(Actor actor) => accounts.IsServerAdmin(actor.Account.Id);
}
