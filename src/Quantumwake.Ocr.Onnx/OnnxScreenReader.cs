using Microsoft.ML.OnnxRuntime;
using Quantumwake.Data;

namespace Quantumwake.Ocr.Onnx;

/// <summary>
/// Reads a screenshot with PaddleOCR's models on Microsoft's ONNX Runtime:
/// the screen reader for Linux, and for a Windows machine with no OCR
/// language pack.
/// </summary>
/// <remarks>
/// <para>
/// Same contract as <c>WindowsScreenReader</c>: lines with where they sat, a
/// second look at one patch scaled and sheared with the boxes mapped back,
/// and a patch of colour. The engine underneath is two networks rather than
/// one call - a detector that finds runs of text and a recogniser that reads
/// each crop - and the step between them, grouping the detector's boxes into
/// the lines the readers measure against, is this class's own.
/// </para>
/// <para>
/// The models are loaded on first use, and fetched first if they are not on
/// disk (<see cref="OnnxModels"/>). Loading is a few hundred milliseconds and
/// the sessions are kept; the runtime's sessions take concurrent calls.
/// </para>
/// </remarks>
public sealed class OnnxScreenReader(OnnxModel? recogniser = null) : IScreenReader, IDisposable
{
    private readonly OnnxModel _recogniserModel = recogniser ?? OnnxModels.EnglishRecogniser;
    private readonly SemaphoreSlim _loading = new(1, 1);
    private TextDetector? _detector;
    private TextRecogniser? _recogniser;
    private InferenceSession? _detectorSession, _recogniserSession;

    /// <summary>The files this reader runs on.</summary>
    public IReadOnlyList<OnnxModel> Models => [OnnxModels.Detector, _recogniserModel];

    /// <summary>True once the models are on disk; false means the first read will download them.</summary>
    public bool Ready => OnnxModels.Present(Models);

    /// <summary>Reports each download as it goes, for a status line somewhere.</summary>
    public IProgress<(OnnxModel Model, long Received)>? Progress { get; set; }

    public async Task<IReadOnlyList<ScreenTextLine>> ReadAsync(string imagePath, CancellationToken token = default)
    {
        await LoadAsync(token);

        return await Task.Run(() =>
        {
            using var frame = Frame.Load(imagePath);
            return Read(frame);
        }, token);
    }

    /// <remarks>
    /// Crop, scale and shear, then the same two networks, then the boxes
    /// undone the same three steps in reverse. The shear is undone at each
    /// box's own top, as the Windows reader does: exact for its left edge and
    /// within a pixel for the rest.
    /// </remarks>
    public async Task<IReadOnlyList<ScreenTextLine>> ReadAsync(
        string imagePath, ScreenPatch patch, ScreenTreatment treatment, CancellationToken token = default)
    {
        await LoadAsync(token);

        return await Task.Run(() =>
        {
            using var frame = Frame.Load(imagePath);

            // Clamp to the frame: a panel measured off a bar near an edge can
            // ask for pixels that are not there.
            var left = Math.Clamp(patch.Left, 0, frame.Width);
            var top = Math.Clamp(patch.Top, 0, frame.Height);
            var width = Math.Clamp(patch.Width, 0, frame.Width - left);
            var height = Math.Clamp(patch.Height, 0, frame.Height - top);

            if (width * treatment.Scale < 2 || height * treatment.Scale < 2) return [];

            using var cut = frame.CutScaled(left, top, width, height, treatment.Scale, out _, out var h);
            using var sheared = cut.Sheared(treatment.Shear);

            return (IReadOnlyList<ScreenTextLine>)[.. Read(sheared).Select(line => line with
            {
                Left = left + (line.Left + treatment.Shear * (h - line.Top)) / treatment.Scale,
                Top = top + line.Top / treatment.Scale,
                Height = line.Height / treatment.Scale,
            })];
        }, token);
    }

    public Task<ColourPatch?> PixelsAsync(string imagePath, ScreenPatch patch, CancellationToken token = default) =>
        Task.Run(() =>
        {
            using var frame = Frame.Load(imagePath);

            var left = (int)Math.Clamp(Math.Floor(patch.Left), 0, frame.Width);
            var top = (int)Math.Clamp(Math.Floor(patch.Top), 0, frame.Height);
            var width = (int)Math.Clamp(Math.Ceiling(patch.Width), 0, frame.Width - left);
            var height = (int)Math.Clamp(Math.Ceiling(patch.Height), 0, frame.Height - top);

            if (width < 1 || height < 1) return null;

            using var cut = frame.Cut(left, top, width, height);
            return new ColourPatch(left, top, width, height, cut.Pixels.ToArray());
        }, token);

    /// <summary>Detect, read each box, assemble lines. The whole pipeline on one frame.</summary>
    private List<ScreenTextLine> Read(Frame frame)
    {
        var words = new List<(TextBox Box, string Text, InkBounds Ink)>();

        foreach (var box in _detector!.Detect(frame))
        {
            var left = (int)Math.Floor(box.Left);
            var top = (int)Math.Floor(box.Top);
            var width = Math.Min(frame.Width - left, (int)Math.Ceiling(box.Width));
            var height = Math.Min(frame.Height - top, (int)Math.Ceiling(box.Height));

            if (width < 2 || height < 2) continue;

            using var crop = frame.Cut(left, top, width, height);
            var (text, confidence) = _recogniser!.Read(crop);

            // What the network was unsure of is usually a sliver of a panel
            // edge read as punctuation; a blank is a box with nothing in it.
            if (text.Trim().Length == 0 || confidence < 0.4) continue;

            words.Add((box, text.Trim(), InkBounds.Measure(crop, left, top)));
        }

        return Lines(words);
    }

