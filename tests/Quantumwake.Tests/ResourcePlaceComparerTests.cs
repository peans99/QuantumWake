using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// Joining the two mining sources on the same deposit.
/// </summary>
/// <remarks>
/// Neither source is a superset: the download reaches 234 places and the
/// install knows how rich a rock is, what quality it assays at and how long it
/// takes to come back. Merging them needs the same ore at the same place to be
/// recognised through the different names each source gives it.
/// </remarks>
public class ResourcePlaceComparerTests
{
    private static bool Same(string aOre, string aPlace, string bOre, string bPlace) =>
        ResourcePlaceComparer.Instance.Equals((aOre, aPlace), (bOre, bPlace));

    /// <summary>
    /// The install writes "Copper Ore" where the download writes "Copper".
    /// Matching the raw strings joined 164 rows; matching through this joins 259.
    /// </summary>
    [Theory]
    [InlineData("Copper Ore", "Copper")]
    [InlineData("Agricium Ore", "Agricium")]
    [InlineData("GroundVehicle Beradom", "Beradom")]
    [InlineData("FPS Hadanite", "Hadanite")]
    public void The_same_ore_named_differently_is_one_ore(string install, string dataset)
    {
        Assert.True(Same(install, "Daymar", dataset, "Daymar"));
    }

    [Fact]
    public void Case_alone_never_separates_a_deposit()
    {
        Assert.True(Same("hadanite", "aberdeen", "Hadanite", "Aberdeen"));
    }

    /// <summary>
    /// Two genuinely different ores must stay apart, however alike they read.
    /// </summary>
    [Fact]
    public void Different_ores_stay_different()
    {
        Assert.False(Same("Copper", "Daymar", "Gold", "Daymar"));
        Assert.False(Same("Copper", "Daymar", "Copper", "Yela"));
    }

    /// <summary>
    /// The install says Aluminium and the download says Aluminum. Quietly
    /// treating those as one would be a guess about spelling, not a
    /// normalisation of format, so they are left apart.
    /// </summary>
    [Fact]
    public void A_spelling_difference_is_not_assumed_away()
    {
        Assert.False(Same("Aluminium Ore", "Daymar", "Aluminum", "Daymar"));
    }

    [Fact]
    public void Equal_pairs_agree_on_their_hash()
    {
        Assert.Equal(
            ResourcePlaceComparer.Instance.GetHashCode(("Copper Ore", "Daymar")),
            ResourcePlaceComparer.Instance.GetHashCode(("copper", "daymar")));
    }
}
