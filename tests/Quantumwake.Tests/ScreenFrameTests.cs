using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Sorting a frame into the screen it is, and reading what that screen says.
/// </summary>
/// <remarks>
/// Run against the real frames in <see cref="ScreenFrameFixtures"/> and a
/// catalogue shaped like the install's. What is defended is the honesty as
/// much as the reading: a misread that folds to two parts names neither, a
/// ship whose maker went wrong is offered and not asserted, and a wallet the
/// engine cannot read says so in words.
/// </remarks>
public class ScreenFrameTests
{
    private static ItemReference Item(string cls, string name, string type, int size, int grade, string maker = "") =>
        new(cls, name, type, "", size, grade, maker.Length > 0 ? maker : null, null, "install");

    /// <summary>The parts on the frames, as the install spells them.</summary>
    private static readonly ItemReference[] Catalogue =
    [
        Item("COOL_JSPN_S01_FrostStar_SCItem", "Frost-Star", "Cooler", 1, 3, "J-Span"),
        Item("COOL_JSPN_S02_FrostStarEX_SCItem", "Frost-Star EX", "Cooler", 2, 3, "J-Span"),
        Item("POWR_JUST_S02_Genoa_SCItem", "Genoa", "PowerPlant", 2, 1, "Juno Starwerk"),
        Item("QDRV_ARCC_S02_Torrent_SCItem", "Torrent", "QuantumDrive", 2, 3, "ArcCorp"),
        Item("Turret_PDC_BEHR_G", "MRX \"Torrent\"", "Turret", 2, 1, "Behring"),
        Item("SHLD_BASL_S03_Parapet_SCItem", "Parapet", "Shield", 3, 1, "Basilisk"),
        Item("RADR_WLOP_S02_Chernykh", "Chernykh", "Radar", 2, 3, "WillsOp"),
        Item("Controller_Flight_DRAK_Corsair", "Drake Corsair Standard Flight Blade", "FlightController", 1, 1, "Drake Interplanetary"),
        Item("MISL_RACK_MSD_423", "MSD-423 Missile Rack", "MissileLauncher", 4, 1, "Talon"),
        Item("MISL_S03_IR_VNCL_Chaos", "'Chaos' III Missile", "Missile", 3, 1, "Vanduul"),
        Item("MISL_S03_CS_FSKI_Arrester", "Arrester III Missile", "Missile", 3, 1, "Aegis Dynamics"),
        Item("MISL_S02_IR_VNCL_Chaos", "'Chaos' II Missile", "Missile", 2, 1, "Vanduul"),
        Item("BEHR_LaserCannon_S3", "M5A Cannon", "WeaponGun", 3, 1, "Behring"),
        Item("BEHR_LaserCannon_S4", "M6A Cannon", "WeaponGun", 4, 1, "Behring"),
        Item("BEHR_LaserCannon_S5", "M7A Cannon", "WeaponGun", 5, 1, "Behring"),
        Item("BEHR_LaserCannon_S6", "M8A Cannon", "WeaponGun", 6, 1, "Behring"),
        Item("Mount_Gimbal_S2", "VariPuck S2 Gimbal Mount", "Turret", 2, 1, "Flashfire Systems"),
        Item("Mount_Gimbal_S4", "VariPuck S4 Gimbal Mount", "Turret", 4, 1, "Flashfire Systems"),
        Item("Mount_Gimbal_S5", "VariPuck S5 Gimbal Mount", "Turret", 5, 1, "Flashfire Systems"),
        Item("Mount_Gimbal_S1", "VariPuck S1 Gimbal Mount", "Turret", 1, 1, "Flashfire Systems"),
        Item("Mount_Gimbal_S1_NoSafety", "VariPuck S1 Gimbal Mount", "Turret", 1, 1, "Flashfire Systems"),
        Item("GATS_BallisticGatling_S2", "Scorpion GT-215 Gatling", "WeaponGun", 2, 1, "Gallenson Tactical Systems"),
        Item("DRAK_Corsair", "Drake Corsair", "NOITEM_Vehicle", 0, 0, "Drake Interplanetary"),
        Item("DRAK_Corsair_Exec_Military", "Drake Corsair", "NOITEM_Vehicle", 0, 0, "Drake Interplanetary"),
        new("hdgw_rifle_ballistic_01", "Arlington Rifle", "WeaponPersonal", "Medium", 0, 0, "Hedeby Gunworks", null, "install", MicroScu: 13000),
    ];

