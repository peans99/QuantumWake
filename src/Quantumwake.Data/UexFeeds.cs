using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>One optional UEX feed, as the Settings and setup pages describe it.</summary>
/// <param name="Key">Stable identifier, used in urls and cache file names.</param>
/// <param name="Cost">Rough download size, so the user can judge before clicking.</param>
public sealed record UexFeedInfo(string Key, string Title, string Description, string Cost);

/// <summary>A vehicle's rental price at one terminal.</summary>
public sealed record UexRental(string Vehicle, string Terminal, decimal Price);

/// <summary>A fuel price at one terminal.</summary>
public sealed record UexFuel(string Fuel, string Terminal, decimal Price);

/// <summary>What a refinery yields for one ore, as a percentage the community reports.</summary>
public sealed record UexRefinery(string Commodity, string Terminal, string? System, double Yield, double Capacity);

/// <summary>What raw, unrefined ore fetches at one terminal.</summary>
public sealed record UexRawPrice(string Commodity, string Terminal, decimal Sell);

/// <summary>One place in UEX's own hierarchy: a station, city, outpost or point of interest.</summary>
/// <param name="Clinic">
/// Whether the place has a clinic, when the directory says. Null means the feed
/// did not carry the flag for this row - which is not the same as "no clinic",
/// and callers have to keep the two apart.
/// </param>
/// <param name="Habitation">Whether it has habs, on the same terms.</param>
public sealed record UexPlace(
    string Name,
    string? Nickname,
    string Kind,
    string? System,
    string? Planet,
    bool? Clinic = null,
    bool? Habitation = null);


/// <summary>
/// The optional half of the UEX integration: feeds beyond the core price
/// tables, each switched on individually.
/// </summary>
/// <remarks>
/// <para>
/// The core integration fetches what the whole app leans on - commodity
/// prices, terminals, vehicle and item prices. Everything here is narrower and
/// serves one page or one join, so it is offered feed by feed rather than as
/// one all-or-nothing download: a trader wants fuel and rentals, a miner wants
/// refineries and raw ore, and neither should pay for the other's bytes.
/// </para>
/// <para>
/// Each feed digests to one cache file, and the file's existence IS the
/// enabled state - no separate flag to fall out of step with what is on disk.
/// </para>
/// </remarks>
public sealed class UexFeeds
{
    public const string Rentals = "rentals";
    public const string Fuel = "fuel";
    public const string Refineries = "refineries";
    public const string RawPrices = "raw-prices";
    public const string Places = "places";

    /// <summary>Every optional feed, in the order the settings page lists them.</summary>
    public static readonly IReadOnlyList<UexFeedInfo> All =
    [
        new(Rentals, "Ship rentals",
            "Rental prices per terminal, so rented ships can be told from owned ones and a rental run can be costed.",
            "~60 KB"),
        new(Fuel, "Fuel prices",
            "Quantum and hydrogen fuel prices per terminal - what a long haul actually costs to fly.",
            "~40 KB"),
        new(Refineries, "Refinery yields",
            "What each refinery returns per ore and how much it can hold, closing the loop from rock to sale.",
            "~130 KB"),
        new(RawPrices, "Raw ore prices",
            "What unrefined ore fetches, so refining can be weighed against selling the rock as it came.",
            "~80 KB"),
        new(Places, "Place directory",
            "UEX's own catalogue of stations, cities, outposts and points of interest - better place matching everywhere.",
            "~700 KB"),
    ];

    private static readonly Dictionary<string, string[]> Urls = new(StringComparer.OrdinalIgnoreCase)
    {
        [Rentals] = ["https://api.uexcorp.space/2.0/vehicles_rentals_prices_all"],
        [Fuel] = ["https://api.uexcorp.space/2.0/fuel_prices_all"],
        [Refineries] =
        [
            "https://api.uexcorp.space/2.0/refineries_yields",
            "https://api.uexcorp.space/2.0/refineries_capacities",
        ],
        [RawPrices] = ["https://api.uexcorp.space/2.0/commodities_raw_prices_all"],
        [Places] =
        [
            "https://api.uexcorp.space/2.0/space_stations",
            "https://api.uexcorp.space/2.0/cities",
            "https://api.uexcorp.space/2.0/outposts",
            "https://api.uexcorp.space/2.0/poi",
        ],
    };

