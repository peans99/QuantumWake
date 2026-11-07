using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The hangar's Freight Manager, read from a 4.10 video's frames: the list
/// with its two panes, and the confirmation that writes each contract's crate
/// sizes and commodity out.
/// </summary>
public class FreightFrameTests
{
    private static readonly string[] Commodities = ["Stims", "Silicon", "Tungsten", "Carbon", "Waste", "Potassium"];

    private static FreightReading Read(ScreenTextLine[] lines)
    {
        var frame = ScreenFrames.Read(lines, [], [], Commodities);
        Assert.Equal(ScreenKind.Freight, frame.Kind);
        return frame.Freight!;
    }

    [Fact]
    public void The_list_names_the_contract_and_the_elevators_load()
    {
        var freight = Read(FreightFrameFixtures.ListOneContract);

        Assert.Equal("list", freight.Stage);
        Assert.Equal(new FreightCapacity(94, 1120), freight.Elevator);
        // "CAPACITY: n Isooo scu" has no figure to take.
        Assert.Null(freight.Warehouse);

        var order = Assert.Single(freight.Orders);
        Assert.Equal("COVALEX INDEPENDENT CONTRACTORS", order.Contractor);
        Assert.Equal("ROOKIE RANK - DIRECT MEDIUM CARGO HAUL", order.Title);
        // The list draws crates as pictures; nothing is made of them.
        Assert.Empty(order.Crates);
    }

    [Fact]
    public void A_misread_capacity_is_folded_back_to_its_figure()
    {
        var freight = Read(FreightFrameFixtures.ListTwoContracts);

        Assert.Equal(new FreightCapacity(0, 896), freight.Elevator);
        Assert.Equal(new FreightCapacity(0, 5000), freight.Warehouse); // "O/SOOO SCu"
        Assert.Equal(["ROOKIE RANK - DIRECT SMALL CARGO HAUL", "JUNIOR RANK - DIRECT MEDIUM CARGO HAUL"],
            freight.Orders.Select(o => o.Title));
    }

    [Fact]
    public void The_confirmation_writes_out_each_crate_size_and_its_commodity()
    {
        var freight = Read(FreightFrameFixtures.ConfirmOneContract);

        Assert.Equal("confirm", freight.Stage);
        var order = Assert.Single(freight.Orders);
        Assert.Equal("ROOKIE RANK - DIRECT MEDIUM CARGO HAUL", order.Title);
        // "Time Remaining: 01:sa" - no clock rather than a wrong one.
        Assert.Null(order.SecondsLeft);
        // Every 8 read as "B".
        Assert.Equal([8, 4, 2], order.Crates.Select(c => c.SizeScu));
        Assert.Equal("Stims", order.Crates[0].Commodity);
        Assert.Equal("Stims", order.Crates[2].Commodity);
    }

    [Fact]
    public void Two_contracts_on_one_confirmation_keep_their_own_crates_and_clocks()
    {
        var freight = Read(FreightFrameFixtures.ConfirmTwoContracts);

        Assert.Equal(2, freight.Orders.Count);
        var (small, medium) = (freight.Orders[0], freight.Orders[1]);

        Assert.Equal("ROOKIE RANK - DIRECT EXTRA SMALL CARGO HAUL", small.Title); // read with "•" for the dash
        Assert.Equal(7 * 60 + 17, small.SecondsLeft);
        var tungsten = Assert.Single(small.Crates);
        Assert.Equal(1, tungsten.SizeScu);
        // "TLJNGSTEN" is kept as read and names nothing.
        Assert.Equal("TLJNGSTEN", tungsten.CommodityRead);
        Assert.Null(tungsten.Commodity);

        Assert.Equal("JUNIOR RANK - DIRECT MEDIUM CARGO HAUL", medium.Title);
        Assert.Equal(12 * 60 + 17, medium.SecondsLeft);
        // The 1 SCU tile's label read "scu" alone: without the close read it is not a tile.
        Assert.Equal([8, 2], medium.Crates.Select(c => c.SizeScu));
        Assert.Equal("Silicon", medium.Crates[0].Commodity);
        // The 2 SCU tile's word is under the pilot's cursor on the video; the
        // SILICON beside it is the next tile's, and is not borrowed.
        Assert.Null(medium.Crates[1].Commodity);
    }