    private static readonly string[] Ships = ["Drake Corsair", "Drake Cutlass Black", "RSI Constellation Andromeda"];

    private static ScreenFrame Read(ScreenTextLine[] lines, string? handle = "nekron") =>
        ScreenFrames.Read(lines, Catalogue, Ships, handle);

    // ---- the loadout ----

    [Fact]
    public void A_loadout_frame_is_recognised_and_names_its_ship()
    {
        var frame = Read(ScreenFrameFixtures.Coolers);

        Assert.Equal(ScreenKind.Loadout, frame.Kind);
        Assert.NotNull(frame.Loadout);
        Assert.Equal("DRAKE CORSAIR", frame.Loadout.ShipRead);
        Assert.Equal("Drake Corsair", frame.Loadout.Ship);
        Assert.Equal("Only showing ships and equipment located in Nyx.", frame.Loadout.Scope);
    }

    [Fact]
    public void Every_component_on_the_coolers_frame_is_named_with_its_size_and_grade_vouching()
    {
        var loadout = Read(ScreenFrameFixtures.Coolers).Loadout!;

        Assert.Equal(6, loadout.Fittings.Count);

        Assert.All(loadout.Fittings, f =>
        {
            Assert.NotNull(f.Name);
            Assert.Equal("Exact", f.Tier);
            Assert.Contains("size", f.Agrees);
            Assert.Contains("grade", f.Agrees);
            Assert.Empty(f.Disagrees);
        });

        Assert.Equal(["Cooler 2", "Cooler I", "Power Plant 1", "Power Plant 2", "Quantum Drive", "Shield Generator 1"],
            loadout.Fittings.Select(f => f.Slot));
    }

    /// <summary>
    /// The name alone does not tell the quantum drive from the point-defence
    /// gun of the same name; the screen's own size and grade do.
    /// </summary>
    [Fact]
    public void The_screen_prefix_tells_the_Torrent_drive_from_the_Torrent_gun()
    {
        var drive = Read(ScreenFrameFixtures.Coolers).Loadout!.Fittings.Single(f => f.Slot == "Quantum Drive");

        Assert.Equal("Civ/2/C Torrent", drive.Read);
        Assert.Equal("Torrent", drive.Name);
        Assert.Equal("QDRV_ARCC_S02_Torrent_SCItem", drive.ClassName);
    }

    [Fact]
    public void A_missile_tag_is_stripped_and_its_size_checked_against_the_numeral()
    {
        var loadout = Read(ScreenFrameFixtures.Missiles).Loadout!;

        var chaos = loadout.Fittings.First(f => f.Read == "[IR31 •chaos' Missile");
        Assert.Equal("'Chaos' III Missile", chaos.Name);
        Assert.Equal("Decorated", chaos.Tier);
        Assert.Contains("size", chaos.Agrees);

        var arrester = loadout.Fittings.First(f => f.Read == "tCS31 Arrester Missile");
        Assert.Equal("Arrester III Missile", arrester.Name);
        Assert.Contains("size", arrester.Agrees);

        // The rack whose D read as O.
        var rack = loadout.Fittings.First(f => f.Read == "MSO-423 Missile Rack");
        Assert.Equal("MSD-423 Missile Rack", rack.Name);
        Assert.Equal("Confusable", rack.Tier);
    }

    /// <summary>
    /// Without the tag there is no size to check, and the two Chaos missiles
    /// fold to the same name. Two candidates is the honest answer.
    /// </summary>
    [Fact]
    public void A_missile_read_without_its_tag_is_not_named_when_two_sizes_fit()
    {
        var loadout = Read(ScreenFrameFixtures.Missiles).Loadout!;
        var bare = loadout.Fittings.First(f => f.Read == "'Chaos• Missile");

        Assert.Null(bare.Name);
        Assert.Equal("Decorated", bare.Tier);
    }

