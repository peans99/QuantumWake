using Quantumwake.Core.GameData;

namespace Quantumwake.Data;

/// <summary>A hull whose cross-section is near a figure the target panel showed.</summary>
/// <param name="Name">The hull as a pilot would say it.</param>
/// <param name="CrossSection">Its stock cross-section signature.</param>
/// <param name="Class">The game's class name, as the log writes it - VNCL_Blade.</param>
public sealed record SignatureMatch(string Name, double CrossSection, string? Class = null);

/// <summary>One hull an UNKNOWN target could be, and how likely.</summary>
/// <param name="Name">The hull as a pilot would say it.</param>
/// <param name="Chance">Its share of the odds, 0 to 1, rounded to five percent.</param>
/// <param name="Because">What put it there: signature, seen nearby, faction.</param>
/// <param name="Class">The game's class name, for the hull's picture.</param>
public sealed record HullOdds(string Name, double Chance, IReadOnlyList<string> Because, string? Class = null);

/// <summary>
/// Every hull's cross-section signature, so a target the panel calls UNKNOWN
/// can still be narrowed to the hulls it could be.
/// </summary>
/// <remarks>
/// <para>
/// The panel's third figure is the hull's largest cross-section face times its
/// armour's multiplier - checked against the 2026-11-05 recording on six named
/// targets: Idris-P 60,000 read 60.0K, Perseus 35,430 read 35.4K, Vanduul
/// Blade 10,418 read 10.4K, Glaive 11,922 read 11.9K, Paladin 26,710 x its
/// armour read 28.5K. It does not move with throttle or power, so it is the
/// one figure that belongs to the hull rather than to the moment.
/// </para>
/// <para>
/// It names a hull family, not a ship: the panel cuts to a hundred, so a
/// figure lands on every hull within that, and seven hulls share 9.0K. The
/// page lists them as candidates and says so; a name only replaces them when
/// the panel prints one.
/// </para>
/// </remarks>
public sealed class ShipSignatureBook
{
    private readonly List<SignatureMatch> _hulls;

    private ShipSignatureBook(List<SignatureMatch> hulls) => _hulls = hulls;

    public static ShipSignatureBook Empty { get; } = new([]);

    public int Count => _hulls.Count;

    /// <summary>Builds the book from the community dataset's garage digests.</summary>
    public static ShipSignatureBook From(CommunityData community)
    {
        if (!community.HasGarage)
            return Empty;

        var hulls = new List<SignatureMatch>();

        foreach (var (className, ship) in community.GarageShips)
        {
            if (!ship.IsSpaceship) continue;

            double cs;
            try
            {
                var sheet = ShipSheet.Compute(ship, community.Parts);
                cs = Math.Max(sheet.CrossSection.X, Math.Max(sheet.CrossSection.Y, sheet.CrossSection.Z));
            }
            catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or ArgumentException)
            {
                cs = Math.Max(ship.CrossSection.X, Math.Max(ship.CrossSection.Y, ship.CrossSection.Z));
            }

            if (cs > 0)
                hulls.Add(new SignatureMatch(Spoken(className, ship.Name), cs, className));
        }

