using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The marks written into item names, beyond the sold/unsold one.
/// </summary>
/// <remarks>
/// The gating is the whole job here. 25,944 of the install's 26,028 items carry
/// a size and a grade because 1/1 is the default, so marking on their presence
/// would mark almost everything and say nothing. Only a fitted ship component
/// earns a size, and only when it is not that default.
/// </remarks>
public class ItemLabelMarkTests
{
    private const string Ini =
        "item_Name_cooler_aegs_s02_arctic=Arctic\n"
        + "item_Name_scope_gamma_duo=Gamma Duo\n"
        + "item_Name_armor_heavy_torso=Pembroke Torso\n"
        + "item_Name_behr_rifle_ballistic_01=P4-AR Rifle";

    private static Dictionary<string, GameItem> Facts() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["cooler_aegs_s02_arctic"] = new("Arctic", "Cooler", "", 2, 3, "Aegis Dynamics"),
        ["scope_gamma_duo"] = new("Gamma Duo", "WeaponAttachment", "Optics", 1, 1, "Greycat"),
        ["armor_heavy_torso"] = new("Pembroke Torso", "Char_Armor_Torso", "Heavy", 1, 1, "CDS"),
        ["behr_rifle_ballistic_01"] = new("P4-AR Rifle", "WeaponPersonal", "Medium", 2, 1, "Behring"),
    };

    private static TextOverlayPlan Built(TextOverlayOptions? options = null) =>
        TextOverlay.Build(Ini, _ => true, Facts(), options);

    /// <summary>
    /// Grade is an ordinal in the files and a letter to a player. The mapping
    /// was checked against StarStrings, which calls the same AEGS coolers A, B,
    /// C and D where the install numbers them 1 to 4.
    /// </summary>
    [Fact]
    public void A_fitted_component_shows_its_size_and_grade_as_a_letter()
    {
        Assert.Contains("Arctic [S2C]", Built().Content);
    }

    /// <summary>
    /// A holographic sight is not a size 1 component. Marking it as one was the
    /// first thing this got wrong, and it is the reason size is gated on type
    /// rather than on the field being present.
    /// </summary>
    [Fact]
    public void A_scope_is_not_a_part_and_gets_no_size()
    {
        var content = Built().Content;

        Assert.Contains("item_Name_scope_gamma_duo=Gamma Duo\n", content);
        Assert.DoesNotContain("Gamma Duo [S1", content);
    }

    [Fact]
    public void Armour_shows_the_class_the_game_gives_it()
    {
        Assert.Contains("Pembroke Torso [H]", Built().Content);
    }

    /// <summary>
    /// A personal weapon carries a size too, and it does not mean what a size
    /// means on a component, so it is left off.
    /// </summary>
    [Fact]
    public void A_personal_weapon_is_sized_by_its_class_not_its_number()
    {
        var content = Built().Content;

        Assert.Contains("P4-AR Rifle [M]", content);
        Assert.DoesNotContain("P4-AR Rifle [S2", content);
    }

    [Fact]
    public void Turning_the_facts_off_leaves_only_the_sold_mark()
    {
        var content = Built(new TextOverlayOptions(Facts: false)).Content;

        Assert.Contains("item_Name_cooler_aegs_s02_arctic=Arctic\n", content);
        Assert.DoesNotContain("[S2C]", content);
    }
}
