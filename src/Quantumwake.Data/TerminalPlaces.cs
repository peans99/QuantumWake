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

    private readonly List<(string Compact, PlaceTotal Place)> _places;
    private readonly Dictionary<string, PlaceTotal?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public TerminalPlaces(IEnumerable<PlaceTotal> atlas)
    {
        _places = [.. atlas
            .Select(p => (Compact: Compact(p.Name), Place: p))
            .Where(p => p.Compact.Length >= MinimumMatch)

            // Longest first, so "Area 061" wins over "Area 06" for a terminal
            // whose name contains both.
            .OrderByDescending(p => p.Compact.Length)];
    }

    /// <summary>The place a terminal sits at, or null when nothing fits.</summary>
    public PlaceTotal? Resolve(string? terminal)
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

    /// <summary>
    /// How dangerous the space around a terminal is.
    /// </summary>
    /// <remarks>
    /// By system, because that is what can be known: nothing in the logs and
    /// nothing in the price feed describes threat, but which system a place is
    /// in decides whether UEE law reaches it at all. Stanton is policed and its
    /// stations carry armistice zones; Pyro and Nyx are not. A place the atlas
    /// cannot name gets "unknown" rather than a reassuring guess - being told
    /// somewhere is safe when nobody checked is the one answer worth refusing.
    /// </remarks>
    public string SecurityOf(string? terminal) => SecurityOfSystem(Resolve(terminal)?.System);

    public static string SecurityOfSystem(string? system) => system switch
    {
        null or "" => "unknown",
        "Stanton" => "monitored",
        _ => "lawless",
    };


    /// <remarks>
    /// Three passes, in falling order of confidence. Containment runs both ways
    /// because the naming goes both ways: "Admin - Port Tressler" carries the
    /// whole place name, while "Seraphim" is the place name with the rest of
    /// "Seraphim Station" left off. Each pass insists on a single answer, so an
    /// ambiguous name resolves to nothing rather than to a guess.
    /// </remarks>
    private PlaceTotal? Match(string terminal)
    {
        var haystack = Compact(terminal);
        if (haystack.Length < MinimumMatch)
            return null;

        var exact = _places
            .Where(p => string.Equals(p.Compact, haystack, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exact.Count == 1)
            return exact[0].Place;

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
                return best[0].Place;

            return null;
        }

        // The terminal named inside the place: "Seraphim" is where "Seraphim
        // Station" is. Only ever accepted when exactly one place answers to it.
        var abbreviated = _places
            .Where(p => p.Compact.Contains(haystack, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return abbreviated.Count == 1 ? abbreviated[0].Place : null;
    }

    private static string Compact(string value) =>
        new([.. value.Where(char.IsLetterOrDigit)]);
}
