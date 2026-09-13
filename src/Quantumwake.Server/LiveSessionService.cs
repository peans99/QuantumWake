using Microsoft.AspNetCore.SignalR;
using Quantumwake.Core.Events;
using Quantumwake.Core.Logging;
using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>The live "where am I right now" snapshot pushed to clients.</summary>
public sealed record NowState
{
    public bool Connected { get; init; }
    public bool InGame { get; init; }
    public string? GameRules { get; init; }
    public string? Handle { get; init; }
    public string? GameVersion { get; init; }

    public string? Location { get; init; }
    public string? LocationBody { get; init; }
    public string? LocationSystem { get; init; }
    public string? LocationId { get; init; }
    public string Confidence { get; init; } = "None";

    public bool Travelling { get; init; }
    public string? TravellingTo { get; init; }
    public string? TravellingToId { get; init; }

    public string? Ship { get; init; }
    public DateTimeOffset? SessionStarted { get; init; }
    public int Incapacitations { get; init; }

    /// <summary>Deaths, detected from corpse item-recovery bursts.</summary>
    public int Deaths { get; init; }

    /// <summary>Always zero on SC 4.9 and 4.10 - no event identifies a killer any more.</summary>
    public int Kills { get; init; }

    public IReadOnlyList<TimelineEntry> RecentEvents { get; init; } = [];

    /// <summary>
    /// Everyone the party channel has named this session, most recent first.
    /// </summary>
    /// <remarks>
    /// Not a roster, and the view must not present it as one. A party member who
    /// was already online when you grouped up and never dropped produces no
    /// toast at all, so this is a floor: everyone here was mentioned, and being
    /// absent from it means nothing either way.
    /// </remarks>
    public IReadOnlyList<PartySighting> Party { get; init; } = [];

    /// <summary>True once a "Party Disbanded" toast has been seen this session.</summary>
    public bool PartyDisbanded { get; init; }

    /// <summary>The newest screenshot reading, or null when none has been taken.</summary>
    public NowScreen? Screen { get; init; }

    /// <summary>
    /// Contracts still open in this session, newest first.
    /// </summary>
    /// <remarks>
    /// This session only, and deliberately: /api/contracts reads the store, and
    /// the store gains a session on log rotation - so the contract being flown
    /// right now is the one thing that report cannot show. Filtered to the open
    /// ones and capped, because this rides a snapshot pushed every second and a
    /// history here would be a report inside a heartbeat.
    /// </remarks>
    public IReadOnlyList<NowContract> Contracts { get; init; } = [];

    /// <summary>What the kiosks recorded moving this session, or null when nothing has.</summary>
    public NowCargo? Cargo { get; init; }
}

/// <summary>One contract this session opened and has not closed.</summary>
/// <param name="Steps">
/// Journal objectives and how many of them finished. Zero steps means the
/// journal reported none, which is not the same as none remaining - so a view
/// showing "0 of 0" would be inventing progress, and must say the game was
/// quiet instead.
/// </param>
public sealed record NowContract(
    string Name,
    string Issuer,
    string? Type,
    string? Difficulty,
    int Steps,
    int StepsDone,
    DateTimeOffset Since)
{
    /// <summary>
    /// The contracts a session took and has not closed, newest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every <see cref="ContractRecord"/> comes from an objective marker, and
    /// the game creates objective markers for missions in the journal - so
    /// being here is what "taken" means. Open is then the outcome the logs
    /// never closed.
    /// </para>
    /// <para>
    /// Not <see cref="ContractRecord.Accepted"/>, which nothing sets: filtering
    /// on it returns an empty list for every session ever recorded. Found by
    /// running this page against a real install and getting no contracts out of
    /// a log carrying 24 acceptance toasts.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<NowContract> OpenIn(SessionSummary summary) =>
        [.. summary.Contracts
            .Where(c => c.CompletedAt is null
                && c.Outcome is ContractOutcome.Unknown or ContractOutcome.InProgress)
            .OrderByDescending(c => c.FirstSeen)
            .Take(6)
            .Select(c => new NowContract(
                // The annotations come off the title here for the same reason
                // the logbook takes them off: a contract reads as its own name.
                ContractTags.Clean(c.DisplayName),
                c.Issuer,
                c.Type,
                c.Difficulty,
                c.Steps,
                c.StepsDone,
                c.FirstSeen))];
}

