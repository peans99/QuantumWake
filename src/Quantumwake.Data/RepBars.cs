namespace Quantumwake.Data;

/// <summary>One rank card in the Rep app: its name, its perk, and how full its bar was.</summary>
/// <param name="Name">"JR. CONTRACTOR", the white label, both lines of it.</param>
/// <param name="Perk">"SPECIAL WOLF BARTER CONTRACT", the smaller blue line under some ranks.</param>
/// <param name="Fill">
/// How much of the card's bar is teal, 0 to 1, or null when no bar was found
/// or no pixels were to be had. A rank already passed is full; the rank held
/// is part-full; a rank not reached is an empty grey track.
/// </param>
public sealed record RepRankCard(string Name, string? Perk, double? Fill);

/// <summary>
/// Reads the progress bars on the Rep app's rank cards.
/// </summary>
/// <remarks>
/// <para>
/// The rank the pilot holds is never written down. Every card prints its name,
/// and the only mark of which one is held is drawn: a bar above each label,
/// teal for the part earned. Across the fourteen Rep frames on this install
/// the fill is unmistakable - (144, 255, 212) against a track of about
/// (66, 92, 113) on a card of (56, 74, 87) - and the bar is a little over
/// two hundred pixels long on a 3440-wide frame, so one pixel is half a
/// percent of a rank.
/// </para>
/// <para>
/// The track is the hard part, not the fill. It is only twenty-odd levels
/// bluer than the card, and the panel is translucent, so a glow behind it
/// lifts whole regions by as much. So a pixel counts as track by being bluer
/// than the pixels a bar's thickness above and below it, never against a
/// fixed level: the glow lifts all three together and the bar stands out
/// only from its own neighbours.
/// </para>
/// <para>
/// Everything is measured in label heights from the card's own label, the way
/// the text readers measure, so it holds at any resolution. The engine places
/// labels to within a pixel or two; the bar sits about one label height above
/// the label's top.
/// </para>
/// </remarks>
public static class RepBars
{
    /// <summary>Reads one patch of the frame as colour; what the reader does.</summary>
    public delegate Task<ColourPatch?> PixelReader(ScreenPatch patch, CancellationToken token);

    /// <summary>A rank card's label as the engine placed it, before its bar is measured.</summary>
    public sealed record Card(string Name, string? Perk, ScreenTextLine Label);

    /// <summary>
    /// The rank cards with their bars measured, in the order the app lays
    /// them out; empty when the frame is not the Rep app or has no cards.
    /// </summary>
    /// <remarks>
    /// One decode for the whole grid rather than one per card: the decoder
    /// opens the file each time, and eight cards is eight opens.
    /// </remarks>
    public static async Task<IReadOnlyList<RepRankCard>> MeasureAsync(
        IReadOnlyList<ScreenTextLine> lines, PixelReader read, CancellationToken token = default)
    {
        var cards = ScreenFrames.RepCards(lines);
        if (cards.Count == 0) return [];

        var h = cards.Max(c => c.Label.Height);
        var left = cards.Min(c => c.Label.Left) - 3 * h;
        var top = cards.Min(c => c.Label.Top) - 4 * h;
        var right = cards.Max(c => c.Label.Left) + 20 * h;
        var bottom = cards.Max(c => c.Label.Top);

        ColourPatch? pixels;

        try
        {
            pixels = await read(new ScreenPatch(left, top, right - left, bottom - top), token);
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            // The text already read; losing the bars loses the rank, not the frame.
            pixels = null;
        }

        return [.. cards.Select(c => new RepRankCard(c.Name, c.Perk, pixels is null ? null : Fill(pixels, c.Label)))];
    }

    /// <summary>
    /// The rank held and how far into it, from the cards in layout order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The held rank is the last card with any teal on it - every rank before
    /// it is full, and the first card can be an empty "NOT ELIGIBLE" below
    /// ranks already earned, which is why it is not "the first card that is
    /// not full". A last teal card that is full means the next rank has just
    /// been reached with nothing into it yet. No teal anywhere is the first
    /// card, empty.
    /// </para>
    /// <para>
    /// A card whose bar was not found does not spoil the rest when it comes
    /// after a part-full one, since every card after the rank held is empty
    /// by construction. One frame of fourteen caught a card mid-animation,
    /// sliding in over its neighbour with its bar somewhere else.
    /// </para>
    /// </remarks>
    public static (int Index, double Progress)? Held(IReadOnlyList<RepRankCard> cards)
    {
        if (cards.Count == 0 || cards.All(c => c.Fill is null)) return null;

        var last = -1;
        for (var i = 0; i < cards.Count; i++)
            if (cards[i].Fill > Trace) last = i;

        if (last < 0)
            return cards[0].Fill is null ? null : (0, 0);

        if (cards[last].Fill < Full)
            return (last, cards[last].Fill!.Value);

        if (last + 1 >= cards.Count) return (last, 1);

        return cards[last + 1].Fill is null ? null : (last + 1, 0);
    }

