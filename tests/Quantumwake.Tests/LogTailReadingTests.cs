using Quantumwake.Core.Logging;
using System.Text;

namespace Quantumwake.Tests;

/// <summary>
/// Reading a log that is still being written to.
/// </summary>
/// <remarks>
/// This is what the live view stands on: the game holds Game.log open and
/// appends to it all session, and the tailer resumes from a byte offset rather
/// than re-reading four hundred megabytes every two seconds. Two things go wrong
/// quietly here. A file that shrank means the game rotated it and the offset now
/// points into the middle of a different log, which would show a returning
/// player somebody else's evening. And a file the game has open cannot be read
/// at all without sharing the handle, which fails only on a machine where the
/// game is actually running - never on a developer's.
/// </remarks>
public class LogTailReadingTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"qw-tail-{Guid.NewGuid():N}.log");

    private void Write(params string[] lines) =>
        File.WriteAllText(_path, string.Join(Environment.NewLine, lines) + Environment.NewLine);

    private void Append(params string[] lines) =>
        File.AppendAllText(_path, string.Join(Environment.NewLine, lines) + Environment.NewLine);

    [Fact]
    public void A_first_read_takes_the_whole_file_and_remembers_where_it_stopped()
    {
        Write("<2026-08-01T00:00:00.000Z> first", "<2026-08-01T00:00:01.000Z> second");

        long offset = 0;
        var lines = LogFileReader.ReadFrom(_path, ref offset, out var rotated);

        Assert.Equal(2, lines.Count);
        Assert.False(rotated);
        Assert.Equal(new FileInfo(_path).Length, offset);
    }

    [Fact]
    public void A_second_read_returns_only_what_was_appended_since()
    {
        Write("<2026-08-01T00:00:00.000Z> first");

        long offset = 0;
        LogFileReader.ReadFrom(_path, ref offset, out _);

        Append("<2026-08-01T00:00:02.000Z> second", "<2026-08-01T00:00:03.000Z> third");

        var lines = LogFileReader.ReadFrom(_path, ref offset, out var rotated);

        Assert.Equal(["<2026-08-01T00:00:02.000Z> second", "<2026-08-01T00:00:03.000Z> third"], lines);
        Assert.False(rotated);
    }

    /// <summary>
    /// Nothing new is nothing, not the last line again. The live feed would
    /// otherwise repeat an event on every poll for as long as the game was idle.
    /// </summary>
    [Fact]
    public void A_read_with_nothing_new_returns_nothing()
    {
        Write("<2026-08-01T00:00:00.000Z> only");

        long offset = 0;
        LogFileReader.ReadFrom(_path, ref offset, out _);
        var before = offset;

        Assert.Empty(LogFileReader.ReadFrom(_path, ref offset, out var rotated));
        Assert.False(rotated);
        Assert.Equal(before, offset);
    }

    /// <summary>
    /// The game rotates Game.log on launch. A stored offset then points into the
    /// middle of a different file, and reading from it would hand the page half
    /// a line and then somebody else's session.
    /// </summary>
    [Fact]
    public void A_shorter_file_is_a_rotation_and_is_read_from_the_top()
    {
        Write("<2026-08-01T00:00:00.000Z> a long first session with plenty in it",
              "<2026-08-01T00:00:01.000Z> and more besides");

        long offset = 0;
        LogFileReader.ReadFrom(_path, ref offset, out _);
        Assert.True(offset > 0);

        // The game restarted and began the log again.
        Write("<2026-08-02T00:00:00.000Z> new session");

        var lines = LogFileReader.ReadFrom(_path, ref offset, out var rotated);

        Assert.True(rotated);
        Assert.Equal(["<2026-08-02T00:00:00.000Z> new session"], lines);
        Assert.Equal(new FileInfo(_path).Length, offset);
    }

    /// <summary>
    /// A rotation to a file that happens to be exactly as long is invisible to
    /// this check, and the tailer cannot tell. Worth pinning as the known limit
    /// rather than leaving somebody to discover it as a bug.
    /// </summary>
    [Fact]
    public void A_rotation_to_the_same_length_is_not_detectable_here()
    {
        Write("<2026-08-01T00:00:00.000Z> aaaa");

        long offset = 0;
        LogFileReader.ReadFrom(_path, ref offset, out _);

        Write("<2026-08-01T00:00:00.000Z> bbbb");

        Assert.Empty(LogFileReader.ReadFrom(_path, ref offset, out var rotated));
        Assert.False(rotated);
    }

    /// <summary>
    /// The game keeps Game.log open for writing for the whole session, so any
    /// read that does not share the handle fails on exactly the machines this
    /// app exists for and on none of the ones it is written on.
    /// </summary>
    [Fact]
    public void A_log_the_game_still_holds_open_can_still_be_read()
    {
        Write("<2026-08-01T00:00:00.000Z> written before the lock");

        // What the game does: open for append, keep it, and let readers in.
        using var held = new FileStream(_path, FileMode.Append, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);

        long offset = 0;
        var lines = LogFileReader.ReadFrom(_path, ref offset, out _);
        Assert.Single(lines);

        held.Write(Encoding.UTF8.GetBytes("<2026-08-01T00:00:05.000Z> written while open" + Environment.NewLine));
        held.Flush();

        var more = LogFileReader.ReadFrom(_path, ref offset, out _);
        Assert.Equal(["<2026-08-01T00:00:05.000Z> written while open"], more);

        Assert.Equal(2, LogFileReader.ReadLines(_path).Count());
    }

    /// <summary>
    /// Offsets are bytes, not characters. A log carrying anything outside ASCII -
    /// a ship name, a handle, the mojibake the game sometimes writes - would
    /// otherwise resume mid-character and hand the parser a broken line.
    /// </summary>
    [Fact]
    public void An_offset_is_bytes_so_wide_characters_do_not_shift_it()
    {
        Write("<2026-08-01T00:00:00.000Z> Drafts-of-Singularity flew a 🚀 today");

        long offset = 0;
        LogFileReader.ReadFrom(_path, ref offset, out _);

        Assert.Equal(new FileInfo(_path).Length, offset);

        Append("<2026-08-01T00:00:01.000Z> plain");
        var next = LogFileReader.ReadFrom(_path, ref offset, out _);

        Assert.Equal(["<2026-08-01T00:00:01.000Z> plain"], next);
    }

    [Fact]
    public void An_empty_log_reads_as_nothing_rather_than_failing()
    {
        File.WriteAllText(_path, string.Empty);

        long offset = 0;
        Assert.Empty(LogFileReader.ReadFrom(_path, ref offset, out var rotated));
        Assert.False(rotated);
        Assert.Equal(0, offset);
        Assert.Empty(LogFileReader.ReadLines(_path));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch (IOException) { }
    }
}
