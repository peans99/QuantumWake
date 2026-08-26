using Microsoft.Data.Sqlite;
using Quantumwake.OrgShared;
using System.Security.Cryptography;

namespace Quantumwake.OrgServer.Store;

public sealed record OrgRow(
    string Id, string Name, string? Note, string Status, int RequestExpiryDays,
    string CreatedBy, DateTimeOffset CreatedAt);

/// <summary>What being in an org means: one org, one account, one role.</summary>
public sealed record MemberContext(OrgRow Org, string Role)
{
    public bool Manages => Role is "owner" or "manager";
    public bool Owns => Role is "owner";
}

/// <summary>
/// Orgs, memberships, invites and module switches.
/// </summary>
/// <remarks>
/// <para>
/// Every method that reads or writes org data takes the org id as its first
/// parameter and scopes its SQL with it. There is no method that crosses orgs
/// - the same construction-not-vigilance rule as <c>LanGuard</c>: tenancy is
/// not a filter someone remembers to add, it is the only shape a query can
/// take.
/// </para>
/// <para>
/// One owner per org, always. Ownership transfers rather than accumulating,
/// because "who can delete this org" should have exactly one answer.
/// </para>
/// </remarks>
public sealed class OrgStore(OrgDb db)
{
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /* ---------- creation and the approval gate ---------- */

    /// <summary>
    /// Registers an org. Pending until a server admin activates it - unless an
    /// admin is doing the creating, in which case there is nobody left to ask.
    /// </summary>
    public OrgRow Register(string name, string? note, string createdBy, bool activeImmediately)
    {
        var now = DateTimeOffset.UtcNow;
        var row = new OrgRow(
            Guid.NewGuid().ToString("N")[..12],
            Sanitise.Clean(name, "an org"),
            Sanitise.CleanOptional(note),
            activeImmediately ? "active" : "pending",
            14, createdBy, now);

        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();

        AccountStore.Run(connection, transaction,
            "INSERT INTO orgs (id, name, note, status, request_expiry_days, created_by, created_at, activated_at, activated_by) "
            + "VALUES ($id, $name, $note, $status, 14, $by, $now, $activatedAt, $activatedBy)",
            ("$id", row.Id), ("$name", row.Name), ("$note", (object?)row.Note ?? DBNull.Value),
            ("$status", row.Status), ("$by", createdBy), ("$now", now.ToString("O")),
            ("$activatedAt", activeImmediately ? now.ToString("O") : DBNull.Value),
            ("$activatedBy", activeImmediately ? createdBy : DBNull.Value));

        AccountStore.Run(connection, transaction,
            "INSERT INTO memberships (org_id, account_id, role, joined_at) VALUES ($org, $account, 'owner', $now)",
            ("$org", row.Id), ("$account", createdBy), ("$now", now.ToString("O")));

        transaction.Commit();
        return row;
    }

    public OrgRow? Get(string orgId)
    {
        using var connection = db.Open();
        return One(connection,
            "SELECT id, name, note, status, request_expiry_days, created_by, created_at FROM orgs WHERE id=$id",
            [("$id", orgId)], ReadOrg);
    }

    /// <summary>
    /// The membership check every org endpoint starts with. Null means "not
    /// yours to know about", which the caller answers with 404 - an org's
    /// existence is members-only too.
    /// </summary>
    public MemberContext? Resolve(string orgId, string accountId)
    {
        using var connection = db.Open();
        return One(connection,
            "SELECT o.id, o.name, o.note, o.status, o.request_expiry_days, o.created_by, o.created_at, m.role "
            + "FROM orgs o JOIN memberships m ON m.org_id = o.id "
            + "WHERE o.id=$org AND m.account_id=$account",
            [("$org", orgId), ("$account", accountId)],
            r => new MemberContext(ReadOrg(r), r.GetString(7)));
    }

