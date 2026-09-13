namespace Quantumwake.Data;

/// <summary>A decoded frame, one brightness per pixel.</summary>
/// <remarks>
/// Grey rather than colour, and a float rather than a byte, because everything
/// downstream correlates: the arithmetic wants headroom, and the colour is not
/// yet spent. Decoding lives behind <see cref="ITileImageLoader"/> for the
/// same reason <see cref="IScreenReader"/> exists - an image decoder needs the
/// Windows target and the server has no business having one.
/// </remarks>
public sealed class GreyImage
{
    private readonly float[] _pixels;

    public GreyImage(int width, int height, float[] pixels)
    {
        if (pixels.Length != width * height)
            throw new ArgumentException(
                $"expected {width * height} pixels, got {pixels.Length}", nameof(pixels));

        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public float At(int x, int y) =>
        _pixels[Math.Clamp(y, 0, Height - 1) * Width + Math.Clamp(x, 0, Width - 1)];

    /// <summary>Brightness between the pixels.</summary>
    /// <remarks>
    /// Bilinear, and it matters. The tile grid is drawn on a plane angled away
    /// from the pilot, so a tile's edges land on fractions of a pixel and the
    /// scale differs from one end of a row to the other. Sampling to the
    /// nearest pixel throws that away and takes the correlation with it.
    /// </remarks>
    public double Sample(double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;

        return At(x0, y0) * (1 - fx) * (1 - fy)
             + At(x0 + 1, y0) * fx * (1 - fy)
             + At(x0, y0 + 1) * (1 - fx) * fy
             + At(x0 + 1, y0 + 1) * fx * fy;
    }
}

/// <summary>Turns an image file into brightness.</summary>
/// <remarks>
/// An interface for the reason <see cref="IScreenReader"/> is one: the
/// implementation needs the Windows SDK target, the server does not have it,
/// and a server running without one should say so rather than pretend.
/// </remarks>
public interface ITileImageLoader
{
    Task<GreyImage> LoadAsync(string imagePath, CancellationToken token = default);
}

/// <summary>
/// A square of a frame, resampled to a fixed size and z-normalised.
/// </summary>
/// <remarks>
/// <para>
/// Normalising is what makes two pictures of the same thing comparable when
/// one is lit differently from the other: subtracting the mean and dividing by
/// the deviation leaves shape behind and drops brightness and contrast. The
/// panels this reads are translucent - the hangar shows through them - so
/// brightness is not a property of the item and must not be allowed to count.
/// </para>
/// <para>
/// It is not sufficient on its own. Normalising removes a patch's overall
/// level but not a gradient across it, and on a translucent panel the lighting
/// ramp is exactly that. Correlating without the search in
/// <see cref="TileMatcher"/> scored 0.75 on the wrong glyph and ranked a row
/// of ten almost exactly backwards.
/// </para>
/// </remarks>
public sealed class TilePatch
{
    /// <summary>
    /// 24 square. Measured: large enough to separate ten glyphs that differ in
    /// nothing but shape, small enough that a search over offsets and scales
    /// is affordable. Nothing about the frame's own resolution enters here,
    /// which is the point - a patch is the same size whatever it was cut from,
    /// so a portfolio taught at one resolution is read at another.
    /// </summary>
    public const int Size = 24;

    private readonly float[] _values;

    private TilePatch(float[] values) => _values = values;

    /// <summary>Cuts a square out of a frame and normalises it.</summary>
    /// <param name="half">Half the side of the square, in the frame's pixels.</param>
    public static TilePatch From(GreyImage image, double centreX, double centreY, double half)
    {
        var values = new float[Size * Size];
        var side = 2 * half;

        for (var row = 0; row < Size; row++)
        {
            var y = centreY - half + side * (row + 0.5) / Size;

            for (var column = 0; column < Size; column++)
            {
                var x = centreX - half + side * (column + 0.5) / Size;
                values[row * Size + column] = (float)image.Sample(x, y);
            }
        }

        double mean = 0;
        foreach (var value in values) mean += value;
        mean /= values.Length;

        double variance = 0;
        foreach (var value in values) variance += (value - mean) * (value - mean);
        var deviation = Math.Sqrt(variance / values.Length);

        // Flat colour has no shape to compare. Dividing by a deviation of zero
        // would make it correlate perfectly with everything; leaving it at
        // zero makes it correlate with nothing, which is the true answer.
        var scale = deviation > 1e-6 ? 1 / deviation : 0;

        for (var i = 0; i < values.Length; i++)
            values[i] = (float)((values[i] - mean) * scale);

        return new TilePatch(values);
    }

