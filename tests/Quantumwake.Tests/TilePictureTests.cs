using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Recognising an inventory tile by its picture.
/// </summary>
/// <remarks>
/// <para>
/// The fixtures are drawn rather than photographed, for the reason the OCR
/// suite draws its own: a checked-in screenshot is somebody's actual inventory
/// and the thing being defended here is arithmetic, not art. What is not
/// invented is the shape of the problem. Every distortion below was measured
/// on <c>ScreenShot-2026-09-12_13-51-13-B21.jpg</c> and is written up in
/// <c>docs/screen-insight.md</c>: the panels are drawn on an angled plane, so
/// the same art appears at different scales and offsets, and they are
/// translucent, so the hangar behind them lays a brightness ramp across every
/// tile.
/// </para>
/// <para>
/// The negative tests matter as much as the positive ones. A matcher with too
/// small a search window scored ten out of ten in the middle of a row and
/// failed at both ends, which is the kind of wrong that looks right.
/// </para>
/// </remarks>
public class TilePictureTests
{
    private static readonly string[] Flask =
    [
        "..####..",
        "..#..#..",
        ".##..##.",
        ".#....#.",
        ".#.##.#.",
        ".#.##.#.",
        ".######.",
        "..####..",
    ];

    private static readonly string[] Crate =
    [
        "########",
        "#......#",
        "#.####.#",
        "#.#..#.#",
        "#.#..#.#",
        "#.####.#",
        "#......#",
        "########",
    ];

    /// <summary>A frame of flat panel, ready to be drawn on.</summary>
    private static float[] Panel(int width, int height, float level = 30)
    {
        var pixels = new float[width * height];
        Array.Fill(pixels, level);
        return pixels;
    }

