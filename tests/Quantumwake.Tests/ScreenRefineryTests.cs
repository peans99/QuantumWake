using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The refinery terminal, read: the four frames taken at MIC-L5 on 2026-10-03,
/// one of each state.
/// </summary>
/// <remarks>
/// Every figure asserted here is on the screenshot. Where the engine missed one
/// - the 15 cSCU of agricium on the quote, the silicon's quality while
/// processing - the assertion is that it is null, because the alternative a
/// reader is tempted towards is filling it from the frame beside it.
/// </remarks>
public class ScreenRefineryTests
{
    private static readonly string[] Table =
    [
        "Raw Silicon", "Silicon", "Aslarite (Raw)", "Aslarite", "Agricium (Ore)", "Agricium",
    ];

    private static ScreenFrame Read(ScreenTextLine[] lines) => ScreenFrames.Read(lines, [], [], Table);

    [Fact]
    public void The_station_profile_is_a_refinery_with_its_load_and_the_hold()
    {
        var frame = Read(MiningFrames.F_23_08_03_23B_Whole);

        Assert.Equal(ScreenKind.Refinery, frame.Kind);
        var r = frame.Refinery!;
        Assert.Equal("MIC-L5 Modern Icarus Station", r.Station);
        Assert.Equal("profile", r.Stage);
        Assert.Equal(5339, r.CapacityPercent);
        Assert.Equal("Drake Golem", r.Source);
        Assert.Equal(682, r.Refinable);
        Assert.Equal(151, r.Inert);
        Assert.Empty(r.Lots);

        Assert.Equal(867_190, frame.Wallet!.Balance);
    }

    /// <summary>Every lot's quality and quantity, right on all six rows - "SIO" included, which is 510.</summary>
    [Fact]
    public void Setting_up_an_order_lists_every_lot()
    {
        var frame = Read(MiningFrames.F_23_08_18_EFC_Whole);
        var r = frame.Refinery!;

        Assert.Equal("setup", r.Stage);
        Assert.Null(r.Method);          // "Select an option"
        Assert.Null(r.Ratings);         // the line it prints before a method is picked describes nothing
        Assert.Equal(682, r.InManifest);

        Assert.Equal(["Raw Silicon", "Raw Silicon", "Raw Silicon", "Aslarite (Raw)", "Agricium (Ore)", "Agricium (Ore)"],
            r.Lots.Select(l => l.Mineral));
        Assert.Equal([310, 510, 672, 575, 346, 588], r.Lots.Select(l => l.Quality));
        Assert.Equal([383, 142, 31, 6, 74, 33], r.Lots.Select(l => l.Quantity));
        Assert.All(r.Lots, l => Assert.Null(l.Yield));

        // The balance read as "867" with its last digits gone: no figure.
        Assert.Null(frame.Wallet);
    }

    [Fact]
    public void The_quote_names_the_method_and_what_each_lot_gives_back()
    {
        var frame = Read(MiningFrames.F_23_10_04_B6A_Whole);
        var r = frame.Refinery!;

        Assert.Equal("setup", r.Stage);
        Assert.Equal("Pyrometric Chromalysis", r.Method);
        Assert.Equal("Pyrometric Chromalvsis", r.MethodRead);
        Assert.Equal("HIGH YIELD // LOW COST // SLOWEST", r.Ratings);
        Assert.Equal(682, r.InManifest);
        Assert.Equal(182, r.ToRefine);

        // 64 of 142 silicon and 2 of 6 aslarite read; agricium's 15 did not.
        Assert.Equal([null, 64, null, 2, null, null], r.Lots.Select(l => l.Yield));
        Assert.Equal(121.00m, r.Cost);
        Assert.Equal(6 * 60 + 35, r.Seconds);
        Assert.Equal(5435, r.CapacityPercent);
        Assert.Equal(867_190, frame.Wallet!.Balance);
    }

    [Fact]
    public void The_running_order_reads_its_countdown_and_progress()
    {
        var frame = Read(MiningFrames.F_23_10_19_74F_Whole);
        var r = frame.Refinery!;

        Assert.Equal("processing", r.Stage);
        Assert.Equal(["Silicon", "Aslarite", "Agricium"], r.Lots.Select(l => l.Mineral));
        Assert.Equal([null, 575, 588], r.Lots.Select(l => l.Quality));    // "58B" is 588
        Assert.Equal([64, 2, 15], r.Lots.Select(l => l.Yield));
        Assert.Equal([63, 3, 15], r.Lots.Select(l => l.ToDo));
        Assert.Equal(1, r.Lots[0].Done);
        Assert.Equal(6 * 60 + 26, r.Seconds);
        Assert.Null(r.Source);          // "Select Material Location"
    }

    /// <summary>The balance on the frame after the quote is the quote's cost lower.</summary>
    [Fact]
    public void Confirming_the_quote_took_exactly_its_cost()
    {
        var quote = Read(MiningFrames.F_23_10_04_B6A_Whole);
        var running = Read(MiningFrames.F_23_10_19_74F_Whole);

        Assert.Equal(quote.Refinery!.Cost, quote.Wallet!.Balance - running.Wallet!.Balance);
    }

    [Fact]
    public void A_scan_frame_is_not_a_refinery()
    {
        Assert.NotEqual(ScreenKind.Refinery, Read(MiningFrames.F_16_15_38_0C4_Whole).Kind);
    }
}
