using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Commodity names read from the install rather than downloaded.
/// </summary>
/// <remarks>
/// The lookups are exercised here without an install; the reading itself is
/// checked against the real archive, because a fixture of a 316 MB proprietary
/// blob is neither possible nor useful. What matters and is testable here is
/// that a missing or unreadable install degrades naming and never the app.
/// </remarks>
public class GameCommoditiesTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-gc-{Guid.NewGuid():N}");

    public GameCommoditiesTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Cache => Path.Combine(_directory, "commodities.json");

    [Fact]
    public void No_install_is_not_an_error()
    {
        var names = GameCommodities.Load(null, Cache);

        Assert.False(names.IsLoaded);
        Assert.Null(names.Commodity("dc6fbcbb-5990-4ed5-82ee-93152dab7845"));
    }

    /// <summary>
    /// A folder with no Data.p4k is the ordinary case for someone who has moved
    /// their install, and it must read as "nothing known" rather than throw.
    /// </summary>
    [Fact]
    public void An_install_without_the_archive_reads_as_empty()
    {
        var fake = Path.Combine(_directory, "LIVE");
        Directory.CreateDirectory(fake);

        Assert.False(GameCommodities.Load(fake, Cache).IsLoaded);
    }

    [Fact]
    public void An_unknown_id_returns_null_rather_than_a_guess()
    {
        Assert.Null(GameCommodities.Empty.Commodity("not-a-guid"));
        Assert.Null(GameCommodities.Empty.Commodity(null));
        Assert.Null(GameCommodities.Empty.Commodity(""));
    }

    /// <summary>
    /// A cache written by an older build must be ignored rather than trusted,
    /// or a patch would keep serving the previous patch's names.
    /// </summary>
    [Fact]
    public void A_cache_from_another_build_is_not_used()
    {
        File.WriteAllText(Cache, """{"Stamp":"0:1","Commodities":{"x":"Stale"}}""");

        Assert.False(GameCommodities.Load(null, Cache).IsLoaded);
    }
}
