using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>Recorded frames standing in for the game window, for a demo.</summary>
public sealed class ReplayCaptureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qw-replay-" + Guid.NewGuid().ToString("N"));

    public ReplayCaptureTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    /// <summary>The smallest PNG header that says its size: signature, IHDR length and type, width, height.</summary>
    private string Png(string name, int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
        bytes[16] = (byte)(width >> 24); bytes[17] = (byte)(width >> 16); bytes[18] = (byte)(width >> 8); bytes[19] = (byte)width;
        bytes[20] = (byte)(height >> 24); bytes[21] = (byte)(height >> 16); bytes[22] = (byte)(height >> 8); bytes[23] = (byte)height;
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void No_folder_or_an_empty_one_is_no_replay()
    {
        Assert.Null(ReplayGameCapture.From(null));
        Assert.Null(ReplayGameCapture.From(Path.Combine(_root, "missing")));
        Assert.Null(ReplayGameCapture.From(_root));
    }

    [Fact]
    public void Frames_play_in_name_order_and_round_again_with_their_size()
    {
        Png("b_000.png", 3440, 1440);
        Png("a_000.png", 1920, 1080);
        var replay = ReplayGameCapture.From(_root)!;
        var frame = Path.Combine(_root, "out", "frame.bmp");
        Directory.CreateDirectory(Path.GetDirectoryName(frame)!);

        var first = replay.Capture(frame);
        var second = replay.Capture(frame);
        var third = replay.Capture(frame);

        Assert.True(first.Taken);
        Assert.Equal((1920, 1080), (first.Width, first.Height));
        Assert.Equal((3440, 1440), (second.Width, second.Height));
        Assert.Equal((1920, 1080), (third.Width, third.Height));
        Assert.True(File.Exists(frame));
    }
}
