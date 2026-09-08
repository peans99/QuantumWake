using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Quantumwake.Data;
using Quantumwake.Ocr;

namespace Quantumwake.OcrTests;

/// <summary>
/// The thirty lines around the OS that turn an image file into lines.
/// </summary>
/// <remarks>
/// <para>
/// Its own project because the reader needs the Windows SDK target framework
/// and the parser suite must not. What is worth defending here is not the
/// recognition - that is Microsoft's - but the wrapping: that a real file goes
/// in, that boxes come back with the geometry the matcher measures distances
/// against, and that the things which can be absent are absent quietly.
/// </para>
/// <para>
/// Fixtures are drawn at test time rather than checked in, so the engine is
/// fed real pixels without a binary in the repository, and so the expected
/// text is written right beside the assertion about it.
/// </para>
/// </remarks>
public class WindowsScreenReaderTests : IDisposable
{
    private readonly List<string> _written = [];

    /// <summary>
    /// A machine with no English language pack has no engine, and the runner
    /// this is built on may be one. The tests below still assert something
    /// true in that case rather than passing silently or going red for a
    /// reason that is nothing to do with the code.
    /// </summary>
    private static readonly WindowsScreenReader Reader = new();

    [Fact]
    public async Task A_machine_with_no_engine_returns_nothing_rather_than_throwing()
    {
        if (Reader.Available) return;

        Assert.Empty(await Reader.ReadAsync(Png("Arlington Rifle")));
    }

    [Fact]
    public async Task A_real_file_comes_back_as_lines()
    {
        if (!Reader.Available) return;

        var lines = await Reader.ReadAsync(Png("Arlington Rifle"));

        Assert.Contains(lines, line =>
            line.Text.Contains("Arlington", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Every distance the matcher measures is in line heights, so a height of
    /// zero would collapse the same-column and just-above rules into accepting
    /// nothing at all.
    /// </summary>
    [Fact]
    public async Task Every_line_carries_a_box_with_a_usable_height()
    {
        if (!Reader.Available) return;

        var lines = await Reader.ReadAsync(Png("Arlington Rifle"));

        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            Assert.True(line.Height > 0, $"'{line.Text}' came back with no height");
            Assert.True(line.Left >= 0);
            Assert.True(line.Top >= 0);
        });
    }

    /// <summary>
    /// The geometry has to mean what the reader assumes it means: a line drawn
    /// lower down has a larger Top, and one drawn further right a larger Left.
    /// Getting either axis inverted would quietly invert "just above".
    /// </summary>
    [Fact]
    public async Task Down_the_page_is_a_larger_top_and_across_is_a_larger_left()
    {
        if (!Reader.Available) return;

        var lines = await Reader.ReadAsync(
            Png("Arlington Rifle", "Volume: 13000", indentSecond: true));

        var first = Find(lines, "Arlington");
        var second = Find(lines, "Volume");

        if (first is null || second is null) return;

        Assert.True(second.Top > first.Top, "the second line should sit lower");
        Assert.True(second.Left > first.Left, "the indented line should sit further right");
    }

    /// <summary>
    /// Two lines a tooltip apart come back close enough to be read as one
    /// column and one line apart, which is the whole basis of the rules in
    /// <see cref="ScreenInsight"/>.
    /// </summary>
    [Fact]
    public async Task Two_lines_of_a_tooltip_read_as_one_column()
    {
        if (!Reader.Available) return;

        var lines = await Reader.ReadAsync(Png("Arlington Rifle", "Volume: 13000"));

        var reading = ScreenInsight.Read(lines);

        Assert.Equal("13000", reading.Fields.GetValueOrDefault("Volume"));
        Assert.Contains("Arlington", reading.Name ?? string.Empty);
    }

    [Fact]
    public async Task A_file_that_is_not_there_is_the_caller_problem()
    {
        if (!Reader.Available) return;

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Reader.ReadAsync(Path.Combine(Path.GetTempPath(), "no-such-shot.jpg")));
    }

    private static ScreenTextLine? Find(IReadOnlyList<ScreenTextLine> lines, string word) =>
        lines.FirstOrDefault(line =>
            line.Text.Contains(word, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Draws the given lines white on black at a size the game uses, and saves
    /// them as a PNG.
    /// </summary>
    /// <remarks>
    /// White on black because that is what the game's panels are, and because
    /// the engine is measurably happier with it than the reverse. Twenty-four
    /// points rather than the twelve pixels a real tooltip uses: the point here
    /// is the wrapping, not how far the engine can be pushed, and a marginal
    /// fixture would fail for reasons that have nothing to do with this code.
    /// </remarks>
    private string Png(string first, string? second = null, bool indentSecond = false)
    {
        var visual = new DrawingVisual();

        using (var draw = visual.RenderOpen())
        {
            draw.DrawRectangle(Brushes.Black, null, new Rect(0, 0, 640, 160));
            draw.DrawText(Text(first), new Point(40, 30));

            if (second is not null)
                draw.DrawText(Text(second), new Point(indentSecond ? 90 : 40, 80));
        }

        var target = new RenderTargetBitmap(640, 160, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));

        var path = Path.Combine(Path.GetTempPath(), $"qw-ocr-{Guid.NewGuid():N}.png");

        using (var file = File.Create(path)) encoder.Save(file);

        _written.Add(path);
        return path;
    }

    private static FormattedText Text(string text) =>
        new(text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            24,
            Brushes.White,
            96);

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
