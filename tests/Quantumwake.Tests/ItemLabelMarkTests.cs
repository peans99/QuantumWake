using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The marks written into item names, beyond the sold/unsold one.
/// </summary>
/// <remarks>
/// The gating is the whole job here. 25,944 of the install's 26,028 items carry
/// a size and a grade because 1/1 is the default, so marking on their presence
/// would mark almost everything and say nothing. The gate is the type: only
/// something fitted to a ship earns a size. Gating on the value too looked
/// safer and quietly dropped every size 1 component.
/// </remarks>
public class ItemLabelMarkTests
{
    private const string Ini =
        "item_Name_cooler_aegs_s02_arctic=Arctic\n"
        + "item_Name_scope_gamma_duo=Gamma Duo\n"
        + "item_Name_armor_heavy_torso=Pembroke Torso\n"
        + "item_Name_behr_rifle_ballistic_01=P4-AR Rifle\n"
        + "item_Name_shield_basilisk_s01=Basilisk";

    private static Dictionary<string, GameItem> Facts() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["cooler_aegs_s02_arctic"] = new("Arctic", "Cooler", "", 2, 3, "Aegis Dynamics"),
        ["shield_basilisk_s01"] = new("Basilisk", "Shield", "", 1, 1, "Gorgon Defender"),
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

    /// <summary>
    /// Size 1 is a real size for a fitted component - 24 of the game's 73
    /// shields are S1 - and gating on the value rather than the type left every
    /// one of them with no mark at all.
    /// </summary>
    [Fact]
    public void A_size_one_component_is_still_a_size_one_component()
    {
        Assert.Contains("Basilisk [S1A]", Built().Content);
    }

    /// <summary>
    /// The localisation table keys the Lorica shield as SHLD_BEHR_S02_7MA and
    /// the entity describing it is SHLD_BEHR_S02_7MA_SCItem. 203 items differ
    /// that way, and they were all coming out unmarked.
    /// </summary>
    [Fact]
    public void A_name_keyed_without_the_entity_suffix_still_finds_its_facts()
    {
        var plan = TextOverlay.Build(
            "item_Name_SHLD_BEHR_S02_7MA=7MA 'Lorica'",
            _ => true,
            new Dictionary<string, GameItem>(StringComparer.OrdinalIgnoreCase)
            {
                ["SHLD_BEHR_S02_7MA_SCItem"] = new("7MA 'Lorica'", "Shield", "", 2, 1, "Behring"),
            });

        Assert.Contains("7MA 'Lorica' [S2A]", plan.Content);
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

    /// <summary>
    /// Marking an already-marked table marks it again.
    /// </summary>
    /// <remarks>
    /// This is why <c>Install</c> takes the previous install out before it reads
    /// the table to build on, and not after. Reading first meant a rebuild over
    /// StarStrings took its own last output as the base - the path StarStrings is
    /// recorded at is the live file, which by then carried these marks - and
    /// every name gained a second bracket. Pinned here so the ordering is not
    /// quietly swapped back.
    /// </remarks>
    [Fact]
    public void Marking_an_already_marked_table_doubles_the_marks()
    {
        var once = Built().Content;
        var twice = TextOverlay.Build(once, _ => true, Facts()).Content;

        Assert.Contains("Arctic [S2C]", once);
        Assert.Contains("Arctic [S2C] [S2C]", twice);
    }

    [Fact]
    public void Turning_the_facts_off_leaves_only_the_sold_mark()
    {
        var content = Built(new TextOverlayOptions(Facts: false)).Content;

        Assert.Contains("item_Name_cooler_aegs_s02_arctic=Arctic\n", content);
        Assert.DoesNotContain("[S2C]", content);
    }
}
