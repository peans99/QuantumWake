using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// What the app is busy with, so a page can say so.
/// </summary>
/// <remarks>
/// Two of the four long jobs reported nothing at all before this: a UEX refresh
/// fires at startup and every fifteen minutes, and a community download moves
/// 50 MB. An app that looks idle while it works and empty while it fills is
/// indistinguishable from a broken one.
/// </remarks>
public class BackgroundWorkTests
{
    [Fact]
    public void Nothing_is_running_to_begin_with()
    {
        Assert.Empty(new BackgroundWork().Running());
    }

    [Fact]
    public void A_job_runs_until_its_handle_is_disposed()
    {
        var work = new BackgroundWork();

        var handle = work.Begin("prices", "Refreshing prices from UEX");
        Assert.Equal("Refreshing prices from UEX", Assert.Single(work.Running()).Label);

        handle.Dispose();
        Assert.Empty(work.Running());
    }

    /// <summary>
    /// The case nobody writes by hand: a download that throws. A using block
    /// ends the job on the way out, where a Finish call at the bottom of the
    /// method would leave the strip claiming work that died minutes ago.
    /// </summary>
    [Fact]
    public void A_job_that_throws_still_ends()
    {
        var work = new BackgroundWork();

        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var _ = work.Begin("community", "Downloading the community dataset");
            throw new InvalidOperationException("the download failed");
        }));

        Assert.Empty(work.Running());
    }

    /// <summary>
    /// Oldest first, so a second job appearing does not reorder the strip under
    /// somebody who is reading it.
    /// </summary>
    [Fact]
    public void Jobs_are_reported_oldest_first()
    {
        var work = new BackgroundWork();

        using var first = work.Begin("community", "Downloading the community dataset");
        Thread.Sleep(10);
        using var second = work.Begin("prices", "Refreshing prices from UEX");

        Assert.Equal(["community", "prices"], work.Running().Select(w => w.Key));
    }

    /// <summary>
    /// Disposing twice is not two jobs ending. A stale handle must not clear a
    /// job of the same name that has since restarted — this fires every fifteen
    /// minutes, so the same key comes round again and again.
    /// </summary>
    [Fact]
    public void A_handle_disposed_twice_does_not_clear_a_later_job()
    {
        var work = new BackgroundWork();

        var stale = work.Begin("prices", "Refreshing prices from UEX");
        stale.Dispose();

        using var current = work.Begin("prices", "Refreshing prices from UEX");
        stale.Dispose();

        Assert.Single(work.Running());
    }
}
