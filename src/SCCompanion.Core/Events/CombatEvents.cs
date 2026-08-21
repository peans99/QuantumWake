namespace SCCompanion.Core.Events;

/// <summary>How a death should be presented.</summary>
public enum KillKind
{
    Unknown,
    Suicide,
    /// <summary>The local player was killed by an NPC.</summary>
    Death,
    /// <summary>The local player was killed by another player.</summary>
    PvpDeath,
    /// <summary>The player killed an NPC.</summary>
    PveKill,
    /// <summary>The player killed another player.</summary>
    PvpKill,
    /// <summary>Neither party is the local player.</summary>
    Bystander
}

/// <summary>
/// A death parsed from <c>&lt;Actor Death&gt;</c>.
/// </summary>
/// <remarks>
/// <b>Dormant on current game versions.</b> Star Citizen 4.9 does not emit this
/// event: a scan of 403 MB across 144 log backups found zero occurrences, and
/// CIG has been narrowing combat logging since 4.0.2. The parser is implemented
/// from the archived format so the feature lights up automatically if the event
/// ever returns. See docs/findings.md.
/// </remarks>
public sealed record ActorDeathEvent(
    DateTimeOffset Timestamp,
    string Victim,
    string VictimId,
    string Zone,
    string Killer,
    string KillerId,
    string Weapon,
    string WeaponClass,
    string DamageType,
    double DirectionX,
    double DirectionY,
    double DirectionZ,
    KillKind Classification) : GameEvent(Timestamp)
{
    public override string Kind => "combat.death";

    public bool IsFps => DamageType.Equals("Bullet", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// One item recorded on the player's corpse for recovery.
/// </summary>
/// <remarks>
/// <para>
/// The only reliable death signal left in SC 4.9. <c>&lt;Actor Death&gt;</c> is
/// gone and an <c>Incapacitated</c> HUD notification is not always raised - a
/// session with three confirmed deaths logged zero of them - but every death
/// produces a burst of these as the game records what was being carried:
/// </para>
/// <code>
/// &lt;Adding non kept item [CSCActorCorpseUtils::PopulateItemPortForItemRecoveryEntitlement]&gt;
///   Item 'body_01_noMagicPocket_200000000218 - Class(body_01_noMagicPocket) - ...',
///   Recorded data is: Port Name 'Body_ItemPort', Class ...
/// </code>
/// <para>
/// One line is emitted per carried item, so they arrive in tight clusters -
/// 40, 38 and 19 lines for three separate deaths. Consumers must group by time
/// rather than counting lines.
/// </para>
/// </remarks>
public sealed record CorpseItemEvent(
    DateTimeOffset Timestamp,
    string ItemClass,
    string Port) : GameEvent(Timestamp)
{
    public override string Kind => "combat.corpse";
}

/// <summary>Destruction level reached by a vehicle.</summary>
public enum DestroyLevel
{
    Intact = 0,
    /// <summary>Disabled but not gone - a "soft death".</summary>
    SoftDeath = 1,
    Destroyed = 2
}

/// <summary>
/// A vehicle destruction parsed from <c>&lt;Vehicle Destruction&gt;</c>.
/// Dormant for the same reason as <see cref="ActorDeathEvent"/>.
/// </summary>
public sealed record VehicleDestructionEvent(
    DateTimeOffset Timestamp,
    string Vehicle,
    string VehicleId,
    string Zone,
    string Driver,
    string Attacker,
    DestroyLevel From,
    DestroyLevel To,
    string Cause) : GameEvent(Timestamp)
{
    public override string Kind => "combat.vehicle";
}

/// <summary>
/// Decides whether an entity name refers to an NPC.
/// </summary>
/// <remarks>
/// NPC entity names embed archetype markers, while player handles do not. The
/// markers below are drawn from the naming seen in archived logs and the
/// heuristics used by StarLogs. Two shape-based fallbacks catch archetypes not
/// listed: names are far longer than any handle, and use several hyphens.
/// </remarks>
public static class NpcNames
{
    private static readonly string[] Markers =
    [
        "PU_Pilots", "PU_", "AI_CRIM", "AI_", "_NPC_", "NPC_Archetypes",
        "Criminal-Pilot", "Security-", "Pirate-",
        "-Pilot_Light_", "-Pilot_Medium_", "-Pilot_Heavy_",
        "Kopion_", "Quasigrazer", "vlk_"
    ];

    /// <summary>Longest plausible player handle; anything longer is an entity id.</summary>
    private const int MaxHandleLength = 40;

    private const int HyphenThreshold = 3;

    public static bool IsNpc(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        foreach (var marker in Markers)
        {
            if (name.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (name.Length > MaxHandleLength)
            return true;

        return name.Count(c => c == '-') >= HyphenThreshold;
    }

    /// <summary>Classifies a death relative to the local player.</summary>
    public static KillKind Classify(string victim, string killer, string? localHandle)
    {
        if (string.Equals(victim, killer, StringComparison.OrdinalIgnoreCase))
            return KillKind.Suicide;

        var victimIsPlayer = !IsNpc(victim);
        var killerIsPlayer = !IsNpc(killer);

        var victimIsLocal = localHandle is not null
            && victim.Equals(localHandle, StringComparison.OrdinalIgnoreCase);

        var killerIsLocal = localHandle is not null
            && killer.Equals(localHandle, StringComparison.OrdinalIgnoreCase);

        if (victimIsLocal)
            return killerIsPlayer ? KillKind.PvpDeath : KillKind.Death;

        if (killerIsLocal)
            return victimIsPlayer ? KillKind.PvpKill : KillKind.PveKill;

        return KillKind.Bystander;
    }
}