    private readonly string _directory;
    private readonly Dictionary<string, object> _loaded = new(StringComparer.OrdinalIgnoreCase);

    public UexFeeds(string? directory = null) =>
        _directory = directory ?? AppPaths.In("uex", "feeds");

    private string PathFor(string key) => Path.Combine(_directory, $"{key}.json");

    public bool IsEnabled(string key) => File.Exists(PathFor(key));

    public DateTimeOffset? FetchedAt(string key) =>
        File.Exists(PathFor(key)) ? File.GetLastWriteTimeUtc(PathFor(key)) : null;

    /// <summary>Fetches and digests one feed. Unknown keys are a caller error.</summary>
    public async Task<int> EnableAsync(string key, HttpClient http, CancellationToken token = default)
    {
        if (!Urls.TryGetValue(key, out var urls))
            throw new ArgumentException($"Unknown UEX feed '{key}'.", nameof(key));

        var documents = new List<JsonElement>();
        foreach (var url in urls)
            documents.Add(JsonDocument.Parse(await http.GetStringAsync(url, token)).RootElement);

        object digested = key switch
        {
            Rentals => DigestRentals(documents[0]),
            Fuel => DigestFuel(documents[0]),
            Refineries => DigestRefineries(documents[0], documents[1]),
            RawPrices => DigestRawPrices(documents[0]),
            Places => DigestPlaces(documents),
            _ => throw new ArgumentException($"Unknown UEX feed '{key}'.", nameof(key))
        };

        var count = digested is System.Collections.ICollection collection ? collection.Count : 0;
        if (count == 0)
            throw new InvalidDataException($"The UEX {key} feed parsed to nothing.");

        Directory.CreateDirectory(_directory);
        File.WriteAllText(PathFor(key), JsonSerializer.Serialize(digested));
        _loaded[key] = digested;

        return count;
    }

    public void Disable(string key)
    {
        if (File.Exists(PathFor(key)))
            File.Delete(PathFor(key));

        _loaded.Remove(key);
    }

