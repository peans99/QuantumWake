using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The mining HUD's scan-results panel, read.
/// </summary>
/// <remarks>
/// <para>
/// Two layouts. The first fixture is the engine's own output on the Star
/// Citizen Wiki's <c>Mining-4.7-scan-result.png</c> - a 227 × 310 crop, read
/// 2026-09-15 - kept as returned, misreadings included: the mass label came
/// back as "NASS:" with its figure dropped, 5.96% as "s.gs%", 40.47% as
/// "4147%", "ALUMINUM" once as "ÄLUMNUX" and "INERT" as "WERT".
/// </para>
/// <para>
/// The rest are this install's own frames, in <see cref="MiningFrames"/>: the
/// live panel says RESULTS, RES:, INST: and COMP., and prints each row's share
/// on the name's line. The first reader asked for the wiki's words and filed
/// every one of these as nothing.
/// </para>
/// <para>
/// What is defended is that the frame is filed as a scan, that every figure the
/// engine did return lands in its field, and that a figure it did not return is
/// null and not a guess.
/// </para>
/// </remarks>
public class ScreenMiningTests
{
    /// <summary>Mining-4.7-scan-result.png, as the engine read it at 227 × 310.</summary>
    private static readonly ScreenTextLine[] ScanResults =
    [
        new("SCAN RESULTS", 18, 25, 16),
        new("ALUMINUM (ORE)", 13, 60, 11),
        new("NASS:", 14, 84, 14),
        new("RESISTANCE:", 14, 106, 14),
        new("INSTABILITY: 1.75", 14, 127, 13),
        new("COMPOSITION", 13, 189, 14),
        new("21.07 scu", 147, 188, 11),
        new("s.gs%", 13, 232, 10),
        new("3.85%", 13, 249, 11),
        new("49.70%", 13, 267, 10),
        new("4147%", 12, 284, 11),
        new("CORUNDUM (RAW)", 69, 232, 10),
        new("ÄLUMNUX (ORE)", 68, 246, 12),
        new("ALUMINUM (ORE)", 68, 267, 10),
        new("WERT MATERIALS", 68, 284, 9),
        new("4øø", 183, 232, 11),
        new("789", 183, 250, 10),
        new("367", 183, 268, 10),
    ];

    /// <summary>The same panel with the figures the engine dropped at that size, as the game prints them.</summary>
    private static readonly ScreenTextLine[] ScanResultsFull =
    [
        new("SCAN RESULTS", 18, 25, 16),
        new("ALUMINUM (ORE)", 13, 60, 11),
        new("MASS:", 14, 84, 14),
        new("6295", 120, 84, 14),
        new("RESISTANCE:", 14, 106, 14),
        new("0%", 120, 106, 14),
        new("INSTABILITY:", 14, 127, 13),
        new("1.75", 120, 127, 13),
        new("EASY", 90, 155, 12),
        new("COMPOSITION", 13, 189, 14),
        new("21.07 SCU", 147, 188, 11),
        new("5.96%", 13, 232, 10),
        new("CORUNDUM (RAW)", 69, 232, 10),
        new("400", 183, 232, 11),
        new("3.85%", 13, 249, 11),
        new("ALUMINUM (ORE)", 68, 249, 12),
        new("789", 183, 250, 10),
        new("49.70%", 13, 267, 10),
        new("ALUMINUM (ORE)", 68, 267, 10),
        new("367", 183, 268, 10),
        new("40.47%", 12, 284, 11),
        new("INERT MATERIALS", 68, 284, 9),
        new("0", 183, 284, 10),
    ];

    private static readonly string[] Commodities = ["Aluminum (Ore)", "Corundum (Raw)", "Quantainium (Raw)", "Gold (Ore)", "Aluminum"];

    [Fact]
    public void The_panel_is_filed_as_a_scan_and_every_figure_the_engine_returned_lands()
    {
        var frame = ScreenFrames.Read(ScanResults, [], [], Commodities);

        Assert.Equal(ScreenKind.Mining, frame.Kind);
        var scan = frame.Mining!;

        Assert.Equal("ALUMINUM (ORE)", scan.PrimaryRead);
        Assert.Equal("Aluminum (Ore)", scan.Primary);
        Assert.Null(scan.MassKg);                 // the engine dropped the figure; nothing is invented
        Assert.Null(scan.ResistancePercent);      // "RESISTANCE:" came back with no figure
        Assert.Equal(1.75, scan.Instability);
        Assert.Equal(21.07, scan.Scu);
        Assert.Null(scan.Difficulty);

        Assert.Equal(4, scan.Parts.Count);
        var corundum = scan.Parts[0];
        Assert.Equal("Corundum (Raw)", corundum.Mineral);
        Assert.Equal(0, corundum.Percent);          // "s.gs%" is no share
        Assert.Equal(400, corundum.Quality);        // "4øø": the HUD draws its zeros slashed, and the full frame says 400

        var mangled = scan.Parts[1];
        Assert.Equal("ÄLUMNUX (ORE)", mangled.Read);
        Assert.Null(mangled.Mineral);               // three letters off is a different word; kept as read, named by nobody
        Assert.Equal(3.85, mangled.Percent);
        Assert.Equal(789, mangled.Quality);

        Assert.Equal(49.70, scan.Parts[2].Percent);
        Assert.Equal(367, scan.Parts[2].Quality);

        var inert = scan.Parts[3];
        Assert.Equal("Inert materials", inert.Mineral);
        Assert.Equal(0, inert.Percent);             // "4147%" is over a hundred and is no share
    }

