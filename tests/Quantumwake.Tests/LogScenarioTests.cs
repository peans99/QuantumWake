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
        "spending",
        "medical-respawn",
        "crew-flight",
        "contract-complete",
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
            Assert.Equal(2, session.Trades.Count);
            Assert.Single(session.Purchases);
            Assert.Equal(1, session.Incapacitations);
            Assert.Single(session.Respawns);
            Assert.Single(session.MedicalBeds);
            Assert.Equal(4, session.PartyNotes.Count);
            Assert.Equal(2, Assert.Single(session.Ships).Sorties);
            Assert.Equal(2, session.Jumps.Count);
            Assert.Equal(1, session.ContractsCompleted);
            Assert.Single(session.Blueprints);
            Assert.Equal(1, session.Kills);
            Assert.Equal(1, session.Deaths);
            Assert.Single(session.Timeline, entry => entry.Kind == "vehicle-destroyed");
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
}