    /// <summary>Draws art into a square, the way a tile renders into one.</summary>
    private static void Draw(
        float[] pixels, int width, string[] art,
        double centreX, double centreY, double half, float ink = 210, float ground = 35)
    {
        var height = pixels.Length / width;

        for (var y = (int)(centreY - half); y <= (int)(centreY + half); y++)
        {
            for (var x = (int)(centreX - half); x <= (int)(centreX + half); x++)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) continue;

                var row = (int)((y - (centreY - half)) / (2 * half) * art.Length);
                var column = (int)((x - (centreX - half)) / (2 * half) * art[0].Length);

                row = Math.Clamp(row, 0, art.Length - 1);
                column = Math.Clamp(column, 0, art[0].Length - 1);

                pixels[y * width + x] = art[row][column] == '#' ? ink : ground;
            }
        }
    }

    /// <summary>What a translucent panel does: a ramp across the whole frame.</summary>
    private static void Ramp(float[] pixels, int width, float from, float to)
    {
        var height = pixels.Length / width;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[y * width + x] += from + (to - from) * x / (width - 1f);
            }
        }
    }

    [Fact]
    public void A_picture_matches_itself_exactly()
    {
        var pixels = Panel(200, 200);
        Draw(pixels, 200, Flask, 100, 100, 40);
        var frame = new GreyImage(200, 200, pixels);

        var patch = TilePatch.From(frame, 100, 100, 40);

        Assert.Equal(1.0, patch.Against(patch), 3);
    }

    [Fact]
    public void Brightness_and_contrast_do_not_count()
    {
        var bright = Panel(200, 200);
        Draw(bright, 200, Flask, 100, 100, 40, ink: 250, ground: 60);

        var dim = Panel(200, 200);
        Draw(dim, 200, Flask, 100, 100, 40, ink: 90, ground: 20);

        var one = TilePatch.From(new GreyImage(200, 200, bright), 100, 100, 40);
        var other = TilePatch.From(new GreyImage(200, 200, dim), 100, 100, 40);

        // The same bottle under a different light is the same bottle. This is
        // what z-normalising buys, and it is why the panel being translucent
        // is survivable at all.
        Assert.True(one.Against(other) > 0.99, $"scored {one.Against(other):0.000}");
    }

    [Fact]
    public void A_picture_is_found_when_it_is_shifted_and_rescaled()
    {
        var taught = Panel(200, 200);
        Draw(taught, 200, Flask, 100, 100, 40);
        var template = TilePatch.From(new GreyImage(200, 200, taught), 100, 100, 40);

        // Drawn larger and off where it was looked for, which is what the
        // angled plane does to a tile at the far end of a row.
        var seen = Panel(400, 400);
        Draw(seen, 400, Flask, 200, 200, 46);
        var frame = new GreyImage(400, 400, seen);

        var match = TileMatcher.Best(template, frame, 197, 211, TileSearch.For(40));

        Assert.True(match.Score > 0.95, $"scored {match.Score:0.000}");
        Assert.Equal(3, match.AcrossBy, 1);
        Assert.Equal(-11, match.DownBy, 1);

        // The scale is searched on a grid rather than solved for, so it comes
        // back near the truth and not on it. That is all it has to do: a few
        // per cent of scale costs almost nothing in correlation, and no part
        // of this reports the size of a thing to anyone.
        Assert.InRange(match.Half, 44, 48);
    }

    [Fact]
    public void A_search_too_short_fails_at_the_far_end_of_a_row()
    {
        var taught = Panel(200, 200);
        Draw(taught, 200, Flask, 100, 100, 40);
        var template = TilePatch.From(new GreyImage(200, 200, taught), 100, 100, 40);

        var seen = Panel(400, 400);
        Draw(seen, 400, Flask, 200, 200, 40);
        var frame = new GreyImage(400, 400, seen);

        var measured = TileSearch.For(40);
        var tooShort = measured with { Down = 5 };

        // 13 px down is inside the measured window and outside the short one.
        // The point of the test is that the short search does not merely score
        // a little lower, it gets the wrong answer while still looking like an
        // answer.
        Assert.True(TileMatcher.Best(template, frame, 200, 187, measured).Score > 0.95);
        Assert.True(TileMatcher.Best(template, frame, 200, 187, tooShort).Score < 0.9);
    }

    [Fact]
    public void A_ramp_across_the_tile_does_not_decide_the_answer()
    {
        var taught = Panel(200, 200);
        Draw(taught, 200, Flask, 100, 100, 40);
        var template = TilePatch.From(new GreyImage(200, 200, taught), 100, 100, 40);

        // The hangar showing through, brightly and unevenly. Correlating raw
        // grey without a search let exactly this rank a row of ten glyphs
        // almost backwards.
        var seen = Panel(400, 400);
        Draw(seen, 400, Flask, 120, 200, 40);
        Draw(seen, 400, Crate, 280, 200, 40);
        Ramp(seen, 400, from: -40, to: 140);
        var frame = new GreyImage(400, 400, seen);

        var flask = TileMatcher.Best(template, frame, 120, 200, TileSearch.For(40));
        var crate = TileMatcher.Best(template, frame, 280, 200, TileSearch.For(40));

        Assert.True(flask.Score > crate.Score,
            $"flask {flask.Score:0.000} did not beat crate {crate.Score:0.000}");
    }

    [Fact]
    public void Different_art_scores_below_the_same_art()
    {
        var pixels = Panel(400, 400);
        Draw(pixels, 400, Flask, 120, 200, 40);
        Draw(pixels, 400, Crate, 280, 200, 40);
        var frame = new GreyImage(400, 400, pixels);

        var template = TilePatch.From(frame, 120, 200, 40);

        var same = TileMatcher.Best(template, frame, 120, 200, TileSearch.For(40)).Score;
        var other = TileMatcher.Best(template, frame, 280, 200, TileSearch.For(40)).Score;

        Assert.True(same > other + TileDecision.MinMargin,
            $"same {same:0.000} did not beat other {other:0.000} by a margin");
    }
}

/// <summary>
/// Turning scores into something a pilot can act on.
/// </summary>
/// <remarks>
/// The rule under test is best-match-plus-margin rather than a threshold, and
/// it is a measurement: on the ten filter glyphs of the measured frame the
/// worst correct match scored 0.947 and the best incorrect one 0.860, so a
/// bare threshold would have to be threaded between the two.
/// </remarks>
public class TileDecisionTests
{
    [Fact]
    public void A_clear_winner_is_named()
    {
        var verdict = TileDecision.Decide(
            [new("Pips Energy Drink", 0.97), new("Bottled Water", 0.71)]);

        Assert.Equal(TileConfidence.Named, verdict.Confidence);
        Assert.Equal("Pips Energy Drink", verdict.Name);
        Assert.Contains("Bottled Water", verdict.Why);
    }

    [Fact]
    public void A_near_tie_is_not_named_even_when_both_fit_well()
    {
        var verdict = TileDecision.Decide(
            [new("Bottled Water", 0.94), new("Sports Drink", 0.92)]);

        Assert.Equal(TileConfidence.Unsure, verdict.Confidence);

        // The name is still carried: "probably this" is worth showing, and the
        // reason is what stops it being read as certainty.
        Assert.Equal("Bottled Water", verdict.Name);
        Assert.Contains("too close to call", verdict.Why);
    }

