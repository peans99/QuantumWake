using System.Text.Json;
using Quantumwake.Core.GameData;
using Quantumwake.Core.State;

namespace Quantumwake.Tests;

/// <summary>
/// Event totals: the installed game's points table against the contracts the
/// logs show finished.
/// </summary>
/// <remarks>
/// The catalogue here is a cut of RSI Discovery Month as 4.10.2 states it -
/// the overall bar's tiers, the Defense and Transport bars and the points of
/// four real contracts - so the arithmetic is checked against numbers the game
/// uses, not invented ones. The reader itself is proven against the archive
/// (docs/events.md); a 316 MB fixture is neither possible nor useful.
/// </remarks>
public class EventProgressTests
{
    private const string Defense = "0cfd698b-a4c5-4139-b175-56157424d5c9";
    private const string Transport = "d26f3e42-d9b4-41dc-a52b-174833256dac";

    private static readonly GameScenarioCatalogue Catalogue = new(
        [
            new GameScenario("Iasi_ScenarioProgress", "RSI Discovery Event", "", Percent: true,
            [
                new GameScenarioTrack("iasi_Journal_PlayerTotal", "Your total", Overall: true, Tag: null, Color: null,
                [
                    new GameScenarioTier(4500, "R_PU_IASI_OP_1", "BriskAir IC-10 Cooler"),
                    new GameScenarioTier(9900, "R_PU_IASI_OP_2", "Ursa Starwalker Livery"),
                    new GameScenarioTier(24000, "R_PU_IASI_OP_3", ""),
                    new GameScenarioTier(30000, "R_PU_IASI_OP_4", ""),
                ]),
                new GameScenarioTrack("iasi_Journal_Defense", "Defense", Overall: false, Defense, "#125b7f",
                [
                    new GameScenarioTier(2000, "R_PU_IASI_DEFENSE_1", ""),
                    new GameScenarioTier(4000, "R_PU_IASI_DEFENSE_2", ""),
                ]),
                new GameScenarioTrack("iasi_Journal_Transport", "Transport", Overall: false, Transport, "#125b7f",
                [
                    new GameScenarioTier(2000, "R_PU_IASI_TRANSPORT_1", ""),
                ]),
            ]),
            new GameScenario("ORS_ScenarioProgress", "Orison Relief", "", Percent: false,
            [
                new GameScenarioTrack("ClearAir_Journal_Total_Personal", "Your Total", Overall: true, null, null,
                    [new GameScenarioTier(10800, "ORS_Pistol", "ORS Pistol")]),
            ]),
        ],
        [
            new GameScenarioContract("Iasi_Patrol_Hard", "Iasi_ScenarioProgress", "Neutralize Threats", "Foxwell Enforcement", 417, [Defense]),
            new GameScenarioContract("Iasi_DefendShip_Easy", "Iasi_ScenarioProgress", "Support Ship", "Foxwell Enforcement", 146, [Defense]),
            new GameScenarioContract("Iasi_HaulCargo_AtoB_Supply_SecuritySupplies", "Iasi_ScenarioProgress", "Important Supply Haul", "Covalex", 625, [Transport]),
            new GameScenarioContract("Iasi_ResourceGathering_Mining_Quantanium", "Iasi_ScenarioProgress", "Procure Refined Quantainium", "Shubin Interstellar", 1142, []),
            new GameScenarioContract("ORS_CA_Medium", "ORS_ScenarioProgress", "Medium Materials Order", "", 2208, []),
        ]);

    private static readonly DateTimeOffset Day = new(2026, 10, 9, 18, 0, 0, TimeSpan.Zero);

    private static ContractRecord Run(string contract, ContractOutcome outcome, int minutes = 0, string? mission = null) =>
        new(Day.AddMinutes(minutes), contract, contract, "", null, null, null, Accepted: false)
        {
            MissionId = mission ?? Guid.NewGuid().ToString(),
            Outcome = outcome,
            CompletedAt = outcome == ContractOutcome.Completed ? Day.AddMinutes(minutes + 20) : null,
        };

    private static EventStatus Discovery(params ContractRecord[] runs) =>
        EventProgress.Build(Catalogue, runs).Single(e => e.Id == "Iasi_ScenarioProgress");

    private static EventTrackStatus Track(EventStatus status, string name) => status.Tracks.Single(t => t.Name == name);

