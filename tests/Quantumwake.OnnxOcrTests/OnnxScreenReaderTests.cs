using Quantumwake.Data;
using Quantumwake.Ocr.Onnx;
using SkiaSharp;

namespace Quantumwake.OnnxOcrTests;

/// <summary>
/// The reader that runs anywhere: a real file goes in, lines come back with
/// the geometry the readers measure distances against, and the second look
/// maps its boxes into the frame's own pixels.
/// </summary>
/// <remarks>
/// What is defended is the wrapping and the geometry, not the recognition,
/// which is the models'. The fixtures are drawn large and clean so that a
/// marginal engine cannot fail them for its own reasons; whether the readers
/// survive this engine on real frames is the measurement in docs/linux.md.
/// </remarks>
[Collection("onnx")]
public sealed class OnnxScreenReaderTests : IDisposable
{
    private static readonly OnnxScreenReader Reader = new();

    private readonly List<string> _written = [];

    public void Dispose()
    {
        foreach (var file in _written)
            try { File.Delete(file); } catch (IOException) { }
    }

    private string Png(string name) =>
        Path.Combine(Path.GetTempPath(), $"qw-onnx-{name}-{Guid.NewGuid():N}.png");

    private string Panel(string text, int width = 1200, int height = 400, float size = 40, float left = 100, float baseline = 220, float skew = 0)
    {
        var path = Png("panel");
        _written.Add(path);
        return Fixtures.Panel(path, text, width, height, size, left, baseline, skew);
    }

