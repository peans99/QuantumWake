using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The one preference that lets the app reach the network unattended, so the
/// interesting cases are all the ones where it must decide not to.
/// </summary>
public class TradeDataStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-trade-{Guid.NewGuid():N}");

    private TradeDataStore New() => new(_directory);

    private static readonly DateTimeOffset Now =
        new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Nothing_is_due_until_somebody_says_yes()
    {
        var store = New();

        Assert.False(store.Current.Asked);
        Assert.False(store.Current.Automatic);

        // Prices a week old, and still no: the app has not been given leave.
        Assert.False(store.IsDue(Now.AddDays(-7), Now));
    }

    /// <summary>
    /// Turning this on says "keep my prices current", not "fetch prices I never
    /// asked for". With UEX off there is no fetch time at all, and that must not
    /// be read as infinitely stale.
    /// </summary>
    [Fact]
    public void Automatic_does_not_enable_UEX_by_the_back_door()
    {
        var store = New();
        store.Answer(automatic: true);

        Assert.False(store.IsDue(fetchedAt: null, Now));
    }

    [Fact]
    public void Fresh_prices_are_left_alone()
    {
        var store = New();
        store.Answer(automatic: true);

        Assert.False(store.IsDue(Now - TradeDataStore.StaleAfter + TimeSpan.FromMinutes(1), Now));
    }

    [Fact]
    public void Stale_prices_are_due()
    {
        var store = New();
        store.Answer(automatic: true);

        Assert.True(store.IsDue(Now - TradeDataStore.StaleAfter, Now));
    }

    /// <summary>
    /// A failed attempt still counts as an attempt. Without this, prices that
    /// are stale and a feed that is down combine into a fetch on every tick for
    /// as long as the app stays open.
    /// </summary>
    [Fact]
    public void A_failed_attempt_backs_off_rather_than_retrying_at_the_tick()
    {
        var store = New();
        store.Answer(automatic: true);

        var stale = Now.AddDays(-1);
        Assert.True(store.IsDue(stale, Now));

        // The refresher records the attempt, then the fetch throws: prices are
        // as old as they were, so staleness alone would say "due" again.
        store.Checked(Now);

        Assert.False(store.IsDue(stale, Now));
        Assert.False(store.IsDue(stale, Now + TradeDataStore.RetryAfter - TimeSpan.FromMinutes(1)));
        Assert.True(store.IsDue(stale, Now + TradeDataStore.RetryAfter));
    }

    /// <summary>Saying no is an answer, and must not be asked again.</summary>
    [Fact]
    public void Refusing_still_counts_as_having_been_asked()
    {
        var store = New();
        store.Answer(automatic: false);

        Assert.True(store.Current.Asked);
        Assert.False(store.Current.Automatic);
    }

    [Fact]
    public void The_answer_survives_a_restart()
    {
        New().Answer(automatic: true);

        var reopened = New();

        Assert.True(reopened.Current.Asked);
        Assert.True(reopened.Current.Automatic);
        Assert.True(reopened.IsDue(Now.AddDays(-1), Now));
    }

    /// <summary>
    /// A file that cannot be read must fail towards asking again, never towards
    /// a fetch nobody agreed to.
    /// </summary>
    [Fact]
    public void A_corrupt_preference_reverts_to_never_having_been_asked()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "trade-data.json"), "{ not json");

        var store = New();

        Assert.False(store.Current.Asked);
        Assert.False(store.Current.Automatic);
        Assert.False(store.IsDue(Now.AddDays(-7), Now));
    }
}
