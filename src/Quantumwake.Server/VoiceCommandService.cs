using System.Threading.Channels;
using Quantumwake.Core.Controls;
using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// The copilot: listens for its phrases while voice commands are on, decides
/// with <see cref="VoiceCopilot"/>, and presses the game's key for a command.
/// </summary>
/// <remarks>
/// <para>
/// Off until the pilot turns it on, and then only as much as asked: the
/// microphone is opened when the switch goes on and let go when it goes off,
/// and a key is pressed only while Star Citizen is the window in front.
/// </para>
/// <para>
/// The key is the pilot's own binding, read from the game's
/// <c>actionmaps.xml</c>, or the game's default when they never changed it.
/// A command whose action has no keyboard key says so on the page and out
/// loud rather than pressing nothing in silence.
/// </para>
/// </remarks>
public sealed class VoiceCommandService(
    VoiceCommandStore store,
    LogLibrary library,
    IJoystickReader joysticks,
    ILogger<VoiceCommandService> logger,
    GameInstall? install = null,
    ISpeechListener? listener = null,
    IGameKeyboard? keyboard = null,
    ICalloutVoice? voice = null) : BackgroundService
{
    /// <summary>The loop's pace: fast enough that push-to-talk feels held the moment it is.</summary>
    private static readonly TimeSpan Every = TimeSpan.FromMilliseconds(50);

    /// <summary>How long a failed microphone waits before it is tried again.</summary>
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(5);

    /// <summary>How long "press the button now" waits.</summary>
    private static readonly TimeSpan LearnFor = TimeSpan.FromSeconds(8);

    private const int HeardKept = 40;

    private readonly VoiceCopilot _copilot = new();
    private readonly Lock _gate = new();
    private readonly Channel<HeardPhrase> _heard = Channel.CreateUnbounded<HeardPhrase>();
    private readonly List<VoiceHeard> _log = [];
    private bool _listening;
    private DateTimeOffset _triedAt = DateTimeOffset.MinValue;
    private DateTimeOffset? _learnUntil;
    private HashSet<(string Device, int Button)> _heldAtLearn = [];
    private (DateTime Written, ControlProfile? Profile)? _profile;

    /// <summary>Whether this copy can listen at all: a recogniser on the machine, and a way to press keys.</summary>
    public bool Available => listener?.Available == true && keyboard is not null;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Tick(DateTimeOffset.UtcNow);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // One bad tick - a stick unplugged mid-read - must not end the copilot.
                logger.LogWarning(e, "Voice command tick failed.");
            }
            try { await Task.Delay(Every, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
        listener?.Stop();
    }

    private void Tick(DateTimeOffset now)
    {
        var settings = store.Current;
        if (listener is null) return;

        if (settings.Enabled && Available)
        {
            if (!_listening || (listener.Trouble is not null && now - _triedAt > RetryEvery))
            {
                _triedAt = now;
                listener.Listen(VoiceCommands.Grammar(), heard => _heard.Writer.TryWrite(heard));
                _listening = true;
            }
        }
        else if (_listening)
        {
            listener.Stop();
            _listening = false;
            lock (_gate) _copilot.Reset();
        }

        Learn(now);

        if (settings.Enabled && settings.UsesButton)
        {
            var down = PushToTalkDown(settings);
            lock (_gate) _copilot.Button(down, now);
        }

        while (_heard.Reader.TryRead(out var heard))
            Handle(heard, settings);
    }

    private bool PushToTalkDown(VoiceCommandSettings settings)
    {
        if (settings.Key is { } key && keyboard?.IsDown(key) == true) return true;
        if (settings.ButtonDevice is not { } guid || settings.Button is not { } button || !joysticks.Available) return false;
        var device = joysticks.Devices().FirstOrDefault(d => string.Equals(d.Guid, guid, StringComparison.OrdinalIgnoreCase));
        if (device is null) return false;
        return joysticks.Read().FirstOrDefault(r => r.Id == device.Id)?.Buttons.Contains(button) == true;
    }

    /// <summary>Waits for a stick button pressed after the page asked, and keeps it as push-to-talk.</summary>
    private void Learn(DateTimeOffset now)
    {
        DateTimeOffset? until;
        lock (_gate) until = _learnUntil;
        if (until is null) return;
        if (now > until)
        {
            lock (_gate) _learnUntil = null;
            return;
        }
        if (!joysticks.Available) return;
        var devices = joysticks.Devices().ToDictionary(d => d.Id);
        foreach (var reading in joysticks.Read())
        {
            if (!devices.TryGetValue(reading.Id, out var device)) continue;
            foreach (var button in reading.Buttons)
            {
                // A button already down when the page asked is a stick
                // resting on a switch, not the pilot's answer.
                if (_heldAtLearn.Contains((reading.Id, button))) continue;
                store.Change(s => s with { ButtonDevice = device.Guid, Button = button, Key = null });
                lock (_gate) _learnUntil = null;
                return;
            }
        }
    }

    /// <summary>Starts waiting for a stick button to use as push-to-talk.</summary>
    public void StartLearning()
    {
        var held = joysticks.Available
            ? joysticks.Read().SelectMany(r => r.Buttons.Select(b => (r.Id, b))).ToHashSet()
            : [];
        lock (_gate)
        {
            _heldAtLearn = held;
            _learnUntil = DateTimeOffset.UtcNow + LearnFor;
        }
    }

    private void Handle(HeardPhrase heard, VoiceCommandSettings settings)
    {
        VoiceDecision decision;
        lock (_gate) decision = _copilot.Hear(heard, settings);
        if (decision.Say is { } say) Say(settings, say);
        if (decision.Press is not { } command)
        {
            Record(heard, decision.Outcome, null);
            return;
        }

        var plan = PlanFor(command);
        if (!plan.CanPress)
        {
            Say(settings, VoiceCommands.SayCannotPress);
            Record(heard, $"{command.Phrase}: {plan.Why}", command.Id);
            return;
        }
        if (keyboard is null)
        {
            Record(heard, "This copy of the app cannot press keys.", command.Id);
            return;
        }

        // Off the loop: a scan holds its key for seconds, and push-to-talk
        // and the next phrase must not wait on it.
        _ = Task.Run(() =>
        {
            var pressed = keyboard.Press(plan);
            if (pressed.Sent)
            {
                Say(settings, command.Reply);
                Record(heard, $"Pressed {plan.Shown}{(plan.Taps > 1 ? " twice" : "")}.", command.Id);
            }
            else
            {
                Say(settings, VoiceCommands.SayNotInFront);
                Record(heard, $"Not pressed: {pressed.Why}.", command.Id);
            }
        });
    }

    private void Say(VoiceCommandSettings settings, string text)
    {
        if (settings.Replies) voice?.Say(text);
    }

    private void Record(HeardPhrase heard, string outcome, string? command)
    {
        lock (_gate)
        {
            _log.Insert(0, new VoiceHeard(heard.At, heard.Text, heard.Confidence, outcome, command));
            if (_log.Count > HeardKept) _log.RemoveRange(HeardKept, _log.Count - HeardKept);
        }
    }

    /// <summary>The plan for a command against the profile as it is on disk now.</summary>
    public KeyPlan PlanFor(VoiceCommand command) =>
        VoiceBinding.Plan(command, library.GameCommodities.Controls.Catalogue, Profile());

    /// <summary>Keys proposed for the commands that have none; nothing is written until the page sends them to /api/controls/bindings.</summary>
    public IReadOnlyList<SpareKey> SpareKeys() =>
        VoiceBinding.SpareKeys(VoiceCommands.All, library.GameCommodities.Controls.Catalogue, Profile());

    /// <summary>The live keybinding profile, read again only when the game has rewritten it.</summary>
    private ControlProfile? Profile()
    {
        if (install is null) return null;
        var path = ControlsWatchService.ProfilePath(install);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return null;
            lock (_gate)
            {
                if (_profile is { } kept && kept.Written == info.LastWriteTimeUtc) return kept.Profile;
            }
            var profile = ControlProfile.Parse(File.ReadAllBytes(path));
            lock (_gate) _profile = (info.LastWriteTimeUtc, profile);
            return profile;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Xml.XmlException)
        {
            // Mid-write, or unreadable: the game's defaults are the best guess.
            return null;
        }
    }

    /// <summary>
    /// What a command answers out loud - its prompt when it waits on
    /// "confirm", else its reply - said now, for the page's "Hear it".
    /// False when there is no such command or no voice.
    /// </summary>
    public bool Sample(string id)
    {
        if (voice is null) return false;
        var text = id == "wake" ? VoiceCommands.SayAwake
            : VoiceCommands.Find(id) is { } c ? SpokenFor(c) : null;
        if (text is null) return false;
        voice.Say(text);
        return true;
    }

    private static string SpokenFor(VoiceCommand c) => c.Confirm ? c.Prompt ?? $"Confirm {c.Phrase}?" : c.Reply;

    /// <summary>Everything the page shows.</summary>
    public object Describe()
    {
        var settings = store.Current;
        var now = DateTimeOffset.UtcNow;
        string state;
        bool learning;
        VoiceHeard[] heard;
        lock (_gate)
        {
            state = _copilot.State(settings, now);
            learning = _learnUntil is { } until && until > now;
            heard = [.. _log];
        }
        var spare = SpareKeys();
        var stick = settings.ButtonDevice is { } guid && joysticks.Available
            ? joysticks.Devices().FirstOrDefault(d => string.Equals(d.Guid, guid, StringComparison.OrdinalIgnoreCase))
            : null;
        return new
        {
            available = Available,
            recogniser = listener?.Available == true,
            canPress = keyboard is not null,
            canSpeak = voice is not null,
            trouble = settings.Enabled ? listener?.Trouble : null,
            state,
            learning,
            settings.Enabled,
            settings.Trigger,
            settings.Button,
            settings.Key,
            keyShown = settings.Key is { } k ? GameKeys.Label(k) : null,
            buttonDevice = settings.ButtonDevice,
            buttonDeviceName = stick?.Name,
            buttonDevicePresent = stick is not null,
            settings.MinConfidence,
            settings.Replies,
            wake = VoiceCommands.WakePhrase,
            sleep = VoiceCommands.SleepPhrase,
            commands = VoiceCommands.All.Select(c =>
            {
                var plan = PlanFor(c);
                var action = library.GameCommodities.Controls.Catalogue.Find(c.ActionMap, c.Action);
                return new
                {
                    c.Id,
                    c.Phrase,
                    also = c.Examples(),
                    c.Confirm,
                    on = !settings.IsOff(c.Id),
                    action = action?.Label ?? c.Action,
                    key = plan.Shown,
                    canPress = plan.CanPress,
                    why = plan.Why,
                    fromProfile = plan.FromProfile,
                    says = SpokenFor(c),
                    voice = voice?.VoiceFor(SpokenFor(c)),
                };
            }),
            heard = heard.Select(h => new { at = h.At, h.Text, confidence = Math.Round(h.Confidence, 2), h.Outcome, h.Command }),
            spare = spare.Select(k => new { k.Command.Id, k.Command.Phrase, k.Command.ActionMap, k.Command.Action, k.Input, k.Shown }),
            gameRunning = spare.Count > 0 && GameProcess.IsRunning(),
        };
    }
}

