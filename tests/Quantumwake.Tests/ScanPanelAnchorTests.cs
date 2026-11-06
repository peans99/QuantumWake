using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Finding the mining scan panel for a second look when the first read lost
/// most of it. The lines are Windows' engine's own, on frames of a 2560x1440
/// MOLE stream (Elguapoe, 2026-08-31), where it made a scan of 3 frames in 200.
/// </summary>
public sealed class ScanPanelAnchorTests
{
    private static ScreenTextLine L(double left, double top, double height, string text) => new(text, left, top, height);

    [Theory]
    [InlineData("RESULTS", true)]
    [InlineData("SCAN RESULTS", true)]
    [InlineData("117kb6CAN RESULTS", true)]
    [InlineData("117RKICAN RESULTS", true)]
    [InlineData("CONTRACT RESULTS AND THE LONG TAIL OF A SENTENCE", false)]
    [InlineData("RESISTANCE:", false)]
    public void A_title_with_the_rocks_label_run_into_it_is_still_the_title(string text, bool title) =>
        Assert.Equal(title, ScreenFrames.IsScanTitle(text));

    [Fact]
    public void The_title_puts_the_panel_around_it()
    {
        // 01:09:55 into the stream, a 6,198 kg Ouratite rock.
        var patch = ScreenFrames.ScanTitlePatch([L(1953, 560, 16, "117kb6CAN RESULTS"), L(2121, 633, 14, "6198"), L(2144, 770, 14, "29.09 scu")]);

        Assert.NotNull(patch);
        Assert.True(patch.Left < 1953 && patch.Top < 560 && patch.Top + patch.Height > 900);
    }

    [Fact]
    public void With_no_title_COMPOSITION_and_its_SCU_find_the_panel()
    {
        // 01:13:30: no title read, "COMPOSITION" and "3.39 SCU" were.
        var patch = ScreenFrames.ScanTitlePatch([L(1986, 600, 16, "cilJtitF"), L(2121, 633, 14, "839"), L(2083, 720, 14, "EASY"),
            L(1976, 768, 14, "COMPOSITION"), L(2154, 770, 14, "3.39 SCU")]);

        Assert.NotNull(patch);
        Assert.True(patch.Top < 560 && patch.Left < 1976 && patch.Left + patch.Width > 2154);
    }

    [Fact]
    public void With_neither_the_difficulty_over_its_SCU_finds_the_panel()
    {
        // 01:19:10: COMPOSITION read as "z:coMAOS1it0N"; EASY and "4.22 scu" survived.
        var patch = ScreenFrames.ScanTitlePatch([L(2122, 631, 14, "1036"), L(2117, 689, 15, "21.67"), L(2083, 719, 13, "EASY"),
            L(1964, 763, 18, "z:coMAOS1it0N"), L(2154, 768, 14, "4.22 scu")]);

        Assert.NotNull(patch);
        Assert.True(patch.Top < 560 && patch.Left < 1964 && patch.Left + patch.Width > 2154);
    }

    [Fact]
    public void A_difficulty_word_with_no_SCU_under_it_is_not_the_panel() =>
        Assert.Null(ScreenFrames.ScanTitlePatch([L(900, 300, 14, "EASY"), L(900, 900, 14, "Contracts")]));

    [Fact]
    public void A_settled_second_look_makes_the_frame_a_scan_even_when_the_whole_read_did_not()
    {
        var settled = new MiningScanReading("OURATITE", "Ouratite", 1036, 46, 21.67, 4.22, "EASY", []);
        var frame = ScreenFrames.Read([L(2083, 719, 13, "EASY"), L(2154, 768, 14, "4.22 scu")], [], [], [], settled);

        Assert.Equal(ScreenKind.Mining, frame.Kind);
        Assert.Same(settled, frame.Mining);
    }
}
