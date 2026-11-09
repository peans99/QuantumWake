using System.Globalization;
using System.Speech.Recognition;
using Quantumwake.Data;

namespace Quantumwake.Ocr;

/// <summary>
/// Listens for the copilot's phrases with the speech recogniser Windows
/// carries on the machine (SAPI, "Microsoft Speech Recognizer 8.0").
/// </summary>
/// <remarks>
/// <para>
/// The desktop recogniser rather than Windows.Media.SpeechRecognition: it
/// runs on the machine with no network and no "online speech recognition"
/// switch, it is what VoiceAttack listens with, and a grammar of a few dozen
/// fixed phrases is the job it is best at. Found on this PC as MS-1033-80-DESK
/// (en-US) on 2026-11-07. A Windows install with no English speech pack has
/// none, and <see cref="Available"/> says so.
/// </para>
/// <para>
/// Audio never leaves the recogniser: nothing is recorded or kept, and the app
/// sees only the phrase matched and its score.
/// </para>
/// </remarks>
public sealed class WindowsSpeechListener : ISpeechListener, IDisposable
{
    private readonly Lock _gate = new();
    private SpeechRecognitionEngine? _engine;
    private Action<HeardPhrase>? _heard;
    private IReadOnlyList<string> _phrases = [];
    private string? _trouble;

    public bool Available { get; } = Recognizer() is not null;

    public string? Trouble
    {
        get { lock (_gate) return _trouble; }
    }

    public void Listen(IReadOnlyList<string> phrases, Action<HeardPhrase> heard)
    {
        lock (_gate)
        {
            _heard = heard;
            if (_engine is not null && phrases.SequenceEqual(_phrases)) return;
            StopLocked();
            if (Recognizer() is not { } info)
            {
                _trouble = "Windows has no English speech recogniser installed";
                return;
            }
            try
            {
                var engine = new SpeechRecognitionEngine(info);
                // Each phrase is a pattern, handed over as its structure rather
                // than every sentence it allows (1.2.5): the shields alone run to
                // thousands of wordings.
                var choices = new Choices([.. phrases.Select(p => Build(PhrasePattern.Parse(p).Parts, info.Culture))]);
                engine.LoadGrammar(new Grammar(new GrammarBuilder(choices) { Culture = info.Culture }) { Name = "copilot" });
                engine.SpeechRecognized += OnRecognized;
                engine.SetInputToDefaultAudioDevice();
                engine.RecognizeAsync(RecognizeMode.Multiple);
                _engine = engine;
                _phrases = [.. phrases];
                _trouble = null;
            }
            catch (InvalidOperationException)
            {
                // SetInputToDefaultAudioDevice throws this with no recording
                // device, and also when Windows' microphone privacy switch
                // keeps desktop apps away from it.
                _trouble = "no microphone could be opened - check one is plugged in, and that Windows Settings → Privacy → Microphone lets desktop apps use it";
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            {
                _trouble = "Windows would not start the speech recogniser";
            }
        }
    }

    public void Stop()
    {
        lock (_gate) StopLocked();
    }

    private void StopLocked()
    {
        if (_engine is not { } engine) return;
        _engine = null;
        _phrases = [];
        engine.SpeechRecognized -= OnRecognized;
        try { engine.RecognizeAsyncCancel(); } catch (InvalidOperationException) { /* already stopped */ }
        engine.Dispose();
    }

    private void OnRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        Action<HeardPhrase>? heard;
        lock (_gate) heard = _heard;
        heard?.Invoke(new HeardPhrase(e.Result.Text, e.Result.Confidence, DateTimeOffset.UtcNow));
    }

    /// <summary>The English recogniser, US first, or null when the machine has none.</summary>
    /// <summary>
    /// A pattern as the engine's own grammar. Every builder carries the
    /// recogniser's culture: one left on the default fails at load, not here.
    /// </summary>
    private static GrammarBuilder Build(IReadOnlyList<PhrasePattern.Part> run, CultureInfo culture)
    {
        var builder = new GrammarBuilder { Culture = culture };
        foreach (var part in run)
        {
            switch (part)
            {
                case PhrasePattern.Word word:
                    builder.Append(word.Text);
                    break;
                case PhrasePattern.Group group:
                    var options = new GrammarBuilder(new Choices([.. group.Choices.Select(c => Build(c, culture))])) { Culture = culture };
                    if (group.Optional) builder.Append(options, 0, 1);
                    else builder.Append(options);
                    break;
            }
        }
        return builder;
    }

    private static RecognizerInfo? Recognizer()
    {
        try
        {
            var installed = SpeechRecognitionEngine.InstalledRecognizers();
            return installed.FirstOrDefault(r => r.Culture.Name == "en-US")
                ?? installed.FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName == "en");
        }
        catch (Exception e) when (e is PlatformNotSupportedException or System.Runtime.InteropServices.COMException or CultureNotFoundException)
        {
            return null;
        }
    }

    public void Dispose() => Stop();
}
