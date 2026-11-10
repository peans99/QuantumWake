using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The fight record (1.2.21): what a lock leaves behind when it ends, what is
/// never written down, and how locks and callouts make fights.
/// </summary>
public sealed class CombatLogTests : IDisposable
{
    private static readonly ShipSignatureBook Book = ShipSignatureBook.Of(
    [
        new("Vanduul Blade", 10418, "VNCL_Blade"), new("Vanduul Glaive", 11922, "VNCL_Glaive"),
        new("Anvil Paladin", 28580, "ANVL_Paladin"), new("RSI Perseus", 35430, "RSI_Perseus"),
    ]);

    private static readonly DateTimeOffset T0 = new(2026, 11, 5, 21, 30, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "qw-combat-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static TargetReading Panel(string? name, double? speed, double? cs, double range = 2.0, string? faction = null) =>
        new(name, name is null, faction, speed, range, null, null, cs, new ScreenPatch(2400, 300, 640, 250));

    [Fact]
    public void A_lock_that_ends_leaves_its_name_closest_range_top_speed_and_a_stop()
    {
        var watch = new CombatWatch(Book);
        watch.See(Panel("VANDUUL BLADE", 220, 10400, 3.1, "Vanduul"), TargetStance.Hostile, T0);
        watch.See(Panel("VANDUUL BLADE", 260, 10400, 0.8, "Vanduul"), TargetStance.Hostile, T0.AddSeconds(1));
        watch.See(Panel("VANDUUL BLADE", 0, 10400, 0.6, "Vanduul"), TargetStance.Hostile, T0.AddSeconds(2));
        watch.See(Panel("VANDUUL BLADE", 0, 10400, 0.6, "Vanduul"), TargetStance.Hostile, T0.AddSeconds(3));

        Assert.Empty(watch.TakeEnded(T0.AddSeconds(5)));   // still inside the grace
        var ended = Assert.Single(watch.TakeEnded(T0.AddSeconds(20)));

        Assert.Equal("Vanduul Blade", ended.Name);
        Assert.Equal("VNCL_Blade", ended.Class);
        Assert.Equal("Vanduul", ended.Faction);
        Assert.Equal("hostile", ended.Stance);
        Assert.Equal(0.6, ended.ClosestKm);
        Assert.Equal(260, ended.TopSpeedMs);
        Assert.True(ended.Stopped);
        Assert.Equal(4, ended.Reads);
        Assert.Equal(T0, ended.Since);
        Assert.Equal(T0.AddSeconds(3), ended.Until);
        Assert.Empty(watch.TakeEnded(T0.AddSeconds(30)));  // taken once
    }

    [Fact]
    public void A_name_that_is_not_a_hull_is_never_written_down()
    {
        // The panel labels a player's ship with the player: KISHINEREN read
        // twice alike is a handle, and the record keeps the signature's guess.
        var watch = new CombatWatch(Book);
        watch.See(Panel("KISHINEREN", 200, 10400), TargetStance.Unread, T0);
        watch.See(Panel("KISHINEREN", 200, 10400), TargetStance.Unread, T0.AddSeconds(1));

        var ended = Assert.Single(watch.Flush());
        Assert.Null(ended.Name);
        Assert.DoesNotContain("KISHINEREN", System.Text.Json.JsonSerializer.Serialize(ended), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_org_on_the_faction_line_is_not_written_down_and_the_games_factions_are()
    {
        var watch = new CombatWatch(Book);
        watch.See(Panel(null, 200, 28500, faction: "Night Riders Collective"), TargetStance.Unread, T0);
        watch.See(Panel(null, 200, 28500, faction: "Night Riders Collective"), TargetStance.Unread, T0.AddSeconds(1));

        Assert.Null(Assert.Single(watch.Flush()).Faction);
    }

    [Fact]
    public void An_unnamed_lock_keeps_the_likely_hull_and_its_odds()
    {
        var watch = new CombatWatch(Book);
        watch.See(Panel(null, 200, 35400), TargetStance.Unread, T0);
        watch.See(Panel(null, 200, 35400), TargetStance.Unread, T0.AddSeconds(1));

        var ended = Assert.Single(watch.Flush());
        Assert.Null(ended.Name);
        Assert.Equal("RSI Perseus", ended.Likely);
        Assert.Equal("RSI_Perseus", ended.Class);
        Assert.True(ended.Chance >= 0.6);
    }

    [Fact]
    public void A_lock_read_once_is_not_kept()
    {
        var watch = new CombatWatch(Book);
        watch.See(Panel("VANDUUL BLADE", 200, 10400), TargetStance.Unread, T0);
        Assert.Empty(watch.Flush());
    }

    [Fact]
    public void A_new_ship_ends_the_lock_before_it()
    {
        var watch = new CombatWatch(Book);
        watch.See(Panel("VANDUUL BLADE", 200, 10400), TargetStance.Unread, T0);
        watch.See(Panel("VANDUUL BLADE", 200, 10400), TargetStance.Unread, T0.AddSeconds(1));
        watch.See(Panel("ANVIL PALADIN", 90, 28500), TargetStance.Unread, T0.AddSeconds(2));

        Assert.Equal("Vanduul Blade", Assert.Single(watch.TakeEnded(T0.AddSeconds(2))).Name);
    }

    [Fact]
    public void Locks_and_callouts_close_together_are_one_fight_and_a_quiet_gap_starts_another()
    {
        var log = new CombatLog(_folder);
        log.Add(new CombatCallout(T0.AddSeconds(-20), "territory", "Entering hostile territory"));
        log.Add(Lock(T0, 30, "Vanduul Blade", "hostile"));
        log.Add(new CombatCallout(T0.AddSeconds(40), "missile", "Missile inbound"));
        log.Add(new CombatCallout(T0.AddSeconds(50), "objective-health", "Objective health below 50 percent"));
        log.Add(new CombatCallout(T0.AddSeconds(70), "objective-health", "Objective health below 25 percent"));
        log.Add(Lock(T0.AddSeconds(90), 20, null, "unread", likely: "Vanduul Glaive"));
        log.Add(Lock(T0.AddMinutes(10), 15, "Anvil Paladin", "neutral"));

        var fights = CombatLog.Fights(log.Between(T0.AddHours(-1), T0.AddHours(1)));

        Assert.Equal(2, fights.Count);
        var first = fights[0];
        Assert.Equal(T0.AddSeconds(-20), first.Start);   // the banner joins the fight it came before
        Assert.Equal(T0.AddSeconds(110), first.End);
        Assert.Equal(2, first.Locks.Count);
        Assert.Equal(1, first.Named);
        Assert.Equal(1, first.Likely);
        Assert.Equal(1, first.Hostile);
        Assert.Equal(1, first.Missiles);
        Assert.Equal(25, first.LowestObjective);
        Assert.Single(fights[1].Locks);
        Assert.Null(fights[1].LowestObjective);
    }

    [Fact]
    public void The_territory_banner_alone_is_not_a_fight()
    {
        // Hostile territory is where rocks are mined in peace too.
        var log = new CombatLog(_folder);
        log.Add(new CombatCallout(T0, "territory", "Entering hostile territory"));
        Assert.Empty(CombatLog.Fights(log.Between(T0.AddHours(-1), T0.AddHours(1))));
    }

    [Fact]
    public void Only_the_window_asked_about_is_read_and_a_torn_line_is_skipped()
    {
        var log = new CombatLog(_folder);
        log.Add(Lock(T0, 10, "Vanduul Blade", "hostile"));
        log.Add(Lock(T0.AddDays(-3), 10, "Anvil Paladin", "hostile"));
        File.AppendAllText(Directory.GetFiles(_folder).Single(f => f.EndsWith($"{T0.ToLocalTime():yyyy-MM-dd}.jsonl")), "{\"lock\":{\"since\":");

        var read = log.Between(T0.AddHours(-1), T0.AddHours(1));
        Assert.Equal("Vanduul Blade", Assert.Single(read).Lock!.Name);
        Assert.True(log.Any());
        Assert.Equal(2, log.Forget());
        Assert.False(log.Any());
    }

    private static LockRecord Lock(DateTimeOffset since, int seconds, string? name, string stance, string? likely = null) =>
        new(since, since.AddSeconds(seconds), name, likely, likely is null ? null : 0.7, null, null, stance,
            10400, 1.2, 250, null, false, 5);
}