    /// <summary>
    /// "MBA Cannon" was the M6A. With 6 read as B the reading fits the M8A
    /// exactly as well, and confidently naming the wrong gun is the one
    /// failure this feature must never have.
    /// </summary>
    [Fact]
    public void A_cannon_whose_six_read_as_B_is_not_named_because_the_M8A_fits_too()
    {
        var loadout = Read(ScreenFrameFixtures.Turrets).Loadout!;
        var mba = loadout.Fittings.First(f => f.Read == "MBA Cannon");

        Assert.Null(mba.Name);
        Assert.Null(mba.ClassName);
        Assert.Equal("Confusable", mba.Tier);
        Assert.Equal("(port label did not read)", mba.Slot);
    }

    [Fact]
    public void A_port_with_nothing_readable_under_it_is_kept_as_such_and_a_garbled_part_is_kept_verbatim()
    {
        var loadout = Read(ScreenFrameFixtures.Turrets).Loadout!;

        var remote = loadout.Fittings.Single(f => f.Slot == "@hardpoint_remote_turret_top");
        Assert.True(remote.NothingRead);

        var garbled = loadout.Fittings.Single(f => f.Slot == "Turret Weapon Slot 2");
        Assert.Equal("vanpuck se Gimbal Mount", garbled.Read);
        Assert.Null(garbled.Name);
        Assert.Equal("None", garbled.Tier);

        // The tree's connector glyph is stripped from the label it sat before.
        Assert.Contains(loadout.Fittings, f => f.Slot == "Turret Weapon Slot 1" && f.Name == "VariPuck S2 Gimbal Mount");
    }

    [Fact]
    public void A_ship_whose_maker_misread_is_offered_as_a_resemblance_and_not_named()
    {
        var loadout = Read(ScreenFrameFixtures.Duke).Loadout!;

        Assert.Equal("DUKE CORSAIR", loadout.ShipRead);
        Assert.Null(loadout.Ship);
        Assert.Equal(["Drake Corsair"], loadout.LooksLike);

        // The parts still read; only the ship is withheld.
        Assert.Contains(loadout.Fittings, f => f.Slot == "Radar" && f.Name == "Chernykh");
        Assert.Contains(loadout.Fittings, f => f.Name == "Drake Corsair Standard Flight Blade");
    }

    [Fact]
    public void A_loadout_frame_keeps_the_component_tooltip_it_carries()
    {
        var frame = Read(ScreenFrameFixtures.Duke);

        Assert.Equal(ScreenKind.Loadout, frame.Kind);
        Assert.NotNull(frame.Tooltip);
        Assert.Equal("Drake Interplanetary", frame.Tooltip.Reading.Fields["Manufacturer"]);
    }

    [Theory]
    [InlineData("Civ/2/C Frost-Star EX", "Frost-Star EX", 2, 3)]
    [InlineData("Ind/3/A Parapet", "Parapet", 3, 1)]
    [InlineData("Civ/I/C Frost-Star", "Frost-Star", 1, 3)]
    [InlineData("M6A Cannon", "M6A Cannon", null, null)]
    public void The_component_prefix_gives_the_size_and_the_grade(string read, string name, int? size, int? grade)
    {
        var (got, gotSize, gotGrade) = ScreenFrames.Undecorate(read);

        Assert.Equal(name, got);
        Assert.Equal(size, gotSize);
        Assert.Equal(grade, gotGrade);
    }

    [Theory]
    [InlineData("[IR31 •chaos' Missile", "•chaos' Missile", 3)]
    [InlineData("tCS31 Arrester Missile", "Arrester Missile", 3)]
    [InlineData("[IR3] 'Chaos' Missile", "'Chaos' Missile", 3)]
    public void The_missile_tag_gives_the_size(string read, string name, int size)
    {
        var (got, gotSize, gotGrade) = ScreenFrames.Undecorate(read);

        Assert.Equal(name, got);
        Assert.Equal(size, gotSize);
        Assert.Null(gotGrade);
    }

