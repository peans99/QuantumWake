using System.Text;
using System.Text.Json;

namespace SCCompanion.Core.GameData;

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

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly Dictionary<string, string> _items;
    private readonly Dictionary<string, string> _vehicles;

    private GameNames(Dictionary<string, string> items, Dictionary<string, string> vehicles)
    {
        _items = items;
        _vehicles = vehicles;
    }

    /// <summary>An empty table, used when the archive is unavailable.</summary>
    public static GameNames Empty { get; } = new(new(StringComparer.OrdinalIgnoreCase),
                                                 new(StringComparer.OrdinalIgnoreCase));

    public int ItemCount => _items.Count;
    public int VehicleCount => _vehicles.Count;
    public bool IsLoaded => _items.Count > 0 || _vehicles.Count > 0;

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

        var stamp = new FileInfo(archivePath).LastWriteTimeUtc.Ticks.ToString();

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

        foreach (var line in ini.Split('\n'))
        {
            var split = line.IndexOf('=');
            if (split <= 0)
                continue;

            var key = line[..split].TrimStart('﻿');
            var value = line[(split + 1)..].TrimEnd('\r').Trim();

            if (value.Length == 0)
                continue;

            // Both "item_Namexyz" and "item_Name_xyz" occur, so the separator is
            // trimmed. Missing it hides everything using the underscored form -
            // most armour, among others.
            if (key.StartsWith("item_Name", StringComparison.OrdinalIgnoreCase))
                items.TryAdd(key["item_Name".Length..].TrimStart('_'), value);
            else if (key.StartsWith("vehicle_Name", StringComparison.OrdinalIgnoreCase))
                vehicles.TryAdd(key["vehicle_Name".Length..].TrimStart('_'), value);
        }

        return new GameNames(items, vehicles);
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
                new Dictionary<string, string>(cache.Vehicles, StringComparer.OrdinalIgnoreCase));
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
                Vehicles = names._vehicles
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
    }
}
