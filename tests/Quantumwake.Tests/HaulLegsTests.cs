using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The selected contract's objectives, read into legs.
/// </summary>
/// <remarks>
/// <para>
/// Three shapes, from three frames. The multi-pickup is this install's own
/// Contracts-app frame of 8 September 2026, as the engine returned it (see
/// <see cref="ScreenAppFixtures.Contracts"/>). The direct haul and the
/// multi-drop are laid out from two published screenshots of the same app
/// - the Star Docs hauling guide and timesaver.gg's 4.10 guide - read by
/// eye on 21 September 2026: the wording and the indents are theirs, the
/// pixel positions are drawn to match the layout, not measured by an
/// engine. What each asserts is the wording and the nesting, which is what
/// those frames actually show.
/// </para>
/// </remarks>
public class HaulLegsTests
{
    private static ScreenFrame Read(ScreenTextLine[] lines) => ScreenFrames.Read(lines, [], []);

    /// <summary>Star Docs, "How do I do hauling missions?": a Rookie direct planetary haul on the Offers tab.</summary>
    /// <remarks>
    /// The Offers tab prints no count, so the reader is given the Accepted
    /// tab's wording to anchor on; the objectives panel is identical.
    /// </remarks>
    private static readonly ScreenTextLine[] Direct =
    [
        new("MARK ALL READ", 195, 119, 18),
        new("ACCEPTED (1/10)", 806, 120, 18),
        new("HISTORY", 953, 120, 18),
        new("Rookie Rank - Direct Planetary Small Cargo Haul", 690, 214, 34),
        new("Reward", 1305, 202, 16),
        new("Ä 37,250", 1665, 202, 18),
        new("Contracted By", 1305, 253, 16),
        new("Covalex Independent Contractors", 1485, 253, 16),
        new("DETAILS", 690, 324, 22),
        new("PRIMARY OBJECTIVES", 1230, 324, 22),
        new("Hello,", 690, 362, 16),
        new("O Collect Agricultural Supplies from Port Tressler.", 1232, 361, 18),
        new("O Deliver 0/13 SCU to NB Int. Spaceport.", 1262, 388, 18),
        new("Need a contractor for a simple cargo haul going from a freight", 690, 394, 16),
        new("elevator at Port Tressler above microTech to a freight elevator at", 690, 411, 16),
        new("ACCEPT OFFER", 1632, 854, 18),
    ];

    /// <summary>timesaver.gg, 4.10 hauling guide: a Rookie solar haul, two sources, two cargos, one destination printed per pair.</summary>
    private static readonly ScreenTextLine[] MultiDrop =
    [
        new("MARK ALL READ", 105, 63, 12),
        new("ACCEPTED (2/10)", 457, 63, 12),
        new("HISTORY", 533, 63, 12),
        new("Rookie Rank - Solar Small Cargo Haul", 367, 125, 20),
        new("Reward", 705, 107, 10),
        new("Ä 42,000", 892, 107, 11),
        new("Contracted By", 705, 135, 10),
        new("Covalex Independent Contractors", 830, 135, 10),
        new("DETAILS", 367, 174, 14),
        new("PRIMARY OBJECTIVES", 668, 174, 14),
        new("O Collect Waste from CRU-L5 Beautiful Glen Station.", 669, 194, 11),
        new("O Deliver 0/2 SCU of Waste to Seraphim Station", 687, 208, 11),
        new("above Crusader.", 697, 219, 11),
        new("O Collect Waste from CRU-L4 Shallow Fields Station.", 669, 237, 11),
        new("O Deliver 0/3 SCU of Waste to Seraphim Station", 687, 251, 11),
        new("above Crusader.", 697, 262, 11),
        new("PICK UP LOCATIONS (ANY ORDER)", 367, 257, 10),
        new("- Freight elevator at Beautiful Glen Station at Crusader's L5", 367, 275, 10),
        new("Lagrange point", 367, 286, 10),
        new("- Freight elevator at Shallow Fields Station at Crusader's L4", 367, 293, 10),
        new("Lagrange point", 367, 304, 10),
        new("O Collect Scrap from CRU-L5 Beautiful Glen Station.", 669, 281, 11),
        new("O Deliver 0/2 SCU of Scrap to Seraphim Station above", 687, 295, 11),
        new("Crusader.", 697, 306, 11),
        new("O Collect Scrap from CRU-L4 Shallow Fields Station.", 669, 324, 11),
        new("O Deliver 0/3 SCU of Scrap to Seraphim Station above", 687, 338, 11),
        new("Crusader.", 697, 349, 11),
        new("Makes no difference what order you grab the stuff, as long as it's", 367, 326, 10),
        new("ACCEPT OFFER", 880, 461, 12),
    ];

