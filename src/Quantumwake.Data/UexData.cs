using System.Net.Http.Json;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>Best known prices for one commodity, from UEX community reports.</summary>
public sealed record UexPrice(
    decimal BestSell,
    string? BestSellTerminal,
    decimal BestBuy,
    string? BestBuyTerminal,
    int Terminals)
{
    /// <summary>15-day average sell across terminals, for trend context.</summary>
    public decimal AvgSell { get; init; }
}

/// <summary>One commodity's price at one terminal.</summary>
public sealed record UexMarketRow(int TerminalId, string Terminal, decimal Buy, decimal Sell);

/// <summary>Cheapest in-game purchase of a vehicle.</summary>
public sealed record UexVehiclePrice(decimal Price, string Terminal);

/// <summary>A buy-here, sell-there margin from one starting terminal.</summary>
public sealed record UexOpportunity(
    string Commodity,
    decimal BuyHere,
    decimal SellThere,
    string SellTerminal,
    decimal MarginPerScu);

/// <summary>One row this install could report to UEX: a real logged sale.</summary>
/// <param name="TerminalId">The matched UEX terminal, or null when the place could not be matched.</param>
public sealed record UexPushRow(
    DateTimeOffset At,
    string Commodity,
    string Place,
    decimal UnitPrice,
    int Scu,
    int? CommodityId,
    int? TerminalId,
    string? TerminalName);

/// <summary>
/// The optional UEX integration: live crowd-sourced prices in, and - with the
/// user's own UEX credentials - this install's logged sale prices back out.
/// </summary>
/// <remarks>
/// <para>
/// A third party, and a different kind of data: where scunpacked is the game's
/// own static tables, UEX is crowd-sourced market state that goes stale in
/// hours. Both directions are opt-in and off by default. Reading fetches one
/// public endpoint on the user's click; reporting requires the user's own UEX
/// application token and secret key, pasted into Settings and stored only in
/// local app data, and every push is an explicit button press showing what
/// will be sent first.
/// </para>
/// <para>
/// The push is the interesting half: every kiosk sale in Game.log carries an
/// exact unit price at a known terminal and time, which is precisely what UEX
/// datarunners type in by hand. A log-reading app can contribute the numbers
/// it already has.
/// </para>
/// </remarks>
public sealed class UexData
{
    public const string PricesUrl = "https://api.uexcorp.space/2.0/commodities_prices_all";
    public const string TerminalsUrl = "https://api.uexcorp.space/2.0/terminals?type=commodity";
    public const string SubmitUrl = "https://api.uexcorp.space/2.0/data_submit";
    public const string VehiclePricesUrl = "https://api.uexcorp.space/2.0/vehicles_purchases_prices_all";
    public const string ItemPricesUrl = "https://api.uexcorp.space/2.0/items_prices_all";

    private readonly string _directory;

    private Dictionary<string, UexPrice> _prices = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _commodityIds = new(StringComparer.OrdinalIgnoreCase);
    private List<(int Id, string Name)> _terminals = [];

    /// <summary>Every commodity price row per terminal - the route advisor's raw material.</summary>
    private Dictionary<string, List<UexMarketRow>> _matrix = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Cheapest in-game purchase per vehicle name (compact), for fleet value.</summary>
    private Dictionary<string, UexVehiclePrice> _vehicles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Cheapest buy per item uuid, for kit and stash value.</summary>
    private Dictionary<string, decimal> _itemPrices = new(StringComparer.OrdinalIgnoreCase);

    public UexData(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Quantumwake", "uex");

        TryLoad();
    }

    private string PricesPath => Path.Combine(_directory, "prices.json");
    private string IdsPath => Path.Combine(_directory, "commodity-ids.json");
    private string TerminalsPath => Path.Combine(_directory, "terminals.json");
    private string MetaPath => Path.Combine(_directory, "meta.json");
    private string CredentialsPath => Path.Combine(_directory, "credentials.json");
    private string MatrixPath => Path.Combine(_directory, "matrix.json");
    private string VehiclesPath => Path.Combine(_directory, "vehicles.json");
    private string ItemPricesPath => Path.Combine(_directory, "item-prices.json");

