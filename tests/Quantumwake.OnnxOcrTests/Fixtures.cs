using SkiaSharp;
using Quantumwake.Ocr.Onnx;

namespace Quantumwake.OnnxOcrTests;

/// <summary>
/// Drawn fixtures and the two things a run may lack: a font to draw with, and
/// the models to read with.
/// </summary>
/// <remarks>
/// <para>
/// As in <c>Quantumwake.OcrTests</c>, fixtures are drawn at test time rather
/// than checked in, so the engine is fed real pixels without a binary in the
/// repository. SkiaSharp's Linux build here links no fontconfig, so a font
/// is opened by file from the places distributions keep one; a machine with
/// none stands the drawing tests down, and says so.
/// </para>
/// <para>
/// The models are fetched once per machine into the app's model folder, the
/// same way the app fetches them, and kept: CI caches that folder. A runner
/// with no network stands the reading tests down rather than failing for a
/// reason that is nothing to do with the code.
/// </para>
/// </remarks>
internal static class Fixtures
{
    private static readonly string[] FontFiles =
    [
        "/usr/share/fonts/liberation-sans-fonts/LiberationSans-Regular.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/TTF/DejaVuSans.ttf",
        @"C:\Windows\Fonts\arial.ttf",
        @"C:\Windows\Fonts\segoeui.ttf",
        "/System/Library/Fonts/Supplemental/Arial.ttf",
    ];

    public static SKTypeface? Font { get; } = FontFiles.Where(File.Exists).Select(SKTypeface.FromFile).FirstOrDefault(t => t is not null);

    private static readonly Lazy<Task<string?>> ModelsReady = new(async () =>
    {
        try
        {
            using var patience = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await OnnxModels.EnsureAsync(token: patience.Token);
            return null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            return $"models unavailable: {e.GetType().Name}";
        }
    });

    /// <summary>Null when the models are on disk, else why the reading tests stand down.</summary>
    public static Task<string?> WhyNoModels() => ModelsReady.Value;

    /// <summary>
    /// A dark panel with light text on it, the way the game draws a tooltip,
    /// written to a PNG. Returns where the text's baseline-left landed.
    /// </summary>
    public static string Panel(string path, string text, int width, int height, float size, float left, float baseline,
        float skew = 0, SKColor? mark = null, SKRectI? markAt = null)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(0x10, 0x18, 0x20));

        if (mark is { } colour && markAt is { } at)
        {
            using var fill = new SKPaint { Color = colour, IsAntialias = false };
            canvas.DrawRect(at, fill);
        }

        using var font = new SKFont(Font ?? SKTypeface.Default, size) { Edging = SKFontEdging.Antialias, SkewX = -skew };
        using var paint = new SKPaint { Color = new SKColor(0xD8, 0xE8, 0xF0), IsAntialias = true };
        canvas.DrawText(text, left, baseline, SKTextAlign.Left, font, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