    /// <summary>How alike two patches are, from -1 to 1.</summary>
    public double Against(TilePatch other)
    {
        double sum = 0;
        for (var i = 0; i < _values.Length; i++) sum += _values[i] * other._values[i];
        return sum / _values.Length;
    }
}

/// <summary>Where a stored picture fitted a frame best, and how well.</summary>
public sealed record TileMatch(double Score, double AcrossBy, double DownBy, double Half);

/// <summary>How far to look for the alignment.</summary>
/// <remarks>
/// <para>
/// Measured on <c>ScreenShot-2026-09-12_13-51-13-B21.jpg</c> and written up in
/// <c>docs/screen-insight.md</c>. The window is not a safety margin, it is the
/// geometry: the inventory panels are drawn on a plane angled towards the
/// pilot, and the two panels angle opposite ways. Across one row of ten
/// glyphs the vertical offset between the same art on the two panels ran
/// +12.5 px to -14 px in a straight line, about -2.9 px per step.
/// </para>
/// <para>
/// With the vertical search capped at 5 px, five of ten glyphs matched and
/// every failure had pinned itself at the limit. At 14 px all ten matched. A
/// window that is too small does not degrade gracefully: it fails at the ends
/// of a row while looking perfect in the middle.
/// </para>
/// </remarks>
public sealed record TileSearch(double Across, double Down, double HalfLow, double HalfHigh)
{
    /// <summary>The measured window, for a tile of the given half-size.</summary>
    /// <remarks>
    /// The scale range covers 0.82x to 1.24x, which is one panel's near and far
    /// ends plus room for a frame taken at another resolution.
    /// </remarks>
    public static TileSearch For(double half) =>
        new(Across: 5, Down: 14, HalfLow: half * 0.82, HalfHigh: half * 1.24);
}

/// <summary>Finds where a stored picture sits in a frame, and how well it fits.</summary>
public static class TileMatcher
{
    /// <summary>
    /// The best alignment of <paramref name="template"/> near a point.
    /// </summary>
    /// <remarks>
    /// Coarse then fine, because the honest search is not affordable: one pass
    /// at half-pixel steps over the measured window is some eighteen thousand
    /// patches for a single comparison, and a stash has dozens of tiles to put
    /// against a portfolio of hundreds. Two pixels and then a half is a
    /// hundredth of the work and found the same answer on every glyph measured.
    /// </remarks>
    public static TileMatch Best(
        TilePatch template, GreyImage frame, double centreX, double centreY, TileSearch search)
    {
        var coarse = Sweep(
            template, frame, centreX, centreY,
            across: search.Across, down: search.Down,
            halfLow: search.HalfLow, halfHigh: search.HalfHigh,
            offsetStep: 2, halfSteps: 8, fromAcross: 0, fromDown: 0);

        return Sweep(
            template, frame,
            centreX + coarse.AcrossBy, centreY + coarse.DownBy,
            across: 2, down: 2,
            halfLow: Math.Max(search.HalfLow, coarse.Half - 1.5),
            halfHigh: Math.Min(search.HalfHigh, coarse.Half + 1.5),
            offsetStep: 0.5, halfSteps: 6,
            fromAcross: coarse.AcrossBy, fromDown: coarse.DownBy);
    }

    private static TileMatch Sweep(
        TilePatch template, GreyImage frame, double centreX, double centreY,
        double across, double down, double halfLow, double halfHigh,
        double offsetStep, int halfSteps, double fromAcross, double fromDown)
    {
        var best = new TileMatch(double.NegativeInfinity, fromAcross, fromDown, halfLow);
        var halfStep = halfSteps > 1 ? (halfHigh - halfLow) / (halfSteps - 1) : 0;

        for (var step = 0; step < Math.Max(halfSteps, 1); step++)
        {
            var half = halfLow + halfStep * step;
            if (half <= 1) continue;

            for (var dx = -across; dx <= across; dx += offsetStep)
            {
                for (var dy = -down; dy <= down; dy += offsetStep)
                {
                    var score = template.Against(
                        TilePatch.From(frame, centreX + dx, centreY + dy, half));

                    if (score > best.Score)
                        best = new TileMatch(score, fromAcross + dx, fromDown + dy, half);
                }
            }
        }

        return best;
    }
}
