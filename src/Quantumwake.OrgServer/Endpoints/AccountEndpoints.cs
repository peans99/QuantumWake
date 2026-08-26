using Quantumwake.OrgServer.Auth;
using Quantumwake.OrgServer.Store;
using Quantumwake.OrgShared;

namespace Quantumwake.OrgServer.Endpoints;

/// <summary>The signed-in person's own account: who they are, what holds their tokens, and the way out.</summary>
public static class AccountEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/me").RequireRateLimiting("api");

        api.MapGet("", (HttpContext context, OrgActors actors, OrgStore orgs) =>
        {
            var (actor, refusal) = actors.Resolve(context);
            if (actor is null)
                return refusal!;

            return Results.Ok(new OrgMeResponse(
                Account(actor, actors), orgs.MyOrgs(actor.Account.Id)));
        });

        api.MapPost("/handle", (HttpContext context, OrgActors actors, AccountStore accounts, AuditStore audit,
            HandleRequest request) =>
        {
            var (actor, refusal) = actors.Resolve(context);
            if (actor is null)
                return refusal!;

            accounts.SetHandle(actor.Account.Id, request.Handle);
            audit.Write(actor.Account.Id, null, "account.handle");
            return Results.Ok();
        });

        api.MapGet("/tokens", (HttpContext context, OrgActors actors, AccountStore accounts) =>
        {
            var (actor, refusal) = actors.Resolve(context);
            if (actor is null)
                return refusal!;

            return Results.Ok(accounts.Tokens(actor.Account.Id).Select(t => new
            {
                t.Id,
                prefix = t.DisplayPrefix,
                t.Name,
                t.CreatedAt,
                t.LastUsedAt,
                revoked = t.RevokedAt is not null,
            }));
        });

        api.MapDelete("/tokens/{id}", (HttpContext context, OrgActors actors, AccountStore accounts, AuditStore audit, string id) =>
        {
            var (actor, refusal) = actors.Resolve(context);
            if (actor is null)
                return refusal!;

            if (!accounts.RevokeToken(actor.Account.Id, id))
                return Results.NotFound(new OrgProblem("No such token."));
            audit.Write(actor.Account.Id, null, "token.revoked", id);
            return Results.Ok();
        });

        // Forget me. Refused while orgs would be left ownerless - the sentence
        // says exactly what to do about it, because a refusal without a way
        // forward is a support request.
        api.MapDelete("", (HttpContext context, OrgActors actors, AccountStore accounts, OrgStore orgs, AuditStore audit) =>
        {
            var (actor, refusal) = actors.Resolve(context);
            if (actor is null)
                return refusal!;

            var owned = orgs.OwnedWithOthers(actor.Account.Id);
            if (owned.Count > 0)
            {
                return Results.Json(new OrgProblem(
                    $"You still own {string.Join(", ", owned)} and other people are in there. "
                    + "Hand ownership over or delete the org, then try again."),
                    OrgWire.Json, statusCode: 409);
            }

            foreach (var org in orgs.DeleteSoleOwned(actor.Account.Id))
                audit.Write(actor.Account.Id, org.Id, "org.deleted.by_account", org.Name);
            audit.Write(actor.Account.Id, null, "account.deleted", actor.Account.Id);
            accounts.Forget(actor.Account.Id);
            return Results.Ok();
        });
    }

    internal static OrgAccount Account(Actor actor, OrgActors actors) => new(
        actor.Account.Id, actor.Account.Handle, actor.Account.HandleVerified,
        actor.Account.DisplayName, actors.IsServerAdmin(actor));
}

public sealed record HandleRequest(string? Handle);