    /// <summary>
    /// The detector's boxes grouped into lines, as the Windows engine hands
    /// lines back: boxes that overlap in height and sit within a line height
    /// of each other across are one line, read left to right.
    /// </summary>
    /// <remarks>
    /// The geometry reported is the ink's, not the detector's box: the box is
    /// the text kernel grown back out by a ratio, and its height runs a third
    /// over the glyphs. Every distance the readers measure is in line heights,
    /// so the height has to be the glyphs' own, as it is on Windows where it
    /// is the tallest word's bounding rectangle.
    /// </remarks>
    internal static List<ScreenTextLine> Lines(List<(TextBox Box, string Text, InkBounds Ink)> words)
    {
        var lines = new List<List<(TextBox Box, string Text, InkBounds Ink)>>();

        foreach (var word in words.OrderBy(w => w.Ink.Top).ThenBy(w => w.Ink.Left))
        {
            var home = lines.FirstOrDefault(line => line.Any(other => SameLine(other, word)));

            if (home is null)
                lines.Add([word]);
            else
                home.Add(word);
        }

        return [.. lines
            .Select(line => line.OrderBy(w => w.Ink.Left).ToList())
            .Select(line => new ScreenTextLine(
                string.Join(' ', line.Select(w => w.Text)),
                line.Min(w => w.Ink.Left),
                line.Min(w => w.Ink.Top),
                line.Max(w => w.Ink.Height)))
            .OrderBy(l => l.Top).ThenBy(l => l.Left)];

        static bool SameLine((TextBox Box, string Text, InkBounds Ink) a, (TextBox Box, string Text, InkBounds Ink) b)
        {
            var overlap = Math.Min(a.Ink.Bottom, b.Ink.Bottom) - Math.Max(a.Ink.Top, b.Ink.Top);
            var shorter = Math.Min(a.Ink.Height, b.Ink.Height);
            if (overlap < 0.5 * shorter) return false;

            var gap = Math.Max(a.Ink.Left, b.Ink.Left) - Math.Min(a.Ink.Right, b.Ink.Right);
            return gap <= 1.2 * Math.Max(a.Ink.Height, b.Ink.Height);
        }
    }

    private async Task LoadAsync(CancellationToken token)
    {
        if (_recogniser is not null) return;

        await _loading.WaitAsync(token);
        try
        {
            if (_recogniser is not null) return;

            await OnnxModels.EnsureAsync(Models, Progress, token);

            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                // Half the cores: a read runs while the game does, and the
                // game is what the cores are for.
                IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2),
            };

            _detectorSession = new InferenceSession(OnnxModels.Detector.Path0, options);
            _recogniserSession = new InferenceSession(_recogniserModel.Path0, options);
            _detector = new TextDetector(_detectorSession);
            _recogniser = new TextRecogniser(_recogniserSession, OnnxModels.Characters(_recogniserModel));
        }
        finally
        {
            _loading.Release();
        }
    }

    public void Dispose()
    {
        _detectorSession?.Dispose();
        _recogniserSession?.Dispose();
        _loading.Dispose();
    }
}

/// <summary>Where the glyphs actually sit inside a detector's crop, in frame pixels.</summary>
/// <remarks>
/// Rows and columns whose brightness varies more than a fifth of the crop's
/// most varied row are ink; the rest is the margin the unclip added. Works
/// for light text on a dark panel and the reverse alike, since it is the
/// variation that is measured and not the level. A crop with no such row -
/// a flat patch the detector should not have offered - reports its full
/// extent, which is also what the box would have said.
/// </remarks>
internal readonly record struct InkBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public static InkBounds Measure(Frame crop, int frameLeft, int frameTop)
    {
        var pixels = crop.Pixels;
        var width = crop.Width;
        var height = crop.Height;

        var rows = new double[height];
        var columns = new double[width];
        var grey = new float[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = (y * width + x) * 4;
                grey[y * width + x] = 0.114f * pixels[at] + 0.587f * pixels[at + 1] + 0.299f * pixels[at + 2];
            }
        }

        for (var y = 0; y < height; y++)
            rows[y] = Spread(grey.AsSpan(y * width, width));

        for (var x = 0; x < width; x++)
        {
            double min = 255, max = 0;
            for (var y = 0; y < height; y++)
            {
                var v = grey[y * width + x];
                if (v < min) min = v;
                if (v > max) max = v;
            }
            columns[x] = max - min;
        }

        var (top, bottom) = Extent(rows);
        var (left, right) = Extent(columns);

        return new InkBounds(frameLeft + left, frameTop + top, right - left + 1, bottom - top + 1);

        static double Spread(ReadOnlySpan<float> row)
        {
            float min = 255, max = 0;
            foreach (var v in row)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }
            return max - min;
        }

        static (int First, int Last) Extent(double[] spread)
        {
            var cut = spread.Max() * 0.2;
            if (cut <= 0) return (0, spread.Length - 1);

            var first = Array.FindIndex(spread, s => s > cut);
            var last = Array.FindLastIndex(spread, s => s > cut);
            return (first, last);
        }
    }
}
