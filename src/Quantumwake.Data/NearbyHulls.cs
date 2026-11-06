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
    private readonly Lock _gate = new();

    /// <summary>Takes one raw log line; anything but an ATC reset is ignored.</summary>
    public void Line(string line, DateTimeOffset at)
    {
        if (!line.Contains("ShipATCDataManager", StringComparison.Ordinal))
            return;

        if (AtcLine().Match(line) is not { Success: true } m)
            return;

        lock (_gate) _seen[m.Groups["cls"].Value] = at;
    }

    /// <summary>The classes seen within twenty minutes of <paramref name="now"/>.</summary>
    public IReadOnlySet<string> Classes(DateTimeOffset now)
    {
        lock (_gate)
            return _seen.Where(kv => now - kv.Value <= Recent).Select(kv => kv.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"ATC '(?<cls>[A-Za-z0-9]+_[A-Za-z0-9_]+?)_ShipATCDataManager_\d+'", RegexOptions.CultureInvariant)]
    private static partial Regex AtcLine();
}