    public IReadOnlyList<OrgMembershipRow> MyOrgs(string accountId)
    {
        using var connection = db.Open();
        var rows = AccountStore.Many(connection,
            "SELECT o.id, o.name, o.status, m.role FROM orgs o "
            + "JOIN memberships m ON m.org_id = o.id WHERE m.account_id=$account ORDER BY m.joined_at",
            [("$account", accountId)],
            r => (Id: r.GetString(0), Name: r.GetString(1), Status: r.GetString(2), Role: r.GetString(3)));

        return [.. rows.Select(r => new OrgMembershipRow(r.Id, r.Name, r.Status, r.Role, Modules(r.Id)))];
    }

    /* ---------- members and roles ---------- */

    public IReadOnlyList<OrgMemberRow> Members(string orgId)
    {
        using var connection = db.Open();
        return AccountStore.Many(connection,
            "SELECT a.id, a.sc_handle, a.handle_verified, a.display_name, m.role, m.joined_at, "
            + "EXISTS (SELECT 1 FROM api_tokens t WHERE t.account_id=a.id AND t.revoked_at IS NULL) "
            + "FROM memberships m JOIN accounts a ON a.id = m.account_id "
            + "WHERE m.org_id=$org ORDER BY m.joined_at",
            [("$org", orgId)],
            r => new OrgMemberRow(
                r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetInt64(2) != 0,
                r.GetString(3), r.GetString(4), DateTimeOffset.Parse(r.GetString(5)),
                r.GetInt64(6) != 0));
    }

    public int MemberCount(string orgId)
    {
        using var connection = db.Open();
        return Convert.ToInt32(AccountStore.Scalar(connection, null,
            "SELECT COUNT(*) FROM memberships WHERE org_id=$org", ("$org", orgId)));
    }

    /// <summary>
    /// Leaving takes your org data with you. Refused for a sole owner with
    /// other members still in the room - somebody has to hold the keys, so
    /// ownership is transferred first or the org is deleted.
    /// </summary>
    public string? Leave(string orgId, string accountId)
    {
        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();

        var role = AccountStore.One(connection, transaction,
            "SELECT role FROM memberships WHERE org_id=$org AND account_id=$account",
            [("$org", orgId), ("$account", accountId)], r => r.GetString(0));
        if (role is null)
            return null;

        if (role == "owner")
        {
            var others = Convert.ToInt64(AccountStore.Scalar(connection, transaction,
                "SELECT COUNT(*) FROM memberships WHERE org_id=$org AND account_id<>$account",
                ("$org", orgId), ("$account", accountId)));
            if (others > 0)
                return "You own this org. Hand ownership to someone else first, or delete the org.";

            // Last one out: the org goes too, rather than lingering unowned.
            AccountStore.Run(connection, transaction, "DELETE FROM orgs WHERE id=$org", ("$org", orgId));
            transaction.Commit();
            return null;
        }

        RemoveMemberData(connection, transaction, orgId, accountId);
        transaction.Commit();
        return null;
    }

    /// <summary>
    /// A manager can remove members; only the owner can remove a manager.
    /// Nobody kicks the owner, and nobody kicks themselves - that is leaving.
    /// </summary>
    public bool Kick(string orgId, MemberContext actor, string actorId, string targetAccountId)
    {
        if (targetAccountId == actorId)
            return false;

        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();

        var target = AccountStore.One(connection, transaction,
            "SELECT role FROM memberships WHERE org_id=$org AND account_id=$account",
            [("$org", orgId), ("$account", targetAccountId)], r => r.GetString(0));

        var allowed = target switch
        {
            "member" => actor.Manages,
            "manager" => actor.Owns,
            _ => false,
        };
        if (!allowed)
            return false;

        RemoveMemberData(connection, transaction, orgId, targetAccountId);
        transaction.Commit();
        return true;
    }

