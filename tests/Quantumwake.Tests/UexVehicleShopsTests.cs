using System.Net;
using System.Text;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Where a ship is sold: every shop, not only the cheapest.
/// </summary>
/// <remarks>
/// The catalogue used to keep one terminal a vehicle - the cheapest - which
/// answered "what is it worth" and not "where do I go". The cheapest is still
/// the first, so fleet value is unchanged; the rest of the list is the hover.
/// The stored file from before this change holds one shop a vehicle, and has
/// to read as a list of one rather than as nothing until the next refresh.
/// </remarks>
public class UexVehicleShopsTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-uex-shops-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        GC.SuppressFinalize(this);
    }

    private sealed class Feed : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();

            var body =
                url.Contains("vehicles_purchases_prices_all") ? """
                {"data":[
                  {"vehicle_name":"Cutlass Black","terminal_name":"New Deal","price_buy":1500000},
                  {"vehicle_name":"Cutlass Black","terminal_name":"Astro Armada","price_buy":1400000},
                  {"vehicle_name":"Cutlass Black","terminal_name":"Astro Armada","price_buy":1450000},
                  {"vehicle_name":"Cutlass Black","terminal_name":"Crusader Showroom","price_buy":1500000},
                  {"vehicle_name":"Idris","terminal_name":"Nowhere","price_buy":0}]}
                """
                : url.Contains("commodities_prices_all") ? """
                {"data":[{"id_commodity":5,"commodity_name":"Aluminum","id_terminal":12,
                  "terminal_name":"TDD Area 18","price_sell":3800,"price_buy":0,
                  "scu_sell_stock":0,"scu_buy":0,"date_modified":1787117859}]}
                """
                : """{"data":[]}""";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task Every_shop_is_kept_cheapest_first_one_row_a_shop_and_the_cheapest_is_still_the_price()
    {
        var uex = new UexData(_directory);
        Assert.True(await uex.EnableAsync(new HttpClient(new Feed())) > 0);

        var shops = uex.VehicleShops("Drake Cutlass Black");

        Assert.Equal(["Astro Armada", "New Deal", "Crusader Showroom"], shops.Select(s => s.Terminal).ToArray());
        Assert.Equal(1400000m, shops[0].Price);
        Assert.Equal(1400000m, uex.VehiclePrice("Cutlass Black")!.Price);
        Assert.Empty(uex.VehicleShops("Idris"));
        Assert.Null(uex.VehiclePrice("Idris"));

        // And the same list comes back from disk.
        var again = new UexData(_directory);
        Assert.Equal(3, again.VehicleShops("Cutlass Black").Count);
    }

    [Fact]
    public void A_file_from_before_the_list_reads_as_one_shop_rather_than_none()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "prices.json"), """{"Copper":{"BestSell":1000,"BestSellTerminal":"Buyer","BestBuy":900,"BestBuyTerminal":"Seller","Terminals":1}}""");
        File.WriteAllText(Path.Combine(_directory, "commodity-ids.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "terminals.json"), "[]");
        File.WriteAllText(Path.Combine(_directory, "vehicles.json"), """{"CutlassBlack":{"Price":1400000,"Terminal":"Astro Armada"}}""");

        var uex = new UexData(_directory);

        var shops = uex.VehicleShops("Cutlass Black");
        Assert.Single(shops);
        Assert.Equal("Astro Armada", shops[0].Terminal);
        Assert.Equal(1400000m, uex.VehiclePrice("Drake Cutlass Black")!.Price);
    }
}
