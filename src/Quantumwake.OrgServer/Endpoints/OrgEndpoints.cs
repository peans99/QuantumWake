using Quantumwake.OrgServer.Auth;
using Quantumwake.OrgServer.Store;
using Quantumwake.OrgShared;

namespace Quantumwake.OrgServer.Endpoints;

/// <summary>
/// Orgs: registering one, being approved, being in one, running one.
/// </summary>
/// <remarks>
/// A non-member gets 404 from everything under an org, never 403 - a wrong
/// guess must not confirm the org exists. The org id comes from the route and
/// the membership from the credential; nothing about tenancy is ever read from
/// a request body.
/// </remarks>
public static class OrgEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api").RequireRateLimiting("api");

        /* ---------- registration and joining ---------- */

        api.MapPost("/orgs", (HttpContext context, OrgActors actors, OrgStore orgs, OrgRegisterRequest request) =>
        {
            var (actor, refusal) = actors.Resolve(context);
            if (actor is null)
                return refusal!;

            if (request.Name is not { Length: > 0 })
                return Results.BadRequest(new OrgProblem("An org needs a name."));

            // An admin creating an org is the approval - there is nobody
            // above them to ask.
            var admin = actors.IsServerAdmin(actor);
            var org = orgs.Register(request.Name, request.Note, actor.Account.Id, activeImmediately: admin);

            return Results.Ok(new OrgMembershipRow(org.Id, org.Name, org.Status, "owner", []));
        });

        api.MapPost("/orgs/join", (HttpContext context, OrgActors actors, OrgStore orgs, OrgJoinRequest request) =>
        {
            var (actor, refusal) = actors.Resolve(context);
            if (actor is null)
                return refusal!;

            var (org, problem) = orgs.Join(request.Code, actor.Account.Id);
            return problem is not null
                ? Results.BadRequest(new OrgProblem(problem))
                : Results.Ok(new OrgMembershipRow(org!.Id, org.Name, org.Status, "member", orgs.Modules(org.Id)));
        });

        api.MapGet("/orgs", (HttpContext context, OrgActors actors, OrgStore orgs) =>
        {
            var (actor, refusal) = actors.Resolve(context);
            if (actor is null)
                return refusal!;

            return Results.Ok(orgs.MyOrgs(actor.Account.Id));
        });

        /* ---------- inside an org ---------- */

        api.MapGet("/orgs/{orgId}", (HttpContext context, OrgActors actors, OrgStore orgs, string orgId) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;

            return Results.Ok(new
            {
                member.Org.Id,
                member.Org.Name,
                member.Org.Note,
                member.Org.Status,
                member.Role,
                modules = orgs.Modules(orgId),
                requestExpiryDays = member.Org.RequestExpiryDays,
                members = orgs.MemberCount(orgId),
            });
        });

        api.MapGet("/orgs/{orgId}/members", (HttpContext context, OrgActors actors, OrgStore orgs, string orgId) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;

            return Results.Ok(orgs.Members(orgId));
        });

        api.MapPost("/orgs/{orgId}/leave", (HttpContext context, OrgActors actors, OrgStore orgs, string orgId) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;

            var (actor, _) = actors.Resolve(context);
            var problem = orgs.Leave(orgId, actor!.Account.Id);
            return problem is null ? Results.Ok() : Results.Json(new OrgProblem(problem), OrgWire.Json, statusCode: 409);
        });

        api.MapDelete("/orgs/{orgId}/members/{accountId}", (HttpContext context, OrgActors actors,
            OrgStore orgs, string orgId, string accountId) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;
            if (!member.Manages)
                return NotYours();

            var (actor, _) = actors.Resolve(context);
            return orgs.Kick(orgId, member, actor!.Account.Id, accountId)
                ? Results.Ok()
                : Results.BadRequest(new OrgProblem("That member is not yours to remove."));
        });

        api.MapPost("/orgs/{orgId}/members/{accountId}/role", (HttpContext context, OrgActors actors,
            OrgStore orgs, string orgId, string accountId, RoleRequest request) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;
            if (!member.Owns)
                return NotYours();

            var (actor, _) = actors.Resolve(context);
            return orgs.SetRole(orgId, actor!.Account.Id, accountId, request.Role ?? "")
                ? Results.Ok()
                : Results.BadRequest(new OrgProblem("That role change is not possible."));
        });

        /* ---------- invites ---------- */

        api.MapPost("/orgs/{orgId}/invites", (HttpContext context, OrgActors actors, OrgStore orgs,
            string orgId, OrgInviteRequest request) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;
            if (!member.Manages)
                return NotYours();

            // A pending org has no business inviting people into a space the
            // admin has not approved.
            if (member.Org.Status != "active")
                return Results.Json(new OrgProblem("This org is waiting for approval; invites come after."),
                    OrgWire.Json, statusCode: 409);

            var (actor, _) = actors.Resolve(context);
            return Results.Ok(orgs.CreateInvite(orgId, actor!.Account.Id,
                request.ExpiresInDays <= 0 ? 14 : request.ExpiresInDays, request.MaxUses));
        });

        api.MapGet("/orgs/{orgId}/invites", (HttpContext context, OrgActors actors, OrgStore orgs, string orgId) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;
            if (!member.Manages)
                return NotYours();

            return Results.Ok(orgs.Invites(orgId));
        });

        api.MapDelete("/orgs/{orgId}/invites/{code}", (HttpContext context, OrgActors actors, OrgStore orgs,
            string orgId, string code) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;
            if (!member.Manages)
                return NotYours();

            return orgs.RevokeInvite(orgId, code)
                ? Results.Ok()
                : Results.NotFound(new OrgProblem("No such invite."));
        });

        /* ---------- the server admin's desk ---------- */

        api.MapGet("/admin/orgs", (HttpContext context, OrgActors actors, OrgStore orgs, string? status) =>
        {
            var (actor, refusal) = Admin(context, actors);
            if (actor is null)
                return refusal!;

            return Results.Ok(orgs.ByStatus(status is "active" or "suspended" ? status : "pending"));
        });

        api.MapPost("/admin/orgs/{orgId}/activate", (HttpContext context, OrgActors actors, OrgStore orgs, string orgId) =>
            Moderate(context, actors, orgs, orgId, "active"));

        api.MapPost("/admin/orgs/{orgId}/suspend", (HttpContext context, OrgActors actors, OrgStore orgs, string orgId) =>
            Moderate(context, actors, orgs, orgId, "suspended"));

        api.MapDelete("/admin/orgs/{orgId}", (HttpContext context, OrgActors actors, OrgStore orgs, string orgId) =>
        {
            var (actor, refusal) = Admin(context, actors);
            if (actor is null)
                return refusal!;

            return orgs.Delete(orgId) ? Results.Ok() : Results.NotFound(new OrgProblem("No such org."));
        });
    }

    /// <summary>The wall every org endpoint stands behind.</summary>
    private static (MemberContext? Member, IResult? Refusal) Member(HttpContext context,
        OrgActors actors, OrgStore orgs, string orgId)
    {
        var (actor, refusal) = actors.Resolve(context);
        if (actor is null)
            return (null, refusal);

        var member = orgs.Resolve(orgId, actor.Account.Id);
        return member is null ? (null, NotYours()) : (member, null);
    }

    private static (Actor? Actor, IResult? Refusal) Admin(HttpContext context, OrgActors actors)
    {
        var (actor, refusal) = actors.Resolve(context);
        if (actor is null)
            return (null, refusal);

        // 404, not 403: the admin surface should look like nothing at all to
        // anyone who is not the admin.
        return actors.IsServerAdmin(actor) ? (actor, null) : (null, NotYours());
    }

    private static IResult Moderate(HttpContext context, OrgActors actors, OrgStore orgs,
        string orgId, string status)
    {
        var (actor, refusal) = Admin(context, actors);
        if (actor is null)
            return refusal!;

        return orgs.SetStatus(orgId, status, actor.Account.Id)
            ? Results.Ok()
            : Results.NotFound(new OrgProblem("No such org."));
    }

    private static IResult NotYours() => Results.NotFound(new OrgProblem("There is nothing here."));
}

public sealed record RoleRequest(string? Role);