/// <summary>
/// The commodity counters' own account of this session.
/// </summary>
/// <remarks>
/// Not a manifest, and nothing built on it may be shown as one. Game.log never
/// states what is in a hold: these are buy and sell requests at a kiosk, so a
/// haul bought last session, transferred from another ship or blown out of the
/// back is invisible either way. Every figure here is a floor, over this
/// session alone.
/// </remarks>
public sealed record NowCargo(int BoughtScu, int SoldScu, NowKioskMove? Last)
{
    /// <summary>What the commodity counters moved this session, or null if none did.</summary>
    /// <param name="name">
    /// Resolves a logged resource id to the game's own word for it - see
    /// <see cref="LogLibrary.CommodityName"/>. Passed in rather than reached
    /// for so this stays a function of the session it is given.
    /// </param>
    /// <remarks>
    /// The last move is the newest by timestamp rather than the last in the
    /// list: the list is appended as events arrive, and the one thing this must
    /// not do is call an older receipt the current one.
    /// </remarks>
    public static NowCargo? From(SessionSummary summary, Func<string?, string?> name)
    {
        if (summary.Trades.Count == 0) return null;

        var last = summary.Trades.MaxBy(trade => trade.At)!;

        return new NowCargo(
            summary.Trades.Where(t => !t.IsSell).Sum(t => t.Quantity),
            summary.Trades.Where(t => t.IsSell).Sum(t => t.Quantity),
            new NowKioskMove(last.At, last.Shop, last.IsSell, last.Quantity, last.Amount, name(last.ResourceId)));
    }
}

/// <summary>The last thing a commodity counter was asked to move.</summary>
/// <param name="Commodity">
/// The resolved name, or null when the id names nothing the install or the
/// dataset knows - see <see cref="LogLibrary.CommodityName"/>. Never the raw
/// id: an unresolved id shown as a name is a cargo nobody carried.
/// </param>
public sealed record NowKioskMove(
    DateTimeOffset At, string Shop, bool Sell, int Scu, decimal Amount, string? Commodity);

/// <summary>The newest screenshot reading, for the Now card and the widget.</summary>
/// <remarks>
/// Without the frame's own text, deliberately. This rides a snapshot pushed
/// every second to every client, and seventy lines of OCR per push is a great
/// deal of nothing. The full reading is a fetch away on the settings page.
/// </remarks>
public sealed record NowScreen(
    string Shot,
    DateTimeOffset ShotAt,
    string Kind,
    string Summary,
    IReadOnlyList<ScreenCheck> Checks)
{
    /// <summary>How many checks the screen and the logs disagreed on.</summary>
    public int Differs => Checks.Count(c => c.Verdict == "differs");
}

/// <summary>The last thing the party channel said about one player.</summary>
/// <param name="Moment">
/// The <see cref="PartyMoment"/> name, lowercased for display.
/// </param>
public sealed record PartySighting(string Handle, string Moment, DateTimeOffset At);

/// <summary>SignalR hub clients subscribe to for live updates.</summary>
public sealed class LiveHub : Hub
{
}

/// <summary>
/// Tails the live Game.log and broadcasts state to connected clients.
/// </summary>
/// <remarks>
/// Keeps a <see cref="SessionBuilder"/> for the session in progress, so the Now
/// view and the historical views share exactly one aggregation implementation.
/// On log rotation the finished session is persisted and a fresh builder starts.
/// </remarks>
public sealed class LiveSessionService : BackgroundService
{
    private readonly IHubContext<LiveHub> _hub;
    private readonly LogLibrary _library;
    private readonly GameInstall? _install;
    private readonly ILogger<LiveSessionService> _logger;
    private readonly TripStore? _trips;
    private readonly Lock _gate = new();

    private SessionBuilder _builder;
    private LogTailer? _tailer;
    private string? _currentShip;
    private readonly List<TimelineEntry> _recent = [];

    /// <summary>Where the player was last seen, so an arrival fires once.</summary>
    private string? _lastPlace;

    private readonly ScreenReadingStore? _screen;
    private readonly ScreenSettingsStore? _screenSettings;

    /// <summary>The newest screenshot already accounted for.</summary>
    private string? _lastScreenShot;

    /// <summary>
    /// Disagreements worth announcing, oldest first.
    /// </summary>
    /// <remarks>
    /// Kept beside the session's own timeline rather than in it: a screenshot
    /// is not something that happened in the game, and it must not end up in
    /// a saved session's history.
    /// </remarks>
    private readonly List<TimelineEntry> _screenNotes = [];

