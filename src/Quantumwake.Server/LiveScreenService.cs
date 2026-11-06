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
/// <param name="Combat">The target panel as the combat watch has it: the lock, whether it is a fight, what was said.</param>
/// <param name="LastPanelMs">How long the last close read of the target panel took.</param>
public sealed record LiveScreenStatus(
    string State,
    string? Why = null,
    DateTimeOffset? LastFrameAt = null,
    string? LastKind = null,
    string? LastSummary = null,
    int Frames = 0,
    int Kept = 0,
    long LastReadMs = 0,
    CombatPicture? Combat = null,
    long LastPanelMs = 0);

/// <summary>What <c>/api/live</c> says.</summary>
/// <param name="Voice">Whether combat callouts are spoken.</param>
/// <param name="CanSpeak">Whether this copy has a voice at all - the Windows app does, the server alone does not.</param>
public sealed record LiveScreenState(
    bool Available,
    string? Unavailable,
    bool Asked,
    bool Enabled,
    DateTimeOffset? AnsweredAt,
    bool Ask,
    LiveScreenStatus Status,
    bool Voice = false,
    bool CanSpeak = false);

/// <summary>The pilot's answer, posted to <c>/api/live</c>.</summary>
public sealed record LiveAnswer(bool Enabled);

/// <summary>The voice switch, posted to <c>/api/live/voice</c>.</summary>
public sealed record LiveVoice(bool Enabled);

