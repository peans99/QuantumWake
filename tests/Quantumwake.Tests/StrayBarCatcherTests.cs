using Quantumwake.Data;
using Quantumwake.Server;
using SkiaSharp;

namespace Quantumwake.Tests;

/// <summary>Pictures of a fracture bar the charge never did anything to (1.2.58).</summary>
public sealed class StrayBarCatcherTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 2, 51, 54, TimeSpan.Zero);
    private readonly string _root = Directory.CreateTempSubdirectory("qw-stray-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Folder => Path.Combine(_root, "stray-bars");

    /// <summary>A 3440x1440 frame, dark, with a yellow box where the bar was found.</summary>
    private string Frame()
    {
        var path = Path.Combine(_root, "frame.bmp");
        using var bitmap = new SKBitmap(3440, 1440);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(10, 14, 20));
            using var yellow = new SKPaint { Color = new SKColor(230, 200, 40) };
            canvas.DrawRect(SKRect.Create(1585, 692, 60, 24), yellow);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 90);
        File.WriteAllBytes(path, png.ToArray());
        return path;
    }

    private static readonly ScreenPatch Bar = new(1585, 692, 60, 24);
    private static FractureRecord Window(int minutes = 0) => new(T0.AddMinutes(minutes), T0.AddMinutes(minutes).AddSeconds(8), false, 0, 0, 0);

    [Fact]
    public void A_stray_is_kept_as_the_part_around_the_bar_outlined_and_the_whole_screen_small()
    {
        var catcher = new StrayBarCatcher(Folder);
        catcher.Hold(Frame(), Bar);
        Assert.True(catcher.Holding);

        var name = catcher.Keep(Window());

        Assert.NotNull(name);
        Assert.Equal(1, catcher.Count);
        using var around = SKBitmap.Decode(Path.Combine(Folder, name + "-around.png"));
        using var screen = SKBitmap.Decode(Path.Combine(Folder, name + "-screen.png"));
        Assert.Equal((3440 / 3, 1440 / 3), (around.Width, around.Height));
        Assert.Equal((StrayBarCatcher.WholeWidth, 268), (screen.Width, screen.Height));

        // The bar sits in the middle of the cut, ringed in magenta.
        var cx = around.Width / 2;
        var cy = around.Height / 2;
        Assert.Equal(SKColors.Magenta, around.GetPixel(cx, cy - 12 - 6));
        Assert.Equal(new SKColor(230, 200, 40), around.GetPixel(cx, cy));
    }

    [Fact]
    public void A_window_that_was_a_rock_leaves_nothing()
    {
        var catcher = new StrayBarCatcher(Folder);
        catcher.Hold(Frame(), Bar);
        catcher.Drop();

        Assert.Null(catcher.Keep(Window()));
        Assert.Equal(0, catcher.Count);
        Assert.False(Directory.Exists(Folder));
    }

    [Fact]
    public void Past_the_most_kept_nothing_more_is_held()
    {
        var catcher = new StrayBarCatcher(Folder);
        var frame = Frame();
        for (var i = 0; i < StrayBarCatcher.MostKept; i++)
        {
            catcher.Hold(frame, Bar);
            Assert.NotNull(catcher.Keep(Window(i)));
        }

        catcher.Hold(frame, Bar);
        Assert.False(catcher.Holding);
        Assert.Equal(StrayBarCatcher.MostKept, catcher.Count);
    }

    [Fact]
    public void A_frame_that_cannot_be_read_holds_nothing()
    {
        var catcher = new StrayBarCatcher(Folder);
        catcher.Hold(Path.Combine(_root, "missing.bmp"), Bar);
        Assert.False(catcher.Holding);
    }
}
