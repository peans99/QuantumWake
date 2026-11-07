using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>A phrase the recogniser matched, with how sure it was.</summary>
/// <param name="Confidence">0 to 1, as the recogniser rates the match.</param>
public sealed record HeardPhrase(string Text, double Confidence, DateTimeOffset At);

/// <summary>
/// Listens to the microphone for a fixed list of phrases. An interface for the
/// same reason the screen capture is one: only a Windows desktop has the
/// recogniser, and the server runs without one.
/// </summary>
public interface ISpeechListener
{
    /// <summary>False when no recogniser is installed; the page then says so.</summary>
    bool Available { get; }

    /// <summary>Why it is not listening when asked to, or null when it is: no microphone, microphone access off.</summary>
    string? Trouble { get; }

    /// <summary>Starts listening for these phrases, or changes them; each match is handed to <paramref name="heard"/>.</summary>
    void Listen(IReadOnlyList<string> phrases, Action<HeardPhrase> heard);

    /// <summary>Stops listening and lets go of the microphone.</summary>
    void Stop();
}

/// <summary>
/// Presses keys in the game. An interface for the same reason as the
/// listener; the bare server has none and every command says so.
/// </summary>
public interface IGameKeyboard
{
    /// <summary>
    /// Presses the plan's keys while Star Citizen is the window in front, and
    /// returns once they are back up - which takes as long as the plan holds
    /// them. Never presses into any other window.
    /// </summary>
    KeyPress Press(KeyPlan plan);

    /// <summary>Whether a key, by the game's name for it, is held down now: push-to-talk on the keyboard.</summary>
    bool IsDown(string key);
}

/// <summary>What happened to one press.</summary>
public sealed record KeyPress(bool Sent, string? Why = null)
{
    public static KeyPress Not(string why) => new(false, why);
}

/// <summary>How the copilot is told to listen.</summary>
public static class VoiceTrigger
{
    /// <summary>Say "Quantum Wake copilot on" and it acts on commands until "copilot off".</summary>
    public const string Phrase = "phrase";

    /// <summary>It acts only on commands said while a button or key is held.</summary>
    public const string Button = "button";

    /// <summary>Either: awake by the phrase, or the button held.</summary>
    public const string Both = "both";

    public static bool Valid(string? trigger) => trigger is Phrase or Button or Both;
}

/// <summary>
/// The pilot's answer about voice commands.
/// </summary>
/// <param name="Enabled">Whether the microphone is listened to at all. Never on unless the pilot turned it on.</param>
/// <param name="Trigger">One of <see cref="VoiceTrigger"/>.</param>
/// <param name="ButtonDevice">The product GUID of the stick whose button is push-to-talk - <c>{0404044F-…}</c> - or null.</param>
/// <param name="Button">That button, numbered from 1 as the game numbers them, or null.</param>
/// <param name="Key">A keyboard key, by the game's name for it, as push-to-talk instead - <c>np_0</c> - or null.</param>
/// <param name="MinConfidence">
/// Below this a match is not acted on. A recogniser told only a dozen phrases
/// fits whatever it hears to the nearest one, so something said to a
/// wingman can come back as a command with a low score; this is what keeps it
/// out.
/// </param>
/// <param name="Off">The commands switched off, by id; a list of what is off so a command added later is on.</param>
/// <param name="Replies">Whether the copilot answers out loud.</param>
public sealed record VoiceCommandSettings(
    bool Enabled = false,
    string Trigger = VoiceTrigger.Phrase,
    string? ButtonDevice = null,
    int? Button = null,
    string? Key = null,
    double MinConfidence = 0.7,
    IReadOnlyList<string>? Off = null,
    bool Replies = true)
{
    public bool IsOff(string id) => Off?.Contains(id) == true;

    public bool UsesPhrase => Trigger is VoiceTrigger.Phrase or VoiceTrigger.Both;

    public bool UsesButton => Trigger is VoiceTrigger.Button or VoiceTrigger.Both;
}

