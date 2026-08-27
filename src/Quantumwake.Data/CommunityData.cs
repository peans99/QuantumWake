using Quantumwake.Core;
using System.Text.Json;
using System.Text.RegularExpressions;

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

/// <summary>
/// One port on a ship that the player is allowed to change.
/// </summary>
/// <param name="Port">
/// The data's own id for this port, unique within the ship. Two turrets both
/// call their gun "hardpoint_class_2", so counting how many of something a
/// ship has needs an identity the name cannot give.
/// </param>
/// <param name="Hardpoint">The game's name for the port, e.g. hardpoint_shield_generator.</param>
/// <param name="Kind">What fits: QuantumDrive, Shield, Cooler, PowerPlant, WeaponGun...</param>
/// <param name="Size">The component size the port takes. Ports with a range are split by size.</param>
/// <param name="Fitted">The part in it as the ship comes, when the data names one.</param>
public sealed record ShipSlot(
    string Port,
    string Hardpoint,
    string Kind,
    int Size,
    string? Fitted,
    int FittedGrade,
    string? FittedUuid);

/// <summary>A body's real position within its system, star at the origin.</summary>
public sealed record BodyPosition(double X, double Y);

/// <summary>
/// One resource spawning at one named location: the game's own deposit tables.
/// </summary>
/// <param name="Resource">The material or thing ("Quartz", "Amiant Pod", a derelict hull).</param>
/// <param name="Deposit">The vein context when the data carries one ("Asteroid C Type Mineable Rock").</param>
/// <param name="GroupChance">The spawn group's probability, 0..1.</param>
/// <param name="Share">This deposit's share within its group, 0..1.</param>
public sealed record ResourceSpawn(
    string Resource,
    string? Deposit,
    string Kind,
    string Location,
    string? System,
    string Group,
    double GroupChance,
    double Share);

