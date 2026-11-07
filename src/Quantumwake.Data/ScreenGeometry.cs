namespace Quantumwake.Data;

/// <summary>
/// How far the Windows engine turned a frame straight before reading it, and
/// the size of the frame it turned.
/// </summary>
/// <param name="Angle">The engine's <c>TextAngle</c>, in degrees, clockwise.</param>
public sealed record ScreenTilt(double Angle, double Width, double Height);

/// <summary>A reader that can say how far it turned a frame it read whole.</summary>
public interface ITiltedReader
{
    /// <summary>The tilt of the last whole read of <paramref name="imagePath"/>, or null when it was read straight.</summary>
    ScreenTilt? TiltOf(string imagePath);
}

/// <summary>Between the engine's straightened copy of a frame and the frame.</summary>
/// <remarks>
/// <para>
/// Windows' OCR straightens a frame whose text is at an angle before it reads
/// it, and reports its word boxes in that straightened copy:
/// <c>OcrWord.BoundingRect</c> is documented as the position "when the value
/// of TextAngle property is 0". A terminal seen from the side reads at 7 to 10
/// degrees, and at 10 degrees a word 300 px from the centre of a 3440 frame is
/// reported 50 px from where it is.
/// </para>
/// <para>
/// The lines are left in the straightened copy, because that is where every
/// reader's layout rules were written: rows are level there. Rotating them
/// onto the frame (tried first for 1.1.25) slanted the Fleet Manager's rows
/// and lost a ship from a 15 Sep frame. What goes wrong is the look back at
/// the pixels - a close read cut around a line lands somewhere else - so only
/// that is translated: the patch onto the frame, the lines it reads back into
/// the straightened copy. See <see cref="Straight"/>.
/// </para>
/// <para>
/// Turned back by <c>+TextAngle</c> about the frame's centre, a box lands on
/// ONNX's own within a few pixels: this install's Freight Manager frame of
/// 24 Oct 21:58 (10.3 degrees) put "B SCu" at 1409,612 and ONNX at 1431,559,
/// and 1409,612 turned is 1433,558; a 4.10 video frame (-3.6 degrees) 892,607
/// against 887,628, turned 886,632.
/// </para>
/// </remarks>
public static class ScreenGeometry
{
    /// <summary>The frame position of a point in the engine's straightened copy.</summary>
    public static (double X, double Y) OnFrame(double x, double y, ScreenTilt tilt) =>
        Turn(x, y, tilt.Angle, tilt.Width / 2, tilt.Height / 2);

    /// <summary>The straightened copy's position of a point on the frame.</summary>
    public static (double X, double Y) Straightened(double x, double y, ScreenTilt tilt) =>
        Turn(x, y, -tilt.Angle, tilt.Width / 2, tilt.Height / 2);

    /// <summary>Turns a point by <paramref name="angle"/> degrees about a centre, clockwise on a frame whose y runs down.</summary>
    public static (double X, double Y) Turn(double x, double y, double angle, double centreX, double centreY)
    {
        if (angle == 0) return (x, y);
        var r = angle * Math.PI / 180;
        var (dx, dy) = (x - centreX, y - centreY);
        return (centreX + dx * Math.Cos(r) - dy * Math.Sin(r), centreY + dx * Math.Sin(r) + dy * Math.Cos(r));
    }

    /// <summary>
    /// The frame's rectangle that holds a straightened copy's patch, turned:
    /// the box around all four corners, so a patch at 10 degrees still holds
    /// the far end of its text. Measured against moving the top-left corner
    /// alone over this install's 155 screenshots: the box read the same two
    /// Freight Manager frames' commodities and one more wallet balance.
    /// </summary>
    public static ScreenPatch OnFrame(ScreenPatch patch, ScreenTilt tilt)
    {
        var corners = new[]
        {
            OnFrame(patch.Left, patch.Top, tilt),
            OnFrame(patch.Left + patch.Width, patch.Top, tilt),
            OnFrame(patch.Left, patch.Top + patch.Height, tilt),
            OnFrame(patch.Left + patch.Width, patch.Top + patch.Height, tilt),
        };
        var left = corners.Min(c => c.X);
        var top = corners.Min(c => c.Y);
        return new ScreenPatch(left, top, corners.Max(c => c.X) - left, corners.Max(c => c.Y) - top);
    }

    /// <summary>
    /// A reader whose close reads take patches in the straightened copy, for a
    /// frame read at <paramref name="tilt"/>; the reader itself when the frame
    /// was read straight.
    /// </summary>
    /// <param name="linesOnFrame">
    /// Whether the lines it reads stay where they are on the frame rather than
    /// going back into the straightened copy. A second look that adds lines to
    /// the whole read's needs them in its copy (the wallet's, the Freight
    /// Manager's); one that reads its patch on its own wants them as they lie
    /// on the frame, because the angle is the engine's guess for the whole
    /// frame and need not be its text's: the mining scan panel is HUD type
    /// drawn level, and on a 3 Oct frame the engine turned straight by 1.7
    /// degrees for the text around it. Turning the panel's rows back by that
    /// slants them.
    /// </param>
    public static IScreenReader Straight(IScreenReader reader, ScreenTilt? tilt, bool linesOnFrame = false) =>
        tilt is null || tilt.Angle == 0 ? reader : new StraightReader(reader, tilt, linesOnFrame);

    private sealed class StraightReader(IScreenReader inner, ScreenTilt tilt, bool linesOnFrame) : IScreenReader
    {
        public Task<IReadOnlyList<ScreenTextLine>> ReadAsync(string imagePath, CancellationToken token = default) =>
            inner.ReadAsync(imagePath, token);

        public async Task<IReadOnlyList<ScreenTextLine>> ReadAsync(
            string imagePath, ScreenPatch patch, ScreenTreatment treatment, CancellationToken token = default)
        {
            var read = await inner.ReadAsync(imagePath, OnFrame(patch, tilt), treatment, token);
            if (linesOnFrame) return read;
            return [.. read.Select(line =>
            {
                var (left, top) = Straightened(line.Left, line.Top, tilt);
                return line with { Left = left, Top = top };
            })];
        }

        // Pixels are measured where they are asked for: the Rep app's bars,
        // the only pixels read off a whole read's lines, measure the same
        // tilted or not on this install's 25 frames.
        public Task<ColourPatch?> PixelsAsync(string imagePath, ScreenPatch patch, CancellationToken token = default) =>
            inner.PixelsAsync(imagePath, patch, token);
    }
}