    /// <summary>
    /// The Direct haul again, with the letter's own DROP OFF list under it -
    /// which spells out the place the objectives line abbreviates.
    /// </summary>
    /// <remarks>
    /// The two panels do not agree on how to write a place: the objective
    /// says "NB Int. Spaceport", the list says "New Babbage International
    /// Spaceport". Both name one destination, and the plan has to see that.
    /// </remarks>
    private static readonly ScreenTextLine[] DirectWithSpelledOutDrop =
    [
        .. Direct,
        new("DROP OFF LOCATIONS (ANY ORDER)", 690, 440, 16),
        new("- Freight elevator at New Babbage International Spaceport on", 690, 462, 16),
        new("microTech", 690, 479, 16),
    ];

    /// <summary>
    /// The abbreviated objective and the spelled-out drop list are one place,
    /// so the backfill adds nothing.
    /// </summary>
    /// <remarks>
    /// Matching them on folded containment alone said they were different -
    /// NBINTSPACEPORT is not inside NEWBABBAGEINTERNATIONALSPACEPORT - so a
    /// second leg was appended for a destination already on the plan. That
    /// became an extra stop on the run and an extra stop in the flight plan,
    /// and nothing said so: a backfilled leg carries no SCU, so the run's
    /// totals still came out right.
    /// </remarks>
    [Fact]
    public void An_abbreviated_objective_and_the_spelled_out_drop_list_are_one_place()
    {
        var app = Read(DirectWithSpelledOutDrop).Contracts!;

        Assert.Equal("New Babbage International Spaceport", Assert.Single(app.DropSites!).Place);

        var (legs, _) = HaulLegs.From(app);

        var leg = Assert.Single(legs);
        Assert.Equal("Port Tressler", leg.Pickup);
        Assert.Equal("NB Int. Spaceport", leg.Delivery);
        Assert.Equal(13, leg.Scu);
    }

    /// <summary>A drop the objectives panel never reached is still a leg of its own.</summary>
    [Fact]
    public void A_drop_the_objectives_panel_did_not_reach_is_still_backfilled()
    {
        var app = Read([
            .. Direct,
            new("DROP OFF LOCATIONS (ANY ORDER)", 690, 440, 16),
            new("- Freight elevator at New Babbage International Spaceport on", 690, 462, 16),
            new("microTech", 690, 479, 16),
            new("- Freight elevator at Everus Harbor on Hurston", 690, 496, 16),
        ]).Contracts!;

        var (legs, _) = HaulLegs.From(app);

        Assert.Equal(2, legs.Count);
        Assert.Equal(["NB Int. Spaceport", "Everus Harbor"], legs.Select(l => l.Delivery));

        // Backfilled from the list, so it carries no count of its own.
        Assert.Null(legs[1].Scu);
        Assert.Equal("Port Tressler", legs[1].Pickup);
    }

