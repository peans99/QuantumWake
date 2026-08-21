using Quantumwake.Core.Events;
using Quantumwake.Core.Parsing;

namespace Quantumwake.Core.Logging;

/// <summary>
/// Follows the live Game.log and raises events as the game writes them.
/// </summary>
/// <remarks>
/// <para>
/// Polling rather than <see cref="FileSystemWatcher"/> alone: the game appends
/// continuously and Windows change notifications for an open, actively written
/// file are unreliable enough that a timer is the dependable path. A watcher is
/// still used to react promptly, with the poll as the guarantee.
/// </para>
/// <para>
/// Reads are strictly read-only and share the handle with the game
/// (<see cref="FileShare.ReadWrite"/> | <see cref="FileShare.Delete"/>). Nothing
/// is ever written to the game directory.
/// </para>
/// </remarks>
public sealed class LogTailer : IAsyncDisposable
{
    private readonly string _path;
    private readonly TimeSpan _interval;
    private readonly LogEventParser _parser = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private long _offset;
    private string? _carry;

    /// <param name="path">Path to the live Game.log.</param>
    /// <param name="interval">Poll interval. One second keeps the UI responsive without cost.</param>
    public LogTailer(string path, TimeSpan? interval = null)
    {
        _path = path;
        _interval = interval ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>Raised for each event parsed from newly appended lines.</summary>
    public event Action<GameEvent>? EventParsed;

    /// <summary>Raised when the log is replaced, i.e. the game restarted.</summary>
    public event Action? Rotated;

    /// <summary>Raised when a read fails; tailing continues regardless.</summary>
    public event Action<Exception>? Faulted;

    /// <summary>True once the file has been opened at least once.</summary>
    public bool IsRunning => _loop is { IsCompleted: false };

    /// <summary>
    /// Starts following the file.
    /// </summary>
    /// <param name="fromStart">
    /// When true the existing contents are replayed first, so a session already
    /// in progress is picked up. When false only new lines are reported.
    /// </param>
    public void Start(bool fromStart = true)
    {
        if (_loop is not null)
            return;

        if (!fromStart && File.Exists(_path))
            _offset = new FileInfo(_path).Length;

        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_interval);

        // Drain immediately so startup does not wait a full interval.
        Drain();

        while (await SafeWaitAsync(timer, token))
            Drain();
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Reads everything appended since the last pass.</summary>
    private void Drain()
    {
        if (!File.Exists(_path))
            return;

        List<string> lines;

        try
        {
            lines = LogFileReader.ReadFrom(_path, ref _offset, out var rotated);

            if (rotated)
            {
                _carry = null;
                Rotated?.Invoke();
            }
        }
        catch (IOException e)
        {
            // The game may briefly lock during rotation; try again next tick.
            Faulted?.Invoke(e);
            return;
        }
        catch (UnauthorizedAccessException e)
        {
            Faulted?.Invoke(e);
            return;
        }

        if (lines.Count == 0)
            return;

        // A poll can land mid-entry. Hold the last line back unless it clearly
        // completes, so multi-line entries are not split across reads.
        if (_carry is not null)
        {
            lines.Insert(0, _carry);
            _carry = null;
        }

        var entries = LogFileReader.ReadEntries(lines).ToList();

        if (entries.Count > 0 && LogFileReader.HasUnterminatedQuote(entries[^1]))
        {
            _carry = entries[^1];
            entries.RemoveAt(entries.Count - 1);
        }

        foreach (var entry in entries)
        {
            if (!LogEnvelope.TryParse(entry, out var line))
                continue;

            var ev = _parser.Parse(line);
            if (ev is not null)
                EventParsed?.Invoke(ev);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is null)
            return;

        await _cts.CancelAsync();

        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        _cts.Dispose();
        _cts = null;
        _loop = null;
    }
}
