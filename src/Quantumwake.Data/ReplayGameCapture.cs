namespace Quantumwake.Data;

/// <summary>
/// Stands in for the game window with a folder of recorded frames, one a
/// capture, in name order, round and round - for showing the live reader
/// without a game running.
/// </summary>
/// <remarks>
/// <para>
/// Switched on only by <c>--Replay=&lt;folder&gt;</c> or the
/// <c>QUANTUMWAKE_REPLAY</c> environment variable, never by a setting: a
/// demo is something someone starts on purpose. It still needs the live
/// reading answer to be yes, like the real thing, and everything after the
/// capture - the readers, the combat watch, the voice, the pages - is the
/// code a fight runs.
/// </para>
/// <para>
/// Frames are copied as they are: both engines tell a JPEG or a PNG by its
/// content, not by the <c>frame.bmp</c> name it is copied to. Frames pulled
/// from an OBS recording one a second play at the pace of the fight, since
/// the live loop takes one a second while a target is held.
/// </para>
/// </remarks>
public sealed class ReplayGameCapture : IGameScreenCapture
{
    private readonly string[] _frames;
    private int _next;

    private ReplayGameCapture(string[] frames) => _frames = frames;

    /// <summary>The folder's frames, or null when it is not set or holds none.</summary>
    public static ReplayGameCapture? From(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return null;

        var frames = Directory.GetFiles(folder)
            .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return frames.Length == 0 ? null : new ReplayGameCapture(frames);
    }

    public int Count => _frames.Length;

    public GameCapture Capture(string path)
    {
        var frame = _frames[_next];
        _next = (_next + 1) % _frames.Length;

        File.Copy(frame, path, overwrite: true);
        var (width, height) = Size(frame);
        return width > 0 ? new GameCapture(true, null, width, height)
            : GameCapture.Not($"the replay frame {Path.GetFileName(frame)} is not a JPEG or PNG this can measure");
    }

    /// <summary>A JPEG's or PNG's size from its header, without decoding it.</summary>
    internal static (int Width, int Height) Size(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> head = stackalloc byte[24];
        if (stream.Read(head) < 24) return (0, 0);

        // PNG: the IHDR chunk's width and height, big-endian, at 16 and 20.
        if (head[0] == 0x89 && head[1] == (byte)'P')
            return (ReadBig(head[16..]), ReadBig(head[20..]));

        if (head[0] != 0xFF || head[1] != 0xD8)
            return (0, 0);

        // JPEG: walk the segments to the first start-of-frame marker.
        stream.Position = 2;
        Span<byte> marker = stackalloc byte[4];
        while (stream.Read(marker) == 4 && marker[0] == 0xFF)
        {
            var kind = marker[1];
            var length = (marker[2] << 8) | marker[3];
            if (kind is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                Span<byte> sof = stackalloc byte[5];
                if (stream.Read(sof) < 5) return (0, 0);
                return ((sof[3] << 8) | sof[4], (sof[1] << 8) | sof[2]);
            }
            stream.Position += length - 2;
        }

        return (0, 0);
    }

    private static int ReadBig(ReadOnlySpan<byte> b) => (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
}
