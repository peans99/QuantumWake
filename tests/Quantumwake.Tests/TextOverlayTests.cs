using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The generated text overlay. It rewrites the file the game reads at startup,
/// so the bar is that nothing changes except the lines meant to.
/// </summary>
public class TextOverlayTests
{
    private const string Ini =
        "item_Name_behr_rifle_ballistic_01=P4-AR Rifle\n"
        + "item_Name_gmni_lmg_ballistic_01=F55 LMG\n"
        + "item_Name_crlf_consumable_healing_01=MedPen\n"
        + "vehicle_NameANVL_Hornet=Anvil Hornet\n"
        + "items_commodities_Agricium=Agricium";

    /// <summary>Nothing is sold, so every shoppable name is marked.</summary>
    private static TextOverlayPlan NothingSold() => TextOverlay.Build(Ini, _ => false);

    [Fact]
    public void It_marks_gear_nothing_is_known_to_sell()
    {
        var plan = NothingSold();

        Assert.Contains("P4-AR Rifle [*]", plan.Content);
        Assert.Contains("F55 LMG [*]", plan.Content);
        Assert.Equal(3, plan.Marked);
    }

    /// <summary>
    /// A receipt or a market listing is enough to leave a name alone. This is the
    /// case that matters: the P4-AR is one of the items UEX does not list and
    /// these logs prove was bought.
    /// </summary>
    [Fact]
    public void An_item_something_sells_is_left_exactly_as_it_was()
    {
        var plan = TextOverlay.Build(Ini, c => c.StartsWith("behr", StringComparison.Ordinal));

        Assert.Contains("item_Name_behr_rifle_ballistic_01=P4-AR Rifle\n", plan.Content);
        Assert.DoesNotContain("P4-AR Rifle [*]", plan.Content);
        Assert.Equal(1, plan.Sold);
    }

    /// <summary>
    /// Only the item lines may move. A vehicle or a commodity gaining a mark
    /// would be a mark in a place the reader was never told about.
    /// </summary>
    [Fact]
    public void Nothing_but_item_names_is_touched()
    {
        var plan = NothingSold();

        Assert.Contains("vehicle_NameANVL_Hornet=Anvil Hornet", plan.Content);
        Assert.Contains("items_commodities_Agricium=Agricium", plan.Content);
        Assert.DoesNotContain("Anvil Hornet [*]", plan.Content);
        Assert.DoesNotContain("Agricium [*]", plan.Content);
    }

    /// <summary>
    /// Line count and the final separator have to survive, or the file cannot be
    /// diffed against the game's own with any confidence.
    /// </summary>
    [Fact]
    public void The_shape_of_the_file_is_preserved()
    {
        var plan = NothingSold();

        Assert.Equal(Ini.Split('\n').Length, plan.Content.Split('\n').Length);
        Assert.False(plan.Content.EndsWith('\n'));
    }

    [Fact]
    public void A_trailing_newline_is_kept_when_the_source_had_one()
    {
        var plan = TextOverlay.Build(Ini + "\n", _ => false);

        Assert.EndsWith("\n", plan.Content);
    }

    /// <summary>
    /// Ship internals and unclassifiable ids are left out: marking all 9,553
    /// names puts a mark on 79% of them, and a mark on almost everything says
    /// nothing at all.
    /// </summary>
    [Fact]
    public void Gear_a_player_never_shops_for_is_not_considered()
    {
        var plan = TextOverlay.Build("item_Name_AEGS_Idris_Retro_CIV=Retro Thruster", _ => false);

        Assert.Equal(0, plan.Marked);
        Assert.Equal(1, plan.Skipped);
        Assert.DoesNotContain("*", plan.Content);
    }

    /// <summary>The samples are what the page shows before anything is written.</summary>
    [Fact]
    public void It_reports_what_it_would_change_before_writing_anything()
    {
        var plan = NothingSold();

        Assert.Equal(plan.Marked + plan.Sold + plan.Skipped, plan.Considered);
        Assert.Contains(plan.Samples, s => s.Was == "F55 LMG" && s.Becomes == "F55 LMG [*]");
    }
}
