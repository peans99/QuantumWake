using Quantumwake.OrgShared;

namespace Quantumwake.OrgServer.Store;

/// <summary>Append-only answers to who changed an org and when.</summary>
public sealed class AuditStore(OrgDb db)
{
    public void Write(string? accountId, string? orgId, string action,
        string? target = null, string? detail = null)
    {
        using var connection = db.Open();
        AccountStore.Run(connection, null,
            "INSERT INTO audit_events (org_id, account_id, action, target, detail, at) "
            + "VALUES ($org, $account, $action, $target, $detail, $at)",
            ("$org", (object?)orgId ?? DBNull.Value),
            ("$account", (object?)accountId ?? DBNull.Value),
            ("$action", action), ("$target", (object?)target ?? DBNull.Value),
            ("$detail", (object?)detail ?? DBNull.Value),
            ("$at", DateTimeOffset.UtcNow.ToString("O")));
    }

    public IReadOnlyList<OrgAuditRow> Recent(string orgId, int count = 100)
    {
        using var connection = db.Open();
        return AccountStore.Many(connection,
            "SELECT id, org_id, account_id, action, target, detail, at FROM audit_events "
            + "WHERE org_id=$org ORDER BY id DESC LIMIT $count",
            [("$org", orgId), ("$count", Math.Clamp(count, 1, 250))],
            r => new OrgAuditRow(r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5), DateTimeOffset.Parse(r.GetString(6))));
    }

    public IReadOnlyList<OrgAuditRow> RecentAll(int count = 200)
    {
        using var connection = db.Open();
        return AccountStore.Many(connection,
            "SELECT id, org_id, account_id, action, target, detail, at FROM audit_events "
            + "ORDER BY id DESC LIMIT $count",
            [("$count", Math.Clamp(count, 1, 500))],
            r => new OrgAuditRow(r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5), DateTimeOffset.Parse(r.GetString(6))));
    }
}
