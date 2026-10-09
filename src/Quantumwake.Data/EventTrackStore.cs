using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// The event the pilot chose to keep on the Now page, or none.
/// </summary>
/// <remarks>
/// <para>
/// Without a choice the Now card follows play: the event a contract in the
/// journal pays into, else one played in the last fortnight. That cannot know
/// about an event the pilot means to play and has not started, or one they
/// want in view between sessions - so a choice, when there is one, wins.
/// </para>
/// <para>
/// Server-side rather than in browser storage for the reason the overlay
/// layout is: the overlay runs in its own WebView2 profile, and a choice made
/// in the browser would never reach the card it was made for.
/// </para>
/// </remarks>
public sealed class EventTrackStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private string? _tracked;

    public EventTrackStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "event-track.json");
        Load();
    }

    /// <summary>The tracked event's id - <c>Iasi_ScenarioProgress</c> - or null.</summary>
    public string? Tracked
    {
        get { lock (_gate) return _tracked; }
    }

    /// <summary>Tracks an event, or clears the choice when given null or blank.</summary>
    public string? Save(string? id)
    {
        var settled = string.IsNullOrWhiteSpace(id) ? null : id.Trim();

        lock (_gate)
        {
            _tracked = settled;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(new Saved(settled)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A choice that fails to save reverts on the next start, which
                // beats refusing to make it.
            }

            return settled;
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            _tracked = JsonSerializer.Deserialize<Saved>(File.ReadAllText(_path))?.Tracked;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // An unreadable file is no choice, and the card follows play.
        }
    }

    private sealed record Saved(string? Tracked);
}
