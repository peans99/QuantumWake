using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>What the community dataset knows about one commodity.</summary>
/// <param name="Sold">Facility keys where kiosks accept it, e.g. <c>DC_Stan_Hurston_S1_Farnesway</c>.</param>
/// <param name="Bought">Facility keys where kiosks stock it.</param>
public sealed record CommodityInfo(
    string Name,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Sold,
    IReadOnlyList<string> Bought);

/// <summary>Reference data for one ship or vehicle.</summary>
/// <param name="ExpeditedCost">Fee to expedite an insurance claim, aUEC.</param>
/// <param name="ExpeditedClaimTime">Expedited claim wait, as the game data states it.</param>
/// <param name="StandardClaimTime">Standard claim wait, same unit.</param>
/// <param name="CargoScu">Cargo grid capacity, SCU. 0 on caches digested before the field existed.</param>
/// <param name="ScmSpeed">SCM speed, m/s. Same caveat, as are the rest.</param>
public sealed record ShipInfo(
    string Name,
    string? Career,
    string? Role,
    int Crew,
    bool IsSpaceship,
    decimal? ExpeditedCost,
    double? ExpeditedClaimTime,
    double? StandardClaimTime,
    double CargoScu = 0,
    double ScmSpeed = 0,
    double MaxSpeed = 0,
    double ShieldHp = 0,
    double Health = 0);

/// <summary>Reference data for one item: what kind of thing it is.</summary>
/// <param name="Uuid">The game's entity uuid — the precise join key to UEX item prices.</param>
/// <param name="Name">The localised display name, when the data carries a real one.</param>
public sealed record ItemInfo(
    string? Type,
    string? SubType,
    int Size,
    int Grade,
    string? Manufacturer,
    string? Uuid = null,
    string? Name = null);

/// <summary>A body's real position within its system, star at the origin.</summary>
public sealed record BodyPosition(double X, double Y);

/// <summary>
/// The optional community dataset: commodity names for the resource ids the
/// game logs but never explains, and where each commodity trades.
/// </summary>
/// <remarks>
/// <para>
/// A cargo sale logs the commodity only as <c>resourceGUID</c>, and that id
/// resolves against nothing in the local install — it is not in the DataCore in
/// any byte order, and the tables that would carry it ship encrypted. The
/// StarCitizenWiki <c>scunpacked-data</c> repository publishes the mapping as
/// JSON, regenerated after each game patch, and it resolves every id this
/// project has seen.
/// </para>
/// <para>
/// Using it is the one place the app touches the network, and it is opt-in,
/// off by default, fetched once into local app data — never on a timer, never
/// at startup, never without the user having pressed the button. The dataset is
/// not shipped in the repository or the binary: it is CIG-derived data under no
/// stated licence, so redistribution is not ours to decide. The user fetching a
/// public file for their own use is.
/// </para>
/// <para>
/// The trade-locations file is 27 MB of room-level rows; it is digested at
/// download time into facility keys per commodity and only the digest is kept,
/// so startup loads kilobytes rather than re-parsing the raw file.
/// </para>
/// </remarks>
public sealed class CommunityData
{
    /// <summary>Pinned sources. Raw files, no API, no query strings.</summary>
    public const string CommoditiesUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/resources/commodities.json";

    public const string TradeLocationsUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/resources/commodity_trade_locations.json";

    public const string ShipsUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/ships.json";

    public const string FpsItemsUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/fps-items.json";

    public const string ShipItemsUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/ship-items.json";

    public const string ManufacturersUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/manufacturers.json";

    public const string StarmapUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/starmap_positions.json";

    private readonly string _directory;
    private Dictionary<string, CommodityInfo> _byId = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ShipInfo> _ships = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ItemInfo> _items = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Dictionary<string, BodyPosition>> _positions = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _manufacturers = new(StringComparer.OrdinalIgnoreCase);

    public CommunityData(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Quantumwake", "community");

        TryLoad();
    }

    private string DigestPath => Path.Combine(_directory, "digest.json");
    private string MetaPath => Path.Combine(_directory, "meta.json");
    private string ShipsDigestPath => Path.Combine(_directory, "digest-ships.json");
    private string ItemsDigestPath => Path.Combine(_directory, "digest-items.json");
    private string PositionsDigestPath => Path.Combine(_directory, "digest-positions.json");
    private string ManufacturersDigestPath => Path.Combine(_directory, "digest-manufacturers.json");

    public bool IsEnabled => _byId.Count > 0;
    public int Count => _byId.Count;
    public DateTimeOffset? FetchedAt { get; private set; }

