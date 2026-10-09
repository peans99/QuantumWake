using System.Text.RegularExpressions;

namespace Quantumwake.Core.GameData;

/// <summary>One thing an event tier hands over, matched to the catalogue where it can be.</summary>
/// <param name="Part">The piece of the reward text it came from - "Sovereign IP-20 Power Plant".</param>
/// <param name="Class">The item or paint class, or null when nothing in the install carries that name.</param>
/// <param name="Name">The catalogue's name for it, or the text's when unmatched.</param>
/// <param name="Kind">The catalogue's type - Cooler, Radar, PowerPlant, FlightController, WeaponPersonal - or Paint.</param>
/// <param name="OneOf">
/// True when the text names a family and the install has several - "Zeus Mk II
/// PHB" fits both the CL and the ES blade. Every candidate is listed and the
/// page says it is one of them, rather than picking.
/// </param>
public sealed record GameRewardItem(string Part, string? Class, string Name, string Kind, bool OneOf = false);

/// <summary>
/// Reads an event tier's reward line into the items it names.
/// </summary>
/// <remarks>
/// <para>
/// The badge carries no item: the line is text, keyed by bar and tier, and the
/// only join is by name. On Discovery Month that is enough - 22 of its 25
/// pieces name a catalogue item exactly once the noun the text adds is taken
/// off ("BriskAir IC-10 Cooler" is the item "BriskAir IC-10").
/// </para>
/// <para>
/// The rest were written before the Constellations became "Mk IV": "Constellation
/// Andromeda PHB" is the item "Constellation Mk IV Andromeda PHB Flight Blade".
/// Those match when every word of the text appears in the name in order, and
/// when that fits several items they are all given as one-of rather than one
/// chosen.
/// </para>
/// </remarks>
public static partial class RewardItems
{
    /// <summary>The most candidates a family match may give before it is too loose to mean anything.</summary>
    private const int MaxCandidates = 3;

    public static IReadOnlyList<GameRewardItem> Match(
        string reward, IReadOnlyDictionary<string, (string Class, string Kind)> byName)
    {
        var items = new List<GameRewardItem>();
        if (string.IsNullOrWhiteSpace(reward)) return items;

        string? previous = null;

        foreach (var part in Parts(reward))
        {
            items.AddRange(One(part, previous, byName));
            previous = part;
        }

        return items;
    }

    private static IEnumerable<GameRewardItem> One(
        string part, string? previous, IReadOnlyDictionary<string, (string Class, string Kind)> byName)
    {
        // "Helios Power Plant (Sizes 1-3)": a range, which is every size of it.
        if (SizeRange().Match(part) is { Success: true } range)
        {
            var family = byName
                .Where(e => e.Key.StartsWith(range.Groups["name"].Value + " ", StringComparison.OrdinalIgnoreCase)
                            && e.Value.Kind == "PowerPlant")
                .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (family.Count > 0)
            {
                foreach (var (name, found) in family)
                    yield return new GameRewardItem(part, found.Class, name, found.Kind);
                yield break;
            }
        }

        foreach (var candidate in Candidates(part, previous))
        {
            if (byName.TryGetValue(candidate, out var exact))
            {
                yield return new GameRewardItem(part, exact.Class, Canonical(byName, candidate), exact.Kind);
                yield break;
            }
        }

        // Every word of the text, in order, inside a longer name.
        foreach (var candidate in Candidates(part, previous))
        {
            var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var fits = byName
                .Where(e => InOrder(e.Key, words))
                .OrderBy(e => e.Key.Length)
                .ThenBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (fits.Count is > 0 and <= MaxCandidates)
            {
                foreach (var (name, found) in fits)
                    yield return new GameRewardItem(part, found.Class, name, found.Kind, OneOf: fits.Count > 1);
                yield break;
            }
        }

        yield return new GameRewardItem(part, null, part, "");
    }

    /// <summary>
    /// The names worth looking up for one piece, most specific first. "TSB
    /// Flight Blades" after "Mantis PHB" is the Mantis's TSB blade: the text
    /// gives the hull once for both.
    /// </summary>
    private static IEnumerable<string> Candidates(string part, string? previous)
    {
        if (BladeTail().Match(part) is { Success: true } tail && previous is not null)
            yield return $"{BladeHead().Replace(previous, "")} {tail.Groups["kind"].Value} Flight Blade";

        if (BladeHead().IsMatch(part))
            yield return $"{part} Flight Blade";

        yield return part;
        yield return Noun().Replace(part, "");
        if (part.EndsWith('s')) yield return part[..^1];
    }

    /// <summary>The pieces of a reward line, split on its commas and its "and"s.</summary>
    private static IEnumerable<string> Parts(string reward) =>
        Splitter().Split(reward.Trim().TrimEnd('.'))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

    /// <summary>
    /// Every word of the text is a whole word of the name, in order. Whole
    /// words, because matching inside them let "CA OP 1" find a Pyro datapad.
    /// </summary>
    private static bool InOrder(string name, string[] words)
    {
        var names = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var at = 0;

        foreach (var word in words)
        {
            while (at < names.Length && !string.Equals(names[at], word, StringComparison.OrdinalIgnoreCase)) at++;
            if (at == names.Length) return false;
            at++;
        }

        return words.Length > 1;
    }

    private static string Canonical(IReadOnlyDictionary<string, (string Class, string Kind)> byName, string key) =>
        byName.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) ?? key;

    [GeneratedRegex(@",\s*(?:and\s+)?|\s+and\s+(?=[A-Z])")]
    private static partial Regex Splitter();

    [GeneratedRegex(@"^(?<name>\w+) Power Plant \(Sizes \d-\d\)$")]
    private static partial Regex SizeRange();

    [GeneratedRegex(@"^(?<kind>PHB|TSB) Flight Blades?$")]
    private static partial Regex BladeTail();

    [GeneratedRegex(@" (PHB|TSB)$")]
    private static partial Regex BladeHead();

    [GeneratedRegex(@" (Cooler|Radar|Power Plant|Flight Blades?)$")]
    private static partial Regex Noun();
}