    public bool IsEnabled => _prices.Count > 0;
    public int Count => _prices.Count;
    public DateTimeOffset? FetchedAt { get; private set; }

    public bool HasCredentials => File.Exists(CredentialsPath);

    /// <summary>Best prices for a commodity display name, or null.</summary>
    public UexPrice? Best(string commodityName) =>
        _prices.TryGetValue(commodityName, out var price) ? price : null;

    /// <summary>
    /// Cheapest known in-game purchase of a vehicle, matched by name. UEX names
    /// carry no manufacturer ("Corsair", "Starlancer MAX") while our display
    /// names do ("Drake Corsair"), so the manufacturer word is stripped when
    /// the full name misses.
    /// </summary>
    public UexVehiclePrice? VehiclePrice(string? vehicleName)
    {
        if (string.IsNullOrWhiteSpace(vehicleName))
            return null;

        if (_vehicles.TryGetValue(Compact(vehicleName), out var exact))
            return exact;

        var words = vehicleName.Trim().Split(' ', 2);
        if (words.Length == 2 && _vehicles.TryGetValue(Compact(words[1]), out var stripped))
            return stripped;

        return null;
    }

    /// <summary>Cheapest known buy price for an item, by the game's entity uuid.</summary>
    public decimal? ItemPrice(string? uuid) =>
        uuid is not null && _itemPrices.TryGetValue(uuid, out var price) ? price : null;

    /// <summary>
    /// Trading opportunities from one place: what its terminal sells, and where
    /// each of those goods fetches the most. Empty when the place matches no
    /// UEX terminal - which the caller should say rather than hide.
    /// </summary>
    public List<UexOpportunity> Opportunities(string place, int limit = 6)
    {
        var terminal = MatchTerminal(place);
        if (terminal is null)
            return [];

        var opportunities = new List<UexOpportunity>();

        foreach (var (commodity, rows) in _matrix)
        {
            var here = rows.FirstOrDefault(r => r.TerminalId == terminal.Value.Id && r.Buy > 0);
            if (here is null)
                continue;

            var bestElsewhere = rows
                .Where(r => r.TerminalId != terminal.Value.Id && r.Sell > 0)
                .OrderByDescending(r => r.Sell)
                .FirstOrDefault();

            if (bestElsewhere is null || bestElsewhere.Sell <= here.Buy)
                continue;

            opportunities.Add(new UexOpportunity(
                commodity, here.Buy, bestElsewhere.Sell, bestElsewhere.Terminal,
                bestElsewhere.Sell - here.Buy));
        }

        return [.. opportunities.OrderByDescending(o => o.MarginPerScu).Take(limit)];
    }

    /// <summary>The matched UEX terminal name for a place, for the UI to show.</summary>
    public string? TerminalFor(string place) => MatchTerminal(place)?.Name;

    /// <summary>
    /// Fetches current prices, the terminal list, vehicle purchase prices and
    /// item prices. Anonymous endpoints, on the user's click only.
    /// </summary>
    public async Task<int> EnableAsync(HttpClient http, CancellationToken token = default)
    {
        var priceDoc = await http.GetFromJsonAsync<JsonElement>(PricesUrl, token);
        var terminalDoc = await http.GetFromJsonAsync<JsonElement>(TerminalsUrl, token);
        var vehicleDoc = await http.GetFromJsonAsync<JsonElement>(VehiclePricesUrl, token);
        var itemDoc = await http.GetFromJsonAsync<JsonElement>(ItemPricesUrl, token);

        var (prices, ids, matrix) = DigestPrices(priceDoc);
        if (prices.Count == 0)
            throw new InvalidDataException("UEX returned no commodity prices.");

        var terminals = DigestTerminals(terminalDoc);
        var vehicles = DigestVehicles(vehicleDoc);
        var itemPrices = DigestItemPrices(itemDoc);

        Directory.CreateDirectory(_directory);
        File.WriteAllText(PricesPath, JsonSerializer.Serialize(prices));
        File.WriteAllText(IdsPath, JsonSerializer.Serialize(ids));
        File.WriteAllText(TerminalsPath, JsonSerializer.Serialize(terminals.Select(t => new[] { (object)t.Id, t.Name })));
        File.WriteAllText(MatrixPath, JsonSerializer.Serialize(matrix));
        File.WriteAllText(VehiclesPath, JsonSerializer.Serialize(vehicles));
        File.WriteAllText(ItemPricesPath, JsonSerializer.Serialize(itemPrices));
        File.WriteAllText(MetaPath, JsonSerializer.Serialize(new Meta(DateTimeOffset.UtcNow)));

        _prices = prices;
        _commodityIds = ids;
        _terminals = terminals;
        _matrix = matrix;
        _vehicles = vehicles;
        _itemPrices = itemPrices;
        FetchedAt = DateTimeOffset.UtcNow;
        return _prices.Count;
    }

