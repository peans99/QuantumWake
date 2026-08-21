namespace Verselog.Core.Logging;

/// <summary>A detected Star Citizen channel install (LIVE, PTU, EPTU, ...).</summary>
/// <param name="Channel">Channel name, e.g. <c>LIVE</c>.</param>
/// <param name="RootPath">Channel root, containing Game.log.</param>
public sealed record GameInstall(string Channel, string RootPath)
{
    /// <summary>The active log the game writes to while running.</summary>
    public string GameLogPath => Path.Combine(RootPath, "Game.log");

    /// <summary>Directory of rotated logs from previous sessions.</summary>
    public string LogBackupsPath => Path.Combine(RootPath, "logbackups");

    public bool HasGameLog => File.Exists(GameLogPath);

    /// <summary>Rotated log files, oldest first.</summary>
    public IReadOnlyList<string> BackupLogs()
    {
        if (!Directory.Exists(LogBackupsPath))
            return [];

        return [.. Directory.EnumerateFiles(LogBackupsPath, "*.log")
            .OrderBy(File.GetLastWriteTimeUtc)];
    }
}

/// <summary>
/// Finds Star Citizen installs on disk.
/// </summary>
/// <remarks>
/// Hardcoding the default LIVE path is a common failure in existing tools, since
/// players routinely install to a second drive. Detection scans the usual roots
/// across every fixed drive and always allows an explicit override.
/// </remarks>
public static class GameInstallLocator
{
    private static readonly string[] RelativeRoots =
    [
        @"Roberts Space Industries\StarCitizen",
        @"Program Files\Roberts Space Industries\StarCitizen",
        @"Games\Roberts Space Industries\StarCitizen",
        @"Program Files (x86)\Roberts Space Industries\StarCitizen"
    ];

    /// <summary>
    /// Discovers installs by scanning fixed drives for the standard layout.
    /// Returns an empty list rather than throwing when nothing is found.
    /// </summary>
    public static IReadOnlyList<GameInstall> Discover()
    {
        var results = new List<GameInstall>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                continue;

            foreach (var relative in RelativeRoots)
            {
                var candidate = Path.Combine(drive.RootDirectory.FullName, relative);
                if (!Directory.Exists(candidate))
                    continue;

                foreach (var channelDir in SafeEnumerateDirectories(candidate))
                {
                    var install = new GameInstall(Path.GetFileName(channelDir), channelDir);
                    if (install.HasGameLog || Directory.Exists(install.LogBackupsPath))
                    {
                        if (seen.Add(channelDir))
                            results.Add(install);
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Builds an install from an explicit path, accepting either the channel
    /// directory itself or a parent containing channel directories.
    /// </summary>
    public static GameInstall? FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return null;

        var direct = new GameInstall(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), path);
        if (direct.HasGameLog || Directory.Exists(direct.LogBackupsPath))
            return direct;

        foreach (var channelDir in SafeEnumerateDirectories(path))
        {
            var install = new GameInstall(Path.GetFileName(channelDir), channelDir);
            if (install.HasGameLog || Directory.Exists(install.LogBackupsPath))
                return install;
        }

        return null;
    }

    /// <summary>Prefers LIVE, then any channel that has a Game.log.</summary>
    public static GameInstall? Preferred(IReadOnlyList<GameInstall>? installs = null)
    {
        installs ??= Discover();

        return installs.FirstOrDefault(i => i.Channel.Equals("LIVE", StringComparison.OrdinalIgnoreCase))
            ?? installs.FirstOrDefault(i => i.HasGameLog)
            ?? installs.FirstOrDefault();
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }
}