    /// <param name="install">
    /// Defaulted so the container can still build this service on a machine
    /// where no install was found: the live tail has nothing to follow, and
    /// the rest of the app runs regardless.
    /// </param>
    /// <param name="trips">
    /// Optional, so the container can build this service before flight plans
    /// exist as a concept - the live tail works with or without one.
    /// </param>
    /// <param name="screen">
    /// What the screenshots said. Optional for the same reason as the rest:
    /// a server with no reader has none, and the Now card then stays hidden.
    /// </param>
    /// <param name="screenSettings">
    /// Whether the pilot has the screen panel switched on. Optional like the
    /// rest; absent means nothing is filtered, which is what the tests want.
    /// </param>
    public LiveSessionService(
        IHubContext<LiveHub> hub,
        LogLibrary library,
        ILogger<LiveSessionService> logger,
        GameInstall? install = null,
        TripStore? trips = null,
        ScreenReadingStore? screen = null,
        ScreenSettingsStore? screenSettings = null)
    {
        _hub = hub;
        _library = library;
        _install = install;
        _logger = logger;
        _trips = trips;
        _screen = screen;
        _screenSettings = screenSettings;
        _builder = new SessionBuilder(install?.GameLogPath ?? "live");

        // Whatever was already read is not news. Seeding this here is what
        // stops the app announcing a week-old disagreement the moment it
        // starts, which is the same mistake the toast code guards against on
        // the other side of the wire.
        _lastScreenShot = screen?.Latest?.Shot;
    }