    [Fact]
    public void A_direct_haul_is_one_leg_with_the_cargo_from_the_collect_line()
    {
        var app = Read(Direct).Contracts!;

        Assert.Equal(2, app.Steps!.Count);
        Assert.Equal(0, app.Steps[0].Depth);
        Assert.Equal(1, app.Steps[1].Depth);

        var (legs, deliveries) = HaulLegs.From(app);

        var leg = Assert.Single(legs);
        Assert.Equal("Port Tressler", leg.Pickup);
        Assert.Equal("NB Int. Spaceport", leg.Delivery);
        Assert.Equal("Agricultural Supplies", leg.Commodity);
        Assert.Equal(13, leg.Scu);
        Assert.Equal(0, leg.ScuDone);
        Assert.Empty(deliveries);
    }

    [Fact]
    public void A_multi_pickup_is_one_leg_per_source_to_the_parents_place()
    {
        var app = Read(ScreenAppFixtures.Contracts).Contracts!;

        Assert.Equal(4, app.Steps!.Count);
        Assert.Equal([0, 1, 1, 1], app.Steps.Select(s => s.Depth));

        var (legs, deliveries) = HaulLegs.From(app);

        Assert.Equal(3, legs.Count);
        Assert.All(legs, leg => Assert.Equal("Stanton Gateway", leg.Delivery));
        Assert.All(legs, leg => Assert.Equal("Aluminum", leg.Commodity));
        Assert.Equal(["Fallow Field", "Ashland", "The Golden Riviera"], legs.Select(l => l.Pickup));

        // The share per source is not printed, and the app does not invent one.
        Assert.All(legs, leg => Assert.Null(leg.Scu));

        var total = Assert.Single(deliveries);
        Assert.Equal(("Stanton Gateway", 18, 0), (total.Place, total.Scu, total.ScuDone));
    }

    /// <summary>The body a pickup is on comes from the contract's own text, which is the only place it is printed.</summary>
    [Fact]
    public void The_pickup_list_says_which_body_each_source_is_on()
    {
        var app = Read(ScreenAppFixtures.Contracts).Contracts!;

        Assert.Equal(3, app.PickupSites!.Count);
        Assert.Equal(("Fallow Field", "Pyro IV"), (app.PickupSites[0].Place, app.PickupSites[0].Body));
        Assert.Equal(("Ashland", "Pyro 5a"), (app.PickupSites[1].Place, app.PickupSites[1].Body));

        // "Pyro Ill" is how the engine read III, and it is kept as read: a
        // body name is a hint for grouping, not a claim the map has to honour.
        Assert.Equal(("The Golden Riviera", "Pyro Ill"), (app.PickupSites[2].Place, app.PickupSites[2].Body));

        var (legs, _) = HaulLegs.From(app);
        Assert.Equal(["Pyro IV", "Pyro 5a", "Pyro Ill"], legs.Select(l => l.PickupBody));
    }

    [Fact]
    public void A_multi_drop_pairs_each_source_with_its_own_delivery_and_joins_the_wrapped_lines()
    {
        var app = Read(MultiDrop).Contracts!;

        Assert.Equal(8, app.Steps!.Count);
        Assert.Equal("Deliver 0/2 SCU of Waste to Seraphim Station above Crusader.", app.Objectives[1]);

        var (legs, _) = HaulLegs.From(app);

        Assert.Equal(4, legs.Count);
        Assert.Equal("CRU-L5 Beautiful Glen Station", legs[0].Pickup);
        Assert.Equal("Seraphim Station", legs[0].Delivery);
        Assert.Equal("Crusader", legs[0].DeliveryBody);
        Assert.Equal(("Waste", 2), (legs[0].Commodity, legs[0].Scu));
        Assert.Equal(("Waste", 3), (legs[1].Commodity, legs[1].Scu));
        Assert.Equal(("Scrap", 2), (legs[2].Commodity, legs[2].Scu));
        Assert.Equal(("Scrap", 3), (legs[3].Commodity, legs[3].Scu));
        Assert.Equal(10, legs.Sum(l => l.Scu));
    }

