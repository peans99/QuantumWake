using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Quantumwake.Ocr.Onnx;

/// <summary>
/// Reads one crop of text with a PP-OCR recogniser and decodes its answer.
/// </summary>
/// <remarks>
/// <para>
/// The crop is drawn 48 pixels tall at its own aspect, normalised to
/// <c>[-1, 1]</c> in B, G, R order (see <see cref="Frame.ToTensor"/>), and the
/// network answers a probability over the character table for each of a
/// sequence of time steps. CTC decoding turns that into text: the best class
/// at each step, repeats collapsed, blanks dropped.
/// </para>
/// <para>
/// Class 0 is the blank, the table's characters follow in order, and the last
/// class is the space the reference adds after the table. Checked against the
/// network's own output width when the session opens, because a table that
/// is one entry out decodes every character as its neighbour and nothing
/// reports it.
/// </para>
/// </remarks>
internal sealed class TextRecogniser
{
    private const int TargetHeight = 48;

    private static readonly float[] Mean = [0.5f, 0.5f, 0.5f];
    private static readonly float[] Std = [0.5f, 0.5f, 0.5f];

    private readonly InferenceSession _session;
    private readonly string _input;
    private readonly string[] _characters;

    public TextRecogniser(InferenceSession session, string[] table)
    {
        _session = session;
        _input = session.InputMetadata.Keys.First();

        var classes = session.OutputMetadata.Values.First().Dimensions[^1];

        _characters = classes == table.Length + 2 ? [.. table, " "]
            : classes == table.Length + 1 ? table
            : throw new InvalidOperationException(
                $"the recogniser answers {classes} classes and the character table has {table.Length} entries; they are not a pair");
    }

    /// <summary>The text in a crop and how sure the network was of it, per character on average.</summary>
    public (string Text, double Confidence) Read(Frame crop)
    {
        if (crop.Width < 2 || crop.Height < 2) return ("", 0);

        // Narrow crops are padded up to a minimum: the network's time steps
        // are a quarter of the width, and a 20-pixel crop has five of them,
        // which is not enough to say one character and a blank beside it.
        var width = Math.Max(32, (int)Math.Ceiling(TargetHeight * (double)crop.Width / crop.Height));

        float[] output;
        int steps;
        using (var sized = crop.Resized(width, TargetHeight))
        {
            var tensor = new DenseTensor<float>(sized.ToTensor(Mean, Std), [1, 3, TargetHeight, width]);

            using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_input, tensor)]);
            var answer = results.First().AsTensor<float>();
            steps = answer.Dimensions[1];
            output = answer.ToArray();
        }

        var classes = _characters.Length + 1;
        var text = new System.Text.StringBuilder();
        double confidence = 0;
        var kept = 0;
        var previous = 0;

        for (var t = 0; t < steps; t++)
        {
            var row = t * classes;
            var best = 0;
            var bestValue = output[row];

            for (var c = 1; c < classes; c++)
            {
                if (output[row + c] > bestValue)
                {
                    bestValue = output[row + c];
                    best = c;
                }
            }

            if (best != 0 && best != previous)
            {
                text.Append(_characters[best - 1]);
                confidence += bestValue;
                kept++;
            }

            previous = best;
        }

        return (text.ToString(), kept == 0 ? 0 : confidence / kept);
    }
}
