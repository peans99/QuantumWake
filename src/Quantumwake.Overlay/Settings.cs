using System.IO;
using System.Text.Json;

namespace Quantumwake.Overlay;

/// <summary>
/// Preferences that outlive a run, stored beside the session cache.
/// </summary>
/// <remarks>
/// Separate from <see cref="OverlayGeometry"/> on purpose: geometry is written
/// on every move and resize, and a preference the user chose deliberately
/// should not share a file with something rewritten dozens of times a session.
/// Nothing is written to the game directory — the read-only stance covers the
/// application's own settings too.
/// </remarks>
internal sealed record Settings
{
    /// <summary>
    /// Whether the transparent in-game overlay is shown. Off by default, at
    /// nekron's call: a first run should put an icon in the tray and a
    /// dashboard in the browser, not an unexpected window over the game. The
    /// overlay is switched on from the Options page or the tray menu, and the
    /// choice sticks.
    /// </summary>
    public bool ShowOverlay { get; init; } = false;

    private static string Path_ => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Quantumwake",
        "settings.json");

    public static Settings Load()
    {
        try
        {
            if (!File.Exists(Path_))
                return new Settings();

            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path_)) ?? new Settings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt settings file should start the app, not stop it.
            return new Settings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path_)!);
            File.WriteAllText(Path_, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing a preference is not worth interrupting the user over.
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