    /// <summary>Less teal than this is a stray pixel, not progress: one column of a 200-pixel bar.</summary>
    private const double Trace = 0.01;

    /// <summary>A bar this full is a rank passed; its last column is the bright end cap, which can read as track.</summary>
    private const double Full = 0.98;

    /// <summary>How full one card's bar is, or null when there is no bar where it should be.</summary>
    internal static double? Fill(ColourPatch pixels, ScreenTextLine label) => Measure(pixels, label)?.Fill;

    /// <summary>Where a bar was found, for the measurement harness to set against the picture.</summary>
    public sealed record Bar(int Row, int Left, int Right, int Reached, double Fill);

    /// <summary>One card's bar in full: its row, its ends, and the teal's last column.</summary>
    public static Bar? Measure(ColourPatch pixels, ScreenTextLine label)
    {
        var h = label.Height;
        var x0 = (int)Math.Floor(label.Left - 2.5 * h);
        var x1 = (int)Math.Ceiling(label.Left + 19 * h);
        var y0 = (int)Math.Floor(label.Top - 2.5 * h);
        var y1 = (int)Math.Ceiling(label.Top - 0.2 * h);

        // A bar is about half a label tall; its neighbours are looked for a
        // little over that above and below so that every row of the bar has
        // at least one neighbour off it.
        var reach = Math.Max(4, (int)Math.Round(0.6 * h));

        bool Teal(int x, int y)
        {
            var (r, g, b) = pixels.At(x, y);
            return g >= 150 && g - r >= 50 && g - b >= 15;
        }

        bool Lit(int x, int y)
        {
            if (Teal(x, y)) return true;

            var b = pixels.At(x, y).B;
            var ground = Math.Min(pixels.At(x, y - reach).B, pixels.At(x, y + reach).B);
            return b - ground >= 12;
        }

        // The bar's rows: the row with the most of it, and its neighbours
        // that carry at least half as much.
        var scores = new int[y1 - y0 + 1];
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
                if (Lit(x, y)) scores[y - y0]++;

        var peak = Array.IndexOf(scores, scores.Max());
        if (scores[peak] < 4 * h) return null;

        var bandTop = peak;
        while (bandTop > 0 && scores[bandTop - 1] >= scores[peak] / 2) bandTop--;
        var bandBottom = peak;
        while (bandBottom < scores.Length - 1 && scores[bandBottom + 1] >= scores[peak] / 2) bandBottom++;

        // Each column by majority over the band, so a speck of glare in one
        // row does not make a column.
        var rows = bandBottom - bandTop + 1;
        var lit = new bool[x1 - x0 + 1];
        var teal = new bool[x1 - x0 + 1];

        for (var x = x0; x <= x1; x++)
        {
            int l = 0, t = 0;
            for (var y = y0 + bandTop; y <= y0 + bandBottom; y++)
            {
                if (Lit(x, y)) l++;
                if (Teal(x, y)) t++;
            }

            lit[x - x0] = l * 2 >= rows;
            teal[x - x0] = t * 2 >= rows;
        }

        // The longest run, bridging gaps of a few columns where the glow
        // behind the panel washed a stretch of track out.
        var gap = Math.Max(3, (int)Math.Round(0.3 * h));
        int bestStart = -1, bestEnd = -1, start = -1, end = -1, missed = 0;

        for (var i = 0; i < lit.Length; i++)
        {
            if (lit[i])
            {
                if (start < 0) start = i;
                end = i;
                missed = 0;
            }
            else if (start >= 0 && ++missed > gap)
            {
                if (end - start > bestEnd - bestStart) (bestStart, bestEnd) = (start, end);
                start = -1;
                missed = 0;
            }
        }

        if (start >= 0 && end - start > bestEnd - bestStart) (bestStart, bestEnd) = (start, end);
        if (bestStart < 0 || bestEnd - bestStart + 1 < 4 * h) return null;

        // Filled from the left, so the fill reaches as far as its last teal
        // column; counting teal columns instead would let one washed-out
        // column inside the fill read as progress lost.
        var reached = -1;
        for (var i = bestStart; i <= bestEnd; i++)
            if (teal[i]) reached = i;

        var row = y0 + peak;

        if (reached < 0) return new Bar(row, x0 + bestStart, x0 + bestEnd, -1, 0);

        var fill = (double)(reached - bestStart + 1) / (bestEnd - bestStart + 1);
        return new Bar(row, x0 + bestStart, x0 + bestEnd, x0 + reached, fill >= Full ? 1 : fill);
    }
}
