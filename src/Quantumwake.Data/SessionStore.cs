using Quantumwake.Core;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Quantumwake.Core.State;

namespace Quantumwake.Data;

/// <summary>
/// Persists parsed session summaries so a restart does not re-read 400 MB of logs.
/// </summary>
/// <remarks>
/// <para>
/// Summaries are stored as JSON in a SQLite row rather than shredded across
/// relational tables. The access pattern is "load whole sessions, aggregate in
/// memory" - there are a few hundred sessions, not millions - so normalising
/// would add schema churn for no query benefit. Timestamps and handle are
/// promoted to real columns for indexed range queries.
/// </para>
/// <para>
/// Idempotency uses a fingerprint of file length plus last-write time rather
/// than a content hash: hashing the full backup set on every start would defeat
/// the point of caching, and rotated logs are immutable once written. The live
/// Game.log changes constantly and is simply re-parsed each scan.
/// </para>
/// </remarks>
public sealed class SessionStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SqliteConnection _connection;

    /// <param name="databasePath">File path, or <c>:memory:</c> for a transient store.</param>
    public SessionStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
        Initialise();
    }

    /// <summary>Default location under the user's local app data.</summary>
    public static string DefaultDatabasePath => DatabasePathFor(null);

    /// <summary>
    /// Database path scoped to one install.
    /// </summary>
    /// <remarks>
    /// Each install gets its own file, keyed by a hash of its root path. Sharing
    /// a single database would merge unrelated installs - pointing the app at a
    /// PTU channel, or at a simulated install for testing, would silently blend
    /// its sessions into the LIVE totals.
    /// </remarks>
    public static string DatabasePathFor(string? installRoot)
    {
        var directory = AppPaths.Root;

        if (string.IsNullOrWhiteSpace(installRoot))
            return Path.Combine(directory, "sessions.db");

        var normalised = Path.GetFullPath(installRoot)
            .TrimEnd(Path.DirectorySeparatorChar)
            .ToLowerInvariant();

        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(normalised)))[..12];

        return Path.Combine(directory, $"sessions-{hash}.db");
    }

    /// <summary>
    /// Bump when the parser starts capturing something the cached payloads lack.
    /// A mismatch clears the cache, so the next scan re-reads every log and the
    /// new field populates without anyone knowing to run -Rescan.
    ///
    /// 2: CommodityTrade.ResourceId.
    /// 3: SessionSummary.Pickups.
    /// </summary>
    private const int SchemaVersion = 3;

    private void Initialise()
    {
        using (var version = _connection.CreateCommand())
        {
            version.CommandText = "PRAGMA user_version";
            var current = Convert.ToInt32(version.ExecuteScalar());

            if (current != SchemaVersion)
            {
                using var reset = _connection.CreateCommand();
                reset.CommandText =
                    $"DROP TABLE IF EXISTS sessions; PRAGMA user_version = {SchemaVersion}";
                reset.ExecuteNonQuery();
            }
        }

        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS sessions (
                id           TEXT PRIMARY KEY,
                source_file  TEXT NOT NULL,
                fingerprint  TEXT NOT NULL,
                started_at   TEXT NOT NULL,
                ended_at     TEXT NOT NULL,
                handle       TEXT,
                payload      TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_sessions_started ON sessions(started_at DESC);
            CREATE INDEX IF NOT EXISTS ix_sessions_file    ON sessions(source_file);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Fingerprint identifying a file's current contents without reading them.
    /// </summary>
    public static string Fingerprint(FileInfo file) =>
        $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";

    /// <summary>True when this exact file version has already been ingested.</summary>
    public bool IsCurrent(string sourceFile, string fingerprint)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            "SELECT 1 FROM sessions WHERE source_file = $file AND fingerprint = $fingerprint LIMIT 1";
        command.Parameters.AddWithValue("$file", sourceFile);
        command.Parameters.AddWithValue("$fingerprint", fingerprint);

        return command.ExecuteScalar() is not null;
    }

    /// <summary>Inserts or replaces a session, keyed on its id.</summary>
    public void Save(SessionSummary session, string fingerprint)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO sessions (id, source_file, fingerprint, started_at, ended_at, handle, payload)
            VALUES ($id, $file, $fingerprint, $started, $ended, $handle, $payload)
            ON CONFLICT(id) DO UPDATE SET
                source_file = excluded.source_file,
                fingerprint = excluded.fingerprint,
                started_at  = excluded.started_at,
                ended_at    = excluded.ended_at,
                handle      = excluded.handle,
                payload     = excluded.payload
            """;

        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$file", session.SourceFile);
        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        command.Parameters.AddWithValue("$started", session.StartedAt.UtcDateTime.ToString("o"));
        command.Parameters.AddWithValue("$ended", session.EndedAt.UtcDateTime.ToString("o"));
        command.Parameters.AddWithValue("$handle", (object?)session.Handle ?? DBNull.Value);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(session, Json));

        command.ExecuteNonQuery();
    }

    /// <summary>Runs several saves in one transaction, which matters for a full backfill.</summary>
    public void SaveAll(IEnumerable<(SessionSummary Session, string Fingerprint)> sessions)
    {
        using var transaction = _connection.BeginTransaction();

        foreach (var (session, fingerprint) in sessions)
            Save(session, fingerprint);

        transaction.Commit();
    }

    /// <summary>All stored sessions, newest first.</summary>
    public IReadOnlyList<SessionSummary> All()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT payload FROM sessions ORDER BY started_at DESC";

        var results = new List<SessionSummary>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var session = JsonSerializer.Deserialize<SessionSummary>(reader.GetString(0), Json);
            if (session is not null)
                results.Add(session);
        }

        return results;
    }

    /// <summary>A single session by id, or null.</summary>
    public SessionSummary? Get(string id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT payload FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);

        return command.ExecuteScalar() is string payload
            ? JsonSerializer.Deserialize<SessionSummary>(payload, Json)
            : null;
    }

    public int Count()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sessions";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    /// <summary>Removes everything, for a forced re-scan.</summary>
    public void Clear()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM sessions";
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
