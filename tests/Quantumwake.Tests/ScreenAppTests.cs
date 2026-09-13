using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The second night's screens: the Contracts app, the Rep app, the Fleet
/// Manager and its estimate, and the map footer split across five lines.
/// </summary>
/// <remarks>
/// Every frame in <see cref="ScreenAppFixtures"/> is real, and the readings
/// asserted here are the ones the frames actually yielded - including what
/// did not read, which is asserted as not reading rather than papered over.
/// </remarks>
public class ScreenAppTests
{
    private static ItemReference Item(string cls, string name, string type, int size, int grade, string maker = "") =>
        new(cls, name, type, "", size, grade, maker.Length > 0 ? maker : null, null, "install");

    private static readonly ItemReference[] Catalogue =
    [
        Item("COOL_S02_ColdSnap", "ColdSnap", "Cooler", 2, 3),
        Item("JUMP_S02_Excelsior", "Excelsior", "JumpModule", 2, 3),
        Item("Fuse", "Fuse", "Misc", 1, 1),
        Item("Fuse_Box", "Fuse Box Cover", "Misc", 1, 1),
        Item("Livery_Hermes_Keystone", "Hermes Keystone Livery", "Paint", 0, 0),
        Item("POWR_S02_Lotus", "Lotus", "PowerPlant", 2, 1),
        Item("QDRV_S02_Hemera", "Hemera", "QuantumDrive", 2, 1),
        Item("RADR_WLOP_S02_Chernykh", "Chernykh", "Radar", 2, 3),
        Item("SHLD_S02_7MA", "7MA 'Lorica'", "Shield", 2, 1),
        Item("Mount_Gimbal_S4", "VariPuck S4 Gimbal Mount", "Turret", 4, 1),
        Item("KLWE_LaserCannon_S4", "Deadbolt IV Cannon", "WeaponGun", 4, 1),
        Item("BEHR_BallisticGatling_S4", "AD4B Ballistic Gatling", "WeaponGun", 4, 1),
        Item("TRAC_Turret", "Tractor Turret", "Turret", 1, 1),
        Item("RSI_Hermes", "RSI Hermes", "NOITEM_Vehicle", 0, 0),
    ];

    private static readonly string[] Ships =
        ["RSI Hermes", "Drake Corsair", "Drake Clipper", "Anvil C8X Pisces Expedition", "MISC Starfarer", "Mirai Fury"];

    private static ScreenFrame Read(ScreenTextLine[] lines) => ScreenFrames.Read(lines, Catalogue, Ships);

    private static readonly DateTimeOffset At = new(2026, 9, 9, 1, 48, 31, TimeSpan.Zero);

    private sealed class Beliefs : IScreenBeliefs
    {
        public IReadOnlyList<string>? Open { get; init; }
        public IReadOnlyList<string> Flown { get; init; } = [];

        public (string Id, string Name, string? System)? WhereAt(DateTimeOffset at) => ("x", "x", "Pyro");
        public (string Id, string Name, string? System)? PlaceNamed(string read) => null;
        public IReadOnlyList<string>? OpenContractsAt(DateTimeOffset at) => Open;
        public decimal? LedgerRunningAt(DateTimeOffset at) => null;
        public IReadOnlyList<string> StockParts(string ship) => [];
        public IReadOnlyList<string> FlownShips() => Flown;
    }

    private static ScreenCheck Only(IReadOnlyList<ScreenCheck> checks, string subject) =>
        Assert.Single(checks, c => c.Subject == subject);

    // ---- the Contracts app ----

