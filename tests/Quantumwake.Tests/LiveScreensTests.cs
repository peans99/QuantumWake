using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// With the screen read live, the screens a screenshot was taken of are kept
/// without one: one reading a screen, never from a single frame, and replaced
/// only by a frame that read more or a change seen twice.
/// </summary>
public sealed class LiveScreensTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 7, 20, 0, 0, TimeSpan.Zero);

    private static ScreenSighting Frame(ScreenKind kind, DateTimeOffset at, string summary = "") =>
        new("live-x", at, kind, summary, [], null, null, null, null, [], 300);

    private static ScreenSighting Kiosk(DateTimeOffset at, params (string Name, double? Price)[] rows) =>
        Frame(ScreenKind.Kiosk, at) with
        {
            Kiosk = new KioskReading(true, "Drake Cutlass Black", "Drake Cutlass Black", 0, 46, null,
                [.. rows.Select(r => new KioskRow(r.Name, r.Name, null, null, null, r.Price, null))]),
        };

    private static ScreenSighting Map(DateTimeOffset at, string place) =>
        Frame(ScreenKind.Map, at) with { Map = new MapReading("STANTON", place, null, null, null, null) };

    [Fact]
    public void A_screen_seen_once_is_not_kept()
    {
        var screens = new LiveScreens();
        Assert.Null(screens.Keep(Kiosk(At, ("Gold", 6000)), At));
        Assert.Null(screens.Keep(Frame(ScreenKind.Unknown, At.AddSeconds(2)), At.AddSeconds(2)));
        Assert.Null(screens.Keep(Kiosk(At.AddSeconds(4), ("Gold", 6000)), At.AddSeconds(4)));
    }

    [Fact]
    public void A_screen_seen_twice_running_is_kept_under_when_it_was_kept()
    {
        var screens = new LiveScreens();
        Assert.Null(screens.Keep(Kiosk(At, ("Gold", 6000)), At));
        Assert.Equal("live-20261107-200002", screens.Keep(Kiosk(At.AddSeconds(2), ("Gold", 6000)), At.AddSeconds(2)));
    }

    [Fact]
    public void The_same_screen_again_is_not_kept_twice()
    {
        var screens = new LiveScreens();
        screens.Keep(Kiosk(At, ("Gold", 6000)), At);
        screens.Keep(Kiosk(At.AddSeconds(2), ("Gold", 6000)), At.AddSeconds(2));

        for (var s = 4; s < 60; s += 2)
            Assert.Null(screens.Keep(Kiosk(At.AddSeconds(s), ("Gold", 6000)), At.AddSeconds(s)));
    }

    [Fact]
    public void A_frame_that_read_more_replaces_the_kept_one_under_its_name()
    {
        var screens = new LiveScreens();
        screens.Keep(Kiosk(At, ("Gold", null)), At);
        var kept = screens.Keep(Kiosk(At.AddSeconds(2), ("Gold", null)), At.AddSeconds(2));

        Assert.Equal(kept, screens.Keep(Kiosk(At.AddSeconds(4), ("Gold", 6000), ("Tin", 300)), At.AddSeconds(4)));
        // Less read is not worse news worth keeping.
        Assert.Null(screens.Keep(Kiosk(At.AddSeconds(6), ("Gold", null)), At.AddSeconds(6)));
    }

    [Fact]
    public void A_change_replaces_the_kept_one_only_once_it_reads_the_same_twice()
    {
        var screens = new LiveScreens();
        screens.Keep(Kiosk(At, ("Gold", 6000)), At);
        var kept = screens.Keep(Kiosk(At.AddSeconds(2), ("Gold", 6000)), At.AddSeconds(2));

        // A digit read one way once is the engine, not the kiosk.
        Assert.Null(screens.Keep(Kiosk(At.AddSeconds(4), ("Gold", 6100)), At.AddSeconds(4)));
        Assert.Null(screens.Keep(Kiosk(At.AddSeconds(6), ("Gold", 6000)), At.AddSeconds(6)));

        // The price moved and stayed moved.
        Assert.Null(screens.Keep(Kiosk(At.AddSeconds(8), ("Gold", 6100)), At.AddSeconds(8)));
        Assert.Equal(kept, screens.Keep(Kiosk(At.AddSeconds(10), ("Gold", 6100)), At.AddSeconds(10)));
    }

    [Fact]
    public void Another_place_on_the_map_is_another_reading()
    {
        var screens = new LiveScreens();
        screens.Keep(Map(At, "DAYMAR"), At);
        var daymar = screens.Keep(Map(At.AddSeconds(2), "DAYMAR"), At.AddSeconds(2));
        screens.Keep(Map(At.AddSeconds(4), "YELA"), At.AddSeconds(4));
        var yela = screens.Keep(Map(At.AddSeconds(6), "YELA"), At.AddSeconds(6));

        Assert.NotNull(daymar);
        Assert.NotNull(yela);
        Assert.NotEqual(daymar, yela);
    }

    [Fact]
    public void Two_minutes_away_and_the_screen_is_a_new_reading()
    {
        var screens = new LiveScreens();
        screens.Keep(Kiosk(At, ("Gold", 6000)), At);
        var first = screens.Keep(Kiosk(At.AddSeconds(2), ("Gold", 6000)), At.AddSeconds(2));

        var back = At.AddMinutes(3);
        Assert.Null(screens.Keep(Kiosk(back, ("Gold", 6000)), back));
        var second = screens.Keep(Kiosk(back.AddSeconds(2), ("Gold", 6000)), back.AddSeconds(2));

        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(ScreenKind.Unknown)]
    [InlineData(ScreenKind.MobiGlas)]
    [InlineData(ScreenKind.Tooltip)]
    public void Frames_no_screenshot_reader_would_keep_live_are_never_kept(ScreenKind kind)
    {
        var screens = new LiveScreens();
        for (var s = 0; s < 10; s += 2)
            Assert.Null(screens.Keep(Frame(kind, At.AddSeconds(s), "the same frame"), At.AddSeconds(s)));
    }

    private static ScreenSighting Home(DateTimeOffset at, long? balance) =>
        Frame(ScreenKind.MobiGlas, at, "a mobiGlas screen this app cannot read yet") with
        {
            Wallet = new WalletReading(balance, balance is null ? ScreenFrames.WalletTrouble : null),
        };

    /// <summary>
    /// The home screen is where a pilot glances at their cash, and a
    /// screenshot of it kept the balance: so does a live look, once the same
    /// figure has read twice.
    /// </summary>
    [Fact]
    public void A_balance_on_a_mobiglas_screen_is_kept_once_it_reads_the_same_twice()
    {
        var screens = new LiveScreens();
        Assert.Null(screens.Keep(Home(At, 2_092_773), At));
        // The same screen, a digit misread: not yet a figure to believe.
        Assert.Null(screens.Keep(Home(At.AddSeconds(2), 2_092_778), At.AddSeconds(2)));
        Assert.Null(screens.Keep(Home(At.AddSeconds(4), 2_092_773), At.AddSeconds(4)));
        var kept = screens.Keep(Home(At.AddSeconds(6), 2_092_773), At.AddSeconds(6));
        Assert.Equal("live-20261107-200006", kept);

        // Spent, and seen twice: the same reading, renewed with the new figure.
        Assert.Null(screens.Keep(Home(At.AddSeconds(8), 1_992_773), At.AddSeconds(8)));
        Assert.Equal(kept, screens.Keep(Home(At.AddSeconds(10), 1_992_773), At.AddSeconds(10)));
    }

    [Fact]
    public void A_mobiglas_screen_whose_balance_did_not_read_is_not_kept()
    {
        var screens = new LiveScreens();
        for (var s = 0; s < 10; s += 2)
            Assert.Null(screens.Keep(Home(At.AddSeconds(s), null), At.AddSeconds(s)));
    }

    /// <summary>What the Ledger carries the cash forward from.</summary>
    [Fact]
    public void A_live_balance_is_the_wallet_the_ledger_counts_from()
    {
        var directory = Path.Combine(Path.GetTempPath(), "qw-live-screens-" + Guid.NewGuid().ToString("N"));
        try
        {
            var readings = new ScreenReadingStore(directory);
            var screens = new LiveScreens();
            for (var s = 0; s < 6; s += 2)
            {
                var frame = Home(At.AddSeconds(s), 2_092_773);
                if (screens.Keep(frame, At.AddSeconds(s)) is { } shot)
                    readings.Renew(frame with { Shot = shot });
            }

            var wallet = readings.LastWallet();
            Assert.NotNull(wallet);
            Assert.Equal(2_092_773, wallet.Balance);
            Assert.StartsWith("live-", wallet.Shot);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void A_screen_that_named_nothing_is_not_kept()
    {
        var screens = new LiveScreens();
        var blank = Frame(ScreenKind.Map, At) with { Map = new MapReading(null, null, null, null, null, null) };
        Assert.Null(screens.Keep(blank, At));
        Assert.Null(screens.Keep(blank with { ShotAt = At.AddSeconds(2) }, At.AddSeconds(2)));
    }

    /// <summary>
    /// What the wallet, the fleet and the hauling plan read from: a minute at
    /// a kiosk is one reading in the log, not thirty.
    /// </summary>
    [Fact]
    public void A_minute_at_a_kiosk_is_one_reading_in_the_log()
    {
        var directory = Path.Combine(Path.GetTempPath(), "qw-live-screens-" + Guid.NewGuid().ToString("N"));
        try
        {
            var readings = new ScreenReadingStore(directory);
            var screens = new LiveScreens();

            for (var s = 0; s < 60; s += 2)
            {
                var frame = Kiosk(At.AddSeconds(s), ("Gold", s < 30 ? 6000 : 6100));
                if (screens.Keep(frame, At.AddSeconds(s)) is { } shot)
                    readings.Renew(frame with { Shot = shot });
            }

            var kept = Assert.Single(readings.All());
            Assert.Equal(6100, kept.Kiosk!.Rows[0].Price);
            // What it says is the newest frame's; when it came up is the first kept one's.
            Assert.Equal(At.AddSeconds(32), kept.ShotAt);
            Assert.Equal(At.AddSeconds(2), kept.SeenAt);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// The pilot invalidates a live reading with the game still up; the next
    /// frame that reads more renews it, and it stays invalidated.
    /// </summary>
    [Fact]
    public void A_live_reading_the_pilot_invalidated_stays_invalidated_when_renewed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "qw-live-screens-" + Guid.NewGuid().ToString("N"));
        try
        {
            var readings = new ScreenReadingStore(directory);
            var first = Kiosk(At, ("Gold", null)) with { Shot = "live-20261107-200002" };
            readings.Renew(first);
            readings.Dismiss(first.Shot, true);

            readings.Renew(Kiosk(At.AddSeconds(4), ("Gold", 6000), ("Tin", 300)) with { Shot = first.Shot });

            var kept = Assert.Single(readings.All());
            Assert.True(kept.Dismissed);
            Assert.Equal(2, kept.Kiosk!.Rows.Count);

            // A file read again on a press still starts afresh.
            readings.Add(kept with { Dismissed = false });
            Assert.False(Assert.Single(readings.All()).Dismissed);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }
}