        return new ShipSignatureBook([.. hulls
            .GroupBy(h => (h.Name.ToUpperInvariant(), Math.Round(h.CrossSection)))
            .Select(g => g.First())
            .OrderBy(h => h.CrossSection)]);
    }

    /// <summary>A book from given hulls, for tests.</summary>
    public static ShipSignatureBook Of(IEnumerable<SignatureMatch> hulls) =>
        new([.. hulls.OrderBy(h => h.CrossSection)]);

    /// <summary>
    /// The hulls whose cross-section is within what the panel's rounding and
    /// a misread digit allow of <paramref name="crossSection"/>, closest first.
    /// </summary>
    public IReadOnlyList<SignatureMatch> Near(double crossSection, int take = 5)
    {
        // The panel cuts to one decimal of a thousand rather than rounding:
        // the Paladin's 28,580 read 28.5K. So a figure of 9.7K is a hull
        // from 9,700 to 9,799, and the middle of that is what is closest. A
        // little either side covers armour this has not modelled exactly.
        var middle = crossSection + 50;
        var slack = Math.Max(80, crossSection * 0.006);

        var near = _hulls
            .Where(h => Math.Abs(h.CrossSection - middle) <= slack)
            .OrderBy(h => Math.Abs(h.CrossSection - middle))
            .ThenBy(h => h.Name.Length)
            .ToList();

        // "Aegis Gladius Valiant", "Pirate" and "Dunlevy" are one hull to a
        // pilot looking at it: a variant whose base is already listed with the
        // same figure is the base.
        var kept = new List<SignatureMatch>();
        foreach (var hull in near.OrderBy(h => h.Name.Length))
        {
            // Within fifty, not equal: the Scorpius and its Antares differ by six.
            // By class as well as by name: "Corsair PYAM Exec" does not start
            // with "Drake Corsair", but DRAK_Corsair_PYAM_Exec starts with DRAK_Corsair.
            if (!kept.Any(k => Math.Abs(k.CrossSection - hull.CrossSection) < 50
                    && (hull.Name.StartsWith(k.Name, StringComparison.OrdinalIgnoreCase)
                        || (k.Class is { } baseClass && hull.Class?.StartsWith(baseClass + "_", StringComparison.OrdinalIgnoreCase) == true))))
                kept.Add(hull);
        }

        return [.. kept
            .OrderBy(h => Math.Abs(h.CrossSection - middle))
            .ThenBy(h => h.Name.Length)
            .Take(take)];
    }

    /// <summary>
    /// Rough odds for which hull an UNKNOWN is, from three clues: the
    /// cross-section, the ship classes the log saw stream in lately, and the
    /// faction line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are weights, not measured frequencies, and the page says so.
    /// What they rest on, each from the 2026-11-05 session:
    /// </para>
    /// <list type="bullet">
    /// <item>A hull whose figure falls inside the panel's hundred counts in
    /// full; one just outside it, which only armour modelled slightly wrong
    /// would put there, a third.</item>
    /// <item>A hull the log saw stream in counts eight times one it did not.
    /// The 85X and the Mustangs share the Blade's and Glaive's figures and
    /// were never in the log; the Vanduul were, dozens of times. Not seen is
    /// not ruled out: a ship can be fought before its line is read.</item>
    /// <item>A Vanduul faction makes a Vanduul hull twenty times likelier, and
    /// any other faction read makes one twenty times less: the panel
    /// printed VANDUUL only on Vanduul hulls, and OUTLAWS never on one.</item>
    /// </list>
    /// </remarks>
    public IReadOnlyList<HullOdds> Weigh(
        double crossSection, string? faction, IReadOnlySet<string> nearby, int take = 4)
    {
        var pool = Near(crossSection, take: 12);
        if (pool.Count == 0)
            return [];

        var vanduulFaction = faction is not null && faction.Contains("Vanduul", StringComparison.OrdinalIgnoreCase);
        var otherFaction = faction is not null && !vanduulFaction;

        var weighed = new List<(SignatureMatch Hull, double Weight, List<string> Because)>();
        foreach (var hull in pool)
        {
            var because = new List<string>();
            var inside = hull.CrossSection >= crossSection && hull.CrossSection < crossSection + 100;
            var weight = inside ? 1.0 : 0.3;
            because.Add(inside ? "signature" : "signature, just outside");

            if (hull.Class is { } cls && nearby.Any(seen => SameClass(seen, cls)))
            {
                weight *= 8;
                because.Add("seen nearby");
            }

            var isVanduul = hull.Class?.StartsWith("VNCL_", StringComparison.OrdinalIgnoreCase) == true;
            if (vanduulFaction)
            {
                weight *= isVanduul ? 20 : 0.05;
                if (isVanduul) because.Add("Vanduul faction");
            }
            else if (otherFaction && isVanduul)
            {
                weight *= 0.05;
            }

            weighed.Add((hull, weight, because));
        }

        var total = weighed.Sum(w => w.Weight);
        return [.. weighed
            .OrderByDescending(w => w.Weight)
            .Select(w => new HullOdds(w.Hull.Name, Math.Round(w.Weight / total * 20) / 20, w.Because, w.Hull.Class))
            .Where(o => o.Chance >= 0.05)
            .Take(take)];
    }

    /// <summary>The log's class and the book's, allowing a variant either way: AEGS_Gladius and AEGS_Gladius_Pirate.</summary>
    private static bool SameClass(string seen, string book) =>
        string.Equals(seen, book, StringComparison.OrdinalIgnoreCase)
        || seen.StartsWith(book + "_", StringComparison.OrdinalIgnoreCase)
        || book.StartsWith(seen + "_", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The hull a printed name is, or null: the panel prints a shortened name
    /// - "ORIGIN M50" for the M50 Interceptor - and the engine reads it as
    /// RSIPERSEUS or ORIGIN MBO, so this compares letters only, against each
    /// hull's name and its first two words, and allows two wrong.
    /// </summary>
    public string? Hull(string printed)
    {
        var flat = Flat(printed);
        if (flat.Length < 4) return null;

        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var hull in _hulls)
        {
            var words = hull.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var form in new[] { hull.Name, string.Join(' ', words.Take(2)), string.Join(' ', words.Take(3)) })
            {
                var d = TargetPanel.Distance(flat, Flat(form));
                if (d < bestDistance || (d == bestDistance && best is not null && hull.Name.Length < best.Length))
                {
                    best = hull.Name;
                    bestDistance = d;
                }
            }
        }

        return bestDistance <= Math.Min(2, flat.Length / 5) ? best : null;
    }
    /// <summary>Every hull name in the book, once each, as the voice would say it.</summary>
    public IReadOnlyList<string> Names() =>
        [.. _hulls.Select(h => h.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The game's class name for a hull the book names, for its picture; null when it is not in the book.</summary>
    public string? ClassOf(string? hull) =>
        hull is null ? null : _hulls.FirstOrDefault(h => string.Equals(h.Name, hull, StringComparison.OrdinalIgnoreCase))?.Class;


    private static string Flat(string text) =>
        new([.. text.ToUpperInvariant().Where(char.IsLetterOrDigit)]);

    /// <summary>
    /// The dataset files the Vanduul hulls a pilot can fly under Esperia; the
    /// ones shooting at him are Vanduul, and the panel says so.
    /// </summary>
    private static string Spoken(string className, string name) =>
        className.StartsWith("VNCL_", StringComparison.OrdinalIgnoreCase)
            ? "Vanduul " + name.Replace("Esperia ", "", StringComparison.OrdinalIgnoreCase).Replace("Vanduul ", "", StringComparison.OrdinalIgnoreCase)
            : name;
}
