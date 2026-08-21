using Verselog.Core.Logging;
using Verselog.Core.Parsing;

namespace Verselog.Tests;

/// <summary>
/// Logical-entry reconstruction. Some Game.log entries embed newlines, and the
/// continuation lines carry their own timestamp, so a timestamp alone cannot
/// tell a new entry from a continuation.
/// </summary>
public class LogFileReaderTests
{
    [Fact]
    public void Single_line_entries_pass_through_unchanged()
    {
        string[] lines =
        [
            "<2026-08-20T01:28:42.748Z> Running 64 bit version",
            "<2026-08-20T01:28:42.748Z> FileVersion: 4.9.188.23497"
        ];

        Assert.Equal(lines, LogFileReader.ReadEntries(lines));
    }

    /// <summary>
    /// The real shape, copied verbatim from
    /// <c>Game Build(11674325) 22 Apr 26 (21 24 39).log</c> lines 1679-1680.
    /// </summary>
    [Fact]
    public void Joins_notification_split_across_two_timestamped_lines()
    {
        string[] lines =
        [
            "<2026-04-23T01:42:30.285Z> [Notice] <SHUDEvent_OnNotification> Added notification \"You have joined channel 'Origin 325a : nekron'.",
            "<2026-04-23T01:42:30.285Z> : \" [4] to queue. New queue size: 1, MissionId: [00000000-0000-0000-0000-000000000000], ObjectiveId: [] [Team_CoreGameplayFeatures][Missions][Comms]",
            "<2026-04-23T01:42:30.287Z> [Notice] <UpdateNotificationItem> Notification \"x\" [4], Action: Next"
        ];

        var entries = LogFileReader.ReadEntries(lines).ToList();

        Assert.Equal(2, entries.Count);
        Assert.Contains("[4] to queue.", entries[0]);
        Assert.DoesNotContain("<2026-04-23T01:42:30.285Z> :", entries[0]);
    }

    /// <summary>The joined entry must actually parse into a usable event.</summary>
    [Fact]
    public void Joined_notification_parses_end_to_end()
    {
        string[] lines =
        [
            "<2026-04-23T01:42:30.285Z> [Notice] <SHUDEvent_OnNotification> Added notification \"You have joined channel 'Origin 325a : nekron'.",
            "<2026-04-23T01:42:30.285Z> : \" [4] to queue. New queue size: 1, MissionId: [00000000-0000-0000-0000-000000000000], ObjectiveId: []"
        ];

        var entry = LogFileReader.ReadEntries(lines).Single();

        Assert.True(LogEnvelope.TryParse(entry, out var line));
        var ev = new LogEventParser().Parse(line);

        var notification = Assert.IsType<Core.Events.NotificationEvent>(ev);
        Assert.Equal("4", notification.NotificationId);
        Assert.Contains("joined channel", notification.Text);
    }

    /// <summary>
    /// Balanced quotes must never trigger a join, or ordinary entries would be
    /// swallowed into their predecessor.
    /// </summary>
    [Fact]
    public void Balanced_quotes_do_not_trigger_a_join()
    {
        string[] lines =
        [
            "<2026-08-20T01:28:58.088Z> [Notice] <Context Establisher Done> establisher=\"Game\" map=\"megamap\" gamerules=\"SC_Frontend\" sessionId=\"abc\"",
            "<2026-08-20T01:28:42.748Z> BackupNameAttachment=\" Build(12344265) 19 Aug 26 (21 28 37)\"  -- used by backup system",
            "<2026-08-20T01:28:42.748Z> Running 64 bit version"
        ];

        Assert.Equal(3, LogFileReader.ReadEntries(lines).Count());
    }

    [Theory]
    [InlineData("no quotes here", false)]
    [InlineData("one \" quote", true)]
    [InlineData("two \"quoted\" words", false)]
    [InlineData("three \"a\" \"b", true)]
    public void Detects_unterminated_quotes(string line, bool expected)
    {
        Assert.Equal(expected, LogFileReader.HasUnterminatedQuote(line));
    }

    [Fact]
    public void Strips_timestamp_from_continuation()
    {
        Assert.Equal(" : \" [4]", LogFileReader.StripTimestamp("<2026-04-23T01:42:30.285Z> : \" [4]"));
        Assert.Equal("no timestamp", LogFileReader.StripTimestamp("no timestamp"));
    }

    /// <summary>
    /// A malformed unbalanced quote must not swallow the rest of the file.
    /// </summary>
    [Fact]
    public void Runaway_joins_are_capped()
    {
        var lines = new List<string> { "<2026-08-20T01:00:00.000Z> broken \" quote" };
        for (var i = 0; i < 100; i++)
            lines.Add($"<2026-08-20T01:00:0{i % 10}.000Z> follower {i}");

        var entries = LogFileReader.ReadEntries(lines).ToList();

        Assert.True(entries.Count > 1, "cap should release subsequent lines as their own entries");
    }

    [Fact]
    public void Trailing_entry_is_emitted()
    {
        string[] lines =
        [
            "<2026-08-20T01:28:42.748Z> first",
            "<2026-08-20T01:28:43.748Z> last"
        ];

        Assert.Equal(2, LogFileReader.ReadEntries(lines).Count());
    }

    [Fact]
    public void Handles_empty_input()
    {
        Assert.Empty(LogFileReader.ReadEntries([]));
    }
}
