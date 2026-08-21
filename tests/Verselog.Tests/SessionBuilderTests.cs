using Verselog.Core.Events;
using Verselog.Core.State;

namespace Verselog.Tests;

public class ContractNameParserTests
{
    [Theory]
    [InlineData("Covalex_Stanton_VeryHard_RecoverCargo", "Covalex", "Stanton", "Very Hard", "Recover Cargo")]
    [InlineData("Ling_Stanton_VeryEasy_RecoverCargo", "Ling", "Stanton", "Very Easy", "Recover Cargo")]
    [InlineData("RedWind_Stanton_Easy_RecoverCargo", "Red Wind", "Stanton", "Easy", "Recover Cargo")]
    public void Decomposes_standard_contract_names(
        string raw, string issuer, string system, string difficulty, string type)
    {
        var parsed = ContractNameParser.Parse(raw);

        Assert.Equal(issuer, parsed.Issuer);
        Assert.Equal(system, parsed.System);
        Assert.Equal(difficulty, parsed.Difficulty);
        Assert.Equal(type, parsed.Type);
    }

    /// <summary>Rank and numeric variant suffixes carry no display value.</summary>
    [Fact]
    public void Drops_rank_and_variant_suffixes()
    {
        var parsed = ContractNameParser.Parse("FTL_Courier_Stanton_AmmoCrate_Rank0_2");

        Assert.Equal("FTL", parsed.Issuer);
        Assert.Equal("Stanton", parsed.System);
        Assert.DoesNotContain("Rank", parsed.Type);
        Assert.DoesNotContain("2", parsed.Type);
    }

    [Fact]
    public void Handles_names_with_no_system_or_difficulty()
    {
        var parsed = ContractNameParser.Parse("GillysPilotSchool_Mission06_2");

        Assert.Equal("Gillys Pilot School", parsed.Issuer);
        Assert.Null(parsed.System);
        Assert.Null(parsed.Difficulty);
    }

    [Fact]
    public void Recognises_interstellar_as_a_scope()
    {
        var parsed = ContractNameParser.Parse(
            "HaulCargo_AToB_Interstellar_Bulk_DistSp_Dia_FresFoo_Gol_Aphor");

        Assert.Equal("Interstellar", parsed.System);
    }

    [Fact]
    public void Handles_empty_input()
    {
        Assert.Equal("Unknown", ContractNameParser.Parse("").Issuer);
    }
}