    [Fact]
    public void The_accepted_tab_gives_its_own_count_and_every_card()
    {
        var frame = Read(ScreenAppFixtures.Contracts);

        Assert.Equal(ScreenKind.Contracts, frame.Kind);
        var app = frame.Contracts!;

        Assert.Equal(5, app.Accepted);
        Assert.Equal(10, app.Capacity);
        Assert.Equal(5, app.Cards.Count);

        // The second card's bracket read as "tso/200/..." and still closes it.
        Assert.Contains("ENDGAME", app.Cards[1].Title);
        Assert.Equal("172k", app.Cards[1].Reward);
        Assert.Contains("LINEHAUL", app.Cards[0].Issuer);
    }

    [Fact]
    public void The_selected_contract_reads_with_its_reward_issuer_and_objectives()
    {
        var app = Read(ScreenAppFixtures.Contracts).Contracts!;

        Assert.Equal("Junior I Stellar Small Haul I to Stanton Gateway", app.SelectedTitle);
        Assert.Equal(215_250, app.SelectedReward);
        Assert.Equal("Red Wind Linehaul", app.SelectedIssuer);
        Assert.Equal(4, app.Objectives.Count);
        Assert.StartsWith("Deliver 0/18 SCU of Aluminum", app.Objectives[0]);
    }

    [Theory]
    [InlineData("JUNIOR 1 STELLAR SMALL HAUL 1 TO STANTON GATEWAY", "Junior | Stellar Small Haul | to Stanton Gateway", true)]
    [InlineData("JUNIOR I STELLAR SMALL HAUL I TO ENDGAME", "Junior | Stellar Small Haul | to Endgame", true)]
    [InlineData("JUNIOR I STELLAR SMALL HAUL I TO STANTON GATEWAY", "Junior | Stellar Small Haul | to Ruin Station", false)]
    public void Contract_titles_match_across_the_bars_the_engine_reads_as_I(string screen, string log, bool same)
    {
        Assert.Equal(same, ScreenFrames.SameContract(screen, log));
    }

    [Fact]
    public void The_contracts_check_agrees_when_the_count_and_the_names_both_do()
    {
        var beliefs = new Beliefs
        {
            Open =
            [
                "Junior | Stellar Small Haul | to Stanton Gateway",
                "Junior | Stellar Small Haul | to Endgame",
                "Junior | Stellar Small Haul | to Stanton Gateway",
                "Junior | Stellar Small Haul | to Stanton Gateway",
                "Junior | Stellar Small Haul | to Ruin Station",
            ],
        };

        var check = Only(ScreenChecks.Check(Read(ScreenAppFixtures.Contracts), At, beliefs, null), "Contracts");

        Assert.Equal("agrees", check.Verdict);
        Assert.StartsWith("5 accepted of 10", check.Claim);
    }

    [Fact]
    public void The_contracts_check_names_what_is_on_screen_and_not_in_the_logs()
    {
        var beliefs = new Beliefs { Open = ["Junior | Stellar Small Haul | to Stanton Gateway"] };

        var check = Only(ScreenChecks.Check(Read(ScreenAppFixtures.Contracts), At, beliefs, null), "Contracts");

        Assert.Equal("differs", check.Verdict);
        Assert.Contains("ENDGAME", check.Note);
        Assert.Contains("the tab says 5, the logs say 1", check.Note);
    }

    // ---- the Rep app ----

    [Fact]
    public void The_rep_app_reads_the_organisation_and_standing_and_admits_the_rank_does_not()
    {
        var frame = Read(ScreenAppFixtures.Reputation);

        Assert.Equal(ScreenKind.Reputation, frame.Kind);
        var rep = frame.Reputation!;

        Assert.Equal("HEADHUNTERS", rep.Organisation);
        Assert.Equal("NEUTRAL", rep.Standing);
        Assert.Null(rep.Rank);
        Assert.Equal(6, rep.Organisations.Count);
        Assert.Contains("RED WIND LINEHAUL", rep.Organisations);

        var check = Only(ScreenChecks.Check(frame, At, new Beliefs(), null), "Reputation");
        Assert.Equal("new", check.Verdict);
        Assert.Contains("highlight", check.Note);
    }

