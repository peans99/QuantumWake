using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Quantumwake.Core.GameData;

/// <summary>
/// Commodity names read from the game's own files, by the id the logs carry.
/// </summary>
/// <remarks>
/// <para>
/// A cargo sale logs a commodity as a GUID that nothing in the app could
/// resolve, which is why naming has needed an opt-in 110 MB community download.
/// It turns out the install already answers it: the GUID is a record id in
/// <c>Data\Game2.dcb</c>, the record carries a <c>displayName</c> localisation
/// key, and <c>global.ini</c> holds the English behind it.
/// </para>
/// <para>
/// Measured against that download on this install: all 203 of its commodities
/// are named, 185 word for word. The rest differ in presentation only — the dump
/// writes "Agricium (Ore)" where the game says "Ore Agricium" — and none
/// disagrees. Of the commodities this install has actually traded, 12 of 13 come
/// from the English table and one, Iron, from the fallback below.
/// </para>
/// <para>
/// The blob is 316 MB decompressed, so it is read once and the answer cached.
/// The cache is stamped with the archive's write time: a patch invalidates it,
/// and nothing else has to notice.
/// </para>
/// </remarks>
public sealed partial class GameCommodities
{
    /// <summary>Bumped when the cached shape changes.</summary>
    private const int CacheVersion = 1;

    private const string DataCoreEntry = @"Data\Game2.dcb";
    private const string LocalisationEntry = @"Data\Localization\english\global.ini";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly Dictionary<string, string> _byId;

    private GameCommodities(Dictionary<string, string> byId) => _byId = byId;

    /// <summary>Nothing known, used when the archive is unreadable.</summary>
    public static GameCommodities Empty { get; } = new(new(StringComparer.OrdinalIgnoreCase));

    public int Count => _byId.Count;
    public bool IsLoaded => _byId.Count > 0;

    /// <summary>The game's name for a logged resource id, or null.</summary>
    public string? Commodity(string? resourceId) =>
        resourceId is { Length: > 0 } && _byId.TryGetValue(resourceId, out var name) ? name : null;

    /// <summary>Everything read, keyed by resource id.</summary>
    public IReadOnlyDictionary<string, string> All => _byId;

    /// <summary>
    /// Loads from the install, using the cache when it is current.
    /// </summary>
    public static GameCommodities Load(string? installRoot, string cachePath)
    {
        if (installRoot is null) return Empty;

        var archive = P4kArchive.PathFor(installRoot);
        if (!File.Exists(archive)) return Empty;

        var stamp = $"{CacheVersion}:{new FileInfo(archive).LastWriteTimeUtc.Ticks}";

        if (TryLoadCache(cachePath, stamp) is { } cached) return cached;

        var built = Read(archive);
        if (built.Count > 0) SaveCache(cachePath, stamp, built);

        return new GameCommodities(built);
    }

    private static Dictionary<string, string> Read(string archivePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var p4k = new P4kArchive(archivePath);

            var blob = p4k.TryRead(DataCoreEntry);
            var ini = p4k.TryRead(LocalisationEntry);

            if (blob is null || ini is null) return result;

            var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in Encoding.UTF8.GetString(ini).Split('\n'))
            {
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;

                var value = line[(eq + 1)..].TrimEnd('\r').Trim();
                if (value.Length > 0) text.TryAdd(line[..eq].TrimStart('﻿').Trim(), value);
            }

            var core = new DataCore(blob);

            // Only the records that can appear as cargo. Walking all 116,921 and
            // resolving each one's name costs far more than it returns.
            foreach (var record in core.Records())
            {
                if (!record.Name.StartsWith("ResourceType.", StringComparison.OrdinalIgnoreCase)
                    && !record.FileName.Contains("entities/commodities/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Name(core, text, record) is { Length: > 0 } name)
                    result.TryAdd(record.Hash.ToString(), name);
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // A missing or unreadable archive degrades naming, never the app.
            return result;
        }

        return result;
    }

    /// <summary>
    /// What the game calls a record.
    /// </summary>
    /// <remarks>
    /// Falls back to the record's own class name, spaced at word boundaries,
    /// when the key has no English behind it. That is not invention: 25 of this
    /// install's commodities have a well-formed key CIG have not filled in —
    /// <c>@items_commodities_iron</c> among them — and the class name is the
    /// game's own word for the thing.
    /// </remarks>
    private static string? Name(DataCore core, Dictionary<string, string> text, DataRecord record)
    {
        if (core.TextProperty(record, "displayName") is { Length: > 0 } key
            && text.TryGetValue(key.TrimStart('@'), out var english))
        {
            return english;
        }

        var bare = record.Name.Contains('.')
            ? record.Name[(record.Name.LastIndexOf('.') + 1)..]
            : record.Name;

        return bare.Length > 0 ? WordBoundary().Replace(bare.Replace('_', ' '), " ").Trim() : null;
    }

    private static GameCommodities? TryLoadCache(string cachePath, string stamp)
    {
        try
        {
            if (!File.Exists(cachePath)) return null;

            var cache = JsonSerializer.Deserialize<Cache>(File.ReadAllText(cachePath));
            if (cache is null || cache.Stamp != stamp) return null;

            return new GameCommodities(
                new Dictionary<string, string>(cache.Commodities, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return null;
        }
    }

    private static void SaveCache(string cachePath, string stamp, Dictionary<string, string> names)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllText(cachePath,
                JsonSerializer.Serialize(new Cache { Stamp = stamp, Commodities = names }, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing the cache only costs a slower next start.
        }
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex WordBoundary();

    private sealed class Cache
    {
        public string Stamp { get; set; } = string.Empty;
        public Dictionary<string, string> Commodities { get; set; } = [];
    }
}