    /// <summary>Current snapshot, also served over REST for first paint.</summary>
    public NowState Current { get; private set; } = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_install is null || !_install.HasGameLog)
        {
            _logger.LogWarning("No Game.log found; live view disabled.");

            // The screen reader runs without a game log, and since the readings
            // moved onto this channel there is no client-side poll left to fall
            // back on - so without this the Now card and the reading log never
            // see a thing on an install the tail cannot follow. Connected stays
            // false: there is no game here, only screenshots.
            if (_screen is not null)
                await BroadcastScreenOnlyAsync(stoppingToken);

            return;
        }

        _tailer = new LogTailer(_install.GameLogPath);
        _tailer.EventParsed += OnEvent;
        _tailer.Rotated += OnRotated;
        _tailer.Faulted += e => _logger.LogDebug(e, "Transient read failure while tailing.");

        _tailer.Start(fromStart: true);
        _logger.LogInformation("Tailing {Path}", _install.GameLogPath);

        // Push a snapshot periodically so clients see the session clock advance
        // even during quiet stretches.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await WaitAsync(timer, stoppingToken))
            await BroadcastAsync();
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
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

    private void OnEvent(GameEvent ev)
    {
        lock (_gate)
        {
            _builder.Add(ev);

            // Leaving a ship clears it; retrieval is tracked by the builder,
            // which is the only signal a ship swap produces on SC 4.9 and 4.10.
            if (ev is VehicleControlEvent { Change: SeatChange.Left })
                _currentShip = null;

            Current = Snapshot();

            // Landing somewhere crosses that stop off the tracked plan. The app
            // already knows where the player is standing, so asking them to
            // tick a box for it would be asking for data it holds. Fired on the
            // change, not on every event, so a long stay ticks once.
            if (Current.LocationId is { Length: > 0 } here && here != _lastPlace)
            {
                _lastPlace = here;
                _trips?.Arrived(here, Current.Location);
            }
        }
    }

    internal void OnRotated()
    {
        lock (_gate)
        {
            _logger.LogInformation("Game.log rotated; archiving session and restarting.");

            try
            {
                var finished = _builder.Build();
                if (finished.StartedAt != default)
                    _library.Store.Save(finished, $"live:{finished.EndedAt.Ticks}");
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Could not archive the rotated session.");
            }

            _builder = new SessionBuilder(_install?.GameLogPath ?? "live");
            _currentShip = null;
            _recent.Clear();

            // The notes belong to the session that just ended. Left here they
            // would be the whole of the next session's feed, on top of a
            // timeline that correctly says nothing has happened yet.
            // _lastScreenShot is deliberately kept: the readings themselves did
            // not rotate, and clearing it would announce the newest one again.
            _screenNotes.Clear();
        }
    }

    /// <summary>Builds a snapshot. Caller must hold the lock.</summary>
    internal NowState Snapshot()
    {
        var summary = _builder.Build();
        var location = _builder.Location;

        // Keep the tail of the timeline for the live feed.
        _recent.Clear();
        _recent.AddRange(summary.Timeline.TakeLast(40).Reverse());

        // Before the initializer, not inside it: ScreenNow is what appends a
        // disagreement to _screenNotes, and Feed() reads that list. Left to the
        // initializer's own order the note arrived one snapshot after the feed
        // it was written for - which the two-second tick hid, and which the
        // next reordering of these lines would not have.
        var screen = ScreenNow();

        return new NowState
        {
            Connected = true,
            InGame = location.InGame,
            GameRules = location.GameRules,
            Handle = summary.Handle,
            GameVersion = summary.GameVersion,
            Location = location.Current?.DisplayName,
            LocationBody = location.Current?.Body,
            LocationSystem = location.Current?.System,
            LocationId = location.Current?.RawId,
            Confidence = location.Confidence.ToString(),
            Travelling = location.IsTravelling,
            TravellingTo = location.TravellingTo?.DisplayName,
            TravellingToId = location.TravellingTo?.RawId,
            Ship = _builder.CurrentShip ?? _currentShip ?? summary.PrimaryShip,
            SessionStarted = summary.StartedAt == default ? null : summary.StartedAt,
            Incapacitations = summary.Incapacitations,
            Deaths = summary.Deaths,
            Kills = summary.Kills,
            RecentEvents = Feed(),
            Screen = screen,
            Contracts = NowContract.OpenIn(summary),
            Cargo = NowCargo.From(summary, _library.CommodityName),
            Party = ReadParty(summary.PartyNotes),
            PartyDisbanded = summary.PartyNotes.Count > 0
                && summary.PartyNotes[^1].Moment == PartyMoment.Disbanded
        };
    }

    /// <summary>
    /// The session's own timeline and the screen's notes, newest first.
    /// </summary>
    private IReadOnlyList<TimelineEntry> Feed() =>
        [.. _recent.Concat(_screenNotes)
            .OrderByDescending(entry => entry.At)
            .Take(40)
            .Select(Named)];

    /// <summary>Puts an item's name into a sentence written before it was known.</summary>
    /// <remarks>
    /// Done when the feed is served rather than when it is stored, because the
    /// name comes from catalogues in the install that the parser cannot see -
    /// and because a pilot who installs a later patch, or the app that learns a
    /// name it did not have, should get the better sentence without every old
    /// session having to be read again.
    /// </remarks>
    private TimelineEntry Named(TimelineEntry entry)
    {
        if (entry.Subject is not { Length: > 0 } itemClass) return entry;

        var name = _library.ItemName(itemClass);

        return name == itemClass
            ? entry
            : entry with { Text = entry.Text.Replace(itemClass, name, StringComparison.Ordinal) };
    }

    /// <summary>
    /// The newest reading, and a note when it is the first sight of one that
    /// disagrees with the logs.
    /// </summary>
    /// <remarks>
    /// Only disagreements are announced. A pilot photographing a loadout takes
    /// several frames in a row, and a toast apiece would teach them to ignore
    /// the toasts - which is the one thing a notification cannot afford. A
    /// reading that agrees updates the card and says nothing.
    /// </remarks>
    private NowScreen? ScreenNow()
    {
        // Enforced here and not only on the page, for the same reason the scan
        // and clipboard endpoints enforce it: a panel that has been switched
        // off should be switched off however the reading arrives. Without it
        // the last screenshot read before the switch stays on the Now card and
        // in the widget for good.
        if (_screenSettings is not null && _screenSettings.Current.Mode != ScreenMode.Screenshots)
            return null;

        if (_screen?.Latest is not { } latest) return null;

        if (!string.Equals(latest.Shot, _lastScreenShot, StringComparison.OrdinalIgnoreCase))
        {
            _lastScreenShot = latest.Shot;

            if (latest.Checks.FirstOrDefault(check => check.Verdict == "differs") is { } differs)
            {
                _screenNotes.Add(new TimelineEntry(
                    latest.ShotAt,
                    "screen-differs",
                    $"{differs.Subject}: {differs.Claim}",
                    differs.Note ?? differs.Belief));

                if (_screenNotes.Count > 10) _screenNotes.RemoveAt(0);
            }
        }

        return new NowScreen(latest.Shot, latest.ShotAt, latest.Kind.ToString(), latest.Summary, latest.Checks);
    }

    /// <summary>
    /// The party channel's latest word on each player, shaped for the client.
    /// </summary>
    private static IReadOnlyList<PartySighting> ReadParty(IReadOnlyList<PartyNote> notes) =>
        [.. Party.Latest(notes)
            .Select(note => new PartySighting(
                note.Handle!,
                note.Moment.ToString().ToLowerInvariant(),
                note.At))];

    /// <summary>
    /// Pushes snapshots on an install with no Game.log, where the only thing
    /// that can change is what the pilot has shown the app.
    /// </summary>
    private async Task BroadcastScreenOnlyAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (await WaitAsync(timer, token))
        {
            NowState snapshot;
            lock (_gate)
            {
                var screen = ScreenNow();
                snapshot = Current = new NowState { Screen = screen, RecentEvents = Feed() };
            }

            await _hub.Clients.All.SendAsync("now", snapshot, token);
        }
    }

    private async Task BroadcastAsync()
    {
        NowState snapshot;
        lock (_gate)
        {
            snapshot = Current = Snapshot();
        }

        await _hub.Clients.All.SendAsync("now", snapshot);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_tailer is not null)
            await _tailer.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }
}
