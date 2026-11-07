using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The per-frame log a play session leaves behind: numbers, never screen text,
/// and nothing at all for a pilot who never turned live reading on.
/// </summary>
public sealed class LiveFrameLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "qw-flog-" + Guid.NewGuid().ToString("N"));
    private DateTimeOffset _now = new(2026, 11, 5, 21, 0, 0, TimeSpan.FromHours(-5));

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    private LiveFrameLog Log() => new(_folder, () => _now);

    private string[] Lines(LiveFrameLog log) => File.ReadAllLines(log.PathFor(_now));

    [Fact]
    public void A_read_frame_is_one_line_under_a_header()
    {
        var log = Log();
        log.Read("Mining", 14, 820, 2560, 1440, "live-20261106-020000");

        var lines = Lines(log);
        Assert.Equal(LiveFrameLog.Header, lines[0]);
        Assert.Equal("2026-11-05T21:00:00.000-05:00,read,Mining,14,820,2560,1440,live-20261106-020000,", lines[1]);
    }

    /// <summary>An hour alt-tabbed is one line, not eighteen hundred.</summary>
    [Fact]
    public void A_wait_is_written_only_when_its_reason_changes()
    {
        var log = Log();
        for (var i = 0; i < 50; i++) log.Waiting("Star Citizen is not the window in front");
        log.Waiting("Star Citizen is minimised");
        log.Read("Unknown", 9, 700, 2560, 1440, null);
        log.Waiting("Star Citizen is not the window in front");

        var states = Lines(log).Skip(1).Select(l => l.Split(',')[1]).ToList();
        Assert.Equal(["waiting", "waiting", "read", "waiting"], states);
    }

    [Fact]
    public void A_reason_with_commas_and_quotes_stays_one_field()
    {
        var log = Log();
        log.Waiting("the copy came back black - exclusive fullscreen; borderless, or \"windowed\", can be read");

        var line = Lines(log)[1];
        Assert.EndsWith("\"the copy came back black - exclusive fullscreen; borderless, or \"\"windowed\"\", can be read\"", line);
    }

    /// <summary>A pilot who said no, or who never turned it on, gets no folder and no file.</summary>
    [Fact]
    public void Switching_off_writes_nothing_unless_reading_had_been_going()
    {
        var log = Log();
        log.Stopped();
        log.Stopped();
        Assert.False(Directory.Exists(_folder));

        log.Read("Mining", 10, 600, 1920, 1080, null);
        log.Stopped();
        log.Stopped();
        Assert.Equal(["read", "off"], Lines(log).Skip(1).Select(l => l.Split(',')[1]));
    }

    [Fact]
    public void A_file_a_day_by_local_date_and_a_week_kept()
    {
        Directory.CreateDirectory(_folder);
        var old = Path.Combine(_folder, "frames-2026-10-20.csv");
        var recent = Path.Combine(_folder, "frames-2026-11-01.csv");
        File.WriteAllText(old, LiveFrameLog.Header);
        File.WriteAllText(recent, LiveFrameLog.Header);

        var log = Log();
        log.Read("Unknown", 5, 500, 1920, 1080, null);

        // The machine's own date for that moment: 21:00 at UTC-5 is the 5th
        // here and already the 6th on a runner kept in UTC, and both are right.
        Assert.EndsWith($"frames-{_now.ToLocalTime():yyyy-MM-dd}.csv", log.PathFor(_now));
        Assert.Contains(_now.ToLocalTime().Day, new[] { 5, 6 });
        Assert.True(File.Exists(log.PathFor(_now)));
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }
}