    /// <summary>Deletes the price cache. Credentials are removed separately.</summary>
    public void Disable()
    {
        foreach (var path in new[] { PricesPath, IdsPath, TerminalsPath, MetaPath, MatrixPath, VehiclesPath, ItemPricesPath })
            if (File.Exists(path))
                File.Delete(path);

        _prices = new Dictionary<string, UexPrice>(StringComparer.OrdinalIgnoreCase);
        _commodityIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _terminals = [];
        _matrix = new Dictionary<string, List<UexMarketRow>>(StringComparer.OrdinalIgnoreCase);
        _vehicles = new Dictionary<string, UexVehiclePrice>(StringComparer.OrdinalIgnoreCase);
        _itemPrices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        FetchedAt = null;
    }

    /// <summary>Stores the user's UEX token and secret key, locally only.</summary>
    public void SetCredentials(string? bearerToken, string? secretKey)
    {
        if (string.IsNullOrWhiteSpace(bearerToken) || string.IsNullOrWhiteSpace(secretKey))
        {
            if (File.Exists(CredentialsPath))
                File.Delete(CredentialsPath);
            return;
        }

        Directory.CreateDirectory(_directory);
        File.WriteAllText(CredentialsPath,
            JsonSerializer.Serialize(new Credentials(bearerToken.Trim(), secretKey.Trim())));
    }

    /// <summary>
    /// Matches this install's recent sales onto UEX ids: the rows a push would
    /// send, including the ones that cannot be sent and why.
    /// </summary>
    public List<UexPushRow> Pushable(IEnumerable<(DateTimeOffset At, string Commodity, string Place, decimal UnitPrice, int Scu)> sales)
    {
        var rows = new List<UexPushRow>();

        foreach (var sale in sales)
        {
            int? commodityId = _commodityIds.TryGetValue(sale.Commodity, out var id) ? id : null;
            var terminal = MatchTerminal(sale.Place);

            rows.Add(new UexPushRow(
                sale.At, sale.Commodity, sale.Place, sale.UnitPrice, sale.Scu,
                commodityId, terminal?.Id, terminal?.Name));
        }

        return rows;
    }

