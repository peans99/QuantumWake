using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Cash on hand: the last balance a screenshot showed, carried forward by the
/// ledger's movements since.
/// </summary>
/// <remarks>
/// The ledger is a movement record and <c>Game.log</c> never states a total,
/// so this is the one figure on the Ledger that does not come from the logs.
/// What is defended is the arithmetic and the provenance: the figure is the
/// screen's, the movement is the ledger's after that moment and not before,
/// and a reading the pilot has set aside carries nothing.
/// </remarks>
public class WalletStandingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "qw-wallet-standing-" + Guid.NewGuid().ToString("N"));
    private readonly SessionStore _sessions = new(":memory:");
    private readonly LogLibrary _library;
    private readonly ScreenReadingStore _readings;

    public WalletStandingTests()
    {
        _library = new LogLibrary(_sessions);
        _readings = new ScreenReadingStore(_dir);
    }

    public void Dispose()
    {
        _sessions.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static readonly DateTimeOffset Shot = new(2026, 9, 11, 0, 53, 54, TimeSpan.Zero);

    private void Trades(params CommodityTrade[] trades) =>
        _sessions.Save(
            new SessionSummary
            {
                Id = "s1",
                SourceFile = "s1.log",
                StartedAt = Shot.AddHours(-2),
                EndedAt = Shot.AddHours(2),
                Handle = "nekron",
                Trades = trades,
            },
            "fingerprint:s1");

    private static CommodityTrade Sale(DateTimeOffset at, decimal amount) =>
        new(at, "SCShop_Admin_lt_base_g", amount, 96, IsSell: true, Mode: null);

    private static CommodityTrade Buy(DateTimeOffset at, decimal amount) =>
        new(at, "SCShop_Admin_lt_base_g", amount, 96, IsSell: false, Mode: null);

    private void Read(string shot, DateTimeOffset at, long? balance, bool dismissed = false) =>
        _readings.Add(new ScreenSighting(shot, at, ScreenKind.Kiosk, "a kiosk", [], null, null, null,
            new WalletReading(balance, balance is null ? "abbreviated" : null), [], 100, Dismissed: dismissed));

    [Fact]
    public void Nothing_read_means_no_standing_rather_than_a_zero()
    {
        Trades(Sale(Shot.AddHours(-1), 100_000));

        Assert.Null(WalletStandings.Now(_readings, _library));
    }

    /// <summary>
    /// This install's own figure, on the day the kiosk first read in full.
    /// The movement before the shot is already inside the balance the screen
    /// showed; only what came after carries it forward.
    /// </summary>
    [Fact]
    public void The_screen_is_the_figure_and_only_the_movement_since_carries_it()
    {
        Trades(
            Sale(Shot.AddHours(-1), 500_000),
            Buy(Shot.AddMinutes(10), 42_000),
            Sale(Shot.AddMinutes(30), 12_000));
        Read("ScreenShot-2026-09-10_20-53-54-CF5.jpg", Shot, 2_092_773);

        var standing = WalletStandings.Now(_readings, _library)!;

        Assert.Equal(2_092_773, standing.Balance);
        Assert.Equal("ScreenShot-2026-09-10_20-53-54-CF5.jpg", standing.Shot);
        Assert.Equal(Shot, standing.ShotAt);
        Assert.Equal(-30_000, standing.MovedSince);
        Assert.Equal(2, standing.MovementsSince);
        Assert.Equal(2_062_773, standing.Estimate);
    }

    [Fact]
    public void With_nothing_moved_since_the_estimate_is_the_figure_itself()
    {
        Trades(Sale(Shot.AddHours(-1), 500_000));
        Read("a.jpg", Shot, 2_092_773);

        var standing = WalletStandings.Now(_readings, _library)!;

        Assert.Equal(0, standing.MovedSince);
        Assert.Equal(0, standing.MovementsSince);
        Assert.Equal(2_092_773, standing.Estimate);
    }

    [Fact]
    public void An_empty_ledger_still_carries_the_figure()
    {
        Read("a.jpg", Shot, 2_092_773);

        var standing = WalletStandings.Now(_readings, _library)!;

        Assert.Equal(2_092_773, standing.Estimate);
        Assert.Equal(0, standing.MovementsSince);
    }

    /// <summary>
    /// A newer reading the pilot has set aside is not the newest word: the one
    /// before it is, and the movement counts from that one.
    /// </summary>
    [Fact]
    public void A_dismissed_reading_gives_way_to_the_one_before_it()
    {
        Trades(Buy(Shot.AddMinutes(10), 42_000));
        Read("older.jpg", Shot, 2_092_773);
        Read("misread.jpg", Shot.AddMinutes(20), 209_277, dismissed: true);

        var standing = WalletStandings.Now(_readings, _library)!;

        Assert.Equal("older.jpg", standing.Shot);
        Assert.Equal(2_092_773, standing.Balance);
        Assert.Equal(-42_000, standing.MovedSince);
    }
}
