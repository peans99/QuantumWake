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

    /// <summary>Always zero on SC 4.9 - no event identifies a killer any more.</summary>
    public int Kills { get; init; }

    public IReadOnlyList<TimelineEntry> RecentEvents { get; init; } = [];
}

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
    private readonly Lock _gate = new();

    private SessionBuilder _builder;
    private LogTailer? _tailer;
    private string? _currentShip;
    private readonly List<TimelineEntry> _recent = [];

    /// <param name="install">
    /// Defaulted so the container can still build this service on a machine
    /// where no install was found: the live tail has nothing to follow, and
    /// the rest of the app runs regardless.
    /// </param>
    public LiveSessionService(
        IHubContext<LiveHub> hub,
        LogLibrary library,
        ILogger<LiveSessionService> logger,
        GameInstall? install = null)
    {
        _hub = hub;
        _library = library;
        _install = install;
        _logger = logger;
        _builder = new SessionBuilder(install?.GameLogPath ?? "live");
    }

    /// <summary>Current snapshot, also served over REST for first paint.</summary>
    public NowState Current { get; private set; } = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_install is null || !_install.HasGameLog)
        {
            _logger.LogWarning("No Game.log found; live view disabled.");
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
            // which is the only signal a ship swap produces on SC 4.9.
            if (ev is VehicleControlEvent { Change: SeatChange.Left })
                _currentShip = null;

            Current = Snapshot();
        }
    }

    private void OnRotated()
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
        }
    }

    /// <summary>Builds a snapshot. Caller must hold the lock.</summary>
    private NowState Snapshot()
    {
        var summary = _builder.Build();
        var location = _builder.Location;

        // Keep the tail of the timeline for the live feed.
        _recent.Clear();
        _recent.AddRange(summary.Timeline.TakeLast(40).Reverse());

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
            RecentEvents = [.. _recent]
        };
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
