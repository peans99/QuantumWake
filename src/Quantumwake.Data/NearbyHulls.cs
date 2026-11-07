using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>
/// The ship classes the game has streamed in around the pilot lately, from
/// raw <c>Game.log</c> lines - a clue for naming a target the panel calls
/// UNKNOWN, never a list of what is being fought.
/// </summary>
/// <remarks>
/// <para>
/// Every ship that streams in near the client gets an air-traffic-control
/// data manager, and its reset is logged with the class in its name:
/// <c>ATC 'VNCL_Blade_ShipATCDataManager_917287752642'</c>. The 2026-11-05
/// session logged ANVL_Paladin 33 times, VNCL_Blade 21, VNCL_Scythe 18 and
/// VNCL_Glaive 17 - and no Origin 85X or Mustang, the hulls that share the
/// Blade's and Glaive's signatures. So the list rules hulls in, not out:
/// a hull the log saw is more likely, one it did not is still possible.
/// </para>
/// <para>
/// NPC ships say more, because they are named as NPCs. The game logs an AI
/// ship's entity by its class - <c>CRUS_Starfighter_Ion_PU_AI_CRIM_916483000013</c>,
/// <c>VNCL_Glaive_PU_AI_VAN_…</c>, <c>RSI_Perseus_AI_Super_Crim_…</c> - in physics
/// grid notices, hit-effect budgets and the wreckage of a destroyed one. The
/// same session named nine NPC hulls that way, every one of them a ship
/// fought in the recording; a player's ship never carries <c>_AI_</c>. Those
/// are what the mission spawned, which no game file says: only 48 of the 184
/// spawn-wave records are named by any mission script, and none of the
/// bounty or strike-group waves (see <c>GameEncounters</c>). They are kept
/// apart, in <see cref="Npcs"/>, and weigh more than a hull merely streamed in.
/// </para>
/// <para>
/// Kept in memory only, and only for the last twenty minutes: a hull that
/// streamed in at the start of an evening says nothing about this fight.
/// Read off the raw line rather than through the parser because nothing of
/// it is stored, and a session summary has no use for it.
/// </para>
/// </remarks>
public sealed partial class NearbyHulls
{
    private static readonly TimeSpan Recent = TimeSpan.FromMinutes(20);

    private readonly Dictionary<string, DateTimeOffset> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _npcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    /// <summary>Takes one raw log line; anything but an ATC reset or a line naming an AI ship is ignored.</summary>
    public void Line(string line, DateTimeOffset at)
    {
        if (line.Contains("_AI_", StringComparison.Ordinal))
        {
            foreach (Match npc in NpcShip().Matches(line))
            {
                var cls = npc.Groups["cls"].Value;
                lock (_gate)
                {
                    _npcs[cls] = at;
                    _seen[cls] = at;
                }
            }
        }

        if (!line.Contains("ShipATCDataManager", StringComparison.Ordinal))
            return;

        if (AtcLine().Match(line) is not { Success: true } m)
            return;

        lock (_gate) _seen[m.Groups["cls"].Value] = at;
    }

    /// <summary>The NPC ship classes named within twenty minutes of <paramref name="now"/>: <c>CRUS_Starfighter_Ion</c>.</summary>
    public IReadOnlySet<string> Npcs(DateTimeOffset now)
    {
        lock (_gate)
            return _npcs.Where(kv => now - kv.Value <= Recent).Select(kv => kv.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The classes seen within twenty minutes of <paramref name="now"/>.</summary>
    public IReadOnlySet<string> Classes(DateTimeOffset now)
    {
        lock (_gate)
            return _seen.Where(kv => now - kv.Value <= Recent).Select(kv => kv.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // A maker's code in capitals, the hull's words, then _AI_ or _PU_AI_: the
    // class is everything before. Not preceded by a letter or digit, so the
    // CRUS in "SCItem_Debris_916483015360_CRUS_Starfighter_Ion_PU_AI_CRIM" is
    // where it starts, and "PU_Pilots-Human-Criminal" (two capitals) never does.
    [GeneratedRegex(@"(?<![A-Za-z0-9])(?<cls>[A-Z]{3,4}_[A-Za-z0-9]+(?:_[A-Za-z0-9]+)*?)_(?:PU_)?AI_", RegexOptions.CultureInvariant)]
    private static partial Regex NpcShip();

    [GeneratedRegex(@"ATC '(?<cls>[A-Za-z0-9]+_[A-Za-z0-9_]+?)_ShipATCDataManager_\d+'", RegexOptions.CultureInvariant)]
    private static partial Regex AtcLine();
}