    [Fact]
    public void Every_completed_contract_counts_on_the_overall_bar()
    {
        var status = Discovery(
            Run("Iasi_Patrol_Hard", ContractOutcome.Completed),
            Run("Iasi_HaulCargo_AtoB_Supply_SecuritySupplies", ContractOutcome.Completed, 30),
            Run("Iasi_ResourceGathering_Mining_Quantanium", ContractOutcome.Completed, 60));

        Assert.Equal(417 + 625 + 1142, Track(status, "Your total").Points);
        Assert.Equal(3, status.Completed);
    }

    [Fact]
    public void A_tag_bar_counts_only_the_contracts_that_award_its_tag()
    {
        var status = Discovery(
            Run("Iasi_Patrol_Hard", ContractOutcome.Completed),
            Run("Iasi_DefendShip_Easy", ContractOutcome.Completed, 30),
            Run("Iasi_HaulCargo_AtoB_Supply_SecuritySupplies", ContractOutcome.Completed, 60),
            Run("Iasi_ResourceGathering_Mining_Quantanium", ContractOutcome.Completed, 90));

        Assert.Equal(417 + 146, Track(status, "Defense").Points);
        Assert.Equal(625, Track(status, "Transport").Points);
    }

    /// <summary>
    /// The journal moves on completion and nothing else, so a contract dropped
    /// or lost - or still running - adds nothing, however many were taken.
    /// </summary>
    [Fact]
    public void Abandoned_failed_and_open_contracts_add_nothing()
    {
        var status = Discovery(
            Run("Iasi_Patrol_Hard", ContractOutcome.Abandoned),
            Run("Iasi_Patrol_Hard", ContractOutcome.Failed, 10),
            Run("Iasi_Patrol_Hard", ContractOutcome.InProgress, 20));

        Assert.Equal(0, Track(status, "Your total").Points);
        Assert.Equal(0, status.Completed);
        Assert.Equal(1, status.Contracts.Single(c => c.Id == "Iasi_Patrol_Hard").Open);
    }

    /// <summary>
    /// The stored copy of a log and the live session both hold the contract
    /// being played: seen open in one and finished in the other, it is one
    /// completion, not one and a running one - and never two completions.
    /// </summary>
    [Fact]
    public void The_same_mission_from_the_store_and_the_live_session_counts_once_and_as_its_latest_word()
    {
        var status = Discovery(
            Run("Iasi_Patrol_Hard", ContractOutcome.InProgress, mission: "m-1"),
            Run("Iasi_Patrol_Hard", ContractOutcome.Completed, mission: "m-1"),
            Run("Iasi_Patrol_Hard", ContractOutcome.Completed, mission: "m-1"));

        Assert.Equal(417, Track(status, "Your total").Points);
        var row = status.Contracts.Single(c => c.Id == "Iasi_Patrol_Hard");
        Assert.Equal(1, row.Completed);
        Assert.Equal(0, row.Open);
    }

    [Fact]
    public void Tiers_are_reached_at_their_points_and_the_next_one_says_what_is_left()
    {
        // Five Neutralize Threats: 2,085 on Defense, past its first tier.
        var status = Discovery([.. Enumerable.Range(0, 5).Select(i => Run("Iasi_Patrol_Hard", ContractOutcome.Completed, i * 30))]);
        var defense = Track(status, "Defense");

        Assert.Equal(2085, defense.Points);
        Assert.True(defense.Tiers[0].Reached);
        Assert.False(defense.Tiers[1].Reached);
        Assert.Equal(4000, defense.NextTier);
        Assert.Equal(1915, defense.ToNext);
    }

    /// <summary>
    /// The suggestion is the arithmetic the journal never shows: 1,915 short
    /// is five more Neutralize Threats or fourteen Support Ships.
    /// </summary>
    [Fact]
    public void The_fastest_way_to_the_next_tier_is_the_fewest_completions_of_a_contract_that_counts_there()
    {
        var status = Discovery([.. Enumerable.Range(0, 5).Select(i => Run("Iasi_Patrol_Hard", ContractOutcome.Completed, i * 30))]);
        var fastest = Track(status, "Defense").Fastest;

        Assert.Equal("Iasi_Patrol_Hard", fastest[0].Contract);
        Assert.Equal(5, fastest[0].Needed);
        Assert.Equal(5, fastest[0].Done);
        Assert.Equal(14, fastest.Single(s => s.Contract == "Iasi_DefendShip_Easy").Needed);
        Assert.DoesNotContain(fastest, s => s.Contract == "Iasi_HaulCargo_AtoB_Supply_SecuritySupplies");
    }

