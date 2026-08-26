using Microsoft.Data.Sqlite;
using Quantumwake.OrgShared;
using System.Security.Cryptography;

namespace Quantumwake.OrgServer.Store;

public sealed record AccountRow(
    string Id, string? Handle, bool HandleVerified, string DisplayName,
    bool AdminFlag, DateTimeOffset CreatedAt);

public sealed record TokenRow(
    string Id, string DisplayPrefix, string Name,
    DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt);

public sealed record LinkRow(
    string Code, string ClientName, string Status, string? AccountId, DateTimeOffset ExpiresAt);

/// <summary>
/// Who people are: accounts, the provider identities that prove them, the API
/// tokens their desktop apps hold, and the link codes that mint those tokens.
/// </summary>
/// <remarks>
/// Tokens are stored as SHA-256 hashes: 256 bits of fresh randomness needs no
/// bcrypt, and a leaked database yields nothing replayable. The full value
/// exists exactly twice - once in the response that minted it, once in the
/// client's own file.
/// </remarks>
public sealed class AccountStore(OrgDb db, IReadOnlyList<string> configuredAdmins, ILogger<AccountStore> logger)
{
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// The provider key LAN mode's single shared account hangs off, so it is
    /// an identity like any other and needs no table of its own.
    /// </summary>
    public const string LanProvider = "lan";

    /* ---------- accounts and identities ---------- */

    /// <summary>Sign-in: the identity's account, created on first sight.</summary>
    public AccountRow UpsertIdentity(string provider, string subject, string display)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        display = Sanitise.Clean(display, "someone", Sanitise.Title);

        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();

        var existing = One(connection, transaction,
            "SELECT account_id FROM identities WHERE provider=$p AND subject=$s",
            [("$p", provider), ("$s", subject)], r => r.GetString(0));

        string id;
        if (existing is null)
        {
            // The bootstrap problem: a standalone server with no configured
            // admins would otherwise have nobody able to approve the first
            // org. Narrowed to the literal first account ever, and logged,
            // because on a public server this is the window someone forgot to
            // close - the log line is how they find out.
            // The LAN account is always the admin - it is the only account
            // there is - and it must not count as the "first account" that
            // claims the server, or turning LAN mode off later would leave a
            // populated database in which nobody can ever become an admin.
            var lan = provider == LanProvider;
            var first = !lan && Convert.ToInt64(Scalar(connection, transaction,
                "SELECT COUNT(*) FROM accounts a WHERE EXISTS "
                + "(SELECT 1 FROM identities i WHERE i.account_id = a.id AND i.provider <> $lan)",
                ("$lan", LanProvider))) == 0;
            var admin = lan || (first && configuredAdmins.Count == 0);

            id = Guid.NewGuid().ToString("N")[..12];
            Run(connection, transaction,
                "INSERT INTO accounts (id, display_name, is_server_admin, created_at, last_seen_at) "
                + "VALUES ($id, $name, $admin, $now, $now)",
                ("$id", id), ("$name", display), ("$admin", admin ? 1 : 0), ("$now", now));
            Run(connection, transaction,
                "INSERT INTO identities (provider, subject, account_id, display, created_at) "
                + "VALUES ($p, $s, $id, $name, $now)",
                ("$p", provider), ("$s", subject), ("$id", id), ("$name", display), ("$now", now));

            if (admin && !lan)
                logger.LogWarning(
                    "No admins are configured; the first account to sign in ({Display}) is now the server admin.",
                    display);
        }
        else
        {
            id = existing;
            Run(connection, transaction,
                "UPDATE accounts SET display_name=$name, last_seen_at=$now WHERE id=$id",
                ("$name", display), ("$now", now), ("$id", id));
        }

