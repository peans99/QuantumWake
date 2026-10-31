namespace Quantumwake.Core.Logging;

/// <summary>
/// Finding the game when it runs under Wine, which is how it runs on Linux.
/// </summary>
/// <remarks>
/// <para>
/// There are no drive letters to walk on Linux: the game lives inside a Wine
/// prefix, under <c>drive_c</c>, at the same relative path the launcher uses
/// on Windows. So discovery here is a question of which prefixes exist, and
/// everything the Windows code knows about the layout under a drive still
/// holds below each one.
/// </para>
/// <para>
/// Measured 2026-10-30 on Fedora, against an install made by the LUG Helper
/// (4.17): the prefix is <c>~/Games/star-citizen</c>, the helper records it in
/// <c>~/.config/starcitizen-lug/winedir.conf</c> and the game folder in
/// <c>gamedir.conf</c> beside it, the game is at
/// <c>drive_c/Program Files/Roberts Space Industries/StarCitizen/LIVE</c>, and
/// the RSI launcher's log sits at
/// <c>drive_c/users/&lt;user&gt;/AppData/Roaming/rsilauncher/logs/log.log</c>
/// with its paths written as <c>C:\\Program Files\\...</c>. The other launchers'
/// prefix folders are taken from their own documentation and are checked on
/// disk like any other guess, never trusted.
/// </para>
/// </remarks>
public static partial class GameInstallLocator
{
    /// <summary>
    /// Wine prefixes worth looking in, surest first. Every entry holds a
    /// <c>drive_c</c>.
    /// </summary>
    public static IReadOnlyList<string> WinePrefixes()
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string? prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return;

            prefix = Path.TrimEndingDirectorySeparator(ExpandHome(prefix.Trim()));

