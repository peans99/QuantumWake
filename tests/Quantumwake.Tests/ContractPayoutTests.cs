using Quantumwake.Core.Events;
using Quantumwake.Core.State;

namespace Quantumwake.Tests;

/// <summary>
/// The award toast: the only line in a Star Citizen log that names a sum of
/// money earned.
/// </summary>
/// <remarks>
/// What makes this worth its own file is that the toast says nothing about
/// what it paid for. Its MissionId field is present and zeroed on all 14
/// awards in this install, so the contract has to come from the completion
/// toast in front of it - and a pairing rule is exactly the kind of thing that
/// looks right until a session where two contracts finish together.
/// </remarks>
public class ContractPayoutTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 5, 3, 18, 6, 45, TimeSpan.Zero);

    private static NotificationEvent Toast(DateTimeOffset at, string text, string id) =>
        new(at, text, id, "00000000-0000-0000-0000-000000000000");

    private static SessionSummary Built(params GameEvent[] events)
    {
        var builder = new SessionBuilder("Game Build(1) 03 May 26 (13 41 25).log");
        foreach (var e in events) builder.Add(e);
        return builder.Build();
    }

    [Fact]
    public void An_award_toast_is_read_as_money()
    {
        var payouts = Built(Toast(T0, "Awarded 50250 aUEC: ", "32")).Payouts;

        var payout = Assert.Single(payouts);
        Assert.Equal(50250m, payout.Amount);
    }

    /// <summary>
    /// The real pairing, taken from this install: the completion lands 178 ms
    /// before the award and one queue index earlier.
    /// </summary>
    [Fact]
    public void The_completion_in_front_of_it_names_what_was_paid()
    {
        var payout = Assert.Single(Built(
            Toast(T0, "Contract Complete: Opportunity for Independent Cargo Hauler: ", "31"),
            Toast(T0.AddMilliseconds(178), "Awarded 50250 aUEC: ", "32")).Payouts);

        Assert.Equal("Opportunity for Independent Cargo Hauler", payout.Contract);
        Assert.Equal(50250m, payout.Amount);
    }

    /// <summary>
    /// A completion from earlier in the session is not what this award paid
    /// for. Widest real gap is 0.4 s; anything past two seconds is a different
    /// event, and borrowing its title would invent a fact.
    /// </summary>
    [Fact]
    public void A_distant_completion_is_not_borrowed()
    {
        var payout = Assert.Single(Built(
            Toast(T0, "Contract Complete: Combat Gauntlet - Scenario #4: ", "31"),
            Toast(T0.AddMinutes(6), "Awarded 50250 aUEC: ", "88")).Payouts);

        Assert.Null(payout.Contract);
        Assert.Equal(50250m, payout.Amount);
    }

    /// <summary>
    /// One completion pays once. Without clearing the pairing, a second award
    /// later in the session would be filed against a contract that had already
    /// been settled.
    /// </summary>
    [Fact]
    public void A_completion_is_only_paid_out_once()
    {
        var payouts = Built(
            Toast(T0, "Contract Complete: Junior Rank - Direct Medium Cargo Haul: ", "31"),
            Toast(T0.AddMilliseconds(200), "Awarded 80500 aUEC: ", "32"),
            Toast(T0.AddMilliseconds(400), "Awarded 55750 aUEC: ", "33")).Payouts;

        Assert.Equal(2, payouts.Count);
        Assert.Equal("Junior Rank - Direct Medium Cargo Haul", payouts[0].Contract);
        Assert.Null(payouts[1].Contract);
    }

    /// <summary>
    /// The same toast fires 3-5 times with differing Action values. Counting
    /// them separately would multiply this install's earnings by four.
    /// </summary>
    [Fact]
    public void Repeats_of_one_toast_are_paid_once()
    {
        var payouts = Built(
            Toast(T0, "Awarded 61500 aUEC: ", "66"),
            Toast(T0.AddSeconds(6), "Awarded 61500 aUEC: ", "66"),
            Toast(T0.AddSeconds(11), "Awarded 61500 aUEC: ", "66")).Payouts;

        Assert.Single(payouts);
    }

    /// <summary>
    /// Two awards of the same size in one session are two payments, not a
    /// repeat: 50250 turns up twice in this corpus, hours apart.
    /// </summary>
    [Fact]
    public void Equal_amounts_under_different_ids_are_both_kept()
    {
        var payouts = Built(
            Toast(T0, "Awarded 50250 aUEC: ", "32"),
            Toast(T0.AddHours(3), "Awarded 50250 aUEC: ", "77")).Payouts;

        Assert.Equal(2, payouts.Count);
    }

    /// <summary>
    /// StarStrings' annotations stay on the stored title. They are what the
    /// player saw, and stripping them is the ledger's job, not the parser's.
    /// </summary>
    [Fact]
    public void The_title_is_kept_as_the_game_rendered_it()
    {
        var payout = Assert.Single(Built(
            Toast(T0, "Contract Complete: Rookie | Extra Small Haul | from Port Tressler <EM4>[BP]*</EM4>: ", "31"),
            Toast(T0.AddMilliseconds(140), "Awarded 9000 aUEC: ", "32")).Payouts);

        Assert.Equal("Rookie | Extra Small Haul | from Port Tressler <EM4>[BP]*</EM4>", payout.Contract);
    }

    /// <summary>
    /// Toasts that merely mention an award are not one. "Awarded" opens the
    /// line or it is not a payment.
    /// </summary>
    [Fact]
    public void Other_toasts_are_not_money()
    {
        Assert.Empty(Built(
            Toast(T0, "Contract Complete: Tracker Training Permit Certification: ", "31"),
            Toast(T0.AddSeconds(1), "Received Blueprint: Reputation Awarded 500 aUEC", "32")).Payouts);
    }

    /// <summary>
    /// The payout reaches the feed. It is the moment a player most wants to
    /// see, and it is the only one the timeline could not previously show.
    /// </summary>
    [Fact]
    public void A_payout_reaches_the_timeline()
    {
        var timeline = Built(
            Toast(T0, "Contract Complete: Rookie Rank - Extra Small Cargo Haul: ", "31"),
            Toast(T0.AddMilliseconds(220), "Awarded 61500 aUEC: ", "32")).Timeline;

        var entry = Assert.Single(timeline, t => t.Kind == "payout");
        Assert.Contains("61,500", entry.Text);
    }
}
