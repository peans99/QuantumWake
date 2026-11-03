using Quantumwake.Core.Events;
using Quantumwake.Core.GameData;
using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// What a logged contract pays, by the installed game rather than a text mod.
/// The figures are 4.10.2's, as <c>--missions</c> reads them off this install:
/// a Red Wind haul offered from Member pays 500 on Hauling and 50 on Affinity,
/// a Bounty Hunters Guild kill pays 500 and takes 250 back if it fails, and an
/// Iasi patrol pays event points and no reputation at all.
/// </summary>
public class MissionPayTests
{
    private const string RedWind = "FactionReputation_Lawful_RedWindLinehaul";
    private const string Guild = "FactionReputation_Lawful_BountyHuntersGuild";
    private const string HaulingScope = "ReputationScope_Hauling";

    private const string MemberHaul = "1f0b2c34-0000-4000-8000-000000000001";
    private const string GuildKill = "1f0b2c34-0000-4000-8000-000000000002";
    private const string IasiPatrol = "1f0b2c34-0000-4000-8000-000000000003";
    private const string FtlCourier = "1f0b2c34-0000-4000-8000-000000000004";

    private static GameRepRank R(string name, long min) => new(name, null, min);

    private static readonly GameRepScope Hauling = new(HaulingScope, "Hauling", "@RepScope_TransportGuild_Name", 367601,
    [
        R("Not Eligible", -1000), R("Trainee", 0), R("Rookie", 50), R("Junior", 250),
        R("Member", 5250), R("Experienced", 27750), R("Senior", 77750), R("Master", 237750),
    ]);

    private static GameRepLine Line(string faction, string scope, int amount, MissionOutcomes when = MissionOutcomes.Completed) =>
        new(faction, scope, amount, when);

    /// <summary>A Red Wind haul for every rank, as the install offers them: each opens at a rank and stays open to Master.</summary>
    private static IEnumerable<KeyValuePair<string, GameMissionPay>> RedWindLadder() =>
        new (int Min, int Pays)[] { (1, 50), (2, 100), (2, 200), (3, 250), (4, 500), (5, 1000), (6, 2000), (7, 4000) }
            .Select((x, i) => new KeyValuePair<string, GameMissionPay>(
                $"00000000-0000-4000-8000-{i:D12}",
                new GameMissionPay($"RedWind_Haul_{i}",
                    [Line(RedWind, HaulingScope, x.Pays), Line(RedWind, "ReputationScope_Affinity", 50)],
                    RankScope: HaulingScope, MinRank: x.Min, MaxRank: 7)));

    private static readonly GameReputationCatalogue Catalogue = new(
        [Hauling], [new GameRepOrganisation(RedWind, "Red Wind Linehaul", null)])
    {
        Missions = new Dictionary<string, GameMissionPay>(StringComparer.OrdinalIgnoreCase)
        {
            [MemberHaul] = new("RedWind_Pyro_SmallGrade_Multi3ToSingle",
                [Line(RedWind, HaulingScope, 500), Line(RedWind, "ReputationScope_Affinity", 50)],
                RankScope: HaulingScope, MinRank: 4, MaxRank: 7),
            [GuildKill] = new("BountyHuntersGuild_Bounty_Stanton_Easy_0",
                [Line(Guild, "ReputationScope_Bounty_BountyHuntersGuild", 500),
                 Line(Guild, "ReputationScope_Bounty_BountyHuntersGuild", -250, MissionOutcomes.Failed)]),
            [IasiPatrol] = new("Iasi_Patrol_Hard", []),
            [FtlCourier] = new("FTL_Courier_Stanton_Food_Rank0_2",
                [Line("FactionReputation_Lawful_FTLCourier", "FactionReputationScope", 100)], Blueprint: 1),
        }
        .Concat(RedWindLadder())
        .ToDictionary(StringComparer.OrdinalIgnoreCase),
    };

    private static ContractRecord Contract(string? definitionId, string? title = null, string issuer = "Red Wind") =>
        new(DateTimeOffset.UnixEpoch, "raw", "Red Wind · Haul", issuer, "Pyro", null, "Hauling", false)
        {
            DefinitionId = definitionId,
            Title = title,
            Outcome = ContractOutcome.Completed,
        };

    [Fact]
    public void The_installs_figure_is_the_contracts_own_and_beats_the_ladder_tag()
    {
        var haul = Contract(MemberHaul, "Member | Stellar Small Haul | to Gaslight [50/200/250/500/1000/2000/4000 Rep]: ");

        Assert.Equal(500, ContractPay.Rep(haul, Catalogue));
        Assert.Equal(ContractPay.Game, ContractPay.RepSource(haul, Catalogue));
    }

