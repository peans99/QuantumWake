namespace Quantumwake.Data;

/// <summary>One tile found in a frame.</summary>
/// <remarks>
/// A centre and two half-sides rather than a rectangle, because that is what
/// <see cref="TilePatch.From"/> wants and because a tile is not truly a
/// rectangle at all: the panel is drawn on an angled plane, so the shape on
/// screen is a quad. The box is the upright one that best covers it, which is
/// enough because the matcher searches for the rest.
/// </remarks>
public sealed record TileBox(double CentreX, double CentreY, double HalfWidth, double HalfHeight)
{
    /// <summary>The half-size a square patch should be cut at.</summary>
    /// <remarks>The smaller side, so a patch never reaches outside the tile.</remarks>
    public double Half => Math.Min(HalfWidth, HalfHeight);
}

/// <summary>A row of tiles, and the rows of pixels it occupies.</summary>
public sealed record TileBand(int Top, int Bottom, IReadOnlyList<TileBox> Tiles);

/// <summary>Finds the tiles in a region of a frame.</summary>
/// <remarks>
/// <para>
/// Tiles are drawn as a lighter quad on a darker panel, so they are found the
/// dull way: rows whose average brightness stands above the panel's, then
/// within those rows the columns that do the same. On
/// <c>ScreenShot-2026-09-12_13-51-13-B21.jpg</c> the tile row averages 30
/// against 8 for the empty panel below it, which is not a subtle distinction.
/// </para>
/// <para>
/// <b>Give it the panel, not the screen.</b> A row of tiles is found by the
/// brightness it adds to the rows it occupies, so it has to occupy a decent
/// share of the width it is averaged across. On the measured frame four tiles
/// filled 89% of their panel; one tile in a region eight times its width
/// disappears into the mean and is not found at all.
/// </para>
/// <para>
/// <b>This is fitted to one frame and should be treated as such.</b> That
/// frame held four tiles in a single row on an otherwise empty panel, which is
/// a soft test: it says nothing about a full stash, a tile the pilot has
/// selected or is hovering, a scrolled list, or a panel with the world showing
/// brightly through it. The thresholds below are proportions rather than
/// pixel counts so that at least the resolution is not baked in, but nobody
/// should mistake that for having been tested. See
/// <c>docs/screen-insight.md</c>.
/// </para>
/// </remarks>
public static class TileGrid
{
    /// <summary>
    /// The smallest thing called a tile, as a fraction of the frame's width.
    /// The tiles measured were 3.8% of a 3440-wide frame; half of that keeps a
    /// smaller layout while dropping the filter glyphs, which are 0.5%.
    /// </summary>
    private const double SmallestSide = 0.019;

    /// <summary>
    /// How far above the panel, in levels of 255, something has to stand to be
    /// a tile rather than the panel it sits on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured on the frame: the panel reads 7, a tile reads 25, and an empty
    /// panel varies by 1.0 levels below the tile row and 2.3 above it. Four is
    /// clear of that variation and far below any real tile.
    /// </para>
    /// <para>
    /// It is an offset from the floor rather than a fraction of the way up to
    /// something bright, and that is the third scheme tried here. A fraction
    /// needs a level to be a fraction of, and there is no percentile that
    /// serves both directions: high enough to clear a band filling a fifth of
    /// a region, and it lands on the item art instead of the tile; low enough
    /// to stay off the art, and a band filling a fifth of the region reads as
    /// panel and disappears. The floor is the one level that is always
    /// measured well, because a region of an inventory is mostly panel.
    /// </para>
    /// <para>
    /// What this does not cover, and nobody has a frame of: a panel with the
    /// hangar lit brightly enough behind it that the panel itself varies by
    /// more than four levels. The width and band requirements below are what
    /// stands between that and a row of imaginary tiles.
    /// </para>
    /// </remarks>
    private const double Lift = 4;

