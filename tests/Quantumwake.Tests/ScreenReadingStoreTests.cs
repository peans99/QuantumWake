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

    [Fact]
    public void Screenshot_watching_is_only_a_setting_in_the_mode_that_allows_it()
    {
        Assert.True(ScreenSettings.Clean(ScreenMode.Screenshots, false, true).WatchScreenshots);
        Assert.False(ScreenSettings.Clean(ScreenMode.CopyOnly, false, true).WatchScreenshots);
        Assert.False(ScreenSettings.Clean(ScreenMode.Off, true, true).WatchScreenshots);
        Assert.False(ScreenSettings.Clean(ScreenMode.Screenshots, false, null).WatchScreenshots);
    }
}
