using System.Text.Json;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>
/// Copies what the game window is showing, while it is the window in front.
/// </summary>
/// <remarks>
/// <para>
/// An interface for the same reason <see cref="IClipboardReader"/> is one: only
/// a Windows desktop can do it, and the server is built to run without one.
/// The overlay supplies it; a server started on its own has none and says so.
/// </para>
/// <para>
/// What it may do is narrower than what it could. It copies pixels from the
/// screen over the game's window - the same pixels a screenshot key would - and
/// nothing else: it never opens the game's process, never reads its memory,
/// never hooks it and never sends it input. Those are what the licence and an
/// anti-cheat care about; see docs/screen-insight.md, "Reading the screen live".
/// </para>
/// </remarks>
public interface IGameScreenCapture
{
    /// <summary>
    /// Writes the game window's picture to <paramref name="path"/> when the game
    /// is the window in front, or says why it did not.
    /// </summary>
    GameCapture Capture(string path);
}

/// <summary>One attempt at copying the game window.</summary>
/// <param name="Taken">Whether a picture was written.</param>
/// <param name="Why">Why not, in a sentence the page can show; null when taken.</param>
/// <param name="Width">The picture's width in pixels.</param>
/// <param name="Height">Its height.</param>
public sealed record GameCapture(bool Taken, string? Why = null, int Width = 0, int Height = 0)
{
    public static GameCapture Not(string why) => new(false, why);
}

/// <summary>
/// The pilot's answer about reading the screen live.
/// </summary>
/// <param name="Asked">
/// Whether the question has been answered, either way. The dashboard asks at
/// start until it has: an install that finished its first-run setup before the
/// question existed is asked on its next start rather than finding it buried
/// in Settings, or worse, switched on.
/// </param>
/// <param name="Enabled">Whether live reading is on. Never on unless the pilot said yes.</param>
/// <param name="AnsweredAt">When it was last answered or changed.</param>
/// <param name="Voice">
/// Whether combat callouts are spoken. Its own switch, off until turned on:
/// reading the screen is one agreement and a voice in the pilot's headset is
/// another, and a yes to the first never turns on the second.
/// </param>
/// <param name="VoiceCombat">
/// Whether the voice says combat callouts. On by default under <paramref name="Voice"/>:
/// the two below it exist so a pilot can quiet one activity without losing the
/// other - the mining calls in a fight that started at a rock, say - and an
/// answer saved before they existed reads as both on, which is what it meant.
/// </param>
/// <param name="VoiceMining">Whether the voice says mining callouts: the charge window, the crack, the radar ping.</param>
/// <param name="VoiceMissions">Whether the voice says mission callouts: objectives, contracts, an escort's hull.</param>
/// <param name="Muted">
/// The callouts switched off one by one on Settings → Callouts, by kind (see
/// <see cref="Callouts"/>). A list of what is off rather than of what is on, so
/// a callout added in a later version is heard rather than silently missing
/// from an answer saved before it existed.
/// </param>
/// <param name="PingPlace">
/// The place pings are read against: null to take it from where the log puts
/// the pilot, <see cref="Anywhere"/> for every rock, or a place's name from
/// <see cref="PingPlaces"/>.
/// </param>
/// <param name="Wanted">The rocks the pilot is after, by the name the voice says - "Quantainium", "Hand-mining deposit".</param>
/// <param name="WantedOnly">Say only pings that could be a wanted rock.</param>
/// <param name="KeepFights">
/// Whether each lock and fight callout is written to the combat log for the
/// Sessions debrief (1.2.21). Off until turned on: the privacy page promised
/// from 1.1 that the targeted ship was held in memory and never written down,
/// and a yes to reading the screen was given on that promise.
/// </param>
public sealed record LiveScreenSettings(
    bool Asked = false, bool Enabled = false, DateTimeOffset? AnsweredAt = null, bool Voice = false,
    bool VoiceCombat = true, bool VoiceMining = true, bool VoiceMissions = true, IReadOnlyList<string>? Muted = null,
    string? PingPlace = null, IReadOnlyList<string>? Wanted = null, bool WantedOnly = false, bool KeepFights = false)
{
    /// <summary>The <see cref="PingPlace"/> that reads a ping against every rock.</summary>
    public const string Anywhere = "*";

    /// <summary>Whether a combat callout is said now.</summary>
    public bool SpeaksCombat => Voice && VoiceCombat;

    /// <summary>Whether a mining callout is said now.</summary>
    public bool SpeaksMining => Voice && VoiceMining;

    /// <summary>Whether a callout of this kind is said now: the voice, its group's switch, and its own.</summary>
    public bool Speaks(string kind) => Voice && GroupOn(Callouts.GroupOf(kind)) && !IsMuted(kind);

    /// <summary>Whether this one kind has been switched off on its own.</summary>
    public bool IsMuted(string kind) => Muted?.Contains(kind) == true;

    /// <summary>Whether a group's switch on the Log page is on.</summary>
    public bool GroupOn(string group) => group switch
    {
        Callouts.Mining => VoiceMining,
        Callouts.Missions => VoiceMissions,
        Callouts.Ship => true,
        _ => VoiceCombat,
    };
}

