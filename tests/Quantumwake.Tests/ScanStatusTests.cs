using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// Claiming the log scan, now that a button can start one.
/// </summary>
/// <remarks>
/// The scan was only ever started by the app itself, once, at boot. A button
/// that anybody can press twice is a different thing: two scans over one
/// library would interleave their progress into a bar that goes backwards,
/// quite apart from reading 400 MB twice to answer one question.
/// </remarks>
public class ScanStatusTests
{
    [Fact]
    public void A_fresh_status_is_not_running()
    {
        Assert.False(new ScanStatus().Running);
    }

    [Fact]
    public void The_first_claim_wins_and_the_second_is_refused()
    {
        var status = new ScanStatus();

        Assert.True(status.TryBegin());
        Assert.False(status.TryBegin());
        Assert.True(status.Running);
    }

    [Fact]
    public void Finishing_lets_the_next_scan_start()
    {
        var status = new ScanStatus();
        status.TryBegin();
        status.Finish();

        Assert.False(status.Running);
        Assert.True(status.TryBegin());
    }

    /// <summary>
    /// A new claim starts from nothing. Carrying the last run's counts into the
    /// next would have the strip opening at 100% and counting down.
    /// </summary>
    [Fact]
    public void A_new_scan_does_not_inherit_the_last_ones_progress()
    {
        var status = new ScanStatus();

        status.TryBegin();
        status.Report(120, 159, "Game.log.41", cached: false);
        status.Finish();

        status.TryBegin();

        var (done, total, file, _) = status.Progress;
        Assert.Equal(0, done);
        Assert.Equal(0, total);
        Assert.Null(file);
    }

    [Fact]
    public void Progress_reports_what_it_was_told()
    {
        var status = new ScanStatus();
        status.TryBegin();
        status.Report(59, 159, "Game.log.41", cached: false);

        var (done, total, file, _) = status.Progress;

        Assert.Equal(59, done);
        Assert.Equal(159, total);
        Assert.Equal("Game.log.41", file);
    }
}