    /// <summary>A line that does not parse is kept as read and filed as "other", never dropped.</summary>
    [Theory]
    [InlineData("Deliver 12/18 SCU of Aluminum to Stanton Gateway.", "deliver", "Aluminum", "Stanton Gateway", 12, 18)]
    [InlineData("Deliver O/13 SCU to NB Int. Spaceport.", "deliver", null, "NB Int. Spaceport", 0, 13)]
    [InlineData("Collect Aluminum from The Golden Riviera.", "collect", "Aluminum", "The Golden Riviera", null, null)]
    [InlineData("Return the cargo to Ruin Station.", "other", null, null, null, null)]
    public void A_step_reads_its_verb_cargo_place_and_count(
        string text, string kind, string? cargo, string? place, int? done, int? total)
    {
        var step = ScreenFrames.Step(text, 0);

        Assert.Equal(kind, step.Kind);
        Assert.Equal(cargo, step.Commodity);
        Assert.Equal(place, step.Place);
        Assert.Equal(done, step.Done);
        Assert.Equal(total, step.Total);
        Assert.Equal(text, step.Text);
    }

    /// <summary>
    /// A pickup line wrapped onto a second line is one site, not one site and
    /// a break; and a Lagrange station's tail is the body, not the name, so
    /// the objective's bare "Beautiful Glen Station" finds it.
    /// </summary>
    [Fact]
    public void A_wrapped_pickup_line_is_still_one_site()
    {
        var app = Read(MultiDrop).Contracts!;

        Assert.Equal(2, app.PickupSites!.Count);
        Assert.Equal(("Beautiful Glen Station", "Crusader"), (app.PickupSites[0].Place, app.PickupSites[0].Body));
        Assert.Equal(("Shallow Fields Station", "Crusader"), (app.PickupSites[1].Place, app.PickupSites[1].Body));
    }

    /// <summary>
    /// The 21 Sep Potassium card: the letter listed two pickups, the
    /// objectives panel read one - the second Collect line was below the
    /// fold. The missing pickup comes from the letter, with its body off the
    /// Pyro phrasing of a Lagrange point, so the card is two pickups and
    /// matches the two-source contract it belongs to.
    /// </summary>
    [Fact]
    public void A_pickup_the_objectives_missed_is_taken_from_the_letter()
    {
        var app = Read(CutOffPickup).Contracts!;

        Assert.Equal(2, app.PickupSites!.Count);
        Assert.Equal(("Patch City", "Pyro Ill"), (app.PickupSites[0].Place, app.PickupSites[0].Body));
        Assert.Equal(("Rat's Nest", "Pyro V"), (app.PickupSites[1].Place, app.PickupSites[1].Body));
        Assert.Equal(2, app.Steps!.Count);

        var (legs, deliveries) = HaulLegs.From(app);

        Assert.Equal(2, legs.Count);
        Assert.Equal(("Patch City", "Pyro Ill", "Ruin Station", "Potassium"), (legs[0].Pickup, legs[0].PickupBody, legs[0].Delivery, legs[0].Commodity));
        Assert.Equal(("Rat's Nest", "Pyro V", "Ruin Station", "Potassium"), (legs[1].Pickup, legs[1].PickupBody, legs[1].Delivery, legs[1].Commodity));
        Assert.All(legs, leg => Assert.Null(leg.Scu));
        Assert.Equal(213, Assert.Single(deliveries).Scu);
    }

