using Quantumwake.Core.GameData;

namespace Quantumwake.Data;

/// <summary>A hull whose cross-section is near a figure the target panel showed.</summary>
/// <param name="Name">The hull as a pilot would say it.</param>
/// <param name="CrossSection">Its stock cross-section signature.</param>
public sealed record SignatureMatch(string Name, double CrossSection);

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
                hulls.Add(new SignatureMatch(Spoken(className, ship.Name), cs));
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
            if (!kept.Any(k => Math.Abs(k.CrossSection - hull.CrossSection) < 1
                    && hull.Name.StartsWith(k.Name, StringComparison.OrdinalIgnoreCase)))
                kept.Add(hull);
        }

        return [.. kept
            .OrderBy(h => Math.Abs(h.CrossSection - middle))
            .ThenBy(h => h.Name.Length)
            .Take(take)];
    }

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
