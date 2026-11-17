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
        Assert.Equal(["In the window"], Run(R("in", 0.1), R("in", 0.2), R("in", 0.3), null, null));

    [Fact]
    public void One_frame_of_a_window_is_not_a_fracture()
    {
        // The ship's floor and the cargo arc each made a bar for one frame.
        Assert.Empty(Run(null, R("above"), null, null, R("in"), null, null));
    }

    [Fact]
    public void Leaving_the_window_and_going_over_it_are_said()
    {
        Assert.Equal(["In the window", "Out of the window"], Run(R("in"), R("in"), R("in"), R("below", 0, 1), R("below", 0, 1.2)));
        Assert.Equal(["In the window", "Overcharge, back off"], Run(R("in"), R("in"), R("in"), R("above"), R("above")));
    }

    [Fact]
    public void An_overcharge_the_charge_never_rose_through_the_window_for_is_not_said()
    {
        // 2026-11-16 22:17, on foot in New Babbage: a yellow box read as over
        // the window with the bar 0% filled, and "Overcharge, back off" was said.
        var watch = new MiningWatch();
        var said = new List<string>();
        var i = 0;
        foreach (var f in new ChargeReading?[] { R("above"), R("above"), R("above"), R("above"), null, null })
            said.AddRange(watch.See(f, T0.AddSeconds(i++)).Select(c => c.Text));

        Assert.Empty(said);
        var window = Assert.Single(watch.TakeStrays());
        Assert.Equal(0, window.Overcharges);
    }

    [Fact]
    public void Going_over_a_bar_already_filling_is_said_even_without_a_frame_in_the_window() =>
        Assert.Equal(["Overcharge, back off"], Run(R("below", 0.1, 0.5), R("below", 0.1, 0.5), R("below", 0.1, 0.5), R("above", 0.2), R("above", 0.2)));

    [Fact]
    public void The_picture_counts_cracks_and_shows_the_charge()
    {
        var watch = new MiningWatch();
        foreach (var (f, i) in new ChargeReading?[] { R("in", 0.5), R("in", 0.9), R("in", 0.95), null, null, R("below", 0, 3), R("below", 0, 3), R("below", 0, 3) }.Select((f, i) => (f, i)))
            watch.See(f, T0.AddSeconds(i));

        var picture = watch.Picture();
        Assert.True(picture.Active);
        Assert.Equal("below", picture.State);
        Assert.Equal(3, picture.BelowBy);
        Assert.Equal(1, picture.Cracked);
    }

    // ---- 1.1.18: mining callouts heard in fights and at a Fleet terminal ----

    private static ChargeReading At(double left, double top, double fill = 0.1) =>
        new("in", fill, null, new ScreenPatch(left, top, 60, 24));

    [Fact]
    public void Two_frames_are_no_longer_enough()
    {
        Assert.Empty(Run(R("in"), R("in"), null, null));
    }

    [Fact]
    public void A_box_that_moves_about_is_not_the_window()
    {
        // A combat marker of the bar's size, tracking a ship across the screen.
        Assert.Empty(Run(At(1585, 692), At(1700, 640), At(1500, 760), At(1620, 600), null, null));
    }

    [Fact]
    public void A_bar_growing_taller_in_place_is_still_the_window()
    {
        var growing = new ChargeReading("in", 0.1, null, new ScreenPatch(1584, 700, 60, 30));
        Assert.Equal(["In the window"], Run(At(1585, 692), At(1585, 695), growing));
    }

    [Fact]
    public void A_solid_green_box_first_seen_full_is_not_a_window_being_filled()
    {
        Assert.Empty(Run(R("in", 1), R("in", 1), R("in", 1), R("in", 1), null, null));
    }

    [Fact]
    public void In_a_fight_a_new_bar_is_not_taken_up_but_a_followed_one_carries_on()
    {
        var watch = new MiningWatch();
        Assert.Empty(new[] { R("in"), R("in"), R("in") }.SelectMany((f, i) => watch.See(f, T0.AddSeconds(i), mayStart: false)));
        Assert.False(watch.Picture().Active);

        var mining = new MiningWatch();
        mining.See(R("in", 0.2), T0);
        mining.See(R("in", 0.4), T0.AddSeconds(1));
        mining.See(R("in", 0.6), T0.AddSeconds(2));
        mining.See(R("in", 0.9), T0.AddSeconds(3), mayStart: false);
        mining.See(null, T0.AddSeconds(4), mayStart: false);
        Assert.Equal(["Rock cracked"], mining.See(null, T0.AddSeconds(5), mayStart: false).Select(c => c.Text));
    }

    // ---- 1.2.41: a "rock cracked" at a Fleet terminal, 2026-11-14 ----

    /// <summary>
    /// What 1.1.18 did with the terminal's bar once it was being followed:
    /// one absence when the whole frame read as a terminal, the bar again on
    /// the two ticks between. Two absences in a row never came, so the window
    /// stayed open for as long as the pilot stood there.
    /// </summary>
    [Fact]
    public void One_absence_in_three_never_ends_a_window()
    {
        var watch = new MiningWatch();
        for (var i = 0; i < 3; i++) watch.See(R("in", 0.5), T0.AddSeconds(i));

        for (var i = 3; i < 60; i++)
            watch.See(i % 3 == 0 ? null : R("in", 0.94), T0.AddSeconds(i));

        Assert.True(watch.Picture().Active);
    }

    [Fact]
    public void A_disowned_bar_is_neither_a_crack_nor_a_rock_worked()
    {
        var watch = new MiningWatch();
        for (var i = 0; i < 3; i++) watch.See(R("in", 0.5), T0.AddSeconds(i));
        watch.See(R("in", 0.94), T0.AddSeconds(3));
        watch.See(R("in", 0.94), T0.AddSeconds(4));

        // The whole frame reads as a terminal with the bar still on it.
        watch.Disown();
        var after = watch.See(null, T0.AddSeconds(6)).Concat(watch.See(null, T0.AddSeconds(8)));

        Assert.Empty(after);
        Assert.False(watch.Picture().Active);
        Assert.Equal(0, watch.Picture().Cracked);
        Assert.Empty(watch.TakeEnded());
    }

    /// <summary>A terminal's bar seen again after being disowned has to earn its three frames again.</summary>
    [Fact]
    public void A_disowned_bar_starts_from_nothing_if_it_is_seen_again()
    {
        var watch = new MiningWatch();
        for (var i = 0; i < 3; i++) watch.See(R("in", 0.5), T0.AddSeconds(i));
        watch.Disown();

        watch.See(R("in", 0.5), T0.AddSeconds(4));
        watch.See(R("in", 0.5), T0.AddSeconds(5));

        Assert.False(watch.Picture().Active);
    }

    /// <summary>mobiGlas opened over a real rock: no bar on the frame, so the window ends and is kept.</summary>
    [Fact]
    public void A_window_that_ends_by_absence_is_still_recorded()
    {
        var watch = new MiningWatch();
        for (var i = 0; i < 3; i++) watch.See(R("in", 0.3), T0.AddSeconds(i));
        watch.See(null, T0.AddSeconds(3));
        watch.See(null, T0.AddSeconds(4));

        Assert.Single(watch.TakeEnded());
    }
}