        transaction.Commit();
        return Get(id)!;
    }

    /// <summary>
    /// The single account LAN mode signs everybody in as, made on first need.
    /// </summary>
    /// <remarks>
    /// One row rather than one per browser: with no sign-in there is nothing
    /// to tell two people apart, and inventing a per-browser identity would
    /// put a name on a member list that means nothing. The Org page's floor -
    /// handles are self-declared - becomes the whole truth here.
    /// </remarks>
    public AccountRow LanAccount() =>
        UpsertIdentity(LanProvider, LanProvider, "Everyone on this network");

    public AccountRow? Get(string accountId)
    {
        using var connection = db.Open();
        return One(connection, null,
            "SELECT id, sc_handle, handle_verified, display_name, is_server_admin, created_at "
            + "FROM accounts WHERE id=$id",
            [("$id", accountId)], ReadAccount);
    }

    public void SetHandle(string accountId, string? handle)
    {
        using var connection = db.Open();
        Run(connection, null,
            "UPDATE accounts SET sc_handle=$handle, handle_verified=0 WHERE id=$id",
            ("$handle", (object?)Sanitise.CleanOptional(handle, OrgLimits.Handle) ?? DBNull.Value),
            ("$id", accountId));
    }

    /// <summary>
    /// Admin is the flag on the account or a configured provider subject,
    /// checked live so rotating the config needs a restart, not a migration.
    /// </summary>
    public bool IsServerAdmin(string accountId)
    {
        using var connection = db.Open();

        var flag = Convert.ToInt64(Scalar(connection, null,
            "SELECT COUNT(*) FROM accounts WHERE id=$id AND is_server_admin=1", ("$id", accountId)));
        if (flag > 0)
            return true;

        if (configuredAdmins.Count == 0)
            return false;

        // A bare subject matches whichever provider it came from, which was
        // the whole configuration when Discord was the only door. With three,
        // "google:1234" says which one is meant - and both forms are accepted,
        // because the short one is what existing deployments have written down.
        var identities = Many(connection,
            "SELECT provider, subject FROM identities WHERE account_id=$id",
            [("$id", accountId)], r => (Provider: r.GetString(0), Subject: r.GetString(1)));

        return identities.Any(i =>
            configuredAdmins.Contains(i.Subject, StringComparer.Ordinal)
            || configuredAdmins.Contains($"{i.Provider}:{i.Subject}", StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Everything about them, gone. Cascades take the rest.</summary>
    public void Forget(string accountId)
    {
        using var connection = db.Open();
        Run(connection, null, "DELETE FROM accounts WHERE id=$id", ("$id", accountId));
    }

    /* ---------- API tokens ---------- */

    public (TokenRow Row, string Token) MintToken(string accountId, string name)
    {
        var token = "qwo_" + RandomToken(32);
        var row = new TokenRow(
            Guid.NewGuid().ToString("N")[..12], token[..8],
            Sanitise.Clean(name, "a device"), DateTimeOffset.UtcNow, null, null);

        using var connection = db.Open();
        Run(connection, null,
            "INSERT INTO api_tokens (id, account_id, token_hash, display_prefix, name, created_at) "
            + "VALUES ($id, $account, $hash, $prefix, $name, $now)",
            ("$id", row.Id), ("$account", accountId), ("$hash", Hash(token)),
            ("$prefix", row.DisplayPrefix), ("$name", row.Name), ("$now", row.CreatedAt.ToString("O")));

        return (row, token);
    }

    /// <summary>The account a bearer token proves, or null.</summary>
    public AccountRow? ResolveToken(string token)
    {
        using var connection = db.Open();

        var found = One(connection, null,
            "SELECT a.id, a.sc_handle, a.handle_verified, a.display_name, a.is_server_admin, a.created_at, "
            + "t.id, t.last_used_at "
            + "FROM api_tokens t JOIN accounts a ON a.id = t.account_id "
            + "WHERE t.token_hash=$hash AND t.revoked_at IS NULL",
            [("$hash", Hash(token))],
            r => (Account: ReadAccount(r), TokenId: r.GetString(6),
                  LastUsed: r.IsDBNull(7) ? (DateTimeOffset?)null : DateTimeOffset.Parse(r.GetString(7))));

        if (found == default)
            return null;

        // Stamped at most hourly: "last used today" is the answer the account
        // page needs, and a write per request is not a price worth paying for
        // minutes.
        var now = DateTimeOffset.UtcNow;
        if (found.LastUsed is not { } used || now - used > TimeSpan.FromHours(1))
        {
            Run(connection, null, "UPDATE api_tokens SET last_used_at=$now WHERE id=$id",
                ("$now", now.ToString("O")), ("$id", found.TokenId));
        }

        return found.Account;
    }

    /// <summary>
    /// Stable limiter identity without retaining the bearer value or stamping
    /// last-used. Invalid tokens deliberately fall back to the caller IP.
    /// </summary>
    public string? RateLimitIdentity(string token)
    {
        using var connection = db.Open();
        return One(connection, null,
            "SELECT account_id, id FROM api_tokens WHERE token_hash=$hash AND revoked_at IS NULL",
            [("$hash", Hash(token))], r => $"account:{r.GetString(0)}:token:{r.GetString(1)}");
    }

    public IReadOnlyList<TokenRow> Tokens(string accountId)
    {
        using var connection = db.Open();
        return Many(connection,
            "SELECT id, display_prefix, name, created_at, last_used_at, revoked_at "
            + "FROM api_tokens WHERE account_id=$id ORDER BY created_at",
            [("$id", accountId)],
            r => new TokenRow(r.GetString(0), r.GetString(1), r.GetString(2),
                DateTimeOffset.Parse(r.GetString(3)),
                r.IsDBNull(4) ? null : DateTimeOffset.Parse(r.GetString(4)),
                r.IsDBNull(5) ? null : DateTimeOffset.Parse(r.GetString(5))));
    }

    /// <summary>True when the token was theirs to revoke.</summary>
    public bool RevokeToken(string accountId, string tokenId)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE api_tokens SET revoked_at=$now WHERE id=$id AND account_id=$account AND revoked_at IS NULL";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", tokenId);
        command.Parameters.AddWithValue("$account", accountId);
        return command.ExecuteNonQuery() > 0;
    }

    /* ---------- link codes ---------- */

    public OrgLinkStartResponse StartLink(string? clientName, string verifyBase, DateTimeOffset now)
    {
        var code = LinkCode();
        var secret = RandomToken(32);

        using var connection = db.Open();
        Run(connection, null,
            "INSERT INTO link_codes (code, device_secret_hash, client_name, status, created_at, expires_at) "
            + "VALUES ($code, $hash, $name, 'pending', $now, $expires)",
            ("$code", code), ("$hash", Hash(secret)),
            ("$name", Sanitise.Clean(clientName, "a device")),
            ("$now", now.ToString("O")),
            ("$expires", now.AddMinutes(OrgLimits.LinkCodeMinutes).ToString("O")));

        // Expired codes from abandoned flows are swept here, on the next
        // start, rather than by a timer nothing else needs.
        Run(connection, null, "DELETE FROM link_codes WHERE expires_at < $cutoff",
            ("$cutoff", now.AddHours(-1).ToString("O")));

        return new OrgLinkStartResponse(
            code, secret, $"{verifyBase}/link?code={code}",
            now.AddMinutes(OrgLimits.LinkCodeMinutes), OrgLimits.LinkPollSeconds);
    }

    public LinkRow? GetLink(string code)
    {
        using var connection = db.Open();
        return One(connection, null,
            "SELECT code, client_name, status, account_id, expires_at FROM link_codes WHERE code=$code",
            [("$code", code)],
            r => new LinkRow(r.GetString(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3), DateTimeOffset.Parse(r.GetString(4))));
    }

    /// <summary>True when the code was pending and is now decided.</summary>
    public bool DecideLink(string code, string accountId, bool approved, DateTimeOffset now)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE link_codes SET status=$status, account_id=$account "
            + "WHERE code=$code AND status='pending' AND expires_at > $now";
        command.Parameters.AddWithValue("$status", approved ? "approved" : "denied");
        command.Parameters.AddWithValue("$account", accountId);
        command.Parameters.AddWithValue("$code", code);
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// The token is released exactly once, and only to the holder of the
    /// device secret - the code was visible in a browser URL, the secret never
    /// left the machine that asked.
    /// </summary>
    public OrgLinkPollResponse PollLink(string? code, string? secret, DateTimeOffset now)
    {
        if (code is not { Length: > 0 } || secret is not { Length: > 0 })
            return new OrgLinkPollResponse("expired", null, null);

        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();

        var row = One(connection, transaction,
            "SELECT status, account_id, expires_at, device_secret_hash FROM link_codes WHERE code=$code",
            [("$code", code)],
            r => (Status: r.GetString(0), AccountId: r.IsDBNull(1) ? null : r.GetString(1),
                  Expires: DateTimeOffset.Parse(r.GetString(2)), Hash: r.GetString(3)));

        // Wrong code, wrong secret, already claimed, or too late: the same
        // answer for all of them, because telling them apart tells an
        // attacker which guesses were close.
        if (row == default || row.Hash != Hash(secret) || row.Expires <= now
            || row.Status is "claimed" or "expired")
            return new OrgLinkPollResponse("expired", null, null);

        if (row.Status == "pending")
            return new OrgLinkPollResponse("pending", null, null);

        if (row.Status == "denied")
            return new OrgLinkPollResponse("denied", null, null);

        Run(connection, transaction, "UPDATE link_codes SET status='claimed' WHERE code=$code",
            ("$code", code));
        transaction.Commit();

        var link = GetLink(code)!;
        var (_, token) = MintToken(row.AccountId!, link.ClientName);
        var account = Get(row.AccountId!)!;

        return new OrgLinkPollResponse("approved", token,
            new OrgAccount(account.Id, account.Handle, account.HandleVerified,
                account.DisplayName, IsServerAdmin(account.Id)));
    }

    /* ---------- plumbing, shared with the other stores ---------- */

    private static AccountRow ReadAccount(SqliteDataReader r) => new(
        r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetInt64(2) != 0,
        r.GetString(3), r.GetInt64(4) != 0, DateTimeOffset.Parse(r.GetString(5)));

    private static string RandomToken(int bytes) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string LinkCode()
    {
        var chars = new char[9];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = i == 4 ? '-' : CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    internal static void Run(SqliteConnection connection, SqliteTransaction? transaction,
        string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    internal static object? Scalar(SqliteConnection connection, SqliteTransaction? transaction,
        string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return command.ExecuteScalar();
    }

    internal static T? One<T>(SqliteConnection connection, SqliteTransaction? transaction,
        string sql, (string Name, object Value)[] parameters, Func<SqliteDataReader, T> read)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        using var reader = command.ExecuteReader();
        return reader.Read() ? read(reader) : default;
    }

    internal static IReadOnlyList<T> Many<T>(SqliteConnection connection,
        string sql, (string Name, object Value)[] parameters, Func<SqliteDataReader, T> read)
        => Many(connection, null, sql, parameters, read);

    internal static IReadOnlyList<T> Many<T>(SqliteConnection connection, SqliteTransaction? transaction,
        string sql, (string Name, object Value)[] parameters, Func<SqliteDataReader, T> read)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
            rows.Add(read(reader));
        return rows;
    }
}