    public static IReadOnlyList<TileBand> Find(
        GreyImage frame, int left, int top, int right, int bottom)
    {
        left = Math.Clamp(left, 0, frame.Width - 1);
        right = Math.Clamp(right, 0, frame.Width - 1);
        top = Math.Clamp(top, 0, frame.Height - 1);
        bottom = Math.Clamp(bottom, 0, frame.Height - 1);
        if (right <= left || bottom <= top) return [];

        var smallest = Math.Max(4, (int)(frame.Width * SmallestSide));

        var rows = new double[bottom - top + 1];
        for (var y = top; y <= bottom; y++)
        {
            double sum = 0;
            for (var x = left; x <= right; x++) sum += frame.At(x, y);
            rows[y - top] = sum / (right - left + 1);
        }

        var bands = new List<TileBand>();

        if (Threshold(rows) is not { } rowCut) return [];

        foreach (var (first, last) in Runs(rows, rowCut, smallest))
        {
            var bandTop = top + first;
            var bandBottom = top + last;

            // Down a column, what is wanted is the tile's own background, not
            // an average of the background and whatever is drawn on it. A
            // percentile gives that whatever the item happens to look like: a
            // bright render lifts the mean and a dark one sinks it, and on the
            // measured frame a dark can sank two dozen columns of its own tile
            // to 8.5 against a tile background of 25, which cut the tile in
            // half and lost the larger piece.
            var down = new double[bandBottom - bandTop + 1];
            var columns = new double[right - left + 1];

            for (var x = left; x <= right; x++)
            {
                for (var y = bandTop; y <= bandBottom; y++) down[y - bandTop] = frame.At(x, y);

                Array.Sort(down);
                columns[x - left] = down[(int)((down.Length - 1) * 0.75)];
            }

            // A band of even brightness is a lit panel, not a row of tiles.
            if (Threshold(columns) is not { } columnCut) continue;


            var tiles = Runs(columns, columnCut, smallest)
                .Select(run => new TileBox(
                    CentreX: left + (run.First + run.Last) / 2.0,
                    CentreY: (bandTop + bandBottom) / 2.0,
                    HalfWidth: (run.Last - run.First + 1) / 2.0,
                    HalfHeight: (bandBottom - bandTop + 1) / 2.0))
                .ToList();

            if (tiles.Count > 0) bands.Add(new TileBand(bandTop, bandBottom, tiles));
        }

        return bands;
    }

    /// <summary>
    /// Where the brightness has to reach to count as a tile rather than panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The floor is taken very low - the 2nd percentile rather than the
    /// minimum - because the gaps between tiles are the only dark thing in a
    /// row of them and there is very little of them. On the measured frame the
    /// four tiles fill 89% of their panel and the gaps are 6 to 15 px of a
    /// 603 px region, so a floor read at the 20th percentile lands inside a
    /// tile and the whole row is thrown away as featureless.
    /// </para>
    /// <para>
    /// Otsu's method was tried here and is wrong for this picture, which is
    /// worth recording because it is the textbook answer. It looks for the
    /// split between two clusters, and a row of tiles has three: the panel,
    /// the tile background, and the item rendered on the tile, which is far
    /// the brightest. Otsu duly separated the items from the tiles they stand
    /// on - a cut of about 35 against a tile background of 25 - and then found
    /// no tiles, because what was left of each was too narrow to be one. The
    /// gaps it should have split on were too few pixels to weigh against the
    /// art.
    /// </para>
    /// </remarks>
    /// <returns>Null when the region is flat and holds nothing to find.</returns>
    private static double? Threshold(double[] profile)
    {
        var sorted = (double[])profile.Clone();
        Array.Sort(sorted);

        var floor = sorted[(int)((sorted.Length - 1) * 0.02)];
        var brightest = sorted[^1];

        if (brightest - floor < Lift) return null;

        return floor + Lift;
    }

    private static List<(int First, int Last)> Runs(double[] profile, double threshold, int smallest)
    {
        var runs = new List<(int, int)>();
        var start = -1;

        for (var i = 0; i < profile.Length; i++)
        {
            var above = profile[i] >= threshold;

            if (above && start < 0) start = i;

            if (!above && start >= 0)
            {
                if (i - start >= smallest) runs.Add((start, i - 1));
                start = -1;
            }
        }

        if (start >= 0 && profile.Length - start >= smallest)
            runs.Add((start, profile.Length - 1));

        return runs;
    }
}
