using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Reading one build number out of two different sentences.
/// </summary>
/// <remarks>
/// The dataset and the game name the same build in different wrappers -
/// scunpacked commits "4.10.0-LIVE.12519617", a log header carries
/// "Build(12519617) 27 Aug 26 (09 47 03)" - so the comparison that answers "is
/// this data older than the patch I am playing?" is exact rather than a guess
/// from dates. Both strings are real: the first from the 27 Aug 2026 dump, the
/// second from this install's own log of the same morning.
/// </remarks>
public class CommunityVersionTests
{
    [Theory]
    [InlineData("4.10.0-LIVE.12519617", "12519617")]
    [InlineData("4.9.0-LIVE.12344265", "12344265")]
    [InlineData("Build(12519617) 27 Aug 26 (09 47 03)", "12519617")]
    [InlineData("Build(12344265) 19 Aug 26 (21 28 37)", "12344265")]
    public void The_build_number_is_read_from_either_wrapper(string stamp, string expected)
    {
        Assert.Equal(expected, CommunityData.BuildIn(stamp));
    }

    /// <summary>
    /// The version parts must not be mistaken for the build. "4.10.0" carries
    /// three numbers before the one that matters, and a reader taking the first
    /// digits it saw would compare 4 against 4 and call everything current.
    /// </summary>
    [Fact]
    public void The_version_in_front_is_not_the_build()
    {
        Assert.Equal("12519617", CommunityData.BuildIn("4.10.0-LIVE.12519617"));
        Assert.NotEqual(CommunityData.BuildIn("4.9.0-LIVE.12344265"),
            CommunityData.BuildIn("4.10.0-LIVE.12519617"));
    }

    /// <summary>
    /// Nothing to read is answered with null, not with an empty string that
    /// would later compare equal to another install's silence.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("remove items.json from LFS tracking")]
    public void A_stamp_with_no_build_in_it_reads_as_nothing(string? stamp)
    {
        Assert.Null(CommunityData.BuildIn(stamp));
    }

    /// <summary>
    /// A fresh install has no dataset and therefore no dump to report - and
    /// must not claim to be behind, which would send someone to fetch a
    /// replacement for nothing.
    /// </summary>
    [Fact]
    public void A_dataset_that_was_never_fetched_names_no_dump()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"qw-community-{Guid.NewGuid():N}");
        var data = new CommunityData(directory);

        Assert.False(data.IsEnabled);
        Assert.Null(data.Dump);
        Assert.Null(data.DumpBuild);
    }
}