    /// <summary>The one frame on which the balance read, and the reading it gave.</summary>
    [Fact]
    public void The_wallet_reads_on_the_rep_frame()
    {
        var wallet = Read(ScreenAppFixtures.Reputation).Wallet!;

        Assert.Equal(2_108_600, wallet.Balance);
    }

    // ---- the map, footer in pieces ----

    [Fact]
    public void A_footer_the_engine_split_into_five_lines_still_reads_with_its_trail()
    {
        var frame = Read(ScreenAppFixtures.MapSplit);

        Assert.Equal(ScreenKind.Map, frame.Kind);
        var map = frame.Map!;

        Assert.Equal("PYRO", map.SystemRead);
        Assert.Equal("RUIN STATION", map.PlaceRead);
        Assert.Equal("TERMINUS > RUIN STATION", map.PathRead);
        Assert.Equal(134.77, map.Longitude);
        Assert.Equal(68.32, map.Gigametres);
        Assert.True(map.AcceptedContracts);
    }

    [Fact]
    public void A_map_listing_accepted_contracts_agrees_when_the_logs_have_some_open()
    {
        var frame = Read(ScreenAppFixtures.MapSplit);

        Assert.Equal("agrees", Only(ScreenChecks.Check(frame, At, new Beliefs { Open = ["one"] }, null), "Contracts").Verdict);
        Assert.Equal("differs", Only(ScreenChecks.Check(frame, At, new Beliefs { Open = [] }, null), "Contracts").Verdict);
    }

    // ---- the Fleet Manager ----

    [Fact]
    public void The_fleet_manager_reads_each_row_under_its_column_and_keeps_names_that_did_not_read()
    {
        var frame = Read(ScreenAppFixtures.FleetManager);

        Assert.Equal(ScreenKind.Fleet, frame.Kind);
        var fleet = frame.Fleet!;

        Assert.Equal(5, fleet.Ships.Count);

        var corsair = fleet.Ships.Single(r => r.Ship == "Drake Corsair");
        Assert.Equal("Levski", corsair.Location);
        Assert.Equal("Stored", corsair.State);
        Assert.Equal("Expedition", corsair.Focus);
        Assert.Equal(72, corsair.Cargo);

        var clipper = fleet.Ships.Single(r => r.Ship == "Drake Clipper");
        Assert.Equal("Port Tressler", clipper.Location);
        Assert.Equal(12, clipper.Cargo);

        // The terminal's face defeated the engine on this one; it is kept as read.
        var pisces = fleet.Ships[0];
        Assert.Null(pisces.Ship);
        Assert.Contains("Anvil", pisces.Read);
        Assert.Equal("Stored", pisces.State);
    }

    [Fact]
    public void A_ship_the_logs_never_saw_flown_is_new_rather_than_a_contradiction()
    {
        var frame = Read(ScreenAppFixtures.FleetManager);

        var check = Only(ScreenChecks.Check(frame, At, new Beliefs { Flown = ["Drake Corsair"] }, null), "Fleet");

        Assert.Equal("new", check.Verdict);
        Assert.Contains("never flown in the logs: Drake Clipper", check.Note);
        Assert.Contains("3 names did not read", check.Claim);
    }

    [Fact]
    public void A_fleet_the_logs_have_all_flown_agrees_and_says_the_berths_are_new()
    {
        var frame = Read(ScreenAppFixtures.FleetManager);

        var check = Only(ScreenChecks.Check(frame, At, new Beliefs { Flown = ["Drake Corsair", "Drake Clipper"] }, null), "Fleet");

        Assert.Equal("agrees", check.Verdict);
        Assert.Contains("stored is new", check.Note);
    }

    // ---- the loadout estimate ----

