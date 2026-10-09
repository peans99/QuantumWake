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

    private long _revision;

    /// <summary>Moves on every write, so a reader can keep an answer until it does.</summary>
    /// <remarks>
    /// Reading the store means deserialising every session, and the atlas asks
    /// for that once per terminal it resolves - a few hundred full reads for one
    /// map request. A counter is enough to say "nothing changed" because this
    /// instance is the only writer its library has.
    /// </remarks>
    public long Revision => Interlocked.Read(ref _revision);

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
            CREATE TABLE IF NOT EXISTS scans (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                started_at   TEXT NOT NULL,
                finished_at  TEXT NOT NULL,
                files        INTEGER NOT NULL,
                parsed       INTEGER NOT NULL,
                forced       INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        // Added alongside rather than by bumping SchemaVersion: a bump drops
        // every session and costs a cold backfill, and all this column does is
        // say when a row was written. Rows from before it stay null, which the
        // page reads as "before this was recorded" rather than as a date.
        if (!HasColumn("sessions", "scanned_at"))
        {
            using var add = _connection.CreateCommand();
            add.CommandText = "ALTER TABLE sessions ADD COLUMN scanned_at TEXT";
            add.ExecuteNonQuery();
        }
    }

    private bool HasColumn(string table, string column)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        using var reader = command.ExecuteReader();

        while (reader.Read())
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    /// <summary>
    /// Bumped whenever a parser change makes stored summaries incomplete.
    /// </summary>
    /// <remarks>
    /// Backups are skipped by fingerprint, so a session parsed before a field
    /// existed keeps its stale payload for ever. Medical beds landed exactly
    /// that way: the parser reads them, the page asks for them, and everyone
    /// who had already run the app saw none, because their sessions had been
    /// summarised by a build that had never heard of a bed. Folding the version
    /// into the fingerprint retires every row at once, at the cost of one cold
    /// backfill after an upgrade - and that cost is the feature working.
    /// </remarks>
    // 3: bed visits gained a kind, so summaries cached before it would show
    //    every login as a medical bed for ever.
    // 4: commodity purchases parse at last. Every session summarised before
    //    0.7.0 recorded the sale and dropped the buy, so Cargo bought would
    //    have stayed at zero on exactly the installs with the most history.
    // 5: party notifications are kept, so sessions summarised before them name
    //    nobody and the Crew page would be empty for everyone but new installs.
    // 6: the party channel's other two titles are read - New Member Joined and
    //    Member Left. 55 membership events on this install were being dropped
    //    for not starting with the word "Party", and without this every one of
    //    them stays dropped in sessions already summarised.
    // 7: ship comms channels are kept, so sessions summarised before them know
    //    nobody was ever aboard anything and the Crew page's ships would be
    //    empty for every install except a brand new one.
    // 12: a timeline entry now carries the engine class it is about, so the
    //     activity feed can put the item's name into a sentence written while
    //     the log was parsed. Sessions summarised before it have no class
    //     beside the sentence and nothing later can supply one, so every
    //     existing install would have gone on reading
    //     "Bought cds_legacy_armor_heavy_helmet_01_01_12" for ever.
    // 13: shard stays are kept - which server each session was placed on and
    //     how it left. 269 joins across 167 backups on this install, every one
    //     of them in a session already summarised; without this the Servers
    //     page would list nothing but the session played after updating.
    // 14: the join line in the timeline names the shard by its id rather than
    //     a reading of it. Only one install ever ran 13, for a day.
    // 15: a disconnect carries its gamerules, and only the world's channel going
    //     down ends a stay. No stored stay changes on this install - every
    //     non-routine disconnect in 195 logs is SC_Default - but the builder did.
    // 16: a contract carries the title the acceptance toast showed, joined by
    //     mission id, and its journal steps counted by kind (pickup, dropoff).
    //     Every session ever summarised had the title in its timeline and never
    //     on the contract, so the rep chips, the Contracts-app check and the
    //     hauling route all read a name that carried none of it.
    //     Contracts are also keyed by mission id now rather than archetype, so
    //     two of one kind in a session are two records - one session of 209
    //     folded a pair of Covalex recoveries into one.
    // 17: refinery work orders the game says completed are kept. One in 234
    //     logs on this install, and the build running that night summarises
    //     the log with no such field when it becomes a backup - without this
    //     the one completion there is would never be shown.
    private const int PayloadVersion = 17;


    /// <summary>
    /// Fingerprint identifying a file's current contents without reading them.
    /// </summary>
    public static string Fingerprint(FileInfo file) =>
        $"v{PayloadVersion}:{file.Length}:{file.LastWriteTimeUtc.Ticks}";

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
            INSERT INTO sessions (id, source_file, fingerprint, started_at, ended_at, handle, payload, scanned_at)
            VALUES ($id, $file, $fingerprint, $started, $ended, $handle, $payload, $scanned)
            ON CONFLICT(id) DO UPDATE SET
                source_file = excluded.source_file,
                fingerprint = excluded.fingerprint,
                started_at  = excluded.started_at,
                ended_at    = excluded.ended_at,
                handle      = excluded.handle,
                payload     = excluded.payload,
                scanned_at  = excluded.scanned_at
            """;

        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$file", session.SourceFile);
        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        command.Parameters.AddWithValue("$started", session.StartedAt.UtcDateTime.ToString("o"));
        command.Parameters.AddWithValue("$ended", session.EndedAt.UtcDateTime.ToString("o"));
        command.Parameters.AddWithValue("$handle", (object?)session.Handle ?? DBNull.Value);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(session, Json));
        command.Parameters.AddWithValue("$scanned", DateTimeOffset.UtcNow.ToString("o"));

        command.ExecuteNonQuery();
        Interlocked.Increment(ref _revision);
    }

    /// <summary>
    /// What the store holds for each log file: the fingerprint it was read at
    /// and when, so a page can say whether the file on disk is the one that
    /// was summarised.
    /// </summary>
    public IReadOnlyList<IngestedFile> Ingested()
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            "SELECT source_file, fingerprint, scanned_at, id, started_at, ended_at, handle FROM sessions";

        var results = new List<IngestedFile>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            results.Add(new IngestedFile(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : DateTimeOffset.Parse(reader.GetString(2)),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4)),
                DateTimeOffset.Parse(reader.GetString(5)),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return results;
    }

    /// <summary>Keeps one scan's outcome, so the page can show what each run did.</summary>
    public void RecordScan(ScanRun run)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO scans (started_at, finished_at, files, parsed, forced)
            VALUES ($started, $finished, $files, $parsed, $forced)
            """;

        command.Parameters.AddWithValue("$started", run.StartedAt.UtcDateTime.ToString("o"));
        command.Parameters.AddWithValue("$finished", run.FinishedAt.UtcDateTime.ToString("o"));
        command.Parameters.AddWithValue("$files", run.Files);
        command.Parameters.AddWithValue("$parsed", run.Parsed);
        command.Parameters.AddWithValue("$forced", run.Forced ? 1 : 0);

        command.ExecuteNonQuery();
    }

    /// <summary>The most recent scans, newest first.</summary>
    public IReadOnlyList<ScanRun> Scans(int take = 50)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            "SELECT started_at, finished_at, files, parsed, forced FROM scans ORDER BY id DESC LIMIT $take";
        command.Parameters.AddWithValue("$take", take);

        var results = new List<ScanRun>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            results.Add(new ScanRun(
                DateTimeOffset.Parse(reader.GetString(0)),
                DateTimeOffset.Parse(reader.GetString(1)),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4) != 0));
        }

        return results;
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
        Interlocked.Increment(ref _revision);
    }

    public void Dispose() => _connection.Dispose();
}

/// <summary>One pass over the install's logs, as the store remembers it.</summary>
/// <param name="Files">How many log files the install had at the time.</param>
/// <param name="Parsed">How many were read rather than served from the cache.</param>
/// <param name="Forced">A full re-read from Settings rather than a routine pass.</param>
public sealed record ScanRun(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int Files,
    int Parsed,
    bool Forced);

/// <summary>A log file as the store last read it.</summary>
/// <param name="ScannedAt">Null for rows written before this was recorded.</param>
public sealed record IngestedFile(
    string SourceFile,
    string Fingerprint,
    DateTimeOffset? ScannedAt,
    string SessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string? Handle);