    // ---- the map ----

    [Fact]
    public void The_map_footer_gives_the_system_the_place_and_the_position()
    {
        var frame = Read(ScreenFrameFixtures.Map);

        Assert.Equal(ScreenKind.Map, frame.Kind);
        Assert.NotNull(frame.Map);
        Assert.Equal("PYRO", frame.Map.SystemRead);
        Assert.Equal("DUDLEY & DAUGHTERS", frame.Map.PlaceRead);
        Assert.Equal(0.0, frame.Map.Latitude);
        Assert.Equal(-155.25, frame.Map.Longitude);
        Assert.Equal(68.33, frame.Map.Gigametres);
        Assert.True(frame.Map.NoAcceptedContracts);
    }

    /// <summary>The same footer read the other way, with the system inline and the ampersand as a B.</summary>
    [Fact]
    public void The_footer_reads_with_the_system_inline_too()
    {
        ScreenTextLine[] lines =
        [
            new("9 PYRO DUDLEY B DAUGHTERS > 0.000 -155.250 68.33GM", 623, 1202, 20),
            .. ScreenFrameFixtures.Map.Where(l => l.Top > 1300),
        ];

        var map = Read(lines).Map!;

        Assert.Equal("PYRO", map.SystemRead);
        Assert.Equal("DUDLEY & DAUGHTERS", map.PlaceRead);
        Assert.Equal(68.33, map.Gigametres);
    }

    // ---- the wallet ----

    [Fact]
    public void The_wallet_says_why_it_did_not_read_rather_than_returning_nothing()
    {
        var wallet = Read(ScreenFrameFixtures.Map).Wallet;

        Assert.NotNull(wallet);
        Assert.Null(wallet.Balance);
        Assert.Contains("face", wallet.Trouble);
    }

    [Fact]
    public void The_wallet_reads_when_the_engine_returns_a_figure_above_the_handle()
    {
        ScreenTextLine[] lines =
        [
            new("1,971,263", 980, 1305, 17),
            .. ScreenFrameFixtures.Map,
        ];

        var wallet = Read(lines).Wallet!;

        Assert.Equal(1_971_263, wallet.Balance);
        Assert.Null(wallet.Trouble);
    }

    [Fact]
    public void Without_a_handle_the_wallet_cannot_be_looked_for_and_says_so()
    {
        var wallet = Read(ScreenFrameFixtures.Map, handle: null).Wallet!;

        Assert.Null(wallet.Balance);
        Assert.Contains("handle", wallet.Trouble);
    }

    // ---- the rest ----

    [Fact]
    public void The_looting_view_is_a_tooltip_and_carries_no_wallet()
    {
        var frame = Read(ScreenFrameFixtures.Looting);

        Assert.Equal(ScreenKind.Tooltip, frame.Kind);
        Assert.Equal("Arlington Rifle", frame.Tooltip!.Reading.Name);

        // No mobiGlas bar on the looting view, so no wallet to fail to read.
        Assert.Null(frame.Wallet);
    }

    [Fact]
    public void A_mobiGlas_screen_with_no_reader_is_filed_as_such_and_keeps_its_text()
    {
        // The app bar alone, at the bottom of a screen nobody has written a reader for.
        var bar = ScreenFrameFixtures.Map.Where(l => l.Top > 1300).ToArray();
        ScreenTextLine[] lines = [new("REPUTATION", 900, 200, 30), new("Hurston Dynamics", 900, 300, 16), .. bar];

        var frame = Read(lines);

        Assert.Equal(ScreenKind.MobiGlas, frame.Kind);
        Assert.Contains("Hurston Dynamics", frame.Lines);
    }

    [Fact]
    public void A_frame_with_nothing_on_it_is_unknown()
    {
        // Two lines, because a single line on its own is read as a crop of a
        // name - the one thing a lone line can be.
        var frame = Read([new("CLICK DRAG", 1342, 1398, 18), new("QUICK EQUIP", 1623, 1398, 20)]);

        Assert.Equal(ScreenKind.Unknown, frame.Kind);
        Assert.Null(frame.Wallet);
    }
}