public class SessionBuilderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 20, 0, 0, TimeSpan.Zero);

    private static SessionBuilder Build(params GameEvent[] events)
    {
        var builder = new SessionBuilder("Game Build(1) 20 Aug 26 (20 00 00).log");
        foreach (var ev in events)
            builder.Add(ev);

        return builder;
    }

    [Fact]
    public void Captures_identity_and_version()
    {
        var summary = Build(
            new SessionStartEvent(T0, "Build(12344265)", "4.9.188.23497"),
            new LoginEvent(T0.AddSeconds(10), "nekron"),
            new CharacterEvent(T0.AddSeconds(11), "nekron", "204721322607", "51915", "STATE_CURRENT")
        ).Build();

        Assert.Equal("nekron", summary.Handle);
        Assert.Equal("204721322607", summary.Geid);
        Assert.Equal("4.9.188.23497", summary.GameVersion);
    }

    /// <summary>
    /// The core playtime improvement: menu time must not count as play. Every
    /// existing tool that sums last-minus-first gets this wrong.
    /// </summary>
    [Fact]
    public void Separates_in_game_time_from_menu_time()
    {
        var summary = Build(
            new LoadingScreenEvent(T0, "Frontend_Main", "SC_Frontend", 3.4),
            new LoadingScreenEvent(T0.AddMinutes(10), "PU", "SC_Default", 20.0),
            new LoadingScreenEvent(T0.AddMinutes(70), "Frontend_Main", "SC_Frontend", 2.0),
            new ClientSpawnedEvent(T0.AddMinutes(80))
        ).Build();

        Assert.Equal(TimeSpan.FromMinutes(60), summary.InGameDuration);
        Assert.Equal(TimeSpan.FromMinutes(20), summary.MenuDuration);
        Assert.Equal(TimeSpan.FromMinutes(80), summary.Duration);
    }

    /// <summary>Retained for the day CIG restores a boarding event.</summary>
    [Fact]
    public void Uses_exact_time_when_a_boarding_event_is_present()
    {
        var summary = Build(
            new VehicleControlEvent(T0, "DRAK_Clipper_1", "Clipper", "DRAK", "1", SeatChange.Entered),
            new VehicleControlEvent(T0.AddMinutes(30), "DRAK_Clipper_1", "Clipper", "DRAK", "1", SeatChange.Left)
        ).Build();

        var ship = Assert.Single(summary.Ships);
        Assert.Equal("Clipper", ship.Model);
        Assert.Equal("DRAK", ship.Manufacturer);
        Assert.Equal(TimeSpan.FromMinutes(30), ship.EstimatedTime);
        Assert.Equal(1, ship.Sorties);
    }

    /// <summary>
    /// SC 4.9 emits only ClearDriver - 497 of 497 vehicle events in the sample
    /// set. With no anchor to measure from, no time may be invented.
    /// </summary>
    [Fact]
    public void Release_with_no_anchor_counts_a_sortie_without_inventing_time()
    {
        var summary = Build(
            new VehicleControlEvent(T0, "RSI_Aurora_Mk2_9", "Aurora_Mk2", "RSI", "9", SeatChange.Left)
        ).Build();

        var ship = Assert.Single(summary.Ships);
        Assert.Equal(TimeSpan.Zero, ship.EstimatedTime);
        Assert.Equal(1, ship.Sorties);
    }

    /// <summary>Departing a known location gives a usable anchor to estimate from.</summary>
    [Fact]
    public void Estimates_flight_time_from_the_last_known_stop()
    {
        var summary = Build(
            new LocationInventoryEvent(T0, "nekron", "RR_MIC_LEO"),
            new VehicleControlEvent(T0.AddMinutes(12), "DRAK_Clipper_1", "Clipper", "DRAK", "1", SeatChange.Left)
        ).Build();

        Assert.Equal(TimeSpan.FromMinutes(12), Assert.Single(summary.Ships).EstimatedTime);
    }

    /// <summary>
    /// A long idle gap is not a four-hour sortie; the estimate is capped so one
    /// AFK stretch cannot dominate the totals.
    /// </summary>
    [Fact]
    public void Caps_the_flight_estimate_across_idle_gaps()
    {
        var summary = Build(
            new LocationInventoryEvent(T0, "nekron", "RR_MIC_LEO"),
            new VehicleControlEvent(T0.AddHours(9), "DRAK_Clipper_1", "Clipper", "DRAK", "1", SeatChange.Left)
        ).Build();

        Assert.Equal(TimeSpan.FromHours(2), Assert.Single(summary.Ships).EstimatedTime);
    }

    [Fact]
    public void Ranks_ships_by_flights_not_estimated_time()
    {
        var summary = Build(
            new LocationInventoryEvent(T0, "nekron", "RR_MIC_LEO"),
            new VehicleControlEvent(T0.AddHours(2), "RSI_Hermes_1", "Hermes", "RSI", "1", SeatChange.Left),
            new VehicleControlEvent(T0.AddHours(2).AddMinutes(5), "DRAK_Clipper_1", "Clipper", "DRAK", "1", SeatChange.Left),
            new VehicleControlEvent(T0.AddHours(2).AddMinutes(10), "DRAK_Clipper_1", "Clipper", "DRAK", "1", SeatChange.Left)
        ).Build();

        Assert.Equal("Clipper", summary.Ships[0].Model);
        Assert.Equal(2, summary.Ships[0].Sorties);
        Assert.Equal("DRAK Clipper", summary.PrimaryShip);
    }

    [Fact]
    public void Collapses_repeated_location_signals()
    {
        var summary = Build(
            new LocationInventoryEvent(T0, "nekron", "RR_MIC_LEO"),
            new LocationInventoryEvent(T0.AddMinutes(1), "nekron", "RR_MIC_LEO"),
            new LocationInventoryEvent(T0.AddMinutes(2), "nekron", "RR_MIC_LEO"),
            new LocationInventoryEvent(T0.AddMinutes(30), "nekron", "Stanton4_NewBabbage")
        ).Build();

        Assert.Equal(2, summary.Locations.Count);
        Assert.Equal("Port Tressler", summary.Locations[0].DisplayName);
        Assert.Equal("New Babbage", summary.Locations[1].DisplayName);
    }

    [Fact]
    public void Collapses_repeated_quantum_route_lines()
    {
        var summary = Build(
            new QuantumRouteEvent(T0, "Aurora_Mk2", "Gaslight", "rs_ext_pyro-stan_jp1"),
            new QuantumRouteEvent(T0.AddSeconds(1), "Aurora_Mk2", "Gaslight", "rs_ext_pyro-stan_jp1"),
            new QuantumRouteEvent(T0.AddMinutes(5), "Aurora_Mk2", null, "Stanton4_NewBabbage")
        ).Build();

        Assert.Equal(2, summary.Jumps.Count);
        Assert.Equal("Pyro – Stanton Jump Point", summary.Jumps[0].ToName);
    }

    /// <summary>
    /// ObjectContainer_RestStop names a category, not a place - the same string
    /// is used for every rest stop. Left as-is it merges unrelated destinations
    /// into one bucket, so the actual arrival must replace it.
    /// </summary>
    [Fact]
    public void Resolves_generic_destinations_to_the_place_actually_reached()
    {
        var summary = Build(
            new LocationInventoryEvent(T0, "nekron", "Stanton3_Area18"),
            new QuantumRouteEvent(T0.AddMinutes(2), "Clipper", "Area18", "ObjectContainer_RestStop"),
            new LocationInventoryEvent(T0.AddMinutes(9), "nekron", "RR_MIC_LEO")
        ).Build();

        var jump = Assert.Single(summary.Jumps);
        Assert.Equal("RR_MIC_LEO", jump.ToId);
        Assert.Equal("Port Tressler", jump.ToName);
        Assert.Contains(summary.Timeline, t => t.Kind == "quantum" && t.Text.Contains("Port Tressler"));
    }

    /// <summary>With no arrival signal there is nothing to resolve to; the
    /// generic label must survive rather than being guessed at.</summary>
    [Fact]
    public void Leaves_generic_destination_alone_when_arrival_is_unknown()
    {
        var summary = Build(
            new LocationInventoryEvent(T0, "nekron", "Stanton3_Area18"),
            new QuantumRouteEvent(T0.AddMinutes(2), "Clipper", "Area18", "ObjectContainer_RestStop")
        ).Build();

        Assert.Equal("Rest Stop", Assert.Single(summary.Jumps).ToName);
    }

    [Fact]
    public void Specific_destinations_are_never_rewritten()
    {
        var summary = Build(
            new QuantumRouteEvent(T0, "Clipper", null, "Stanton4_NewBabbage"),
            new LocationInventoryEvent(T0.AddMinutes(6), "nekron", "RR_MIC_LEO")
        ).Build();

        Assert.Equal("New Babbage", Assert.Single(summary.Jumps).ToName);
    }

    [Fact]
    public void Deduplicates_incapacitation_notifications()
    {
        var text = "Incapacitated: While incapacitated, ask others...";

        var summary = Build(
            new NotificationEvent(T0, text, "44", null),
            new NotificationEvent(T0.AddSeconds(1), text, "44", null),
            new NotificationEvent(T0.AddSeconds(2), text, "44", null),
            new NotificationEvent(T0.AddMinutes(20), text, "51", null)
        ).Build();

        Assert.Equal(2, summary.Incapacitations);
    }

    [Fact]
    public void Records_contracts_with_facets()
    {
        var summary = Build(
            new ContractEvent(T0, "m1", "Covalex_RecoverCargo", "Covalex_Stanton_VeryHard_RecoverCargo", "def1"),
            new ContractEvent(T0.AddSeconds(5), "m1", "Covalex_RecoverCargo", "Covalex_Stanton_VeryHard_RecoverCargo", "def1")
        ).Build();

        var contract = Assert.Single(summary.Contracts);
        Assert.Equal("Covalex", contract.Issuer);
        Assert.Equal("Very Hard", contract.Difficulty);
        Assert.Equal("Stanton", contract.System);
    }

    /// <summary>Routine "Nub destroyed" teardown is not a disconnect worth showing.</summary>
    [Fact]
    public void Ignores_routine_teardown_disconnects()
    {
        var summary = Build(
            new DisconnectEvent(T0, "30010", "Nub destroyed", false),
            new DisconnectEvent(T0.AddMinutes(1), "30011", "Timeout", true)
        ).Build();

        Assert.Equal(1, summary.Disconnects);
    }

    [Fact]
    public void Builds_an_ordered_timeline()
    {
        var summary = Build(
            new LocationInventoryEvent(T0.AddMinutes(5), "nekron", "RR_MIC_LEO"),
            new LoginEvent(T0, "nekron"),
            new QuantumRouteEvent(T0.AddMinutes(10), "Clipper", null, "Stanton4_NewBabbage")
        ).Build();

        Assert.Equal(3, summary.Timeline.Count);
        Assert.True(summary.Timeline[0].At <= summary.Timeline[1].At);
        Assert.True(summary.Timeline[1].At <= summary.Timeline[2].At);
        Assert.Equal("login", summary.Timeline[0].Kind);
    }

    [Fact]
    public void Reports_no_kills_on_current_game_versions()
    {
        var summary = Build(new LoginEvent(T0, "nekron")).Build();

        Assert.Equal(0, summary.Kills);
        Assert.Equal(0, summary.Deaths);
    }
}