    /// <summary>Reads a feed, loading it from disk on first use. Empty when off.</summary>
    private List<T> Read<T>(string key)
    {
        if (_loaded.TryGetValue(key, out var cached) && cached is List<T> hit)
            return hit;

        if (!File.Exists(PathFor(key)))
            return [];

        try
        {
            var loaded = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(PathFor(key))) ?? [];
            _loaded[key] = loaded;
            return loaded;
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return [];
        }
    }

    public IReadOnlyList<UexRental> RentalPrices => Read<UexRental>(Rentals);
    public IReadOnlyList<UexFuel> FuelPrices => Read<UexFuel>(Fuel);
    public IReadOnlyList<UexRefinery> RefineryYields => Read<UexRefinery>(Refineries);
    public IReadOnlyList<UexRawPrice> RawOrePrices => Read<UexRawPrice>(RawPrices);
    public IReadOnlyList<UexPlace> PlaceDirectory => Read<UexPlace>(Places);

    /// <summary>
    /// Whether a place has a clinic: true, false, or null for "not known".
    /// </summary>
    /// <remarks>
    /// Used to tell a bed at a hospital from a bed in a hab. The directory
    /// names places its own way, so a name is matched exactly, then by
    /// nickname, then by one containing the other - the same shape of match the
    /// terminal join uses, and equally unwilling to guess between two hits.
    /// </remarks>
    public bool? HasClinic(string? place)
    {
        if (string.IsNullOrWhiteSpace(place))
            return null;

        var wanted = Compact(place);
        if (wanted.Length < 4)
            return null;

        var directory = PlaceDirectory;

        var exact = directory.FirstOrDefault(p =>
            string.Equals(Compact(p.Name), wanted, StringComparison.OrdinalIgnoreCase)
                || (p.Nickname is not null && string.Equals(Compact(p.Nickname), wanted, StringComparison.OrdinalIgnoreCase)));

        if (exact is not null)
            return exact.Clinic;

        var loose = directory
            .Where(p => Compact(p.Name).Contains(wanted, StringComparison.OrdinalIgnoreCase)
                || wanted.Contains(Compact(p.Name), StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        return loose.Count == 1 ? loose[0].Clinic : null;
    }



    /// <summary>
    /// The cheapest rental of a vehicle, matched the way purchase prices are:
    /// UEX drops the manufacturer from names that our display names carry.
    /// </summary>
    public UexRental? CheapestRental(string? vehicleName)
    {
        if (string.IsNullOrWhiteSpace(vehicleName))
            return null;

        var rentals = RentalPrices;
        if (rentals.Count == 0)
            return null;

        var compact = Compact(vehicleName);
        var words = vehicleName.Trim().Split(' ', 2);
        var stripped = words.Length == 2 ? Compact(words[1]) : null;

        return rentals
            .Where(r => Compact(r.Vehicle) == compact
                || (stripped is not null && Compact(r.Vehicle) == stripped))
            .OrderBy(r => r.Price)
            .FirstOrDefault();
    }

    private static string Compact(string value) => new([.. value.Where(char.IsLetterOrDigit)]);

    /* ---------- digests ---------- */

    private static List<UexRental> DigestRentals(JsonElement root) =>
        [.. Rows(root)
            .Select(r => new UexRental(
                Str(r, "vehicle_name") ?? "", Str(r, "terminal_name") ?? "", (decimal)(Num(r, "price_rent") ?? 0)))
            .Where(r => r.Vehicle.Length > 0 && r.Price > 0)];

    private static List<UexFuel> DigestFuel(JsonElement root) =>
        [.. Rows(root)
            .Select(r => new UexFuel(
                Str(r, "commodity_name") ?? "", Str(r, "terminal_name") ?? "", (decimal)(Num(r, "price_buy") ?? 0)))
            .Where(f => f.Fuel.Length > 0 && f.Price > 0)];

    private static List<UexRawPrice> DigestRawPrices(JsonElement root) =>
        [.. Rows(root)
            .Select(r => new UexRawPrice(
                Str(r, "commodity_name") ?? "", Str(r, "terminal_name") ?? "", (decimal)(Num(r, "price_sell") ?? 0)))
            .Where(p => p.Commodity.Length > 0 && p.Sell > 0)];

    /// <summary>
    /// Yields and capacities arrive as separate reports about the same
    /// terminals, so they are joined here rather than shown as two tables.
    /// </summary>
    private static List<UexRefinery> DigestRefineries(JsonElement yields, JsonElement capacities)
    {
        var capacityByTerminal = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in Rows(capacities))
        {
            var terminal = Str(row, "terminal_name");
            var value = Num(row, "value") ?? 0;

            if (terminal is not null && value > 0)
                capacityByTerminal[terminal] = value;
        }

        return
        [
            .. Rows(yields)
                .Select(r =>
                {
                    var terminal = Str(r, "terminal_name") ?? "";
                    return new UexRefinery(
                        Str(r, "commodity_name") ?? "",
                        terminal,
                        Str(r, "star_system_name"),
                        Num(r, "value") ?? 0,
                        capacityByTerminal.GetValueOrDefault(terminal));
                })
                .Where(r => r.Commodity.Length > 0 && r.Terminal.Length > 0)
        ];
    }

    /// <summary>
    /// The place directory, from four endpoints that share a shape. The kind
    /// comes from which endpoint a row arrived on, since the rows do not say.
    /// </summary>
    private static List<UexPlace> DigestPlaces(List<JsonElement> documents)
    {
        var kinds = new[] { "Station", "City", "Outpost", "Point of interest" };
        var places = new List<UexPlace>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < documents.Count && i < kinds.Length; i++)
        {
            foreach (var row in Rows(documents[i]))
            {
                var name = Str(row, "name");
                if (name is null || !seen.Add($"{kinds[i]}|{name}"))
                    continue;

                places.Add(new UexPlace(
                    name,
                    Str(row, "nickname"),
                    kinds[i],
                    Str(row, "star_system_name"),
                    Str(row, "planet_name"),
                    Flag(row, "has_clinic"),
                    Flag(row, "has_habitation")));
            }
        }

        return places;
    }

    /// <summary>
    /// A 0/1 flag from the directory, or null when the row does not carry it.
    /// </summary>
    /// <remarks>
    /// Absent and false are different answers here: one says "this place has no
    /// clinic", the other says "nobody recorded whether it does", and a bed at
    /// each of those means something different.
    /// </remarks>
    private static bool? Flag(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32() != 0
            : null;

    private static IEnumerable<JsonElement> Rows(JsonElement root) =>
        root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray()
            : [];

    private static string? Str(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? Num(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
}
