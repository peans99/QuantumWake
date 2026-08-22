namespace Quantumwake.Core.Logging;

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
public static partial class GameInstallLocator
{
    private static readonly string[] RelativeRoots =
    [
        @"Roberts Space Industries\StarCitizen",
        @"Program Files\Roberts Space Industries\StarCitizen",
        @"Games\Roberts Space Industries\StarCitizen",
        @"Program Files (x86)\Roberts Space Industries\StarCitizen",

        // The launcher lets a player name any library folder, and these are
        // the shapes people actually choose.
        @"StarCitizen",
        @"Games\StarCitizen",
        @"Games\Roberts Space Industries",
        @"RSI\StarCitizen",
        @"SC\Roberts Space Industries\StarCitizen",
        @"Program Files\StarCitizen",
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
            // Removable included: an external SSD is a normal place to keep a
            // 100 GB game, and Windows reports those as Removable.
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable) || !drive.IsReady)
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

        // Nothing in the usual places: ask the launcher where it put the game.
        if (installs.Count == 0)
            installs = FromLauncherLog();

        return installs.FirstOrDefault(i => i.Channel.Equals("LIVE", StringComparison.OrdinalIgnoreCase))
            ?? installs.FirstOrDefault(i => i.HasGameLog)
            ?? installs.FirstOrDefault();
    }

    /// <summary>
    /// Installs named by the RSI launcher's own log.
    /// </summary>
    /// <remarks>
    /// The launcher writes lines carrying the full path of the build it is
    /// starting, whatever folder the player chose, so its log answers the
    /// question that scanning drives can only guess at. Read newest-first and
    /// treated as a hint: every path is still checked on disk.
    /// </remarks>
    public static IReadOnlyList<GameInstall> FromLauncherLog()
    {
        var log = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "rsilauncher", "logs", "log.log");

        if (!File.Exists(log))
            return [];

        var found = new List<GameInstall>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // The file runs to megabytes; the recent tail is the useful part.
            var lines = File.ReadLines(log).TakeLast(4000).Reverse();

            foreach (var line in lines)
            {
                foreach (System.Text.RegularExpressions.Match match in LauncherPathRegex().Matches(line))
                {
                    var path = match.Groups["path"].Value.Replace(@"\\", @"\");

                    if (!seen.Add(path) || !Directory.Exists(path))
                        continue;

                    var install = new GameInstall(Path.GetFileName(path), path);
                    if (install.HasGameLog || Directory.Exists(install.LogBackupsPath))
                        found.Add(install);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return found;
        }

        return found;
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"(?<path>[A-Za-z]:\\{1,2}(?:[^""\\]+\\{1,2})*StarCitizen\\{1,2}(?:LIVE|PTU|EPTU|TECH-PREVIEW|HOTFIX))",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex LauncherPathRegex();

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