    [Fact]
    public void A_bar_with_every_tier_reached_has_nothing_next_and_suggests_nothing()
    {
        var status = Discovery([.. Enumerable.Range(0, 4).Select(i => Run("Iasi_HaulCargo_AtoB_Supply_SecuritySupplies", ContractOutcome.Completed, i * 30))]);
        var transport = Track(status, "Transport");

        Assert.Equal(2500, transport.Points);
        Assert.Null(transport.NextTier);
        Assert.Null(transport.ToNext);
        Assert.Empty(transport.Fastest);
    }

    /// <summary>
    /// A patch that renames the event's contracts would leave every bar short
    /// and say nothing. A contract carrying the event's prefix that the table
    /// does not list is counted as unrecognised so the page can say so.
    /// </summary>
    [Fact]
    public void A_contract_with_the_events_prefix_that_the_table_does_not_list_is_reported_not_dropped_silently()
    {
        var status = Discovery(
            Run("Iasi_Patrol_Hard", ContractOutcome.Completed),
            Run("Iasi_Patrol_Hard_v2", ContractOutcome.Completed, 30));

        Assert.Equal(1, status.Unrecognised);
        Assert.Equal(417, Track(status, "Your total").Points);
    }

    /// <summary>
    /// The check that the arithmetic is the game's: this install's 15 Medium
    /// and 12 Large Materials Orders for Orison Relief come to 72,648 points,
    /// past its 72,000 top tier. Here the Medium ones alone, against the first.
    /// </summary>
    [Fact]
    public void Each_event_is_totalled_on_its_own()
    {
        var all = EventProgress.Build(Catalogue,
        [
            .. Enumerable.Range(0, 5).Select(i => Run("ORS_CA_Medium", ContractOutcome.Completed, i * 30)),
            Run("Iasi_Patrol_Hard", ContractOutcome.Completed, 200),
        ]);

        var ors = all.Single(e => e.Id == "ORS_ScenarioProgress");
        Assert.Equal(5 * 2208, ors.Tracks[0].Points);
        Assert.True(ors.Tracks[0].Tiers[0].Reached);
        Assert.Equal(417, all.Single(e => e.Id == "Iasi_ScenarioProgress").Tracks[0].Points);
    }

    [Fact]
    public void The_event_played_most_recently_comes_first_and_an_unplayed_one_says_it_has_not_been_seen()
    {
        var all = EventProgress.Build(Catalogue, [Run("Iasi_Patrol_Hard", ContractOutcome.Completed)]);

        Assert.Equal("Iasi_ScenarioProgress", all[0].Id);
        Assert.NotNull(all[0].FirstSeen);
        Assert.Null(all.Single(e => e.Id == "ORS_ScenarioProgress").FirstSeen);
    }

    [Fact]
    public void A_contract_row_names_the_bars_it_counts_on_overall_first()
    {
        var pay = EventPay.For(Catalogue).Pay("iasi_patrol_hard");

        Assert.NotNull(pay);
        Assert.Equal(417, pay.Points);
        Assert.Equal("RSI Discovery Event", pay.Event);
        Assert.Equal(["Your total", "Defense"], pay.Tracks);
        Assert.Null(EventPay.For(Catalogue).Pay("Covalex_Stanton_Easy_RecoverCargo"));
    }

    [Fact]
    public void No_game_data_means_no_events_rather_than_an_error()
    {
        Assert.Empty(EventProgress.Build(GameScenarioCatalogue.Empty, [Run("Iasi_Patrol_Hard", ContractOutcome.Completed)]));
    }

    /// <summary>
    /// The catalogue rides the game-data cache as JSON. Records with
    /// read-only lists have lost their contents through a cache before, so
    /// the round trip is pinned down here rather than discovered on a restart.
    /// </summary>
    [Fact]
    public void The_catalogue_survives_the_game_data_cache()
    {
        var back = JsonSerializer.Deserialize<GameScenarioCatalogue>(JsonSerializer.Serialize(Catalogue))!;

        Assert.Equal(2, back.Scenarios.Count);
        Assert.Equal(3, back.Scenarios[0].Tracks.Count);
        Assert.Equal(4500, back.Scenarios[0].Tracks[0].Tiers[0].MinPoints);
        Assert.Equal(Defense, back.Scenarios[0].Tracks[1].Tag);
        Assert.Equal([Defense], back.Contracts[0].Tags);
        Assert.Equal(417, back.Contracts[0].Points);
    }
}
