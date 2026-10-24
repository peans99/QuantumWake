using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The Rep app's bars, read off pixels drawn here in the colours measured on
/// fourteen real frames: a card of (56, 74, 87), a track of (66, 92, 113), a
/// teal fill of (144, 255, 212). The geometry is the measured one too - a
/// label 14 px tall, its bar seven rows thick, its top edge eighteen pixels
/// above the label's top, starting just left of the label and 170 px long.
/// </summary>
public class RepBarsTests
{
    private static readonly (byte R, byte G, byte B) Card = (56, 74, 87);
    private static readonly (byte R, byte G, byte B) Track = (66, 92, 113);
    private static readonly (byte R, byte G, byte B) Teal = (144, 255, 212);

    private static readonly ScreenTextLine Label = new("NEUTRAL", 1426, 664, 14);

    /// <summary>A patch around one card with its bar filled to <paramref name="fill"/>.</summary>
    /// <param name="glow">Added to every channel across the right half, as the panel's translucency does.</param>
    private static ColourPatch Drawn(double? fill, int glow = 0)
    {
        const int left = 1300, top = 560, width = 500, height = 120;
        var bytes = new byte[width * height * 4];

        void Paint(int x, int y, (byte R, byte G, byte B) c)
        {
            var lift = x >= 1500 ? glow : 0;
            var at = ((y - top) * width + (x - left)) * 4;
            bytes[at] = (byte)Math.Min(255, c.B + lift);
            bytes[at + 1] = (byte)Math.Min(255, c.G + lift);
            bytes[at + 2] = (byte)Math.Min(255, c.R + lift);
            bytes[at + 3] = 255;
        }

        for (var y = top; y < top + height; y++)
            for (var x = left; x < left + width; x++)
                Paint(x, y, Card);

        if (fill is { } f)
        {
            const int barLeft = 1423, barLength = 170, barTop = 646;
            var reached = barLeft + (int)Math.Round(barLength * f);

            for (var y = barTop; y < barTop + 7; y++)
                for (var x = barLeft; x < barLeft + barLength; x++)
                    Paint(x, y, x < reached ? Teal : Track);
        }

        return new ColourPatch(left, top, width, height, bytes);
    }

    [Theory]
    [InlineData(0.135)]
    [InlineData(0.54)]
    [InlineData(0.759)]
    [InlineData(0.011)]
    public void A_part_filled_bar_reads_to_within_a_pixel(double fill)
    {
        var bar = RepBars.Measure(Drawn(fill), Label)!;

        Assert.Equal(1423, bar.Left);
        Assert.Equal(1592, bar.Right);
        Assert.InRange(bar.Fill, fill - 1.0 / 170, fill + 1.0 / 170);
    }

    [Fact]
    public void An_empty_track_is_nothing_earned_and_a_full_one_is_a_rank_passed()
    {
        Assert.Equal(0, RepBars.Measure(Drawn(0), Label)!.Fill);
        Assert.Equal(1, RepBars.Measure(Drawn(1), Label)!.Fill);
    }

    /// <summary>
    /// The track is twenty-odd levels bluer than the card and a glow behind
    /// the panel lifts a region by as much; measured against its own
    /// neighbours rather than a fixed level, the bar keeps its length.
    /// </summary>
    [Fact]
    public void A_glow_behind_the_panel_does_not_shorten_the_track()
    {
        var bar = RepBars.Measure(Drawn(0.2, glow: 30), Label)!;

        Assert.Equal(1592, bar.Right);
        Assert.InRange(bar.Fill, 0.2 - 1.0 / 170, 0.2 + 1.0 / 170);
    }

    [Fact]
    public void No_bar_where_one_should_be_is_null_rather_than_empty()
    {
        Assert.Null(RepBars.Measure(Drawn(null), Label));
    }

    private static RepRankCard C(string name, double? fill) => new(name, null, fill);

    /// <summary>Red Wind on 24 October: NOT ELIGIBLE empty below three ranks passed, MEMBER part way.</summary>
    [Fact]
    public void The_rank_held_is_the_last_with_teal_even_above_an_empty_first_card()
    {
        var held = RepBars.Held(
        [
            C("NOT ELIGIBLE", 0), C("TRAINEE", 1), C("ROOKIE", 1), C("JUNIOR", 1),
            C("MEMBER", 0.54), C("EXPERIENCED", 0), C("SENIOR", 0),
        ]);

        Assert.Equal((4, 0.54), held);
    }

    [Fact]
    public void A_full_last_bar_is_the_next_rank_just_reached()
    {
        Assert.Equal((2, 0.0), RepBars.Held([C("A", 1), C("B", 1), C("C", 0), C("D", 0)]));
        Assert.Equal((1, 1.0), RepBars.Held([C("A", 1), C("B", 1)]));
    }

    [Fact]
    public void No_teal_anywhere_is_the_first_rank_empty()
    {
        Assert.Equal((0, 0.0), RepBars.Held([C("NEUTRAL", 0), C("JR. CONTRACTOR", 0)]));
    }

    /// <summary>
    /// One frame caught RANK VI sliding in over its neighbour, its bar
    /// elsewhere. Every card after the rank held is empty, so it cannot
    /// change the answer - unless it is the very card that answer depends on.
    /// </summary>
    [Fact]
    public void An_unread_card_after_the_rank_held_does_not_matter_but_one_it_depends_on_does()
    {
        Assert.Equal((2, 0.678), RepBars.Held([C("NOT ELIGIBLE", 0), C("APPLICANT", 1), C("RANK I", 0.678), C("RANK VI", null)]));
        Assert.Null(RepBars.Held([C("APPLICANT", 1), C("RANK I", 1), C("RANK II", null)]));
    }

    /// <summary>Without pixels the frame keeps its text and its rank stays unsaid.</summary>
    [Fact]
    public async Task A_reader_with_no_pixels_leaves_every_bar_unmeasured()
    {
        var cards = await RepBars.MeasureAsync(ScreenAppFixtures.RepRedWind, (_, _) => Task.FromResult<ColourPatch?>(null));

        Assert.Equal(7, cards.Count);
        Assert.All(cards, c => Assert.Null(c.Fill));
        Assert.Null(RepBars.Held(cards));
    }
}
