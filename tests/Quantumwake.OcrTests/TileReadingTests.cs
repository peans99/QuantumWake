using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Quantumwake.Data;
using Quantumwake.Ocr;
using Xunit.Abstractions;

namespace Quantumwake.OcrTests;

/// <summary>
/// Turning a screenshot into tiles, and tiles into a name.
/// </summary>
/// <remarks>
/// <para>
/// Here for the reason the rest of this project is: decoding an image needs
/// the Windows SDK target framework, and the arithmetic that follows it - in
/// <see cref="TileMatcher"/> and <see cref="TileGrid"/> - is tested in the
/// parser suite where it belongs, on drawn fixtures and without an OS.
/// </para>
/// <para>
/// The second class below is not really a test. It is the instrument for the
/// one question this feature is gated on and cannot answer from a single
/// screenshot: whether the same item, photographed twice, renders alike enough
/// to recognise. Point it at two frames of the same stash and it prints the
/// answer:
/// </para>
/// <code>
/// $env:QUANTUMWAKE_TILE_FRAMES = "C:\a.jpg;C:\b.jpg"
/// dotnet test tests\Quantumwake.OcrTests -c Release `
///   --filter FullyQualifiedName~TilePairMeasurement -l "console;verbosity=detailed"
/// </code>
/// <para>
/// With the variable unset it stands down, the same way every test here stands
/// down on a machine with no OCR engine. A measurement that needs somebody's
/// own screenshots cannot be a condition of the build going green.
/// </para>
/// </remarks>
public class TileReadingTests : IDisposable
{
    private readonly List<string> _written = [];

