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
/// <param name="Fracture">The fracture HUD as the mining watch has it: the charge against its window, cracks, what was said.</param>
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
    long LastPanelMs = 0,
    MiningPicture? Fracture = null);

/// <summary>What <c>/api/live</c> says.</summary>
/// <param name="Voice">Whether callouts are spoken at all.</param>
/// <param name="VoiceCombat">Whether combat callouts are, under it.</param>
/// <param name="VoiceMining">Whether mining callouts are, under it.</param>
/// <param name="CanSpeak">Whether this copy has a voice at all - the Windows app does, the server alone does not.</param>
/// <param name="VoiceTrouble">Why the last callout was not heard, when it was not.</param>
/// <param name="VoiceMissions">Whether mission callouts are, under it.</param>
/// <param name="Callouts">Every sound the voice can make and whether it is on, for Settings → Callouts.</param>
public sealed record LiveScreenState(
    bool Available,
    string? Unavailable,
    bool Asked,
    bool Enabled,
    DateTimeOffset? AnsweredAt,
    bool Ask,
    LiveScreenStatus Status,
    bool Voice = false,
    bool CanSpeak = false,
    string? VoiceTrouble = null,
    bool VoiceCombat = true,
    bool VoiceMining = true,
    bool VoiceMissions = true,
    IReadOnlyList<CalloutSwitch>? Callouts = null,
    PingSettings? Pings = null);

/// <summary>How pings are read, for Mining → Radar.</summary>
/// <param name="Place">The pilot's choice: null for where the log puts them, "*" for every rock, or a place.</param>
/// <param name="Here">The place the log puts them at now, when it is one with a deposit table.</param>
/// <param name="Places">Every place with mineables, to pick from.</param>
/// <param name="Wanted">The rocks marked wanted, by the name the voice says.</param>
/// <param name="WantedOnly">Whether only those are said.</param>
public sealed record PingSettings(string? Place, string? Here, IReadOnlyList<PingPlaceName> Places, IReadOnlyList<string> Wanted, bool WantedOnly);

/// <summary>A place to pick, with its system and whether it is an asteroid field.</summary>
public sealed record PingPlaceName(string Name, string? System, bool Asteroids);

/// <summary>How pings are read, posted to <c>/api/live/ping</c>; anything left out stays as it was.</summary>
/// <param name="Place">"auto", "*" or a place's name.</param>
public sealed record LivePing(string? Place = null, IReadOnlyList<string>? Wanted = null, bool? WantedOnly = null);

/// <summary>One row of Settings → Callouts.</summary>
/// <param name="On">Its own switch; the voice and the group's switch still have to be on too.</param>
/// <param name="Voice">Which voice says its sample: the recorded pack's name, or "windows" for a sentence the pack lacks a word of.</param>
public sealed record CalloutSwitch(string Kind, string Group, string Name, string Sample, bool On, string? Voice);

/// <summary>The pilot's answer, posted to <c>/api/live</c>.</summary>
public sealed record LiveAnswer(bool Enabled);

/// <summary>The voice switch, posted to <c>/api/live/voice</c>.</summary>
/// <param name="Combat">The combat callouts' own switch; left as it was when not sent.</param>
/// <param name="Mining">The mining callouts' own switch; left as it was when not sent.</param>
/// <param name="Missions">The mission callouts' own switch; left as it was when not sent.</param>
public sealed record LiveVoice(bool Enabled, bool? Combat = null, bool? Mining = null, bool? Missions = null);