    [Fact]
    public void Nothing_fitting_is_said_plainly_rather_than_guessed()
    {
        var verdict = TileDecision.Decide(
            [new("Bottled Water", 0.62), new("Sports Drink", 0.40)]);

        Assert.Equal(TileConfidence.Unknown, verdict.Confidence);
        Assert.Null(verdict.Name);
        Assert.Contains("0.62", verdict.Why);
    }

    [Fact]
    public void An_empty_portfolio_explains_itself_instead_of_returning_a_zero()
    {
        var verdict = TileDecision.Decide([]);

        Assert.Equal(TileConfidence.Unknown, verdict.Confidence);
        Assert.Contains("nothing has been taught yet", verdict.Why);
    }

    [Fact]
    public void The_only_picture_taught_still_has_to_fit()
    {
        Assert.Equal(
            TileConfidence.Named,
            TileDecision.Decide([new("Bottled Water", 0.95)]).Confidence);

        // With one candidate there is no runner-up to beat, so the score is
        // the whole of the test and must not be waved through.
        Assert.Equal(
            TileConfidence.Unknown,
            TileDecision.Decide([new("Bottled Water", 0.70)]).Confidence);
    }
}

/// <summary>
/// Finding the tiles in a frame before anything is matched.
/// </summary>
/// <remarks>
/// Fitted to one real frame, which held four tiles in a row on an otherwise
/// empty panel. These fixtures reproduce that shape and the one distinction it
/// rests on - tiles stand brighter than the panel - and nothing more. Written
/// so the limit is visible rather than implied.
/// </remarks>
public class TileGridTests
{
    private static GreyImage WithTiles(int count, double width, double gap, float tile = 30)
    {
        const int W = 800, H = 600;
        var pixels = new float[W * H];
        Array.Fill(pixels, 8f);

        for (var i = 0; i < count; i++)
        {
            var left = 100 + i * (width + gap);

            for (var y = 200; y < 320; y++)
                for (var x = (int)left; x < left + width; x++)
                    pixels[y * W + x] = tile;
        }

        return new GreyImage(W, H, pixels);
    }

    [Fact]
    public void A_row_of_tiles_is_found_with_its_centres()
    {
        var bands = TileGrid.Find(WithTiles(4, width: 60, gap: 10), 0, 0, 799, 599);

        var band = Assert.Single(bands);
        Assert.Equal(4, band.Tiles.Count);

        Assert.Equal(129.5, band.Tiles[0].CentreX, 1);
        Assert.Equal(339.5, band.Tiles[3].CentreX, 1);
        Assert.Equal(259.5, band.Tiles[0].CentreY, 1);
        Assert.Equal(30, band.Tiles[0].HalfWidth, 1);
    }

    [Fact]
    public void An_empty_panel_yields_nothing_rather_than_one_enormous_tile()
    {
        var pixels = new float[800 * 600];
        Array.Fill(pixels, 8f);

        Assert.Empty(TileGrid.Find(new GreyImage(800, 600, pixels), 0, 0, 799, 599));
    }

    [Fact]
    public void The_filter_glyphs_above_a_row_are_too_small_to_be_tiles()
    {
        const int W = 800, H = 600;
        var pixels = new float[W * H];
        Array.Fill(pixels, 8f);

        // A row of ten marks the width of the real filter glyphs, which are
        // 0.5% of the frame against a tile's 3.8%, and which sit directly
        // above the tiles they filter.
        for (var i = 0; i < 10; i++)
            for (var y = 150; y < 175; y++)
                for (var x = 100 + i * 20; x < 100 + i * 20 + 8; x++)
                    pixels[y * W + x] = 30;

        for (var i = 0; i < 3; i++)
            for (var y = 200; y < 320; y++)
                for (var x = 100 + i * 70; x < 160 + i * 70; x++)
                    pixels[y * W + x] = 30;

        // The panel, not the screen - see the remarks on TileGrid. The glyph
        // row is bright enough to be a band of its own and is kept out by
        // width alone, which is the distinction under test.
        var bands = TileGrid.Find(new GreyImage(W, H, pixels), 90, 100, 310, 400);

        Assert.Single(bands);
        Assert.Equal(3, bands[0].Tiles.Count);
        Assert.Equal(129.5, bands[0].Tiles[0].CentreX, 1);
    }
}
