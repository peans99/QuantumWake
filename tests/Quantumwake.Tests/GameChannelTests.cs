using Quantumwake.Core;
using Quantumwake.Core.Logging;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// LIVE and PTU side by side: where each one's caches live, and which one the
/// game is being played on.
/// </summary>
public class GameChannelTests
{
    /// <summary>LIVE keeps the data folder itself, so nothing anyone already has moves.</summary>
    [Fact]
    public void Live_keeps_the_data_folder_and_others_get_their_own()
    {
        Assert.Equal(AppPaths.Root, AppPaths.ForChannel(null));
        Assert.Equal(AppPaths.Root, AppPaths.ForChannel(@"D:\Games\StarCitizen\LIVE"));
        Assert.Equal(AppPaths.Root, AppPaths.ForChannel(@"D:\Games\StarCitizen\live\"));
        Assert.Equal(AppPaths.In("channels", "PTU"), AppPaths.ForChannel(@"D:\Games\StarCitizen\PTU"));
        Assert.Equal(AppPaths.In("channels", "EPTU"), AppPaths.ForChannel("/home/p/Games/drive_c/StarCitizen/eptu/"));
        Assert.Equal(AppPaths.In("channels", "TECH-PREVIEW"), AppPaths.ForChannel(@"C:\RSI\StarCitizen\TECH-PREVIEW"));
    }

    [Fact]
    public void The_game_running_on_another_channel_is_found_by_its_log()
    {
        using var folder = new Channels();
        var live = folder.Channel("LIVE", TimeSpan.FromHours(20));
        var ptu = folder.Channel("PTU", TimeSpan.FromMinutes(3));

        var playing = GameChannels.PlayingElsewhere([live, ptu], live, DateTimeOffset.UtcNow);

        Assert.Equal("PTU", playing?.Channel);
    }

    /// <summary>A log written an hour ago is a game that was played, not one being played.</summary>
    [Fact]
    public void An_old_log_elsewhere_is_not_a_game_running()
    {
        using var folder = new Channels();
        var live = folder.Channel("LIVE", TimeSpan.FromHours(20));
        var ptu = folder.Channel("PTU", TimeSpan.FromHours(1));

        Assert.Null(GameChannels.PlayingElsewhere([live, ptu], live, DateTimeOffset.UtcNow));
    }

    /// <summary>Both written lately, the one being read is the newer: nothing to say.</summary>
    [Fact]
    public void Nothing_is_said_when_the_channel_being_read_is_the_newer()
    {
        using var folder = new Channels();
        var live = folder.Channel("LIVE", TimeSpan.FromMinutes(1));
        var ptu = folder.Channel("PTU", TimeSpan.FromMinutes(6));

        Assert.Null(GameChannels.PlayingElsewhere([live, ptu], live, DateTimeOffset.UtcNow));
        Assert.Equal("LIVE", GameChannels.PlayingElsewhere([live, ptu], ptu, DateTimeOffset.UtcNow)?.Channel);
    }

    [Fact]
    public void One_folder_is_one_key_however_it_was_written()
    {
        var a = Path.Combine(Path.GetTempPath(), "SC", "PTU");
        Assert.Equal(GameChannels.Key(a), GameChannels.Key(a.ToUpperInvariant() + Path.DirectorySeparatorChar));
    }

    private sealed class Channels : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "qw-channels-" + Guid.NewGuid().ToString("N"));

        public GameInstall Channel(string name, TimeSpan age)
        {
            var dir = Path.Combine(_root, name);
            Directory.CreateDirectory(dir);
            var log = Path.Combine(dir, "Game.log");
            File.WriteAllText(log, "<2026-11-03T10:00:00.000Z> Log started\n");
            File.SetLastWriteTimeUtc(log, DateTime.UtcNow - age);
            return new GameInstall(name, dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        }
    }
}
