using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Turning the install's commodity records into rows a player would recognise.
/// </summary>
/// <remarks>
/// The community download had quietly done this for us. Reading commodities
/// from the game instead meant inheriting the game's own table, which holds
/// unfinished entries and more than one record per commodity — both of which
/// showed up the first time the page was rendered rather than in any diff.
/// </remarks>
public class MarketRowsTests
{
    private static Dictionary<string, string> Named(params (string Id, string Name)[] records) =>
        records.ToDictionary(r => r.Id, r => r.Name, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void An_unfinished_row_is_not_a_commodity()
    {
        var rows = LogLibrary.TradeableRows(Named(("a", "Tin"), ("b", "<= PLACEHOLDER =>")));

        Assert.Equal(["Tin"], rows.Select(r => r.Key));
    }

    /// <summary>
    /// A resource type and its commodity entity both display "Agricium", and
    /// listing both put the same numbers on the page twice.
    /// </summary>
    [Fact]
    public void Records_sharing_a_name_become_one_row()
    {
        var rows = LogLibrary.TradeableRows(Named(("a", "Agricium"), ("b", "agricium"), ("c", "Tin")));

        Assert.Equal(2, rows.Count());
        Assert.Single(rows.Single(r => r.Key.Equals("Agricium", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Value).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Merging must keep every id, because the player's trades are logged
    /// against one of them and the caller sums across all of them.
    /// </summary>
    [Fact]
    public void A_merged_row_still_carries_both_ids()
    {
        var row = LogLibrary.TradeableRows(Named(("a", "Agricium"), ("b", "agricium"))).Single();

        Assert.Equal(["a", "b"], row.Select(p => p.Key).Order());
    }

    /// <summary>
    /// A name that merely contains a real word must not be caught: only the
    /// game's own placeholder wording is dropped.
    /// </summary>
    [Fact]
    public void A_real_commodity_is_never_mistaken_for_a_placeholder()
    {
        var rows = LogLibrary.TradeableRows(Named(("a", "Placeholder Alloy Composite")));

        Assert.Empty(rows);
    }
}