    /// <summary>The commodity name for a logged resource id, or null.</summary>
    public string? Commodity(string? resourceId) =>
        resourceId is not null && _byId.TryGetValue(resourceId, out var info) ? info.Name : null;

    /// <summary>Everything known, keyed by resource id.</summary>
    public IReadOnlyDictionary<string, CommodityInfo> All => _byId;

    /// <summary>
    /// Ship reference by display name, e.g. "DRAK Corsair". The ship database
    /// keys by class name (<c>DRAK_Corsair</c>), which is the display name with
    /// underscores for spaces; a prefix match catches variant suffixes.
    /// </summary>
    public ShipInfo? Ship(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || _ships.Count == 0)
            return null;

        var key = displayName.Trim().Replace(' ', '_');

        if (_ships.TryGetValue(key, out var exact))
            return exact;

        // Variants: RSI_Aurora_MK2 for "RSI Aurora Mk2" differs only in case
        // (the dictionary ignores it) or carries a suffix; take the shortest
        // class that extends the requested name.
        return _ships
            .Where(p => p.Key.StartsWith(key + "_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Key.Length)
            .Select(p => p.Value)
            .FirstOrDefault();
    }

    /// <summary>Item reference by class name, or null.</summary>
    public ItemInfo? Item(string? itemClass) =>
        itemClass is not null && _items.TryGetValue(itemClass, out var info) ? info : null;

    /// <summary>Every ship in the digest, keyed by class name, for the reference catalogue.</summary>
    public IReadOnlyDictionary<string, ShipInfo> Ships => _ships;

    /// <summary>Manufacturer code to full name ("BEHR" -> "Behring Applied Technology").</summary>
    public IReadOnlyDictionary<string, string> Manufacturers => _manufacturers;

    /// <summary>Every item in the digest, keyed by class name, for the reference catalogue.</summary>
    public IReadOnlyDictionary<string, ItemInfo> Items => _items;

    /// <summary>Real body positions for a system, body name (lower) to coordinates. Empty when unknown.</summary>
    public IReadOnlyDictionary<string, BodyPosition> BodyPositions(string system) =>
        _positions.TryGetValue(system, out var bodies)
            ? bodies
            : new Dictionary<string, BodyPosition>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Downloads the dataset files, digests them into the local cache, and
    /// loads the result. The only scunpacked requests in the application;
    /// callers own the consent.
    /// </summary>
    public async Task<int> EnableAsync(HttpClient http, CancellationToken token = default)
    {
        var commoditiesJson = await http.GetStringAsync(CommoditiesUrl, token);
        var tradesJson = await http.GetStringAsync(TradeLocationsUrl, token);
        var shipsJson = await http.GetStringAsync(ShipsUrl, token);
        var fpsItemsJson = await http.GetStringAsync(FpsItemsUrl, token);
        var shipItemsJson = await http.GetStringAsync(ShipItemsUrl, token);
        var starmapJson = await http.GetStringAsync(StarmapUrl, token);
        var manufacturersJson = await http.GetStringAsync(ManufacturersUrl, token);

        // Digest before persisting: a failed download or a moved file must not
        // leave a cache that then fails on every startup.
        var digest = Digest(commoditiesJson, tradesJson);
        if (digest.Count == 0)
            throw new InvalidDataException("The community dataset parsed to zero commodities.");

        var ships = DigestShips(shipsJson);
        var items = DigestItems(fpsItemsJson, shipItemsJson);
        var positions = DigestPositions(starmapJson);
        var manufacturers = DigestManufacturers(manufacturersJson);

        Directory.CreateDirectory(_directory);
        File.WriteAllText(DigestPath, JsonSerializer.Serialize(digest));
        File.WriteAllText(ShipsDigestPath, JsonSerializer.Serialize(ships));
        File.WriteAllText(ItemsDigestPath, JsonSerializer.Serialize(items));
        File.WriteAllText(PositionsDigestPath, JsonSerializer.Serialize(positions));
        File.WriteAllText(ManufacturersDigestPath, JsonSerializer.Serialize(manufacturers));
        File.WriteAllText(MetaPath, JsonSerializer.Serialize(new Meta(DateTimeOffset.UtcNow)));

        _byId = digest;
        _ships = ships;
        _items = items;
        _positions = positions;
        _manufacturers = manufacturers;
        FetchedAt = DateTimeOffset.UtcNow;
        return _byId.Count;
    }

    /// <summary>Deletes the cache and forgets everything.</summary>
    public void Disable()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        _byId = new Dictionary<string, CommodityInfo>(StringComparer.OrdinalIgnoreCase);
        _ships = new Dictionary<string, ShipInfo>(StringComparer.OrdinalIgnoreCase);
        _items = new Dictionary<string, ItemInfo>(StringComparer.OrdinalIgnoreCase);
        _manufacturers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FetchedAt = null;
    }