    [Fact]
    public async Task A_real_file_comes_back_as_lines()
    {
        if (Fixtures.Font is null || await Fixtures.WhyNoModels() is not null) return;

        var lines = await Reader.ReadAsync(Panel("Arlington Rifle"));

        Assert.Contains(lines, line => line.Text.Contains("Arlington", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Every distance the matcher measures is in line heights, and in the
    /// frame's pixels: the box has to sit where the text was drawn, at about
    /// the glyphs' own height rather than the detector's padded one.
    /// </summary>
    [Fact]
    public async Task A_line_sits_where_it_was_drawn_at_the_height_of_its_glyphs()
    {
        if (Fixtures.Font is null || await Fixtures.WhyNoModels() is not null) return;

        var lines = await Reader.ReadAsync(Panel("Manufacturer: Hedeby Gunworks", size: 40, left: 100, baseline: 220));

        var line = Assert.Single(lines, l => l.Text.Contains("Hedeby", StringComparison.OrdinalIgnoreCase));

        // Cap height of a 40 px face is about 29 px; with descenders the ink
        // runs to about 37. The detector's own box would be 50 or more.
        Assert.InRange(line.Height, 24, 44);
        Assert.InRange(line.Left, 90, 112);
        Assert.InRange(line.Top, 180, 200);
    }

    [Fact]
    public async Task Two_words_on_a_row_are_one_line_and_a_row_apart_are_two()
    {
        if (Fixtures.Font is null || await Fixtures.WhyNoModels() is not null) return;

        var path = Png("rows");
        _written.Add(path);
        Fixtures.Panel(path, "Item Type: Rifle", 1200, 400, 40, 100, 150);

        // A second row drawn into the same file by reopening it.
        using (var bitmap = SKBitmap.Decode(path))
        using (var canvas = new SKCanvas(bitmap))
        using (var font = new SKFont(Fixtures.Font, 40))
        using (var paint = new SKPaint { Color = new SKColor(0xD8, 0xE8, 0xF0), IsAntialias = true })
        {
            canvas.DrawText("Class: Ballistic", 100, 300, SKTextAlign.Left, font, paint);
            canvas.Flush();
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
        }

        var lines = await Reader.ReadAsync(path);

        Assert.Contains(lines, l => l.Text.Replace(" ", "").Contains("Type:Rifle", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, l => l.Text.Replace(" ", "").Contains("Class:Ballistic", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, l => l.Text.Contains("Rifle", StringComparison.OrdinalIgnoreCase) && l.Text.Contains("Ballistic", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The patch read's boxes come back in frame pixels - not the crop's, and
    /// not multiplied by the scale - or the wallet's "left of the bar, near
    /// its row" test would be measuring against the wrong frame.
    /// </summary>
    [Fact]
    public async Task A_second_look_maps_its_boxes_back_into_the_frame()
    {
        if (Fixtures.Font is null || await Fixtures.WhyNoModels() is not null) return;

        var path = Panel("Balance", size: 36, left: 700, baseline: 260);

        var lines = await Reader.ReadAsync(path, new ScreenPatch(600, 180, 400, 120), new ScreenTreatment(2, 0));

        var line = Assert.Single(lines, l => l.Text.Contains("Balance", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(line.Left, 690, 712);
        Assert.InRange(line.Top, 225, 245);
        Assert.InRange(line.Height, 22, 40);
    }

    /// <summary>
    /// Drawn leaning right by the shear's own angle and read with that shear:
    /// the wrong sign would double the slant, and the box would move.
    /// </summary>
    [Fact]
    public async Task A_sheared_look_stands_a_slanted_line_up_and_still_places_it()
    {
        if (Fixtures.Font is null || await Fixtures.WhyNoModels() is not null) return;

        var path = Panel("Hedeby", size: 48, left: 500, baseline: 240, skew: 0.2f);

        var lines = await Reader.ReadAsync(path, new ScreenPatch(400, 150, 500, 150), new ScreenTreatment(2, 0.2));

        var line = Assert.Single(lines, l => l.Text.Contains("Hedeby", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(line.Left, 480, 530);
        Assert.InRange(line.Top, 195, 215);
    }

    [Fact]
    public async Task A_patch_asked_for_off_the_frame_is_clamped_rather_than_thrown()
    {
        if (Fixtures.Font is null || await Fixtures.WhyNoModels() is not null) return;

        var path = Panel("Edge", size: 36, left: 1000, baseline: 380);

        var lines = await Reader.ReadAsync(path, new ScreenPatch(900, 300, 600, 300), new ScreenTreatment(1.5, 0));

        Assert.Contains(lines, l => l.Text.Contains("Edge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Pixels_come_back_in_colour_addressed_in_frame_pixels()
    {
        if (Fixtures.Font is null) return;

        var path = Png("pixels");
        _written.Add(path);
        Fixtures.Panel(path, "x", 400, 200, 20, 10, 190, mark: new SKColor(0x20, 0xB0, 0xA0), markAt: new SKRectI(100, 50, 160, 90));

        var patch = await Reader.PixelsAsync(path, new ScreenPatch(90, 40, 100, 70));

        Assert.NotNull(patch);
        Assert.Equal((0x20, 0xB0, 0xA0), patch.At(130, 70));
        Assert.Equal((0x10, 0x18, 0x20), patch.At(95, 45));

        // A pixel outside the patch reads as the nearest edge, not a throw.
        Assert.Equal((0x10, 0x18, 0x20), patch.At(0, 0));
    }
}

/// <summary>The pieces around the networks, which need no model to check.</summary>
[Collection("onnx")]
public sealed class OnnxPiecesTests
{
    [Fact]
    public void The_character_tables_are_whole()
    {
        Assert.Equal(95, OnnxModels.Characters(OnnxModels.EnglishRecogniser).Length);
        Assert.Equal(6623, OnnxModels.Characters(OnnxModels.MultilingualRecogniser).Length);
        Assert.Equal("0", OnnxModels.Characters(OnnxModels.EnglishRecogniser)[0]);
    }

    [Fact]
    public void Nothing_is_present_in_an_empty_folder()
    {
        var before = Environment.GetEnvironmentVariable("QUANTUMWAKE_OCR_MODELS");
        var empty = Path.Combine(Path.GetTempPath(), "qw-no-models-" + Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("QUANTUMWAKE_OCR_MODELS", empty);
            Assert.False(OnnxModels.Present());
            Assert.Equal(empty, OnnxModels.Folder);
        }
        finally
        {
            Environment.SetEnvironmentVariable("QUANTUMWAKE_OCR_MODELS", before);
        }
    }

    /// <summary>
    /// The detector's crop is the kernel grown back out by a ratio; the ink
    /// inside it is what the line's height has to be.
    /// </summary>
    [Fact]
    public void Ink_is_measured_inside_the_padded_crop()
    {
        var width = 120;
        var height = 60;
        var bgra = new byte[width * height * 4];

        // Dark everywhere, a light block from (20, 15) to (99, 44).
        for (var i = 0; i < width * height; i++)
        {
            var x = i % width;
            var y = i / width;
            var light = x is >= 20 and < 100 && y is >= 15 and < 45;
            bgra[i * 4] = bgra[i * 4 + 1] = bgra[i * 4 + 2] = light ? (byte)230 : (byte)20;
            bgra[i * 4 + 3] = 255;
        }

        using var crop = FrameAccess.From(width, height, bgra);
        var ink = InkBounds.Measure(crop, 1000, 500);

        Assert.Equal(1020, ink.Left);
        Assert.Equal(515, ink.Top);
        Assert.Equal(80, ink.Width);
        Assert.Equal(30, ink.Height);
    }

    [Fact]
    public void Words_are_grouped_into_lines_by_row_and_gap()
    {
        static (TextBox, string, InkBounds) Word(string text, double left, double top, double width, double height) =>
            (new TextBox(left - 4, top - 4, width + 8, height + 8, 0.9), text, new InkBounds(left, top, width, height));

        var lines = OnnxScreenReader.Lines(
        [
            Word("Rifle", 200, 100, 60, 16),
            Word("Item", 100, 101, 50, 16),
            Word("Type:", 155, 100, 40, 15),
            Word("Class:", 100, 140, 60, 16),
            Word("MAPS", 900, 102, 70, 16),   // same row, far across: a toolbar, not this line
        ]);

        Assert.Equal(3, lines.Count);
        Assert.Equal("Item Type: Rifle", lines[0].Text);
        Assert.Equal(100, lines[0].Left);
        Assert.Equal(16, lines[0].Height);
        Assert.Equal("MAPS", lines[1].Text);
        Assert.Equal("Class:", lines[2].Text);
    }
}

/// <summary>Lets the pieces tests build a frame from bytes without a file.</summary>
internal static class FrameAccess
{
    public static Frame From(int width, int height, byte[] bgra) => Frame.FromPixels(width, height, bgra);
}
