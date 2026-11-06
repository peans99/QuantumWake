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
public sealed record LiveScreenSettings(bool Asked = false, bool Enabled = false, DateTimeOffset? AnsweredAt = null, bool Voice = false);

/// <summary>
/// Says a callout out loud. An interface for the same reason the capture is
/// one: only a Windows desktop has a voice, and the server runs without one.
/// </summary>
public interface ICalloutVoice
{
    /// <summary>Speaks <paramref name="text"/>, or drops it when the voice is still busy with something newer.</summary>
    void Say(string text);
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

    /// <summary>Turns the spoken callouts on or off; the reading answer is left as it was.</summary>
    public LiveScreenSettings SetVoice(bool on)
    {
        lock (_gate)
        {
            _current = _current with { Voice = on };
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
