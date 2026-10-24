using System.Runtime.InteropServices.WindowsRuntime;
using Quantumwake.Data;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace Quantumwake.Ocr;

/// <summary>
/// Reads a screenshot with the engine Windows already has.
/// </summary>
/// <remarks>
/// <para>
/// No package reference and no model to ship: <c>Windows.Media.Ocr</c> and its
/// English data are on the machine. Measured on this install, a 3440x1440
/// frame goes through whole in about 170 ms, which is why the first read
/// neither crops nor rescales - both were tried across the frame and neither
/// helped. Upscaling made a line worse, dropping a whole word at three times
/// the size.
/// </para>
/// <para>
/// The second read is the exception, and a narrow one: one patch, scaled and
/// sheared, for a single italic line the engine drops from the whole frame.
/// The measurements that justify it are on <see cref="WalletSecondLook"/>.
/// </para>
/// <para>
/// The engine is created once. Creating one per call is the obvious way to
/// write this and costs more than the recognition does.
/// </para>
/// </remarks>
public sealed class WindowsScreenReader : IScreenReader
{
    private readonly OcrEngine? _engine =
        OcrEngine.TryCreateFromLanguage(new Language("en-US"))
        ?? OcrEngine.TryCreateFromUserProfileLanguages();

    /// <summary>Whether this machine has an engine at all.</summary>
    /// <remarks>
    /// A Windows install with no English language pack has none. Better asked
    /// once at startup than discovered by a pilot pressing the button.
    /// </remarks>
    public bool Available => _engine is not null;

    public async Task<IReadOnlyList<ScreenTextLine>> ReadAsync(
        string imagePath, CancellationToken token = default)
    {
        if (_engine is null) return [];

        var file = await StorageFile.GetFileFromPathAsync(imagePath).AsTask(token);
        using var stream = await file.OpenAsync(FileAccessMode.Read).AsTask(token);

        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);
        using var bitmap = await decoder.GetSoftwareBitmapAsync().AsTask(token);

        var result = await _engine.RecognizeAsync(bitmap).AsTask(token);

