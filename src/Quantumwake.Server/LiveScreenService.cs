using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>What the live reader is doing, for the page.</summary>
/// <param name="State">off, unavailable, waiting (no frame to take) or reading.</param>
/// <param name="Why">The reason behind off, unavailable or waiting, in a sentence.</param>
/// <param name="LastFrameAt">When a frame was last read.</param>
/// <param name="LastKind">What that frame was - Mining, Unknown, and so on.</param>
/// <param name="LastSummary">What it said.</param>
/// <param name="Frames">Frames read since the app started.</param>
/// <param name="Kept">Rock scans kept in the reading log since the app started.</param>
/// <param name="LastReadMs">How long the engine took over the last frame.</param>
public sealed record LiveScreenStatus(
    string State,
    string? Why = null,
    DateTimeOffset? LastFrameAt = null,
    string? LastKind = null,
    string? LastSummary = null,
    int Frames = 0,
    int Kept = 0,
    long LastReadMs = 0);

/// <summary>What <c>/api/live</c> says.</summary>
public sealed record LiveScreenState(
    bool Available,
    string? Unavailable,
    bool Asked,
    bool Enabled,
    DateTimeOffset? AnsweredAt,
    bool Ask,
    LiveScreenStatus Status);

/// <summary>The pilot's answer, posted to <c>/api/live</c>.</summary>
public sealed record LiveAnswer(bool Enabled);

/// <summary>
/// Reads the game's screen while the pilot has said yes to it.
/// </summary>
/// <remarks>
/// <para>
/// A frame every two seconds, and only while Star Citizen is the window in
/// front: <see cref="IGameScreenCapture"/> refuses otherwise, so alt-tabbing to
/// the dashboard stops it without anything being switched. The frame goes
/// through exactly what a screenshot does - the same engine, the same second
/// looks, the same screen recognisers - and is overwritten by the next, so
/// nothing of the screen is kept on disk.
/// </para>
/// <para>
/// What is kept is the prototype's one screen, the mining scan panel, one
/// reading a rock (see <see cref="LiveRocks"/>). Every other kind is read and
/// shown as the last thing seen, and not stored: that is the next decision,
/// screen by screen, rather than a flood into the reading log by default.
/// </para>
/// </remarks>
public sealed class LiveScreenService(
    LiveScreenStore settings,
    ScreenInsightService insight,
    ScreenReadingStore readings,
    ILogger<LiveScreenService> logger,
    IGameScreenCapture? capture = null) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(2);

    private readonly LiveRocks _rocks = new();
    private readonly Lock _gate = new();
    private LiveScreenStatus _status = new("off");

    /// <summary>Whether this copy can read the screen live at all.</summary>
    public bool Available => capture is not null && insight.CanReadScreenshots;

    /// <summary>Why it cannot, or null.</summary>
    public string? Unavailable => capture is null
        ? "only the Windows app can read the screen - this copy is the server on its own"
        : insight.CanReadScreenshots ? null : "this copy has no text engine to read the screen with";

    public LiveScreenStatus Status
    {
        get { lock (_gate) return _status; }
    }

    /// <summary>
    /// The answer, whether there is one, and what reading is doing - what
    /// <c>/api/live</c> returns. <c>Ask</c> is the dashboard's cue: a copy that
    /// could read the screen and has never been told whether to.
    /// </summary>
    public LiveScreenState Describe(LiveScreenStore store)
    {
        var answer = store.Current;
        return new LiveScreenState(Available, Unavailable, answer.Asked, answer.Enabled, answer.AnsweredAt,
            Available && !answer.Asked, Status);
    }

    private void Set(Func<LiveScreenStatus, LiveScreenStatus> change)
    {
        lock (_gate) _status = change(_status);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var frame = Core.AppPaths.In("live", "frame.bmp");
        using var timer = new PeriodicTimer(Every);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!settings.Current.Enabled)
                {
                    Set(s => s with { State = "off", Why = null });
                    Forget(frame);
                    continue;
                }

                if (Unavailable is { } why)
                {
                    Set(s => s with { State = "unavailable", Why = why });
                    continue;
                }

                await Tick(frame, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        finally
        {
            Forget(frame);
        }
    }

    private async Task Tick(string frame, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(frame)!);

        GameCapture taken;
        try
        {
            taken = capture!.Capture(frame);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Live capture failed");
            Set(s => s with { State = "waiting", Why = $"the screen could not be copied ({e.GetType().Name})" });
            return;
        }

        if (!taken.Taken)
        {
            Set(s => s with { State = "waiting", Why = taken.Why });
            return;
        }

        var at = DateTimeOffset.UtcNow;
        ScreenSighting sighting;
        try
        {
            sighting = await insight.ReadLiveAsync(frame, $"live-{at:yyyyMMdd-HHmmss}", at, token);
        }
        catch (ScreenEngineUnavailableException e)
        {
            Set(s => s with { State = "waiting", Why = e.Message });
            return;
        }

        var kept = 0;
        if (sighting.Mining is { } scan && _rocks.Keep(scan, at) is { } shot)
        {
            readings.Add(sighting with { Shot = shot });
            kept = 1;
            logger.LogInformation("Live scan kept as {Shot}: {Summary}", shot, sighting.Summary);
        }

        Set(s => s with
        {
            State = "reading",
            Why = null,
            LastFrameAt = at,
            LastKind = sighting.Kind.ToString(),
            LastSummary = sighting.Summary,
            Frames = s.Frames + 1,
            Kept = s.Kept + kept,
            LastReadMs = sighting.TookMs,
        });
    }

    /// <summary>The frame on disk goes when reading stops: nothing of the screen outlives the switch.</summary>
    private static void Forget(string frame)
    {
        try
        {
            if (File.Exists(frame)) File.Delete(frame);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Overwritten by the next frame, or deleted on the next tick.
        }
    }
}
