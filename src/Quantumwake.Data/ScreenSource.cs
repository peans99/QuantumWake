namespace Quantumwake.Data;

/// <summary>
/// What the app is allowed to do with the screen, chosen by the pilot.
/// </summary>
/// <remarks>
/// A setting rather than a preference, and it defaults to the narrow one. See
/// <c>docs/screen-insight.md</c>: the clipboard route asks the game a question
/// and reads its answer, which is what every other tool in this space does and
/// what the licence plainly permits. Reading a screenshot is a file the pilot
/// saved and is also fine, but it is a bigger thing to be doing and nobody
/// should find it switched on because a default said so.
/// </remarks>
public enum ScreenMode
{
    /// <summary>Neither. The panel is there and does nothing until asked.</summary>
    Off,

    /// <summary>Read what the pilot copied, and nothing else.</summary>
    CopyOnly,

    /// <summary>Read the clipboard, and screenshots when asked to.</summary>
    Screenshots
}

/// <summary>
/// Reads text out of an image file.
/// </summary>
/// <remarks>
/// An interface because the implementation needs the Windows SDK target and
/// the server does not. The overlay supplies one; a server run on its own has
/// none, and the endpoint says so rather than pretending.
/// </remarks>
public interface IScreenReader
{
    /// <summary>Every line the engine found, with where it sat.</summary>
    Task<IReadOnlyList<ScreenTextLine>> ReadAsync(string imagePath, CancellationToken token = default);
}

/// <summary>
/// Reads what the pilot last copied.
/// </summary>
/// <remarks>
/// Also an interface, for a duller reason: the clipboard belongs to a desktop
/// with a message pump, and the server has neither.
/// </remarks>
public interface IClipboardReader
{
    /// <summary>The clipboard as text, or null when it holds none.</summary>
    Task<string?> ReadTextAsync(CancellationToken token = default);
}

/// <summary>
/// Finds the screenshots the game has written.
/// </summary>
/// <remarks>
/// <para>
/// Lowercase <c>screenshots</c>, beside the install, named
/// <c>ScreenShot-&lt;date&gt;_&lt;time&gt;-&lt;hex&gt;.jpg</c> - measured, not
/// assumed. The folder does not exist until the pilot takes their first shot.
/// </para>
/// <para>
/// This looks at files and never at the screen. That is the rule the whole
/// feature is built around and the reason it is a folder listing rather than
/// a capture: what gets read is something the pilot chose to save.
/// </para>
/// </remarks>
public static class Screenshots
{
    public static string FolderFor(string installRoot) =>
        Path.Combine(installRoot, "screenshots");

    /// <summary>The most recent screenshot, or null if there are none.</summary>
    public static string? Newest(string installRoot)
    {
        var folder = FolderFor(installRoot);
        if (!Directory.Exists(folder)) return null;

        return new DirectoryInfo(folder)
            .EnumerateFiles("*.jpg")
            .Concat(new DirectoryInfo(folder).EnumerateFiles("*.png"))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()
            ?.FullName;
    }
}
