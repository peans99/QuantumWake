using Quantumwake.Data;
using SkiaSharp;

namespace Quantumwake.Server;

/// <summary>
/// Pictures of what the live reader followed as the fracture bar when the
/// charge never did anything to it (1.2.58) - only while the pilot has
/// turned on <see cref="LiveScreenSettings.CatchStrayBars"/>.
/// </summary>
/// <remarks>
/// <para>
/// By 16 November 2026 31 of the 32 windows this install had recorded never
/// filled, entered the window or went over, and the game logs had the pilot
/// in a Hermes, a Pisces, a Super Hornet, an Ironclad or on foot each time.
/// 1.2.55 stopped counting them, but what the yellow box was is unseen: the
/// frame log keeps no picture, by promise, and asking a pilot to screenshot
/// a box that is gone in seconds has not worked.
/// </para>
/// <para>
/// So the pictures are cut when a bar is first taken up, while it is still
/// on screen, held in memory, and written only once its window has closed
/// unworked: a real rock's pictures are dropped. Two per window - the part
/// of the screen around the bar at full size with the bar outlined, and the
/// whole screen at a width where its text cannot be read - because the
/// crop shows what the box is and the small one says which screen it was on.
/// At most <see cref="MostKept"/> windows, then nothing more is held: a
/// handful answers the question, and a switch forgotten on should not fill a
/// folder.
/// </para>
/// </remarks>
public sealed class StrayBarCatcher(string folder)
{
    /// <summary>The most windows kept: past this, bars are no longer pictured.</summary>
    public const int MostKept = 20;

    /// <summary>The small picture's width: a screen's layout, not its words.</summary>
    public const int WholeWidth = 640;

    private const string Around = "-around.png";
    private const string Screen = "-screen.png";

    private (byte[] Around, byte[] Screen)? _held;

    /// <summary>Where the pictures go.</summary>
    public string Folder => folder;

    /// <summary>Windows pictured so far.</summary>
    public int Count => Directory.Exists(folder) ? Directory.GetFiles(folder, "*" + Around).Length : 0;

    /// <summary>Whether a window's pictures are waiting for it to close.</summary>
    public bool Holding => _held is not null;

    /// <summary>
    /// Cuts the pictures of a bar just taken up, from the frame it was found
    /// on, and holds them. Nothing is held once <see cref="MostKept"/> are kept,
    /// or when the frame cannot be read.
    /// </summary>
    public void Hold(string frame, ScreenPatch bar)
    {
        _held = null;
        if (Count >= MostKept) return;

        try
        {
            using var bitmap = Decode(frame);
            if (bitmap is null) return;
            _held = (Cut(bitmap, bar), Shrink(bitmap));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Mid-write: this window goes unpictured, the next may not.
        }
    }

    /// <summary>Writes what is held for a window that closed unworked; returns the name it was written under.</summary>
    public string? Keep(FractureRecord window)
    {
        if (_held is not { } held) return null;
        _held = null;

        Directory.CreateDirectory(folder);
        var name = $"bar-{window.Since.ToLocalTime():yyyyMMdd-HHmmss}";
        File.WriteAllBytes(Path.Combine(folder, name + Around), held.Around);
        File.WriteAllBytes(Path.Combine(folder, name + Screen), held.Screen);
        return name;
    }

    /// <summary>Lets go of what is held: the window was a rock, a terminal's bar, or reading stopped.</summary>
    public void Drop() => _held = null;

    private static SKBitmap? Decode(string frame)
    {
        // Shared, as Hologram reads it: the next capture may be writing.
        using var stream = new FileStream(frame, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return SKBitmap.Decode(copy.ToArray());
    }

    /// <summary>
    /// A third of the screen each way around the bar, at full size, with the
    /// bar outlined in a colour the HUD does not use - so the box is found at
    /// a glance among whatever else was yellow there.
    /// </summary>
    private static byte[] Cut(SKBitmap bitmap, ScreenPatch bar)
    {
        var w = (int)(bitmap.Width / 3.0);
        var h = (int)(bitmap.Height / 3.0);
        var cx = (int)(bar.Left + bar.Width / 2);
        var cy = (int)(bar.Top + bar.Height / 2);
        var left = Math.Clamp(cx - w / 2, 0, Math.Max(0, bitmap.Width - w));
        var top = Math.Clamp(cy - h / 2, 0, Math.Max(0, bitmap.Height - h));
        var rect = SKRectI.Intersect(new SKRectI(0, 0, bitmap.Width, bitmap.Height), SKRectI.Create(left, top, w, h));

        using var cut = new SKBitmap(rect.Width, rect.Height);
        using (var canvas = new SKCanvas(cut))
        {
            canvas.DrawBitmap(bitmap, rect, new SKRect(0, 0, rect.Width, rect.Height));
            using var pen = new SKPaint { Color = SKColors.Magenta, Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = false };
            const float pad = 6;
            canvas.DrawRect(new SKRect(
                (float)(bar.Left - left - pad), (float)(bar.Top - top - pad),
                (float)(bar.Left + bar.Width - left + pad), (float)(bar.Top + bar.Height - top + pad)), pen);
        }
        return Png(cut);
    }

    private static byte[] Shrink(SKBitmap bitmap)
    {
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * (double)WholeWidth / bitmap.Width));
        using var small = bitmap.Resize(new SKImageInfo(WholeWidth, height), new SKSamplingOptions(SKFilterMode.Linear));
        return Png(small);
    }

    private static byte[] Png(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 90);
        return png.ToArray();
    }
}