/// <summary>A change from the page; anything left out stays as it was.</summary>
public sealed record VoiceSettingsBody(
    bool? Enabled = null,
    string? Trigger = null,
    string? Key = null,
    bool? ClearButton = null,
    double? MinConfidence = null,
    bool? Replies = null,
    string? Command = null,
    bool? On = null);

public static class VoiceCommandEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/voice", (VoiceCommandService service) => Results.Ok(service.Describe()));

        app.MapPost("/api/voice", (VoiceSettingsBody body, VoiceCommandStore store, VoiceCommandService service) =>
        {
            if (body.Trigger is { } t && !VoiceTrigger.Valid(t))
                return Results.Problem($"no trigger called '{t}'", statusCode: 400);
            if (body.Command is { } id && VoiceCommands.Find(id) is null)
                return Results.Problem($"no command called '{id}'", statusCode: 400);
            if (body.Key is { Length: > 0 } key && !GameKeys.CanPress(key))
                return Results.Problem($"no key called '{key}'", statusCode: 400);
            store.Change(s =>
            {
                var off = (s.Off ?? []).ToList();
                if (body.Command is { } c && body.On is { } on)
                {
                    off.Remove(c);
                    if (!on) off.Add(c);
                }
                return s with
                {
                    // A yes on a copy that cannot listen or press is kept as a no,
                    // so nothing comes on later, on another machine, unasked.
                    Enabled = body.Enabled is { } e ? e && service.Available : s.Enabled,
                    Trigger = body.Trigger ?? s.Trigger,
                    Key = body.Key is null ? s.Key : body.Key.Length == 0 ? null : body.Key,
                    ButtonDevice = body.ClearButton == true ? null : body.Key is { Length: > 0 } ? null : s.ButtonDevice,
                    Button = body.ClearButton == true ? null : body.Key is { Length: > 0 } ? null : s.Button,
                    MinConfidence = body.MinConfidence ?? s.MinConfidence,
                    Replies = body.Replies ?? s.Replies,
                    Off = off,
                };
            });
            return Results.Ok(service.Describe());
        });

        // "Press the button now": the next stick button pressed in the next
        // few seconds becomes push-to-talk.
        // "Hear it": the command's answer, in the voice the pilot will hear.
        app.MapPost("/api/voice/hear", (string id, VoiceCommandService service) =>
            service.Sample(id) ? Results.Ok() : Results.Problem($"nothing to say for '{id}'", statusCode: 400));

        app.MapPost("/api/voice/learn", (VoiceCommandService service) =>
        {
            service.StartLearning();
            return Results.Ok(service.Describe());
        });
    }
}
