namespace Quantumwake.Data;

/// <summary>One thing a tile might be, and how well its picture fitted.</summary>
public sealed record TileCandidate(string Name, double Score);

/// <summary>How sure the matcher is about a tile.</summary>
/// <remarks>
/// Three words rather than a number on screen, for the reason the rest of this
/// app gives confidences at all: a pilot can act on "unsure" and cannot act on
/// 0.91. The number is kept beside it for anyone who wants it.
/// </remarks>
public enum TileConfidence
{
    /// <summary>Fitted well and beat the runner-up clearly.</summary>
    Named,

    /// <summary>Fitted well, but something else fitted nearly as well.</summary>
    Unsure,

    /// <summary>Nothing in the portfolio fitted. The pilot is asked to name it.</summary>
    Unknown
}

/// <summary>What the matcher made of one tile.</summary>
/// <param name="Why">
/// Worded for a reader, and never left empty. A tile the app cannot name gets
/// a sentence saying why rather than a blank space, which is the same rule the
/// Crew page follows about counts it cannot complete.
/// </param>
public sealed record TileVerdict(
    TileConfidence Confidence,
    string? Name,
    double Score,
    double Margin,
    string Why,
    IReadOnlyList<TileCandidate> Ranked);

/// <summary>Turns a portfolio's scores into a verdict about one tile.</summary>
/// <remarks>
/// <para>
/// The rule is the best match <em>and</em> its margin over the runner-up,
/// never a score on its own, and that is a measurement rather than a
/// preference. On the ten filter glyphs of
/// <c>ScreenShot-2026-09-12_13-51-13-B21.jpg</c> the worst correct match
/// scored 0.947 and the best incorrect one 0.860: a bare threshold would have
/// to be threaded between those two, which is far too fine to trust on a
/// sample of ten from a single frame.
/// </para>
/// <para>
/// The margin is the honest discriminator because it asks the question that
/// matters - is this item more like its match than like anything else the
/// pilot has taught - and it gets safer rather than more dangerous as the
/// portfolio grows.
/// </para>
/// <para>
/// Both numbers below are provisional and marked as such deliberately. They
/// come from flat white line art on an identical button, which is a hard case
/// for shape and an easy one for everything else, and from one frame, which
/// cannot say what a render does across two. See <c>docs/screen-insight.md</c>.
/// </para>
/// </remarks>
public static class TileDecision
{
    /// <summary>Below this, nothing in the portfolio is claimed to fit.</summary>
    public const double MinScore = 0.90;

    /// <summary>
    /// How far the best must beat the runner-up. Set well under the 0.119 the
    /// ten glyphs managed at worst: too tight a margin turns a near-tie into a
    /// confident wrong answer, which is the one failure this feature cannot
    /// afford, and too loose only asks the pilot a question they can answer.
    /// </summary>
    public const double MinMargin = 0.06;

    public static TileVerdict Decide(IReadOnlyList<TileCandidate> scored)
    {
        if (scored.Count == 0)
            return new TileVerdict(
                TileConfidence.Unknown, null, 0, 0,
                "nothing has been taught yet - name it and it is known from here on",
                []);

        var ranked = scored.OrderByDescending(c => c.Score).ToList();
        var best = ranked[0];
        var margin = ranked.Count > 1 ? best.Score - ranked[1].Score : double.PositiveInfinity;

        if (best.Score < MinScore)
            return new TileVerdict(
                TileConfidence.Unknown, null, best.Score, margin,
                $"no taught picture fits - the closest, {best.Name}, reached "
                    + $"{best.Score:0.00} of the {MinScore:0.00} needed",
                ranked);

        if (margin < MinMargin)
            return new TileVerdict(
                TileConfidence.Unsure, best.Name, best.Score, margin,
                $"{best.Name} fits at {best.Score:0.00}, but {ranked[1].Name} fits at "
                    + $"{ranked[1].Score:0.00} and the two are too close to call apart",
                ranked);

        return new TileVerdict(
            TileConfidence.Named, best.Name, best.Score, margin,
            ranked.Count > 1
                ? $"fits at {best.Score:0.00}, ahead of {ranked[1].Name} by {margin:0.00}"
                : $"fits at {best.Score:0.00}, and is the only picture taught so far",
            ranked);
    }
}
