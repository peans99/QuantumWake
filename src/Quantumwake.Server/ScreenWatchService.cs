using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// Reads every screenshot in the game's folder the app has not read, while
/// the pilot has said so - the ones already there and each new one as the
/// game writes it.
/// </summary>
/// <remarks>
/// <para>
/// A folder listing every two seconds rather than a file system watcher. The
/// folder does not exist until the first screenshot is taken, a watcher on a
/// folder that is not there has nothing to bind to, and the listing costs
/// nothing - eight files, or eight hundred. The rules for which files count
/// live in <see cref="ScreenFolder"/> and are tested there; this is the loop.
/// </para>
/// <para>
/// The archive is read too, newest first, a few files a tick so the newest
/// screenshot is never queued behind a hundred old ones. The watch used to
/// begin from the moment it was switched on, which left the only photographs
/// of a ship's loadout - taken the evening the watch shipped - unread for a
/// week. The switch is the pilot's choice; a reading they do not want
/// believed is invalidated on the Log tab.
/// </para>
/// </remarks>
public sealed class ScreenWatchService(
    ScreenSettingsStore settings,
    ScreenInsightService insight,
    ScreenReadingStore readings,
    ILogger<ScreenWatchService> logger,
    GameInstall? install = null) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Files read per tick. The engine takes a few hundred milliseconds a
    /// frame, so this is about a second of work between listings, and a new
    /// screenshot waits at most that long behind the archive.
    /// </summary>
    private const int BatchPerTick = 5;

    /// <summary>
    /// How long a file the engine refused is given before the refusal is
    /// kept. A screenshot the game is still saving reads as nothing and is
    /// worth another look; one from last week that reads as nothing will
    /// read as nothing next tick too, and kept unread it would head the list
    /// for ever.
    /// </summary>
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (install is null || !insight.CanReadScreenshots)
        {
            // Nothing to watch, or no engine to read with. The settings
            // endpoint says which, so the page can too.
            return;
        }

        using var timer = new PeriodicTimer(Every);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!settings.Current.WatchScreenshots) continue;

                var waiting = insight.Unread(install.RootPath);

                if (waiting.Count == 0)
                {
                    await Remeasure(stoppingToken);
                    continue;
                }

                foreach (var file in waiting.Take(BatchPerTick))
                {
                    try
                    {
                        var sighting = await insight.ReadShotAsync(file.Path, stoppingToken);
                        logger.LogInformation("Read {Shot}: {Summary}", sighting.Shot, sighting.Summary);

                        if (!readings.Has(sighting.Shot) && DateTimeOffset.UtcNow - file.LastWrite >= GiveUpAfter)
                            readings.Add(sighting);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        // One bad file must not stop the next good one being read.
                        logger.LogWarning(e, "Could not read {Shot}", file.Path);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    /// <summary>Rep frames already tried this run, so one that still will not measure is not read every tick.</summary>
    private readonly HashSet<string> _remeasured = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads again the Rep app frames that were read before their bars were
    /// measured - see <see cref="ScreenInsightService.Unmeasured"/> - and the
    /// ones filed as unreadable that carry a Rep tab - see
    /// <see cref="ScreenInsightService.Misfiled"/> - and the ones whose bars
    /// read as nothing earned - see <see cref="ScreenInsightService.Doubtful"/>.
    /// Only when nothing new is waiting, so a fresh screenshot never queues
    /// behind them. A reading the pilot dismissed is left dismissed, which is
    /// why those are not offered.
    /// </summary>
    private async Task Remeasure(CancellationToken token)
    {
        var root = install!.RootPath;
        var waiting = insight.Unmeasured(root).Concat(insight.Misfiled(root)).Concat(insight.Doubtful(root));

        foreach (var path in waiting.Where(_remeasured.Add).Take(BatchPerTick))
        {
            try
            {
                var sighting = await insight.ReadShotAsync(path, token);
                logger.LogInformation("Measured {Shot} again: {Summary}", sighting.Shot, sighting.Summary);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Could not measure {Shot} again", path);
            }
        }
    }
}
