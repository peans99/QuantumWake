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

    /// <summary>
    /// One patch of the frame again, scaled and sheared before the engine
    /// sees it, with the boxes mapped back into the frame's own pixels.
    /// </summary>
    /// <remarks>
    /// The second look <see cref="WalletSecondLook"/> takes when the whole
    /// frame read and the balance did not. Mapping back is the reader's job
    /// because only it knows what it did to the pixels; the caller wants to
    /// ask the same questions of these lines as of the first read's.
    /// </remarks>
    Task<IReadOnlyList<ScreenTextLine>> ReadAsync(
        string imagePath, ScreenPatch patch, ScreenTreatment treatment, CancellationToken token = default);

    /// <summary>
    /// One patch of the frame as colour, unscaled, or null when this reader
    /// cannot decode pixels.
    /// </summary>
    /// <remarks>
    /// For the things on a screen that are drawn rather than written - the
    /// Rep app's progress bars, which are a teal fill on a grey track with no
    /// figure anywhere. See <see cref="RepBars"/>.
    /// </remarks>
    Task<ColourPatch?> PixelsAsync(string imagePath, ScreenPatch patch, CancellationToken token = default) =>
        Task.FromResult<ColourPatch?>(null);
}

/// <summary>
/// The engine cannot read anything at the moment, as opposed to one file
/// being unreadable.
/// </summary>
/// <remarks>
/// Kept apart because a refusal about a file is stored and the file is not
/// offered again, and a refusal about the engine must not be. The ONNX
/// reader fetches its models on first use, so a first switch-on while
/// offline would otherwise have filed every shot it touched as unreadable
/// for good.
/// </remarks>
public sealed class ScreenEngineUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>A rectangle of the frame, in its pixels.</summary>
public sealed record ScreenPatch(double Left, double Top, double Width, double Height);

/// <summary>A rectangle of a frame in colour, addressed in the frame's own pixels.</summary>
/// <remarks>
/// Frame coordinates rather than patch coordinates so that a caller measuring
/// against the read's text lines never has to subtract anything. A pixel
/// outside the patch reads as the nearest edge, the way <see cref="GreyImage"/>
/// clamps, so a search that strays a pixel over does not throw.
/// </remarks>
public sealed class ColourPatch
{
    private readonly byte[] _bgra;

    public ColourPatch(int left, int top, int width, int height, byte[] bgra)
    {
        if (bgra.Length != width * height * 4)
            throw new ArgumentException($"expected {width * height * 4} bytes, got {bgra.Length}", nameof(bgra));

        Left = left;
        Top = top;
        Width = width;
        Height = height;
        _bgra = bgra;
    }

    public int Left { get; }

    public int Top { get; }

    public int Width { get; }

    public int Height { get; }

    public (byte R, byte G, byte B) At(int x, int y)
    {
        var px = Math.Clamp(x - Left, 0, Width - 1);
        var py = Math.Clamp(y - Top, 0, Height - 1);
        var at = (py * Width + px) * 4;
        return (_bgra[at + 2], _bgra[at + 1], _bgra[at]);
    }
}

/// <summary>What to do to a patch before the engine reads it.</summary>
/// <param name="Scale">How many times larger to draw it.</param>
/// <param name="Shear">
/// How far each row is pulled left per pixel of height above the bottom, so
/// that a right-leaning face stands up. 0.2 is about eleven degrees, which is
/// what makes the mobiGlas balance read; see <see cref="WalletSecondLook"/>.
/// </param>
public sealed record ScreenTreatment(double Scale, double Shear);

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
