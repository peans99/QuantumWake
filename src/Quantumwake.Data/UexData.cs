using Quantumwake.Core;
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

    /// <summary>
    /// When UEX last saw the best-sell price reported - the age a trader
    /// judges the number by. Null on caches digested before the field existed.
    /// </summary>
    public DateTimeOffset? SeenAt { get; init; }
}

/// <summary>One commodity's price at one terminal.</summary>
/// <param name="BuyScu">Stock available to buy, SCU. 0 on caches digested before the field existed.</param>
/// <param name="SellScu">Demand the terminal accepts when selling to it, SCU. Same caveat.</param>
/// <param name="Seen">Unix seconds of UEX's date_modified for this row. 0 on older caches.</param>
public sealed record UexMarketRow(
    int TerminalId, string Terminal, decimal Buy, decimal Sell, decimal BuyScu = 0, decimal SellScu = 0,
    long Seen = 0);

/// <summary>One commodity's price and stock at one counter, at one moment.</summary>
/// <param name="Sell">What the counter pays you per SCU; 0 when it does not buy.</param>
/// <param name="Buy">What it charges you per SCU; 0 when it does not sell.</param>
/// <param name="Demand">SCU it will still take off you.</param>
/// <param name="Stock">SCU it has on the shelf.</param>
public sealed record UexHistoryPoint(
    DateTimeOffset At, decimal Sell, decimal Buy, decimal Demand, decimal Stock);

/// <summary>One counter's history for one commodity, oldest first.</summary>
public sealed record UexTerminalHistory(
    int TerminalId, string Terminal, IReadOnlyList<UexHistoryPoint> Points);

/// <summary>
/// What a commodity has been doing lately, across a sample of its counters.
/// </summary>
/// <param name="Sampled">Counters actually fetched.</param>
/// <param name="Terminals">Counters trading it at all - the sample's denominator.</param>
public sealed record UexHistory(
    string Commodity,
    int Sampled,
    int Terminals,
    IReadOnlyList<UexTerminalHistory> Series);

/// <summary>Cheapest in-game purchase of a vehicle.</summary>
public sealed record UexVehiclePrice(decimal Price, string Terminal);

/// <summary>One item's buy price at one terminal - where a part is stocked.</summary>
public sealed record UexItemRow(string Terminal, decimal Buy);

