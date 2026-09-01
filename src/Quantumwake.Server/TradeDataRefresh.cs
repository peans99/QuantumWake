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
    BackgroundWork work,
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

    /// <remarks>
    /// Nothing in here may escape. An exception out of <see cref="ExecuteAsync"/>
    /// stops the whole host by default, so a full disk or a locked cache file
    /// would close the dashboard, the overlay and the live feed - because a
    /// price refresh failed. Both the timestamp write and the fetch touch the
    /// filesystem, so both are inside.
    /// </remarks>
    private async Task RefreshIfDueAsync(CancellationToken token)
    {
        try
        {
            if (!preference.IsDue(uex.FetchedAt, DateTimeOffset.UtcNow))
                return;

            // Recorded before the attempt, not after: a fetch that throws must
            // still push the next try out to RetryAfter, and one that hangs
            // until shutdown must not leave the timestamp untouched.
            preference.Checked();

            // Claimed after IsDue, not before: this fires every fifteen minutes
            // and almost always has nothing to do, and a strip that blinked
            // "refreshing prices" four times an hour for no reason would be
            // noise rather than news.
            using var _ = work.Begin("prices", "Refreshing prices from UEX");

            var count = await uex.EnableAsync(factory.CreateClient("community"), token);

            // Zero means the fetch stood down because UEX was disabled while it
            // was in flight - EnableAsync throws rather than returning zero for
            // an empty feed. Logging it as a refresh of nothing reads as a
            // failure; it is the guard doing its job.
            if (count == 0)
                logger.LogInformation("Automatic UEX refresh stood down: the integration was turned off.");
            else
                logger.LogInformation("Refreshed {Count} UEX prices automatically.", count);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Shutdown arrived mid-fetch. Not a failure, and not ours to log.
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException
                                      or InvalidDataException or JsonException)
        {
            // Being unable to refresh is not a problem with the running copy:
            // the prices in hand stay, the page keeps saying how old they are,
            // and the next attempt is RetryAfter away.
            logger.LogDebug(e, "Automatic UEX refresh could not reach the price feed.");
        }
        catch (Exception e)
        {
            // Anything else is a surprise and worth saying so - but still not
            // worth taking the app down for.
            logger.LogWarning(e, "Automatic UEX refresh failed unexpectedly.");
        }
    }
}
