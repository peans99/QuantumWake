using System.Text.Json;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>
/// Remembers how the player wants their names marked.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from <see cref="TextOverlayStore"/> on purpose. That one records
/// an install so it can be undone exactly; this one is a preference, and it has
/// to outlive installing and removing rather than being forgotten with them.
/// </para>
/// <para>
/// Colour defaults to off. Every other mark here can be checked from this
/// machine - the file is on disk and can be read back - but whether the game
/// renders an emphasis tag inside an item name is only knowable by launching it
/// and looking. Defaulting something unverified to on would put it in front of
/// people who never asked for it.
/// </para>
/// </remarks>
public sealed class ItemLabelStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private TextOverlayOptions _current = new();

    public ItemLabelStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "item-labels.json");
        Load();
    }

    public TextOverlayOptions Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>Stores a choice, clamping the emphasis level to what exists.</summary>
    public TextOverlayOptions Save(TextOverlayOptions options)
    {
        var settled = options with { Level = Math.Clamp(options.Level, 1, 5) };

        lock (_gate)
        {
            _current = settled;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(settled));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A preference that fails to save is a preference that reverts
                // next start, which is a good deal better than refusing to apply.
            }
        }

        return settled;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            if (JsonSerializer.Deserialize<TextOverlayOptions>(File.ReadAllText(_path)) is { } stored)
                _current = stored with { Level = Math.Clamp(stored.Level, 1, 5) };
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // An unreadable preference falls back to the default rather than
            // stopping the app from starting.
        }
    }
}