/// <summary>One haul worth flying, sized to a real hold and a real wallet.</summary>
/// <param name="Units">SCU this run can actually carry, after every cap.</param>
/// <param name="LimitedBy">"hold", "capital" or "stock" - what caps this run.</param>
public sealed record UexRoute(
    string Commodity,
    string BuyAt,
    decimal BuyPrice,
    string SellAt,
    decimal SellPrice,
    decimal MarginPerScu,
    decimal Units,
    decimal Profit,
    decimal Outlay,
    string LimitedBy);

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

    /// <summary>
    /// Per-counter price and stock history. Takes id_terminal and id_commodity
    /// and serves nothing without the first, which is why a commodity-wide
    /// trend has to be sampled rather than simply asked for.
    /// </summary>
    public const string PriceHistoryUrl = "https://api.uexcorp.space/2.0/commodities_prices_history";

    /// <summary>
    /// How long a fetched history is reused. Long, because these move on the
    /// scale of days and the alternative is a burst of requests every time
    /// somebody clicks back into a commodity.
    /// </summary>
    public static readonly TimeSpan HistoryFreshFor = TimeSpan.FromHours(6);

    private readonly Dictionary<string, (DateTimeOffset At, UexHistory History)> _historyCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Guards the swap between a fetched table and the one in use.
    /// </summary>
    /// <remarks>
    /// Held only around the commit and around Disable, never across a network
    /// call. Everything it protects - the dictionaries, FetchedAt, the history
    /// cache - is replaced wholesale rather than edited, so readers outside the
    /// lock see one table or the other and never a half-written one.
    /// </remarks>
    private readonly Lock _gate = new();

    /// <summary>Bumped by Disable, so a fetch begun before it stands down.</summary>
    private int _generation;

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

    /// <summary>Every terminal stocking each item uuid - where to actually buy a part.</summary>
    private Dictionary<string, List<UexItemRow>> _itemMarket = new(StringComparer.OrdinalIgnoreCase);

    public UexData(string? directory = null)
    {
        _directory = directory ?? AppPaths.In("uex");

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
    private string ItemMarketPath => Path.Combine(_directory, "item-market.json");

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

    /// <summary>Every terminal stocking an item, by uuid. Empty when unknown.</summary>
    public IReadOnlyList<UexItemRow> ItemMarket(string? uuid) =>
        uuid is not null && _itemMarket.TryGetValue(uuid, out var rows) ? rows : [];

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
    /// The best hauls in the price table: for each commodity, the cheapest
    /// place to buy against the dearest place to sell.
    /// </summary>
    /// <remarks>
    /// What UEX does well already - except for the two things it cannot know.
    /// The hold is a real ship out of the player's own fleet, and the capital
    /// is what they actually have, so the ranking is by what THIS run would
    /// earn rather than by margin per SCU in the abstract. A run capped by
    /// money rather than by space says so, which is the difference between a
    /// route and a daydream.
    /// </remarks>
    public List<UexRoute> Routes(double scu, decimal capital, string? from = null, int limit = 25)
    {
        var origin = from is { Length: > 0 } ? MatchTerminal(from) : null;
        var routes = new List<UexRoute>();

        foreach (var (commodity, rows) in _matrix)
        {
            var buy = rows
                .Where(r => r.Buy > 0 && (origin is null || r.TerminalId == origin.Value.Id))
                .OrderBy(r => r.Buy)
                .FirstOrDefault();

            if (buy is null)
                continue;

            var sell = rows
                .Where(r => r.Sell > 0 && r.TerminalId != buy.TerminalId)
                .OrderByDescending(r => r.Sell)
                .FirstOrDefault();

            if (sell is null || sell.Sell <= buy.Buy)
                continue;

            var margin = sell.Sell - buy.Buy;

            // The hold, the wallet and the shop's own stock all cap a run;
            // whichever bites first is the one worth naming. With no ship
            // named, everything is priced per SCU rather than shown as
            // nothing - the page has to say something before it is configured.
            var byHold = scu > 0 ? (decimal)scu : 1;
            var byWallet = buy.Buy > 0 ? Math.Floor(capital / buy.Buy) : 0;
            var byStock = buy.BuyScu > 0 ? buy.BuyScu : decimal.MaxValue;

            var units = byHold;
            var limiter = scu > 0 ? "hold" : "per SCU";

            if (capital > 0 && byWallet < units)
            {
                units = byWallet;
                limiter = "capital";
            }

            if (byStock < units)
            {
                units = byStock;
                limiter = "stock";
            }

            if (units <= 0)
                continue;

            routes.Add(new UexRoute(
                commodity,
                buy.Terminal, buy.Buy,
                sell.Terminal, sell.Sell,
                margin,
                units,
                margin * units,
                buy.Buy * units,
                limiter));
        }

        return [.. routes.OrderByDescending(r => r.Profit).Take(limit)];
    }

    /// <summary>
    /// Every terminal row for one commodity - the map's price shading reads
    /// this to grade sellers by price or capacity. Empty when UEX is off or
    /// the commodity is unknown to it.
    /// </summary>
    public IReadOnlyList<UexMarketRow> Market(string commodity) =>
        _matrix.TryGetValue(commodity, out var rows) ? rows : [];

    /// <summary>
    /// Fetches current prices, the terminal list, vehicle purchase prices and
    /// item prices. Anonymous endpoints, on the user's click only.
    /// </summary>
    /// <remarks>
    /// The fetch happens outside the lock and the commit inside it, with a
    /// generation taken at the start. Since 0.7.0 this can be running on the
    /// background refresher when the player presses Disable, and a fetch that
    /// finished afterwards used to rewrite every file it had just deleted -
    /// turning UEX back on behind them, and re-arming the automatic refresh,
    /// which only runs while UEX is enabled. A stale generation stands down.
    /// </remarks>
    public async Task<int> EnableAsync(HttpClient http, CancellationToken token = default)
    {
        int began;
        lock (_gate)
            began = _generation;

        var priceDoc = await http.GetFromJsonAsync<JsonElement>(PricesUrl, token);
        var terminalDoc = await http.GetFromJsonAsync<JsonElement>(TerminalsUrl, token);
        var vehicleDoc = await http.GetFromJsonAsync<JsonElement>(VehiclePricesUrl, token);
        var itemDoc = await http.GetFromJsonAsync<JsonElement>(ItemPricesUrl, token);

        var (prices, ids, matrix) = DigestPrices(priceDoc);
        if (prices.Count == 0)
            throw new InvalidDataException("UEX returned no commodity prices.");

        var terminals = DigestTerminals(terminalDoc);
        var vehicles = DigestVehicles(vehicleDoc);
        var (itemPrices, itemMarket) = DigestItemPrices(itemDoc);

        lock (_gate)
        {
            // Disable landed while this was in flight, so the player has since
            // said no. Writing now would answer a question nobody asked twice.
            if (began != _generation)
                return 0;

            Directory.CreateDirectory(_directory);
            File.WriteAllText(PricesPath, JsonSerializer.Serialize(prices));
            File.WriteAllText(IdsPath, JsonSerializer.Serialize(ids));
            File.WriteAllText(TerminalsPath, JsonSerializer.Serialize(terminals.Select(t => new[] { (object)t.Id, t.Name })));
            File.WriteAllText(MatrixPath, JsonSerializer.Serialize(matrix));
            File.WriteAllText(VehiclesPath, JsonSerializer.Serialize(vehicles));
            File.WriteAllText(ItemPricesPath, JsonSerializer.Serialize(itemPrices));
            File.WriteAllText(ItemMarketPath, JsonSerializer.Serialize(itemMarket));
            File.WriteAllText(MetaPath, JsonSerializer.Serialize(new Meta(DateTimeOffset.UtcNow)));

            _prices = prices;
            _commodityIds = ids;
            _terminals = terminals;
            _matrix = matrix;
            _vehicles = vehicles;
            _itemPrices = itemPrices;
            _itemMarket = itemMarket;
            FetchedAt = DateTimeOffset.UtcNow;

            // A price table that has just been replaced makes every cached
            // history a description of the previous one.
            _historyCache.Clear();

            return _prices.Count;
        }
    }

    /// <summary>
    /// Which counters to ask about, when a commodity has more than anyone wants
    /// to make requests for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UEX serves history one counter at a time, so a commodity trading at
    /// thirty-five of them would be thirty-five requests to draw one line. The
    /// sample is taken from both ends of the trade instead: the counters with
    /// the most demand, which is where a full hold goes, and the ones with the
    /// most stock, which is where it comes from. A counter that leads both lists
    /// is asked about once.
    /// </para>
    /// <para>
    /// Deliberately not "the best price". Best price is one number and often a
    /// bad plan - it can be a counter wanting nine SCU - and a trend drawn only
    /// from the top of the market describes a market nobody trades in.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<UexMarketRow> SampleTerminals(
        IEnumerable<UexMarketRow> rows, int perSide)
    {
        var all = rows.ToList();

        var demanded = all.Where(r => r.Sell > 0)
            .OrderByDescending(r => r.SellScu)
            .Take(perSide);

        var stocked = all.Where(r => r.Buy > 0)
            .OrderByDescending(r => r.BuyScu)
            .Take(perSide);

        return
        [
            .. demanded.Concat(stocked)
                .DistinctBy(r => r.TerminalId)
                .OrderByDescending(r => Math.Max(r.SellScu, r.BuyScu))
        ];
    }

    /// <summary>
    /// What a commodity has been doing at a sample of its counters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On a click, never on a timer: this is a fetch per counter, and the app
    /// does not spend somebody else's bandwidth speculatively. Kept for
    /// <see cref="HistoryFreshFor"/> afterwards, because reopening a commodity
    /// to look at the other chart should not re-fetch anything.
    /// </para>
    /// <para>
    /// A counter that fails or returns nothing is dropped rather than failing
    /// the request: a chart from six counters is worth having when the seventh
    /// times out.
    /// </para>
    /// </remarks>
    public async Task<UexHistory> HistoryAsync(
        string commodity, HttpClient http, int perSide = 4, CancellationToken token = default)
    {
        // Two readers can open the same commodity at once, and a refresh can
        // replace the tables underneath both, so the lookups are taken together
        // under the lock rather than read from a dictionary being swapped.
        int commodityId;
        List<UexMarketRow> rows;

        lock (_gate)
        {
            if (_historyCache.TryGetValue(commodity, out var cached)
                && DateTimeOffset.UtcNow - cached.At < HistoryFreshFor)
                return cached.History;

            if (!_commodityIds.TryGetValue(commodity, out commodityId)
                || !_matrix.TryGetValue(commodity, out rows!))
                return new UexHistory(commodity, 0, 0, []);
        }

        var sample = SampleTerminals(rows, perSide);

        var fetched = await Task.WhenAll(sample.Select(async row =>
        {
            try
            {
                var url = $"{PriceHistoryUrl}?id_terminal={row.TerminalId}&id_commodity={commodityId}";
                var doc = await http.GetFromJsonAsync<JsonElement>(url, token);
                var points = DigestHistory(doc);

                return points.Count > 0
                    ? new UexTerminalHistory(row.TerminalId, row.Terminal, points)
                    : null;
            }
            catch (Exception e) when (e is HttpRequestException or JsonException or InvalidOperationException)
            {
                return null;
            }
        }));

        var series = fetched.Where(s => s is not null).Select(s => s!).ToList();
        var history = new UexHistory(commodity, series.Count, rows.Count, series);

        lock (_gate)
            _historyCache[commodity] = (DateTimeOffset.UtcNow, history);

        return history;
    }

    /// <summary>Turns one counter's history response into points, oldest first.</summary>
    private static List<UexHistoryPoint> DigestHistory(JsonElement doc)
    {
        var points = new List<UexHistoryPoint>();

        if (!doc.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return points;

        foreach (var row in data.EnumerateArray())
        {
            var added = Number(row, "date_added");
            if (added <= 0)
                continue;

            points.Add(new UexHistoryPoint(
                DateTimeOffset.FromUnixTimeSeconds((long)added),
                Number(row, "price_sell"),
                Number(row, "price_buy"),
                Number(row, "scu_sell"),
                Number(row, "scu_buy")));
        }

        // UEX returns newest first; a chart reads left to right.
        points.Sort((a, b) => a.At.CompareTo(b.At));
        return points;
    }

    /// <summary>A numeric field, however the feed chose to encode it.</summary>
    private static decimal Number(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Number => value.GetDecimal(),
                JsonValueKind.String => decimal.TryParse(
                    value.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
                _ => 0
            }
            : 0;

    /// <summary>Deletes the price cache. Credentials are removed separately.</summary>
    /// <remarks>
    /// Bumps the generation first, so a refresh already fetching stands down
    /// instead of putting everything back. See <see cref="EnableAsync"/>.
    /// </remarks>
    public void Disable()
    {
        lock (_gate)
            DisableCore();
    }

    private void DisableCore()
    {
        _generation++;
        _historyCache.Clear();

        foreach (var path in new[]
        {
            PricesPath, IdsPath, TerminalsPath, MetaPath, MatrixPath, VehiclesPath,
            ItemPricesPath, ItemMarketPath,
        })
            if (File.Exists(path))
                File.Delete(path);

        _prices = new Dictionary<string, UexPrice>(StringComparer.OrdinalIgnoreCase);
        _commodityIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _terminals = [];
        _matrix = new Dictionary<string, List<UexMarketRow>>(StringComparer.OrdinalIgnoreCase);
        _vehicles = new Dictionary<string, UexVehiclePrice>(StringComparer.OrdinalIgnoreCase);
        _itemPrices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        _itemMarket = new Dictionary<string, List<UexItemRow>>(StringComparer.OrdinalIgnoreCase);
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
            var buyScu = (decimal)(Num(row, "scu_buy") ?? 0);
            var sellScu = (decimal)(Num(row, "scu_sell_stock") ?? 0);
            var seen = (long)(Num(row, "date_modified") ?? 0);

            if (!matrix.TryGetValue(name, out var list))
                matrix[name] = list = [];

            list.Add(new UexMarketRow(terminalId, terminal, buy, sell, buyScu, sellScu, seen));

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
                    : 0,
                SeenAt = bestSell?.Seen > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(bestSell.Seen)
                    : null
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

    private static (Dictionary<string, decimal>, Dictionary<string, List<UexItemRow>>)
        DigestItemPrices(JsonElement root)
    {
        var items = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var market = new Dictionary<string, List<UexItemRow>>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return (items, market);

        foreach (var row in data.EnumerateArray())
        {
            var uuid = Str(row, "item_uuid");
            var price = (decimal)(Num(row, "price_buy") ?? 0);
            var terminal = Str(row, "terminal_name");

            if (uuid is null || price <= 0)
                continue;

            if (!items.TryGetValue(uuid, out var existing) || price < existing)
                items[uuid] = price;

            if (terminal is not null)
            {
                if (!market.TryGetValue(uuid, out var rows))
                    market[uuid] = rows = [];
                rows.Add(new UexItemRow(terminal, price));
            }
        }

        return (items, market);
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

            if (File.Exists(ItemMarketPath))
                _itemMarket = JsonSerializer.Deserialize<Dictionary<string, List<UexItemRow>>>(File.ReadAllText(ItemMarketPath))
                    is { } im ? new Dictionary<string, List<UexItemRow>>(im, StringComparer.OrdinalIgnoreCase)
                              : new Dictionary<string, List<UexItemRow>>(StringComparer.OrdinalIgnoreCase);

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