    /// <summary>
    /// This install's own terminal, 24 Oct at Ruin Station: Red Wind titles
    /// lose their rep ladder however it read, and "00:00" is a haul with no
    /// timer, not one out of time.
    /// </summary>
    [Fact]
    public void Red_wind_titles_lose_their_ladder_and_a_zero_clock_is_no_clock()
    {
        var carbon = Read(FreightFrameFixtures.OwnCarbon);
        Assert.Equal(["MEMBER 1 STELLAR MEDIUM HAUL 1 TO RUIN STATION", "MEMBER 1 STELLAR MEDIUM HAUL 1 TO RUIN STATION"],
            carbon.Orders.Select(o => o.Title));
        Assert.All(carbon.Orders, o => Assert.Null(o.SecondsLeft));
        Assert.Equal([8], carbon.Orders[0].Crates.Select(c => c.SizeScu));
        Assert.Equal([8, 4, 1], carbon.Orders[1].Crates.Select(c => c.SizeScu));
        // The next contract's title is not the first tile's commodity.
        Assert.Null(carbon.Orders[0].Crates[0].CommodityRead);
        // Still the contract the planner knows: "|" read as "1" is folded there.
        Assert.True(ScreenFrames.SameContract(carbon.Orders[0].Title, "Member | Stellar Medium Haul | to Ruin Station"));

        var waste = Read(FreightFrameFixtures.OwnWaste);
        Assert.Equal("MEMBER 1 STELLAR MEDIUM HAUL 1 FROM RUIN STATION", waste.Orders[1].Title); // "tsan00/2001250…" cut
        Assert.Equal([8, 2], waste.Orders[0].Crates.Select(c => c.SizeScu)); // the 1 SCU tile read "scu"
        Assert.All(waste.Orders[0].Crates, c => Assert.Equal("Waste", c.Commodity));
        Assert.Equal([2], waste.Orders[1].Crates.Select(c => c.SizeScu)); // its 8 read "e scu"
    }

    /// <summary>
    /// The close read, standing in for ONNX with what it returned on the
    /// video: the lost tile's size, and Tungsten spelled right. The size the
    /// close read finds again on a tile that already had one is not a second
    /// tile.
    /// </summary>
    [Fact]
    public async Task The_close_read_gives_back_a_lost_size_and_the_commodity_spelled_right()
    {
        var lines = FreightFrameFixtures.ConfirmTwoContracts;

        Task<IReadOnlyList<ScreenTextLine>> Close(ScreenPatch patch, ScreenTreatment treatment, CancellationToken token)
        {
            bool Holds(double x, double y) => x >= patch.Left && x <= patch.Left + patch.Width && y >= patch.Top && y <= patch.Top + patch.Height;
            IReadOnlyList<ScreenTextLine> seen =
                Holds(1180, 833) ? [new("1 SCU", 1176, 852, 17), new("1/1", 1190, 874, 13), new("SILICON", 1170, 915, 15)]
                : Holds(895, 608) ? [new("1SCU", 890, 628, 17), new("10/10", 899, 649, 14), new("TUNGSTEN", 884, 690, 14)]
                : [new("8 SCU", patch.Left + 40, patch.Top + 20, 19), new("1/1", patch.Left + 55, patch.Top + 40, 13)];
            return Task.FromResult(seen);
        }

        var looked = await FreightSecondLook.TakeAsync(lines, Close);
        var freight = Read([.. looked]);

        Assert.Equal("Tungsten", Assert.Single(freight.Orders[0].Crates).Commodity);
        Assert.Equal([8, 2, 1], freight.Orders[1].Crates.Select(c => c.SizeScu));
        Assert.Equal("Silicon", freight.Orders[1].Crates[2].Commodity);
    }

    [Fact]
    public async Task The_close_read_leaves_any_other_screen_alone()
    {
        var looked = await FreightSecondLook.TakeAsync(FreightFrameFixtures.ListOneContract,
            (_, _, _) => throw new InvalidOperationException("not asked"));
        Assert.Same(FreightFrameFixtures.ListOneContract, looked);
    }

    [Theory]
    [InlineData("8 SCU", 8)]
    [InlineData("B SCU", 8)]
    [InlineData("z scu", 2)]
    [InlineData("32 SCU", 32)]
    [InlineData("3 SCU", null)]  // no crate is made in 3
    [InlineData("scu", null)]
    [InlineData("88 SCU", null)]
    public void Only_the_sizes_crates_come_in_are_taken(string read, int? size) =>
        Assert.Equal(size, ScreenFrames.CrateSize(read));
}
