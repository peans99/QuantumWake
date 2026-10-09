using System.Globalization;
using System.IO;
using System.Text.Json;
using Quantumwake.Data;
using Quantumwake.Ocr;
using Xunit.Abstractions;

namespace Quantumwake.OcrTests;

/// <summary>
/// What the engine makes of a mining frame: the whole read, then the scan
/// panel's second look rung by rung.
/// </summary>
/// <remarks>
/// <para>
/// Stands down unless <c>QUANTUMWAKE_MINING_FRAMES</c> names image files,
/// separated by semicolons - it needs somebody's own screenshots, and a build
/// going green must not. Every line is printed in the CLI's
/// <c>[left top hHeight] text</c> form so that a frame's output can be pasted
/// into a fixture or fed to <c>--screen</c> as it stands.
/// </para>
/// <para>
/// The rungs are all printed rather than stopping at agreement, for the
/// reason given on <see cref="WalletFrameMeasurement"/>: the point is seeing
/// how near the edge a frame is.
/// </para>
/// </remarks>
public class MiningFrameMeasurement(ITestOutputHelper output)
{
    private static string[] Frames =>
        (Environment.GetEnvironmentVariable("QUANTUMWAKE_MINING_FRAMES") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [Fact]
    public async Task Each_frame_whole_and_by_the_scan_panel()
    {
        var frames = Frames;

        if (frames.Length == 0)
        {
            output.WriteLine(
                "QUANTUMWAKE_MINING_FRAMES is not set, so there is nothing to measure. "
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
            Print(lines);

            var scan = await MiningSecondLook.SettleAsync(lines,
                (patch, treatment, token) => reader.ReadAsync(frame, patch, treatment, token), Commodities);
            var read = ScreenFrames.Read(lines, [], [], Commodities, scan);

            output.WriteLine($"  the app: {read.Kind}, wallet {read.Wallet?.Balance}");
            if (read.Mining is { } m) output.WriteLine("  " + JsonSerializer.Serialize(m));
            if (read.Refinery is { } r) output.WriteLine("  " + JsonSerializer.Serialize(r));

            if (ScreenFrames.ScanPanel(lines) is not { } panel)
                continue;

            output.WriteLine($"  scan panel: {panel.Left:0},{panel.Top:0} {panel.Width:0}x{panel.Height:0}");

            foreach (var treatment in MiningSecondLook.Treatments)
            {
                var again = await reader.ReadAsync(frame, panel, treatment);
                output.WriteLine($"  --- x{treatment.Scale} shear {treatment.Shear:0.00}: {again.Count} lines");
                Print(again);
            }
        }
    }

    /// <summary>The names the install's table gives the minerals on these frames, raw and refined.</summary>
    private static readonly string[] Commodities =
    [
        "Raw Silicon", "Silicon", "Raw Hephaestanite", "Hephaestanite", "Aphorite",
        "Aslarite (Raw)", "Aslarite", "Agricium (Ore)", "Agricium",
    ];

    private void Print(IReadOnlyList<ScreenTextLine> lines)
    {
        foreach (var line in lines.OrderBy(l => l.Top).ThenBy(l => l.Left))
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[{line.Left:0} {line.Top:0} h{line.Height:0}] {line.Text}"));
        }
    }
}
