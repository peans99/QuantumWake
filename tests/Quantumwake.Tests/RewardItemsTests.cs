using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Reading an event tier's reward line into catalogue items.
/// </summary>
/// <remarks>
/// The lines and names are Discovery Month's as 4.10.2 has them. The badge
/// carries no item, so the name is the only join, and these pin down the four
/// shapes the line takes: a plain item with a noun added, a hull's two blades
/// given once, a size range, and an old name for a renamed hull.
/// </remarks>
public class RewardItemsTests
{
    private static readonly Dictionary<string, (string Class, string Kind)> Catalogue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BriskAir IC-10"] = ("COOL_IASI_S01_Name", "Cooler"),
        ["Delphi RS-10"] = ("RADR_IASI_S01_Name", "Radar"),
        ["Sovereign IP-10"] = ("POWR_S01_IASI_Name_SCItem", "PowerPlant"),
        ["Helios IP-10"] = ("POWR_S01_IASI_NameA_SCItem", "PowerPlant"),
        ["Helios IP-20"] = ("POWR_S02_IASI_NameA_SCItem", "PowerPlant"),
        ["Helios IP-30"] = ("POWR_S03_IASI_NameA_SCItem", "PowerPlant"),
        ["Mantis PHB Flight Blade"] = ("Controller_Flight_RSI_Mantis_Blade_HND", "FlightController"),
        ["Mantis TSB Flight Blade"] = ("Controller_Flight_RSI_Mantis_Blade_SPD", "FlightController"),
        ["Zeus Mk II CL PHB Flight Blade"] = ("Controller_Flight_RSI_Zeus_CL_Blade_HND", "FlightController"),
        ["Zeus Mk II ES PHB Flight Blade"] = ("Controller_Flight_RSI_Zeus_ES_Blade_HND", "FlightController"),
        ["Constellation Mk IV Andromeda PHB Flight Blade"] = ("Controller_Flight_RSI_Constellation_Andromeda_Blade_HND", "FlightController"),
        ["Zeus Keelback Livery"] = ("Paint_Zeus_Blue_Blue_Yellow", "Paint"),
        ["P8-SC \"Echo\" SMG"] = ("behr_smg_ballistic_01_iasi", "WeaponPersonal"),
        // Its words contain "CA" and "OP" without being them.
        ["Welcome to Pyro Cassiopeia Operations 1 Datapad"] = ("Carryable_Datapad", "Misc"),
    };

    private static IReadOnlyList<GameRewardItem> Match(string line) => RewardItems.Match(line, Catalogue);

    [Fact]
    public void A_line_of_three_items_reads_as_three_with_the_added_nouns_taken_off()
    {
        var items = Match("BriskAir IC-10 Cooler, Delphi RS-10 Radar, and Sovereign IP-10 Power Plant");

        Assert.Equal(["COOL_IASI_S01_Name", "RADR_IASI_S01_Name", "POWR_S01_IASI_Name_SCItem"], items.Select(i => i.Class));
        Assert.Equal(["Cooler", "Radar", "PowerPlant"], items.Select(i => i.Kind));
        Assert.All(items, i => Assert.False(i.OneOf));
    }

    /// <summary>"Mantis PHB and TSB Flight Blades" names the hull once for both blades.</summary>
    [Fact]
    public void A_hulls_two_blades_given_once_are_both_found()
    {
        var items = Match("Mantis PHB and TSB Flight Blades");

        Assert.Equal(["Mantis PHB Flight Blade", "Mantis TSB Flight Blade"], items.Select(i => i.Name));
    }

    [Fact]
    public void A_size_range_is_every_size_of_it()
    {
        var items = Match("Helios Power Plant (Sizes 1-3)");

        Assert.Equal(["Helios IP-10", "Helios IP-20", "Helios IP-30"], items.Select(i => i.Name));
    }

    /// <summary>
    /// The text was written before the Constellations became "Mk IV"; every
    /// word of it still appears, in order, in exactly one name.
    /// </summary>
    [Fact]
    public void An_old_name_for_a_renamed_hull_still_finds_its_item()
    {
        var item = Assert.Single(Match("Constellation Andromeda PHB"));

        Assert.Equal("Constellation Mk IV Andromeda PHB Flight Blade", item.Name);
        Assert.False(item.OneOf);
    }

    /// <summary>
    /// "Zeus Mk II PHB" fits the CL and the ES blade. Both are given and marked
    /// as one of, rather than one picked and presented as the answer.
    /// </summary>
    [Fact]
    public void A_name_that_fits_several_items_gives_them_all_as_one_of()
    {
        var items = Match("Zeus Mk II PHB");

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.True(i.OneOf));
        Assert.Contains(items, i => i.Name == "Zeus Mk II CL PHB Flight Blade");
    }

    [Fact]
    public void Quoted_names_and_liveries_match_as_written()
    {
        Assert.Equal("behr_smg_ballistic_01_iasi", Assert.Single(Match("P8-SC \"Echo\" SMG")).Class);
        Assert.Equal("Paint", Assert.Single(Match("Zeus Keelback Livery")).Kind);
    }

    /// <summary>
    /// A piece the catalogue does not have is kept as text, so the page can
    /// still list it - and a loose match on letters inside words, which once
    /// turned "CA OP 1" into a Pyro datapad, is not made.
    /// </summary>
    [Fact]
    public void A_piece_nothing_matches_is_kept_as_text_and_partial_words_do_not_match()
    {
        var item = Assert.Single(Match("Helios Power Supply"));
        Assert.Null(item.Class);
        Assert.Equal("Helios Power Supply", item.Name);

        Assert.Null(Assert.Single(Match("CA OP")).Class);
    }

    [Fact]
    public void An_empty_line_names_nothing()
    {
        Assert.Empty(Match(""));
    }
}
