using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// A recorded voice for the combat callouts: every phrase the combat watch
/// says and every hull name, one clip each, joined when a callout fires.
/// </summary>
/// <remarks>
/// <para>
/// Recorded rather than synthesised on the spot because the neural voice the
/// pilot chose (Kokoro's af_bella) took about three seconds a sentence on his
/// CPU on 2026-11-06 - a fight does not wait that long - and the voices
/// Windows ships are the robotic ones he asked to replace. The recorder is
/// <c>tools/voice-pack</c>; run it again when a patch adds hulls.
/// </para>
/// <para>
/// A sentence is said from the pack only when every word of it is covered;
/// otherwise the caller falls back to Windows' voice for the whole sentence,
/// so a hull added since the pack was made is still named, just less well,
/// rather than skipped.
/// </para>
/// </remarks>
public sealed class VoicePack
{
    private readonly Dictionary<string, string> _clips;
    private readonly int _longest;

    private VoicePack(string voice, Dictionary<string, string> clips)
    {
        Voice = voice;
        _clips = clips;
        _longest = clips.Keys.Max(k => k.Split(' ').Length);
    }

    /// <summary>The recorded voice's name, af_bella.</summary>
    public string Voice { get; }

    /// <summary>Reads the pack's <c>manifest.json</c>; null when there is none or it is unreadable.</summary>
    public static VoicePack? Load(Stream? manifest)
    {
        if (manifest is null) return null;

        try
        {
            using var doc = JsonDocument.Parse(manifest);
            var root = doc.RootElement;
            var clips = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var section in new[] { "phrases", "names" })
                if (root.TryGetProperty(section, out var entries))
                    foreach (var entry in entries.EnumerateObject())
                        if (entry.Value.GetString() is { Length: > 0 } file)
                            clips[Key(entry.Name)] = file;

            return clips.Count == 0 ? null
                : new VoicePack(root.TryGetProperty("voice", out var v) ? v.GetString() ?? "pack" : "pack", clips);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The clips that say <paramref name="sentence"/>, in order, or null when
    /// some part of it was never recorded. Longest match first, so "Hostile
    /// contact, likely" is one clip rather than three.
    /// </summary>
    public IReadOnlyList<string>? Clips(string sentence)
    {
        var words = Key(sentence).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var clips = new List<string>();

        for (var i = 0; i < words.Length;)
        {
            string? found = null;
            var take = 0;
            for (var n = Math.Min(_longest, words.Length - i); n > 0 && found is null; n--)
            {
                if (_clips.TryGetValue(string.Join(' ', words, i, n), out var clip))
                {
                    found = clip;
                    take = n;
                }
            }

            if (found is null) return null;
            clips.Add(found);
            i += take;
        }

        return clips.Count == 0 ? null : clips;
    }

    /// <summary>Lower case, punctuation gone, single spaces: "Hostile contact, likely" and "hostile contact likely" are one key.</summary>
    private static string Key(string text) =>
        string.Join(' ', new string([.. text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
