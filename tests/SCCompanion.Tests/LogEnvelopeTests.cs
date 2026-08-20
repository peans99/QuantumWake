using SCCompanion.Core.Logging;

namespace SCCompanion.Tests;

/// <summary>
/// Envelope parsing. Every fixture below is a real line copied from a
/// 4.9.188.23497 install, not a synthesised example.
/// </summary>
public class LogEnvelopeTests
{
    [Fact]
    public void Parses_standard_notice_line()
    {
        const string raw =
            "<2026-08-20T01:28:55.402Z> [Notice] <Legacy login response> [CIG-net] " +
            "User Login Success - Handle[nekron] - Time[177332566] [Team_GameServices][Login]";

        Assert.True(LogEnvelope.TryParse(raw, out var line));
        Assert.Equal("Notice", line.Severity);
        Assert.Equal("Legacy login response", line.Tag);
        Assert.False(line.IsSpam);
        Assert.StartsWith("[CIG-net]", line.Body);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 20, 1, 28, 55, 402, TimeSpan.Zero),
            line.Timestamp);
    }

    /// <summary>
    /// Trap 3: header lines and "Loading screen for ..." carry no severity tag at
    /// all. Assuming [Notice] is always present silently drops them.
    /// </summary>
    [Fact]
    public void Parses_line_with_no_severity_and_no_tag()
    {
        const string raw =
            "<2026-04-21T01:41:50.494Z> Loading screen for Frontend_Main : SC_Frontend closed after 3.44 seconds";

        Assert.True(LogEnvelope.TryParse(raw, out var line));
        Assert.Null(line.Severity);
        Assert.Null(line.Tag);
        Assert.StartsWith("Loading screen for", line.Body);
    }

    /// <summary>
    /// Trap 2: [SPAM nnn] stacks in front of the real severity tag. The line must
    /// still parse, and must be flagged so callers can drop the duplicate.
    /// </summary>
    [Fact]
    public void Flags_spam_lines_and_still_reads_real_severity()
    {
        const string raw =
            "<2026-04-27T01:53:07.044Z> [SPAM 299][Notice] <CObjectiveMarkerComponent::AddToPlayerDataBank> " +
            "MissionObjectiveMarker_7169[7169] - Added to DataBank of Player: nekron[9730519752057]";

        Assert.True(LogEnvelope.TryParse(raw, out var line));
        Assert.True(line.IsSpam);
        Assert.Equal("Notice", line.Severity);
        Assert.Equal("CObjectiveMarkerComponent::AddToPlayerDataBank", line.Tag);
    }

    /// <summary>
    /// A bracket group can stand in for the tag entirely, as with the session
    /// manager's spawn line.
    /// </summary>
    [Fact]
    public void Parses_bracket_only_line()
    {
        const string raw = "<2026-08-20T01:28:58.254Z> [CSessionManager::OnClientSpawned] Spawned!";

        Assert.True(LogEnvelope.TryParse(raw, out var line));
        Assert.Equal("CSessionManager::OnClientSpawned", line.Severity);
        Assert.Null(line.Tag);
        Assert.Equal("Spawned!", line.Body);
    }

    [Fact]
    public void Timestamps_are_normalised_to_utc()
    {
        const string raw = "<2026-08-20T01:28:42.748Z> Running 64 bit version";

        Assert.True(LogEnvelope.TryParse(raw, out var line));
        Assert.Equal(TimeSpan.Zero, line.Timestamp.Offset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no timestamp here")]
    [InlineData("<not-a-date> body")]
    public void Rejects_lines_without_a_usable_timestamp(string raw)
    {
        Assert.False(LogEnvelope.TryParse(raw, out _));
    }
}
