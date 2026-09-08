using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Contract payouts reaching the ledger.
/// </summary>
/// <remarks>
/// Until now the ledger had two sources - purchases and commodity trades - so
/// every credit in it came from selling something. A contract payout is the
/// first income that is not a sale, and the thing worth testing is that it
/// arrives with its sign and its title intact: booked negative it would read as
/// a hauling run that cost money, and StarStrings' "[150 Rep]" left on the row
/// names a reputation figure next to a number of credits it is not.
/// </remarks>
public class LedgerPayoutTests : IDisposable
{
    private static readonly DateTimeOffset At =
        new(2026, 5, 3, 18, 6, 45, TimeSpan.Zero);

    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;

    public LedgerPayoutTests() => _library = new LogLibrary(_store);

    private void Save(params ContractPayout[] payouts) =>
        _store.Save(
            new SessionSummary
            {
                Id = "s1",
                SourceFile = "s1.log",
                StartedAt = At.AddHours(-1),
                EndedAt = At.AddHours(1),
                Handle = "nekron",
                Payouts = payouts,
            },
            "fingerprint:s1");

    [Fact]
    public void A_payout_is_money_in()
    {
        Save(new ContractPayout(At, "Junior Rank - Direct Medium Cargo Haul", 80500m));

        var entry = Assert.Single(_library.Ledger(), e => e.Kind == "Contract paid");

        Assert.Equal(80500m, entry.Amount);
        Assert.Equal("Junior Rank - Direct Medium Cargo Haul", entry.What);
    }

    /// <summary>
    /// The game stated the number outright, so it is settled - unlike a
    /// commodity trade, which the ledger marks unconfirmed because no server
    /// response ever backs it.
    /// </summary>
    [Fact]
    public void A_payout_needs_no_confirmation()
    {
        Save(new ContractPayout(At, "Rookie Rank - Extra Small Cargo Haul", 61500m));

        Assert.True(Assert.Single(_library.Ledger()).Confirmed);
    }

    /// <summary>
    /// A rep figure is not a price. Left on the row it sits in a column of
    /// credits and reads as one.
    /// </summary>
    [Fact]
    public void The_mods_annotations_come_off_the_row()
    {
        Save(new ContractPayout(At, "Large Covalex Shipment Needs Recovering <EM4>[200 Rep]</EM4>", 50250m));

        var entry = Assert.Single(_library.Ledger());

        Assert.DoesNotContain("200 Rep", entry.What);
        Assert.DoesNotContain("EM4", entry.What);
        Assert.Contains("Covalex", entry.What);
    }

    /// <summary>
    /// An award with no completion close enough to name says so, rather than
    /// showing an empty cell that reads as a missing row.
    /// </summary>
    [Fact]
    public void An_unattributed_payout_still_appears()
    {
        Save(new ContractPayout(At, null, 39750m));

        var entry = Assert.Single(_library.Ledger());

        Assert.Equal("Contract payout", entry.What);
        Assert.Equal(39750m, entry.Amount);
    }

    public void Dispose() => _store.Dispose();
}
