using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Reading a screenshot's text and saying what it is.
/// </summary>
/// <remarks>
/// Every reading below is a real one. The tooltip lines are what
/// <c>Windows.Media.Ocr</c> actually returned for a looting-view screenshot
/// from this install, mangling and all, and the errors being corrected here
/// are the errors that engine was measured making rather than errors that
/// seemed plausible. A fixture written from the design instead of the run
/// would pass while defending nothing.
/// </remarks>
public class ScreenInsightTests
{
    /// <summary>The Arlington tooltip, exactly as the engine returned it.</summary>
    private static readonly string[] Arlington =
    [
        "LOOTING VIEW",
        "Arlington Rifle",
        "Volume: 13000 pscu",
        "Manufacturer. Hedeby Gunworks",
        "Item Type: Rifle",
        "Class: Ballistic",
        "Magazine Size: 20",
        "Rate Of Fire: 320 rpm",
        "Effective Range: SO m",
        "Attachments: Optics (S2). Barrel (S2),",
        "Underbarrel (S3)",
        "Gunsmiths at Hedeby Gunworks looked to classic",
        "ballistic designs to construct a long rifle steeped",
        "accura marksmanship, an easy cycling of",
    ];

    /// <summary>
    /// A catalogue entry shaped like the ones this install actually holds.
    /// </summary>
    /// <remarks>
    /// The defaults are the Arlington's real values, read out of the install:
    /// class <c>hdgw_rifle_ballistic_01</c>, type <c>WeaponPersonal</c>,
    /// sub-type <c>Medium</c>, 13000 micro-SCU. The sub-type matters - it is a
    /// size class and not the ammunition class the tooltip prints, which is
    /// exactly the mismatch this matcher had to be taught about.
    /// </remarks>
    private static ItemReference Item(
        string name,
        string className = "hdgw_rifle_ballistic_01",
        string? maker = "Hedeby Gunworks",
        long microScu = 13000) =>
        new(className, name, "WeaponPersonal", "Medium", 2, 1, maker, null, "install", MicroScu: microScu);

    [Fact]
    public void The_name_is_the_line_above_the_stats_not_the_panel_title()
    {
        var reading = ScreenInsight.Read(Arlington);

        Assert.Equal("Arlington Rifle", reading.Name);
    }

    [Fact]
    public void Every_stat_the_tooltip_carried_is_read()
    {
        var reading = ScreenInsight.Read(Arlington);

        Assert.Equal("13000 pscu", reading.Fields["Volume"]);
        Assert.Equal("Hedeby Gunworks", reading.Fields["Manufacturer"]);
        Assert.Equal("Rifle", reading.Fields["Item Type"]);
        Assert.Equal("Ballistic", reading.Fields["Class"]);
        Assert.Equal("320 rpm", reading.Fields["Rate Of Fire"]);
    }

    /// <summary>
    /// The colon the engine flattened to a full stop still separates a label
    /// from its value - that reading is in the fixture above, unaltered.
    /// </summary>
    [Fact]
    public void A_colon_read_as_a_full_stop_is_still_a_label()
    {
        var reading = ScreenInsight.Read(Arlington);

        Assert.Equal("Hedeby Gunworks", reading.Fields["Manufacturer"]);
    }