/// <summary>
/// Says a callout out loud. An interface for the same reason the capture is
/// one: only a Windows desktop has a voice, and the server runs without one.
/// </summary>
public interface ICalloutVoice
{
    /// <summary>Speaks <paramref name="text"/>, or drops it when the voice is still busy with something newer.</summary>
    void Say(string text);

    /// <summary>Which voice would say <paramref name="text"/>: the recorded pack's name, "windows" when the pack lacks a word, null when unknown.</summary>
    string? VoiceFor(string text) => null;

    /// <summary>
    /// Why the last callout was not heard, or null when the last one played:
    /// no voice installed, no audio device. Said on the Log page, because a
    /// voice that fails in silence looks exactly like a fight with nothing to say.
    /// </summary>
    string? Trouble => null;
}

/// <summary>
/// Remembers whether the pilot wants the screen read live, and that they were asked.
/// </summary>
/// <remarks>
/// Its own file, apart from <see cref="ScreenSettingsStore"/>: that one says
/// what may be done with screenshots the pilot saved, and this is a different
/// and bigger thing to agree to. Keeping them apart means turning one on can
/// never turn the other on with it - the rule every feed in this app follows.
/// </remarks>
public sealed class LiveScreenStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private LiveScreenSettings _current = new();

    public LiveScreenStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "live-screen.json");
        Load();
    }

    public LiveScreenSettings Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>Records an answer: asked, and on or off.</summary>
    public LiveScreenSettings Answer(bool enabled)
    {
        lock (_gate)
        {
            _current = _current with { Asked = true, Enabled = enabled, AnsweredAt = DateTimeOffset.UtcNow };
            Save();
            return _current;
        }
    }

    /// <summary>
    /// Turns the spoken callouts on or off, and either kind of them when it is
    /// named; the reading answer, and a kind left unnamed, stay as they were.
    /// </summary>
    public LiveScreenSettings SetVoice(bool on, bool? combat = null, bool? mining = null, bool? missions = null)
    {
        lock (_gate)
        {
            _current = _current with
            {
                Voice = on,
                VoiceCombat = combat ?? _current.VoiceCombat,
                VoiceMining = mining ?? _current.VoiceMining,
                VoiceMissions = missions ?? _current.VoiceMissions,
            };
            Save();
            return _current;
        }
    }

    /// <summary>
    /// How pings are read: the place ("auto" for the log's, <see cref="LiveScreenSettings.Anywhere"/>
    /// for none, or a name), the wanted rocks, and whether only those are said.
    /// Anything not sent stays as it was.
    /// </summary>
    public LiveScreenSettings SetPing(string? place = null, IReadOnlyList<string>? wanted = null, bool? wantedOnly = null)
    {
        lock (_gate)
        {
            _current = _current with
            {
                PingPlace = place is null ? _current.PingPlace
                    : place.Equals("auto", StringComparison.OrdinalIgnoreCase) || place.Length == 0 ? null : place,
                Wanted = wanted is null ? _current.Wanted
                    : wanted.Count == 0 ? null : [.. wanted.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct(StringComparer.Ordinal)],
                WantedOnly = wantedOnly ?? _current.WantedOnly,
            };
            Save();
            return _current;
        }
    }

    /// <summary>Turns the fight record on or off; everything else stays as it was.</summary>
    public LiveScreenSettings SetKeepFights(bool on)
    {
        lock (_gate)
        {
            _current = _current with { KeepFights = on };
            Save();
            return _current;
        }
    }

    /// <summary>
    /// Switches one callout on or off on its own; the voice and the group
    /// switches stay as they were. A kind this build does not list is refused
    /// (null), so a typo cannot sit in the file muting nothing for ever.
    /// </summary>
    public LiveScreenSettings? SetCallout(string kind, bool on)
    {
        if (Callouts.Find(kind) is null) return null;
        lock (_gate)
        {
            var muted = (_current.Muted ?? []).Where(k => k != kind).ToList();
            if (!on) muted.Add(kind);
            _current = _current with { Muted = muted.Count == 0 ? null : muted };
            Save();
            return _current;
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_current));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // An answer that fails to save is asked again next start, which
            // beats reading the screen on an answer nobody can find.
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            var read = JsonSerializer.Deserialize<LiveScreenSettings>(File.ReadAllText(_path));

            // Enabled without asked cannot be a real answer; it is read as no.
            _current = read is { Asked: true } ? read : new LiveScreenSettings();
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _current = new LiveScreenSettings();
        }
    }
}