public class LocationStateMachineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Starts_unknown()
    {
        Assert.Equal(LocationConfidence.None, new LocationStateMachine().State.Confidence);
    }

    [Fact]
    public void Inventory_request_gives_high_confidence()
    {
        var machine = new LocationStateMachine();
        machine.Apply(new LocationInventoryEvent(T0, "nekron", "RR_MIC_LEO"));

        Assert.Equal(LocationConfidence.High, machine.State.Confidence);
        Assert.Equal("Port Tressler", machine.State.Current!.DisplayName);
    }

    [Fact]
    public void Quantum_target_marks_travel_without_moving_yet()
    {
        var machine = new LocationStateMachine();
        machine.Apply(new LocationInventoryEvent(T0, "nekron", "RR_MIC_LEO"));
        machine.Apply(new QuantumTargetEvent(T0.AddMinutes(1), "Clipper", "Stanton4_NewBabbage"));

        Assert.True(machine.State.IsTravelling);
        Assert.Equal("New Babbage", machine.State.TravellingTo!.DisplayName);
        Assert.Equal("Port Tressler", machine.State.Current!.DisplayName);
    }

    [Fact]
    public void Arrival_clears_travel_and_records_a_quantum_change()
    {
        var machine = new LocationStateMachine();
        machine.Apply(new LocationInventoryEvent(T0, "nekron", "RR_MIC_LEO"));
        machine.Apply(new QuantumTargetEvent(T0.AddMinutes(1), "Clipper", "Stanton4_NewBabbage"));
        machine.Apply(new LocationInventoryEvent(T0.AddMinutes(9), "nekron", "Stanton4_NewBabbage"));

        Assert.False(machine.State.IsTravelling);
        Assert.Equal("New Babbage", machine.State.Current!.DisplayName);
        Assert.True(machine.History[^1].ViaQuantum);
    }

    [Fact]
    public void Spawning_lowers_confidence_but_keeps_last_known_location()
    {
        var machine = new LocationStateMachine();
        machine.Apply(new LocationInventoryEvent(T0, "nekron", "RR_MIC_LEO"));
        machine.Apply(new ClientSpawnedEvent(T0.AddMinutes(5)));

        Assert.Equal(LocationConfidence.Low, machine.State.Confidence);
        Assert.NotNull(machine.State.Current);
    }

    [Fact]
    public void Frontend_gamerules_means_not_in_game()
    {
        var machine = new LocationStateMachine();
        machine.Apply(new ContextEvent(T0, "Game", "megamap", "SC_Default", "s1"));
        Assert.True(machine.State.InGame);

        machine.Apply(new ContextEvent(T0.AddMinutes(1), "Game", "megamap", "SC_Frontend", "s1"));
        Assert.False(machine.State.InGame);
    }

    [Fact]
    public void Repeated_same_location_does_not_duplicate_history()
    {
        var machine = new LocationStateMachine();
        machine.Apply(new LocationInventoryEvent(T0, "nekron", "RR_MIC_LEO"));
        machine.Apply(new LocationInventoryEvent(T0.AddMinutes(1), "nekron", "RR_MIC_LEO"));

        Assert.Single(machine.History);
    }

    [Fact]
    public void Raises_changed_event()
    {
        var machine = new LocationStateMachine();
        LocationChange? seen = null;
        machine.Changed += c => seen = c;

        machine.Apply(new LocationInventoryEvent(T0, "nekron", "Stanton1_Lorville"));

        Assert.NotNull(seen);
        Assert.Equal("Lorville", seen.To.DisplayName);
    }
}
