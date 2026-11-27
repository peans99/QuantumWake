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
public sealed partial class TerminalPlaces
{
    /// <summary>Shorter than this and a name matches half the system.</summary>
    private const int MinimumMatch = 5;

    private readonly List<(string Compact, PlaceTotal Place)> _places;

    /// <summary>
    /// Places whose name opens with a station code, by that code.
    /// </summary>
    /// <remarks>
    /// The shop chains name their counters after the code alone - "Platinum
    /// HUR-L5", "Dumper's CRU-L4" - while the atlas carries the full name,
    /// "HUR-L5 High Course Station". Neither name contains the other, so
    /// every item shop on a rest stop resolved to nothing and could not be put
    /// on the map or into a plan. A code is safe to match on because it is
    /// never a word: only a leading token carrying a digit counts, which keeps
    /// "Port Tressler" from claiming every terminal with "port" in its name.
    /// </remarks>
    private readonly Dictionary<string, PlaceTotal?> _codes;

    /// <summary>
    /// Each word of five letters or more that belongs to one place's name and
    /// no other's, by that word (1.2.61).
    /// </summary>
    /// <remarks>
    /// The shop counters shorten the station: "FPS Armor Tressler" is at Port
    /// Tressler, "FPS Armor Everus" at Everus Harbor, "Gear Up Orbituary" at
    /// Orbituary. Neither name contains the other, so 249 of the 589 counters
    /// in this install's price data stood nowhere, and a list sorted nearest
    /// first sent the pilot past Everus Harbor to New Babbage, 38 Gm away. A
    /// word only counts when no other place uses it, so "Station", "Harbor"
    /// and "Outpost" never match anything.
    /// </remarks>
    private readonly Dictionary<string, PlaceTotal> _words;
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

        // A code shared by two places is no use, so it is kept as null rather
        // than dropped - a lookup that finds it knows to stop.
        _codes = new Dictionary<string, PlaceTotal?>(StringComparer.OrdinalIgnoreCase);

        foreach (var place in atlas)
        {
            if (LeadingCode(place.Name) is not { } code)
                continue;

            _codes[code] = _codes.ContainsKey(code) ? null : place;
        }

        var owners = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var byId = new Dictionary<string, PlaceTotal>(StringComparer.OrdinalIgnoreCase);
        foreach (var place in atlas)
        {
            byId.TryAdd(place.RawId, place);
            foreach (var word in Words(place.Name))
            {
                if (!owners.TryGetValue(word, out var ids))
                    owners[word] = ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                ids.Add(place.RawId);
            }
        }

        _words = owners
            .Where(o => o.Value.Count == 1)
            .ToDictionary(o => o.Key, o => byId[o.Value.First()], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Words that say what kind of place it is, not which.</summary>
    private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
    {
        "Field", "Fields", "Supplies", "Station", "Point", "Harbor", "Outpost", "Gateway",
        "Depot", "Plaza", "Market", "Store", "Center", "Centre", "Circle", "Discovery",
    };

    [System.Text.RegularExpressions.GeneratedRegex(@"\([^)]*\)")]
    private static partial System.Text.RegularExpressions.Regex Brackets();

    /// <summary>The words of a name worth matching on: five letters or more, no digits.</summary>
    private static IEnumerable<string> Words(string name) =>
        name.Split([' ', ',', '-', '(', ')', '/', '\'', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Select(Compact)
            .Where(w => w.Length >= MinimumMatch && !w.Any(char.IsDigit))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>The station code a place name opens with, or null.</summary>
    private static string? LeadingCode(string name)
    {
        var first = Compact(name.Split(' ')[0]);

        return first.Length >= 4 && first.Any(char.IsDigit) ? first : null;
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
    /// <summary>
    /// Counters whose name carries no place at all, by the place they stand in.
    /// </summary>
    /// <remarks>
    /// Kept to the ones a pilot is sent to. Astro Armada is Area18's ship
    /// showroom and the only counter UEX lists 68 hulls at besides New Deal;
    /// with nothing in its name to match, every ship bought there was a stop
    /// the map could not draw once ships went on a shopping list (1.2.69). The
    /// alias still has to resolve through the atlas, so an install whose map
    /// lacks Area18 places it nowhere rather than somewhere wrong.
    /// </remarks>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Astro Armada"] = "Area18",
    };

    private PlaceTotal? Match(string terminal)
    {
        if (Aliases.TryGetValue(terminal.Trim(), out var alias))
            terminal = alias;

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

        if (abbreviated.Count == 1)
            return abbreviated[0].Place;

        // Last, the station code the shop counters use: "Platinum HUR-L5" is
        // at "HUR-L5 High Course Station".
        foreach (var (code, place) in _codes)
            if (place is not null && haystack.Contains(code, StringComparison.OrdinalIgnoreCase))
                return place;

        // Then the terminal's last word, when only one place's name has it:
        // the chains name a counter "<shop> <station>". Any word at all also
        // matched "Tools & Supplies Levski" to Rod's Fuel 'N Supplies and
        // "Ellroy's Green Circle" to Green Glade Station; the last word, less
        // a bracketed system and a few words that name a kind of place,
        // matched 61 more counters on this install, every one right.
        var last = Words(Brackets().Replace(terminal, " ")).LastOrDefault();
        return last is not null && !Generic.Contains(last) && terminal.Trim().Contains(' ')
            ? _words.GetValueOrDefault(last)
            : null;
    }

    private static string Compact(string value) =>
        new([.. value.Where(char.IsLetterOrDigit)]);
}
