using Quantumwake.Data;
using Quantumwake.Core.Logging;

namespace Quantumwake.Server;

/// <summary>
/// The game channels on this machine - LIVE, PTU, EPTU, TECH-PREVIEW - and
/// which one the game was last running on.
/// </summary>
/// <remarks>
/// <para>
/// The app reads one channel at a time, and that is deliberate rather than a
/// gap: everything downstream holds the install it was started with, and each
/// channel's sessions are kept in their own database so a PTU evening never
/// lands in the LIVE totals. Switching is a restart pointed at the other
/// folder, which is what the tray's folder picker already did.
/// </para>
/// <para>
/// "Running" is read off Game.log's write time, not the process list. The
/// game is started fresh for every session and writes its log within seconds,
/// so a log written in the last few minutes is a game being played; asking
/// the process for its path works on Windows only, and not at all for a game
/// under Wine seen from the Linux side.
/// </para>
/// </remarks>
internal static class GameChannels
{
    /// <summary>How recently a log has to have been written to call the game running there.</summary>
    internal static readonly TimeSpan Recent = TimeSpan.FromMinutes(10);

    private static readonly object Gate = new();
    private static (DateTime At, IReadOnlyList<GameInstall> Found)? _cached;

    /// <summary>
    /// Every channel found: the drives, the launcher's own log, the folder
    /// the app is reading now and any folder it was pointed at by hand.
    /// </summary>
    /// <remarks>
    /// The drive walk is a few dozen existence checks, but the page asks every
    /// minute and a sleeping external drive can make one of them slow, so the
    /// list is kept for two minutes. Write times are read fresh each time.
    /// </remarks>
    public static IReadOnlyList<GameInstall> Find(GameInstall? current)
    {
        lock (Gate)
        {
            if (_cached is { } hit && DateTime.UtcNow - hit.At < TimeSpan.FromMinutes(2))
                return WithCurrent(hit.Found, current);
        }

        var found = new List<GameInstall>();
        try { found.AddRange(GameInstallLocator.Discover()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        try { found.AddRange(GameInstallLocator.FromLauncherLog()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }

        // A channel beside the one being read, even when it lives somewhere no
        // drive scan looks: the launcher puts every channel under one folder.
        var parents = new[] { current?.RootPath, InstallPathStore.Load() }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.GetDirectoryName(Path.GetFullPath(p!).TrimEnd('\\', '/')))
            .Where(p => p is not null && Directory.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var parent in parents)
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(parent!))
                {
                    var channel = new GameInstall(Path.GetFileName(dir), dir);
                    if (channel.HasGameLog || Directory.Exists(channel.LogBackupsPath))
                        found.Add(channel);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }

        var list = Distinct(found);
        lock (Gate) _cached = (DateTime.UtcNow, list);
        return WithCurrent(list, current);
    }

    private static IReadOnlyList<GameInstall> WithCurrent(IReadOnlyList<GameInstall> found, GameInstall? current) =>
        current is null ? found : Distinct([current, .. found]);

    private static IReadOnlyList<GameInstall> Distinct(IEnumerable<GameInstall> installs)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<GameInstall>();
        foreach (var install in installs)
            if (seen.Add(Key(install.RootPath)))
                list.Add(install);

        // LIVE first, then the test channels by name: the order the launcher shows.
        return [.. list
            .OrderBy(i => i.Channel.Equals("LIVE", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(i => i.Channel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.RootPath, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>One folder however it was written: slashes, case, a trailing separator.</summary>
    internal static string Key(string path) =>
        Path.GetFullPath(path).TrimEnd('\\', '/').ToLowerInvariant();

    /// <summary>When the game last wrote a log in this channel, or null for never.</summary>
    internal static DateTimeOffset? LastPlayed(GameInstall install)
    {
        try
        {
            if (install.HasGameLog)
                return File.GetLastWriteTimeUtc(install.GameLogPath);

            var newest = install.BackupLogs().LastOrDefault();
            return newest is null ? null : File.GetLastWriteTimeUtc(newest);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The other channel the game is being played on right now, or null when
    /// it is this one, or none.
    /// </summary>
    /// <param name="now">The clock, so a test can say what "recent" means.</param>
    internal static GameInstall? PlayingElsewhere(IReadOnlyList<GameInstall> channels, GameInstall? current, DateTimeOffset now)
    {
        var currentAt = current is null ? null : LastPlayed(current);
        var currentKey = current is null ? null : Key(current.RootPath);

        return channels
            .Where(c => Key(c.RootPath) != currentKey && c.HasGameLog)
            .Select(c => (Channel: c, At: LastPlayed(c)))
            .Where(c => c.At is { } at && now - at < Recent && (currentAt is null || at > currentAt))
            .OrderByDescending(c => c.At)
            .Select(c => c.Channel)
            .FirstOrDefault();
    }

    /// <summary>Forgets the drive walk, so the next ask looks again.</summary>
    internal static void Forget()
    {
        lock (Gate) _cached = null;
    }
}

public static partial class ServerHost
{
    /// <summary>
    /// The channel switcher: which channels exist, which is being read, and
    /// which the game is running on.
    /// </summary>
    /// <param name="pinned">
    /// True when the folder was named on the command line or in the settings
    /// file. That always wins over the remembered one, so a switch would come
    /// back to the same folder; the page says so instead of restarting for
    /// nothing.
    /// </param>
    static void MapChannels(WebApplication app, GameInstall? install, bool pinned)
    {
        app.MapGet("/api/install/channels", (ShellBridge shell, bool? refresh) =>
        {
            if (refresh == true) GameChannels.Forget();

            var channels = GameChannels.Find(install);
            var currentKey = install is null ? null : GameChannels.Key(install.RootPath);
            var playing = GameChannels.PlayingElsewhere(channels, install, DateTimeOffset.UtcNow);

            return Results.Ok(new
            {
                current = install?.Channel,
                pinned,
                canRestart = shell.Available,
                channels = channels.Select(c => new
                {
                    channel = c.Channel,
                    root = c.RootPath,
                    current = GameChannels.Key(c.RootPath) == currentKey,
                    hasGameLog = c.HasGameLog,
                    lastPlayed = GameChannels.LastPlayed(c),
                    logs = c.BackupLogs().Count,
                }),
                playing = playing is null ? null : new
                {
                    channel = playing.Channel,
                    root = playing.RootPath,
                    lastPlayed = GameChannels.LastPlayed(playing),
                },
            });
        });

        app.MapPost("/api/install/channel", (ChannelSwitchRequest body, ShellBridge shell) =>
        {
            if (pinned)
                return Results.Conflict(new
                {
                    message = "Quantum Wake was started with a game folder named on its command line, "
                        + "so it always reads that one. Start it without --path to switch channels.",
                });

            if (string.IsNullOrWhiteSpace(body.Root))
                return Results.BadRequest(new { message = "Pick a channel to switch to." });

            // Only a channel this machine was seen to have: the page sends back
            // a path it was given, and anything else is not a switch.
            var target = GameChannels.Find(install)
                .FirstOrDefault(c => GameChannels.Key(c.RootPath) == GameChannels.Key(body.Root));
            if (target is null)
                return Results.BadRequest(new { message = "That channel is no longer there." });

            if (install is not null && GameChannels.Key(install.RootPath) == GameChannels.Key(target.RootPath))
                return Results.Ok(new { channel = target.Channel, restarting = false, already = true });

            if (InstallPathStore.Save(target.RootPath) is null)
                return Results.BadRequest(new { message = "That channel is no longer there." });

            DiagnosticTrace.Mark("server", $"Switching to the {target.Channel} channel.");
            return Results.Ok(new { channel = target.Channel, restarting = shell.TryRestart() });
        });
    }
}

/// <summary>Body of POST /api/install/channel: the channel folder, as the list gave it.</summary>
public sealed record ChannelSwitchRequest(string? Root);
