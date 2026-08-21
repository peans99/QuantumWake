using System.Text;
using System.Text.Json;

namespace Quantumwake.Core.GameData;

/// <summary>
/// Resolves engine identifiers to the names players actually see.
/// </summary>
/// <remarks>
/// <para>
/// The logs only ever name things in engine terms - <c>behr_rifle_ballistic_01_white02</c>,
/// <c>ANVL_Hornet_F7CM_Mk2</c>. The game's own localisation table maps those to
/// "P4-AR &quot;Boneyard&quot; Rifle" and "Anvil F7C-M Super Hornet Mk II", which
/// turns unreadable item walls into something meaningful.
/// </para>
/// <para>
/// The table lives at <c>Data\Localization\english\global.ini</c> inside
/// <c>Data.p4k</c>: about 90,000 entries, 10 MB decompressed. Extracting it means
/// walking 1.36 million central directory records, so the useful subset is cached
/// to local app data and only rebuilt when the archive changes.
/// </para>
/// <para>
/// Every lookup falls back to the raw identifier. A missing or unreadable archive
/// degrades the display, never the app.
/// </para>
/// </remarks>
public sealed class GameNames
{
    private const string LocalizationEntry = @"Data\Localization\english\global.ini";

    /// <summary>Bumped whenever the cached shape changes.</summary>
    private const int CacheVersion = 2;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>
    /// Key prefixes that identify a place rather than an item or a line of
    /// dialogue. Location keys carry no functional prefix of their own, so they
    /// have to be recognised by the id itself.
    /// </summary>
    private static readonly string[] PlacePrefixes =
        ["Stanton", "Pyro", "RR_", "ATC_", "OOC_", "LOC_", "Terra", "Nyx"];

    private readonly Dictionary<string, string> _items;
    private readonly Dictionary<string, string> _vehicles;
    private readonly Dictionary<string, string> _places;
    private readonly Dictionary<string, string> _shops;

    private GameNames(
        Dictionary<string, string> items,
        Dictionary<string, string> vehicles,
        Dictionary<string, string> places,
        Dictionary<string, string> shops)
    {
        _items = items;
        _vehicles = vehicles;
        _places = places;
        _shops = shops;
    }

