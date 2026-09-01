namespace Quantumwake.Server;

/// <summary>
/// Progress of the log scan, so the UI can say what is happening.
/// </summary>
/// <remarks>
/// A cold backfill reads 400 MB across 145 files and takes about half a minute.
/// Without this the dashboard just sits empty while it runs, with no way to tell
/// a slow first start from a broken one.
/// </remarks>
public sealed class ScanStatus
{
    private readonly Lock _gate = new();

    private int _done;
    private int _total;
    private int _parsed;
    private string? _file;
    private bool _running;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _finishedAt;

    /// <summary>A snapshot safe to serialise.</summary>
    public object Snapshot()
    {
        lock (_gate)
        {
            var elapsed = _startedAt is null
                ? TimeSpan.Zero
                : (_finishedAt ?? DateTimeOffset.UtcNow) - _startedAt.Value;

            return new
            {
                running = _running,
                done = _done,
                total = _total,

                // Files newly parsed rather than served from cache - on a warm
                // start this stays near zero even as `done` climbs.
                parsed = _parsed,
                file = _file,
                percent = _total == 0 ? 0 : (int)Math.Round(_done * 100.0 / _total),
                elapsedSeconds = (int)elapsed.TotalSeconds
            };
        }
    }

    public void Begin()
    {
        lock (_gate)
        {
            _running = true;
            _done = 0;
            _total = 0;
            _parsed = 0;
            _file = null;
            _startedAt = DateTimeOffset.UtcNow;
            _finishedAt = null;
        }
    }

    /// <summary>
    /// Claims the scan, or answers false because one is already running.
    /// </summary>
    /// <remarks>
    /// The button can be pressed twice, and two scans over one library would
    /// interleave their progress into a bar that goes backwards - quite apart
    /// from reading 400 MB twice for one answer.
    /// </remarks>
    public bool TryBegin()
    {
        lock (_gate)
        {
            if (_running)
                return false;

            _running = true;
            _done = 0;
            _total = 0;
            _parsed = 0;
            _file = null;
            _startedAt = DateTimeOffset.UtcNow;
            _finishedAt = null;
            return true;
        }
    }

    /// <summary>Whether a scan is running, for the activity feed.</summary>
    public bool Running { get { lock (_gate) return _running; } }

    /// <summary>How far along, or null when the total is not known yet.</summary>
    public (int Done, int Total, string? File, int Seconds) Progress
    {
        get
        {
            lock (_gate)
            {
                var elapsed = _startedAt is null
                    ? TimeSpan.Zero
                    : (_finishedAt ?? DateTimeOffset.UtcNow) - _startedAt.Value;

                return (_done, _total, _file, (int)elapsed.TotalSeconds);
            }
        }
    }

    public void Report(int done, int total, string file, bool cached)
    {
        lock (_gate)
        {
            _done = done;
            _total = total;
            _file = file;

            if (!cached)
                _parsed++;
        }
    }

    public void Finish()
    {
        lock (_gate)
        {
            _running = false;
            _finishedAt = DateTimeOffset.UtcNow;
            _file = null;
        }
    }
}
