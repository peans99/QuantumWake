using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The wipe applied to the library: what every page is allowed to count.
/// </summary>
/// <remarks>
/// The filter lives in one accessor precisely so a view added later cannot
/// forget it, and these tests say what that accessor must do - including that
/// nothing is destroyed, since a wipe date moved back has to bring the whole
/// history straight back with it.
/// </remarks>
public class WipeFilterTests : IDisposable
{
    private static readonly DateTimeOffset Wiped =
        new(2026, 5, 15, 0, 0, 0, TimeSpan.Zero);

    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;

    public WipeFilterTests()
    {
        _library = new LogLibrary(_store);

        Save("before-1", Wiped.AddDays(-30));
        Save("before-2", Wiped.AddDays(-1));
        Save("after-1", Wiped.AddHours(2));
        Save("after-2", Wiped.AddDays(20));
    }

    /// <param name="spent">
    /// Booked as one confirmed purchase, so a money total has something in it
    /// on either side of the wipe.
    /// </param>
    private void Save(string id, DateTimeOffset started, decimal spent = 0) =>
        _store.Save(
            new SessionSummary
            {
                Id = id,
                SourceFile = $"{id}.log",
                StartedAt = started,
                EndedAt = started.AddHours(2),
                Handle = "nekron",
                Purchases = spent > 0
                    ? [new PurchaseRecord(started.AddMinutes(5), "Shop", $"kit {id}", spent, 1, true)]
                    : [],
            },
            $"fingerprint:{id}");

    [Fact]
    public void With_no_wipe_every_session_counts()
    {
        Assert.Equal(4, _library.Sessions().Count);
        Assert.Equal(0, _library.SessionsBeforeWipe());
    }

    [Fact]
    public void Sessions_from_before_the_wipe_are_not_counted()
    {
        _library.Wipe = new Wipe(Wiped, "Alpha 4.8");

        Assert.Equal(2, _library.Sessions().Count);
        Assert.All(_library.Sessions(), s => Assert.True(s.StartedAt >= Wiped));
    }

    /// <summary>A session that began before the wipe and ran into it is a
    /// pre-wipe session: the account it describes ended part way through.</summary>
    [Fact]
    public void A_session_is_judged_by_when_it_started()
    {
        _library.Wipe = new Wipe(Wiped, "Alpha 4.8");

        Save("straddles", Wiped.AddMinutes(-30));

        Assert.DoesNotContain(_library.Sessions(), s => s.Id == "straddles");
    }

    [Fact]
    public void The_page_can_say_how_much_is_being_held_back()
    {
        _library.Wipe = new Wipe(Wiped, "Alpha 4.8");

        Assert.Equal(2, _library.SessionsBeforeWipe());
        Assert.Equal(4, _store.Count());
    }

    /// <summary>Nothing is deleted, so moving the line back restores the lot.</summary>
    [Fact]
    public void Moving_the_line_back_brings_the_history_back()
    {
        _library.Wipe = new Wipe(Wiped, "Alpha 4.8");
        Assert.Equal(2, _library.Sessions().Count);

        _library.Wipe = null;
        Assert.Equal(4, _library.Sessions().Count);
    }

    /// <summary>
    /// The totals every page reads come from the same accessor, so they move
    /// with it - this is the check that the filter is not merely on the list.
    /// </summary>
    [Fact]
    public void The_totals_move_with_the_line()
    {
        var all = _library.Stats().Sessions;

        _library.Wipe = new Wipe(Wiped, "Alpha 4.8");
        var counted = _library.Stats().Sessions;

        Assert.Equal(4, all);
        Assert.Equal(2, counted);
    }

    /// <summary>
    /// The common case, and the one a single global cutoff got wrong: aUEC
    /// reset, hangar untouched. Blanking the fleet there costs the player real
    /// history for nothing.
    /// </summary>
    [Fact]
    public void A_money_wipe_leaves_the_hangar_and_the_history_alone()
    {
        _library.Wipe = new Wipe(Wiped, "Alpha 4.8", WipeScope.Money);

        Assert.Equal(4, _library.Sessions().Count);
        Assert.Equal(4, _library.Stats().Sessions);
    }

    [Fact]
    public void A_money_wipe_still_starts_the_ledger_again()
    {
        Save("earned-before", Wiped.AddDays(-2), spent: 5000);
        Save("earned-after", Wiped.AddDays(2), spent: 900);

        _library.Wipe = new Wipe(Wiped, "Alpha 4.8", WipeScope.Money);

        Assert.Equal(900, _library.Stats().Spend);
        Assert.DoesNotContain(_library.Ledger(), e => e.What.Contains("before"));
    }

    [Fact]
    public void A_wipe_that_took_nothing_but_kit_leaves_money_counted()
    {
        Save("earned-before", Wiped.AddDays(-2), spent: 5000);
        Save("earned-after", Wiped.AddDays(2), spent: 900);

        _library.Wipe = new Wipe(Wiped, "Alpha 4.8", WipeScope.Inventory);

        Assert.Equal(5900, _library.Stats().Spend);
    }

    [Fact]
    public void A_full_wipe_takes_the_lot()
    {
        Save("earned-before", Wiped.AddDays(-2), spent: 5000);
        Save("earned-after", Wiped.AddDays(2), spent: 900);

        _library.Wipe = new Wipe(Wiped, "Alpha 4.8", WipeScope.Everything);

        Assert.Equal(900, _library.Stats().Spend);
        Assert.Equal(3, _library.Sessions().Count);
    }

    /// <summary>
    /// The count is about the date, not the depth: it says what lies before the
    /// line, which is what the Settings row is reporting.
    /// </summary>
    [Fact]
    public void The_count_of_older_sessions_does_not_depend_on_the_depth()
    {
        _library.Wipe = new Wipe(Wiped, "Alpha 4.8", WipeScope.Money);

        Assert.Equal(2, _library.SessionsBeforeWipe());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _store.Dispose();
    }
}
