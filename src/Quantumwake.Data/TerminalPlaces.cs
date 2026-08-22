namespace Quantumwake.Data;

/// <summary>
/// Resolves a UEX terminal name to the map's own place.
/// </summary>
/// <remarks>
/// <para>
/// The two naming schemes do not agree and never will: UEX names the counter
/// ("Admin - Port Tressler", "TDD, Area 18"), the game names the place
/// ("Port Tressler", "Area18"). Every feature that puts a price on the map, or
/// a terminal on a flight plan, needs the join, and each one guessing at it
/// separately is how the map ends up disagreeing with the panel beside it.
/// </para>
/// <para>
/// The rule is deliberately narrow: a terminal belongs to the place whose name
/// its own name contains, longest match wins, and an ambiguous or short match
/// is no match at all. Being wrong here is worse than being empty - a stop on
/// the wrong dot sends someone to the wrong moon, while a stop with no dot is
/// still a stop, with its name and its notes intact.
/// </para>
/// </remarks>
public sealed class TerminalPlaces
{
    /// <summary>Shorter than this and a name matches half the system.</summary>
    private const int MinimumMatch = 5;

    private readonly List<(string Compact, string RawId, string Name)> _places;
    private readonly Dictionary<string, (string RawId, string Name)?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public TerminalPlaces(IEnumerable<PlaceTotal> atlas)
    {
        _places = [.. atlas
            .Select(p => (Compact: Compact(p.Name), p.RawId, p.Name))
            .Where(p => p.Compact.Length >= MinimumMatch)

            // Longest first, so "Area 061" wins over "Area 06" for a terminal
            // whose name contains both.
            .OrderByDescending(p => p.Compact.Length)];
    }

    /// <summary>The place a terminal sits at, or null when nothing fits.</summary>
    public (string RawId, string Name)? Resolve(string? terminal)
    {
        if (string.IsNullOrWhiteSpace(terminal))
            return null;

        lock (_gate)
        {
            if (_cache.TryGetValue(terminal, out var known))
                return known;

            var answer = Match(terminal);
            _cache[terminal] = answer;
            return answer;
        }
    }

    /// <summary>The place id alone, empty when nothing fits.</summary>
    public string IdFor(string? terminal) => Resolve(terminal)?.RawId ?? string.Empty;

    /// <remarks>
    /// Three passes, in falling order of confidence. Containment runs both ways
    /// because the naming goes both ways: "Admin - Port Tressler" carries the
    /// whole place name, while "Seraphim" is the place name with the rest of
    /// "Seraphim Station" left off. Each pass insists on a single answer, so an
    /// ambiguous name resolves to nothing rather than to a guess.
    /// </remarks>
    private (string RawId, string Name)? Match(string terminal)
    {
        var haystack = Compact(terminal);
        if (haystack.Length < MinimumMatch)
            return null;

        var exact = _places
            .Where(p => string.Equals(p.Compact, haystack, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exact.Count == 1)
            return (exact[0].RawId, exact[0].Name);

        // The place named inside the terminal: longest wins, so a terminal
        // holding both "Area 06" and "Area 061" resolves to the longer.
        var named = _places
            .Where(p => haystack.Contains(p.Compact, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (named.Count > 0)
        {
            var longest = named[0].Compact.Length;
            var best = named.Where(p => p.Compact.Length == longest).ToList();

            if (best.Count == 1)
                return (best[0].RawId, best[0].Name);

            return null;
        }

        // The terminal named inside the place: "Seraphim" is where "Seraphim
        // Station" is. Only ever accepted when exactly one place answers to it.
        var abbreviated = _places
            .Where(p => p.Compact.Contains(haystack, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return abbreviated.Count == 1 ? (abbreviated[0].RawId, abbreviated[0].Name) : null;
    }

    private static string Compact(string value) =>
        new([.. value.Where(char.IsLetterOrDigit)]);
}