    /// <summary>ScreenShot-2026-09-21_21-43-47-C74: the objectives panel ended after one Collect line, the letter above named both pickups.</summary>
    private static readonly ScreenTextLine[] CutOffPickup =
    [
        new("ACCEPTED (4/10)", 533, 63, 12),
        new("Member I Stellar Medium Haul I to Ruin Station", 367, 125, 20),
        new("DETAILS", 367, 174, 14),
        new("PRIMARY OBJECTIVES", 668, 174, 14),
        new("O Deliver 0/213 SCU of Potassium to Ruin Station above", 669, 194, 11),
        new("Pyro VI.", 679, 205, 11),
        new("o Collect Potassium from Patch City.", 687, 222, 11),
        new("PICK UP LOCATIONS (ANY ORDER)", 367, 257, 10),
        new("- Freight elevator at Patch City at the L3 Lagrange of Pyro Ill", 367, 275, 10),
        new("- Freight elevator at Rat's Nest at the LS Lagrange of Pyro V", 367, 293, 10),
        new("I'd recommend taking a few minutes to plan your route before", 367, 326, 10),
    ];

    /// <summary>
    /// ScreenShot-2026-09-21_21-43-46-950: the title wrapped onto a second
    /// line and the reader kept "…from Ruin", which matched no contract; the
    /// objectives panel showed one of three drop-offs, the letter listed
    /// them all. The title joins, and the drops the panel did not reach come
    /// from the letter as legs from the one source.
    /// </summary>
    [Fact]
    public void A_wrapped_title_joins_and_a_multi_drops_missing_destinations_come_from_the_letter()
    {
        var app = Read(WasteMultiDrop).Contracts!;

        Assert.Equal("Member I Stellar Medium Haul I from Ruin Station", app.SelectedTitle);
        Assert.Equal(2, app.Steps!.Count);
        Assert.Equal(3, app.DropSites!.Count);
        Assert.Equal(("Starlight Service Station", "Pyro III"), (app.DropSites[0].Place, app.DropSites[0].Body));
        Assert.Equal(("Gaslight", "Pyro V"), (app.DropSites[1].Place, app.DropSites[1].Body));
        Assert.Equal(("Endgame", "Pyro VI"), (app.DropSites[2].Place, app.DropSites[2].Body));

        var (legs, deliveries) = HaulLegs.From(app);

        Assert.Equal(3, legs.Count);
        Assert.All(legs, leg => Assert.Equal(("Ruin Station", "Waste"), (leg.Pickup, leg.Commodity)));
        Assert.Equal(["Starlight Service Station", "Gaslight", "Endgame"], legs.Select(l => l.Delivery));
        Assert.Equal(["Pyro III", "Pyro V", "Pyro VI"], legs.Select(l => l.DeliveryBody));

        // Only the drop the panel printed has a count; the others are not invented.
        Assert.Equal(61, Assert.Single(deliveries).Scu);
        Assert.All(legs, leg => Assert.Null(leg.Scu));
    }

    private static readonly ScreenTextLine[] WasteMultiDrop =
    [
        new("MARK ALL READ", 105, 63, 12),
        new("ACCEPTED (4/10)", 457, 63, 12),
        new("HISTORY", 533, 63, 12),
        new("Member I Stellar Medium Haul I from Ruin", 460, 118, 26),
        new("Station", 460, 148, 26),
        new("DETAILS", 367, 184, 14),
        new("PRIMARY OBJECTIVES", 668, 184, 14),
        new("O Deliver 0/61 SCU of Waste to Starlight Service Station at", 669, 204, 11),
        new("the Li Lagrange of Pyro III.", 679, 215, 11),
        new("o Collect Waste from Ruin Station.", 687, 232, 11),
        new("DROP OFF LOCATIONS (ANY ORDER)", 367, 257, 10),
        new("- Freight elevator at Starlight Service Station at the LI Lagrange", 367, 275, 10),
        new("of Pyro III", 367, 286, 10),
        new("- Freight elevator at Gaslight at the L2 Lagrange of Pyro V", 367, 293, 10),
        new("- Freight elevator at Endgame at the L3 Lagrange of Pyro VI", 367, 311, 10),
        new("I'd recommend taking a few minutes to plan your route before", 367, 344, 10),
    ];

