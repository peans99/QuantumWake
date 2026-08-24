using System.Net;
using System.Text;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>The route table must prefer a run a pilot can plausibly complete.</summary>
public class UexRouteReliabilityTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"qw-routes-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        GC.SuppressFinalize(this);
    }

    private sealed class Feed : HttpMessageHandler
    {
        private static readonly long Now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        private static readonly long Stale = DateTimeOffset.UtcNow.AddDays(-8).ToUnixTimeSeconds();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var rows = $$"""
                {"data":[
                  {"id_commodity":1,"commodity_name":"Fresh cargo","id_terminal":1,"terminal_name":"Fresh seller","price_buy":10,"price_sell":0,"scu_buy":80,"scu_sell_stock":0,"date_modified":{{Now}}},
                  {"id_commodity":1,"commodity_name":"Fresh cargo","id_terminal":2,"terminal_name":"Thin buyer","price_buy":0,"price_sell":25,"scu_buy":0,"scu_sell_stock":18,"date_modified":{{Now}}},
                  {"id_commodity":1,"commodity_name":"Fresh cargo","id_terminal":3,"terminal_name":"Backup buyer","price_buy":0,"price_sell":22,"scu_buy":0,"scu_sell_stock":64,"date_modified":{{Now}}},
                  {"id_commodity":2,"commodity_name":"Stale gold","id_terminal":4,"terminal_name":"Old seller","price_buy":10,"price_sell":0,"scu_buy":80,"scu_sell_stock":0,"date_modified":{{Stale}}},
                  {"id_commodity":2,"commodity_name":"Stale gold","id_terminal":5,"terminal_name":"Old buyer","price_buy":0,"price_sell":100,"scu_buy":0,"scu_sell_stock":64,"date_modified":{{Stale}}}
                ]}
                """;

            var body = request.RequestUri!.ToString().Contains("commodities_prices_all")
                ? rows
                : "{\"data\":[]}";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task Reliable_first_prefers_fresh_quotes_and_caps_the_run_at_buyer_demand()
    {
        var uex = new UexData(_directory);
        await uex.EnableAsync(new HttpClient(new Feed()));

        var reliable = uex.Routes(scu: 64, capital: 10_000);
        var fresh = Assert.Single(reliable, r => r.Commodity == "Fresh cargo");

        Assert.Equal("Fresh cargo", reliable[0].Commodity);
        Assert.Equal(18, fresh.Units);
        Assert.Equal("demand", fresh.LimitedBy);
        Assert.Equal("fresh", fresh.Freshness);
        Assert.Equal(18, fresh.SellDemandScu);
        Assert.Contains(fresh.FallbackSells, f => f.Terminal == "Backup buyer" && f.Freshness == "fresh");

        Assert.All(uex.Routes(64, 10_000, freshOnly: true), r => Assert.Equal("fresh", r.Freshness));
        Assert.Equal("Stale gold", uex.Routes(64, 10_000, reliableFirst: false)[0].Commodity);
    }
}
