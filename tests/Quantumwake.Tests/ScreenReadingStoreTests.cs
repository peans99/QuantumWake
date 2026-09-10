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

    /// <param name="drift">
    /// Moves the reading, for the cases that need each paste to be a different
    /// one: identical coordinates are taken for the same paste still sitting on
    /// the clipboard, which is the whole point of the dedupe below.
    /// </param>
    private static ClipboardSighting Paste(
        DateTimeOffset at, string? believed = "Ruin Station", double drift = 0) =>
        new(at, -9641671346.9 + drift, -11490734321.2, -91805.1, 14.99996, believed, believed is null ? null : "Pyro");

    [Fact]
    public void Pastes_come_back_newest_first_and_survive_a_restart()
    {
        var store = new ScreenReadingStore(_dir);
        store.AddClipboard(Paste(At.AddMinutes(-5), drift: 5000));
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
            store.AddClipboard(Paste(At.AddSeconds(i), drift: i));

        Assert.Equal(ScreenReadingStore.Keep, store.Clipboards().Count);
    }

    /// <summary>
    /// The watcher reads the clipboard every three seconds and the clipboard
    /// keeps what was copied until something else is copied, so one paste would
    /// otherwise become a row every three seconds - filling the bound above
    /// with three hundred copies of itself in a quarter of an hour and throwing
    /// away every genuinely different paste to do it.
    /// </summary>
    [Fact]
    public void The_same_paste_read_again_is_not_stored_again()
    {
        var store = new ScreenReadingStore(_dir);

        Assert.True(store.AddClipboard(Paste(At)));
        Assert.False(store.AddClipboard(Paste(At.AddSeconds(3))));
        Assert.False(store.AddClipboard(Paste(At.AddSeconds(6))));

        Assert.Single(store.Clipboards());
        Assert.Equal(At, store.Clipboards()[0].At);
    }

    [Fact]
    public void A_paste_from_somewhere_else_is_stored()
    {
        var store = new ScreenReadingStore(_dir);

        store.AddClipboard(Paste(At));
        Assert.True(store.AddClipboard(Paste(At.AddSeconds(3), drift: 5000)));

        Assert.Equal(2, store.Clipboards().Count);
    }

    /// <summary>
    /// Copying the same place again after going elsewhere is a new paste: only
    /// the row on top is compared, because only that one can be the clipboard
    /// still holding what it held three seconds ago.
    /// </summary>
    [Fact]
    public void The_same_place_copied_again_later_is_stored()
    {
        var store = new ScreenReadingStore(_dir);

        store.AddClipboard(Paste(At));
        store.AddClipboard(Paste(At.AddMinutes(1), drift: 5000));

        Assert.True(store.AddClipboard(Paste(At.AddMinutes(2))));
        Assert.Equal(3, store.Clipboards().Count);
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
