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

        api.MapPost("/orgs", (HttpContext context, OrgActors actors, OrgStore orgs, AuditStore audit, OrgRegisterRequest request) =>
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
            audit.Write(actor.Account.Id, org.Id, "org.registered", org.Name,
                admin ? "active" : "pending");

            return Results.Ok(new OrgMembershipRow(org.Id, org.Name, org.Status, "owner", []));
        });

        api.MapPost("/orgs/join", (HttpContext context, OrgActors actors, OrgStore orgs, AuditStore audit, OrgJoinRequest request) =>
        {
            var (actor, refusal) = actors.Resolve(context);
            if (actor is null)
                return refusal!;

            var (org, problem, joined) = orgs.Join(request.Code, actor.Account.Id);
            if (joined)
                audit.Write(actor.Account.Id, org!.Id, "member.joined", actor.Account.Id);
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

        api.MapPost("/orgs/{orgId}/leave", (HttpContext context, OrgActors actors, OrgStore orgs, AuditStore audit, string orgId) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;

            var (actor, _) = actors.Resolve(context);
            var problem = orgs.Leave(orgId, actor!.Account.Id);
            if (problem is null)
            {
                var action = orgs.Get(orgId) is null ? "org.deleted.by_leave" : "member.left";
                audit.Write(actor.Account.Id, orgId, action, actor.Account.Id);
            }
            return problem is null ? Results.Ok() : Results.Json(new OrgProblem(problem), OrgWire.Json, statusCode: 409);
        });

        api.MapDelete("/orgs/{orgId}/members/{accountId}", (HttpContext context, OrgActors actors,
            OrgStore orgs, AuditStore audit, string orgId, string accountId) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;
            if (!member.Manages)
                return NotYours();

            var (actor, _) = actors.Resolve(context);
            if (!orgs.Kick(orgId, member, actor!.Account.Id, accountId))
                return Results.BadRequest(new OrgProblem("That member is not yours to remove."));
            audit.Write(actor.Account.Id, orgId, "member.removed", accountId);
            return Results.Ok();
        });

        api.MapPost("/orgs/{orgId}/members/{accountId}/role", (HttpContext context, OrgActors actors,
            OrgStore orgs, AuditStore audit, string orgId, string accountId, RoleRequest request) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;
            if (!member.Owns)
                return NotYours();

            var (actor, _) = actors.Resolve(context);
            if (!orgs.SetRole(orgId, actor!.Account.Id, accountId, request.Role ?? ""))
                return Results.BadRequest(new OrgProblem("That role change is not possible."));
            audit.Write(actor.Account.Id, orgId, "member.role", accountId, request.Role);
            return Results.Ok();
        });

        /* ---------- invites ---------- */

        api.MapPost("/orgs/{orgId}/invites", (HttpContext context, OrgActors actors, OrgStore orgs,
            AuditStore audit, string orgId, OrgInviteRequest request) =>
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
            var invite = orgs.CreateInvite(orgId, actor!.Account.Id,
                request.ExpiresInDays <= 0 ? 14 : request.ExpiresInDays, request.MaxUses);
            audit.Write(actor.Account.Id, orgId, "invite.created", invite.Code,
                $"expires {invite.ExpiresAt:O}; max uses {invite.MaxUses}");
            return Results.Ok(invite);
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
            AuditStore audit, string orgId, string code) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null)
                return refusal!;
            if (!member.Manages)
                return NotYours();

            if (!orgs.RevokeInvite(orgId, code))
                return Results.NotFound(new OrgProblem("No such invite."));
            var (actor, _) = actors.Resolve(context);
            audit.Write(actor!.Account.Id, orgId, "invite.revoked", code);
            return Results.Ok();
        });

        /* ---------- opt-in modules and blueprint snapshots ---------- */

        api.MapPost("/orgs/{orgId}/modules/blueprints", (HttpContext context, OrgActors actors,
            OrgStore orgs, AuditStore audit, string orgId, OrgModuleRequest request) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null) return refusal!;
            if (!member.Manages) return NotYours();
            var (actor, _) = actors.Resolve(context);
            orgs.SetModule(orgId, "blueprints", request.Enabled, actor!.Account.Id);
            audit.Write(actor.Account.Id, orgId, "module.blueprints", null,
                request.Enabled ? "enabled" : "disabled");
            return Results.Ok();
        });

        api.MapGet("/orgs/{orgId}/blueprints", (HttpContext context, OrgActors actors,
            OrgStore orgs, string orgId) =>
        {
            var (member, refusal) = ActiveModule(context, actors, orgs, orgId, "blueprints");
            return member is null ? refusal! : Results.Ok(orgs.Blueprints(orgId));
        });

        api.MapPut("/orgs/{orgId}/blueprints", (HttpContext context, OrgActors actors,
            OrgStore orgs, AuditStore audit, string orgId, OrgBlueprintUpload upload) =>
        {
            var (member, refusal) = ActiveModule(context, actors, orgs, orgId, "blueprints");
            if (member is null) return refusal!;
            if (upload.FormatVersion > OrgWire.FormatVersion)
                return Results.BadRequest(new OrgProblem("This blueprint share comes from a newer app version."));
            if (upload.Blueprints.Count > OrgLimits.MaxBlueprints)
                return Results.BadRequest(new OrgProblem($"A blueprint share is limited to {OrgLimits.MaxBlueprints} rows."));
            var (actor, _) = actors.Resolve(context);
            var receipt = orgs.ReplaceBlueprints(orgId, actor!.Account.Id, upload.Blueprints);
            audit.Write(actor.Account.Id, orgId, "blueprints.shared", actor.Account.Id,
                $"{receipt.Rows} rows");
            return Results.Ok(receipt);
        });

        api.MapDelete("/orgs/{orgId}/blueprints", (HttpContext context, OrgActors actors,
            OrgStore orgs, AuditStore audit, string orgId) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null) return refusal!;
            var (actor, _) = actors.Resolve(context);
            if (orgs.DeleteBlueprints(orgId, actor!.Account.Id))
                audit.Write(actor.Account.Id, orgId, "blueprints.removed", actor.Account.Id);
            return Results.Ok();
        });

        api.MapGet("/orgs/{orgId}/audit", (HttpContext context, OrgActors actors,
            OrgStore orgs, AuditStore audit, string orgId) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null) return refusal!;
            return member.Manages ? Results.Ok(audit.Recent(orgId)) : NotYours();
        });

        api.MapDelete("/orgs/{orgId}", (HttpContext context, OrgActors actors,
            OrgStore orgs, AuditStore audit, string orgId) =>
        {
            var (member, refusal) = Member(context, actors, orgs, orgId);
            if (member is null) return refusal!;
            if (!member.Owns) return NotYours();
            var (actor, _) = actors.Resolve(context);
            if (!orgs.Delete(orgId))
                return Results.NotFound();
            audit.Write(actor!.Account.Id, orgId, "org.deleted", member.Org.Name);
            return Results.Ok();
        });

        /* ---------- the server admin's desk ---------- */

        api.MapGet("/admin/orgs", (HttpContext context, OrgActors actors, OrgStore orgs, string? status) =>
        {
            var (actor, refusal) = Admin(context, actors);
            if (actor is null)
                return refusal!;

            return Results.Ok(orgs.ByStatus(status is "active" or "suspended" ? status : "pending"));
        });

        api.MapGet("/admin/audit", (HttpContext context, OrgActors actors, AuditStore audit) =>
        {
            var (actor, refusal) = Admin(context, actors);
            return actor is null ? refusal! : Results.Ok(audit.RecentAll());
        });

        api.MapPost("/admin/orgs/{orgId}/activate", (HttpContext context, OrgActors actors, OrgStore orgs, AuditStore audit, string orgId) =>
            Moderate(context, actors, orgs, audit, orgId, "active"));

        api.MapPost("/admin/orgs/{orgId}/suspend", (HttpContext context, OrgActors actors, OrgStore orgs, AuditStore audit, string orgId) =>
            Moderate(context, actors, orgs, audit, orgId, "suspended"));

        api.MapDelete("/admin/orgs/{orgId}", (HttpContext context, OrgActors actors, OrgStore orgs, AuditStore audit, string orgId) =>
        {
            var (actor, refusal) = Admin(context, actors);
            if (actor is null)
                return refusal!;

            if (!orgs.Delete(orgId))
                return Results.NotFound(new OrgProblem("No such org."));
            audit.Write(actor.Account.Id, orgId, "admin.org.deleted", orgId);
            return Results.Ok();
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

    private static (MemberContext? Member, IResult? Refusal) ActiveModule(HttpContext context,
        OrgActors actors, OrgStore orgs, string orgId, string module)
    {
        var (member, refusal) = Member(context, actors, orgs, orgId);
        if (member is null) return (null, refusal);
        if (member.Org.Status != "active")
            return (null, Results.Json(new OrgProblem("This org is not active."), OrgWire.Json, statusCode: 409));
        if (!orgs.Modules(orgId).Contains(module, StringComparer.Ordinal))
            return (null, Results.Json(new OrgProblem("This module is off for the org."), OrgWire.Json, statusCode: 409));
        return (member, null);
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
        AuditStore audit, string orgId, string status)
    {
        var (actor, refusal) = Admin(context, actors);
        if (actor is null)
            return refusal!;

        if (!orgs.SetStatus(orgId, status, actor.Account.Id))
            return Results.NotFound(new OrgProblem("No such org."));
        audit.Write(actor.Account.Id, orgId, $"admin.org.{status}", orgId);
        return Results.Ok();
    }

    private static IResult NotYours() => Results.NotFound(new OrgProblem("There is nothing here."));
}

public sealed record RoleRequest(string? Role);