    [Fact]
    public void The_lore_paragraph_does_not_become_fields()
    {
        var reading = ScreenInsight.Read(Arlington);

        Assert.Equal(
            ["Attachments", "Class", "Effective Range", "Item Type",
             "Magazine Size", "Manufacturer", "Rate Of Fire", "Volume"],
            reading.Fields.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void A_name_that_read_perfectly_and_a_stat_that_agrees_is_an_answer()
    {
        var result = ScreenInsight.Look(Arlington, [Item("Arlington Rifle")]);

        Assert.True(result.Certain);
        Assert.Null(result.Trouble);
        Assert.Equal(ScreenMatchTier.Exact, result.Candidates[0].Tier);
        Assert.Contains("manufacturer", result.Candidates[0].Agrees);
        Assert.Contains("volume", result.Candidates[0].Agrees);
        Assert.Empty(result.Candidates[0].Disagrees);
    }

    /// <summary>
    /// <c>MSO-423</c> for <c>MSD-423</c> is the misreading that was measured,
    /// and undoing it is the whole reason this layer folds anything at all.
    /// </summary>
    [Fact]
    public void A_D_read_as_an_O_still_finds_the_missile_rack()
    {
        var result = ScreenInsight.Look(
            ["MSO-423 Missile Rack"],
            [Item("MSD-423 Missile Rack", "msd_423_missile_rack", maker: null, microScu: 0)]);

        Assert.Single(result.Candidates);
        Assert.Equal(ScreenMatchTier.Confusable, result.Candidates[0].Tier);
    }

    /// <summary>
    /// The line this must not cross. A scorer loose enough to repair
    /// <c>MSO</c> to <c>MSD</c> by edit distance would read one of these as
    /// the other, and naming the wrong weapon confidently is worse than
    /// naming none.
    /// </summary>
    [Fact]
    public void A_four_is_never_read_as_an_eight()
    {
        var result = ScreenInsight.Look(
            ["P4-AR Rifle"],
            [Item("P8-AR Rifle", "behr_rifle_ballistic_01", maker: null, microScu: 0)]);

        Assert.Empty(result.Candidates);
        Assert.Equal("nothing in the catalogue reads like \"P4-AR Rifle\"", result.Trouble);
    }

    [Fact]
    public void A_stat_that_contradicts_is_shown_rather_than_hidden()
    {
        var result = ScreenInsight.Look(Arlington, [Item("Arlington Rifle", maker: "Klaus & Werner")]);

        Assert.Contains("manufacturer", result.Candidates[0].Disagrees);
        Assert.False(result.Certain);
        Assert.Equal("the name matched but the details do not", result.Trouble);
    }

    [Fact]
    public void A_volume_that_disagrees_counts_against_it()
    {
        var result = ScreenInsight.Look(Arlington, [Item("Arlington Rifle", microScu: 9000)]);

        Assert.Contains("volume", result.Candidates[0].Disagrees);
        Assert.DoesNotContain("volume", result.Candidates[0].Agrees);
    }

    /// <summary>
    /// Two things that read the same are two answers. Picking one would be a
    /// coin toss with a confident face on it.
    /// </summary>
    [Fact]
    public void Things_that_read_alike_are_all_reported()
    {
        var result = ScreenInsight.Look(
            ["MSO-423 Missile Rack"],
            [Item("MSD-423 Missile Rack", "msd_423", maker: null, microScu: 0),
             Item("MS0-423 Missile Rack", "ms0_423", maker: null, microScu: 0)]);

        Assert.Equal(2, result.Candidates.Count);
        Assert.False(result.Certain);
        Assert.Equal("2 things in the catalogue read the same", result.Trouble);
    }

    /// <summary>
    /// A clean candidate outranks a contradicted one even when the
    /// contradicted one matched the name better.
    /// </summary>
    [Fact]
    public void A_contradicted_exact_loses_to_a_clean_one()
    {
        var result = ScreenInsight.Look(
            Arlington,
            [Item("Arlington Rifle", maker: "Klaus & Werner", microScu: 1),
             Item("Arlington Rifle")]);

        Assert.Single(result.Candidates);
        Assert.True(result.Certain);
        Assert.Equal("Hedeby Gunworks", result.Candidates[0].Item.Manufacturer);
    }

    /// <summary>
    /// The tooltip's words for what a thing is are found in the class name,
    /// because the catalogue's own Type and SubType are a different vocabulary
    /// entirely - <c>WeaponPersonal</c> and <c>Medium</c> for a weapon whose
    /// tooltip says <c>Rifle</c> and <c>Ballistic</c>.
    /// </summary>
    [Fact]
    public void What_the_tooltip_calls_it_is_found_in_the_class_name()
    {
        var result = ScreenInsight.Look(Arlington, [Item("Arlington Rifle")]);

        Assert.Contains("item type", result.Candidates[0].Agrees);
        Assert.Contains("class", result.Candidates[0].Agrees);
    }

    /// <summary>
    /// A class name is an id, not a description. Its silence about a word is
    /// no evidence against a candidate the other fields vouch for.
    /// </summary>
    [Fact]
    public void A_class_name_that_says_nothing_is_not_held_against_it()
    {
        var result = ScreenInsight.Look(Arlington, [Item("Arlington Rifle", "hdgw_wpn_01")]);

        Assert.Empty(result.Candidates[0].Disagrees);
        Assert.True(result.Certain);
    }

    /// <summary>
    /// <c>SO m</c> was a real reading of <c>50 m</c>. Repairing it would be
    /// inventing a measurement, so the value stays as read and the number
    /// stays absent.
    /// </summary>
    [Fact]
    public void A_number_that_did_not_read_is_not_guessed()
    {
        var reading = ScreenInsight.Read(Arlington);

        Assert.Equal("SO m", reading.Fields["Effective Range"]);
    }

    /// <summary>
    /// A loadout frame is thirty names and no tooltip, which is the commoner
    /// shape: six of the nine screenshots measured for this were one.
    /// </summary>
    [Fact]
    public void A_frame_of_names_is_swept_rather_than_read_as_a_tooltip()
    {
        string[] frame =
        [
            "Vehicle Loadout Manager",
            "Missile Slot 1",
            "MSD-423 Missile Rack",
            "MSO-423 Missile Rack",
        ];

        Assert.Null(ScreenInsight.Read(frame).Name);

        var swept = ScreenInsight.Sweep(
            frame, [Item("MSD-423 Missile Rack", "MRCK_S04_BEHR_Dual_S03", maker: null, microScu: 0)]);

        Assert.Equal(2, swept.Count);
        Assert.True(swept[0].Named);
        Assert.False(swept[1].Named);
        Assert.Equal(ScreenMatchTier.Confusable, swept[1].Candidates[0].Tier);
    }

    /// <summary>
    /// The catalogue holds entries called "Available" - empty rack slots - so
    /// the loadout screen's own "Available:" header matched three of them
    /// exactly until a line ending in a colon stopped counting as a name.
    /// </summary>
    [Fact]
    public void A_heading_is_not_a_name()
    {
        var swept = ScreenInsight.Sweep(
            ["Available:"],
            [Item("Available", "ugf_cargo_rack_slot_1_empty_a", maker: null, microScu: 0)]);

        Assert.Empty(swept);
    }

    /// <summary>
    /// Half a name is refused outright in a sweep. There is no manufacturer
    /// and no volume on a loadout frame to check a guess against, so the
    /// tolerance that a tooltip can afford would only produce noise here.
    /// </summary>
    [Fact]
    public void A_sweep_refuses_the_partial_matches_a_tooltip_allows()
    {
        var items = new[] { Item("Arlington Rifle", maker: null, microScu: 0) };

        Assert.Empty(ScreenInsight.Sweep(["Arlington Ri"], items));
        Assert.Single(ScreenInsight.Look(["Arlington Ri"], items).Candidates);
    }

    [Fact]
    public void A_frame_with_no_tooltip_on_it_says_so()
    {
        var result = ScreenInsight.Look([], []);

        Assert.Null(result.Reading.Name);
        Assert.Equal("nothing on this frame looked like an item tooltip", result.Trouble);
    }

    /// <summary>
    /// Half a name is a lead, not an answer: it comes back as a candidate and
    /// never as a certainty on its own.
    /// </summary>
    [Fact]
    public void Half_a_name_is_never_certain_by_itself()
    {
        var result = ScreenInsight.Look(
            ["Arlington Ri"],
            [Item("Arlington Rifle", maker: null, microScu: 0)]);

        Assert.Single(result.Candidates);
        Assert.Equal(ScreenMatchTier.Partial, result.Candidates[0].Tier);
        Assert.False(result.Certain);
    }

    [Fact]
    public void A_short_word_does_not_match_everything_containing_it()
    {
        var result = ScreenInsight.Look(
            ["Rifle"],
            [Item("Arlington Rifle", maker: null, microScu: 0)]);

        Assert.Empty(result.Candidates);
    }
}
