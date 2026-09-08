using Quantumwake.Core.GameData;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// Joining the install's deposits to the download's, without losing either.
/// </summary>
/// <remarks>
/// Ore and place look like they identify a deposit and do not. The same ore sits
/// in different rocks at one place at wildly different concentrations, so taking
/// whichever install row came first advertised a rich deposit at trace
/// concentration - and dropped the variants it displaced as already covered.
/// </remarks>
public class SpawnMergeTests
{
    private static GameSpawn Install(
        string resource, string location, string deposit, double min, double max,
        string group = "Cave Rich") =>
        new(resource, deposit, min, max, "mineable", location, "Stanton", group,
            0.5, 1, new QualityBand(501, 1000, 750, 200), 3600);

    private static ResourceSpawn Dataset(string resource, string location) =>
        new(resource, null, "mineable", location, "Stanton", "Rocks", 0.4, 1);

    /// <summary>
    /// At Fuego borase is 9.7-74.3% of a Borase (Ore) deposit and 2-5% of a
    /// Bexalite (Raw) one. Neither may be stamped on the download's row, and
    /// neither may be thrown away to make room for it.
    /// </summary>
    [Fact]
    public void Conflicting_variants_are_kept_and_none_is_stamped_on_the_download_row()
    {
        var merged = SpawnMerge.Merge(
            [
                Install("Borase", "Fuego", "Borase (Ore)", 9.7, 74.3),
                Install("Borase", "Fuego", "Bexalite (Raw)", 2, 5),
            ],
            [Dataset("Borase", "Fuego")]);

        var fromDataset = Assert.Single(merged, m => m.Source == "dataset");
        Assert.Null(fromDataset.MinPercent);
        Assert.Null(fromDataset.MaxPercent);

        var kept = merged.Where(m => m.Source == "install").ToList();
        Assert.Equal(2, kept.Count);
        Assert.Contains(kept, m => m.Deposit == "Borase (Ore)" && m.MinPercent == 9.7);
        Assert.Contains(kept, m => m.Deposit == "Bexalite (Raw)" && m.MinPercent == 2);
    }

    /// <summary>
    /// The common case, and the one worth keeping: an ore drawn from Cave Rich,
    /// Cave Medium and Cave Poor is one deposit listed three times, so there is
    /// nothing to choose between and the download's row gets its richness.
    /// </summary>
    [Fact]
    public void Variants_that_agree_still_enrich_the_download_row()
    {
        var merged = SpawnMerge.Merge(
            [
                Install("Aphorite", "Hurston", "Aphorite", 50, 100, "Cave Rich"),
                Install("Aphorite", "Hurston", "Aphorite", 50, 100, "Cave Medium"),
                Install("Aphorite", "Hurston", "Aphorite", 50, 100, "Cave Poor"),
            ],
            [Dataset("Aphorite", "Hurston")]);

        var row = Assert.Single(merged);
        Assert.Equal("both", row.Source);
        Assert.Equal(50, row.MinPercent);
        Assert.Equal(100, row.MaxPercent);
        Assert.Equal(3600, row.RespawnSeconds);
    }

    /// <summary>
    /// The install names an ore "Copper Ore" where the download says "Copper",
    /// and the join has to see through that or nothing matches at all.
    /// </summary>
    [Fact]
    public void The_two_spellings_of_one_ore_still_join()
    {
        var merged = SpawnMerge.Merge(
            [Install("Copper Ore", "Daymar", "Copper", 10, 40)],
            [Dataset("Copper", "Daymar")]);

        Assert.Equal("both", Assert.Single(merged).Source);
    }

    [Fact]
    public void A_deposit_the_download_never_had_is_kept()
    {
        var merged = SpawnMerge.Merge(
            [Install("Quantainium", "Yela", "Quantainium", 3, 9)],
            [Dataset("Copper", "Daymar")]);

        Assert.Equal(2, merged.Count);
        Assert.Contains(merged, m => m.Resource == "Quantainium" && m.Source == "install");
    }

    [Fact]
    public void With_no_download_every_install_row_is_returned()
    {
        var merged = SpawnMerge.Merge(
            [
                Install("Borase", "Fuego", "Borase (Ore)", 9.7, 74.3),
                Install("Borase", "Fuego", "Bexalite (Raw)", 2, 5),
            ],
            []);

        Assert.Equal(2, merged.Count);
        Assert.All(merged, m => Assert.Equal("install", m.Source));
    }
}
