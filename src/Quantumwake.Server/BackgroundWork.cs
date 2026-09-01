namespace Quantumwake.Server;

/// <summary>One thing the app is doing that the pilot did not ask for twice.</summary>
public sealed record RunningWork(string Key, string Label, DateTimeOffset StartedAt);

/// <summary>
/// What the app is busy with, so a page can say so.
/// </summary>
/// <remarks>
/// <para>
/// The log scan and the game-files read each kept their own progress and were
/// each surfaced in one place - the scan on a strip at the top, the game files
/// only on Settings. Everything else was silent: a UEX refresh fires at startup
/// and every fifteen minutes, and a community download moves 50 MB, and neither
/// ever said a word. The result is an app that looks idle while it is working
/// and empty while it is filling, which is indistinguishable from broken.
/// </para>
/// <para>
/// Deliberately not a progress bar. Two of these four know how far along they
/// are and two do not, and inventing a percentage for a download whose length
/// nobody checked would be worse than saying "still going, 12s". What every job
/// can honestly report is that it started and has not finished.
/// </para>
/// </remarks>
public sealed class BackgroundWork
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, RunningWork> _running = new(StringComparer.Ordinal);

    /// <summary>
    /// Marks a job as running until the returned handle is disposed.
    /// </summary>
    /// <remarks>
    /// A handle rather than a Finish call because the interesting case is the
    /// one nobody writes: a download that throws. A job left permanently
    /// "running" would have the strip claiming work that died minutes ago.
    /// </remarks>
    public IDisposable Begin(string key, string label)
    {
        lock (_gate)
            _running[key] = new RunningWork(key, label, DateTimeOffset.UtcNow);

        return new Handle(this, key);
    }

    /// <summary>What is running now, oldest first, so the strip is stable.</summary>
    public IReadOnlyList<RunningWork> Running()
    {
        lock (_gate)
            return [.. _running.Values.OrderBy(w => w.StartedAt)];
    }

    private void End(string key)
    {
        lock (_gate)
            _running.Remove(key);
    }

    private sealed class Handle(BackgroundWork work, string key) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            // Disposed twice is not two jobs ending; a using inside a using on
            // the same key would otherwise clear a job that had restarted.
            if (_done) return;

            _done = true;
            work.End(key);
        }
    }
}