/// <summary>Remembers the voice-command settings, in their own file.</summary>
/// <remarks>
/// Apart from <see cref="LiveScreenStore"/> on purpose: reading the screen and
/// listening to the microphone and pressing keys are different things to
/// agree to, and turning one on must never turn on another.
/// </remarks>
public sealed class VoiceCommandStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private VoiceCommandSettings _current = new();

    public VoiceCommandStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? Quantumwake.Core.AppPaths.Root, "voice-commands.json");
        Load();
    }

    public VoiceCommandSettings Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>Applies a change and saves it; the change is handed the current settings and returns the new ones.</summary>
    public VoiceCommandSettings Change(Func<VoiceCommandSettings, VoiceCommandSettings> change)
    {
        lock (_gate)
        {
            var next = change(_current);
            _current = Clean(next);
            Save();
            return _current;
        }
    }

    private static VoiceCommandSettings Clean(VoiceCommandSettings s) => s with
    {
        Trigger = VoiceTrigger.Valid(s.Trigger) ? s.Trigger : VoiceTrigger.Phrase,
        MinConfidence = Math.Clamp(double.IsFinite(s.MinConfidence) ? s.MinConfidence : 0.7, 0.3, 0.95),
        // A command this build does not list is dropped, so a typo cannot
        // sit in the file switching nothing off for ever.
        Off = s.Off?.Where(id => VoiceCommands.Find(id) is not null).Distinct().ToList() is { Count: > 0 } off ? off : null,
        Key = string.IsNullOrWhiteSpace(s.Key) ? null : s.Key.Trim().ToLowerInvariant(),
    };

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_current));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Unsaved means off again next start, which is the safe way round.
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path) && JsonSerializer.Deserialize<VoiceCommandSettings>(File.ReadAllText(_path)) is { } read)
                _current = Clean(read);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _current = new VoiceCommandSettings();
        }
    }
}

/// <summary>One line of what the copilot heard and did, for the page.</summary>
/// <param name="Outcome">What came of it, in a sentence: "Pressed LAlt + N.", "Asleep: say Quantum Wake copilot on first."</param>
/// <param name="Command">The command's id, or null for the wake words and anything not acted on as a command.</param>
public sealed record VoiceHeard(DateTimeOffset At, string Text, double Confidence, string Outcome, string? Command = null);

/// <summary>What the copilot decided to do with a phrase.</summary>
/// <param name="Say">What to answer out loud, or null.</param>
/// <param name="Press">The command to press now, or null.</param>
/// <param name="Outcome">The line for the page.</param>
public sealed record VoiceDecision(string? Say, VoiceCommand? Press, string Outcome);

/// <summary>
/// Decides what a heard phrase means: whether the copilot is awake, whether a
/// command is waiting on "confirm", whether the push-to-talk button is down.
/// Pure, so the rules can be tested without a microphone; the service does the
/// listening and the pressing.
/// </summary>
public sealed class VoiceCopilot
{
    /// <summary>How long after letting go of push-to-talk a phrase still counts: the recogniser finishes a beat after the words do.</summary>
    public static readonly TimeSpan ButtonGrace = TimeSpan.FromSeconds(1.5);

    private DateTimeOffset? _buttonUpAt;

    /// <summary>Awake by the wake phrase.</summary>
    public bool Awake { get; private set; }

    /// <summary>Whether push-to-talk is held now.</summary>
    public bool ButtonDown { get; private set; }

    /// <summary>The command waiting on "confirm", and until when.</summary>
    public (VoiceCommand Command, DateTimeOffset Until)? Pending { get; private set; }

    /// <summary>The state in a word, for the page: off, asleep, awake, listening (button), confirming.</summary>
    public string State(VoiceCommandSettings settings, DateTimeOffset now)
    {
        if (!settings.Enabled) return "off";
        if (Pending is { } p && p.Until > now) return "confirming";
        if (settings.UsesButton && ButtonDown) return "listening";
        if (settings.UsesPhrase && Awake) return "awake";
        return "asleep";
    }

