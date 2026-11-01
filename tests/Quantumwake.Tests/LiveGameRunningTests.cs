using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Quantumwake.Core.Events;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// Whether the Now snapshot calls the game running. Game.log keeps the last
/// session after the game closes, so without this the page showed a session
/// days old as if it were being flown.
/// </summary>
public class LiveGameRunningTests : IDisposable
{
    private readonly SessionStore _store = new(":memory:");

    public void Dispose() => _store.Dispose();

    private LiveSessionService Live(bool process) =>
        new(new MuteHub(), new LogLibrary(_store), NullLogger<LiveSessionService>.Instance)
        {
            GameProcessRunning = () => process,
        };

    private static ShardJoinEvent Join(DateTimeOffset at) =>
        new(at, "pub_use1b_12545750_150", "1.2.3.4", 1, "x");

    [Fact]
    public void A_log_from_yesterday_with_no_game_is_a_closed_session()
    {
        var ended = DateTimeOffset.UtcNow.AddDays(-1);
        var live = Live(process: false);

        live.OnEvent(Join(ended.AddHours(-2)));
        live.OnEvent(Join(ended));

        Assert.False(live.Current.GameRunning);
        Assert.Equal(ended, live.Current.LastSeen);
    }

    [Fact]
    public void The_game_process_means_running_however_old_the_log()
    {
        var live = Live(process: true);
        live.OnEvent(Join(DateTimeOffset.UtcNow.AddDays(-1)));

        Assert.True(live.Current.GameRunning);
    }

    /// <summary>
    /// A log written to a moment ago is a game being played, even when the
    /// process check misses it - unmeasured under Wine, and being told the
    /// game is closed mid-flight is the worse mistake.
    /// </summary>
    [Fact]
    public void A_freshly_written_log_is_running_even_when_the_process_is_missed()
    {
        var live = Live(process: false);
        live.OnEvent(Join(DateTimeOffset.UtcNow.AddSeconds(-20)));

        Assert.True(live.Current.GameRunning);
    }
}

/// <summary>Nothing here broadcasts; the service only needs a hub to be constructed.</summary>
file sealed class MuteHub : IHubContext<LiveHub>
{
    public IHubClients Clients => throw new NotSupportedException("these tests never broadcast");
    public IGroupManager Groups => throw new NotSupportedException("these tests never broadcast");
}
