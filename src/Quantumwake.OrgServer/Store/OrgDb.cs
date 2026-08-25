using Microsoft.Data.Sqlite;

namespace Quantumwake.OrgServer.Store;

/// <summary>
/// The one database file, and the rules for touching it.
/// </summary>
/// <remarks>
/// <para>
/// Raw SQL over <see cref="SqliteConnection"/>, as <c>SessionStore</c> does -
/// but the versioning rule is the opposite of that store's, on purpose.
/// SessionStore drops its tables on a schema mismatch because everything in
/// them can be rebuilt from the logs. Nothing here can: this file is the only
/// copy of what members chose to share, and the sources are on other people's
/// machines. So migrations are additive only, and a database written by a
/// <em>newer</em> build refuses to open rather than being half-read.
/// </para>
/// <para>
/// WAL by default; "delete" for network filesystems (Azure App Service's
/// /home is SMB-backed and WAL's shared-memory files are not safe there).
/// Either way this server runs as exactly one instance - SQLite is the
/// reason, and the deployment docs say so in bold.
/// </para>
/// </remarks>
public sealed class OrgDb
{
    /// <summary>
    /// 1: 0.9.0 - accounts, identities, api_tokens, link_codes, orgs,
    ///    memberships, invites, org_modules.
    /// </summary>
    private const int SchemaVersion = 1;

    private readonly string _connectionString;
    private readonly string _journal;

    public OrgDb(string directory, string journal = "wal")
    {
        EnsureWritable(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "org.db"),
        }.ToString();
        _journal = journal.Equals("delete", StringComparison.OrdinalIgnoreCase) ? "delete" : "wal";

        Initialise();
    }

    /// <summary>
    /// Fail here, naming the fix, rather than deep inside SQLite.
    /// </summary>
    /// <remarks>
    /// The container runs as a non-root user and a mounted data directory
    /// arrives owned by whoever mounted it - on App Service that is the
    /// platform, and /home's ownership is not ours to set. Without this the
    /// symptom is "SQLite Error 14: unable to open database file", or a bare
    /// UnauthorizedAccessException from Directory.CreateDirectory, and a
    /// container that restarts for ever without saying why.
    /// </remarks>
    private static void EnsureWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            // Creating the directory can succeed where writing into it cannot,
            // so prove the write rather than inferring it.
            var probe = Path.Combine(directory, ".write-probe");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new InvalidOperationException(
                $"The data directory '{directory}' is not writable by the server "
                + $"(running as '{Environment.UserName}'). In a container the server "
                + "runs as a non-root user and a mounted directory keeps the ownership "
                + "of whatever mounted it: chown it to uid 1654, or point --Data at a "
                + "directory the server owns. On Azure App Service use "
                + "OrgServer__Data=/home/data with WEBSITES_ENABLE_APP_SERVICE_STORAGE=true.",
                ex);
        }
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragmas = connection.CreateCommand();
        pragmas.CommandText = $"PRAGMA journal_mode={_journal}; PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";
        pragmas.ExecuteNonQuery();

        return connection;
    }

    private void Initialise()
    {
        using var connection = Open();

        var found = Convert.ToInt32(Scalar(connection, "PRAGMA user_version"));

        if (found > SchemaVersion)
        {
            // Do not "repair" it, do not fall back to empty: it belongs to a
            // newer build and half-reading it would corrupt other people's
            // shared data quietly.
            throw new InvalidOperationException(
                $"org.db was written by a newer build (schema {found}, this build reads {SchemaVersion}). "
                + "Update the server, or point it at a different data folder.");
        }

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Schema;
        command.ExecuteNonQuery();

        command.CommandText = $"PRAGMA user_version={SchemaVersion}";
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    // Additive by construction: CREATE TABLE IF NOT EXISTS throughout, so
    // re-running the whole script against any older schema is the migration.
    // Timestamps are ISO-8601 UTC strings, as SessionStore stores them.
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS accounts (
            id              TEXT PRIMARY KEY,
            sc_handle       TEXT,
            handle_verified INTEGER NOT NULL DEFAULT 0,
            display_name    TEXT NOT NULL,
            is_server_admin INTEGER NOT NULL DEFAULT 0,
            created_at      TEXT NOT NULL,
            last_seen_at    TEXT
        );

        CREATE TABLE IF NOT EXISTS identities (
            provider   TEXT NOT NULL,
            subject    TEXT NOT NULL,
            account_id TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
            display    TEXT,
            created_at TEXT NOT NULL,
            PRIMARY KEY (provider, subject)
        );
        CREATE INDEX IF NOT EXISTS ix_identities_account ON identities(account_id);

        CREATE TABLE IF NOT EXISTS api_tokens (
            id             TEXT PRIMARY KEY,
            account_id     TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
            token_hash     TEXT NOT NULL UNIQUE,
            display_prefix TEXT NOT NULL,
            name           TEXT NOT NULL,
            created_at     TEXT NOT NULL,
            last_used_at   TEXT,
            revoked_at     TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_tokens_account ON api_tokens(account_id);

        CREATE TABLE IF NOT EXISTS link_codes (
            code               TEXT PRIMARY KEY,
            device_secret_hash TEXT NOT NULL,
            client_name        TEXT NOT NULL,
            status             TEXT NOT NULL,
            account_id         TEXT,
            created_at         TEXT NOT NULL,
            expires_at         TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS orgs (
            id                  TEXT PRIMARY KEY,
            name                TEXT NOT NULL,
            note                TEXT,
            status              TEXT NOT NULL,
            request_expiry_days INTEGER NOT NULL DEFAULT 14,
            created_by          TEXT NOT NULL REFERENCES accounts(id),
            created_at          TEXT NOT NULL,
            activated_at        TEXT,
            activated_by        TEXT
        );

        CREATE TABLE IF NOT EXISTS memberships (
            org_id     TEXT NOT NULL REFERENCES orgs(id) ON DELETE CASCADE,
            account_id TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
            role       TEXT NOT NULL,
            joined_at  TEXT NOT NULL,
            invited_by TEXT,
            PRIMARY KEY (org_id, account_id)
        );
        CREATE INDEX IF NOT EXISTS ix_memberships_account ON memberships(account_id);

        CREATE TABLE IF NOT EXISTS invites (
            code       TEXT PRIMARY KEY,
            org_id     TEXT NOT NULL REFERENCES orgs(id) ON DELETE CASCADE,
            created_by TEXT NOT NULL,
            created_at TEXT NOT NULL,
            expires_at TEXT NOT NULL,
            max_uses   INTEGER NOT NULL DEFAULT 0,
            uses       INTEGER NOT NULL DEFAULT 0,
            revoked_at TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_invites_org ON invites(org_id);

        CREATE TABLE IF NOT EXISTS org_modules (
            org_id     TEXT NOT NULL REFERENCES orgs(id) ON DELETE CASCADE,
            module     TEXT NOT NULL,
            enabled    INTEGER NOT NULL,
            updated_at TEXT NOT NULL,
            updated_by TEXT NOT NULL,
            PRIMARY KEY (org_id, module)
        );
        """;
}