    [Fact]
    public void At_full_size_every_field_reads_and_the_summary_says_the_rock()
    {
        var frame = ScreenFrames.Read(ScanResultsFull, [], [], Commodities);

        Assert.Equal(ScreenKind.Mining, frame.Kind);
        var scan = frame.Mining!;
        Assert.Equal(6295, scan.MassKg);
        Assert.Equal(0, scan.ResistancePercent);
        Assert.Equal(1.75, scan.Instability);
        Assert.Equal(21.07, scan.Scu);
        Assert.Equal("EASY", scan.Difficulty);
        Assert.Equal([5.96, 3.85, 49.70, 40.47], scan.Parts.Select(p => p.Percent));
        Assert.Equal([400, 789, 367, 0], scan.Parts.Select(p => p.Quality));
    }

    /// <summary>The fracture HUD prints resistance and instability too; without the title it is not the scan.</summary>
    [Fact]
    public void Resistance_and_instability_alone_are_not_a_scan()
    {
        ScreenTextLine[] fracture =
        [
            new("0.71  0.71  Instability", 90, 406, 18),
            new("0.09  0.09  Resistance", 90, 450, 15),
            new("Laser Throttle", 99, 539, 16),
            new("Fracture Mode", 634, 663, 20),
        ];

        Assert.NotEqual(ScreenKind.Mining, ScreenFrames.Read(fracture, [], [], Commodities).Kind);
    }

    // ---- this install's frames ----

    /// <summary>What the install's table calls the minerals on these frames - raw and refined, as it lists them.</summary>
    private static readonly string[] Table =
    [
        "Raw Silicon", "Silicon", "Raw Hephaestanite", "Hephaestanite", "Aphorite",
        "Aslarite (Raw)", "Aslarite", "Agricium (Ore)", "Agricium", "Aluminum (Ore)",
    ];

    /// <summary>A patch reader that answers from the measured looks, by size.</summary>
    private static WalletSecondLook.PatchReader Looks(params (double Scale, ScreenTextLine[] Lines)[] looks) =>
        (patch, treatment, token) => Task.FromResult<IReadOnlyList<ScreenTextLine>>(
            looks.FirstOrDefault(l => l.Scale == treatment.Scale).Lines ?? []);

    private static readonly WalletSecondLook.PatchReader SiliconLooks = Looks(
        (2, MiningFrames.F_16_15_38_0C4_X2), (2.5, MiningFrames.F_16_15_38_0C4_X2_5),
        (3, MiningFrames.F_16_15_38_0C4_X3), (1.5, MiningFrames.F_16_15_38_0C4_X1_5));

    private static readonly WalletSecondLook.PatchReader AphoriteLooks = Looks(
        (2, MiningFrames.F_16_02_49_333_X2), (2.5, MiningFrames.F_16_02_49_333_X2_5),
        (3, MiningFrames.F_16_02_49_333_X3), (1.5, MiningFrames.F_16_02_49_333_X1_5));

    /// <summary>
    /// The live panel's words, not the wiki's, and the fracture HUD's
    /// "Instabil:ity" and "Resistance (-16%)" on the same frame left alone -
    /// they are the Surge module's, in another column.
    /// </summary>
    [Fact]
    public void The_live_panel_is_a_scan_by_its_own_words()
    {
        var frame = ScreenFrames.Read(MiningFrames.F_16_15_38_0C4_Whole, [], [], Table);

        Assert.Equal(ScreenKind.Mining, frame.Kind);
        Assert.Equal(16.74, frame.Mining!.Scu);

        // The whole read made "21 .eø" of 21.89. Taking its first digits would
        // have said 21; a figure that is not whole on its line is no figure.
        Assert.Null(frame.Mining.Instability);
        Assert.Null(frame.Mining.MassKg);
    }

