using System.Net;
using System.Text;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Turning UEX off has to stay off.
/// </summary>
/// <remarks>
/// Before 0.7.0 a fetch only ran on a click, so racing one against Disable took
/// deliberate effort. The background refresher made it ordinary: a refresh
/// falls due, the player presses Disable while it is in flight, and the fetch
/// finishes afterwards. Rewriting the cache then would turn the integration
/// back on behind them - and re-arm the refresher, which only runs while UEX is
/// enabled, so the app would keep going back out on its own.
/// </remarks>
public class UexEnableDisableRaceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-uex-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Serves canned UEX payloads, and can be held open mid-fetch so a test can
    /// act while the call is in flight.
    /// </summary>
    private sealed class Feed(TaskCompletionSource? gate = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (gate is not null)
                await gate.Task;

            var url = request.RequestUri!.ToString();

            var body = url.Contains("commodities_prices_all") ? """
                {"data":[{"id_commodity":5,"commodity_name":"Aluminum","id_terminal":12,
                  "terminal_name":"TDD Area 18","price_sell":3800,"price_buy":0,
                  "scu_sell_stock":0,"scu_buy":0,"date_modified":1787117859}]}
                """
                : """{"data":[]}""";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public async Task A_refresh_finishing_after_Disable_does_not_turn_UEX_back_on()
    {
        var uex = new UexData(_directory);

        // Prime it, so there is something for Disable to remove.
        Assert.True(await uex.EnableAsync(new HttpClient(new Feed())) > 0);
        Assert.True(uex.IsEnabled);

        // A refresh begins and is held at the network call.
        var gate = new TaskCompletionSource();
        var refresh = uex.EnableAsync(new HttpClient(new Feed(gate)));

        // The player presses Disable while it is in flight.
        uex.Disable();
        Assert.False(uex.IsEnabled);

        // Now let the refresh land.
        gate.SetResult();
        var applied = await refresh;

        Assert.Equal(0, applied);
        Assert.False(uex.IsEnabled);
        Assert.Null(uex.FetchedAt);

        // And nothing was written back to disk behind the removal.
        Assert.False(File.Exists(Path.Combine(_directory, "prices.json")));
    }

    /// <summary>
    /// The guard must not make Disable sticky: enabling again afterwards is an
    /// ordinary thing to do and has to work.
    /// </summary>
    [Fact]
    public async Task Enabling_again_after_Disable_still_works()
    {
        var uex = new UexData(_directory);

        await uex.EnableAsync(new HttpClient(new Feed()));
        uex.Disable();

        Assert.True(await uex.EnableAsync(new HttpClient(new Feed())) > 0);
        Assert.True(uex.IsEnabled);
        Assert.NotNull(uex.FetchedAt);
    }
}