    /// <summary>An empty table, used when the archive is unavailable.</summary>
    public static GameNames Empty { get; } = new(
        new(StringComparer.OrdinalIgnoreCase),
        new(StringComparer.OrdinalIgnoreCase),
        new(StringComparer.OrdinalIgnoreCase),
        new(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Every location id the game publishes a name for.
    /// </summary>
    /// <remarks>
    /// The visited-places list only ever holds somewhere the player has actually
    /// stood. This is the other half - the whole map, so unvisited places can be
    /// drawn too.
    /// </remarks>
    public IEnumerable<string> PlaceIds => _places.Keys;

    public int ItemCount => _items.Count;
    public int VehicleCount => _vehicles.Count;
    public int PlaceCount => _places.Count;
    public int ShopCount => _shops.Count;
    public bool IsLoaded => _items.Count > 0 || _vehicles.Count > 0;

    /// <summary>
    /// The game's own name for a location id, or null when it has none.
    /// </summary>
    /// <remarks>
    /// Worth preferring over anything derived: <c>RR_P5_L2</c> is really
    /// "Gaslight" and <c>Pyro2_Outpost_col_m_scrp_indy_001</c> is "Sunset Mesa",
    /// neither of which is guessable from the id.
    /// </remarks>
    public string? Place(string locationId)
    {
        if (string.IsNullOrWhiteSpace(locationId))
            return null;

        return _places.TryGetValue(locationId, out var name) ? name : null;
    }

    /// <summary>
    /// The game's name for a shop id, or null.
    /// </summary>
    /// <remarks>
    /// Shop ids bury the brand among layout tokens -
    /// <c>SCShop_lt_a_casaba_small_base_a-002</c> is a Casaba Outlet - so every
    /// token is tested rather than just the first, and a token matches a table
    /// key if either is a prefix of the other (<c>LiveFire</c> finds
    /// <c>livefireweapons</c>, <c>ShubinInterstellar</c> finds <c>shubin</c>).
    /// The longest match wins so "Casaba Outlet" beats a stray short key.
    /// Spaces count as separators too, so an id already broken up for display
    /// resolves as readily as the raw one.
    /// </remarks>
    public string? Shop(string shopId)
    {
        if (string.IsNullOrWhiteSpace(shopId) || _shops.Count == 0)
            return null;

        var body = shopId.StartsWith("SCShop_", StringComparison.OrdinalIgnoreCase)
            ? shopId["SCShop_".Length..]
            : shopId;

        string? best = null;
        var bestLength = 0;

        foreach (var token in body.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var probe = Normalise(token);
            if (probe.Length < 3)
                continue;

            foreach (var (key, name) in _shops)
            {
                var related = probe.Equals(key, StringComparison.OrdinalIgnoreCase)
                    || probe.StartsWith(key, StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith(probe, StringComparison.OrdinalIgnoreCase);

                if (related && key.Length > bestLength)
                {
                    best = name;
                    bestLength = key.Length;
                }
            }
        }

        return best;
    }

    private static string Normalise(string value) =>
        new([.. value.Where(char.IsLetterOrDigit)]);

    /// <summary>Display name for an item class, or the class itself.</summary>
    public string Item(string itemClass)
    {
        if (string.IsNullOrWhiteSpace(itemClass))
            return itemClass;

        if (_items.TryGetValue(itemClass, out var name))
            return name;

        // Variants tack suffixes onto a base class: behr_rifle_ballistic_01_white02
        // falls back to behr_rifle_ballistic_01 when the variant is not listed.
        var trimmed = itemClass;
        while (true)
        {
            var cut = trimmed.LastIndexOf('_');
            if (cut <= 0)
                break;

            trimmed = trimmed[..cut];

            if (_items.TryGetValue(trimmed, out var baseName))
                return baseName;
        }

        return itemClass;
    }

    /// <summary>Display name for a vehicle id, or a tidied version of the id.</summary>
    public string Vehicle(string vehicleId)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
            return vehicleId;

        return _vehicles.TryGetValue(vehicleId, out var name)
            ? name
            : vehicleId.Replace('_', ' ');
    }

    /// <summary>
    /// Loads the table, using the cache when it is current.
    /// </summary>
    /// <param name="installRoot">Channel directory containing Data.p4k.</param>
    /// <param name="cachePath">Where to keep the extracted subset.</param>
    public static GameNames Load(string? installRoot, string cachePath)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
            return Empty;

        var archivePath = P4kArchive.PathFor(installRoot);
        if (!File.Exists(archivePath))
            return Empty;

        // The schema version is part of the stamp so adding a table rebuilds the
        // cache. Without it a new table stays empty until the next game patch.
        var stamp = $"{CacheVersion}:{new FileInfo(archivePath).LastWriteTimeUtc.Ticks}";

        if (TryLoadCache(cachePath, stamp) is { } cached)
            return cached;

        try
        {
            var raw = new P4kArchive(archivePath).TryRead(LocalizationEntry);
            if (raw is null)
                return Empty;

            var names = Parse(Encoding.UTF8.GetString(raw));
            SaveCache(cachePath, stamp, names);
            return names;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // A game update mid-read, or a locked file. Names are a nicety.
            return Empty;
        }
    }

    /// <summary>
    /// Pulls the name keys out of the ini. Descriptions are skipped - they run to
    /// paragraphs and would bloat the cache for no display value.
    /// </summary>
    private static GameNames Parse(string ini)
    {
        var items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var vehicles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var places = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var shops = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in ini.Split('\n'))
        {
            var split = line.IndexOf('=');
            if (split <= 0)
                continue;

            var key = line[..split].TrimStart('﻿');
            var value = line[(split + 1)..].TrimEnd('\r').Trim();

            if (value.Length == 0)
                continue;

            // Some keys carry a ",P" grammatical variant suffix.
            var comma = key.IndexOf(',');
            if (comma > 0)
                key = key[..comma];

            if (IsPlaceKey(key))
                places.TryAdd(key, value);

            if (key.StartsWith("shop_name_", StringComparison.OrdinalIgnoreCase))
                shops.TryAdd(key["shop_name_".Length..], value);

            // Both "item_Namexyz" and "item_Name_xyz" occur, so the separator is
            // trimmed. Missing it hides everything using the underscored form -
            // most armour, among others.
            if (key.StartsWith("item_Name", StringComparison.OrdinalIgnoreCase))
                items.TryAdd(key["item_Name".Length..].TrimStart('_'), value);
            else if (key.StartsWith("vehicle_Name", StringComparison.OrdinalIgnoreCase))
                vehicles.TryAdd(key["vehicle_Name".Length..].TrimStart('_'), value);
        }

        return new GameNames(items, vehicles, places, shops);
    }

    /// <summary>
    /// True for keys that name a place. Descriptive and sub-facility suffixes
    /// are excluded so a station's blurb never becomes its name.
    /// </summary>
    private static bool IsPlaceKey(string key)
    {
        var matchesPrefix = false;

        foreach (var prefix in PlacePrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                matchesPrefix = true;
                break;
            }
        }

        if (!matchesPrefix)
            return false;

        return !key.EndsWith("_desc", StringComparison.OrdinalIgnoreCase)
            && !key.EndsWith("_addr", StringComparison.OrdinalIgnoreCase)
            && !key.Contains("_SK_", StringComparison.Ordinal)
            && !key.Contains("_MG_", StringComparison.Ordinal);
    }

    private static GameNames? TryLoadCache(string cachePath, string stamp)
    {
        try
        {
            if (!File.Exists(cachePath))
                return null;

            var cache = JsonSerializer.Deserialize<NameCache>(File.ReadAllText(cachePath));

            if (cache is null || cache.Stamp != stamp)
                return null;

            return new GameNames(
                new Dictionary<string, string>(cache.Items, StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(cache.Vehicles, StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(cache.Places, StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(cache.Shops, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return null;
        }
    }

    private static void SaveCache(string cachePath, string stamp, GameNames names)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

            var cache = new NameCache
            {
                Stamp = stamp,
                Items = names._items,
                Vehicles = names._vehicles,
                Places = names._places,
                Shops = names._shops
            };

            File.WriteAllText(cachePath, JsonSerializer.Serialize(cache, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing the cache only costs a slower next start.
        }
    }

    private sealed class NameCache
    {
        public string Stamp { get; set; } = string.Empty;
        public Dictionary<string, string> Items { get; set; } = [];
        public Dictionary<string, string> Vehicles { get; set; } = [];
        public Dictionary<string, string> Places { get; set; } = [];
        public Dictionary<string, string> Shops { get; set; } = [];
    }
}