    /// <summary>
    /// Owner-only. Making somebody else the owner demotes the current one to
    /// manager in the same transaction - one owner, always.
    /// </summary>
    public bool SetRole(string orgId, string actorId, string targetAccountId, string role)
    {
        if (role is not ("manager" or "member" or "owner") || targetAccountId == actorId)
            return false;

        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();

        var target = AccountStore.One(connection, transaction,
            "SELECT role FROM memberships WHERE org_id=$org AND account_id=$account",
            [("$org", orgId), ("$account", targetAccountId)], r => r.GetString(0));
        if (target is null or "owner")
            return false;

        if (role == "owner")
        {
            AccountStore.Run(connection, transaction,
                "UPDATE memberships SET role='manager' WHERE org_id=$org AND account_id=$account",
                ("$org", orgId), ("$account", actorId));
        }

        AccountStore.Run(connection, transaction,
            "UPDATE memberships SET role=$role WHERE org_id=$org AND account_id=$account",
            ("$role", role), ("$org", orgId), ("$account", targetAccountId));

        transaction.Commit();
        return true;
    }

    /// <summary>Orgs this account owns that still hold other members.</summary>
    public IReadOnlyList<string> OwnedWithOthers(string accountId)
    {
        using var connection = db.Open();
        return AccountStore.Many(connection,
            "SELECT o.name FROM orgs o JOIN memberships m ON m.org_id = o.id "
            + "WHERE m.account_id=$account AND m.role='owner' AND EXISTS "
            + "(SELECT 1 FROM memberships x WHERE x.org_id = o.id AND x.account_id<>$account)",
            [("$account", accountId)], r => r.GetString(0));
    }

    /// <summary>Delete orgs this account owns alone so forget-me cannot strand them.</summary>
    public IReadOnlyList<(string Id, string Name)> DeleteSoleOwned(string accountId)
    {
        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();
        var deleted = AccountStore.Many(connection, transaction,
            "SELECT o.id, o.name FROM orgs o JOIN memberships m ON m.org_id=o.id "
            + "WHERE m.account_id=$account AND m.role='owner' AND NOT EXISTS "
            + "(SELECT 1 FROM memberships x WHERE x.org_id=m.org_id AND x.account_id<>$account)",
            [("$account", accountId)], r => (r.GetString(0), r.GetString(1)));
        AccountStore.Run(connection, transaction,
            "DELETE FROM orgs WHERE id IN (SELECT m.org_id FROM memberships m "
            + "WHERE m.account_id=$account AND m.role='owner' AND NOT EXISTS "
            + "(SELECT 1 FROM memberships x WHERE x.org_id=m.org_id AND x.account_id<>$account))",
            ("$account", accountId));
        transaction.Commit();
        return deleted;
    }

    /* ---------- invites ---------- */

    public OrgInviteRow CreateInvite(string orgId, string createdBy, int expiresInDays, int maxUses)
    {
        var now = DateTimeOffset.UtcNow;
        var days = Math.Clamp(expiresInDays, 1, OrgLimits.MaxInviteDays);
        var code = InviteCode();

        using var connection = db.Open();
        AccountStore.Run(connection, null,
            "INSERT INTO invites (code, org_id, created_by, created_at, expires_at, max_uses) "
            + "VALUES ($code, $org, $by, $now, $expires, $max)",
            ("$code", code), ("$org", orgId), ("$by", createdBy),
            ("$now", now.ToString("O")), ("$expires", now.AddDays(days).ToString("O")),
            ("$max", Math.Max(0, maxUses)));

        return new OrgInviteRow(code, now.AddDays(days), Math.Max(0, maxUses), 0, false);
    }

    public IReadOnlyList<OrgInviteRow> Invites(string orgId)
    {
        using var connection = db.Open();
        return AccountStore.Many(connection,
            "SELECT code, expires_at, max_uses, uses, revoked_at FROM invites "
            + "WHERE org_id=$org ORDER BY created_at DESC",
            [("$org", orgId)],
            r => new OrgInviteRow(r.GetString(0), DateTimeOffset.Parse(r.GetString(1)),
                (int)r.GetInt64(2), (int)r.GetInt64(3), !r.IsDBNull(4)));
    }

