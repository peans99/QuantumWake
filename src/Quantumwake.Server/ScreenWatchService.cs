using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// Reads each screenshot as the game writes it, while the pilot has said so.
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
/// The watch begins from the moment it is switched on. Whatever was already
/// in the folder stays unread, because the pilot enabled reading their new
/// screenshots and not their archive - the button for the newest one is
/// still there for that. Switching it off and on again starts a fresh
/// baseline for the same reason.
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

    /// <summary>When the current watch began, or null while it is off.</summary>
    private DateTimeOffset? _since;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (install is null || !insight.CanReadScreenshots)
        {
            // Nothing to watch, or no engine to read with. The settings
            // endpoint says which, so the page can too.
            return;
        }

        var folder = Screenshots.FolderFor(install.RootPath);
        using var timer = new PeriodicTimer(Every);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var wanted = settings.Current.WatchScreenshots;

                if (!wanted)
                {
                    _since = null;
                    continue;
                }

                _since ??= DateTimeOffset.UtcNow;

                if (!Directory.Exists(folder)) continue;

                IReadOnlyList<ScreenFile> ready;

                try
                {
                    ready = ScreenFolder.Ready(
                        new DirectoryInfo(folder).EnumerateFiles()
                            .Where(f => ScreenFolder.IsScreenshot(f.Name))
                            .Select(f => new ScreenFile(f.FullName, f.Length, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero))),
                        _since.Value,
                        DateTimeOffset.UtcNow,
                        path => readings.Has(Path.GetFileName(path)));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    logger.LogDebug(e, "Could not list {Folder}", folder);
                    continue;
                }

                foreach (var file in ready)
                {
                    try
                    {
                        var sighting = await insight.ReadShotAsync(file.Path, stoppingToken);
                        logger.LogInformation("Read {Shot}: {Summary}", sighting.Shot, sighting.Summary);
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
}
