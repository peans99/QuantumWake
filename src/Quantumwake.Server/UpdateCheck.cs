using System.Net.Http.Json;
using System.Text.Json;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>The published single file, and what it should turn out to be.</summary>
/// <param name="Digest">
/// GitHub's own SHA-256 for the asset, as <c>sha256:...</c>. Published by the
/// API rather than by this project, so there is nothing extra to upload and
/// nothing to forget - and it is what makes replacing the running application
/// safe enough to do without asking a human to check anything.
/// </param>
public sealed record ReleaseAsset(string Name, string Url, long Size, string? Digest);

/// <summary>What a look at the release feed found.</summary>
/// <param name="Newer">True when the published release is ahead of this build.</param>
/// <param name="Notes">The release's own words, for the page to show before anyone downloads.</param>
/// <param name="Asset">The file an update would install, when the release carries one.</param>
public sealed record UpdateResult(
    bool Newer,
    string Current,
    string? Latest,
    string? Url,
    string? Notes,
    DateTimeOffset? PublishedAt,
    ReleaseAsset? Asset = null);

/// <summary>
/// Looks up the newest published release, when asked to.
/// </summary>
/// <remarks>
/// <para>
/// One GET of a public JSON feed, carrying nothing but the request itself: no
/// identifier, no version, no telemetry. The app cannot be told a version is
/// current by a server that has not been told which version is asking, and that
/// is the intended trade - a check learns what is out, GitHub learns nothing
/// about who asked.
/// </para>
/// <para>
/// Never called on a timer and never on a start unless the player has said yes.
/// The comparison is done here rather than by the page so "newer" means one
/// thing: a real version comparison, not a string that differs.
/// </para>
/// </remarks>
public sealed class UpdateCheck(IHttpClientFactory factory, ILogger<UpdateCheck> logger)
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/peans99/QuantumWake/releases/latest";

    /// <summary>Where a human goes to read about and download a release.</summary>
    public const string ReleasesPage = "https://github.com/peans99/QuantumWake/releases/latest";

    public async Task<UpdateResult> LookAsync(string currentVersion, CancellationToken token = default)
    {
        try
        {
            using var client = factory.CreateClient("community");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var response = await client.GetAsync(LatestReleaseUrl, token);
            response.EnsureSuccessStatusCode();

            var release = await response.Content.ReadFromJsonAsync<JsonElement>(token);

            var tag = Text(release, "tag_name");

            return new UpdateResult(
                Newer: UpdateStore.IsNewer(currentVersion, tag),
                Current: currentVersion,
                Latest: tag?.TrimStart('v', 'V'),
                Url: Text(release, "html_url") ?? ReleasesPage,
                Notes: Text(release, "body"),
                PublishedAt: release.TryGetProperty("published_at", out var at)
                             && at.TryGetDateTimeOffset(out var published)
                    ? published
                    : null,
                Asset: SingleFile(release));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Being unable to look is not a problem with the running copy, so it
            // is logged and reported as "no news", never surfaced as an error.
            logger.LogDebug(e, "Update check could not reach the release feed.");
            return new UpdateResult(false, currentVersion, null, ReleasesPage, null, null);
        }
    }

    /// <summary>
    /// The one-file download, out of the assets a release carries.
    /// </summary>
    /// <remarks>
    /// Matched on the exact name rather than on an extension: the release also
    /// carries a zip and a CLI, and installing either over the running
    /// application would replace it with something that is not it.
    /// </remarks>
    private static ReleaseAsset? SingleFile(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (Text(asset, "name") is not "QuantumWake.exe")
                continue;

            if (Text(asset, "browser_download_url") is not { Length: > 0 } url)
                continue;

            return new ReleaseAsset(
                "QuantumWake.exe",
                url,
                asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0,
                Text(asset, "digest"));
        }

        return null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
