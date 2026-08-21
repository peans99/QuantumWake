using System.IO;
using System.Text.Json;

namespace Verselog.Overlay;

/// <summary>
/// Remembers where the overlay was left, so a resize survives a restart.
/// </summary>
/// <remarks>
/// Stored beside the session cache in local app data. Nothing is written to the
/// game directory - the overlay's read-only stance covers its own settings too.
/// </remarks>
internal sealed record OverlayGeometry(double Left, double Top, double Width, double Height)
{
    private static string Path_ => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Verselog",
        "overlay.json");

    public static OverlayGeometry? Load()
    {
        try
        {
            if (!File.Exists(Path_))
                return null;

            var geometry = JsonSerializer.Deserialize<OverlayGeometry>(File.ReadAllText(Path_));

            // Guard against a corrupt or stale file leaving the window invisible.
            if (geometry is null || geometry.Width < 200 || geometry.Height < 140)
                return null;

            return geometry;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path_)!);
            File.WriteAllText(Path_, JsonSerializer.Serialize(this));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing the saved position is not worth interrupting the user over.
        }
    }

    /// <summary>
    /// True when the window would land on a screen that still exists. Monitors
    /// get unplugged, and a remembered position on a missing one is invisible.
    /// </summary>
    public bool IsOnScreen()
    {
        var virtualLeft = System.Windows.SystemParameters.VirtualScreenLeft;
        var virtualTop = System.Windows.SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + System.Windows.SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + System.Windows.SystemParameters.VirtualScreenHeight;

        // Require a reasonable slice of the title area to be reachable.
        const double margin = 80;

        return Left + margin < virtualRight
            && Left + Width - margin > virtualLeft
            && Top + margin < virtualBottom
            && Top + 30 > virtualTop;
    }
}
