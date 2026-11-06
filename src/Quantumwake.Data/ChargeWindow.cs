namespace Quantumwake.Data;

/// <summary>Where a fracture's charge stands against the laser's optimal window.</summary>
/// <param name="State">below, in or above the window.</param>
/// <param name="Fill">How much of the window bar has turned green - the crack's progress - 0 to 1.</param>
/// <param name="BelowBy">How far under the window the charge is, in bar heights; null in or above it.</param>
/// <param name="Bar">Where the window bar sat, so the next look can go straight to it.</param>
public sealed record ChargeReading(string State, double Fill, double? BelowBy, ScreenPatch Bar);

/// <summary>
/// Reads the mining fracture HUD's charge arc against its OPTIMAL bar - from
/// pixels alone, because neither engine reads that HUD's words reliably.
/// </summary>
/// <remarks>
/// <para>
/// Measured on Elguapoe's MOLE stream of 2026-08-31 (2560x1440), one frame a
/// second over 17 minutes. The window is a short yellow bar, about 60 x 24 at
/// 1440 high, that turns green from the left while the charge sits in it, and
/// the charge is a green arc whose top end meets the bar's right end when it
/// gets there. On the rock at 01:09:55-01:10:28 the arc's tip climbed from 135
/// px under the bar to 15, sat in the window for 14 s while the bar filled 3%,
/// 14%, 20% ... 96%, and the bar vanished as the rock cracked.
/// </para>
/// <para>
/// The bar's yellow (about 230/205/70) is told from Daymar's sand (232/115/53)
/// and sky (249/179/113) by its green: at least 0.8 of its red, with blue under
/// 0.45 of the green. Windows' engine never read the "OPTIMAL" label beside it
/// in that stretch, so nothing here waits on text.
/// </para>
/// <para>
/// Above the window - an overcharge - is read the same way, as the tip above
/// the bar, but no overcharge happened in the stretch measured: the two frames
/// that read as one were the ship's yellow floor and the cargo arc in
/// extraction, a frame each. It is held to two agreeing frames for that
/// reason, and stays unproven until a real one is seen.
/// </para>
/// </remarks>
public static class ChargeWindow
{
    /// <summary>
    /// Where to look on a frame: right of centre and around the middle, where
    /// the MOLE's arc sits. Centre-relative and in frame heights, so an
    /// ultrawide frame looks in the same place relative to the reticle.
    /// </summary>
    public static ScreenPatch SearchArea(int width, int height) => new(
        Math.Max(0, width / 2.0 - height * 0.15), height * 0.2,
        Math.Min(width - Math.Max(0, width / 2.0 - height * 0.15), height * 0.75), height * 0.55);

    /// <summary>The bar's neighbourhood, for the look after one found it: the bar, the arc's lane beside it, and the arc's reach below.</summary>
    public static ScreenPatch Around(ScreenPatch bar)
    {
        var h = bar.Height;
        return new ScreenPatch(Math.Max(0, bar.Left - h), Math.Max(0, bar.Top - h * 4), bar.Width + h * 5, h * 20);
    }

    private static bool Green(byte r, byte g, byte b) => g > 170 && g > r * 1.12 && g > b * 1.35;

    private static bool Yellow(byte r, byte g, byte b) => r > 170 && g >= r * 0.8 && b < g * 0.45 && !Green(r, g, b);

    /// <summary>The charge against the window in <paramref name="pixels"/>, or null when there is no window bar - not fracturing.</summary>
    public static ChargeReading? Read(ColourPatch pixels, int frameHeight)
    {
        var u = frameHeight / 1440.0;
        int left = pixels.Left, top = pixels.Top, right = pixels.Left + pixels.Width, bottom = pixels.Top + pixels.Height;
        bool BarColour(int x, int y)
        {
            var (r, g, b) = pixels.At(x, y);
            return Green(r, g, b) || Yellow(r, g, b);
        }

        // Rows carrying a long run of bar colour: the bar is wide and short,
        // where the arcs are thin and nearly upright at that height.
        var minRun = (int)(30 * u);
        var bars = new List<(int X0, int X1, int Y0, int Y1)>();
        for (var y = top; y < bottom; y++)
        {
            int run = 0, start = 0, bestStart = 0, bestLength = 0;
            for (var x = left; x < right; x++)
            {
                if (BarColour(x, y))
                {
                    if (run == 0) start = x;
                    run++;
                    if (run > bestLength) { bestLength = run; bestStart = start; }
                }
                else run = 0;
            }

            if (bestLength < minRun) continue;
            if (bars.Count > 0 && y - bars[^1].Y1 <= 2 && Math.Abs(bestStart - bars[^1].X0) < 12 * u)
                bars[^1] = (bars[^1].X0, Math.Max(bars[^1].X1, bestStart + bestLength), bars[^1].Y0, y);
            else
                bars.Add((bestStart, bestStart + bestLength, y, y));
        }

        // 60 x 24 measured; a margin either way, and nothing taller than a bar.
        var found = bars
            .Where(b => b.Y1 - b.Y0 >= 8 * u && b.Y1 - b.Y0 <= 30 * u && b.X1 - b.X0 >= 35 * u && b.X1 - b.X0 <= 120 * u)
            .OrderByDescending(b => b.X1 - b.X0)
            .Cast<(int X0, int X1, int Y0, int Y1)?>()
            .FirstOrDefault();
        if (found is not { } bar) return null;

        // The whole bar, edges included, and its fill.
        int bx0 = int.MaxValue, bx1 = -1, by0 = int.MaxValue, by1 = -1, green = 0, yellow = 0;
        for (var y = bar.Y0 - 4; y <= bar.Y1 + 4; y++)
        {
            for (var x = bar.X0 - 10; x < bar.X1 + 4; x++)
            {
                var (r, g, b) = pixels.At(x, y);
                var isGreen = Green(r, g, b);
                if (!isGreen && !Yellow(r, g, b)) continue;
                if (isGreen) green++; else yellow++;
                bx0 = Math.Min(bx0, x); bx1 = Math.Max(bx1, x); by0 = Math.Min(by0, y); by1 = Math.Max(by1, y);
            }
        }

        var height = by1 - by0 + 1;
        var fill = green + yellow == 0 ? 0 : green / (double)(green + yellow);

        // The arc's lane: just right of the bar, past the two tick marks at
        // its end, where the arc's top meets it. Topmost green there is the tip.
        int? tip = null;
        for (var y = Math.Max(top, by0 - (int)(60 * u)); y < Math.Min(bottom, by1 + (int)(300 * u)) && tip is null; y++)
        {
            if (y >= by0 - 6 * u && y <= by1 + 3 * u) continue;
            var hits = 0;
            for (var x = bx1 + (int)(4 * u); x <= bx1 + (int)(height * 1.8); x++)
            {
                var (r, g, b) = pixels.At(x, y);
                if (Green(r, g, b)) hits++;
            }
            if (hits >= 2) tip = y;
        }

        var patch = new ScreenPatch(bx0, by0, bx1 - bx0 + 1, height);
        if (tip is null) return new ChargeReading("below", fill, null, patch);
        if (tip < by0 - 6 * u) return new ChargeReading("above", fill, null, patch);
        if (tip <= by1 + 10 * u) return new ChargeReading("in", fill, null, patch);
        return new ChargeReading("below", fill, (tip.Value - by1) / (double)height, patch);
    }
}
