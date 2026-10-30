using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Quantumwake.Ocr.Onnx;

/// <summary>Where the detector thinks a run of text is, in the frame's own pixels.</summary>
/// <param name="Score">The mean probability over the pixels that made it.</param>
internal readonly record struct TextBox(double Left, double Top, double Width, double Height, double Score)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

/// <summary>
/// Finds text with PP-OCR's DBNet detector, and turns its probability map
/// into boxes.
/// </summary>
/// <remarks>
/// <para>
/// The network answers one probability per pixel that it lies inside a text
/// kernel: the text region shrunk inwards, which is how DBNet keeps adjacent
/// lines apart. Boxes are read off it the way the reference code does, less
/// the geometry: pixels above the threshold are grouped into connected
/// components, each component scored by its mean probability, and the box
/// grown back out by <c>area × ratio / perimeter</c>, the "unclip" that undoes
/// the shrink. Axis-aligned rather than a minimum-area rectangle, because
/// everything the game writes is horizontal and a rotated box would only
/// have to be straightened again before the crop.
/// </para>
/// <para>
/// The frame goes in at its own size, up to a cap, with each side rounded to
/// the multiple of 32 the network wants. The reference limit is 960 on the
/// long side; a 3440 wide frame shrunk to that puts the 12-pixel item names
/// at four pixels, below anything the detector sees.
/// </para>
/// </remarks>
internal sealed class TextDetector(InferenceSession session)
{
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Std = [0.229f, 0.224f, 0.225f];

    /// <summary>A pixel counts as text above this. The reference default.</summary>
    public float Threshold { get; init; } = 0.3f;

    /// <summary>A component is kept above this mean probability.</summary>
    public float BoxThreshold { get; init; } = 0.5f;

    /// <summary>How far to grow a kernel back into a box. The reference default is 1.5.</summary>
    public float Unclip { get; init; } = 1.6f;

    /// <summary>The long side the frame is shrunk to when larger.</summary>
    public int MaxSide { get; init; } = 4096;

    private readonly string _input = session.InputMetadata.Keys.First();

    public List<TextBox> Detect(Frame frame)
    {
        var ratio = Math.Min(1.0, (double)MaxSide / Math.Max(frame.Width, frame.Height));
        var width = Math.Max(32, (int)Math.Round(frame.Width * ratio / 32) * 32);
        var height = Math.Max(32, (int)Math.Round(frame.Height * ratio / 32) * 32);

        float[] map;
        using (var sized = width == frame.Width && height == frame.Height ? null : frame.Resized(width, height))
        {
            var tensor = new DenseTensor<float>((sized ?? frame).ToTensor(Mean, Std), [1, 3, height, width]);

            using var results = session.Run([NamedOnnxValue.CreateFromTensor(_input, tensor)]);
            map = results.First().AsTensor<float>().ToArray();
        }

        var boxes = Components(map, width, height);

        // Back into the frame's pixels. The two axes were rounded separately.
        var sx = (double)frame.Width / width;
        var sy = (double)frame.Height / height;

        return [.. boxes.Select(b => new TextBox(b.Left * sx, b.Top * sy, b.Width * sx, b.Height * sy, b.Score))];
    }

    /// <summary>Connected components of the thresholded map, each grown back out.</summary>
    private List<TextBox> Components(float[] map, int width, int height)
    {
        var seen = new bool[map.Length];
        var stack = new Stack<int>();
        var boxes = new List<TextBox>();

        for (var start = 0; start < map.Length; start++)
        {
            if (seen[start] || map[start] <= Threshold)
                continue;

            int left = width, top = height, right = -1, bottom = -1, count = 0;
            double sum = 0;

            stack.Push(start);
            seen[start] = true;

            while (stack.Count > 0)
            {
                var at = stack.Pop();
                var x = at % width;
                var y = at / width;

                count++;
                sum += map[at];
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;

                // Four-connected. Eight would join lines that touch at a
                // corner, which on a tight HUD they do.
                if (x > 0) Visit(at - 1);
                if (x + 1 < width) Visit(at + 1);
                if (y > 0) Visit(at - width);
                if (y + 1 < height) Visit(at + width);
            }

            var w = right - left + 1;
            var h = bottom - top + 1;
            var score = sum / count;

            // Three pixels either way is the reference minimum; below it the
            // crop has nothing a recogniser can use.
            if (score < BoxThreshold || Math.Min(w, h) < 3)
                continue;

            var grow = (double)w * h * Unclip / (2.0 * (w + h));

            boxes.Add(new TextBox(
                Math.Max(0, left - grow),
                Math.Max(0, top - grow),
                Math.Min(width, right + 1 + grow) - Math.Max(0, left - grow),
                Math.Min(height, bottom + 1 + grow) - Math.Max(0, top - grow),
                score));

            void Visit(int next)
            {
                if (!seen[next] && map[next] > Threshold)
                {
                    seen[next] = true;
                    stack.Push(next);
                }
            }
        }

        return boxes;
    }
}
