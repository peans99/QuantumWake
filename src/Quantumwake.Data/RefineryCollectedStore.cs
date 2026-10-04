using System.Text.Json;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>
/// Which refinery orders the pilot says they have picked up, and when.
/// </summary>
/// <remarks>
/// <para>
/// The game logs an order finishing and nothing after it: collecting writes no
/// line, so an order the log called complete stays "ready" for ever unless the
/// pilot says otherwise. Without this the list of what is waiting grows by
/// every order ever placed, and the one still sitting at a station is the one
/// that is hard to find.
/// </para>
/// <para>
/// A mark by order id, nothing more - the order itself is worked out again
/// from the screenshots and the log every time. Not in the backup: losing it
/// puts old orders back under "waiting", one click each to clear, which is a
/// smaller cost than another kind of record through export and restore.
/// </para>
/// </remarks>
public sealed class RefineryCollectedStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private Dictionary<string, DateTimeOffset> _collected = new(StringComparer.Ordinal);

    public RefineryCollectedStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "refinery-collected.json");
        Load();
    }

    public IReadOnlyDictionary<string, DateTimeOffset> All()
    {
        lock (_gate) return new Dictionary<string, DateTimeOffset>(_collected, StringComparer.Ordinal);
    }

    /// <summary>Marks an order collected at a moment, or clears the mark.</summary>
    public void Set(string id, DateTimeOffset? at)
    {
        lock (_gate)
        {
            if (at is { } when) _collected[id] = when;
            else _collected.Remove(id);
            Save();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_collected));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The mark is kept in memory and the next save writes it; at worst
            // an order shows as waiting again after a restart.
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            _collected = JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(File.ReadAllText(_path))
                is { } read ? new Dictionary<string, DateTimeOffset>(read, StringComparer.Ordinal) : new(StringComparer.Ordinal);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _collected = new(StringComparer.Ordinal);
        }
    }
}