/// <summary>
/// Reads the game's screen while the pilot has said yes to it.
/// </summary>
/// <remarks>
/// <para>
/// Only while Star Citizen is the window in front: <see cref="IGameScreenCapture"/>
/// refuses otherwise, so alt-tabbing to the dashboard stops it without anything
/// being switched. The frame goes through exactly what a screenshot does - the
/// same engine, the same second looks, the same screen recognisers - and is
/// overwritten by the next, so nothing of the screen is kept on disk.
/// </para>
/// <para>
/// What is kept is the prototype's one screen, the mining scan panel, one
/// reading a rock (see <see cref="LiveRocks"/>). Every other kind is read and
/// shown as the last thing seen, and not stored: that is the next decision,
/// screen by screen, rather than a flood into the reading log by default.
/// </para>
/// <para>
/// The cockpit's target panel sets a faster pace. A fight changes by the
/// second and the whole frame takes about a second to read at 3440x1440, so
/// once the panel has been found the loop reads only its patch - about 130 ms
/// on the 2026-11-05 recording - every second, and the whole frame every
/// third second, for the lock label's colour and everything else. Out of a
/// fight it reads a whole frame every two seconds, as it always did. Nothing
/// of a fight is stored: the combat watch keeps the current lock and the last
/// few callouts in memory, and the voice, when switched on, says them.
/// </para>
/// </remarks>
public sealed class LiveScreenService(
    LiveScreenStore settings,
    ScreenInsightService insight,
    ScreenReadingStore readings,
    ILogger<LiveScreenService> logger,
    IGameScreenCapture? capture = null,
    LiveFrameLog? frames = null,
    ICalloutVoice? voice = null) : BackgroundService
{
    /// <summary>The tick, at a fight's pace. Out of a fight every other tick is skipped.</summary>
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(1);

    /// <summary>With a panel to follow, the whole frame is read once in this many ticks and the patch on the rest.</summary>
    private const int WholeEvery = 3;

    /// <summary>One line a frame, in the data folder's <c>live</c> folder; see <see cref="LiveFrameLog"/>.</summary>
    private readonly LiveFrameLog _log = frames ?? new LiveFrameLog(Core.AppPaths.In("live"));

    private readonly LiveRocks _rocks = new();
    private readonly Lock _gate = new();
    private LiveScreenStatus _status = new("off");
    private CombatWatch? _combat;
    private ScreenPatch? _panel;
    private long _tick;

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
        var status = Status;

        CombatPicture? picture;
        lock (_gate) picture = _combat?.Picture(DateTimeOffset.UtcNow);
        if (picture is not null) status = status with { Combat = picture };

        return new LiveScreenState(Available, Unavailable, answer.Asked, answer.Enabled, answer.AnsweredAt,
            Available && !answer.Asked, status, answer.Voice, voice is not null);
    }

    /// <summary>Says a sentence through the voice now, so the pilot can hear what it sounds like before a fight.</summary>
    public bool TestVoice()
    {
        if (voice is null) return false;
        voice.Say("Target identified: Vanduul Blade");
        return true;
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
                _tick++;

                if (!settings.Current.Enabled)
                {
                    Set(s => s with { State = "off", Why = null });
                    _log.Stopped();
                    _panel = null;
                    Forget(frame);
                    continue;
                }

                if (Unavailable is { } why)
                {
                    Set(s => s with { State = "unavailable", Why = why });
                    _log.Waiting(why);
                    continue;
                }

                // Out of a fight, the pace it always had: a frame every two seconds.
                if (_panel is null && _tick % 2 != 0)
                    continue;

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
        var copying = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            taken = capture!.Capture(frame);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Live capture failed");
            var failed = $"the screen could not be copied ({e.GetType().Name})";
            Set(s => s with { State = "waiting", Why = failed });
            _log.Waiting(failed);
            return;
        }
        copying.Stop();

        if (!taken.Taken)
        {
            Set(s => s with { State = "waiting", Why = taken.Why });
            _log.Waiting(taken.Why);
            return;
        }

        var at = DateTimeOffset.UtcNow;

        lock (_gate)
        {
            _combat ??= new CombatWatch(insight.SignatureBook());
            _combat.Book = insight.SignatureBook();
        }

        // ---- the panel alone, while there is one to follow ----
        if (_panel is { } known && _tick % WholeEvery != 0)
        {
            var close = System.Diagnostics.Stopwatch.StartNew();
            var (panel, stance) = await insight.ReadTargetAsync(frame, null, known, taken.Width, taken.Height, token);
            close.Stop();

            if (panel is not null)
            {
                See(panel, stance, at);
                _log.Read("Target", copying.ElapsedMilliseconds, close.ElapsedMilliseconds, taken.Width, taken.Height, null);
                Set(s => s with
                {
                    State = "reading", Why = null, LastFrameAt = at, LastKind = "Target",
                    LastSummary = Describe(panel), Frames = s.Frames + 1, LastPanelMs = close.ElapsedMilliseconds,
                });
                return;
            }

            // Lost: the whole frame says whether it moved or went away.
            _panel = null;
        }

        // ---- the whole frame ----
        ScreenSighting sighting;
        IReadOnlyList<ScreenTextLine> lines;
        try
        {
            (sighting, lines) = await insight.ReadLiveFrameAsync(frame, $"live-{at:yyyyMMdd-HHmmss}", at, token);
        }
        catch (ScreenEngineUnavailableException e)
        {
            Set(s => s with { State = "waiting", Why = e.Message });
            _log.Waiting(e.Message);
            return;
        }

        var kept = 0;
        string? keptAs = null;
        if (sighting.Mining is { } scan && _rocks.Keep(scan, at) is { } shot)
        {
            readings.Add(sighting with { Shot = shot });
            kept = 1;
            keptAs = shot;
            logger.LogInformation("Live scan kept as {Shot}: {Summary}", shot, sighting.Summary);
        }

        var panelMs = 0L;
        TargetReading? target = null;
        if (sighting.Mining is null)
        {
            var close = System.Diagnostics.Stopwatch.StartNew();
            (target, var stance) = await insight.ReadTargetAsync(frame, lines, _panel, taken.Width, taken.Height, token);
            close.Stop();
            panelMs = close.ElapsedMilliseconds;
            See(target, stance, at);
        }
        else
        {
            See(null, TargetStance.Unread, at);
        }

        var kind = target is not null && sighting.Kind == ScreenKind.Unknown ? "Target" : sighting.Kind.ToString();
        _log.Read(kind, copying.ElapsedMilliseconds, sighting.TookMs + panelMs, taken.Width, taken.Height, keptAs);

        Set(s => s with
        {
            State = "reading",
            Why = null,
            LastFrameAt = at,
            LastKind = kind,
            LastSummary = target is not null && sighting.Kind == ScreenKind.Unknown ? Describe(target) : sighting.Summary,
            Frames = s.Frames + 1,
            Kept = s.Kept + kept,
            LastReadMs = sighting.TookMs,
            LastPanelMs = panelMs,
        });
    }

    /// <summary>Hands a frame's panel to the combat watch, and anything it says to the voice.</summary>
    private void See(TargetReading? panel, TargetStance stance, DateTimeOffset at)
    {
        _panel = panel?.Panel;

        IReadOnlyList<CombatCallout> said;
        lock (_gate) said = _combat!.See(panel, stance, at);

        foreach (var callout in said)
        {
            logger.LogInformation("Combat callout: {Text}", callout.Text);
            if (settings.Current.Voice) voice?.Say(callout.Text);
        }
    }

    private static string Describe(TargetReading panel) =>
        string.Join(" · ", new[]
        {
            panel.Name ?? "UNKNOWN",
            panel.SpeedMs is { } v ? $"{v:0} m/s" : null,
            panel.RangeKm is { } r ? $"{r:0.00} km" : null,
            panel.CrossSection is { } cs ? $"CS {cs / 1000:0.0}K" : null,
        }.Where(p => p is not null));

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
