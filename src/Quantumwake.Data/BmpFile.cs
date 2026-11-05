namespace Quantumwake.Data;

/// <summary>
/// Writes a 24-bit Windows bitmap from the rows a screen copy gives back.
/// </summary>
/// <remarks>
/// <para>
/// A BMP because it is the format that needs no encoder: a 54-byte header and
/// the rows as they came. A live frame is written every couple of seconds and
/// read straight back by the same engine that reads screenshots, so spending
/// time compressing it would be time taken from the reading. Both engines open
/// BMP - Windows' through its imaging decoder, the ONNX one through Skia.
/// </para>
/// <para>
/// Rows are bottom-up, which is what <c>GetDIBits</c> returns for a positive
/// height and what every BMP reader handles; a negative, top-down height is
/// valid too but is the variant decoders get wrong.
/// </para>
/// </remarks>
public static class BmpFile
{
    /// <summary>Bytes per row once padded to the four-byte boundary BMP requires.</summary>
    public static int Stride(int width) => (width * 3 + 3) & ~3;

    /// <summary>Writes <paramref name="pixels"/> - bottom-up BGR rows, each <see cref="Stride"/> bytes - as a BMP.</summary>
    public static void Write(string path, int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "a bitmap needs pixels");
        var size = Stride(width) * height;
        if (pixels.Length < size) throw new ArgumentException($"expected {size} bytes of rows, got {pixels.Length}", nameof(pixels));

        Span<byte> header = stackalloc byte[54];
        header.Clear();
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        BitConverter.TryWriteBytes(header[2..], 54 + size);   // file size
        BitConverter.TryWriteBytes(header[10..], 54);         // where the rows start
        BitConverter.TryWriteBytes(header[14..], 40);         // BITMAPINFOHEADER
        BitConverter.TryWriteBytes(header[18..], width);
        BitConverter.TryWriteBytes(header[22..], height);     // positive: bottom-up
        BitConverter.TryWriteBytes(header[26..], (short)1);   // planes
        BitConverter.TryWriteBytes(header[28..], (short)24);  // bits a pixel
        BitConverter.TryWriteBytes(header[34..], size);       // BI_RGB, so this may be 0, but it costs nothing to say
        BitConverter.TryWriteBytes(header[38..], 2835);       // 72 dpi, which nothing reads
        BitConverter.TryWriteBytes(header[42..], 2835);

        // Written next to the target and moved over it, so the reader never
        // opens a frame half-written by the next capture.
        var temp = path + ".part";
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            file.Write(header);
            file.Write(pixels[..size]);
        }
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Whether a copy came back pure black - what the screen gives for a game
    /// in exclusive fullscreen, where the desktop never sees its frames.
    /// </summary>
    /// <remarks>
    /// Exactly zero, not merely dark: space is mostly black, and a scan panel
    /// over a dark sky can light very little of the frame. Anything the game
    /// drew lifts some pixel above zero; a frame the desktop never had does
    /// not. Every byte is checked - a few million, and it stops at the first.
    /// </remarks>
    public static bool LooksBlank(int width, int height, ReadOnlySpan<byte> pixels)
    {
        var size = Math.Min(pixels.Length, Stride(width) * height);
        return size > 0 && pixels[..size].IndexOfAnyExcept((byte)0) < 0;
    }
}