/// <summary>One callout switched on or off, posted to <c>/api/live/callout</c>.</summary>
public sealed record LiveCallout(string Kind, bool On);

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
    ICalloutVoice? voice = null,
    NearbyHulls? nearby = null,
    LiveSessionService? session = null) : BackgroundService
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
    private ScreenPatch? _bar;
    private readonly MiningWatch _mining = new();
    private readonly HudWatch _hud = new();
    private int _frameHeight;
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
        lock (_gate) status = status with { Fracture = _mining.Picture() };

        return new LiveScreenState(Available, Unavailable, answer.Asked, answer.Enabled, answer.AnsweredAt,
            Available && !answer.Asked, status, answer.Voice, voice is not null, voice?.Trouble,
            answer.VoiceCombat, answer.VoiceMining, answer.VoiceMissions,
            [.. Callouts.All.Select(k => new CalloutSwitch(k.Kind, k.Group, k.Name, k.Sample, !answer.IsMuted(k.Kind), voice?.VoiceFor(k.Sample)))],
            new PingSettings(answer.PingPlace, Here()?.Name,
                [.. insight.PingPlaces().Select(p => new PingPlaceName(p.Name, p.System, p.Asteroids))],
                answer.Wanted ?? [], answer.WantedOnly));
    }

    /// <summary>The place with a deposit table the log puts the pilot at: the body, the place, then where a jump is headed.</summary>
    private PingPlace? Here()
    {
        if (session?.Current is not { } now) return null;
        return PingPlaces.Find(insight.PingPlaces(), now.LocationBody, now.Location, now.TravellingTo);
    }

    /// <summary>The rows a ping is read against now, and how: the pilot's place, the log's, or every rock.</summary>
    private (IReadOnlyList<Quantumwake.Core.GameData.RadarRow> Rows, PingOptions Options) PingRows()
    {
        var answer = settings.Current;
        var rows = insight.RadarRows();
        var wanted = answer.Wanted is { Count: > 0 } w ? new HashSet<string>(w, StringComparer.Ordinal) : null;

        PingPlace? place;
        var chosen = answer.PingPlace is not null;
        if (answer.PingPlace == LiveScreenSettings.Anywhere) place = null;
        else if (chosen) place = PingPlaces.Find(insight.PingPlaces(), answer.PingPlace);
        else place = Here();

        // Narrowed in the mining watch, which falls back to every rock when the place reads nothing.
        return (rows, new PingOptions(place, chosen, wanted, answer.WantedOnly));
    }

    /// <summary>
    /// Says a sentence through the voice now, so the pilot can hear what it
    /// sounds like before a fight: the sample of <paramref name="kind"/>, or a
    /// target identified. Said whatever the switches are, because asking to
    /// hear one is the point.
    /// </summary>
    public bool TestVoice(string? kind = null)
    {
        if (voice is null) return false;
        voice.Say(Callouts.Find(kind)?.Sample ?? "Target identified: Vanduul Blade");
        return true;
    }

    /// <summary>
    /// Says what a watch said, when its sound is on, and keeps a fight's with
    /// the combat watch's so the page lists them together.
    /// </summary>
    private void Speak(IEnumerable<CombatCallout> said, string what, bool keep = false)
    {
        foreach (var callout in said)
        {
            logger.LogInformation("{What} callout: {Text}", what, callout.Text);
            if (keep) lock (_gate) _combat?.Note(callout);
            if (settings.Current.Speaks(callout.Kind)) voice?.Say(callout.Text);
        }
    }

    /// <summary>Whether a fight is on: the territory banner lately, or a hostile lock.</summary>
    private bool InAFight(DateTimeOffset at)
    {
        if (_hud.Hostile(at)) return true;
        lock (_gate) return _combat?.Picture(at).Combat == true;
    }

    /// <summary>The missile band read up close, and anything it says.</summary>
    private async Task Missile(string frame, int width, int height, DateTimeOffset at, CancellationToken token)
    {
        var showing = await insight.ReadMissileAsync(frame, width, height, token);
        IReadOnlyList<CombatCallout> said;
        lock (_gate) said = _hud.SeeMissile(showing, at);
        Speak(said, "HUD", keep: true);
    }

    /// <summary>A whole frame's HUD text: the missile warning, the territory banner, a mission's health.</summary>
    private void Hud(IReadOnlyList<ScreenTextLine> lines, int width, int height, DateTimeOffset at)
    {
        IReadOnlyList<CombatCallout> missile, whole;
        lock (_gate)
        {
            missile = _hud.SeeMissile(HudText.Missile(lines, width, height), at);
            whole = _hud.SeeWhole(HudText.HostileTerritory(lines, width, height), HudText.Health(lines, width, height), at,
                HudText.FlightHud(lines));
        }
        Speak(missile.Concat(whole), "HUD", keep: true);
    }

    private void Set(Func<LiveScreenStatus, LiveScreenStatus> change)
    {
        lock (_gate) _status = change(_status);
    }

    private bool? _standingBack;

    /// <summary>
    /// Below-normal priority for the whole app while the screen is being read
    /// (1.1.17), so the game is first to every core when both want one - the
    /// reads, the OCR engine's threads and the dashboard alike. Back to normal
    /// when reading is off. A pilot felt the game lag with reading on.
    /// </summary>
    private void StandBack(bool reading)
    {
        if (_standingBack == reading || !OperatingSystem.IsWindows()) return;
        _standingBack = reading;
        try
        {
            using var self = System.Diagnostics.Process.GetCurrentProcess();
            self.PriorityClass = reading
                ? System.Diagnostics.ProcessPriorityClass.BelowNormal
                : System.Diagnostics.ProcessPriorityClass.Normal;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // Only a courtesy to the game; reading goes on at normal priority.
        }
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

                StandBack(settings.Current.Enabled);

                if (!settings.Current.Enabled)
                {
                    Set(s => s with { State = "off", Why = null });
                    _log.Stopped();
                    _panel = null;
                    _bar = null;
                    Forget(frame);
                    continue;
                }

                if (Unavailable is { } why)
                {
                    Set(s => s with { State = "unavailable", Why = why });
                    _log.Waiting(why);
                    continue;
                }

                // Out of a fight or a fracture, the pace it always had: a frame
                // every two seconds. In a fight with nothing locked, the
                // missile band on the seconds between - the warning was up
                // about two seconds at a time on the recording, and a
                // whole-frame read every other second misses half of those.
                if (_panel is null && _bar is null && _tick % 2 != 0)
                {
                    if (InAFight(DateTimeOffset.UtcNow)) await Tick(frame, stoppingToken, bandOnly: true);
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

    private async Task Tick(string frame, CancellationToken token, bool bandOnly = false)
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
        _frameHeight = taken.Height;

        lock (_gate)
        {
            _combat ??= new CombatWatch(insight.SignatureBook());
            _combat.Book = insight.SignatureBook();
            if (nearby is not null)
            {
                _combat.Nearby = () => nearby.Classes(DateTimeOffset.UtcNow);
                _combat.Npcs = () => nearby.Npcs(DateTimeOffset.UtcNow);
            }
        }

        if (bandOnly)
        {
            var band = System.Diagnostics.Stopwatch.StartNew();
            await Missile(frame, taken.Width, taken.Height, at, token);
            _log.Read("Missile", copying.ElapsedMilliseconds, band.ElapsedMilliseconds, taken.Width, taken.Height, null);
            return;
        }

        // ---- the fracture HUD: pixels only, so every tick while a bar is held ----
        var charging = System.Diagnostics.Stopwatch.StartNew();
        var charge = _bar is { } bar
            ? await insight.ReadChargeAsync(frame, ChargeWindow.Around(bar), taken.Height, token)
            : null;
        // Not where it was, or not looked for yet: the whole search area, still pixels only.
        charge ??= await insight.ReadChargeAsync(frame, ChargeWindow.SearchArea(taken.Width, taken.Height), taken.Height, token);
        charging.Stop();
        _bar = charge?.Bar;
        Fracture(charge, at);

        // ---- the missile band, on the ticks that do not read the whole frame ----
        // A whole-frame read sees the warning in its own lines; the ticks
        // between read only a patch, so the band is read up close beside it.
        if (_tick % WholeEvery != 0 && (_panel is not null || (charge is not null && InAFight(at))))
            await Missile(frame, taken.Width, taken.Height, at, token);

        if (charge is not null && _panel is null && _tick % WholeEvery != 0)
        {
            _log.Read("Fracture", copying.ElapsedMilliseconds, charging.ElapsedMilliseconds, taken.Width, taken.Height, null);
            Set(s => s with
            {
                State = "reading", Why = null, LastFrameAt = at, LastKind = "Fracture",
                LastSummary = $"charge {charge.State} the window, crack {charge.Fill:P0}", Frames = s.Frames + 1,
            });
            return;
        }

        // ---- the panel alone, while there is one to follow ----
        if (_panel is { } known && _tick % WholeEvery != 0)
        {
            var close = System.Diagnostics.Stopwatch.StartNew();
            var (panel, stance) = await insight.ReadTargetAsync(frame, null, known, taken.Width, taken.Height, token);
            close.Stop();

            if (panel is not null)
            {
                See(panel, stance, at, await Hologram(frame, panel, taken.Height, token));
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

        Hud(lines, taken.Width, taken.Height, at);

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
            See(target, stance, at, await Hologram(frame, target, taken.Height, token));
        }
        else
        {
            See(null, TargetStance.Unread, at);
        }

        // The radar's signature readout, when nothing faster is going on: a
        // ping is read while the pilot sweeps across contacts, and a fight or
        // a fracture has the loop's attention.
        if (target is null && charge is null && sighting.Mining is null)
        {
            var ping = await insight.ReadPingAsync(frame, taken.Width, taken.Height, token);
            IReadOnlyList<CombatCallout> pinged;
            var (rows, options) = PingRows();
            lock (_gate) pinged = _mining.SeePing(ping, rows, at, options);
            Speak(pinged, "Ping");
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

    /// <summary>
    /// The game's own hologram of the locked ship, cut from the frame just
    /// read, as a PNG; null when reading is off or no panel is held.
    /// </summary>
    /// <remarks>
    /// The hologram sits under the target panel - 40 to 230 below the
    /// VELOCITY row and 80 left of it to 340 right, at 1440 high, which holds
    /// it on both HUDs in the recording though they centre it differently -
    /// and it is the one picture of the target that is always
    /// there: a fifth of the hulls have no vehicle icon in the game files
    /// (the Paladin, the Perseus and the Scythe among them). It also shows
    /// the damage, which a silhouette cannot. Cut in memory from the frame on
    /// request and never written anywhere.
    /// </remarks>
    public byte[]? Hologram()
    {
        if (!settings.Current.Enabled || _panel is not { } panel || _frameHeight <= 0)
            return null;

        var frame = Core.AppPaths.In("live", "frame.bmp");
        try
        {
            byte[] bytes;
            using (var stream = new FileStream(frame, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                bytes = copy.ToArray();
            }

            using var bitmap = SkiaSharp.SKBitmap.Decode(bytes);
            if (bitmap is null) return null;

            var u = _frameHeight / 1440.0;
            var rect = SkiaSharp.SKRectI.Intersect(
                new SkiaSharp.SKRectI(0, 0, bitmap.Width, bitmap.Height),
                SkiaSharp.SKRectI.Create((int)(panel.Left + 120 * u), (int)(panel.Top + 230 * u), (int)(420 * u), (int)(190 * u)));
            if (rect.Width <= 0 || rect.Height <= 0) return null;

            using var cut = new SkiaSharp.SKBitmap(rect.Width, rect.Height);
            if (!bitmap.ExtractSubset(cut, rect)) return null;
            using var image = SkiaSharp.SKImage.FromBitmap(cut);
            using var png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
            return png.ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Mid-write, or gone because reading just stopped: the next ask gets the next frame.
            return null;
        }
    }

    /// <summary>Hands a frame's charge to the mining watch, and anything it says to the voice.</summary>
    private void Fracture(ChargeReading? charge, DateTimeOffset at)
    {
        IReadOnlyList<CombatCallout> said;
        lock (_gate) said = _mining.See(charge, at);
        Speak(said, "Mining");
    }

    /// <summary>
    /// The hologram under a panel just read, judged for damage: pixels only, a
    /// few milliseconds beside the panel read, so every frame with a panel.
    /// </summary>
    private async Task<HologramReading?> Hologram(string frame, TargetReading? panel, int height, CancellationToken token)
    {
        if (panel?.Panel is not { } where) return null;
        try
        {
            return await insight.ReadHologramAsync(frame, where, height, token);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // A hologram not judged is a callout not made; the panel stands.
            logger.LogDebug(e, "Hologram not read");
            return null;
        }
    }

    /// <summary>Hands a frame's panel to the combat watch, and anything it says to the voice.</summary>
    private void See(TargetReading? panel, TargetStance stance, DateTimeOffset at, HologramReading? hologram = null)
    {
        _panel = panel?.Panel;

        IReadOnlyList<CombatCallout> said;
        lock (_gate) said = _combat!.See(panel, stance, at, hologram);
        Speak(said, "Combat");
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