    /// <summary>
    /// Reports matched rows to UEX, one submission per terminal, using the
    /// stored credentials. Returns per-terminal outcomes verbatim enough to
    /// show the user what UEX said.
    /// </summary>
    public async Task<List<string>> PushAsync(HttpClient http, IReadOnlyList<UexPushRow> rows, CancellationToken token = default)
    {
        var credentials = JsonSerializer.Deserialize<Credentials>(File.ReadAllText(CredentialsPath))
            ?? throw new InvalidOperationException("No UEX credentials stored.");

        var results = new List<string>();

        foreach (var group in rows.Where(r => r.TerminalId is not null && r.CommodityId is not null)
                                   .GroupBy(r => r.TerminalId!.Value))
        {
            // One price per commodity per terminal: the most recent sale wins.
            var prices = group
                .GroupBy(r => r.CommodityId!.Value)
                .Select(g => g.OrderByDescending(r => r.At).First())
                .Select(r => new Dictionary<string, object>
                {
                    ["id_commodity"] = r.CommodityId!.Value,
                    ["price_sell"] = Math.Round(r.UnitPrice, 2)
                })
                .ToList();

            var body = new Dictionary<string, object>
            {
                ["id_terminal"] = group.Key,
                ["type"] = "commodity",
                ["is_production"] = 1,
                ["prices"] = prices,
                ["details"] = "Reported from Game.log by Quantum Wake"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, SubmitUrl);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {credentials.Token}");
            request.Headers.TryAddWithoutValidation("secret_key", credentials.Secret);
            request.Content = JsonContent.Create(body);

            using var response = await http.SendAsync(request, token);
            var payload = await response.Content.ReadAsStringAsync(token);

            results.Add($"{group.First().TerminalName}: {(int)response.StatusCode} " +
                        Truncate(payload, 160));
        }

        return results;
    }

