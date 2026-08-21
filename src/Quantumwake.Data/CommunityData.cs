using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// The optional community dataset: commodity names for the resource ids the
/// game logs but never explains.
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
/// off by default, and a single file fetched once into local app data — never
/// on a timer, never at startup, never without the user having pressed the
/// button. The dataset is not shipped in the repository or the binary: it is
/// CIG-derived data under no stated licence, so redistribution is not ours to
/// decide. The user fetching a public file for their own use is.
/// </para>
/// </remarks>
public sealed class CommunityData
{
    /// <summary>Pinned source. Raw file, no API, no query strings.</summary>
    public const string CommoditiesUrl =
        "https://raw.githubusercontent.com/StarCitizenWiki/scunpacked-data/master/resources/commodities.json";

    private readonly string _directory;
    private Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);

    public CommunityData(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Quantumwake", "community");

        TryLoad();
    }

    private string CommoditiesPath => Path.Combine(_directory, "commodities.json");
    private string MetaPath => Path.Combine(_directory, "meta.json");

    public bool IsEnabled => _names.Count > 0;
    public int Count => _names.Count;
    public DateTimeOffset? FetchedAt { get; private set; }

    /// <summary>The commodity name for a logged resource id, or null.</summary>
    public string? Commodity(string? resourceId) =>
        resourceId is not null && _names.TryGetValue(resourceId, out var name) ? name : null;

    /// <summary>
    /// Downloads the dataset into the local cache and loads it. The only
    /// outbound request in the application; callers own the consent.
    /// </summary>
    public async Task<int> EnableAsync(HttpClient http, CancellationToken token = default)
    {
        var json = await http.GetStringAsync(CommoditiesUrl, token);

        // Parse before persisting: a failed download or a moved file must not
        // leave a cache that then fails on every startup.
        var parsed = Parse(json);
        if (parsed.Count == 0)
            throw new InvalidDataException("The community dataset parsed to zero commodities.");

        Directory.CreateDirectory(_directory);
        File.WriteAllText(CommoditiesPath, json);
        File.WriteAllText(MetaPath, JsonSerializer.Serialize(new Meta(DateTimeOffset.UtcNow)));

        _names = parsed;
        FetchedAt = DateTimeOffset.UtcNow;
        return _names.Count;
    }

    /// <summary>Deletes the cache and forgets the names.</summary>
    public void Disable()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        _names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FetchedAt = null;
    }

    private void TryLoad()
    {
        try
        {
            if (!File.Exists(CommoditiesPath))
                return;

            _names = Parse(File.ReadAllText(CommoditiesPath));

            if (File.Exists(MetaPath))
                FetchedAt = JsonSerializer.Deserialize<Meta>(File.ReadAllText(MetaPath))?.FetchedAt;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt cache means the feature is off, not that the app fails.
            _names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Pulls UUID → display name out of the scunpacked commodities file. Only
    /// those two fields are read, so schema drift elsewhere cannot break this.
    /// </summary>
    public static Dictionary<string, string> Parse(string json)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return names;

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("UUID", out var uuid) || uuid.ValueKind != JsonValueKind.String)
                continue;

            var name = entry.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString()
                : null;

            // Some entries have a Key but a null Name; the Key is still a word.
            name ??= entry.TryGetProperty("Key", out var k) && k.ValueKind == JsonValueKind.String
                ? k.GetString()
                : null;

            if (uuid.GetString() is { Length: 36 } id && !string.IsNullOrWhiteSpace(name))
                names[id] = name!;
        }

        return names;
    }

    private sealed record Meta(DateTimeOffset FetchedAt);
}