    [Fact]
    public void The_loadout_estimate_reads_as_a_loadout_with_types_for_ports()
    {
        var frame = Read(ScreenAppFixtures.Estimate);

        Assert.Equal(ScreenKind.Loadout, frame.Kind);
        var loadout = frame.Loadout!;

        Assert.Equal("RSI Hermes", loadout.Ship);
        Assert.Contains("24 items", loadout.Scope);
        Assert.Contains("22,506", loadout.Scope);

        Assert.Equal("ColdSnap", loadout.Fittings.Single(f => f.Slot == "Cooler").Name);
        Assert.Equal("Deadbolt IV Cannon", loadout.Fittings.Single(f => f.Slot == "Gun ×2").Name);
    }

    /// <summary>
    /// The estimate cuts names to fit its column. A prefix counts when it is
    /// long, or when the screen's own size and grade vouch for it.
    /// </summary>
    [Fact]
    public void A_name_the_estimate_cut_short_is_matched_as_a_prefix_when_something_vouches()
    {
        var loadout = Read(ScreenAppFixtures.Estimate).Loadout!;

        var gimbal = loadout.Fittings.Single(f => f.Slot == "Turret ×4");
        Assert.Equal("VariPuck S4 Gimbal Mount", gimbal.Name);
        Assert.Equal("Truncated", gimbal.Tier);

        var shield = loadout.Fittings.Single(f => f.Slot == "Shield Generator");
        Assert.Equal("7MA 'Lorica'", shield.Name);
        Assert.Contains("size", shield.Agrees);
        Assert.Contains("grade", shield.Agrees);

        // "Fuse" is short and undecorated, so it is the exact Fuse and not a
        // prefix of the Fuse Box Cover beside it.
        Assert.Equal("Fuse", loadout.Fittings.Single(f => f.Slot == "Misc.").Name);
    }

    // ---- the kiosk, filed and not read ----

    /// <summary>
    /// The words come from a photograph of a kiosk, not a frame, so this
    /// defends only the filing: the frame is kept whole for the reader to be
    /// written from once one lands.
    /// </summary>
    [Fact]
    public void A_commodity_kiosk_is_filed_as_such_and_its_text_kept()
    {
        var frame = Read(
        [
            new("COMMODITIES", 100, 40, 40),
            new("YOUR INVENTORIES", 70, 155, 18),
            new("DRAKE CATERPILLAR", 90, 210, 16),
            new("CARGO CAPACITY", 62, 335, 14),
            new("67 / 576 SCU", 460, 335, 14),
            new("DYMANTIUM", 160, 473, 16),
            new("8 SCU", 470, 473, 16),
            new("¤ 1/SCU", 470, 500, 12),
        ]);

        Assert.Equal(ScreenKind.Kiosk, frame.Kind);
        Assert.Contains("DYMANTIUM", frame.Lines);
        Assert.Null(frame.Loadout);
    }

    // ---- empty ports ----

    [Fact]
    public void A_port_the_game_calls_empty_is_empty_and_not_an_unmatched_part()
    {
        var loadout = Read(ScreenAppFixtures.HermesFlair).Loadout!;

        var flair = loadout.Fittings.Where(f => f.Slot.StartsWith("Flair")).ToList();
        Assert.Equal(2, flair.Count);
        Assert.All(flair, f => Assert.True(f.IsEmpty));

        var check = Only(ScreenChecks.Check(Read(ScreenAppFixtures.HermesFlair), At, new Beliefs(), null), "Fitted parts");
        Assert.Contains("2 ports empty", check.Claim);
    }

    [Fact]
    public void The_hermes_systems_frame_names_eight_of_nine_and_keeps_the_ninth_verbatim()
    {
        var loadout = Read(ScreenAppFixtures.HermesSystems).Loadout!;

        Assert.Equal(8, loadout.Fittings.Count(f => f.Name is not null));

        var garbled = loadout.Fittings.Single(f => f.Name is null);
        Assert.Equal("Shield Generator 4", garbled.Slot);
        Assert.Contains("Lorjca", garbled.Read);
    }
}
