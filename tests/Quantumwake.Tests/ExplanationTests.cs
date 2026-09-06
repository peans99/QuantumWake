using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// "Why this number?", generalised.
/// </summary>
/// <remarks>
/// The shape arrived three times on its own before it was written down - the
/// run review's money in and out, a kit preparation's rule, the refinery
/// caveat - so what is under test is not the wrapper but the two claims it is
/// easy to make by accident: that an unexplained figure has nothing behind it,
/// and that a figure with no records is a figure with no answer.
/// </remarks>
public class ExplanationTests : IDisposable
{
    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;

    public ExplanationTests() => _library = new LogLibrary(_store);
    public void Dispose() => _store.Dispose();

    private static readonly DateTimeOffset At = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private void Save(string id, params CommodityTrade[] trades) =>
        _store.Save(
            new SessionSummary
            {
                Id = id,
                SourceFile = $"{id}.log",
                StartedAt = At.AddHours(-1),
                EndedAt = At.AddHours(1),
                Handle = "nekron",
                Trades = trades,
            },
            $"fingerprint:{id}");

    private static CommodityTrade Sale(decimal amount) =>
        new(At, "SCShop_Admin_lt_base_g", amount, 96, IsSell: true, Mode: null);

    /// <summary>
    /// A figure nobody has taught this to explain is absent, not empty.
    /// Answering with a blank explanation would say there is nothing behind the
    /// number, which is a much stronger claim than "no one wrote that down".
    /// </summary>
    [Fact]
    public void An_unknown_figure_has_no_explanation_rather_than_an_empty_one()
    {
        Assert.Null(Explanations.For("something.invented", _library));
        Assert.Null(Explanations.For(null, _library));
        Assert.Null(Explanations.For("", _library));
    }

    [Fact]
    public void Money_in_carries_the_records_behind_it()
    {
        Save("s1", Sale(288_000));

        var explained = Explanations.For(Explanations.Figures.MoneyIn, _library)!;

        Assert.Equal(288_000, explained.Value);
        Assert.Single(explained.Records);
        Assert.True(explained.FromRecords);
    }

    /// <summary>
    /// Every figure states its rule, so no page has to word the inference
    /// itself and no two pages can word it differently.
    /// </summary>
    [Fact]
    public void Every_known_figure_states_a_rule_and_what_it_left_out()
    {
        foreach (var figure in Explanations.Known)
        {
            var explained = Explanations.For(figure, _library);

            Assert.NotNull(explained);
            Assert.NotEmpty(explained.Rule);
            Assert.NotEmpty(explained.Excluded);
            Assert.NotEmpty(explained.Label);
        }
    }

    /// <summary>
    /// The one the whole feature is for. An inferred death count has no rows to
    /// show, and that is the answer rather than a gap - a control that hid
    /// itself on exactly the figures people most doubt would be worse than no
    /// control at all.
    /// </summary>
    [Fact]
    public void A_figure_with_no_records_still_answers_and_says_so()
    {
        var explained = Explanations.For(Explanations.Figures.Deaths, _library)!;

        Assert.Empty(explained.Records);
        Assert.False(explained.FromRecords);
        Assert.Contains("no underlying records", string.Join(" ", explained.Excluded));
        Assert.Contains("stopped writing a death event", explained.Rule);
    }

    /// <summary>
    /// A trade is what a terminal was asked for. A figure built out of requests
    /// has to say how much of it is one.
    /// </summary>
    [Fact]
    public void A_figure_says_how_much_of_it_was_only_requested()
    {
        Save("s1", Sale(288_000));

        var explained = Explanations.For(Explanations.Figures.MoneyIn, _library)!;

        Assert.Contains(explained.Excluded, line => line.Contains("requested rather than confirmed"));
    }

    /// <summary>
    /// The net is the figure people argue with, and the argument is almost
    /// always about what the logs never saw rather than about the arithmetic.
    /// </summary>
    [Fact]
    public void The_net_says_what_the_logs_never_saw()
    {
        var explained = Explanations.For(Explanations.Figures.Net, _library)!;

        Assert.Contains(explained.Excluded, line => line.Contains("Mining, salvage"));
    }

    /// <summary>
    /// Contract payouts are a floor. Across this install the game priced 14
    /// completions out of 231, and a total shown without that reads as contract
    /// income.
    /// </summary>
    [Fact]
    public void Contract_payouts_say_they_are_a_floor()
    {
        var explained = Explanations.For(Explanations.Figures.Payouts, _library)!;

        Assert.Contains(explained.Excluded,
            line => line.Contains("said nothing, not that a contract paid nothing"));
    }

    [Fact]
    public void A_window_is_named_among_the_exclusions()
    {
        var windowed = Explanations.For(Explanations.Figures.MoneyIn, _library, days: 7)!;
        var everything = Explanations.For(Explanations.Figures.MoneyIn, _library)!;

        Assert.Contains(windowed.Excluded, line => line.Contains("last 7 days"));
        Assert.Contains(everything.Excluded, line => line.Contains("covers everything counted"));
    }

    /// <summary>
    /// The records have to add up to the figure, or the explanation is decoration.
    /// </summary>
    [Fact]
    public void The_records_add_up_to_the_figure_they_explain()
    {
        Save("s1", Sale(288_000));
        Save("s2", Sale(120_000));

        var explained = Explanations.For(Explanations.Figures.MoneyIn, _library)!;

        Assert.Equal(explained.Value, explained.Records.Sum(r => Math.Abs(r.Amount ?? 0)));
    }
}
