namespace Quantumwake.Data;

/// <summary>How far along the read of the game's own files is.</summary>
public enum GameDataState
{
    /// <summary>No install was found, so there is nothing to read.</summary>
    NoInstall,

    /// <summary>Reading. Pages backed by it are empty and not broken.</summary>
    Reading,

    /// <summary>Read. Everything backed by it is answerable.</summary>
    Ready,

    /// <summary>Tried and could not. The reason is worth showing.</summary>
    Failed,
}

/// <summary>
/// Whether the install has been read yet, and what came out of it.
/// </summary>
/// <remarks>
/// <para>
/// The first read after a patch takes about half a minute, and it happens on a
/// background task so the app starts quickly. For that half minute every page
/// backed by it is empty - and several of them said "enable the community
/// dataset", which is advice to download 110 MB to fix a wait.
/// </para>
/// <para>
/// Worse, it is indistinguishable from failure. An install the app cannot find
/// looks exactly like one it has not finished reading, and the difference
/// matters: one resolves itself and the other never will.
/// </para>
/// </remarks>
public sealed class GameDataStatus
{
    private readonly Lock _gate = new();

    private GameDataState _state = GameDataState.Reading;
    private string? _problem;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _finishedAt;
    private IReadOnlyDictionary<string, int> _counts = new Dictionary<string, int>();

    public void Begin()
    {
        lock (_gate)
        {
            _state = GameDataState.Reading;
            _startedAt = DateTimeOffset.UtcNow;
            _finishedAt = null;
            _problem = null;
        }
    }

    public void Ready(IReadOnlyDictionary<string, int> counts)
    {
        lock (_gate)
        {
            // Counts of zero across the board mean the archive was opened and
            // said nothing, which is a failure wearing success's clothes.
            _state = counts.Values.Any(c => c > 0) ? GameDataState.Ready : GameDataState.Failed;
            _problem = _state == GameDataState.Failed
                ? "The game archive was read but produced nothing. It may be mid-patch."
                : null;

            _counts = counts;
            _finishedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Failed(string problem)
    {
        lock (_gate)
        {
            _state = GameDataState.Failed;
            _problem = problem;
            _finishedAt = DateTimeOffset.UtcNow;
        }
    }

    public void NoInstall()
    {
        lock (_gate)
        {
            _state = GameDataState.NoInstall;
            _problem = "No Star Citizen install was found, so nothing could be read from one.";
            _finishedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>What to tell a page that has nothing to show.</summary>
    /// <summary>Whether the install is being read right now, for the activity feed.</summary>
    public bool Reading { get { lock (_gate) return _state == GameDataState.Reading; } }

    /// <summary>How long the current read has been going.</summary>
    public int Seconds
    {
        get
        {
            lock (_gate)
                return _startedAt is { } began
                    ? (int)((_finishedAt ?? DateTimeOffset.UtcNow) - began).TotalSeconds
                    : 0;
        }
    }

    public object Snapshot()
    {
        lock (_gate)
        {
            return new
            {
                state = _state.ToString().ToLowerInvariant(),
                problem = _problem,
                startedAt = _startedAt,
                finishedAt = _finishedAt,
                seconds = _startedAt is { } began
                    ? Math.Round(((_finishedAt ?? DateTimeOffset.UtcNow) - began).TotalSeconds, 1)
                    : (double?)null,
                counts = _counts,
            };
        }
    }
}
