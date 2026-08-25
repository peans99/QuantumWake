using Quantumwake.Core.Logging;
using Quantumwake.Core.Parsing;
using Quantumwake.Core.State;
using Quantumwake.Data;
using Quantumwake.LogSim;

namespace Quantumwake.Tests;

/// <summary>
/// The simulator is useful only if its claimed story survives the same parser
/// and session builder as a real Game.log.
/// </summary>
public sealed class LogScenarioTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 24, 20, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> ScenarioNames => new()
    {
        "cargo-run",
        "multi-stop-trader",
        "spending",
        "purchase-pairing",
        "medical-respawn",
        "medical-kinds",
        "death-recovery",
        "revived-in-place",
        "crew-flight",
        "party-lifecycle",
        "contract-complete",
        "contract-abandoned",
        "loadout-swap",
        "stash-browse",
        "fleet-growth",
        "ship-retrieval",
        "location-resolution",
        "unexpected-disconnect",
        "combat",
        "all"
    };

    [Fact]
    public void Scenario_names_are_unique()
    {
        Assert.Equal(
            ScenarioCatalogue.All.Count,
            ScenarioCatalogue.All.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Multi_client_scenario_has_distinct_clients_and_ordered_visual_stages()
    {
        var scenario = Assert.Single(MultiClientScenarioCatalogue.All);

        Assert.Equal("org-activity", scenario.Name);
        Assert.Equal(3, scenario.Pilots.Select(p => p.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(3, scenario.Pilots.Select(p => p.Handle).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(3, scenario.Pilots.Select(p => p.Geid).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(3, scenario.Pilots.Select(p => p.SuggestedPort).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, scenario.Stages.Count), scenario.Stages.Select(s => s.Number));
    }

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void Every_scenario_is_a_complete_log_with_no_broken_known_tags(string name)
    {
        WithScenario(name, (path, session) =>
        {
            var parser = new LogEventParser();
            var events = LogFileReader.ReadEvents(path, parser).ToList();

            Assert.NotEmpty(events);
            Assert.Equal(0, parser.UnmatchedKnownTags);
            Assert.Equal("testpilot", session.Handle);
            Assert.True(session.Duration > TimeSpan.Zero);
        });
    }

    [Fact]
    public void Cargo_run_preserves_quantity_totals_and_the_ship_sortie()
    {
        WithScenario("cargo-run", (_, session) =>
        {
            Assert.Collection(session.Trades,
                buy =>
                {
                    Assert.False(buy.IsSell);
                    Assert.Equal(16, buy.Quantity);
                    Assert.Equal(9_600m, buy.Amount);
                },
                sell =>
                {
                    Assert.True(sell.IsSell);
                    Assert.Equal(16, sell.Quantity);
                    Assert.Equal(12_480m, sell.Amount);
                });
            Assert.Single(session.Ships);
            Assert.Equal(1, session.Ships[0].Sorties);
        });
    }

    [Fact]
    public void Spending_counts_only_the_server_confirmed_purchase()
    {
        WithScenario("spending", (_, session) =>
        {
            var purchase = Assert.Single(session.Purchases);
            Assert.Equal("behr_rifle_ballistic_01", purchase.Item);
            Assert.Equal(2_400m, session.Spend);
        });
    }

    [Fact]
    public void Multi_stop_trade_keeps_real_buy_units_and_both_commodities()
    {
        WithScenario("multi-stop-trader", (_, session) =>
        {
            Assert.Equal(4, session.Trades.Count);
            Assert.Equal([32, 32, 64, 64], session.Trades.Select(t => t.Quantity));
            Assert.Equal(2, session.Trades.Select(t => t.ResourceId).Distinct().Count());
            Assert.Equal(2, session.Jumps.Count);
        });
    }

    [Fact]
    public void Purchase_pairing_waits_for_the_matching_final_answer()
    {
        WithScenario("purchase-pairing", (_, session) =>
        {
            var purchase = Assert.Single(session.Purchases);
            Assert.Equal(4, purchase.Quantity);
            Assert.Equal(2_080m, purchase.Total);
            Assert.Equal(520m, purchase.UnitPrice);
        });
    }

    [Fact]
    public void Medical_scenario_records_the_inference_as_an_inference()
    {
        WithScenario("medical-respawn", (_, session) =>
        {
            Assert.Equal(1, session.Incapacitations);
            var respawn = Assert.Single(session.Respawns);
            Assert.Equal("New Babbage", respawn.Place);
            Assert.Equal("incapacitated", respawn.Cause);
            var bed = Assert.Single(session.MedicalBeds);
            Assert.Equal("New Babbage", bed.Place);
            Assert.Equal("after-death", bed.Kind);
        });
    }

    [Fact]
    public void Medical_kinds_cover_waking_treatment_and_a_later_heal()
    {
        WithScenario("medical-kinds", (_, session) =>
        {
            Assert.Equal(["wake", "after-death", "heal"], session.MedicalBeds.Select(b => b.Kind));
            Assert.Single(session.Respawns);
        });
    }

    [Fact]
    public void Corpse_burst_is_one_death_while_same_place_revival_is_not_a_respawn()
    {
        WithScenario("death-recovery", (_, session) =>
        {
            Assert.Equal(1, session.Deaths);
            Assert.Equal("death", Assert.Single(session.Respawns).Cause);
        });

        WithScenario("revived-in-place", (_, session) =>
        {
            Assert.Equal(1, session.Incapacitations);
            Assert.Empty(session.Respawns);
        });
    }

    [Fact]
    public void Crew_scenario_keeps_people_and_party_moments()
    {
        WithScenario("crew-flight", (_, session) =>
        {
            Assert.Collection(session.PartyNotes,
                note => Assert.Equal(("D-Rud", PartyMoment.Connected), (note.Handle, note.Moment)),
                note => Assert.Equal(("astro_ice", PartyMoment.Connected), (note.Handle, note.Moment)),
                note => Assert.Equal(("D-Rud", PartyMoment.BecameLeader), (note.Handle, note.Moment)),
                note => Assert.Equal(("astro_ice", PartyMoment.Disconnected), (note.Handle, note.Moment)));
            Assert.Single(session.Jumps);
            Assert.Single(session.Ships);
        });
    }

    [Fact]
    public void Party_lifecycle_keeps_reconnects_and_disband_but_not_queue_chatter()
    {
        WithScenario("party-lifecycle", (_, session) =>
        {
            Assert.Equal(5, session.PartyNotes.Count);
            Assert.Equal(2, session.PartyNotes.Count(n => n.Moment == PartyMoment.Connected));
            Assert.Single(session.PartyNotes, n => n.Moment == PartyMoment.Disbanded);
        });
    }

    [Fact]
    public void Contract_scenario_completes_two_visible_steps_and_awards_a_blueprint()
    {
        WithScenario("contract-complete", (_, session) =>
        {
            var contract = Assert.Single(session.Contracts);
            Assert.Equal(ContractOutcome.Completed, contract.Outcome);
            Assert.Equal(2, contract.Steps);
            Assert.Equal(2, contract.StepsDone);
            Assert.Equal("Omnisky IX", Assert.Single(session.Blueprints).Name);
        });
    }

    [Fact]
    public void Withdrawn_contract_is_abandoned_without_completed_steps()
    {
        WithScenario("contract-abandoned", (_, session) =>
        {
            var contract = Assert.Single(session.Contracts);
            Assert.Equal(ContractOutcome.Abandoned, contract.Outcome);
            Assert.Equal(1, contract.Steps);
            Assert.Equal(0, contract.StepsDone);
        });
    }

    [Fact]
    public void Equipment_storage_and_fleet_scenarios_preserve_their_distinct_signals()
    {
        WithScenario("loadout-swap", (_, session) =>
        {
            Assert.Equal(3, session.Loadout.Count);
            Assert.Equal(2, session.Loadout.Count(i => i.Port == "weapon_attach_hand_right"));
            var repeated = Assert.Single(session.Loadout, i => i.Port == "Armor_Undersuit");
            Assert.True(repeated.LastSeen > repeated.FirstSeen);
        });

        WithScenario("stash-browse", (_, session) =>
        {
            Assert.Equal(2, session.Stash.Count);
            Assert.Equal(2, session.Stash.Select(i => i.LocationId).Distinct().Count());
            Assert.Equal(3, session.Pickups.Count);
        });

        WithScenario("fleet-growth", (_, session) => Assert.Equal(14, session.FleetSize));
    }

    [Fact]
    public void Retrieval_location_resolution_and_disconnect_cover_late_and_inferred_state()
    {
        WithScenario("ship-retrieval", (_, session) =>
        {
            var ship = Assert.Single(session.Ships);
            Assert.Equal("MISC Freelancer MAX", ship.DisplayName);
            Assert.Equal(1, ship.Sorties);
            Assert.Single(session.Timeline, e => e.Text == "Retrieved MISC Freelancer MAX");
        });

        WithScenario("location-resolution", (_, session) =>
        {
            var jump = Assert.Single(session.Jumps);
            Assert.Equal("RR_MIC_L1", jump.ToId);
            Assert.Equal("microTech L1 Rest Stop", jump.ToName);
        });

        WithScenario("unexpected-disconnect", (_, session) => Assert.Equal(2, session.Disconnects));
    }

    [Fact]
    public void Combat_scenario_exercises_both_sides_of_the_archived_format()
    {
        WithScenario("combat", (_, session) =>
        {
            Assert.Equal(1, session.Kills);
            Assert.Equal(1, session.Deaths);
            Assert.Single(session.Timeline, entry => entry.Kind == "vehicle-destroyed");
        });
    }

    [Fact]
    public void Combined_scenario_composes_every_focused_story()
    {
        WithScenario("all", (_, session) =>
        {
            Assert.Equal(6, session.Trades.Count);
            Assert.Equal(2, session.Purchases.Count);
            Assert.Equal(3, session.Incapacitations);
            Assert.Equal(3, session.Respawns.Count);
            Assert.Equal(4, session.MedicalBeds.Count);
            Assert.Equal(9, session.PartyNotes.Count);
            Assert.Equal(5, Assert.Single(session.Ships).Sorties);
            Assert.Equal(5, session.Jumps.Count);
            Assert.Equal(1, session.ContractsCompleted);
            Assert.Single(session.Contracts, c => c.Outcome == ContractOutcome.Abandoned);
            Assert.Single(session.Blueprints);
            Assert.Equal(3, session.Loadout.Count);
            Assert.Equal(2, session.Stash.Count);
            Assert.Equal(14, session.FleetSize);
            Assert.Equal(2, session.Disconnects);
            Assert.Equal(1, session.Kills);
            Assert.Equal(2, session.Deaths);
            Assert.Single(session.Timeline, entry => entry.Kind == "vehicle-destroyed");
        });
    }

    [Fact]
    public void Org_activity_scenario_survives_three_independent_production_parse_paths()
    {
        WithMultiClientScenario((sessions, unmatched) =>
        {
            Assert.All(unmatched.Values, count => Assert.Equal(0, count));

            var captain = sessions["captain"];
            Assert.Equal("D-Rud", captain.Handle);
            Assert.Equal(9, captain.FleetSize);
            Assert.Equal(2, captain.Loadout.Count);
            Assert.Equal(6, captain.PartyNotes.Count);
            Assert.Single(captain.Jumps);
            Assert.Single(captain.Ships);
            Assert.Equal(1, captain.Kills);
            Assert.Equal(0, captain.Deaths);
            Assert.Single(captain.Timeline, entry => entry.Kind == "vehicle-destroyed");
            var contract = Assert.Single(captain.Contracts);
            Assert.Equal(ContractOutcome.Completed, contract.Outcome);
            Assert.Equal((2, 2), (contract.Steps, contract.StepsDone));
            Assert.Single(captain.Blueprints);

            var trader = sessions["trader"];
            Assert.Equal("astro_ice", trader.Handle);
            Assert.Equal(14, trader.FleetSize);
            Assert.Equal(3, trader.PartyNotes.Count);
            Assert.Equal(2, trader.Trades.Count);
            Assert.Equal([32, 32], trader.Trades.Select(t => t.Quantity));
            Assert.Equal([false, true], trader.Trades.Select(t => t.IsSell));
            Assert.Single(trader.Purchases);
            Assert.Equal(2_400m, trader.Spend);
            Assert.Single(trader.Jumps);
            Assert.Equal(2, trader.Disconnects);

            var medic = sessions["medic"];
            Assert.Equal("Patchwork", medic.Handle);
            Assert.Equal(6, medic.FleetSize);
            Assert.Equal(2, medic.Loadout.Count);
            Assert.Equal(3, medic.PartyNotes.Count);
            Assert.Equal(2, medic.Stash.Count);
            Assert.Equal(3, medic.Pickups.Count);
            Assert.Single(medic.Jumps);
            Assert.Equal(1, medic.Incapacitations);
            Assert.Equal(1, medic.Deaths);
            Assert.Single(medic.Respawns);
            Assert.Equal(["after-death", "heal"], medic.MedicalBeds.Select(b => b.Kind));

            Assert.Equal(3, sessions.Count);
            Assert.Equal(3, sessions.Values.Sum(s => s.Jumps.Count));
            Assert.Equal(12, sessions.Values.Sum(s => s.PartyNotes.Count));
            Assert.Equal(29, sessions.Values.Sum(s => s.FleetSize));
        });
    }

    private static void WithScenario(string name, Action<string, SessionSummary> assert)
    {
        var path = Path.Combine(Path.GetTempPath(), $"qw-scenario-{Guid.NewGuid():N}.log");

        try
        {
            var scenario = Assert.IsType<ScenarioDefinition>(ScenarioCatalogue.Find(name));

            using (var writer = new LogWriter(path))
                ScenarioRunner.Run(writer, scenario, Start);

            assert(path, LogLibrary.BuildSession(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WithMultiClientScenario(
        Action<IReadOnlyDictionary<string, SessionSummary>, IReadOnlyDictionary<string, int>> assert)
    {
        var root = Path.Combine(Path.GetTempPath(), $"qw-multi-scenario-{Guid.NewGuid():N}");
        var scenario = Assert.IsType<MultiClientScenarioDefinition>(
            MultiClientScenarioCatalogue.Find("org-activity"));
        var logs = scenario.Pilots.ToDictionary(
            pilot => pilot.Key,
            pilot => Path.Combine(root, pilot.Key, "Game.log"),
            StringComparer.OrdinalIgnoreCase);
        var writers = logs.ToDictionary(
            pair => pair.Key,
            pair => new LogWriter(pair.Value),
            StringComparer.OrdinalIgnoreCase);

        try
        {
            MultiClientScenarioRunner.Run(scenario, writers, Start);
            foreach (var writer in writers.Values)
                writer.Dispose();

            var sessions = new Dictionary<string, SessionSummary>(StringComparer.OrdinalIgnoreCase);
            var unmatched = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in logs)
            {
                var parser = new LogEventParser();
                Assert.NotEmpty(LogFileReader.ReadEvents(pair.Value, parser));
                sessions.Add(pair.Key, LogLibrary.BuildSession(pair.Value));
                unmatched.Add(pair.Key, parser.UnmatchedKnownTags);
            }

            assert(sessions, unmatched);
        }
        finally
        {
            foreach (var writer in writers.Values)
                writer.Dispose();

            Directory.Delete(root, recursive: true);
        }
    }
}