    /// <summary>
    /// A panel with a row of tiles on it, drawn rather than photographed.
    /// </summary>
    /// <remarks>
    /// Deliberately unlike a screenshot in everything but the one distinction
    /// the detector rests on. What is being defended is the wrapping - that a
    /// real file on disk goes in and boxes in the right places come out - and
    /// a fixture that tried to look like Star Citizen would defend less while
    /// claiming more.
    /// </remarks>
    private string PanelFile(int tiles = 4)
    {
        const int width = 700, height = 400;
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(8, 9, 12)), null,
                new Rect(0, 0, width, height));

            for (var i = 0; i < tiles; i++)
            {
                context.DrawRectangle(
                    new SolidColorBrush(Color.FromRgb(40, 44, 52)), null,
                    new Rect(40 + i * 150, 120, 130, 160));

                // Something with a shape in it, and a different shape in each,
                // so the matcher has more to go on than a flat rectangle. Four
                // sizes of the same ellipse is not enough: two of them came
                // within 0.06 of each other, which is a near-tie and correctly
                // reported as one.
                var ink = new SolidColorBrush(Color.FromRgb(200, 210, 225));
                var centre = new Point(105 + i * 150, 200);

                switch (i)
                {
                    case 0:
                        context.DrawEllipse(ink, null, centre, 45, 20);
                        break;
                    case 1:
                        context.DrawRectangle(ink, null, new Rect(centre.X - 30, 150, 60, 100));
                        break;
                    case 2:
                        context.DrawEllipse(ink, null, centre, 18, 55);
                        context.DrawRectangle(ink, null, new Rect(centre.X - 45, 230, 90, 20));
                        break;
                    default:
                        context.DrawRectangle(ink, null, new Rect(centre.X - 40, 150, 20, 110));
                        context.DrawEllipse(ink, null, new Point(centre.X + 25, 175), 25, 25);
                        break;
                }
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        var path = Path.Combine(Path.GetTempPath(), $"qw-tiles-{Guid.NewGuid():N}.png");
        using (var file = File.Create(path)) encoder.Save(file);

        _written.Add(path);
        return path;
    }

    [Fact]
    public async Task A_file_on_disk_becomes_brightness()
    {
        var image = await new WindowsTileImageLoader().LoadAsync(PanelFile());

        Assert.Equal(700, image.Width);
        Assert.Equal(400, image.Height);

        // The panel is dark and the tiles are not, which is the whole of what
        // the detector below asks of an image.
        Assert.True(image.At(10, 10) < 20, $"panel read {image.At(10, 10):0.0}");
        Assert.True(image.At(50, 130) > 30, $"tile read {image.At(50, 130):0.0}");
    }

    [Fact]
    public async Task The_tiles_of_a_real_file_are_found_where_they_were_drawn()
    {
        var image = await new WindowsTileImageLoader().LoadAsync(PanelFile());

        var bands = TileGrid.Find(image, 30, 60, 620, 340);

        var band = Assert.Single(bands);
        Assert.Equal(4, band.Tiles.Count);

        // Where the brightness changes rather than where the rectangle was
        // drawn, so the centres are about right rather than exactly so. That
        // is all they have to be: the matcher searches for the rest.
        Assert.InRange(band.Tiles[0].CentreX, 102, 108);
        Assert.InRange(band.Tiles[3].CentreX, 552, 558);
        Assert.InRange(band.Tiles[0].CentreY, 197, 203);
    }

    [Fact]
    public async Task A_tile_is_recognised_from_a_picture_of_another_tile()
    {
        var loader = new WindowsTileImageLoader();
        var taught = await loader.LoadAsync(PanelFile());
        var seen = await loader.LoadAsync(PanelFile());

        var first = TileGrid.Find(taught, 30, 60, 620, 340)[0].Tiles;
        var again = TileGrid.Find(seen, 30, 60, 620, 340)[0].Tiles;

        var template = TilePatch.From(taught, first[1].CentreX, first[1].CentreY, first[1].Half);

        var scored = again
            .Select((tile, i) => new TileCandidate(
                $"tile {i}",
                TileMatcher.Best(template, seen, tile.CentreX, tile.CentreY,
                    TileSearch.For(tile.Half)).Score))
            .ToList();

        var verdict = TileDecision.Decide(scored);

        Assert.Equal(TileConfidence.Named, verdict.Confidence);
        Assert.Equal("tile 1", verdict.Name);
    }

    public void Dispose()
    {
        foreach (var path in _written)
        {
            try { File.Delete(path); } catch (IOException) { }
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// The measurement the portfolio is gated on, run against real frames.
/// </summary>
/// <remarks>
/// Stands down unless <c>QUANTUMWAKE_TILE_FRAMES</c> names one or two image
/// files, separated by a semicolon. One frame prints the grid it found. Two
/// print every tile of the first against every tile of the second, which is
/// the determinism question stated as a table: the diagonal should be high,
/// everything else lower, and the gap between them is what a threshold would
/// have to live in.
/// </remarks>
public class TilePairMeasurement(ITestOutputHelper output)
{
    private static string[] Frames =>
        (Environment.GetEnvironmentVariable("QUANTUMWAKE_TILE_FRAMES") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The region to look in, as fractions of the frame.</summary>
    /// <remarks>
    /// The detector has to be given a panel rather than a screen. Defaults to
    /// the right-hand panel, where a location's stash sits, and is overridable
    /// with <c>QUANTUMWAKE_TILE_REGION</c> as <c>left,top,right,bottom</c> in
    /// fractions, because nothing here knows where the pilot's panels were.
    /// </remarks>
    private static (double Left, double Top, double Right, double Bottom) Region()
    {
        var set = Environment.GetEnvironmentVariable("QUANTUMWAKE_TILE_REGION");
        if (set is null) return (0.665, 0.23, 0.840, 0.56);

        var parts = set.Split(',').Select(double.Parse).ToArray();
        return (parts[0], parts[1], parts[2], parts[3]);
    }

    private IReadOnlyList<TileBand> BandsOf(GreyImage frame)
    {
        var (left, top, right, bottom) = Region();

        int x0 = (int)(frame.Width * left), y0 = (int)(frame.Height * top);
        int x1 = (int)(frame.Width * right), y1 = (int)(frame.Height * bottom);

        var bands = TileGrid.Find(frame, x0, y0, x1, y1);

        if (bands.Count == 0 || bands.All(band => band.Tiles.Count == 0))
        {
            // Not a bare zero. Nearly every way this goes wrong is the region
            // rather than the frame - a panel the pilot had somewhere else, or
            // a region so much wider than the tiles that they vanish into the
            // average - and the reader cannot tell which without these.
            output.WriteLine(
                $"  no tiles in {x0},{y0} to {x1},{y1} ({bands.Count} bands). "
                + "Set QUANTUMWAKE_TILE_REGION to left,top,right,bottom in fractions.");

            var rows = new double[y1 - y0 + 1];
            for (var y = y0; y <= y1; y++)
            {
                double sum = 0;
                for (var x = x0; x <= x1; x++) sum += frame.At(x, y);
                rows[y - y0] = sum / (x1 - x0 + 1);
            }

            output.WriteLine(
                $"  rows in that region run {rows.Min():0.0} to {rows.Max():0.0}, "
                + $"mean {rows.Average():0.0}");

            foreach (var band in bands)
                output.WriteLine($"  band {band.Top}..{band.Bottom}, {band.Tiles.Count} tiles");

            foreach (var band in bands)
                output.WriteLine($"  band {band.Top}..{band.Bottom}, {band.Tiles.Count} tiles");
        }

        return bands;
    }

    [Fact]
    public async Task Tiles_across_two_frames()
    {
        var frames = Frames;

        if (frames.Length == 0)
        {
            output.WriteLine(
                "QUANTUMWAKE_TILE_FRAMES is not set, so there is nothing to measure. "
                + "Set it to one or two image files separated by a semicolon.");
            return;
        }

        var loader = new WindowsTileImageLoader();

        var first = await loader.LoadAsync(frames[0]);
        var taught = BandsOf(first).SelectMany(band => band.Tiles).ToList();

        output.WriteLine($"{Path.GetFileName(frames[0])}  {first.Width}x{first.Height}");
        foreach (var tile in taught)
            output.WriteLine(
                $"  tile at {tile.CentreX:0} x {tile.CentreY:0}, "
                + $"{tile.HalfWidth * 2:0} wide by {tile.HalfHeight * 2:0}");

        Assert.NotEmpty(taught);

        if (frames.Length == 1) return;

        var second = await loader.LoadAsync(frames[1]);
        var bands = BandsOf(second);
        Assert.NotEmpty(bands);

        output.WriteLine("");
        output.WriteLine($"{Path.GetFileName(frames[1])}  {second.Width}x{second.Height}");

        foreach (var band in bands)
            output.WriteLine($"  band {band.Top}..{band.Bottom}, {band.Tiles.Count} tiles");

        output.WriteLine("");

        // Every taught picture is looked for along the whole of the second
        // frame's band rather than at the tiles found in it. Two reasons, and
        // the second is the one that will still hold when this is a feature:
        // the detector is the weakest part of this and should not be trusted
        // twice in one measurement, and a pilot's stash does not keep its
        // order between two visits, so a picture has to be found rather than
        // looked up.
        foreach (var band in bands)
        {
            var left = band.Tiles.Min(tile => tile.CentreX - tile.HalfWidth);
            var right = band.Tiles.Max(tile => tile.CentreX + tile.HalfWidth);
            var middle = (left + right) / 2;
            var centreY = (band.Top + band.Bottom) / 2.0;

            output.WriteLine($"band {band.Top}..{band.Bottom}, searched from {left:0} to {right:0}");

            foreach (var (tile, i) in taught.Select((tile, i) => (tile, i)))
            {
                var template = TilePatch.From(first, tile.CentreX, tile.CentreY, tile.Half);

                var match = TileMatcher.Best(
                    template, second, middle, centreY,
                    TileSearch.For(tile.Half) with { Across = (right - left) / 2 });

                output.WriteLine(
                    $"  taught #{i} (at {tile.CentreX:0}) fits best at "
                    + $"{middle + match.AcrossBy:0} x {centreY + match.DownBy:0}, "
                    + $"scale {match.Half / tile.Half:0.00}, score {match.Score:0.000}");
            }
        }

        output.WriteLine("");
        output.WriteLine(
            "Read it like this: each taught picture should find its own item, at a "
            + "sensible place, with a score near 1. A picture that lands on the wrong "
            + "item, or on the right one with a middling score, is the portfolio "
            + "failing - and the whole feature rests on it not failing.");
    }
}
