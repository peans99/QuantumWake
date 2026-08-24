using Quantumwake.Data;
using System.Net;
using System.Text;

namespace Quantumwake.Tests;

/// <summary>
/// The optional UEX feeds: rentals, fuel, refineries, raw ore and the place
/// directory.
/// </summary>
/// <remarks>
/// Every one is opt-in, downloaded once on a click, and digested down to the few
/// fields a page actually draws. What matters here is what survives that digest
/// and what does not: a row UEX has not priced is worse than no row, because it
/// draws as a real offer at zero, and a place matched loosely to two entries is
/// a guess this app has decided not to make.
/// </remarks>
public class UexFeedsTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-feeds-{Guid.NewGuid():N}");

    private UexFeeds NewFeeds() => new(_directory);

    /// <summary>Answers every request with the same body, whatever was asked for.</summary>
    private sealed class Feed(params string[] bodies) : HttpMessageHandler
    {
        private int _next;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            // Refineries ask for two documents; the rest ask for one.
            var body = bodies[Math.Min(_next++, bodies.Length - 1)];

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static HttpClient Serving(params string[] bodies) => new(new Feed(bodies));

    [Fact]
    public async Task A_feed_is_off_until_it_is_enabled_and_off_again_when_removed()
    {
        var feeds = NewFeeds();
        Assert.False(feeds.IsEnabled(UexFeeds.Fuel));
        Assert.Null(feeds.FetchedAt(UexFeeds.Fuel));
        Assert.Empty(feeds.FuelPrices);

        await feeds.EnableAsync(UexFeeds.Fuel, Serving(
            "{\"data\":[{\"commodity_name\":\"Hydrogen\",\"terminal_name\":\"Port Tressler\",\"price_buy\":1.5}]}"));

        Assert.True(feeds.IsEnabled(UexFeeds.Fuel));
        Assert.NotNull(feeds.FetchedAt(UexFeeds.Fuel));
        Assert.Single(feeds.FuelPrices);

        feeds.Disable(UexFeeds.Fuel);

        Assert.False(feeds.IsEnabled(UexFeeds.Fuel));
        Assert.Empty(feeds.FuelPrices);
    }

    [Fact]
    public async Task An_enabled_feed_survives_a_restart()
    {
        await NewFeeds().EnableAsync(UexFeeds.Rentals, Serving(
            "{\"data\":[{\"vehicle_name\":\"Cutlass Black\",\"terminal_name\":\"New Deal\",\"price_rent\":34000}]}"));

        var rental = Assert.Single(NewFeeds().RentalPrices);
        Assert.Equal("Cutlass Black", rental.Vehicle);
        Assert.Equal(34000m, rental.Price);
    }

    /// <summary>
    /// A row UEX has not priced is worse than no row at all: it draws as a real
    /// offer costing nothing, which is a lie the page cannot walk back.
    /// </summary>
    [Theory]
    [InlineData(UexFeeds.Rentals, "vehicle_name", "price_rent")]
    [InlineData(UexFeeds.Fuel, "commodity_name", "price_buy")]
    [InlineData(UexFeeds.RawPrices, "commodity_name", "price_sell")]
    public async Task A_row_with_no_price_is_dropped_rather_than_shown_as_free(
        string key, string nameField, string priceField)
    {
        var feeds = NewFeeds();

        await feeds.EnableAsync(key, Serving(
            "{\"data\":["
            + $"{{\"{nameField}\":\"Priced\",\"terminal_name\":\"Somewhere\",\"{priceField}\":10}},"
            + $"{{\"{nameField}\":\"Free somehow\",\"terminal_name\":\"Somewhere\",\"{priceField}\":0}},"
            + $"{{\"{nameField}\":\"\",\"terminal_name\":\"Somewhere\",\"{priceField}\":99}}]}}"));

        var kept = key switch
        {
            UexFeeds.Rentals => feeds.RentalPrices.Select(r => r.Vehicle).ToList(),
            UexFeeds.Fuel => feeds.FuelPrices.Select(f => f.Fuel).ToList(),
            _ => feeds.RawOrePrices.Select(p => p.Commodity).ToList(),
        };

        Assert.Equal(["Priced"], kept);
    }

    /// <summary>
    /// A feed that parses to nothing is refused rather than written, because an
    /// empty file on disk is indistinguishable from a feed that is switched on
    /// and simply has nothing to say.
    /// </summary>
    [Fact]
    public async Task A_feed_that_digests_to_nothing_is_refused_and_left_off()
    {
        var feeds = NewFeeds();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            feeds.EnableAsync(UexFeeds.Fuel, Serving("{\"data\":[]}")));

        Assert.False(feeds.IsEnabled(UexFeeds.Fuel));
    }

    [Fact]
    public async Task A_feed_nobody_has_heard_of_is_refused_by_name()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            NewFeeds().EnableAsync("telemetry", Serving("{\"data\":[]}")));

        Assert.Contains("telemetry", error.Message);
    }

    /// <summary>
    /// Yields and capacities are two reports about the same terminals, joined
    /// here so the page shows one table rather than asking a reader to do it.
    /// </summary>
    [Fact]
    public async Task Refinery_yields_and_capacities_are_joined_on_the_terminal()
    {
        var feeds = NewFeeds();

        await feeds.EnableAsync(UexFeeds.Refineries, Serving(
            "{\"data\":[{\"commodity_name\":\"Quantainium\",\"terminal_name\":\"ARC-L1\","
            + "\"star_system_name\":\"Stanton\",\"value\":0.82},"
            + "{\"commodity_name\":\"Taranite\",\"terminal_name\":\"Nowhere\",\"value\":0.5}]}",
            "{\"data\":[{\"terminal_name\":\"ARC-L1\",\"value\":1200}]}"));

        var rows = feeds.RefineryYields;

        var known = Assert.Single(rows, r => r.Terminal == "ARC-L1");
        Assert.Equal(0.82, known.Yield, 3);
        Assert.Equal(1200, known.Capacity, 3);
        Assert.Equal("Stanton", known.System);

        // A terminal with no capacity report keeps its yield rather than being
        // dropped: half an answer about a real refinery is still an answer.
        var unknown = Assert.Single(rows, r => r.Terminal == "Nowhere");
        Assert.Equal(0, unknown.Capacity);
    }

    /* ---------- the two lookups pages actually call ---------- */

    /// <summary>
    /// The directory is four documents - stations, cities, outposts, points of
    /// interest - and the digest tags each row with which one it came from. Only
    /// the first carries anything here, so a name means one place rather than
    /// four of it under different kinds.
    /// </summary>
    private async Task<UexFeeds> WithPlaces(string data)
    {
        var feeds = NewFeeds();
        await feeds.EnableAsync(UexFeeds.Places, Serving(data, "{\"data\":[]}"));
        return feeds;
    }

    [Fact]
    public async Task A_clinic_is_answered_for_a_place_the_directory_names_exactly()
    {
        var feeds = await WithPlaces(
            "{\"data\":[{\"name\":\"Port Tressler\",\"nickname\":\"Tressler\",\"has_clinic\":1},"
            + "{\"name\":\"Baijini Point\",\"has_clinic\":0}]}");

        Assert.True(feeds.HasClinic("Port Tressler"));
        Assert.True(feeds.HasClinic("port tressler"));
        Assert.False(feeds.HasClinic("Baijini Point"));
    }

    /// <summary>
    /// Null is "the directory does not say", and it has to stay distinguishable
    /// from false, which is "it says there is none".
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("HUR")]
    [InlineData("Somewhere the directory has never heard of")]
    public async Task A_place_the_directory_cannot_answer_for_is_unknown_rather_than_false(string? place)
    {
        var feeds = await WithPlaces("{\"data\":[{\"name\":\"Port Tressler\",\"has_clinic\":1}]}");

        Assert.Null(feeds.HasClinic(place));
    }

    /// <summary>
    /// Two loose hits is a guess between them, and this app would rather say
    /// nothing than pick one.
    /// </summary>
    [Fact]
    public async Task A_name_matching_two_places_loosely_is_left_unanswered()
    {
        var feeds = await WithPlaces(
            "{\"data\":[{\"name\":\"Shubin SMCa-6\",\"has_clinic\":1},"
            + "{\"name\":\"Shubin SMCa-8\",\"has_clinic\":0}]}");

        Assert.Null(feeds.HasClinic("Shubin SMCa"));
    }

    /// <summary>
    /// UEX drops the manufacturer that this app's display names carry, so
    /// "Drake Cutlass Black" has to find the row called "Cutlass Black".
    /// </summary>
    [Fact]
    public async Task A_rental_is_found_with_or_without_the_manufacturer_and_the_cheapest_wins()
    {
        var feeds = NewFeeds();

        await feeds.EnableAsync(UexFeeds.Rentals, Serving(
            "{\"data\":["
            + "{\"vehicle_name\":\"Cutlass Black\",\"terminal_name\":\"New Deal\",\"price_rent\":34000},"
            + "{\"vehicle_name\":\"Cutlass Black\",\"terminal_name\":\"Astro Armada\",\"price_rent\":29000}]}"));

        var cheapest = feeds.CheapestRental("Drake Cutlass Black");
        Assert.NotNull(cheapest);
        Assert.Equal(29000m, cheapest.Price);
        Assert.Equal("Astro Armada", cheapest.Terminal);

        Assert.Equal(29000m, feeds.CheapestRental("Cutlass Black")!.Price);
    }

    [Fact]
    public async Task A_vehicle_nobody_rents_is_null_rather_than_a_nearest_guess()
    {
        var feeds = NewFeeds();

        await feeds.EnableAsync(UexFeeds.Rentals, Serving(
            "{\"data\":[{\"vehicle_name\":\"Cutlass Black\",\"terminal_name\":\"New Deal\",\"price_rent\":34000}]}"));

        Assert.Null(feeds.CheapestRental("Idris"));
        Assert.Null(feeds.CheapestRental(null));
        Assert.Null(feeds.CheapestRental("   "));
    }

    /// <summary>Nothing is enabled, so every lookup answers with nothing.</summary>
    [Fact]
    public void With_every_feed_off_the_lookups_are_quiet_rather_than_broken()
    {
        var feeds = NewFeeds();

        Assert.Null(feeds.CheapestRental("Cutlass Black"));
        Assert.Null(feeds.HasClinic("Port Tressler"));
        Assert.Empty(feeds.PlaceDirectory);
        Assert.Empty(feeds.RefineryYields);
        Assert.Empty(feeds.RawOrePrices);
    }

    /// <summary>
    /// A half-written file must read as "off" rather than taking a page down,
    /// which is the same way every other store here decides to be wrong.
    /// </summary>
    [Fact]
    public async Task A_corrupt_feed_reads_as_empty_rather_than_throwing()
    {
        var feeds = NewFeeds();
        await feeds.EnableAsync(UexFeeds.Fuel, Serving(
            "{\"data\":[{\"commodity_name\":\"Hydrogen\",\"terminal_name\":\"Tressler\",\"price_buy\":1.5}]}"));

        File.WriteAllText(Path.Combine(_directory, "fuel.json"), "{ not json");

        Assert.Empty(NewFeeds().FuelPrices);
    }

    /// <summary>Every feed offered on the Settings page has somewhere to fetch from.</summary>
    [Fact]
    public void Every_advertised_feed_can_actually_be_enabled()
    {
        Assert.NotEmpty(UexFeeds.All);

        foreach (var feed in UexFeeds.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(feed.Title), feed.Key);
            Assert.False(string.IsNullOrWhiteSpace(feed.Description), feed.Key);

            // Enabling an advertised key must not be the "unknown feed" path.
            var error = Record.ExceptionAsync(() =>
                NewFeeds().EnableAsync(feed.Key, Serving("{\"data\":[]}"))).Result;

            Assert.IsNotType<ArgumentException>(error);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }
}
