using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The fracture HUD's charge against its optimal window, read from pixels,
/// and what the mining watch says about it. Frames are painted with the
/// colours measured on Elguapoe's MOLE stream of 2026-08-31: the bar's yellow
/// 230/205/70, its fill green 172/254/122, Daymar's sand 232/115/53.
/// </summary>
public sealed class FractureTests
{
    private const int W = 1000, H = 760, Left = 1100, Top = 300;

    private static readonly (byte R, byte G, byte B) Sand = (232, 115, 53), Yellow = (230, 205, 70), Green = (172, 254, 122);

    /// <summary>
    /// A 2560x1440 frame's search area with the bar at (1585, 692) 60 x 24 as
    /// measured, filled <paramref name="fill"/> from the left, and the charge
    /// arc's lane right of it lit from <paramref name="tipY"/> down.
    /// </summary>
    private static ColourPatch Frame(double fill = 0, int? tipY = null, bool bar = true)
    {
        var bgra = new byte[W * H * 4];
        void Put(int x, int y, (byte R, byte G, byte B) c)
        {
            var i = ((y - Top) * W + (x - Left)) * 4;
            bgra[i] = c.B; bgra[i + 1] = c.G; bgra[i + 2] = c.R; bgra[i + 3] = 255;
        }
        for (var y = Top; y < Top + H; y++) for (var x = Left; x < Left + W; x++) Put(x, y, Sand);

        if (bar)
            for (var y = 692; y < 716; y++)
                for (var x = 1585; x < 1645; x++)
                    Put(x, y, x - 1585 < 60 * fill ? Green : Yellow);

        // The arc's lane: 3 px wide, just right of the bar past its tick marks.
        if (tipY is { } tip)
            for (var y = tip; y < 1000; y++)
                for (var x = 1660; x < 1663; x++)
                    Put(x, y, Green);

        return new ColourPatch(Left, Top, W, H, bgra);
    }

    [Fact]
    public void Charge_under_the_window_says_how_far_under()
    {
        var read = ChargeWindow.Read(Frame(tipY: 850), 1440);

        Assert.NotNull(read);
        Assert.Equal("below", read.State);
        Assert.InRange(read.BelowBy!.Value, 5, 6.5);   // the stream read 5.4-5.9 bars at 01:09:55
        Assert.InRange(read.Bar.Left, 1580, 1590);
    }

    [Fact]
    public void Charge_level_with_the_bar_is_in_the_window_and_the_fill_is_the_crack()
    {
        var read = ChargeWindow.Read(Frame(fill: 0.5, tipY: 712), 1440);

        Assert.NotNull(read);
        Assert.Equal("in", read.State);
        Assert.InRange(read.Fill, 0.45, 0.55);
    }

    [Fact]
    public void Charge_above_the_bar_is_over_the_window()
    {
        var read = ChargeWindow.Read(Frame(tipY: 640), 1440);
        Assert.Equal("above", read!.State);
    }

    [Fact]
    public void Sand_alone_is_no_window_at_all() =>
        Assert.Null(ChargeWindow.Read(Frame(bar: false, tipY: 800), 1440));

    // ---- the watch ----

    private static readonly DateTimeOffset T0 = new(2026, 8, 31, 2, 10, 0, TimeSpan.Zero);

    private static ChargeReading R(string state, double fill = 0, double? below = null) =>
        new(state, fill, below, new ScreenPatch(1585, 692, 60, 24));

    private static List<string> Run(params ChargeReading?[] frames)
    {
        var watch = new MiningWatch();
        return [.. frames.SelectMany((f, i) => watch.See(f, T0.AddSeconds(i))).Select(c => c.Text)];
    }

    [Fact]
    public void A_rock_charged_into_the_window_and_cracked_is_two_callouts()
    {
        // The rock at 01:09:55: charging, fourteen seconds in the window
        // filling to 96%, then the bar gone.
        var said = Run(R("below", 0, 5.8), R("below", 0, 2.5), R("below", 0, 0.6),
            R("in", 0.03), R("in", 0.14), R("in", 0.5), R("in", 0.96), null, null);

        Assert.Equal(["In the window", "Rock cracked"], said);
    }

    [Fact]
    public void A_bar_let_go_before_it_filled_is_no_crack() =>
        Assert.Equal(["In the window"], Run(R("in", 0.1), R("in", 0.2), null, null));

    [Fact]
    public void One_frame_of_a_window_is_not_a_fracture()
    {
        // The ship's floor and the cargo arc each made a bar for one frame.
        Assert.Empty(Run(null, R("above"), null, null, R("in"), null, null));
    }

    [Fact]
    public void Leaving_the_window_and_going_over_it_are_said()
    {
        Assert.Equal(["In the window", "Out of the window"], Run(R("in"), R("in"), R("below", 0, 1), R("below", 0, 1.2)));
        Assert.Equal(["Overcharge, back off"], Run(R("above"), R("above")));
    }

    [Fact]
    public void The_picture_counts_cracks_and_shows_the_charge()
    {
        var watch = new MiningWatch();
        foreach (var (f, i) in new ChargeReading?[] { R("in", 0.9), R("in", 0.95), null, null, R("below", 0, 3), R("below", 0, 3) }.Select((f, i) => (f, i)))
            watch.See(f, T0.AddSeconds(i));

        var picture = watch.Picture();
        Assert.True(picture.Active);
        Assert.Equal("below", picture.State);
        Assert.Equal(3, picture.BelowBy);
        Assert.Equal(1, picture.Cracked);
    }
}
