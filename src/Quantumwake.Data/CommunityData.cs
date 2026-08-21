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

    private readonly string _directory;
    private Dictionary<string, CommodityInfo> _byId = new(StringComparer.OrdinalIgnoreCase);

    public CommunityData(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Quantumwake", "community");

        TryLoad();
    }

    private string DigestPath => Path.Combine(_directory, "digest.json");
    private string MetaPath => Path.Combine(_directory, "meta.json");

    public bool IsEnabled => _byId.Count > 0;
    public int Count => _byId.Count;
    public DateTimeOffset? FetchedAt { get; private set; }

    /// <summary>The commodity name for a logged resource id, or null.</summary>
    public string? Commodity(string? resourceId) =>
        resourceId is not null && _byId.TryGetValue(resourceId, out var info) ? info.Name : null;

    /// <summary>Everything known, keyed by resource id.</summary>
    public IReadOnlyDictionary<string, CommodityInfo> All => _byId;

    /// <summary>
    /// Downloads both files, digests them into the local cache, and loads the
    /// result. The only outbound requests in the application; callers own the
    /// consent.
    /// </summary>
    public async Task<int> EnableAsync(HttpClient http, CancellationToken token = default)
    {
        var commoditiesJson = await http.GetStringAsync(CommoditiesUrl, token);
        var tradesJson = await http.GetStringAsync(TradeLocationsUrl, token);

        // Digest before persisting: a failed download or a moved file must not
        // leave a cache that then fails on every startup.
        var digest = Digest(commoditiesJson, tradesJson);
        if (digest.Count == 0)
            throw new InvalidDataException("The community dataset parsed to zero commodities.");

        Directory.CreateDirectory(_directory);
        File.WriteAllText(DigestPath, JsonSerializer.Serialize(digest));
        File.WriteAllText(MetaPath, JsonSerializer.Serialize(new Meta(DateTimeOffset.UtcNow)));

        _byId = digest;
        FetchedAt = DateTimeOffset.UtcNow;
        return _byId.Count;
    }

    /// <summary>Deletes the cache and forgets everything.</summary>
    public void Disable()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        _byId = new Dictionary<string, CommodityInfo>(StringComparer.OrdinalIgnoreCase);
        FetchedAt = null;
    }

    private void TryLoad()
    {
        try
        {
            if (!File.Exists(DigestPath))
                return;

            _byId = JsonSerializer.Deserialize<Dictionary<string, CommodityInfo>>(
                        File.ReadAllText(DigestPath))
                    ?? new Dictionary<string, CommodityInfo>(StringComparer.OrdinalIgnoreCase);

            _byId = new Dictionary<string, CommodityInfo>(_byId, StringComparer.OrdinalIgnoreCase);

            if (File.Exists(MetaPath))
                FetchedAt = JsonSerializer.Deserialize<Meta>(File.ReadAllText(MetaPath))?.FetchedAt;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt cache means the feature is off, not that the app fails.
            _byId = new Dictionary<string, CommodityInfo>(StringComparer.OrdinalIgnoreCase);
        }
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

    private sealed record Meta(DateTimeOffset FetchedAt);
}
