using System.Security.Cryptography;
using Quantumwake.Core;

namespace Quantumwake.Ocr.Onnx;

/// <summary>One model file: where it comes from and what it must hash to.</summary>
/// <param name="FileName">Its name in the app's model folder.</param>
/// <param name="Url">Pinned to a revision, so the bytes cannot change under the hash.</param>
/// <param name="Sha256">Of the file as downloaded on 2026-10-30.</param>
/// <param name="Bytes">Its size, for a progress figure and a quick first check.</param>
/// <param name="Dictionary">The embedded character table a recogniser decodes with; null for a detector.</param>
public sealed record OnnxModel(string FileName, string Url, string Sha256, long Bytes, string? Dictionary = null)
{
    public string Path0 => Path.Combine(OnnxModels.Folder, FileName);
}

/// <summary>
/// The detector and recogniser the reader runs: PaddleOCR's PP-OCR models as
/// RapidOCR exports them to ONNX.
/// </summary>
/// <remarks>
/// <para>
/// Fetched on first use into the app's data folder rather than shipped in the
/// binary or checked in. Fifteen megabytes of model would sit in the
/// repository's history for ever, and in every Windows build that never loads
/// them; the community dataset and StarStrings already come the same way. Each
/// file is pinned to a revision of the publishing repository and checked
/// against its hash, so what runs is what was measured.
/// </para>
/// <para>
/// The English recogniser is the PP-OCRv3 one: no PP-OCRv4 English export is
/// published, and the game writes nothing but Latin text. The detector is
/// PP-OCRv4's, which takes any script. The multilingual PP-OCRv4 recogniser is
/// kept as an alternative for the comparison harness and nothing else.
/// </para>
/// </remarks>
public static class OnnxModels
{
    private const string Repository = "https://huggingface.co/SWHL/RapidOCR/resolve/1cfba2e90fc938db55889873735088de210cc173/";

    public static readonly OnnxModel Detector = new(
        "ch_PP-OCRv4_det_infer.onnx",
        Repository + "PP-OCRv4/ch_PP-OCRv4_det_infer.onnx",
        "d2a7720d45a54257208b1e13e36a8479894cb74155a5efe29462512d42f49da9",
        4_745_517);

    public static readonly OnnxModel EnglishRecogniser = new(
        "en_PP-OCRv3_rec_infer.onnx",
        Repository + "PP-OCRv3/en_PP-OCRv3_rec_infer.onnx",
        "ef7abd8bd3629ae57ea2c28b425c1bd258a871b93fd2fe7c433946ade9b5d9ea",
        8_967_018,
        Dictionary: "ocr/en_dict.txt");

    public static readonly OnnxModel MultilingualRecogniser = new(
        "ch_PP-OCRv4_rec_infer.onnx",
        Repository + "PP-OCRv4/ch_PP-OCRv4_rec_infer.onnx",
        "48fc40f24f6d2a207a2b1091d3437eb3cc3eb6b676dc3ef9c37384005483683b",
        10_857_958,
        Dictionary: "ocr/ppocr_keys_v1.txt");

    /// <summary>What a reader needs: the two models it loads by default.</summary>
    public static IReadOnlyList<OnnxModel> Default => [Detector, EnglishRecogniser];

    /// <summary>
    /// Where the files live. <c>QUANTUMWAKE_OCR_MODELS</c> moves it, for a
    /// test runner or a machine that keeps one copy for several data folders.
    /// </summary>
    public static string Folder =>
        Environment.GetEnvironmentVariable("QUANTUMWAKE_OCR_MODELS") is { Length: > 0 } moved
            ? moved
            : AppPaths.In("ocr");

    /// <summary>True when every file is on disk at its expected size.</summary>
    /// <remarks>
    /// Size, not hash: the hash is checked once, when the file arrives, and
    /// hashing fifteen megabytes at every startup buys nothing a size check
    /// does not, since a half-written file is the failure this guards.
    /// </remarks>
    public static bool Present(IEnumerable<OnnxModel>? models = null) =>
        (models ?? Default).All(m => File.Exists(m.Path0) && new FileInfo(m.Path0).Length == m.Bytes);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>
    /// Fetches whatever is missing. Each file is written beside its final name
    /// and moved into place only once its hash matches, so a failed download
    /// leaves nothing that <see cref="Present"/> would believe.
    /// </summary>
    public static async Task EnsureAsync(
        IEnumerable<OnnxModel>? models = null,
        IProgress<(OnnxModel Model, long Received)>? progress = null,
        CancellationToken token = default)
    {
        Directory.CreateDirectory(Folder);

        foreach (var model in models ?? Default)
        {
            if (File.Exists(model.Path0) && new FileInfo(model.Path0).Length == model.Bytes)
                continue;

            // Its own name per attempt. Two fetches of one file - a second
            // process, or the test suite beside the app - shared ".part" and
            // threw on it, or one hashed the other's truncation and deleted it.
            var partial = $"{model.Path0}.{Guid.NewGuid():N}.part";

            try
            {
                using (var stall = CancellationTokenSource.CreateLinkedTokenSource(token))
                using (var response = await Http.GetAsync(model.Url, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();

                    await using var body = await response.Content.ReadAsStreamAsync(token);
                    await using var file = File.Create(partial);

                    var buffer = new byte[1 << 16];
                    long received = 0;

                    while (true)
                    {
                        // HttpClient's timeout ends at the headers. A connection
                        // that goes quiet mid-body - wifi dropped, the PC slept -
                        // would otherwise hold every read behind this one until
                        // the app restarted.
                        stall.CancelAfter(StallLimit);

                        var read = await body.ReadAsync(buffer, stall.Token);
                        if (read == 0) break;

                        await file.WriteAsync(buffer.AsMemory(0, read), token);
                        received += read;
                        progress?.Report((model, received));
                    }
                }

                var hash = await HashAsync(partial, token);

                if (!hash.Equals(model.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"{model.FileName} downloaded with hash {hash}, expected {model.Sha256}; not kept.");
                }

                File.Move(partial, model.Path0, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(partial)) File.Delete(partial);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static readonly TimeSpan StallLimit = TimeSpan.FromSeconds(30);

    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
    }

    /// <summary>The character table a recogniser's classes stand for, in order.</summary>
    internal static string[] Characters(OnnxModel recogniser)
    {
        if (recogniser.Dictionary is null)
            throw new ArgumentException($"{recogniser.FileName} is not a recogniser", nameof(recogniser));

        using var stream = typeof(OnnxModels).Assembly.GetManifestResourceStream(recogniser.Dictionary)
            ?? throw new InvalidOperationException($"missing embedded dictionary {recogniser.Dictionary}");
        using var reader = new StreamReader(stream);

        var characters = new List<string>();
        while (reader.ReadLine() is { } line)
            characters.Add(line);

        // The last line of the file is blank-terminated, not blank: a trailing
        // newline is not a character.
        return [.. characters];
    }
}
