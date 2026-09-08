using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Keeping a mining record the game does not keep.
/// </summary>
/// <remarks>
/// Everything here is typed rather than read, so the store's job is to refuse
/// entries that could not be true and to keep the rest exactly as given.
/// </remarks>
public class MiningLogStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-mining-{Guid.NewGuid():N}");

    public MiningLogStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void A_run_survives_a_restart()
    {
        new MiningLogStore(_directory).Add("Aberdeen", "Hadanite", 12, 740, 480_000, null);

        var again = new MiningLogStore(_directory).All();

        Assert.Single(again);
        Assert.Equal("Hadanite", again[0].Resource);
        Assert.Equal(740, again[0].Quality);
    }

    [Theory]
    [InlineData("", 12)]
    [InlineData("Hadanite", 0)]
    [InlineData("Hadanite", -4)]
    public void A_run_that_is_not_a_run_is_refused(string resource, double scu)
    {
        Assert.Null(new MiningLogStore(_directory).Add("Aberdeen", resource, scu, null, null, null));
    }

    /// <summary>
    /// The game's quality scale is 1 to 1000. Anything outside it is a typo, and
    /// storing it would put a reading on the page no rock could have had.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    [InlineData(-50)]
    public void A_quality_off_the_scale_is_dropped_rather_than_stored(int quality)
    {
        var run = new MiningLogStore(_directory).Add("Aberdeen", "Hadanite", 12, quality, null, null);

        Assert.NotNull(run);
        Assert.Null(run.Quality);
    }

    /// <summary>
    /// Mining somewhere you did not note is still mining, so the run is kept
    /// rather than refused for want of a place name.
    /// </summary>
    [Fact]
    public void A_run_with_no_place_is_still_a_run()
    {
        Assert.Equal("somewhere", new MiningLogStore(_directory)
            .Add(null, "Hadanite", 12, null, null, null)?.Place);
    }

    [Fact]
    public void A_removed_run_stays_removed()
    {
        var store = new MiningLogStore(_directory);
        var run = store.Add("Aberdeen", "Hadanite", 12, null, null, null)!;

        Assert.True(store.Remove(run.Id));
        Assert.Empty(new MiningLogStore(_directory).All());
    }

    [Fact]
    public void The_newest_run_is_first()
    {
        var store = new MiningLogStore(_directory);
        store.Add("Aberdeen", "First", 1, null, null, null);
        store.Add("Daymar", "Second", 1, null, null, null);

        Assert.Equal("Second", store.All()[0].Resource);
    }
}