    public bool RevokeInvite(string orgId, string code)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE invites SET revoked_at=$now WHERE code=$code AND org_id=$org AND revoked_at IS NULL";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$code", code);
        command.Parameters.AddWithValue("$org", orgId);
        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>Joins by invite code. The sentence explains any refusal.</summary>
    public (OrgRow? Org, string? Problem, bool Joined) Join(string? code, string accountId)
    {
        if (code is not { Length: > 0 })
            return (null, "An invite code is needed to join an org.", false);

        var now = DateTimeOffset.UtcNow;

        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();

        var invite = AccountStore.One(connection, transaction,
            "SELECT org_id, expires_at, max_uses, uses, revoked_at FROM invites WHERE code=$code",
            [("$code", code.Trim().ToUpperInvariant())],
            r => (OrgId: r.GetString(0), Expires: DateTimeOffset.Parse(r.GetString(1)),
                  Max: (int)r.GetInt64(2), Uses: (int)r.GetInt64(3), Revoked: !r.IsDBNull(4)));

        if (invite == default || invite.Revoked || invite.Expires <= now
            || (invite.Max > 0 && invite.Uses >= invite.Max))
            return (null, "That invite code is not valid any more. Ask for a fresh one.", false);

        var org = One(connection,
            "SELECT id, name, note, status, request_expiry_days, created_by, created_at FROM orgs WHERE id=$id",
            [("$id", invite.OrgId)], ReadOrg, transaction);

        if (org is null || org.Status != "active")
            return (null, "That org is waiting for the server admin's approval. Try again once it is approved.", false);

        var already = Convert.ToInt64(AccountStore.Scalar(connection, transaction,
            "SELECT COUNT(*) FROM memberships WHERE org_id=$org AND account_id=$account",
            ("$org", org.Id), ("$account", accountId)));
        if (already > 0)
            return (org, null, false);

        AccountStore.Run(connection, transaction,
            "INSERT INTO memberships (org_id, account_id, role, joined_at, invited_by) "
            + "VALUES ($org, $account, 'member', $now, $code)",
            ("$org", org.Id), ("$account", accountId), ("$now", now.ToString("O")), ("$code", code));
        AccountStore.Run(connection, transaction,
            "UPDATE invites SET uses = uses + 1 WHERE code=$code", ("$code", code));

        transaction.Commit();
        return (org, null, true);
    }

    /* ---------- modules ---------- */

    public IReadOnlyList<string> Modules(string orgId)
    {
        using var connection = db.Open();
        return AccountStore.Many(connection,
            "SELECT module FROM org_modules WHERE org_id=$org AND enabled=1 ORDER BY module",
            [("$org", orgId)], r => r.GetString(0));
    }

    public bool SetModule(string orgId, string module, bool enabled, string accountId)
    {
        if (module != "blueprints")
            return false;

        using var connection = db.Open();
        AccountStore.Run(connection, null,
            "INSERT INTO org_modules (org_id, module, enabled, updated_at, updated_by) "
            + "VALUES ($org, $module, $enabled, $at, $by) "
            + "ON CONFLICT(org_id, module) DO UPDATE SET enabled=$enabled, updated_at=$at, updated_by=$by",
            ("$org", orgId), ("$module", module), ("$enabled", enabled ? 1 : 0),
            ("$at", DateTimeOffset.UtcNow.ToString("O")), ("$by", accountId));
        return true;
    }

    public OrgBlueprintReceipt ReplaceBlueprints(string orgId, string accountId,
        IReadOnlyList<OrgBlueprintUploadRow> rows)
    {
        var sharedAt = DateTimeOffset.UtcNow;
        var clean = rows
            .Select(r => new OrgBlueprintUploadRow(r.ObservedAt,
                Sanitise.Clean(r.Name, "an unnamed blueprint", OrgLimits.BlueprintName)))
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(r => r.ObservedAt).First())
            .ToArray();
        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();

        AccountStore.Run(connection, transaction,
            "DELETE FROM shared_blueprints WHERE org_id=$org AND account_id=$account",
            ("$org", orgId), ("$account", accountId));

        foreach (var row in clean)
        {
            AccountStore.Run(connection, transaction,
                "INSERT INTO shared_blueprints (org_id, account_id, name, observed_at, shared_at) "
                + "VALUES ($org, $account, $name, $observed, $shared)",
                ("$org", orgId), ("$account", accountId), ("$name", row.Name),
                ("$observed", row.ObservedAt.ToUniversalTime().ToString("O")),
                ("$shared", sharedAt.ToString("O")));
        }

        transaction.Commit();
        return new OrgBlueprintReceipt(clean.Length, sharedAt);
    }

    public IReadOnlyList<OrgBlueprintRow> Blueprints(string orgId)
    {
        using var connection = db.Open();
        return AccountStore.Many(connection,
            "SELECT b.account_id, a.sc_handle, a.handle_verified, a.display_name, "
            + "b.observed_at, b.name, b.shared_at FROM shared_blueprints b "
            + "JOIN accounts a ON a.id=b.account_id WHERE b.org_id=$org "
            + "ORDER BY b.name, a.display_name",
            [("$org", orgId)],
            r => new OrgBlueprintRow(r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1),
                r.GetInt64(2) != 0, r.GetString(3), DateTimeOffset.Parse(r.GetString(4)),
                r.GetString(5), DateTimeOffset.Parse(r.GetString(6))));
    }

    public bool DeleteBlueprints(string orgId, string accountId)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM shared_blueprints WHERE org_id=$org AND account_id=$account";
        command.Parameters.AddWithValue("$org", orgId);
        command.Parameters.AddWithValue("$account", accountId);
        return command.ExecuteNonQuery() > 0;
    }

    /* ---------- the server admin's view ---------- */

    public IReadOnlyList<OrgSummary> ByStatus(string status)
    {
        using var connection = db.Open();
        var rows = AccountStore.Many(connection,
            "SELECT o.id, o.name, o.note, o.status, o.created_at, a.display_name "
            + "FROM orgs o JOIN accounts a ON a.id = o.created_by WHERE o.status=$status ORDER BY o.created_at",
            [("$status", status)],
            r => (Id: r.GetString(0), Name: r.GetString(1),
                  Note: r.IsDBNull(2) ? null : r.GetString(2), Status: r.GetString(3),
                  Created: DateTimeOffset.Parse(r.GetString(4)), By: r.GetString(5)));

        return [.. rows.Select(r => new OrgSummary(r.Id, r.Name, r.Note, r.Status, r.Created, r.By, MemberCount(r.Id)))];
    }

    public bool SetStatus(string orgId, string status, string byAccount)
    {
        if (status is not ("active" or "suspended"))
            return false;

        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = status == "active"
            ? "UPDATE orgs SET status='active', activated_at=$now, activated_by=$by WHERE id=$id"
            : "UPDATE orgs SET status='suspended' WHERE id=$id";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$by", byAccount);
        command.Parameters.AddWithValue("$id", orgId);
        return command.ExecuteNonQuery() > 0;
    }

    public bool Delete(string orgId)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM orgs WHERE id=$id";
        command.Parameters.AddWithValue("$id", orgId);
        return command.ExecuteNonQuery() > 0;
    }

    /* ---------- plumbing ---------- */

    private static void RemoveMemberData(SqliteConnection connection, SqliteTransaction transaction,
        string orgId, string accountId)
    {
        AccountStore.Run(connection, transaction,
            "DELETE FROM shared_blueprints WHERE org_id=$org AND account_id=$account",
            ("$org", orgId), ("$account", accountId));
        AccountStore.Run(connection, transaction,
            "DELETE FROM memberships WHERE org_id=$org AND account_id=$account",
            ("$org", orgId), ("$account", accountId));
    }

    private static OrgRow ReadOrg(SqliteDataReader r) => new(
        r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2),
        r.GetString(3), (int)r.GetInt64(4), r.GetString(5), DateTimeOffset.Parse(r.GetString(6)));

    private static T? One<T>(SqliteConnection connection, string sql,
        (string Name, object Value)[] parameters, Func<SqliteDataReader, T> read,
        SqliteTransaction? transaction = null) =>
        AccountStore.One(connection, transaction, sql, parameters, read);

    private static string InviteCode()
    {
        var chars = new char[11];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = i == 5 ? '-' : CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }
}
