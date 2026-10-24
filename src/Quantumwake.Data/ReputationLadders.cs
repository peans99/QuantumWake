using Quantumwake.Core.GameData;

namespace Quantumwake.Data;

/// <summary>
/// A Rep app reading set against the ladder the game defines for it.
/// </summary>
/// <param name="Scope">The ladder's record, <c>ReputationScope_Hauling</c>.</param>
/// <param name="Section">The ladder's English heading, "Hauling", or null where the game has none.</param>
/// <param name="Rank">The rank held, in the game's own spelling.</param>
/// <param name="Floor">The reputation the rank held starts at.</param>
/// <param name="Next">The reputation the next rank starts at, or null at the top.</param>
/// <param name="Estimate">
/// The floor plus the bar's share of the gap to the next rank. An estimate, and
/// called one everywhere it is shown: it assumes the bar fills in proportion,
/// which nothing has yet confirmed, and one pixel of bar is about half a percent
/// of the gap - on Hauling's Member rank, a hundred and thirty reputation.
/// </param>
/// <param name="Resolution">What one pixel of bar is worth in reputation, for the ± beside the estimate.</param>
public sealed record ReputationPlace(
    string Scope,
    string? Section,
    string Rank,
    int RankIndex,
    int RankCount,
    long Floor,
    long? Next,
    long Estimate,
    long Resolution,
    string? Perk);

/// <summary>
/// Finds the ladder a Rep app page shows, by its cards.
/// </summary>
/// <remarks>
/// <para>
/// The card names are the signature. Ladders are shared - Red Wind, Covalex,
/// Ling and FTL all show Hauling's NOT ELIGIBLE to MASTER - so the scope is
/// found from the cards rather than from the organisation, whose own list of
/// scopes in the DataCore leaves out ones the game plainly shows (the
/// Headhunters' Rank I to VI, among them).
/// </para>
/// <para>
/// Names are compared on letters alone, because the engine reads underscores
/// as hyphens and drops a roman numeral now and then, and a frame may show
/// fewer cards than the ladder has - Red Wind's eighth card was scrolled off
/// one of the measured frames. Two ladders can share every name: Hired Muscle
/// and the Headhunters' ship combat both run Applicant, Rank I to VI. The
/// heading and the perks settle those, which is why both are scored.
/// </para>
/// </remarks>
public static class ReputationLadders
{
    /// <summary>Where a reading sits on its ladder, or null when no ladder fits or the rank did not read.</summary>
    public static ReputationPlace? Place(ReputationReading reading, GameReputationCatalogue catalogue)
    {
        if (reading.Ranks is not { Count: > 0 } cards || reading.Rank is null || reading.Progress is not { } progress)
            return null;

        if (Match(reading, catalogue) is not { } scope) return null;

        var held = cards.ToList().FindIndex(c => c.Name == reading.Rank);
        if (held < 0 || held >= scope.Ranks.Count) return null;

        var rank = scope.Ranks[held];
        long? next = held + 1 < scope.Ranks.Count ? scope.Ranks[held + 1].MinReputation : null;

        // At the top there is no next rank, but there is a ceiling, and the
        // bar fills towards it.
        var top = next ?? (scope.Ceiling > rank.MinReputation ? scope.Ceiling : (long?)null);
        var gap = top is { } t ? t - rank.MinReputation : 0;

        return new ReputationPlace(
            scope.Id,
            scope.Name,
            rank.Name,
            held,
            scope.Ranks.Count,
            rank.MinReputation,
            next,
            rank.MinReputation + (long)Math.Round(gap * progress),
            Math.Max(1, (long)Math.Round(gap / BarPixels)),
            rank.Perk);
    }

    /// <summary>
    /// The bar's length on a 3440-wide frame: 168 to 173 pixels on every card
    /// of the fourteen measured frames.
    /// </summary>
    private const double BarPixels = 170;

    /// <summary>The ladder whose rank names are the cards', best fit first; null when none fits.</summary>
    public static GameRepScope? Match(ReputationReading reading, GameReputationCatalogue catalogue)
    {
        if (reading.Ranks is not { Count: > 0 } cards) return null;

        GameRepScope? best = null;
        var bestScore = 0.0;

        foreach (var scope in catalogue.Scopes)
        {
            if (scope.Ranks.Count < cards.Count) continue;

            var named = 0;
            for (var i = 0; i < cards.Count; i++)
                if (Same(cards[i].Name, scope.Ranks[i].Name)) named++;

            // Most of the cards must agree; one misread label is allowed for.
            if (named < cards.Count - 1 || named * 2 <= cards.Count) continue;

            var score = (double)named;

            // The heading, English or the bare key the game shows instead.
            if (reading.Section is { } section
                && (Same(section, scope.Name) || Same(section, scope.NameKey)))
                score += 2;

            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i].Perk is null && scope.Ranks[i].Perk is null) continue;
                score += Same(cards[i].Perk, scope.Ranks[i].Perk) ? 0.5 : -0.5;
            }

            // A ladder with exactly the cards shown beats a longer one that
            // merely begins the same way.
            if (scope.Ranks.Count == cards.Count) score += 0.25;

            if (score > bestScore)
            {
                best = scope;
                bestScore = score;
            }
        }

        return best;
    }

    private static bool Same(string? read, string? game) =>
        read is not null && game is not null && Letters(read) == Letters(game);

    private static string Letters(string text) =>
        new([.. text.Where(char.IsLetter).Select(char.ToUpperInvariant)]);
}