    /// <summary>Tells the copilot where push-to-talk is now.</summary>
    public void Button(bool down, DateTimeOffset now)
    {
        if (ButtonDown && !down) _buttonUpAt = now;
        ButtonDown = down;
    }

    /// <summary>Forgets being awake and anything waiting, as when the switch goes off.</summary>
    public void Reset()
    {
        Awake = false;
        Pending = null;
        ButtonDown = false;
        _buttonUpAt = null;
    }

    public VoiceDecision Hear(HeardPhrase heard, VoiceCommandSettings settings)
    {
        var text = heard.Text.Trim().ToLowerInvariant();
        var now = heard.At;

        if (!settings.Enabled) return new(null, null, "Voice commands are off.");

        // The wake words are held to a higher bar than commands: a wrongly
        // heard command does one thing, a wrongly heard wake leaves every
        // command live.
        var bar = settings.MinConfidence;
        if (text is VoiceCommands.WakePhrase or VoiceCommands.SleepPhrase or VoiceCommands.SleepShort)
        {
            if (heard.Confidence < Math.Min(0.95, bar + 0.1)) return new(null, null, Unsure(heard));
            if (text == VoiceCommands.WakePhrase)
            {
                if (!settings.UsesPhrase) return new(null, null, "Push-to-talk only: the wake phrase is switched off.");
                Awake = true;
                return new("Copilot on", null, "Awake: listening for commands.");
            }
            Awake = false;
            Pending = null;
            return new("Copilot off", null, "Asleep: only the wake phrase is listened for.");
        }

        if (heard.Confidence < bar) return new(null, null, Unsure(heard));

        var listening = (settings.UsesPhrase && Awake)
            || (settings.UsesButton && (ButtonDown || (_buttonUpAt is { } up && now - up <= ButtonGrace)));

        if (Pending is { } pending)
        {
            Pending = null;
            if (pending.Until < now)
            {
                // Too late: fall through and treat the phrase on its own, so a
                // "confirm" a minute later presses nothing.
                if (text == VoiceCommands.ConfirmPhrase) return new(null, null, $"Too late to confirm {pending.Command.Phrase}; say it again.");
            }
            else if (text == VoiceCommands.ConfirmPhrase)
            {
                // Held to the same rule as the command it confirms. With
                // push-to-talk, a "confirm" said with the button up - to a
                // wingman, on comms - is not the pilot answering, and is the
                // one word that must never fire a self destruct by accident.
                if (!listening)
                    return new("Cancelled", null, $"{pending.Command.Phrase} cancelled: confirm was said without push-to-talk.");
                return new(null, pending.Command, $"Confirmed {pending.Command.Phrase}.");
            }
            else
            {
                // Anything else, cancel included, stands the command down: the
                // safe reading of a pilot who said something other than yes.
                if (text == VoiceCommands.CancelPhrase || !listening)
                    return new("Cancelled", null, $"{pending.Command.Phrase} cancelled.");
            }
        }

        if (text is VoiceCommands.ConfirmPhrase or VoiceCommands.CancelPhrase)
            return new(null, null, "Nothing was waiting on that.");

        if (VoiceCommands.ForPhrase(text) is not { } command) return new(null, null, "Not a command.");

        if (!listening)
            return new(null, null, settings.UsesPhrase && settings.UsesButton
                ? "Asleep: say the wake phrase or hold push-to-talk."
                : settings.UsesButton ? "Push-to-talk was not held." : "Asleep: say Quantum Wake copilot on first.");

        if (settings.IsOff(command.Id)) return new(null, null, $"{command.Phrase} is switched off.");

        if (command.Confirm)
        {
            Pending = (command, now + VoiceCommands.ConfirmWindow);
            return new($"Confirm {command.Reply.ToLowerInvariant()}?", null, $"Waiting for confirm on {command.Phrase}.");
        }

        return new(null, command, $"Heard {command.Phrase}.");
    }

    private static string Unsure(HeardPhrase heard) =>
        $"Not sure enough ({heard.Confidence:0.00}) to act on it.";
}
