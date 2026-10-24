using System.Globalization;
using System.IO;
using Quantumwake.Data;
using Quantumwake.Ocr;
using Xunit.Abstractions;

namespace Quantumwake.OcrTests;

/// <summary>
/// What the engine and the bar reader make of a Rep app frame.
/// </summary>
/// <remarks>
/// Stands down unless <c>QUANTUMWAKE_REP_FRAMES</c> names image files,
/// separated by semicolons, for the reason <see cref="MiningFrameMeasurement"/>
/// does. Lines print in the CLI's <c>[left top hHeight] text</c> form so a
/// frame can be pasted into a fixture as it stands.
/// </remarks>
public class RepFrameMeasurement(ITestOutputHelper output)
{
    private static string[] Frames =>
        (Environment.GetEnvironmentVariable("QUANTUMWAKE_REP_FRAMES") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// <c>QUANTUMWAKE_REP_STORE</c> names a folder to keep the readings in, as
    /// the app would. Never the app's own folder: point it at a copy.
    /// </summary>
    private readonly ScreenReadingStore? _store =
        Environment.GetEnvironmentVariable("QUANTUMWAKE_REP_STORE") is { Length: > 0 } dir ? new ScreenReadingStore(dir) : null;

    /// <summary><c>QUANTUMWAKE_REP_QUIET</c> set prints the bars without the lines.</summary>
    private static bool Quiet => Environment.GetEnvironmentVariable("QUANTUMWAKE_REP_QUIET") is { Length: > 0 };

    [Fact]
    public async Task Each_frame_its_lines_and_its_bars()
    {
        var frames = Frames;

        if (frames.Length == 0)
        {
            output.WriteLine(
                "QUANTUMWAKE_REP_FRAMES is not set, so there is nothing to measure. "
                + "Set it to one or more image files separated by semicolons.");
            return;
        }

        var reader = new WindowsScreenReader();

        if (!reader.Available)
        {
            output.WriteLine("no OCR engine on this machine");
            return;
        }

        foreach (var frame in frames)
        {
            var lines = await reader.ReadAsync(frame);

            output.WriteLine($"=== {Path.GetFileName(frame)}: {lines.Count} lines");

            if (!Quiet)
            {
                foreach (var line in lines.OrderBy(l => l.Top).ThenBy(l => l.Left))
                {
                    output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"[{line.Left:0} {line.Top:0} h{line.Height:0}] {line.Text}"));
                }
            }

            var bars = await RepBars.MeasureAsync(lines, (patch, token) => reader.PixelsAsync(frame, patch, token));
            var rep = ScreenFrames.Read(lines, [], [])?.Reputation?.WithBars(bars);

            // Into a copy of the app's folder, for a second server to draw the
            // Reputation page from without an engine of its own.
            if (_store is { } store && rep is not null)
            {
                store.Add(new ScreenSighting(
                    Path.GetFileName(frame), new DateTimeOffset(File.GetLastWriteTimeUtc(frame), TimeSpan.Zero),
                    ScreenKind.Reputation, $"reputation with {rep.Organisation}", [], null, null, null, null,
                    [.. lines.Select(l => l.Text)], 0, Reputation: rep));
            }

            output.WriteLine($"  {rep?.Organisation} / {rep?.Standing} / {rep?.Section}: "
                + $"rank {rep?.Rank ?? "(none)"} at {rep?.Progress:P1}");

            // The bars again with where they were found, to set against the
            // picture: a fill that looks wrong is usually a track end lost.
            var cards = ScreenFrames.RepCards(lines);
            var grid = cards.Count == 0 ? null : await reader.PixelsAsync(frame, new ScreenPatch(0, 0, 100_000, 100_000));

            foreach (var card in cards)
            {
                var bar = grid is null ? null : RepBars.Measure(grid, card.Label);
                var where = bar is null ? "" : $"  row {bar.Row} x {bar.Left}-{bar.Right} teal to {bar.Reached}";
                output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"    {card.Name,-26} {(bar is null ? "no bar" : bar.Fill.ToString("P1", CultureInfo.InvariantCulture)),8}{where}  {card.Perk}"));
            }
        }
    }
}
