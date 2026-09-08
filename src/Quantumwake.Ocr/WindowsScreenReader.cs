using Quantumwake.Data;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace Quantumwake.Ocr;

/// <summary>
/// Reads a screenshot with the engine Windows already has.
/// </summary>
/// <remarks>
/// <para>
/// No package reference and no model to ship: <c>Windows.Media.Ocr</c> and its
/// English data are on the machine. Measured on this install, a 3440x1440
/// frame goes through whole in about 170 ms, which is why nothing here crops
/// or rescales - both were tried and neither helped. Upscaling made a line
/// worse, dropping a whole word at three times the size.
/// </para>
/// <para>
/// The engine is created once. Creating one per call is the obvious way to
/// write this and costs more than the recognition does.
/// </para>
/// </remarks>
public sealed class WindowsScreenReader : IScreenReader
{
    private readonly OcrEngine? _engine =
        OcrEngine.TryCreateFromLanguage(new Language("en-US"))
        ?? OcrEngine.TryCreateFromUserProfileLanguages();

    /// <summary>Whether this machine has an engine at all.</summary>
    /// <remarks>
    /// A Windows install with no English language pack has none. Better asked
    /// once at startup than discovered by a pilot pressing the button.
    /// </remarks>
    public bool Available => _engine is not null;

    public async Task<IReadOnlyList<ScreenTextLine>> ReadAsync(
        string imagePath, CancellationToken token = default)
    {
        if (_engine is null) return [];

        var file = await StorageFile.GetFileFromPathAsync(imagePath).AsTask(token);
        using var stream = await file.OpenAsync(FileAccessMode.Read).AsTask(token);

        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);
        using var bitmap = await decoder.GetSoftwareBitmapAsync().AsTask(token);

        var result = await _engine.RecognizeAsync(bitmap).AsTask(token);

        return [.. result.Lines
            .Where(line => line.Words.Count > 0)
            .Select(line => new ScreenTextLine(
                line.Text,
                line.Words.Min(w => w.BoundingRect.Left),
                line.Words.Min(w => w.BoundingRect.Top),

                // The tallest word, because a line of mixed case is as tall as
                // its capitals and the distances measured against this are all
                // in line heights.
                line.Words.Max(w => w.BoundingRect.Height)))];
    }
}