    /// <summary>
    /// Our place names against UEX terminal names. UEX names commodity
    /// terminals like "TDD - Trade and Development Division - Area 18" or
    /// "Admin - Port Tressler", so containment of our shorter name is the
    /// reliable direction; ambiguity returns nothing rather than guessing.
    /// </summary>
    private (int Id, string Name)? MatchTerminal(string place)
    {
        if (string.IsNullOrWhiteSpace(place))
            return null;

        var wanted = Compact(place);
        if (wanted.Length < 5)
            return null;

        var matches = _terminals
            .Where(t => Compact(t.Name).Contains(wanted, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1)
            return matches[0];

        // A station carries several terminals - "Admin - Port Tressler" beside
        // "Platinum Bay - Port Tressler" - and commodity kiosks are the Admin
        // and TDD ones. Prefer those; still ambiguous means still skipped.
        foreach (var prefix in new[] { "Admin", "TDD" })
        {
            var preferred = matches
                .Where(t => t.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (preferred.Count == 1)
                return preferred[0];
        }

        return null;
    }

    private static string Compact(string value) =>
        new([.. value.Where(char.IsLetterOrDigit)]);

    private static (Dictionary<string, UexPrice>, Dictionary<string, int>, Dictionary<string, List<UexMarketRow>>)
        DigestPrices(JsonElement root)
    {
        var prices = new Dictionary<string, UexPrice>(StringComparer.OrdinalIgnoreCase);
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var matrix = new Dictionary<string, List<UexMarketRow>>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return (prices, ids, matrix);

        var avgSells = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in data.EnumerateArray())
        {
            var name = Str(row, "commodity_name");
            var terminal = Str(row, "terminal_name") ?? "?";
            if (name is null)
                continue;

            if (row.TryGetProperty("id_commodity", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                ids[name] = idEl.GetInt32();

            var terminalId = (int)(Num(row, "id_terminal") ?? 0);
            var sell = (decimal)(Num(row, "price_sell") ?? 0);
            var buy = (decimal)(Num(row, "price_buy") ?? 0);
            var sellAvg = (decimal)(Num(row, "price_sell_avg") ?? 0);

            if (!matrix.TryGetValue(name, out var list))
                matrix[name] = list = [];

            list.Add(new UexMarketRow(terminalId, terminal, buy, sell));

            if (sellAvg > 0)
            {
                if (!avgSells.TryGetValue(name, out var avgs))
                    avgSells[name] = avgs = [];
                avgs.Add(sellAvg);
            }
        }

        foreach (var (name, list) in matrix)
        {
            var bestSell = list.Where(x => x.Sell > 0).OrderByDescending(x => x.Sell).FirstOrDefault();
            var bestBuy = list.Where(x => x.Buy > 0).OrderBy(x => x.Buy).FirstOrDefault();

            prices[name] = new UexPrice(
                bestSell?.Sell ?? 0, bestSell?.Terminal,
                bestBuy?.Buy ?? 0, bestBuy?.Terminal,
                list.Count)
            {
                AvgSell = avgSells.TryGetValue(name, out var avgs) && avgs.Count > 0
                    ? Math.Round(avgs.Max(), 0)
                    : 0
            };
        }

        return (prices, ids, matrix);
    }

    private static Dictionary<string, UexVehiclePrice> DigestVehicles(JsonElement root)
    {
        var vehicles = new Dictionary<string, UexVehiclePrice>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return vehicles;

        foreach (var row in data.EnumerateArray())
        {
            var name = Str(row, "vehicle_name");
            var terminal = Str(row, "terminal_name") ?? "?";
            var price = (decimal)(Num(row, "price_buy") ?? 0);

            if (name is null || price <= 0)
                continue;

            var key = Compact(name);

            if (!vehicles.TryGetValue(key, out var existing) || price < existing.Price)
                vehicles[key] = new UexVehiclePrice(price, terminal);
        }

        return vehicles;
    }

    private static Dictionary<string, decimal> DigestItemPrices(JsonElement root)
    {
        var items = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return items;

        foreach (var row in data.EnumerateArray())
        {
            var uuid = Str(row, "item_uuid");
            var price = (decimal)(Num(row, "price_buy") ?? 0);

            if (uuid is null || price <= 0)
                continue;

            if (!items.TryGetValue(uuid, out var existing) || price < existing)
                items[uuid] = price;
        }

        return items;
    }

    private static List<(int Id, string Name)> DigestTerminals(JsonElement root)
    {
        var terminals = new List<(int, string)>();

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return terminals;

        foreach (var row in data.EnumerateArray())
        {
            if (row.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number
                && Str(row, "name") is { Length: > 0 } name)
                terminals.Add((id.GetInt32(), name));
        }

        return terminals;
    }

    private void TryLoad()
    {
        try
        {
            if (!File.Exists(PricesPath))
                return;

            _prices = JsonSerializer.Deserialize<Dictionary<string, UexPrice>>(File.ReadAllText(PricesPath))
                is { } p ? new Dictionary<string, UexPrice>(p, StringComparer.OrdinalIgnoreCase)
                         : new Dictionary<string, UexPrice>(StringComparer.OrdinalIgnoreCase);

            _commodityIds = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(IdsPath))
                is { } i ? new Dictionary<string, int>(i, StringComparer.OrdinalIgnoreCase)
                         : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            _terminals = (JsonSerializer.Deserialize<List<JsonElement>>(File.ReadAllText(TerminalsPath)) ?? [])
                .Where(x => x.ValueKind == JsonValueKind.Array && x.GetArrayLength() == 2)
                .Select(x => (x[0].GetInt32(), x[1].GetString() ?? ""))
                .ToList();

            if (File.Exists(MatrixPath))
                _matrix = JsonSerializer.Deserialize<Dictionary<string, List<UexMarketRow>>>(File.ReadAllText(MatrixPath))
                    is { } m ? new Dictionary<string, List<UexMarketRow>>(m, StringComparer.OrdinalIgnoreCase)
                             : new Dictionary<string, List<UexMarketRow>>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(VehiclesPath))
                _vehicles = JsonSerializer.Deserialize<Dictionary<string, UexVehiclePrice>>(File.ReadAllText(VehiclesPath))
                    is { } v ? new Dictionary<string, UexVehiclePrice>(v, StringComparer.OrdinalIgnoreCase)
                             : new Dictionary<string, UexVehiclePrice>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(ItemPricesPath))
                _itemPrices = JsonSerializer.Deserialize<Dictionary<string, decimal>>(File.ReadAllText(ItemPricesPath))
                    is { } ip ? new Dictionary<string, decimal>(ip, StringComparer.OrdinalIgnoreCase)
                              : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(MetaPath))
                FetchedAt = JsonSerializer.Deserialize<Meta>(File.ReadAllText(MetaPath))?.FetchedAt;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or FormatException or InvalidOperationException)
        {
            _prices = new Dictionary<string, UexPrice>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? Str(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? Num(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private sealed record Credentials(string Token, string Secret);
    private sealed record Meta(DateTimeOffset FetchedAt);
}