            if (Directory.Exists(Path.Combine(prefix, "drive_c")) && seen.Add(prefix))
                found.Add(prefix);
        }

        // The prefix the game is actually launched with beats any guess.
        Add(Environment.GetEnvironmentVariable("WINEPREFIX"));

        // The LUG Helper writes down the prefix it made.
        Add(ReadSetting(LugSetting("winedir.conf")));

        var home = Home();

        // The LUG Helper's default, and the Lutris installer's.
        Add(Path.Combine(home, "Games", "star-citizen"));
        Add(Path.Combine(home, ".wine"));

        // Any other prefix kept under ~/Games, which is where both put a
        // renamed one.
        foreach (var folder in SafeEnumerateDirectories(Path.Combine(home, "Games")))
            Add(folder);

        // Steam, for a Proton prefix: steamapps/compatdata/<appid>/pfx. The
        // ~/.steam/steam alias is usually a link to ~/.local/share/Steam, and
        // following it rather than listing it keeps one install from showing
        // up twice under two spellings.
        var steamRoots = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.Combine(home, ".local", "share", "Steam"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
        };

        var alias = new DirectoryInfo(Path.Combine(home, ".steam", "steam"));
        steamRoots.Add(alias.LinkTarget is { } target
            ? Path.GetFullPath(target, alias.Parent?.FullName ?? home)
            : alias.FullName);

        foreach (var steam in steamRoots)
            foreach (var app in SafeEnumerateDirectories(Path.Combine(steam, "steamapps", "compatdata")))
                Add(Path.Combine(app, "pfx"));

        // Bottles, native and Flatpak.
        foreach (var root in new[]
        {
            Path.Combine(home, ".local", "share", "bottles", "bottles"),
            Path.Combine(home, ".var", "app", "com.usebottles.bottles", "data", "bottles", "bottles"),
        })
            foreach (var bottle in SafeEnumerateDirectories(root))
                Add(bottle);

        // Heroic.
        foreach (var prefix in SafeEnumerateDirectories(Path.Combine(home, "Games", "Heroic", "Prefixes", "default")))
            Add(prefix);

        return found;
    }

    /// <summary>Installs inside the Wine prefixes on this machine.</summary>
    public static IReadOnlyList<GameInstall> DiscoverUnderWine() =>
        DiscoverUnderWine(WinePrefixes(), ReadSetting(LugSetting("gamedir.conf")));

    /// <summary>
    /// Installs inside the given prefixes, plus one game folder named outright
    /// (the LUG Helper records the <c>StarCitizen</c> folder itself).
    /// </summary>
    internal static IReadOnlyList<GameInstall> DiscoverUnderWine(IEnumerable<string> prefixes, string? gameFolder = null)
    {
        var results = new List<GameInstall>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Consider(string candidate)
        {
            foreach (var channelDir in SafeEnumerateDirectories(candidate))
            {
                var install = new GameInstall(Path.GetFileName(channelDir), channelDir);
                if ((install.HasGameLog || Directory.Exists(install.LogBackupsPath)) && seen.Add(channelDir))
                    results.Add(install);
            }
        }

        if (!string.IsNullOrWhiteSpace(gameFolder))
            Consider(ExpandHome(gameFolder.Trim()));

        foreach (var prefix in prefixes)
            foreach (var drive in WineDrives(prefix))
                foreach (var relative in RelativeRoots)
                    Consider(Path.Combine(drive, relative.Replace('\\', '/')));

        return results;
    }

    /// <summary>
    /// The folders a prefix's drive letters stand for: <c>drive_c</c>, then
    /// whatever else <c>dosdevices</c> maps. A player who told the launcher
    /// to install on <c>D:</c> has that letter pointed at a Linux folder.
    /// </summary>
    internal static IReadOnlyList<string> WineDrives(string prefix)
    {
        var drives = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string path)
        {
            if (!Directory.Exists(path))
                return;

            // Kept as what the link points at, not as the link: c: is a link
            // to drive_c, and an install found through both would be the same
            // folder under two names - and two names mean two session caches,
            // since the cache is keyed on the install's path.
            var real = Path.TrimEndingDirectorySeparator(ResolveLink(path));
            if (seen.Add(real))
                drives.Add(real);
        }

        Add(Path.Combine(prefix, "drive_c"));

        foreach (var device in SafeEnumerateEntries(Path.Combine(prefix, "dosdevices")))
        {
            var name = Path.GetFileName(device);

            // "d:" and nothing else: "d::" is the CD drive, com1 a serial port.
            if (name.Length == 2 && name[1] == ':' && char.IsAsciiLetter(name[0]))
                Add(device);
        }

        return drives;
    }

    /// <summary>
    /// A path as the game or its launcher writes it (<c>C:\Program Files\...</c>)
    /// as the Linux folder it stands for inside a prefix, or null when nothing
    /// is there.
    /// </summary>
    /// <remarks>
    /// Walked a segment at a time, matching case-insensitively where an exact
    /// name is missing: Wine looks names up the way Windows does and Linux does
    /// not, so a path that is right for the game can still name a folder this
    /// process cannot open directly.
    /// </remarks>
    internal static string? MapWindowsPath(string prefix, string windowsPath)
    {
        if (!LooksLikeWindowsPath(windowsPath))
            return null;

        var letter = char.ToLowerInvariant(windowsPath[0]);
        var device = Path.Combine(prefix, "dosdevices", letter + ":");

        // The folder the letter stands for, named as discovery names it (see
        // WineDrives), so the same install never carries two paths.
        string drive;
        if (Directory.Exists(device))
            drive = ResolveLink(device);
        else if (letter == 'c')
            drive = Path.Combine(prefix, "drive_c");
        else
            return null;

        var current = drive;

        foreach (var segment in windowsPath[2..].Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Combine(current, segment);

            if (!Path.Exists(next))
            {
                next = SafeEnumerateEntries(current)
                    .FirstOrDefault(entry => string.Equals(Path.GetFileName(entry), segment, StringComparison.OrdinalIgnoreCase));

                if (next is null)
                    return null;
            }

            current = next;
        }

        return current;
    }

    /// <summary>The RSI launcher's logs inside a prefix, one per Windows user it has.</summary>
    internal static IEnumerable<string> WineLauncherLogs(string prefix) =>
        SafeEnumerateDirectories(Path.Combine(prefix, "drive_c", "users"))
            .Select(user => Path.Combine(user, "AppData", "Roaming", "rsilauncher", "logs", "log.log"))
            .Where(File.Exists);

    internal static bool LooksLikeWindowsPath(string path) =>
        path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    /// <summary>The install an explicit path names when that path is a prefix or a Windows path.</summary>
    private static GameInstall? FromWinePath(string path)
    {
        path = ExpandHome(path.Trim());

        // A path copied out of the launcher's log or Game.log is a Windows
        // one. It is the same folder in whichever prefix holds it.
        if (LooksLikeWindowsPath(path))
        {
            return WinePrefixes()
                .Select(prefix => MapWindowsPath(prefix, path))
                .Where(mapped => mapped is not null)
                .Select(mapped => FromPath(mapped!))
                .FirstOrDefault(install => install is not null);
        }

        // The prefix itself: look inside it the same way discovery would.
        if (Directory.Exists(Path.Combine(path, "drive_c")))
            return Preferred(DiscoverUnderWine([path]));

        return null;
    }

    /// <summary>Where a symbolic link ends up, or the path itself when it is not one.</summary>
    private static string ResolveLink(string path)
    {
        try
        {
            return new DirectoryInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path;
        }
        catch (IOException)
        {
            return path;
        }
    }

    private static string Home() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);

    /// <summary>
    /// The LUG Helper's settings live under the XDG config folder, which is
    /// what .NET answers for ApplicationData on Linux. Unverified, as in
    /// <see cref="AppPaths"/>, so a missing folder reads as a path and not as
    /// an empty string.
    /// </summary>
    private static string LugSetting(string file) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            "starcitizen-lug", file);

    /// <summary>The first non-blank line of a one-line settings file, or null.</summary>
    private static string? ReadSetting(string file)
    {
        try
        {
            if (!File.Exists(file))
                return null;

            return File.ReadLines(file).Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A shell's <c>~</c> spelled out, since a pilot types one and no API expands it.</summary>
    private static string ExpandHome(string path) =>
        path == "~" ? Home()
        : path.StartsWith("~/", StringComparison.Ordinal) ? Path.Combine(Home(), path[2..])
        : path;

    private static IEnumerable<string> SafeEnumerateEntries(string path)
    {
        try
        {
            return Directory.Exists(path) ? Directory.EnumerateFileSystemEntries(path) : [];
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }
}
