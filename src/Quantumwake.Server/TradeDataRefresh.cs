using System.Text.Json;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// Refetches UEX market prices while the app is open, if asked to.
/// </summary>
/// <remarks>
/// <para>
/// The only thing in the app that reaches the network without a click, and it
/// does so only after the player has said it may - see <see cref="TradeDataStore"/>
/// for why that preference exists at all. Two conditions guard every fetch, and
/// both are re-read on each tick rather than captured at startup: the preference
/// must be on, and UEX must already be enabled. Turning either off in Settings
/// stops the next tick, without a restart.
/// </para>
/// <para>
/// The tick is deliberately far shorter than the staleness window. Waking every
/// fifteen minutes to ask a local question costs nothing and means a refresh
/// lands within a quarter hour of falling due, rather than the app having to be
/// restarted to notice - and it is <see cref="TradeDataStore.IsDue"/>, not the
/// tick, that decides whether anything is fetched.
/// </para>
/// </remarks>
public sealed class TradeDataRefresh(
    UexData uex,
    TradeDataStore preference,
    IHttpClientFactory factory,
    ILogger<TradeDataRefresh> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A first look on startup, before the tick, so prices left stale
        // overnight are current by the time anyone opens the Market page. It
        // still goes through IsDue, so a copy started twice in an hour does not
        // fetch twice.
        await RefreshIfDueAsync(stoppingToken);

        using var timer = new PeriodicTimer(Tick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RefreshIfDueAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    private async Task RefreshIfDueAsync(CancellationToken token)
    {
        if (!preference.IsDue(uex.FetchedAt, DateTimeOffset.UtcNow))
            return;

        // Recorded before the attempt, not after: a fetch that throws must still
        // push the next try out to RetryAfter, and a fetch that hangs until
        // shutdown must not leave the timestamp untouched for the next launch.
        preference.Checked();

        try
        {
            var count = await uex.EnableAsync(factory.CreateClient("community"), token);
            logger.LogInformation("Refreshed {Count} UEX prices automatically.", count);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException
                                      or InvalidDataException or JsonException)
        {
            // Being unable to refresh is not a problem with the running copy: the
            // prices already in hand stay, the page keeps saying how old they
            // are, and the next attempt is RetryAfter away.
            logger.LogDebug(e, "Automatic UEX refresh could not reach the price feed.");
        }
    }
}
