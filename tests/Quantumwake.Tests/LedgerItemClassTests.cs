using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// A ledger row about an item carries the item's class beside its name.
/// </summary>
/// <remarks>
/// The row reads as the display name, which is what a pilot recognises; the
/// class is what opens the item's Reference card from the row (0.20.32), as
/// every other table does. Cargo and payout rows have no card and carry none.
/// </remarks>
public class LedgerItemClassTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 5, 3, 18, 6, 45, TimeSpan.Zero);

    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;

    public LedgerItemClassTests() => _library = new LogLibrary(_store);

    [Fact]
    public void A_purchase_carries_its_item_class_and_a_payout_none()
    {
        _store.Save(
            new SessionSummary
            {
                Id = "s1",
                SourceFile = "s1.log",
                StartedAt = At.AddHours(-1),
                EndedAt = At.AddHours(1),
                Handle = "nekron",
                Purchases = [new PurchaseRecord(At, "SCShop_CubbyBlast_Lorville", "crlf_consumable_healing_01", 400m, 1, true)],
                Payouts = [new ContractPayout(At.AddMinutes(5), "Junior Rank - Direct Medium Cargo Haul", 80500m)],
            },
            "fingerprint:s1");

        var ledger = _library.Ledger();

        var bought = Assert.Single(ledger, e => e.Kind == "Item bought");
        Assert.Equal("crlf_consumable_healing_01", bought.Item);

        var paid = Assert.Single(ledger, e => e.Kind == "Contract paid");
        Assert.Null(paid.Item);
    }

    public void Dispose() => _store.Dispose();
}