    [Fact]
    public async Task The_second_look_settles_what_the_whole_read_lost()
    {
        var scan = ScreenFrames.Read(MiningFrames.F_16_15_38_0C4_Whole, [], [], Table,
            await MiningSecondLook.SettleAsync(MiningFrames.F_16_15_38_0C4_Whole, SiliconLooks, Table)).Mining!;

        Assert.Equal("Raw Silicon", scan.Primary);
        Assert.Equal(4294, scan.MassKg);
        Assert.Equal(21.89, scan.Instability);
        Assert.Equal(16.74, scan.Scu);

        // "0%" did not read at any size, and is not supplied.
        Assert.Null(scan.ResistancePercent);

        Assert.Equal([8.56, 42.13, 2.67, 2.71, 43.91], scan.Parts.Select(p => p.Percent));
        Assert.Equal(["Raw Silicon", "Raw Silicon", "Raw Hephaestanite", "Raw Hephaestanite", "Inert materials"],
            scan.Parts.Select(p => p.Mineral));

        // 572 and 692 read at three sizes and four; x3 made 72 of the first,
        // which agreement outvotes. The silicon rows' 510 and 310 read at none.
        Assert.Equal([null, null, 572, 692, null], scan.Parts.Select(p => p.Quality));
        Assert.True(scan.SharesAddUp);
    }

    /// <summary>
    /// A gem cluster prints its SCU in thousandths, and two looks agreeing on a
    /// share is not proof: both took 23.74% for 3.74%. The shares are the
    /// panel's checksum and say so.
    /// </summary>
    [Fact]
    public async Task A_gem_scan_reads_in_thousandths_and_its_misread_share_shows_in_the_sum()
    {
        var scan = (await MiningSecondLook.SettleAsync(MiningFrames.F_16_02_49_333_Whole, AphoriteLooks, Table))!;

        Assert.Equal("Aphorite", scan.Primary);
        Assert.Equal(0.12, scan.MassKg);
        Assert.Equal(0.00315, scan.Scu);
        Assert.Equal(76.25, scan.Parts[0].Percent);
        Assert.Equal(348, scan.Parts[0].Quality);

        Assert.Equal(79.99, scan.ShareTotal);
        Assert.False(scan.SharesAddUp);
    }

    /// <summary>After the rock breaks the panel keeps its title, empties, and loses INST:. There is nothing to read.</summary>
    [Fact]
    public void The_panel_left_after_a_break_is_not_a_scan()
    {
        Assert.NotEqual(ScreenKind.Mining, ScreenFrames.Read(MiningFrames.F_16_17_02_66C_Whole, [], [], Table).Kind);
        Assert.Null(ScreenFrames.ScanPanel(MiningFrames.F_16_17_02_66C_Whole));
    }

    /// <summary>The fracture HUD with the Pitman's figures and no scan panel.</summary>
    [Fact]
    public void The_fracture_hud_is_not_a_scan_and_gets_no_second_look()
    {
        Assert.NotEqual(ScreenKind.Mining, ScreenFrames.Read(MiningFrames.F_16_01_49_88E_Whole, [], [], Table).Kind);
        Assert.Null(ScreenFrames.ScanPanel(MiningFrames.F_16_01_49_88E_Whole));
    }

    [Fact]
    public void The_second_look_points_at_the_panel_and_reaches_past_the_whole_reads_offset()
    {
        var panel = ScreenFrames.ScanPanel(MiningFrames.F_16_15_38_0C4_Whole)!;

        // RESULTS read at y=512 whole and 545 in every patch - the whole read
        // sits 33 px high. The patch has to hold the panel either way: title
        // to the last row, 545 to 893, and the quality column at x=2906.
        Assert.True(panel.Top <= 512);
        Assert.True(panel.Top + panel.Height >= 893 + 33);
        Assert.True(panel.Left <= 2641 && panel.Left + panel.Width >= 2906 + 40);
    }

    [Fact]
    public async Task A_second_look_that_throws_leaves_the_whole_read_standing()
    {
        WalletSecondLook.PatchReader broken = (_, _, _) => throw new InvalidOperationException("engine gone");

        var settled = await MiningSecondLook.SettleAsync(MiningFrames.F_16_15_38_0C4_Whole, broken, Table);
        Assert.Null(settled);

        var frame = ScreenFrames.Read(MiningFrames.F_16_15_38_0C4_Whole, [], [], Table, settled);
        Assert.Equal(ScreenKind.Mining, frame.Kind);
        Assert.Equal(16.74, frame.Mining!.Scu);
    }

    /// <summary>
    /// The HUD's names are not the table's: "HEPH (RAW)" is Raw Hephaestanite,
    /// and the raw marker chooses Raw Silicon over the refined Silicon.
    /// </summary>
    [Theory]
    [InlineData("HEPH (RAW)", "Raw Hephaestanite")]
    [InlineData("SILICON (RAW)", "Raw Silicon")]
    [InlineData("SILICON CRAW)", "Raw Silicon")]
    [InlineData("SILICON", "Silicon")]
    [InlineData("AGRICIUM (ORE)", "Agricium (Ore)")]
    [InlineData("ASI-ARITE (RAW)", "Aslarite (Raw)")]
    [InlineData("SIRON (RAW)", null)]
    [InlineData("HEN (RAW)", null)]
    public void Minerals_are_named_across_the_huds_spelling_and_the_tables(string read, string? named)
    {
        Assert.Equal(named, ScreenFrames.NameMineral(read, Table));
    }
}
