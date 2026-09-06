namespace Quantumwake.Data;

/// <summary>One record that went into a figure.</summary>
public sealed record ExplainedRecord(
    DateTimeOffset At,
    string What,
    string? Where,
    decimal? Amount,
    bool Confirmed = true);

/// <summary>
/// A figure, with everything needed to argue with it.
/// </summary>
/// <param name="Rule">
/// The inference in one sentence, written once beside the code that applies it
/// rather than re-typed into a caption on every page that shows the number.
/// </param>
/// <param name="Excluded">
/// What was left out and why. The half that is easy to skip and the half that
/// answers the question people actually ask, which is almost never "where did
/// this come from" and almost always "why is it lower than I expected".
/// </param>
/// <param name="Records">
/// Empty is a real answer. Some figures are inferences with no underlying rows
/// at all - see <paramref name="Rule"/> - and a blank list beside a promise of
/// records reads as data that failed to load.
/// </param>
public sealed record Explanation(
    string Figure,
    string Label,
    decimal Value,
    string Source,
    string Rule,
    IReadOnlyList<ExplainedRecord> Records,
    IReadOnlyList<string> Excluded)
{
    /// <summary>
    /// Whether there are rows behind this at all, said outright.
    /// </summary>
    /// <remarks>
    /// So a page can word "no underlying records, only the rule" differently
    /// from "no records matched", which look identical from a count of zero and
    /// mean opposite things.
    /// </remarks>
    public bool FromRecords => Records.Count > 0;
}

/// <summary>
/// Answers "why this number?" for the figures that most invite it.
/// </summary>
/// <remarks>
/// <para>
/// Generalised from three places that had already grown the same shape by
/// themselves - the run review's money in and out, a kit preparation's rule,
/// and the refinery caveat. Three independent arrivals at value-plus-rule-plus-
/// what-was-left-out is the point at which writing it down once stops being
/// speculative.
/// </para>
/// <para>
/// A figure with no entry here is absent rather than empty. Returning a blank
/// explanation for something nobody has taught this to explain would say "there
/// is nothing behind this number", which is a much stronger claim than "no one
/// has written that down yet".
/// </para>
/// </remarks>
public static class Explanations
{
    /// <summary>Figure keys, spelled once so a page and the server cannot drift.</summary>
    public static class Figures
    {
        public const string MoneyIn = "ledger.in";
        public const string MoneyOut = "ledger.out";
        public const string Net = "ledger.net";
        public const string Payouts = "contracts.paid";
        public const string Deaths = "combat.deaths";
    }

    /// <summary>Every figure this build can explain.</summary>
    public static IReadOnlyList<string> Known =>
        [Figures.MoneyIn, Figures.MoneyOut, Figures.Net, Figures.Payouts, Figures.Deaths];

    private const string PlaceRule =
        "Where a movement happened is back-tracked from the last arrival before it, "
        + "because a receipt names a kiosk and never a place.";

    private const string RequestRule =
        "A commodity trade is what a terminal was asked for, never what it confirmed - "
        + "the game logs the request and not the answer.";

    public static Explanation? For(string? figure, LogLibrary library, int days = 0) =>
        figure switch
        {
            Figures.MoneyIn => Money(library, days, into: true),
            Figures.MoneyOut => Money(library, days, into: false),
            Figures.Net => Net(library, days),
            Figures.Payouts => Payouts(library, days),
            Figures.Deaths => Deaths(library),
            _ => null,
        };

    private static Explanation Money(LogLibrary library, int days, bool into)
    {
        var rows = library.Ledger(days)
            .Where(entry => into ? entry.Amount > 0 : entry.Amount < 0)
            .ToList();

        var unconfirmed = rows.Count(row => !row.Confirmed);

        var excluded = new List<string>();

        if (unconfirmed > 0)
        {
            excluded.Add($"{unconfirmed} of these were requested rather than confirmed. {RequestRule}");
        }

        excluded.Add(days > 0
            ? $"Anything outside the last {days} days."
            : "Nothing on time - this covers everything counted.");

        return new Explanation(
            into ? Figures.MoneyIn : Figures.MoneyOut,
            into ? "Money in" : "Money out",
            rows.Sum(row => Math.Abs(row.Amount)),
            "your logs",
            $"Every movement of money your logs recorded, {(into ? "into" : "out of")} your account. {PlaceRule}",
            [.. rows.Select(Row)],
            excluded);
    }

    private static Explanation Net(LogLibrary library, int days)
    {
        var rows = library.Ledger(days);

        return new Explanation(
            Figures.Net,
            "Net",
            rows.Sum(row => row.Amount),
            "your logs",
            $"Money in less money out, across everything counted. {PlaceRule}",
            [.. rows.Select(Row)],

            // The one people ask about. A net that looks wrong is nearly always
            // a question about what the logs never saw rather than about the
            // arithmetic.
            ["Mining, salvage and anything else you were paid for outside a "
             + "terminal or a contract, because the game records none of it.",
             RequestRule]);
    }

    private static Explanation Payouts(LogLibrary library, int days)
    {
        var rows = library.Ledger(days)
            .Where(entry => entry.Kind == "Contract paid")
            .ToList();

        return new Explanation(
            Figures.Payouts,
            "Contracts paid",
            rows.Sum(row => row.Amount),
            "your logs",
            "The game states a contract payout in a single toast, and only sometimes. "
            + "This is the sum of the ones it stated.",
            [.. rows.Select(Row)],

            // The caveat that makes the number honest. Across this install the
            // game priced 14 completions out of 231.
            ["Every completion the game did not price, which is most of them. "
             + "A missing payout means the log said nothing, not that a contract paid nothing.",
             "Combat contracts, which have never stated a payout in this install."]);
    }

    private static Explanation Deaths(LogLibrary library)
    {
        var deaths = library.Sessions().Sum(session => session.Deaths);

        return new Explanation(
            Figures.Deaths,
            "Deaths",
            deaths,
            "your logs",

            // No records, and that is the answer rather than a gap. SC 4.9
            // stopped writing <Actor Death> at all.
            "Inferred from corpse item-recovery bursts. The game stopped writing a death "
            + "event, so nothing here is a record of a death - it is a count of the pattern "
            + "one leaves behind.",
            [],
            ["Deaths that left no recovery burst, which cannot be counted at all.",
             "This figure has no underlying records - only the rule above."]);
    }

    private static ExplainedRecord Row(LedgerEntry entry) =>
        new(entry.At, entry.What, entry.Where, entry.Amount, entry.Confirmed);
}