    private void TryLoad()
    {
        try
        {
            if (!File.Exists(DigestPath))
                return;

            _byId = Load<CommodityInfo>(DigestPath);
            _ships = Load<ShipInfo>(ShipsDigestPath);
            _items = Load<ItemInfo>(ItemsDigestPath);

            if (File.Exists(PositionsDigestPath))
                _positions = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, BodyPosition>>>(
                        File.ReadAllText(PositionsDigestPath))
                    ?? new Dictionary<string, Dictionary<string, BodyPosition>>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(ManufacturersDigestPath))
                _manufacturers = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        File.ReadAllText(ManufacturersDigestPath))
                    is { } m ? new Dictionary<string, string>(m, StringComparer.OrdinalIgnoreCase)
                             : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(MetaPath))
                FetchedAt = JsonSerializer.Deserialize<Meta>(File.ReadAllText(MetaPath))?.FetchedAt;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt cache means the feature is off, not that the app fails.
            _byId = new Dictionary<string, CommodityInfo>(StringComparer.OrdinalIgnoreCase);
            _ships = new Dictionary<string, ShipInfo>(StringComparer.OrdinalIgnoreCase);
            _items = new Dictionary<string, ItemInfo>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, T> Load<T>(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

        var parsed = JsonSerializer.Deserialize<Dictionary<string, T>>(File.ReadAllText(path));

        return parsed is null
            ? new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, T>(parsed, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Merges the two files into resource id → commodity info. Only the fields
    /// used are read, so schema drift elsewhere cannot break this.
    /// </summary>
    public static Dictionary<string, CommodityInfo> Digest(string commoditiesJson, string tradeLocationsJson)
    {
        var result = new Dictionary<string, CommodityInfo>(StringComparer.OrdinalIgnoreCase);

        // Facilities per commodity first, keyed by the commodity's UUID.
        var sold = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var bought = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        using (var trades = JsonDocument.Parse(tradeLocationsJson))
        {
            if (trades.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in trades.RootElement.EnumerateArray())
                {
                    if (!entry.TryGetProperty("CommodityUUID", out var uuid) || uuid.GetString() is not { Length: 36 } id)
                        continue;

                    sold[id] = Facilities(entry, "SoldAt");
                    bought[id] = Facilities(entry, "BoughtAt");
                }
            }
        }

        using var commodities = JsonDocument.Parse(commoditiesJson);

        if (commodities.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var entry in commodities.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("UUID", out var uuid) || uuid.GetString() is not { Length: 36 } id)
                continue;

            var name = entry.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString()
                : null;

            // Some entries have a Key but a null Name; the Key is still a word.
            name ??= entry.TryGetProperty("Key", out var k) && k.ValueKind == JsonValueKind.String
                ? k.GetString()
                : null;

            // The dataset carries CIG's own placeholder rows; a catalogue that
            // lists "<= PLACEHOLDER =>" is not a catalogue.
            if (string.IsNullOrWhiteSpace(name)
                || name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
                continue;

            var groups = entry.TryGetProperty("CommodityGroups", out var g) && g.ValueKind == JsonValueKind.Array
                ? g.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()!).ToList()
                : [];

            result[id] = new CommodityInfo(
                name!,
                groups,
                sold.GetValueOrDefault(id, []),
                bought.GetValueOrDefault(id, []));
        }

        return result;
    }

    /// <summary>
    /// Rolls room-level rows up to facilities: the class name
    /// <c>DC_Stan_Hurston_S1_Farnesway_CargoShop</c> is one room of the
    /// facility <c>DC_Stan_Hurston_S1_Farnesway</c>, and a commodity that sells
    /// in any room of a facility sells at that facility.
    /// </summary>
    private static List<string> Facilities(JsonElement entry, string side)
    {
        if (!entry.TryGetProperty(side, out var rows) || rows.ValueKind != JsonValueKind.Array)
            return [];

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("TradeLocationClassName", out var c) || c.GetString() is not { Length: > 0 } className)
                continue;

            var parts = className.Split('_');
            keys.Add(string.Join('_', parts.Take(Math.Min(5, parts.Length))));
        }

        return [.. keys.Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Ship class name → reference. 41 MB of specs kept down to the fields the
    /// Fleet page shows: role, crew, and what a claim costs and takes.
    /// </summary>
    public static Dictionary<string, ShipInfo> DigestShips(string shipsJson)
    {
        var result = new Dictionary<string, ShipInfo>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(shipsJson);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var className = Str(entry, "ClassName");
            var name = Str(entry, "Name");

            if (className is null || name is null || name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
                continue;

            decimal? expeditedCost = null;
            double? expedited = null;
            double? standard = null;

            if (entry.TryGetProperty("Insurance", out var insurance) && insurance.ValueKind == JsonValueKind.Object)
            {
                expeditedCost = Num(insurance, "ExpeditedCost") is { } cost ? (decimal)cost : null;
                expedited = Num(insurance, "ExpeditedClaimTime");
                standard = Num(insurance, "StandardClaimTime");
            }

            // The spec-sheet numbers: SCM and top speed sit under
            // FlightCharacteristics.Speeds; the rest are top-level.
            double scm = 0;
            double max = 0;

            if (entry.TryGetProperty("FlightCharacteristics", out var flight)
                && flight.ValueKind == JsonValueKind.Object
                && flight.TryGetProperty("Speeds", out var speeds)
                && speeds.ValueKind == JsonValueKind.Object)
            {
                scm = Num(speeds, "Scm") ?? 0;
                max = Num(speeds, "Max") ?? 0;
            }

            result[className] = new ShipInfo(
                name,
                Str(entry, "Career"),
                Str(entry, "Role"),
                (int)(Num(entry, "Crew") ?? 0),
                entry.TryGetProperty("IsSpaceship", out var s) && s.ValueKind == JsonValueKind.True,
                expeditedCost,
                expedited,
                standard,
                Num(entry, "Cargo") ?? 0,
                scm,
                max,
                Num(entry, "ShieldHp") ?? 0,
                Num(entry, "Health") ?? 0);
        }

        return result;
    }

    /// <summary>
    /// Item class name → what kind of thing it is, from both the FPS and the
    /// ship item files - the loadout holds armour and the spending history
    /// holds power plants, and both deserve a size and a maker.
    /// </summary>
    /// <summary>Manufacturer code to full display name, placeholders skipped.</summary>
    public static Dictionary<string, string> DigestManufacturers(string manufacturersJson)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(manufacturersJson);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var code = Str(entry, "Code");
            var name = Str(entry, "Name");

            if (code is { Length: > 0 } && name is { Length: > 0 }
                && !name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
                result[code] = name;
        }

        return result;
    }

    public static Dictionary<string, ItemInfo> DigestItems(params string[] jsonFiles)
    {
        var result = new Dictionary<string, ItemInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var json in jsonFiles)
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                var className = Str(entry, "className");
                if (className is null)
                    continue;

                string? manufacturer = null;
                if (entry.TryGetProperty("stdItem", out var std) && std.ValueKind == JsonValueKind.Object
                    && std.TryGetProperty("Manufacturer", out var maker) && maker.ValueKind == JsonValueKind.Object)
                {
                    manufacturer = Str(maker, "Name");
                    if (manufacturer is "Unknown Manufacturer")
                        manufacturer = null;
                }

                // The real display name, when localisation gave the item one.
                var name = Str(entry, "name");
                if (name is null || name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
                    name = null;

                result[className] = new ItemInfo(
                    Str(entry, "type"),
                    Str(entry, "subType") is "UNDEFINED" or null ? null : Str(entry, "subType"),
                    (int)(Num(entry, "size") ?? 0),
                    (int)(Num(entry, "grade") ?? 0),
                    manufacturer,
                    Str(entry, "reference"),
                    name);
            }
        }

        return result;
    }

    /// <summary>
    /// Per system, the real coordinates of its planets and moons - star at the
    /// origin. Entities repeat and most types are noise; planets and moons are
    /// what the map lays out.
    /// </summary>
    public static Dictionary<string, Dictionary<string, BodyPosition>> DigestPositions(string starmapJson)
    {
        var result = new Dictionary<string, Dictionary<string, BodyPosition>>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(starmapJson);

        if (!doc.RootElement.TryGetProperty("entities", out var entities)
            || entities.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var entry in entities.EnumerateArray())
        {
            var type = Str(entry, "type");
            if (type is not ("Planet" or "Moon"))
                continue;

            var system = Str(entry, "system");
            var name = Str(entry, "name");
            var x = Num(entry, "x");
            var y = Num(entry, "y");

            if (system is null || name is null || x is null || y is null)
                continue;

            if (!result.TryGetValue(system, out var bodies))
                result[system] = bodies = new Dictionary<string, BodyPosition>(StringComparer.OrdinalIgnoreCase);

            // Entities repeat; the coordinates agree, so first wins.
            bodies.TryAdd(name, new BodyPosition(x.Value, y.Value));
        }

        return result;
    }

    private static string? Str(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? Num(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private sealed record Meta(DateTimeOffset FetchedAt);
}
