using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Which screenshots the folder watch reads, and which it leaves alone.
/// </summary>
public class ScreenFolderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 19, 30, 30, TimeSpan.Zero);
    private static readonly DateTimeOffset Baseline = Now.AddMinutes(-10);

    private static ScreenFile Shot(string name, TimeSpan ago, long length = 800_000) =>
        new($@"E:\rsi\StarCitizen\LIVE\screenshots\{name}", length, Now - ago);

    [Fact]
    public void A_file_still_being_written_waits_until_it_has_settled()
    {
        var fresh = Shot("ScreenShot-a.jpg", TimeSpan.FromMilliseconds(300));
        var settled = Shot("ScreenShot-b.jpg", TimeSpan.FromSeconds(5));

        var ready = ScreenFolder.Ready([fresh, settled], Baseline, Now, _ => false);

        Assert.Equal([settled], ready);
    }

    [Fact]
    public void Nothing_from_before_the_watch_began_is_read()
    {
        var archive = Shot("ScreenShot-old.jpg", TimeSpan.FromDays(3));
        var since = Shot("ScreenShot-new.jpg", TimeSpan.FromSeconds(30));

        var ready = ScreenFolder.Ready([archive, since], Baseline, Now, _ => false);

        Assert.Equal([since], ready);
    }

    [Fact]
    public void A_file_already_read_is_not_read_again()
    {
        var shot = Shot("ScreenShot-a.jpg", TimeSpan.FromSeconds(30));

        var ready = ScreenFolder.Ready([shot], Baseline, Now, path => path.EndsWith("a.jpg"));

        Assert.Empty(ready);
    }

    [Fact]
    public void An_empty_file_is_not_a_screenshot_yet()
    {
        var empty = Shot("ScreenShot-a.jpg", TimeSpan.FromSeconds(30), length: 0);

        Assert.Empty(ScreenFolder.Ready([empty], Baseline, Now, _ => false));
    }

    [Fact]
    public void Ready_files_come_oldest_first_so_the_readings_land_in_order()
    {
        var later = Shot("ScreenShot-later.jpg", TimeSpan.FromSeconds(10));
        var earlier = Shot("ScreenShot-earlier.jpg", TimeSpan.FromSeconds(90));

        var ready = ScreenFolder.Ready([later, earlier], Baseline, Now, _ => false);

        Assert.Equal([earlier, later], ready);
    }

    [Theory]
    [InlineData("ScreenShot-2026-09-07_21-30-17-B52.jpg", true)]
    [InlineData("shot.PNG", true)]
    [InlineData("shot.jpeg", true)]
    [InlineData("Thumbs.db", false)]
    [InlineData("notes.txt", false)]
    public void Only_image_files_count(string name, bool counts)
    {
        Assert.Equal(counts, ScreenFolder.IsScreenshot(name));
    }
}
