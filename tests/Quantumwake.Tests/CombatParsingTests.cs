using Quantumwake.Core.Events;
using Quantumwake.Core.Logging;
using Quantumwake.Core.Parsing;
using Quantumwake.Core.State;

namespace Quantumwake.Tests;

/// <summary>
/// The dormant combat parser.
/// </summary>
/// <remarks>
/// Star Citizen 4.9 emits none of these events - a scan of 403 MB across 144
/// backups found zero. The parser exists so the feature revives with no rework
/// if CIG restores them, and these tests are what guarantee that. Fixtures use
/// the archived format documented in docs/log-format-reference.md.
/// </remarks>
public class CombatParsingTests
{
    private const string DeathLine =
        "<2026-08-20T02:00:00.000Z> [Notice] <Actor Death> CActor::Kill: 'VictimGuy' [12345] " +
        "in zone 'Stanton_Yela' killed by 'KillerGuy' [67890] using 'behr_lmg_ballistic_01' " +
        "[Class behr_lmg_ballistic_01] with damage type 'Bullet' " +
        "from direction x: 0.512, y: -0.234, z: 0.100 [Team_ActorFeatures][Actor]";

    private static GameEvent? Parse(string raw, string? loginFirst = null)
    {
        var parser = new LogEventParser();

        if (loginFirst is not null)
        {
            var login = $"<2026-08-20T01:00:00.000Z> [Notice] <Legacy login response> " +
                        $"[CIG-net] User Login Success - Handle[{loginFirst}] - Time[1]";
            LogEnvelope.TryParse(login, out var loginLine);
            parser.Parse(loginLine!);
        }

        Assert.True(LogEnvelope.TryParse(raw, out var line));
        return parser.Parse(line);
    }

    [Fact]
    public void Parses_the_archived_actor_death_format()
    {
        var death = Assert.IsType<ActorDeathEvent>(Parse(DeathLine));

        Assert.Equal("VictimGuy", death.Victim);
        Assert.Equal("KillerGuy", death.Killer);
        Assert.Equal("Stanton_Yela", death.Zone);
        Assert.Equal("behr_lmg_ballistic_01", death.Weapon);
        Assert.Equal("Bullet", death.DamageType);
        Assert.True(death.IsFps);
        Assert.Equal(0.512, death.DirectionX, 3);
        Assert.Equal(-0.234, death.DirectionY, 3);
    }

    [Fact]
    public void Classifies_a_kill_by_the_local_player()
    {
        var death = Assert.IsType<ActorDeathEvent>(Parse(DeathLine, loginFirst: "KillerGuy"));
        Assert.Equal(KillKind.PvpKill, death.Classification);
    }

    [Fact]
    public void Classifies_a_death_of_the_local_player()
    {
        var death = Assert.IsType<ActorDeathEvent>(Parse(DeathLine, loginFirst: "VictimGuy"));
        Assert.Equal(KillKind.PvpDeath, death.Classification);
    }

    [Fact]
    public void Classifies_unrelated_deaths_as_bystander()
    {
        var death = Assert.IsType<ActorDeathEvent>(Parse(DeathLine, loginFirst: "SomeoneElse"));
        Assert.Equal(KillKind.Bystander, death.Classification);
    }

    [Fact]
    public void Classifies_suicide()
    {
        var line = DeathLine.Replace("'KillerGuy'", "'VictimGuy'");
        var death = Assert.IsType<ActorDeathEvent>(Parse(line, loginFirst: "VictimGuy"));

        Assert.Equal(KillKind.Suicide, death.Classification);
    }

    [Fact]
    public void Parses_the_archived_vehicle_destruction_format()
    {
        var raw =
            "<2026-08-20T02:05:00.000Z> [Notice] <Vehicle Destruction> " +
            "CVehicle::OnAdvanceDestroyLevel: Vehicle 'ANVL_Paladin_6763231335005' [6763231335005] " +
            "in zone 'Stanton_Yela' [pos x: 1.0, y: 2.0, z: 3.0 vel x: 0.0, y: 0.0, z: 0.0] " +
            "driven by 'PilotGuy' [999] advanced from destroy level 0 to 1 " +
            "caused by 'AttackerGuy' [888] with 'Combat' [Team_VehicleFeatures][Vehicle]";

        var destruction = Assert.IsType<VehicleDestructionEvent>(Parse(raw));

        Assert.Equal("ANVL_Paladin_6763231335005", destruction.Vehicle);
        Assert.Equal("PilotGuy", destruction.Driver);
        Assert.Equal("AttackerGuy", destruction.Attacker);
        Assert.Equal(DestroyLevel.Intact, destruction.From);
        Assert.Equal(DestroyLevel.SoftDeath, destruction.To);
        Assert.Equal("Combat", destruction.Cause);
    }

    [Theory]
    [InlineData("PU_Pilots-Human-NPC_Pilot_Criminal_Gunner", true)]
    [InlineData("AI_CRIM_Gunner_01", true)]
    [InlineData("Kopion_Ranger_01", true)]
    [InlineData("NPC_Archetypes-Human-Bartender", true)]
    [InlineData("Security-Pilot-Light-01", true)]
    [InlineData("nekron", false)]
    [InlineData("SomePlayer99", false)]
    public void Detects_npc_entity_names(string name, bool expected)
    {
        Assert.Equal(expected, NpcNames.IsNpc(name));
    }

    [Fact]
    public void Very_long_entity_names_are_treated_as_npcs()
    {
        Assert.True(NpcNames.IsNpc(new string('x', 41)));
        Assert.False(NpcNames.IsNpc(new string('x', 20)));
    }

    [Fact]
    public void Session_counts_kills_and_deaths_when_events_exist()
    {
        var t0 = new DateTimeOffset(2026, 8, 20, 20, 0, 0, TimeSpan.Zero);
        var builder = new SessionBuilder("test.log");

        builder.Add(new ActorDeathEvent(t0, "Bandit", "1", "z", "nekron", "2", "gun", "c", "Bullet",
            0, 0, 0, KillKind.PveKill));
        builder.Add(new ActorDeathEvent(t0.AddMinutes(1), "nekron", "2", "z", "Bandit", "1", "gun", "c", "Bullet",
            0, 0, 0, KillKind.Death));

        var summary = builder.Build();

        Assert.Equal(1, summary.Kills);
        Assert.Equal(1, summary.Deaths);
        Assert.Contains(summary.Timeline, t => t.Kind == "kill");
        Assert.Contains(summary.Timeline, t => t.Kind == "death");
    }

    /// <summary>
    /// The point of the whole exercise: the parser must be silent, not broken,
    /// on logs that contain no combat at all.
    /// </summary>
    [Fact]
    public void Reports_nothing_on_logs_without_combat_events()
    {
        var summary = new SessionBuilder("test.log");
        summary.Add(new LoginEvent(DateTimeOffset.UnixEpoch, "nekron"));

        var built = summary.Build();

        Assert.Equal(0, built.Kills);
        Assert.Equal(0, built.Deaths);
    }
}
