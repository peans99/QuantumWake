using SkiaSharp;

namespace Quantumwake.Ocr.Onnx;

/// <summary>
/// A picture as BGRA bytes, with the few operations the reader needs: decode
/// a file, cut a patch, scale, shear.
/// </summary>
/// <remarks>
/// Bgra8888 asked for at decode time so there is one layout whatever the file
/// was, as the Windows reader asks for Bgra8. Unpremultiplied, because a
/// screenshot is opaque and premultiplying only invites a decoder to darken
/// the colours being measured.
/// </remarks>
internal sealed class Frame : IDisposable
{
    private readonly SKBitmap _bitmap;

    private Frame(SKBitmap bitmap) => _bitmap = bitmap;

    public int Width => _bitmap.Width;

    public int Height => _bitmap.Height;

    /// <summary>Row-major BGRA, four bytes a pixel, no padding.</summary>
    public ReadOnlySpan<byte> Pixels => _bitmap.GetPixelSpan();

    public static Frame Load(string path)
    {
        using var codec = SKCodec.Create(path)
            ?? throw new InvalidDataException($"{Path.GetFileName(path)} is not an image this reader can decode");

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var bitmap = SKBitmap.Decode(codec, info)
            ?? throw new InvalidDataException($"{Path.GetFileName(path)} could not be decoded");

        return new Frame(bitmap);
    }

    public static Frame FromPixels(int width, int height, byte[] bgra)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        bgra.AsSpan().CopyTo(bitmap.GetPixelSpan());
        return new Frame(bitmap);
    }

    /// <summary>
    /// The frame drawn at another size. Mitchell when growing, so the engine
    /// sees smooth strokes and not stairs; a box filter when shrinking, so
    /// thin strokes are averaged in rather than dropped between samples.
    /// </summary>
    public Frame Resized(int width, int height)
    {
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(target);

        var sampling = width >= Width
            ? new SKSamplingOptions(SKCubicResampler.Mitchell)
            : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        canvas.DrawBitmap(_bitmap, new SKRect(0, 0, Width, Height), new SKRect(0, 0, width, height), sampling);
        canvas.Flush();
        return new Frame(target);
    }

    /// <summary>A rectangle of the frame, unscaled. The rectangle is clamped to the frame first.</summary>
    public Frame Cut(int left, int top, int width, int height)
    {
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(target);
        canvas.DrawBitmap(_bitmap, new SKRect(left, top, left + width, top + height), new SKRect(0, 0, width, height));
        canvas.Flush();
        return new Frame(target);
    }

    /// <summary>A rectangle of the frame drawn <paramref name="scale"/> times larger.</summary>
    public Frame CutScaled(double left, double top, double width, double height, double scale, out int outWidth, out int outHeight)
    {
        outWidth = (int)Math.Round(width * scale);
        outHeight = (int)Math.Round(height * scale);

        var target = new SKBitmap(new SKImageInfo(outWidth, outHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(target);
        canvas.DrawBitmap(
            _bitmap,
            new SKRect((float)left, (float)top, (float)(left + width), (float)(top + height)),
            new SKRect(0, 0, outWidth, outHeight),
            new SKSamplingOptions(SKCubicResampler.Mitchell));
        canvas.Flush();
        return new Frame(target);
    }

    /// <summary>
    /// Row <c>y</c> of the output is row <c>y</c> of the input read
    /// <c>shear * (height - y)</c> pixels to the right, so the bottom row
    /// stays put and each row above slides left by more, which stands a
    /// right-leaning glyph up. The same arithmetic as the Windows reader's,
    /// so the boxes map back the same way.
    /// </summary>
    public Frame Sheared(double shear)
    {
        if (shear == 0) return this;

        var source = Pixels;
        var output = new byte[source.Length];
        var width = Width;

        for (var y = 0; y < Height; y++)
        {
            var slide = shear * (Height - y);
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
                    source.Slice(edge, 4).CopyTo(output.AsSpan(at, 4));
                    continue;
                }

                var a = (row + x0) * 4;
                var b = a + 4;

                for (var c = 0; c < 4; c++)
                    output[at + c] = (byte)Math.Round(source[a + c] * (1 - frac) + source[b + c] * frac);
            }
        }

        return FromPixels(width, Height, output);
    }

    /// <summary>
    /// The frame as the network wants it: planar, channel-major, one float a
    /// sample, <c>(value / 255 - mean) / std</c> per channel in B, G, R order.
    /// </summary>
    /// <remarks>
    /// B, G, R and not R, G, B. PaddleOCR reads its training images with
    /// OpenCV, which hands pixels back blue first, and applies these means in
    /// that order; the models learned on that, so the same order goes in here.
    /// </remarks>
    public float[] ToTensor(ReadOnlySpan<float> mean, ReadOnlySpan<float> std)
    {
        var pixels = Pixels;
        var plane = Width * Height;
        var data = new float[3 * plane];

        for (var i = 0; i < plane; i++)
        {
            var at = i * 4;
            data[i] = (pixels[at] / 255f - mean[0]) / std[0];
            data[plane + i] = (pixels[at + 1] / 255f - mean[1]) / std[1];
            data[2 * plane + i] = (pixels[at + 2] / 255f - mean[2]) / std[2];
        }

        return data;
    }

    public void Dispose() => _bitmap.Dispose();
}
