using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Remembering what the screenshots said.
/// </summary>
public class ScreenReadingStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "qw-screen-readings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static readonly DateTimeOffset At = new(2026, 9, 7, 19, 30, 17, TimeSpan.Zero);

    private static ScreenSighting Sighting(string shot, DateTimeOffset at, ScreenKind kind = ScreenKind.Map,
        LoadoutReading? loadout = null, WalletReading? wallet = null) =>
        new(shot, at, kind, "summary", [], null, loadout, null, wallet, ["a line"], 170);

    [Fact]
    public void Readings_come_back_newest_first_and_survive_a_restart()
    {
        var store = new ScreenReadingStore(_dir);
        store.Add(Sighting("a.jpg", At.AddMinutes(-5)));
        store.Add(Sighting("b.jpg", At));

        var again = new ScreenReadingStore(_dir);

        Assert.Equal(["b.jpg", "a.jpg"], again.All().Select(s => s.Shot));
        Assert.Equal("b.jpg", again.Latest?.Shot);
        Assert.True(again.Has("A.JPG"));
        Assert.False(again.Has("c.jpg"));
    }

    [Fact]
    public void Reading_the_same_file_again_replaces_rather_than_duplicates()
    {
        var store = new ScreenReadingStore(_dir);
        store.Add(Sighting("a.jpg", At));
        store.Add(Sighting("a.jpg", At) with { Summary = "read again" });

        var only = Assert.Single(store.All());
        Assert.Equal("read again", only.Summary);
    }

    [Fact]
    public void The_store_is_bounded()
    {
        var store = new ScreenReadingStore(_dir);

        for (var i = 0; i < ScreenReadingStore.Keep + 20; i++)
            store.Add(Sighting($"{i}.jpg", At.AddSeconds(i)));

        Assert.Equal(ScreenReadingStore.Keep, store.All().Count);
        Assert.Equal($"{ScreenReadingStore.Keep + 19}.jpg", store.Latest?.Shot);
    }

    [Fact]
    public void The_last_wallet_that_actually_read_is_the_baseline()
    {
        var store = new ScreenReadingStore(_dir);
        store.Add(Sighting("a.jpg", At.AddMinutes(-10), wallet: new WalletReading(1_000_000, null)));
        store.Add(Sighting("b.jpg", At, wallet: new WalletReading(null, "did not read")));

        var baseline = store.LastWallet();

        Assert.NotNull(baseline);
        Assert.Equal(1_000_000, baseline.Balance);
        Assert.Equal(At.AddMinutes(-10), baseline.At);
    }

    [Fact]
    public void The_newest_loadout_per_ship_is_what_the_fleet_page_gets()
    {
        var store = new ScreenReadingStore(_dir);

        LoadoutReading Corsair(string part) =>
            new("DRAKE CORSAIR", "Drake Corsair", [], null, [new ScreenFitting("Cooler 1", part, part, null, "Exact", [], [])]);

        store.Add(Sighting("old.jpg", At.AddDays(-1), ScreenKind.Loadout, Corsair("Frost-Star")));
        store.Add(Sighting("new.jpg", At, ScreenKind.Loadout, Corsair("Frost-Star EX")));
        store.Add(Sighting("unnamed.jpg", At.AddHours(1), ScreenKind.Loadout,
            new LoadoutReading("DUKE CORSAIR", null, ["Drake Corsair"], null, [])));

        var latest = Assert.Single(store.LatestLoadouts());

        Assert.Equal("new.jpg", latest.Shot);
        Assert.Equal("Frost-Star EX", latest.Loadout!.Fittings[0].Name);
    }

    // ---- what was pasted ----

    private static ClipboardSighting Paste(DateTimeOffset at, string? believed = "Ruin Station") =>
        new(at, -9641671346.9, -11490734321.2, -91805.1, 14.99996, believed, believed is null ? null : "Pyro");

    [Fact]
    public void Pastes_come_back_newest_first_and_survive_a_restart()
    {
        var store = new ScreenReadingStore(_dir);
        store.AddClipboard(Paste(At.AddMinutes(-5)));
        store.AddClipboard(Paste(At));

        var again = new ScreenReadingStore(_dir);

        Assert.Equal([At, At.AddMinutes(-5)], again.Clipboards().Select(p => p.At));
        Assert.Equal("Ruin Station", again.Clipboards()[0].Believed);
    }

    /// <summary>
    /// Its own file: a paste and a screenshot have nothing in common but the
    /// panel they end up on, and one being unreadable must not take the other.
    /// </summary>
    [Fact]
    public void A_ruined_screenshot_file_does_not_take_the_pastes_with_it()
    {
        var store = new ScreenReadingStore(_dir);
        store.Add(Sighting("a.jpg", At));
        store.AddClipboard(Paste(At));

        File.WriteAllText(Path.Combine(_dir, "screen-readings.json"), "{ not json");

        var again = new ScreenReadingStore(_dir);

        Assert.Empty(again.All());
        Assert.Single(again.Clipboards());
    }

    [Fact]
    public void The_paste_history_is_bounded_like_the_rest()
    {
        var store = new ScreenReadingStore(_dir);

        for (var i = 0; i < ScreenReadingStore.Keep + 10; i++)
            store.AddClipboard(Paste(At.AddSeconds(i)));

        Assert.Equal(ScreenReadingStore.Keep, store.Clipboards().Count);
    }

    [Fact]
    public void Repeated_clipboard_checks_refresh_one_location_instead_of_filling_the_log()
    {
        var store = new ScreenReadingStore(_dir);
        store.AddClipboard(Paste(At), mergeWithLatest: true);
        store.AddClipboard(Paste(At.AddSeconds(3)), mergeWithLatest: true);

        var only = Assert.Single(store.Clipboards());
        Assert.Equal(At, only.At);
        Assert.Equal(2, only.TimesSeen);
        Assert.Equal(At.AddSeconds(3), only.LastSeenAt);

        store.AddClipboard(Paste(At.AddSeconds(6)) with { X = 42 });
        Assert.Equal(2, store.Clipboards().Count);
    }

    [Fact]
    public void A_paste_can_be_kept_as_a_point_of_interest_after_the_log_is_cleared()
    {
        var store = new ScreenReadingStore(_dir);
        store.AddClipboard(Paste(At));

        var pin = store.Pin(At);
        store.Clear();

        Assert.NotNull(pin);
        Assert.Equal(At, pin.SourceAt);
        Assert.Equal(14.99996, pin.Gigametres);
        Assert.Empty(store.Clipboards());
        Assert.Single(new ScreenReadingStore(_dir).Pinned());
    }

    [Fact]
    public void Pinning_the_same_clipboard_reading_twice_keeps_one_point()
    {
        var store = new ScreenReadingStore(_dir);
        store.AddClipboard(Paste(At));

        var first = store.Pin(At);
        var again = store.Pin(At);

        Assert.Equal(first, again);
        Assert.Single(store.Pinned());
        Assert.Null(store.Pin(At.AddMinutes(1)));
    }

    [Fact]
    public void A_pinned_point_can_be_removed_without_losing_its_log_entry()
    {
        var store = new ScreenReadingStore(_dir);
        store.AddClipboard(Paste(At));
        store.Pin(At);

        Assert.True(store.Unpin(At));
        Assert.False(store.Unpin(At));
        Assert.Empty(store.Pinned());
        Assert.Single(store.Clipboards());
    }

    [Fact]
    public void A_pinned_point_keeps_its_pilot_name_and_category_after_a_restart()
    {
        var store = new ScreenReadingStore(_dir);
        store.AddClipboard(Paste(At));
        store.Pin(At);

        var updated = store.UpdatePin(At, "Ruin mining shelf", "Mining");
        var again = new ScreenReadingStore(_dir);

        Assert.NotNull(updated);
        Assert.Equal("Ruin mining shelf", updated.Label);
        Assert.Equal("Mining", updated.Category);
        Assert.Equal(updated, Assert.Single(again.Pinned()));
    }

    [Fact]
    public void Clearing_takes_both_halves()
    {
        var store = new ScreenReadingStore(_dir);
        store.Add(Sighting("a.jpg", At));
        store.AddClipboard(Paste(At));

        store.Clear();

        Assert.Empty(store.All());
        Assert.Empty(store.Clipboards());
        Assert.Empty(new ScreenReadingStore(_dir).Clipboards());
    }

    [Fact]
    public void Screenshot_watching_is_only_a_setting_in_the_mode_that_allows_it()
    {
        Assert.True(ScreenSettings.Clean(ScreenMode.Screenshots, false, true).WatchScreenshots);
        Assert.False(ScreenSettings.Clean(ScreenMode.CopyOnly, false, true).WatchScreenshots);
        Assert.False(ScreenSettings.Clean(ScreenMode.Off, true, true).WatchScreenshots);
        Assert.False(ScreenSettings.Clean(ScreenMode.Screenshots, false, null).WatchScreenshots);
    }
}