    /// <summary>The Affinity 50 is a second bar; what a haul "pays" is the 500.</summary>
    [Fact]
    public void Two_ladders_give_the_larger_figure_and_keep_both_lines()
    {
        var pay = Catalogue.Mission(MemberHaul)!;

        Assert.Equal(500, pay.OnCompletion);
        Assert.Equal(2, pay.Rep.Count);
    }

    [Fact]
    public void A_penalty_for_failing_is_not_what_finishing_pays()
    {
        Assert.Equal(500, ContractPay.Rep(Contract(GuildKill, issuer: "Bounty Hunters Guild"), Catalogue));
    }

    /// <summary>The install knows an Iasi patrol pays no rep; a tag cannot overrule that.</summary>
    [Fact]
    public void When_the_install_knows_the_contract_its_silence_is_an_answer()
    {
        var patrol = Contract(IasiPatrol, "Iasi Patrol [150 Rep] [BP]: ");

        Assert.Null(ContractPay.Rep(patrol, Catalogue));
        Assert.Null(ContractPay.RepSource(patrol, Catalogue));
        Assert.False(ContractPay.Blueprint(patrol, Catalogue));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-00000000dead")]
    public void A_contract_the_install_does_not_know_falls_back_to_the_tag(string? definitionId)
    {
        var tagged = Contract(definitionId, "Large Covalex Shipment Needs Recovering [200 Rep] [BP]*: ");

        Assert.Equal(200, ContractPay.Rep(tagged, Catalogue));
        Assert.Equal(ContractPay.Tag, ContractPay.RepSource(tagged, Catalogue));
        Assert.True(ContractPay.Blueprint(tagged, Catalogue));
        Assert.Null(ContractPay.BlueprintChance(tagged, Catalogue));

        Assert.Null(ContractPay.Rep(Contract(definitionId, "Red Wind Seeking New Haulers: "), Catalogue));
    }

    /// <summary>All 18 blueprints this install received followed a contract like this one; StarStrings had tagged 10.</summary>
    [Fact]
    public void A_blueprint_comes_from_the_contracts_results_with_its_chance()
    {
        var courier = Contract(FtlCourier, "FTL Courier: Food Delivery: ", "FTL");

        Assert.True(ContractPay.Blueprint(courier, Catalogue));
        Assert.Equal(1, ContractPay.BlueprintChance(courier, Catalogue));
    }

    private static ReputationReading RedWindAt(int held) =>
        new ReputationReading("RED WIND LINEHAUL", "NEUTRAL", null, [], "HAULING", null)
            .WithBars([.. Hauling.Ranks.Select((r, i) =>
                new RepRankCard(r.Name.ToUpperInvariant(), null, i < held ? 1 : i == held ? 0.5 : 0))]);

    /// <summary>
    /// The ladder StarStrings printed, from the contracts: what the haul made
    /// for the held rank pays. Rookie offers two, 100 and 200; the more is the figure.
    /// </summary>
    [Theory]
    [InlineData(1, 50)]
    [InlineData(2, 200)]
    [InlineData(3, 250)]
    [InlineData(4, 500)]
    [InlineData(7, 4000)]
    public void One_more_haul_pays_what_the_install_offers_at_the_held_rank(int held, int pays)
    {
        var history = new[] { new RepSighting("a.jpg", DateTimeOffset.UnixEpoch, RedWindAt(held)) };

        var pay = Assert.Single(ReputationBoards.Build(history, Catalogue, []).Organisations).Pay!;

        Assert.Equal(pays, pay.Rep);
        Assert.Equal(ContractPay.Game, pay.Source);
        Assert.Equal(Hauling.Ranks[held].Name, pay.Contract);
        Assert.Null(pay.At);
    }

    /// <summary>Not Eligible is offered nothing on the ladder, so the tag still answers.</summary>
    [Fact]
    public void Below_every_contract_the_tag_is_still_read()
    {
        var history = new[] { new RepSighting("a.jpg", DateTimeOffset.UnixEpoch, RedWindAt(0)) };
        var tagged = Contract(null, "Large Covalex Shipment Needs Recovering [200 Rep]: ");

        var pay = Assert.Single(ReputationBoards.Build(history, Catalogue, [tagged]).Organisations).Pay!;

        Assert.Equal((200, "tag"), (pay.Rep, pay.Source));
    }

    [Fact]
    public void The_finished_work_sums_the_installs_figures_and_counts_the_tags_apart()
    {
        var work = ReputationBoards.Work("Red Wind Linehaul",
            [Contract(MemberHaul), Contract(MemberHaul), Contract(null, "Junior | Haul [250 Rep]: ")],
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, Catalogue)!;

        Assert.Equal(1250, work.Rep);
        Assert.Equal(3, work.RepFrom);
        Assert.Equal(1, work.RepTagged);
    }

    // ----- Two progressions for one organisation -----

    private const string Headhunters = "FactionReputation_Unlawful_HeadHunters";
    private const string StandingScope = "FactionReputationScope";
    private const string ShipCombatScope = "ReputationScope_ShipCombat_HeadHunters";

    private static readonly GameRepScope Standing = new(StandingScope, "Standing", "@RepScope_Standing_Name", 95250,
    [
        R("Neutral", 0), R("Jr. Contractor", 800), R("Contractor", 2200), R("Sr. Contractor", 5800),
        R("Veteran Contractor", 15000), R("Head Contractor", 38000), R("Elite Contractor", 95250),
    ]);

    private static readonly GameRepScope ShipCombat = new(ShipCombatScope, null, "@RepScope_ShipCombat_HeadHunters_Name", 480001,
    [
        R("Not Eligible", -1000), R("Applicant", 0), R("Rank I", 1), R("Rank II", 3000), R("Rank III", 10000),
        R("Rank IV", 30000), R("Rank V", 100000), R("Rank VI", 480000),
    ]);

    private const string Ambush = "1f0b2c34-0000-4000-8000-0000000000a1";
    private const string Hijacked = "1f0b2c34-0000-4000-8000-0000000000a2";

    /// <summary>
    /// 4.10.2's Headhunters: every contract this install logged for them pays
    /// +100 on Standing, gated by Standing rank; only Hijacked Ship pays Ship
    /// Combat, +500, open at every rank.
    /// </summary>
    private static GameReputationCatalogue HeadhuntersCatalogue(bool hijacked) => new(
        [Standing, ShipCombat], [new GameRepOrganisation(Headhunters, "Headhunters", null)])
    {
        Missions = new Dictionary<string, GameMissionPay>(StringComparer.OrdinalIgnoreCase)
        {
            [Ambush] = new("HeadHunters_ShipAmbush_VE", [Line(Headhunters, StandingScope, 100)],
                RankScope: StandingScope, MinRank: 0, MaxRank: 6),
        }
        .Concat(hijacked
            ? [new(Hijacked, new GameMissionPay("HeadHunters_HijackedShip_Caterpillar_Stanton", [Line(Headhunters, ShipCombatScope, 500)]))]
            : Array.Empty<KeyValuePair<string, GameMissionPay>>())
        .ToDictionary(StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>The Ship Combat page as read on 24 October: Rank I, 68% of the way to Rank II.</summary>
    private static RepSighting ShipCombatPage() => new("hh.jpg", DateTimeOffset.UnixEpoch,
        new ReputationReading("HEADHUNTERS", "NEUTRAL", null, [], null, null)
            .WithBars([.. ShipCombat.Ranks.Select((r, i) =>
                new RepRankCard(r.Name.ToUpperInvariant(), null, i < 2 ? 1 : i == 2 ? 0.678 : 0))]));

    private static ContractRecord AmbushFlown() => Contract(Ambush, "Surprise Party [100 Rep]: ", "Head Hunters");

    [Fact]
    public void A_ship_combat_climb_is_priced_by_what_moves_ship_combat_not_by_standings_figure()
    {
        var board = ReputationBoards.Build([ShipCombatPage()], HeadhuntersCatalogue(hijacked: true), [AmbushFlown()]);
        var org = Assert.Single(board.Organisations);

        Assert.Equal(ShipCombatScope, org.Place!.Scope);
        Assert.Equal((500, ReputationBoards.OpenSource), (org.Pay!.Rep, org.Pay.Source));

        // The ambush was finished, and pays nothing on the ladder this page shows.
        Assert.Equal(1, org.Work!.Completed);
        Assert.Equal(0, org.Work.Rep);
        Assert.Equal(0, org.Work.RepFrom);
    }

    /// <summary>
    /// The tag on the ambush says 100, and it is Standing's 100: read against a
    /// Ship Combat bar it is the wrong ladder's figure, so it is not read at all.
    /// </summary>
    [Fact]
    public void When_none_of_their_contracts_moves_the_ladder_shown_the_page_says_which_one_they_move()
    {
        var board = ReputationBoards.Build([ShipCombatPage()], HeadhuntersCatalogue(hijacked: false), [AmbushFlown()]);
        var pay = Assert.Single(board.Organisations).Pay!;

        Assert.Equal(ReputationBoards.ElsewhereSource, pay.Source);
        Assert.Equal(0, pay.Rep);
        Assert.Equal("Standing", pay.Contract);
    }

    /// <summary>
    /// Open contracts are a spread, not a ladder step: Wikelo's run 10 to 1,000.
    /// The figure is what most pay, and the range goes with it.
    /// </summary>
    [Fact]
    public void Contracts_open_at_every_rank_give_the_common_figure_and_the_range()
    {
        var catalogue = HeadhuntersCatalogue(hijacked: true) with
        {
            Missions = new[] { 500, 500, 100, 1000 }
                .Select((rep, i) => new KeyValuePair<string, GameMissionPay>(
                    $"00000000-0000-4000-8000-0000000001{i:D2}",
                    new GameMissionPay($"HeadHunters_Open_{i}", [Line(Headhunters, ShipCombatScope, rep)])))
                .ToDictionary(StringComparer.OrdinalIgnoreCase),
        };

        var pay = Assert.Single(ReputationBoards.Build([ShipCombatPage()], catalogue, []).Organisations).Pay!;

        Assert.Equal((500, 100, 1000, 4), (pay.Rep, pay.Low!.Value, pay.High!.Value, pay.Contracts!.Value));
    }

    /// <summary>
    /// Headhunters pay by difficulty, not by rank: the contracts opening at Jr.
    /// Contractor on 4.10.2 run from 25 to 1,200. The largest priced every +100
    /// patrol as the two Hard outpost raids; the figure is now the usual one.
    /// </summary>
    [Fact]
    public void Contracts_at_one_rank_that_pay_by_difficulty_give_the_common_figure_and_the_range()
    {
        var catalogue = HeadhuntersCatalogue(hijacked: false) with
        {
            Missions = new[] { (100, 1), (100, 1), (150, 1), (1200, 1), (25, 0) }
                .Select((m, i) => new KeyValuePair<string, GameMissionPay>(
                    $"00000000-0000-4000-8000-0000000002{i:D2}",
                    new GameMissionPay($"HH_Pyro_{i}", [Line(Headhunters, StandingScope, m.Item1)],
                        RankScope: StandingScope, MinRank: m.Item2, MaxRank: 6)))
                .ToDictionary(StringComparer.OrdinalIgnoreCase),
        };

        var standing = new RepSighting("st.jpg", DateTimeOffset.UnixEpoch,
            new ReputationReading("HEADHUNTERS", "NEUTRAL", null, [], "STANDING", null)
                .WithBars([.. Standing.Ranks.Select((r, i) =>
                    new RepRankCard(r.Name.ToUpperInvariant(), null, i == 0 ? 1 : i == 1 ? 0.55 : 0))]));

        var pay = Assert.Single(ReputationBoards.Build([standing], catalogue, []).Organisations).Pay!;

        Assert.Equal(ContractPay.Game, pay.Source);
        Assert.Equal((100, 100, 1200, 4), (pay.Rep, pay.Low!.Value, pay.High!.Value, pay.Contracts!.Value));
    }

    [Fact]
    public void A_contract_pays_one_ladder_and_not_the_other()
    {
        var pay = HeadhuntersCatalogue(hijacked: true).Mission(Ambush)!;

        Assert.Equal(100, pay.On(StandingScope));
        Assert.Null(pay.On(ShipCombatScope));
        Assert.Equal(100, pay.OnCompletion);
    }

    /// <summary>The marker's contractDefinitionId reaches the contract, which is the whole join.</summary>
    [Fact]
    public void A_contract_keeps_the_markers_definition_id()
    {
        var builder = new SessionBuilder("test.log");
        builder.Add(new ContractEvent(DateTimeOffset.UnixEpoch, "08cd3d5f-5b63-48e5-8e61-50f7722d98a7",
            "RedWind_Hauling", "RedWind_Pyro_SmallGrade_Multi3ToSingle", MemberHaul));

        var contract = Assert.Single(builder.Build().Contracts);

        Assert.Equal(MemberHaul, contract.DefinitionId);
        Assert.Equal(500, ContractPay.Rep(contract, Catalogue));
    }
}
