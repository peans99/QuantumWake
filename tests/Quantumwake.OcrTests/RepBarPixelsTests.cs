using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Quantumwake.Data;
using Quantumwake.Ocr;

namespace Quantumwake.OcrTests;

/// <summary>
/// The colour patch the bar reader measures, through the real decoder.
/// </summary>
/// <remarks>
/// Needs no OCR engine, so unlike the rest of this project it never stands
/// down: decoding is the OS's imaging stack, which every Windows install has.
/// What it defends is the wrapping - channels in the right order, a patch
/// addressed in the frame's own pixels - because a red and blue swapped
/// would read the teal fill as track and every rank as empty, and nothing
/// else in the suites would notice.
/// </remarks>
public class RepBarPixelsTests : IDisposable
{
    private readonly List<string> _written = [];

    private static readonly Color Card = Color.FromRgb(56, 74, 87);
    private static readonly Color Track = Color.FromRgb(66, 92, 113);
    private static readonly Color Teal = Color.FromRgb(144, 255, 212);

    [Fact]
    public async Task A_patch_comes_back_in_frame_coordinates_with_its_colours_the_right_way_round()
    {
        var path = Png(fill: 0.25);
        var patch = await new WindowsScreenReader().PixelsAsync(path, new ScreenPatch(100, 40, 300, 60));

        Assert.NotNull(patch);
        Assert.Equal(100, patch.Left);
        Assert.Equal(40, patch.Top);

        Assert.Equal((Teal.R, Teal.G, Teal.B), patch.At(130, 60));
        Assert.Equal((Track.R, Track.G, Track.B), patch.At(250, 60));
        Assert.Equal((Card.R, Card.G, Card.B), patch.At(130, 90));
    }

    [Fact]
    public async Task The_bar_measures_off_a_decoded_file()
    {
        var path = Png(fill: 0.25);
        var label = new ScreenTextLine("NEUTRAL", 123, 75, 14);
        var patch = await new WindowsScreenReader().PixelsAsync(path, new ScreenPatch(0, 0, 640, 160));

        var bar = RepBars.Measure(patch!, label)!;

        Assert.Equal(120, bar.Left);
        Assert.Equal(289, bar.Right);
        Assert.InRange(bar.Fill, 0.25 - 1.0 / 170, 0.25 + 1.0 / 170);
    }

    /// <summary>A card with a 170-pixel bar seven rows thick, its top at row 57, filled from x = 120.</summary>
    private string Png(double fill)
    {
        var visual = new DrawingVisual();

        using (var draw = visual.RenderOpen())
        {
            draw.DrawRectangle(new SolidColorBrush(Card), null, new Rect(0, 0, 640, 160));
            draw.DrawRectangle(new SolidColorBrush(Track), null, new Rect(120, 57, 170, 7));
            draw.DrawRectangle(new SolidColorBrush(Teal), null, new Rect(120, 57, Math.Round(170 * fill), 7));
        }

        var target = new RenderTargetBitmap(640, 160, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));

        var path = Path.Combine(Path.GetTempPath(), $"qw-rep-{Guid.NewGuid():N}.png");
        using (var file = File.Create(path)) encoder.Save(file);

        _written.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _written)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A temp file left behind is not worth failing a test over.
            }
        }

        GC.SuppressFinalize(this);
    }
}