    /// <summary>
    /// 25 Oct 21:25:59: the engine read the indented diamonds as lines of
    /// their own and every Collect without one. The old reader joined all
    /// three onto the Deliver above, and the run had Megumi Refueling - a
    /// pickup - as a drop-off, twice.
    /// </summary>
    [Fact]
    public void Collect_lines_that_lost_their_diamonds_are_still_pickups_under_the_delivery()
    {
        var app = Read(HaulingFrameFixtures.Potassium2125).Contracts!;

        Assert.Equal(["deliver", "collect", "collect", "collect"], app.Steps!.Select(s => s.Kind));
        Assert.Equal([0, 1, 1, 1], app.Steps!.Select(s => s.Depth));

        var (legs, deliveries) = HaulLegs.From(app);

        Assert.Equal(["Dudley & Daughters", "Megumi Refueling", "Rat's Nest"], legs.Select(l => l.Pickup));
        Assert.All(legs, leg => Assert.Equal("Ruin Station", leg.Delivery));
        Assert.Equal(249, Assert.Single(deliveries).Scu);
    }

    /// <summary>
    /// 25 Oct 21:26:12: a Waste multi-drop whose Deliver parents lost their
    /// diamonds, so two of its three drops were read as nothing. The letter's
    /// list ends in "Destination2Address", a drop the game has no name for.
    /// </summary>
    [Fact]
    public void A_multi_drop_whose_diamonds_were_read_apart_keeps_every_drop()
    {
        var app = Read(HaulingFrameFixtures.Waste2126).Contracts!;

        Assert.Equal(["deliver", "collect", "deliver", "collect", "deliver", "collect"], app.Steps!.Select(s => s.Kind));
        Assert.Equal([0, 1, 0, 1, 0, 1], app.Steps!.Select(s => s.Depth));

        var (legs, deliveries) = HaulLegs.From(app);
        var named = legs.Where(l => l.Unnamed is null).ToList();

        // The count is printed on each Deliver parent, so it rides on the delivery.
        Assert.Equal([72, 65, 47], deliveries.Select(d => d.Scu));
        Assert.Equal(["Megumi Refueling", "Rat's Nest", "Megumi Refueling"], named.Select(l => l.Delivery));
        Assert.All(legs, leg => Assert.Equal("Ruin Station", leg.Pickup));
        Assert.Equal("Destination2Address", Assert.Single(legs, l => l.Unnamed is not null).Unnamed);
    }

    /// <summary>
    /// 25 Oct 21:26:44: photographed while the game was still typing the
    /// objectives in - "Deliver 0/189 SCU of Car", "Collect Carbon fro" -
    /// next to a letter printed whole, which lists two stations and a
    /// "Location2Address". The pickups come from the letter, the destination
    /// from the title, and the count from the part-written Deliver.
    /// </summary>
    [Fact]
    public void A_card_photographed_before_its_objectives_finished_is_read_from_the_letter_and_title()
    {
        var app = Read(HaulingFrameFixtures.Carbon2126).Contracts!;

        var (legs, deliveries) = HaulLegs.From(app, new Quantumwake.Core.State.HaulRoute(null, "Ruin Station"));

        Assert.Equal(["Dudley & Daughters", "Rod's Fuel 'N Supplies", null], legs.Select(l => l.Pickup));
        Assert.All(legs, leg => Assert.Equal("Ruin Station", leg.Delivery));
        Assert.Equal("Location2Address", legs[2].Unnamed);
        Assert.Equal(189, Assert.Single(deliveries).Scu);
    }

    /// <summary>A part-written objective is filed whole only when it reached its full stop.</summary>
    [Theory]
    [InlineData("Collect Carbon from Gaslight.", true)]
    [InlineData("Collect Carbon from Dudley e D", false)]
    [InlineData("Deliver 0/189 SCU of Car", false)]
    public void An_objective_is_whole_once_it_reaches_its_full_stop(string text, bool whole) =>
        Assert.Equal(whole, ScreenFrames.Whole(ScreenFrames.Step(text, 0)));
}