/// <summary>One crafting blueprint: what it makes, from what, and how it is obtained.</summary>
/// <param name="OutputUuid">The crafted item's entity uuid - joins to UEX item prices.</param>
/// <param name="Materials">Flattened recipe lines ("Agricium 0.36 SCU", "Hadanite ×7").</param>
/// <param name="RewardPools">Prettified reward pool keys when not known by default.</param>
public sealed record BlueprintInfo(
    string Output,
    string? OutputUuid,
    string? Type,
    int Grade,
    string Kind,
    int CraftSeconds,
    IReadOnlyList<string> Materials,
    bool Default,
    IReadOnlyList<string> RewardPools);

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
public sealed partial class CommunityData
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

    public const string ResourcesUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/resources/resources.json";

    public const string ResourceLocationsUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/resources/locations.json";

    public const string BlueprintsUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/blueprints.json";

    public const string StarmapInfoUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/starmap.json";

    public const string StarmapUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/starmap_positions.json";

    /// <summary>
    /// The repository history, read once per fetch to learn which game build
    /// the files were dumped from.
    /// </summary>
    /// <remarks>
    /// scunpacked stamps each dump commit with the build it was generated
    /// against - "4.10.0-LIVE.12519617" - and a Star Citizen log names the same
    /// number in its BackupNameAttachment. That makes "is this dataset older
    /// than the patch I am playing?" a comparison of two build numbers rather
    /// than of two dates, which is the difference between an answer and a
    /// guess: dumps land days after a patch, so a date says stale when it is
    /// merely later.
    /// </remarks>
    public const string HistoryUrl =
        "https://api.github.com/repos/StarCitizenWiki/scunpacked-data/commits?per_page=20";

    private readonly string _directory;

    private Dictionary<string, CommodityInfo> _byId = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ShipInfo> _ships = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<ShipSlot>> _shipSlots = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ItemInfo> _items = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Dictionary<string, BodyPosition>> _positions = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _manufacturers = new(StringComparer.OrdinalIgnoreCase);
    private List<ResourceSpawn> _resourceSpawns = [];
    private List<BlueprintInfo> _blueprints = [];
    private Dictionary<string, string> _placeLore = new(StringComparer.OrdinalIgnoreCase);

    public CommunityData(string? directory = null)
    {
        _directory = directory ?? AppPaths.In("community");

        TryLoad();
    }

    private string DigestPath => Path.Combine(_directory, "digest.json");
    private string MetaPath => Path.Combine(_directory, "meta.json");
    private string ShipsDigestPath => Path.Combine(_directory, "digest-ships.json");
    private string SlotsDigestPath => Path.Combine(_directory, "digest-ship-slots.json");
    private string ItemsDigestPath => Path.Combine(_directory, "digest-items.json");
    private string PositionsDigestPath => Path.Combine(_directory, "digest-positions.json");
    private string ManufacturersDigestPath => Path.Combine(_directory, "digest-manufacturers.json");
    private string ResourceSpawnsDigestPath => Path.Combine(_directory, "digest-resource-spawns.json");
    private string BlueprintsDigestPath => Path.Combine(_directory, "digest-blueprints.json");
    private string PlaceLoreDigestPath => Path.Combine(_directory, "digest-place-lore.json");

    public bool IsEnabled => _byId.Count > 0;
    public int Count => _byId.Count;
    public DateTimeOffset? FetchedAt { get; private set; }

    /// <summary>
    /// The dump these files came from - "4.10.0-LIVE.12519617" - or null when
    /// the history could not be read, or when the cache predates this being
    /// recorded.
    /// </summary>
    public string? Dump { get; private set; }

    /// <summary>The build number inside <see cref="Dump"/>, for comparing.</summary>
    public string? DumpBuild => BuildIn(Dump);

    /// <summary>
    /// The build number in a dump stamp or a log’s build tag.
    /// </summary>
    /// <remarks>
    /// One reader for both because they carry the same number in different
    /// wrappers: "4.10.0-LIVE.12519617" from the dataset, "Build(12519617) 27
    /// Aug 26 (09 47 03)" from a log header. Matching the last run of digits
    /// of real length keeps the 4, the 10 and the 0 out of it.
    /// </remarks>
    public static string? BuildIn(string? stamp)
    {
        if (string.IsNullOrWhiteSpace(stamp))
            return null;

        var m = BuildNumberRegex().Match(stamp);

        return m.Success ? m.Groups["build"].Value : null;
    }

    [GeneratedRegex(@"(?<build>\d{6,})", RegexOptions.Compiled)]
    private static partial Regex BuildNumberRegex();

    /// <summary>A dump commit subject, which is the build stamp and nothing else.</summary>
    [GeneratedRegex(@"^\d+\.\d+(\.\d+)?-[A-Za-z]+\.\d{6,}$", RegexOptions.Compiled)]
    private static partial Regex DumpStampRegex();


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

    /// <summary>
    /// The ports on a ship the player can change, by the same display name
    /// <see cref="Ship"/> takes. Empty when the ship is unknown, and equally
    /// when the reference data predates this build - the caller says which.
    /// </summary>
    public IReadOnlyList<ShipSlot> Slots(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || _shipSlots.Count == 0)
            return [];

        var key = displayName.Trim().Replace(' ', '_');

        if (_shipSlots.TryGetValue(key, out var exact))
            return exact;

        return _shipSlots
            .Where(p => p.Key.StartsWith(key + "_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Key.Length)
            .Select(p => (IReadOnlyList<ShipSlot>)p.Value)
            .FirstOrDefault() ?? [];
    }

    /// <summary>True once the reference data carries ship ports at all.</summary>
    public bool HasSlots => _shipSlots.Count > 0;

    /// <summary>Item reference by class name, or null.</summary>
    public ItemInfo? Item(string? itemClass) =>
        itemClass is not null && _items.TryGetValue(itemClass, out var info) ? info : null;

    /// <summary>Every ship in the digest, keyed by class name, for the reference catalogue.</summary>
    public IReadOnlyDictionary<string, ShipInfo> Ships => _ships;

    /// <summary>Manufacturer code to full name ("BEHR" -> "Behring Applied Technology").</summary>
    public IReadOnlyDictionary<string, string> Manufacturers => _manufacturers;

    /// <summary>The game's resource deposit tables: what spawns where, and how likely.</summary>
    public IReadOnlyList<ResourceSpawn> ResourceSpawns => _resourceSpawns;

    /// <summary>Every crafting blueprint the game data describes.</summary>
    public IReadOnlyList<BlueprintInfo> Blueprints => _blueprints;

    /// <summary>The starmap's own description of a place, by display name. Null when it has none.</summary>
    public string? PlaceLore(string? name) =>
        name is not null && _placeLore.TryGetValue(name, out var lore) ? lore : null;

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
        var resourcesJson = await http.GetStringAsync(ResourcesUrl, token);
        var resourceLocationsJson = await http.GetStringAsync(ResourceLocationsUrl, token);
        var blueprintsJson = await http.GetStringAsync(BlueprintsUrl, token);
        var starmapInfoJson = await http.GetStringAsync(StarmapInfoUrl, token);

        // Digest before persisting: a failed download or a moved file must not
        // leave a cache that then fails on every startup.
        var digest = Digest(commoditiesJson, tradesJson);
        if (digest.Count == 0)
            throw new InvalidDataException("The community dataset parsed to zero commodities.");

        var ships = DigestShips(shipsJson);
        var slots = DigestShipSlots(shipsJson);
        var items = DigestItems(fpsItemsJson, shipItemsJson);
        var positions = DigestPositions(starmapJson);
        var manufacturers = DigestManufacturers(manufacturersJson);
        var spawns = DigestResourceSpawns(resourcesJson, resourceLocationsJson);
        var blueprints = DigestBlueprints(blueprintsJson);
        var lore = DigestPlaceLore(starmapInfoJson);

        Directory.CreateDirectory(_directory);
        File.WriteAllText(DigestPath, JsonSerializer.Serialize(digest));
        File.WriteAllText(ShipsDigestPath, JsonSerializer.Serialize(ships));
        File.WriteAllText(SlotsDigestPath, JsonSerializer.Serialize(slots));
        File.WriteAllText(ItemsDigestPath, JsonSerializer.Serialize(items));
        File.WriteAllText(PositionsDigestPath, JsonSerializer.Serialize(positions));
        File.WriteAllText(ManufacturersDigestPath, JsonSerializer.Serialize(manufacturers));
        File.WriteAllText(ResourceSpawnsDigestPath, JsonSerializer.Serialize(spawns));
        File.WriteAllText(BlueprintsDigestPath, JsonSerializer.Serialize(blueprints));
        File.WriteAllText(PlaceLoreDigestPath, JsonSerializer.Serialize(lore));
        // Cosmetic, so it must not be able to fail the fetch: the files are
        // already downloaded and digested by here, and a dataset that works
        // while declining to name its dump is better than no dataset.
        var dump = await ReadDumpAsync(http, token);

        File.WriteAllText(MetaPath, JsonSerializer.Serialize(new Meta(DateTimeOffset.UtcNow, dump)));


        _byId = digest;
        _ships = ships;
        _shipSlots = slots;
        _items = items;
        _positions = positions;
        _manufacturers = manufacturers;
        _resourceSpawns = spawns;
        _blueprints = blueprints;
        _placeLore = lore;
        FetchedAt = DateTimeOffset.UtcNow;
        Dump = dump;
        return _byId.Count;

    }

    /// <summary>
    /// The newest dump stamp in the repository history, or null.
    /// </summary>
    /// <remarks>
    /// Only subjects that are a build stamp and nothing else are taken, so
    /// housekeeping commits - "remove items.json from LFS tracking" sits
    /// between two dumps in this history - are skipped rather than recorded as
    /// the version of the data.
    /// </remarks>
    private static async Task<string?> ReadDumpAsync(HttpClient http, CancellationToken token)
    {
        try
        {
            using var document = JsonDocument.Parse(await http.GetStringAsync(HistoryUrl, token));

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("commit", out var commit)
                    || !commit.TryGetProperty("message", out var message))
                    continue;

                var subject = (message.GetString() ?? string.Empty)
                    .Split('\n')[0].Trim();

                if (DumpStampRegex().IsMatch(subject))
                    return subject;
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
        }

        return null;
    }

    /// <summary>Deletes the cache and forgets everything.</summary>

    public void Disable()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        _byId = new Dictionary<string, CommodityInfo>(StringComparer.OrdinalIgnoreCase);
        _ships = new Dictionary<string, ShipInfo>(StringComparer.OrdinalIgnoreCase);
        _shipSlots = new Dictionary<string, List<ShipSlot>>(StringComparer.OrdinalIgnoreCase);
        _items = new Dictionary<string, ItemInfo>(StringComparer.OrdinalIgnoreCase);
        _manufacturers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _resourceSpawns = [];
        _blueprints = [];
        _placeLore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FetchedAt = null;
        Dump = null;
    }

    private void TryLoad()
    {
        try
        {
            if (!File.Exists(DigestPath))
                return;

            _byId = Load<CommodityInfo>(DigestPath);
            _ships = Load<ShipInfo>(ShipsDigestPath);

            // Added after the first releases, so an install that downloaded
            // the dataset before this build has everything except this file.
            // Missing means "not known yet", which the pages say out loud
            // rather than showing a ship with no ports.
            if (File.Exists(SlotsDigestPath))
                _shipSlots = JsonSerializer.Deserialize<Dictionary<string, List<ShipSlot>>>(
                        File.ReadAllText(SlotsDigestPath))
                    is { } s ? new Dictionary<string, List<ShipSlot>>(s, StringComparer.OrdinalIgnoreCase)
                             : new Dictionary<string, List<ShipSlot>>(StringComparer.OrdinalIgnoreCase);

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

            if (File.Exists(ResourceSpawnsDigestPath))
                _resourceSpawns = JsonSerializer.Deserialize<List<ResourceSpawn>>(
                    File.ReadAllText(ResourceSpawnsDigestPath)) ?? [];

            if (File.Exists(BlueprintsDigestPath))
                _blueprints = JsonSerializer.Deserialize<List<BlueprintInfo>>(
                    File.ReadAllText(BlueprintsDigestPath)) ?? [];

            if (File.Exists(PlaceLoreDigestPath))
                _placeLore = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        File.ReadAllText(PlaceLoreDigestPath))
                    is { } lore ? new Dictionary<string, string>(lore, StringComparer.OrdinalIgnoreCase)
                                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(MetaPath))
            {
                // A cache written before dumps were recorded deserialises with
                // a null Dump, which reads as "not known" - the honest answer.
                var meta = JsonSerializer.Deserialize<Meta>(File.ReadAllText(MetaPath));
                FetchedAt = meta?.FetchedAt;
                Dump = meta?.Dump;
            }

        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt cache means the feature is off, not that the app fails.
            _byId = new Dictionary<string, CommodityInfo>(StringComparer.OrdinalIgnoreCase);
            _ships = new Dictionary<string, ShipInfo>(StringComparer.OrdinalIgnoreCase);
            _shipSlots = new Dictionary<string, List<ShipSlot>>(StringComparer.OrdinalIgnoreCase);
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
    /// The parts a ship can be shopped for: every port the game lets the
    /// player change, with the size it takes and what it comes with.
    /// </summary>
    /// <remarks>
    /// The loadout is a tree - a turret holds its guns, a quantum drive holds
    /// its jump drive - and every entry carries both what is fitted and what
    /// the port accepts. Only editable ports are kept, because a fixed one is
    /// not a decision, and only the kinds that are actually bought: a ship has
    /// thirty-four manoeuvring thrusters and a dozen doors, and no shop sells
    /// either. A port with a size range becomes one slot per size, since that
    /// is the question being asked of the shop.
    /// </remarks>
    public static Dictionary<string, List<ShipSlot>> DigestShipSlots(string shipsJson)
    {
        var result = new Dictionary<string, List<ShipSlot>>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(shipsJson);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var className = Str(entry, "ClassName");

            if (className is null || !entry.TryGetProperty("Loadout", out var loadout))
                continue;

            var slots = new List<ShipSlot>();
            Walk(loadout, slots);

            if (slots.Count > 0)
                result[className] = slots;
        }

        return result;

        static void Walk(JsonElement ports, List<ShipSlot> into)
        {
            if (ports.ValueKind != JsonValueKind.Array)
                return;

            foreach (var port in ports.EnumerateArray())
            {
                Keep(port, into);

                // A gun hangs off a turret and a jump drive off a quantum
                // drive, so the children are ports too.
                if (port.TryGetProperty("Loadout", out var children))
                    Walk(children, into);
            }
        }

        static void Keep(JsonElement port, List<ShipSlot> into)
        {
            if (!port.TryGetProperty("Editable", out var editable) || editable.ValueKind != JsonValueKind.True)
                return;

            if (!port.TryGetProperty("CompatibleTypes", out var types) || types.ValueKind != JsonValueKind.Array)
                return;

            var hardpoint = Str(port, "HardpointName") ?? "?";
            var min = (int)(Num(port, "MinSize") ?? 0);
            var max = (int)(Num(port, "MaxSize") ?? min);

            // The fitted part is what the ship flies with today, which is the
            // only thing a candidate can be judged against.
            var fitted = Str(port, "Name");
            if (fitted is not null && fitted.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
                fitted = null;

            foreach (var accepted in types.EnumerateArray())
            {
                var kind = Str(accepted, "Type");

                if (kind is null || !Shoppable.Contains(kind))
                    continue;

                for (var size = min; size <= max && size <= 12; size++)
                    into.Add(new ShipSlot(
                        Str(port, "PortId") ?? hardpoint,
                        hardpoint,
                        kind,
                        size,
                        fitted,
                        (int)(Num(port, "Grade") ?? 0),
                        Str(port, "UUID")));
            }
        }
    }

    /// <summary>
    /// The port kinds worth shopping for.
    /// </summary>
    /// <remarks>
    /// Deliberately short. Every ship is a hundred ports of screens, doors,
    /// lights and thrusters, none of which anyone buys; these are the ones
    /// that sit behind a shop counter and change how the ship flies or fights.
    /// A kind nothing is sold for simply comes back empty, so the cost of
    /// keeping one too many here is nothing.
    /// </remarks>
    private static readonly HashSet<string> Shoppable = new(StringComparer.OrdinalIgnoreCase)
    {
        "QuantumDrive", "Shield", "PowerPlant", "Cooler",
        "WeaponGun", "Turret", "MissileLauncher", "Missile",
        "Radar", "EMP", "QuantumInterdictionGenerator", "MiningArm",
    };

    /// <summary>
    /// Item class name → what kind of thing it is, from both the FPS and the
    /// ship item files - the loadout holds armour and the spending history
    /// holds power plants, and both deserve a size and a maker.
    /// </summary>
    /// <summary>
    /// The game's own deposit spawn tables, flattened: every (resource, named
    /// location) pair with its spawn group's probability and this deposit's
    /// share within the group. Mineable names carry the material as a suffix
    /// ("AsteroidCTypeMineableRock_Quartz"), so the suffix becomes the resource
    /// and the rest the vein context; test, template and lootbox rows are noise
    /// and dropped.
    /// </summary>
    public static List<ResourceSpawn> DigestResourceSpawns(string resourcesJson, string locationsJson)
    {
        // uuid -> (display name source, kind)
        var byUuid = new Dictionary<string, (string Name, string Kind)>(StringComparer.OrdinalIgnoreCase);

        using (var resourceDoc = JsonDocument.Parse(resourcesJson))
        {
            if (resourceDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in resourceDoc.RootElement.EnumerateArray())
                {
                    var uuid = Str(entry, "UUID");
                    var kind = Str(entry, "Kind");
                    var name = Str(entry, "Name");

                    // Placeholder display names fall back to the class key,
                    // which still names the deposit ("GPI_Icicle").
                    if (name is null || name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
                        name = Str(entry, "Key");

                    if (uuid is null || kind is null || name is null)
                        continue;

                    if (name.Contains("Test", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("template", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Lootbox", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Placeholder", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Blocker", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Obstacle", StringComparison.OrdinalIgnoreCase))
                        continue;

                    byUuid[uuid] = (name, kind);
                }
            }
        }

        var spawns = new List<ResourceSpawn>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var locationDoc = JsonDocument.Parse(locationsJson);
        if (locationDoc.RootElement.ValueKind != JsonValueKind.Array)
            return spawns;

        foreach (var provider in locationDoc.RootElement.EnumerateArray())
        {
            if (!provider.TryGetProperty("Locations", out var locations)
                || locations.ValueKind != JsonValueKind.Array
                || !provider.TryGetProperty("Groups", out var groups)
                || groups.ValueKind != JsonValueKind.Array)
                continue;

            var places = locations.EnumerateArray()
                .Select(l => (Name: Str(l, "Name"), System: Str(l, "System")))
                .Where(l => l.Name is { Length: > 0 })
                .Distinct()
                .ToList();

            if (places.Count == 0)
                continue;

            foreach (var group in groups.EnumerateArray())
            {
                var groupName = Str(group, "GroupName") ?? "?";
                var groupChance = Num(group, "GroupProbability") ?? 0;

                if (!group.TryGetProperty("Deposits", out var deposits)
                    || deposits.ValueKind != JsonValueKind.Array)
                    continue;

                var rows = deposits.EnumerateArray()
                    .Select(d => (Uuid: Str(d, "ResourceUUID"), Weight: Num(d, "RelativeProbability") ?? 0))
                    .Where(d => d.Uuid is not null && byUuid.ContainsKey(d.Uuid!))
                    .ToList();

                var totalWeight = rows.Sum(d => d.Weight);
                if (totalWeight <= 0)
                    continue;

                foreach (var (uuid, weight) in rows)
                {
                    var (rawName, kind) = byUuid[uuid!];
                    var (resource, deposit) = SplitResource(rawName);

                    foreach (var (placeName, system) in places)
                    {
                        // The same provider is often attached to a location
                        // several times; one row per fact is enough.
                        if (!seen.Add($"{resource}|{deposit}|{placeName}|{groupName}"))
                            continue;

                        spawns.Add(new ResourceSpawn(
                            resource, deposit, kind, placeName!, system,
                            groupName.Replace('_', ' '),
                            Math.Round(groupChance, 4),
                            Math.Round(weight / totalWeight, 4)));
                    }
                }
            }
        }

        return spawns;
    }

    /// <summary>"AsteroidCTypeMineableRock_Quartz" -> ("Quartz", "Asteroid C Type Mineable Rock").</summary>
    private static (string Resource, string? Deposit) SplitResource(string name)
    {
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2 && parts[^1].Length >= 3 && parts[^1].All(char.IsLetter))
            return (PrettyWords(parts[^1]), PrettyWords(string.Join(' ', parts[..^1])));

        return (PrettyWords(name), null);
    }

    /// <summary>"MineableRockFPS" -> "Mineable Rock FPS": camel-case split for display.</summary>
    private static string PrettyWords(string value) =>
        System.Text.RegularExpressions.Regex.Replace(
            value.Replace('_', ' '),
            "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])",
            " ");

    /// <summary>
    /// The crafting blueprints: output item, first-tier craft time and
    /// materials (the requirement tree flattened to its resource and item
    /// leaves), and how the blueprint is obtained.
    /// </summary>
    public static List<BlueprintInfo> DigestBlueprints(string blueprintsJson)
    {
        var result = new List<BlueprintInfo>();

        using var doc = JsonDocument.Parse(blueprintsJson);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("Output", out var output) || output.ValueKind != JsonValueKind.Object)
                continue;

            var name = Str(output, "Name");
            if (name is null || name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
                continue;

            var isDefault = false;
            var pools = new List<string>();

            if (entry.TryGetProperty("Availability", out var availability)
                && availability.ValueKind == JsonValueKind.Object)
            {
                isDefault = availability.TryGetProperty("Default", out var d)
                    && d.ValueKind == JsonValueKind.True;

                if (availability.TryGetProperty("RewardPools", out var rewardPools)
                    && rewardPools.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pool in rewardPools.EnumerateArray())
                    {
                        var key = Str(pool, "Key");
                        if (key is not null)
                            pools.Add(PrettyWords(key.Replace("BP_REWARDS_", "")));
                    }
                }
            }

            var craftSeconds = 0;
            var materials = new List<string>();

            if (entry.TryGetProperty("Tiers", out var tiers)
                && tiers.ValueKind == JsonValueKind.Array
                && tiers.GetArrayLength() > 0)
            {
                var tier = tiers[0];
                craftSeconds = (int)(Num(tier, "CraftTimeSeconds") ?? 0);

                if (tier.TryGetProperty("Requirements", out var requirements))
                    CollectMaterials(requirements, materials);
            }

            result.Add(new BlueprintInfo(
                name,
                Str(output, "UUID"),
                Str(output, "Type"),
                int.TryParse(Str(output, "Grade"), out var grade) ? grade : 0,
                Str(entry, "Kind") ?? "creation",
                craftSeconds,
                materials.Distinct().ToList(),
                isDefault,
                pools.Distinct().ToList()));
        }

        return result;
    }

    /// <summary>Walks a blueprint requirement tree collecting its material leaves.</summary>
    private static void CollectMaterials(JsonElement node, List<string> materials)
    {
        if (node.ValueKind != JsonValueKind.Object)
            return;

        var kind = Str(node, "Kind");
        var name = Str(node, "Name");

        if (name is not null && !name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            if (kind == "resource")
            {
                var scu = Num(node, "QuantityScu") ?? 0;
                materials.Add(scu > 0 ? $"{name} {scu:0.##} SCU" : name);
            }
            else if (kind == "item")
            {
                var quantity = Num(node, "Quantity") ?? 0;
                materials.Add(quantity > 1 ? $"{name} ×{quantity:0}" : name);
            }
        }

        if (node.TryGetProperty("Children", out var children) && children.ValueKind == JsonValueKind.Array)
            foreach (var child in children.EnumerateArray())
                CollectMaterials(child, materials);
    }

    /// <summary>
    /// The starmap's own descriptions, name to text - the paragraph the game
    /// shows about a station or outpost, for the map's detail card.
    /// </summary>
    public static Dictionary<string, string> DigestPlaceLore(string starmapJson)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(starmapJson);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var name = Str(entry, "Name");
            var description = Str(entry, "Description");

            if (name is null || description is null
                || name.Contains("UNINITIALIZED") || name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
                || description.Contains("UNINITIALIZED")
                || description.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
                || description.Trim().Length < 30)
                continue;

            result.TryAdd(name.Trim(), description.Trim());
        }

        return result;
    }

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

    private sealed record Meta(DateTimeOffset FetchedAt, string? Dump = null);
}
