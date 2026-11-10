using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>The rock record (1.2.23): what a charge window leaves behind, and the scan it is joined to.</summary>
public sealed class RockLogTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 31, 2, 10, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "qw-rocks-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static ChargeReading R(string state, double fill = 0, double? below = null) =>
        new(state, fill, below, new ScreenPatch(1585, 692, 60, 24));

    private static IReadOnlyList<FractureRecord> Run(params ChargeReading?[] frames)
    {
        var watch = new MiningWatch();
        for (var i = 0; i < frames.Length; i++) watch.See(frames[i], T0.AddSeconds(i));
        return watch.TakeEnded();
    }

    [Fact]
    public void A_cracked_rock_leaves_its_window_time_and_fill()
    {
        // The 01:09:55 rock of the 2026-08-31 stream, as FractureTests has it.
        var window = Assert.Single(Run(R("below", 0, 5.8), R("below", 0, 2.5), R("below", 0, 0.6),
            R("in", 0.03), R("in", 0.14), R("in", 0.5), R("in", 0.96), null, null));

        Assert.True(window.Cracked);
        Assert.Equal(0.96, window.Filled);
        Assert.Equal(0, window.Overcharges);
        Assert.Equal(T0.AddSeconds(2), window.Since);   // taken up on the third still frame
        Assert.Equal(T0.AddSeconds(6), window.Until);
        // A floor: "in" is agreed on its second frame, and counted from then.
        Assert.InRange(window.InWindowSeconds, 1, 4);
    }

    [Fact]
    public void A_bar_let_go_is_kept_as_let_go_with_its_overcharges()
    {
        var window = Assert.Single(Run(R("in", 0.1), R("in", 0.2), R("above", 0.2), R("above", 0.2),
            R("in", 0.3), R("in", 0.3), null, null));

        Assert.False(window.Cracked);
        Assert.Equal(1, window.Overcharges);
        Assert.Equal(0.3, window.Filled);
    }

    [Fact]
    public void A_flicker_that_was_never_followed_leaves_nothing()
    {
        Assert.Empty(Run(null, R("above"), null, null, R("in"), null, null));
    }

    [Fact]
    public void Each_scan_goes_to_the_window_nearest_after_it_and_to_one_window_only()
    {
        var first = new FractureRecord(T0, T0.AddSeconds(40), false, 0.4, 20, 0);
        var second = new FractureRecord(T0.AddSeconds(60), T0.AddSeconds(90), true, 0.95, 25, 1);
        var scan = new MiningScanReading("QUANTAINIUM", "Quantainium", 8400, 22, 21.89, 31.5, "EASY",
            [new MiningScanPart("QUANTAINIUM", "Quantainium", 34, 610)]);
        var readings = new[]
        {
            new ScreenSighting("live-rock-1", T0.AddSeconds(-30), ScreenKind.Mining, "a rock", [], null, null, null, null, [], 0, Mining: scan),
            new ScreenSighting("old-rock", T0.AddMinutes(-20), ScreenKind.Mining, "a rock", [], null, null, null, null, [], 0, Mining: scan),
        };

        var worked = RockLog.WithScans([second, first], readings);

        Assert.Equal([first, second], worked.Select(w => w.Fracture));
        Assert.Equal(8400, worked[0].Scan!.MassKg);
        Assert.Equal(T0.AddSeconds(-30), worked[0].ScannedAt);
        Assert.Null(worked[1].Scan);   // one scan is one rock's; twenty minutes before is another rock
    }

    [Fact]
    public void Windows_are_kept_a_day_a_file_and_read_back_by_time()
    {
        var log = new RockLog(_folder);
        log.Add(new FractureRecord(T0, T0.AddSeconds(30), true, 0.93, 12, 0));
        log.Add(new FractureRecord(T0.AddDays(-2), T0.AddDays(-2).AddSeconds(30), false, 0.2, 0, 2));

        var read = log.Between(T0.AddHours(-1), T0.AddHours(1));
        Assert.True(Assert.Single(read).Cracked);
        Assert.True(log.Any());
    }
}