        return Lines(result);
    }

    /// <remarks>
    /// <para>
    /// The pixels come out of the decoder already cropped and scaled - it
    /// does both in one transform - and are sheared here, row by row, before
    /// going back into a bitmap the engine will take. Bgra8 asked for so there
    /// is one layout to shear whatever the file was.
    /// </para>
    /// <para>
    /// Shear runs the other way from the slant: row <c>y</c> of the output is
    /// row <c>y</c> of the input read <c>shear * (height - y)</c> pixels to
    /// the right, so the bottom row stays put and each row above slides left
    /// by more, which stands a right-leaning glyph up. Linear interpolation
    /// between the two source pixels, because nearest-neighbour puts a
    /// staircase on every stroke edge and the engine reads the staircase.
    /// </para>
    /// <para>
    /// Boxes are mapped back by undoing the same three steps, so the caller
    /// can measure them against the first read's lines. The shear is undone
    /// at each box's own top, which is exact for its left edge and within a
    /// pixel for the rest.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<ScreenTextLine>> ReadAsync(
        string imagePath, ScreenPatch patch, ScreenTreatment treatment, CancellationToken token = default)
    {
        if (_engine is null) return [];

        var file = await StorageFile.GetFileFromPathAsync(imagePath).AsTask(token);
        using var stream = await file.OpenAsync(FileAccessMode.Read).AsTask(token);

        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);

        // Clamp to the frame: a panel measured off a bar near an edge can ask
        // for pixels that are not there, and the decoder throws rather than
        // clipping.
        var left = Math.Clamp(patch.Left, 0, decoder.PixelWidth);
        var top = Math.Clamp(patch.Top, 0, decoder.PixelHeight);
        var width = Math.Clamp(patch.Width, 0, decoder.PixelWidth - left);
        var height = Math.Clamp(patch.Height, 0, decoder.PixelHeight - top);

        var scale = treatment.Scale;
        var bounds = new BitmapBounds
        {
            X = (uint)Math.Round(left * scale),
            Y = (uint)Math.Round(top * scale),
            Width = (uint)Math.Round(width * scale),
            Height = (uint)Math.Round(height * scale),
        };

        if (bounds.Width < 2 || bounds.Height < 2) return [];

        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Round(decoder.PixelWidth * scale),
            ScaledHeight = (uint)Math.Round(decoder.PixelHeight * scale),
            InterpolationMode = BitmapInterpolationMode.Fant,
            Bounds = bounds,
        };

        var data = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask(token);

        var w = (int)bounds.Width;
        var h = (int)bounds.Height;
        var pixels = Shear(data.DetachPixelData(), w, h, treatment.Shear);

        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);

        var result = await _engine.RecognizeAsync(bitmap).AsTask(token);

        return [.. Lines(result).Select(line => line with
        {
            Left = left + (line.Left + treatment.Shear * (h - line.Top)) / scale,
            Top = top + line.Top / scale,
            Height = line.Height / scale,
        })];
    }

    /// <remarks>
    /// Straight, so not premultiplied: a screenshot is opaque, and asking for
    /// the alpha to be folded in only invites a decoder that does it to
    /// darken the colours being measured.
    /// </remarks>
    public async Task<ColourPatch?> PixelsAsync(string imagePath, ScreenPatch patch, CancellationToken token = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath).AsTask(token);
        using var stream = await file.OpenAsync(FileAccessMode.Read).AsTask(token);

        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);

        var left = (int)Math.Clamp(Math.Floor(patch.Left), 0, decoder.PixelWidth);
        var top = (int)Math.Clamp(Math.Floor(patch.Top), 0, decoder.PixelHeight);
        var width = (int)Math.Clamp(Math.Ceiling(patch.Width), 0, decoder.PixelWidth - left);
        var height = (int)Math.Clamp(Math.Ceiling(patch.Height), 0, decoder.PixelHeight - top);

        if (width < 1 || height < 1) return null;

        var transform = new BitmapTransform
        {
            Bounds = new BitmapBounds { X = (uint)left, Y = (uint)top, Width = (uint)width, Height = (uint)height },
        };

        var data = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask(token);

        return new ColourPatch(left, top, width, height, data.DetachPixelData());
    }

    private static byte[] Shear(byte[] source, int width, int height, double shear)
    {
        if (shear == 0) return source;

        var output = new byte[source.Length];

        for (var y = 0; y < height; y++)
        {
            var slide = shear * (height - y);
            var row = y * width;

            for (var x = 0; x < width; x++)
            {
                var from = x + slide;
                var x0 = (int)Math.Floor(from);
                var frac = from - x0;
                var at = (row + x) * 4;

                if (x0 < 0 || x0 + 1 >= width)
                {
                    // Off the edge: repeat the edge pixel rather than paint a
                    // colour the frame never had, which the engine might read.
                    var edge = (row + Math.Clamp(x0, 0, width - 1)) * 4;
                    Array.Copy(source, edge, output, at, 4);
                    continue;
                }

                var a = (row + x0) * 4;
                var b = a + 4;

                for (var c = 0; c < 4; c++)
                    output[at + c] = (byte)Math.Round(source[a + c] * (1 - frac) + source[b + c] * frac);
            }
        }

        return output;
    }

    private static List<ScreenTextLine> Lines(OcrResult result) =>
        [.. result.Lines
            .Where(line => line.Words.Count > 0)
            .Select(line => new ScreenTextLine(
                line.Text,
                line.Words.Min(w => w.BoundingRect.Left),
                line.Words.Min(w => w.BoundingRect.Top),

                // The tallest word, because a line of mixed case is as tall as
                // its capitals and the distances measured against this are all
                // in line heights.
                line.Words.Max(w => w.BoundingRect.Height)))];
}
