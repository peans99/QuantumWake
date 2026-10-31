using Quantumwake.Core.Logging;

namespace Quantumwake.Tests;

/// <summary>
/// Finding the game inside a Wine prefix, the way it is installed on Linux.
/// </summary>
/// <remarks>
/// The prefixes are built in the temp folder in the shape measured on a LUG
/// Helper install (docs/linux.md): <c>drive_c/Program Files/Roberts Space
/// Industries/StarCitizen/LIVE</c>, <c>dosdevices/&lt;letter&gt;:</c> links,
/// and the launcher's log under <c>drive_c/users</c>. The Windows build ships
/// this code but never calls it - Discover, FromPath and FromLauncherLog take
/// the Wine branch only off Windows - so a test whose answer turns on how
/// paths are split or rooted runs on Linux only. On Windows <c>c:</c> is a
/// drive and <c>\</c> a separator, and such a test would be measuring that.
/// The ones that need a symbolic link or the XDG config folder are Linux-only
/// too; the rest run everywhere.
/// </remarks>
public sealed class WineInstallTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qw-wine-" + Guid.NewGuid().ToString("N"));

    private const string Live = "drive_c/Program Files/Roberts Space Industries/StarCitizen/LIVE";

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string Prefix(string name, string live = Live)
    {
        var prefix = Path.Combine(_root, name);
        var channel = Path.Combine(prefix, live);
        Directory.CreateDirectory(channel);
        File.WriteAllText(Path.Combine(channel, "Game.log"), "<2026-10-30T00:39:53.932Z> Log started\n");
        return prefix;
    }

    [Fact]
    public void Finds_the_channel_under_drive_c()
    {
        if (OperatingSystem.IsWindows()) return; // the drive comes back with '\' there

        var prefix = Prefix("lug");

        var found = GameInstallLocator.DiscoverUnderWine([prefix]);

        var install = Assert.Single(found);
        Assert.Equal("LIVE", install.Channel);
        Assert.Equal(Path.Combine(prefix, Live), install.RootPath);
        Assert.True(install.HasGameLog);
    }

    [Fact]
    public void A_prefix_with_no_game_in_it_finds_nothing()
    {
        var prefix = Path.Combine(_root, "empty");
        Directory.CreateDirectory(Path.Combine(prefix, "drive_c", "windows"));

        Assert.Empty(GameInstallLocator.DiscoverUnderWine([prefix]));
    }

    /// <summary>
    /// The LUG Helper writes the StarCitizen folder itself to gamedir.conf; a
    /// game installed somewhere no prefix walk would find is still found.
    /// </summary>
    [Fact]
    public void The_game_folder_named_outright_counts()
    {
        var elsewhere = Path.Combine(_root, "ssd", "sc");
        Directory.CreateDirectory(Path.Combine(elsewhere, "PTU"));
        File.WriteAllText(Path.Combine(elsewhere, "PTU", "Game.log"), "x");

        var found = GameInstallLocator.DiscoverUnderWine([], elsewhere);

        Assert.Equal("PTU", Assert.Single(found).Channel);
    }

    [Fact]
    public void Follows_a_drive_letter_to_the_folder_it_maps()
    {
        if (OperatingSystem.IsWindows()) return; // needs a symbolic link

        var prefix = Path.Combine(_root, "letters");
        Directory.CreateDirectory(Path.Combine(prefix, "drive_c"));
        var games = Path.Combine(_root, "games-disk");
        Directory.CreateDirectory(Path.Combine(games, "StarCitizen", "LIVE"));
        File.WriteAllText(Path.Combine(games, "StarCitizen", "LIVE", "Game.log"), "x");

        Directory.CreateDirectory(Path.Combine(prefix, "dosdevices"));
        Directory.CreateSymbolicLink(Path.Combine(prefix, "dosdevices", "c:"), Path.Combine(prefix, "drive_c"));
        Directory.CreateSymbolicLink(Path.Combine(prefix, "dosdevices", "d:"), games);
        File.CreateSymbolicLink(Path.Combine(prefix, "dosdevices", "d::"), "/dev/sr0");

        // drive_c once, not again as c:, and d: as the folder it points at.
        Assert.Equal([Path.Combine(prefix, "drive_c"), games], GameInstallLocator.WineDrives(prefix));

        var found = GameInstallLocator.DiscoverUnderWine([prefix]);
        Assert.Equal(Path.Combine(games, "StarCitizen", "LIVE"), Assert.Single(found).RootPath);
    }

    /// <summary>
    /// The line as the launcher writes it on this install, JSON escaping and
    /// all. The folder comes back under drive_c, the name discovery gives it,
    /// even when the prefix has the c: link the launcher's path goes through.
    /// </summary>
    [Fact]
    public void Maps_the_path_the_launcher_wrote()
    {
        if (OperatingSystem.IsWindows()) return; // "dosdevices\c:" is the real C: drive there

        var prefix = Prefix("launcher");
        Directory.CreateDirectory(Path.Combine(prefix, "dosdevices"));
        Directory.CreateSymbolicLink(Path.Combine(prefix, "dosdevices", "c:"), Path.Combine(prefix, "drive_c"));
        var log = Path.Combine(prefix, "drive_c", "users", "pilot", "AppData", "Roaming", "rsilauncher", "logs", "log.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        File.WriteAllText(log,
            """{ "t":"2026-10-29 12:54:10.095", "[main][info] ": "[ComputeSizePhase] Computing required space for (SC LIVE) in C:\\Program Files\\Roberts Space Industries\\StarCitizen\\LIVE"  },""" + "\n");

        Assert.Equal(log, Assert.Single(GameInstallLocator.WineLauncherLogs(prefix)));

        var found = GameInstallLocator.FromLauncherLog(log, path => GameInstallLocator.MapWindowsPath(prefix, path));

        Assert.Equal(Path.Combine(prefix, Live), Assert.Single(found).RootPath);
    }

    [Fact]
    public void A_windows_path_is_matched_without_regard_to_case()
    {
        if (OperatingSystem.IsWindows()) return; // "dosdevices\c:" is the real C: drive there

        var prefix = Prefix("case");

        var mapped = GameInstallLocator.MapWindowsPath(prefix, @"c:\program files\roberts space industries\starcitizen\live");

        Assert.Equal(Path.Combine(prefix, Live), mapped);
        Assert.Null(GameInstallLocator.MapWindowsPath(prefix, @"C:\Program Files\Nothing\LIVE"));
        Assert.Null(GameInstallLocator.MapWindowsPath(prefix, @"D:\StarCitizen\LIVE"));
        Assert.Null(GameInstallLocator.MapWindowsPath(prefix, "/not/windows"));
    }

    [Fact]
    public void The_prefix_itself_or_a_windows_path_is_accepted_as_the_folder()
    {
        if (OperatingSystem.IsWindows()) return; // FromPath only looks inside prefixes on Linux

        var prefix = Prefix("typed");
        var before = Environment.GetEnvironmentVariable("WINEPREFIX");

        try
        {
            Environment.SetEnvironmentVariable("WINEPREFIX", prefix);

            Assert.Contains(prefix, GameInstallLocator.WinePrefixes());
            Assert.Equal(Path.Combine(prefix, Live), GameInstallLocator.FromPath(prefix)?.RootPath);
            Assert.Equal(Path.Combine(prefix, Live), GameInstallLocator.FromPath(prefix + "/")?.RootPath);
            Assert.Equal(Path.Combine(prefix, Live),
                GameInstallLocator.FromPath(@"C:\Program Files\Roberts Space Industries\StarCitizen\LIVE")?.RootPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WINEPREFIX", before);
        }
    }

    /// <summary>
    /// The LUG Helper's own record of the prefix, read from the XDG config
    /// folder, which is where .NET reads ApplicationData from on Linux.
    /// </summary>
    [Fact]
    public void The_lug_helper_config_names_the_prefix()
    {
        if (OperatingSystem.IsWindows()) return; // ApplicationData ignores XDG_CONFIG_HOME there

        var prefix = Prefix("from-config");
        var config = Path.Combine(_root, "config");
        Directory.CreateDirectory(Path.Combine(config, "starcitizen-lug"));
        File.WriteAllText(Path.Combine(config, "starcitizen-lug", "winedir.conf"), prefix + "\n");
        var before = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", config);

            Assert.Contains(prefix, GameInstallLocator.WinePrefixes());
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", before);
        }
    }
}
