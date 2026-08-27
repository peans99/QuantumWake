using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The scrubbing that lets an unreadable log line be quoted in a bug report.
/// </summary>
/// <remarks>
/// The report is an allow-list, so most of it cannot leak anything by
/// construction. The exception is the one field that carries raw log text - the
/// example of a line the parser could not read - which is also the field worth
/// having. These are the cases that field has to survive: a line naming the
/// pilot, a line carrying the ids the game stamps on everything, and lines
/// where a careless replacement would destroy the very text being diagnosed.
/// </remarks>
public class DiagnosticsTests
{
    private static readonly string[] Known = ["nekron", "203059584653"];

    /// <summary>
    /// The handle is the thing a pilot most reasonably fears leaking, and it
    /// appears in the one log line that names them.
    /// </summary>
    [Fact]
    public void A_handle_is_replaced_wherever_it_appears()
    {
        var line = Diagnostics.Scrub(
            "<Legacy login response> [CIG-net] User Login Success - Handle[nekron] - Time[177332566]",
            Known);

        Assert.DoesNotContain("nekron", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<pilot>", line);

        // The shape has to survive, or the line stops being a parser diagnosis.
        Assert.Contains("Legacy login response", line);
        Assert.Contains("Time[177332566]", line);
    }

    /// <summary>
    /// Character and account ids go whether or not this install is the one that
    /// owns them - an unread line may well be about somebody else.
    /// </summary>
    [Fact]
    public void Account_and_character_ids_go_even_when_they_are_not_ours()
    {
        var line = Diagnostics.Scrub(
            "<Character> createdAt 1784476187540 - geid 999888777666 - accountId 51915 - state STATE_CURRENT",
            Known);

        Assert.DoesNotContain("999888777666", line);
        Assert.DoesNotContain("51915", line);
        Assert.Contains("geid <id>", line);
        Assert.Contains("accountId <id>", line);
        Assert.Contains("STATE_CURRENT", line);
    }

    /// <summary>
    /// Session and shard ids are GUIDs, and identify a play session even with
    /// no name attached to it.
    /// </summary>
    [Fact]
    public void Session_guids_are_replaced()
    {
        var line = Diagnostics.Scrub(
            "[Trace] @session: '252b4d6e-2373-2945-9af6-8b0c2609e773' host 'local_shard'", Known);

        Assert.DoesNotContain("252b4d6e", line);
        Assert.Contains("<session>", line);
        Assert.Contains("local_shard", line);
    }

    /// <summary>
    /// A handle appearing inside a longer word still goes. Matching on word
    /// boundaries would leave "nekrons_Corsair" naming its owner.
    /// </summary>
    [Fact]
    public void A_handle_inside_another_word_goes_too()
    {
        var line = Diagnostics.Scrub("<Vehicle> nekrons_Corsair_1234 destroyed", Known);

        Assert.DoesNotContain("nekron", line, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A two-letter handle would match inside half the words in a line and turn
    /// the sample into noise, which helps nobody and hides the defect. Short
    /// ones are left, and the report is honest that it quotes log text.
    /// </summary>
    [Fact]
    public void A_handle_too_short_to_match_safely_is_left_alone()
    {
        var line = Diagnostics.Scrub("<Actor Death> CoreMission destroyed", ["Co"]);

        Assert.Contains("CoreMission", line);
    }

    /// <summary>
    /// Nothing to scrub is not an error, and neither is a line with no
    /// identifiers in it at all.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("<Some Tag> nothing identifying here")]
    public void A_line_with_nothing_to_take_out_is_unchanged(string line)
    {
        Assert.Equal(line, Diagnostics.Scrub(line, Known));
    }

    /// <summary>
    /// The case the value-based pass cannot cover, and the reason there is a
    /// shape-based one.
    /// </summary>
    /// <remarks>
    /// A sample only exists because a known tag stopped parsing. When the tag
    /// that stopped parsing is the login line itself, the handle never reached
    /// the store - so there is no value to search for, and the sample is the one
    /// carrying it. The game still writes the name where it always writes it.
    /// </remarks>
    [Fact]
    public void A_handle_nobody_has_read_yet_still_goes()
    {
        var line = Diagnostics.Scrub(
            "<Legacy login response> User Login Success - Handle[SomeoneWeNeverParsed] - Time[1]",
            known: []);

        Assert.DoesNotContain("SomeoneWeNeverParsed", line);
        Assert.Contains("Handle[<pilot>]", line);
        Assert.Contains("Legacy login response", line);
    }

    /// <summary>
    /// Same for the character line, which is the other place a name appears -
    /// and which carries the account and character ids beside it.
    /// </summary>
    [Fact]
    public void The_character_line_gives_up_nothing_either()
    {
        var line = Diagnostics.Scrub(
            "<Character> geid 203059584653 - accountId 51915 - name SomeoneWeNeverParsed - state STATE_CURRENT",
            known: []);

        Assert.DoesNotContain("SomeoneWeNeverParsed", line);
        Assert.DoesNotContain("203059584653", line);
        Assert.DoesNotContain("51915", line);
        Assert.Contains("STATE_CURRENT", line);
    }

    /// <summary>
    /// Counts without the lines is the default, because counts are safe by
    /// construction and the lines are not.
    /// </summary>
    /// <remarks>
    /// A sample exists only because a format changed, and the changed format is
    /// free to write a name in a shape nothing here has seen. That is not
    /// hypothetical: a log with the login line reshaped to <c>Pilot{name}</c>
    /// came through this scrubber still naming its pilot. The count survives
    /// either way, and the count is what says something broke.
    /// </remarks>
    [Fact]
    public void Without_being_asked_the_lines_themselves_are_left_behind()
    {
        var raw = new Dictionary<string, (int, string)>
        {
            ["Legacy login response"] = (2, "User Login OK - Pilot{TestPilot42} - Time[1]"),
        };

        var health = Diagnostics.Health(2, raw, known: []);

        Assert.Equal(2, health.Unread);
        Assert.Equal("Legacy login response", health.ByTag[0].Tag);
        Assert.Equal(2, health.ByTag[0].Count);
        Assert.Equal(string.Empty, health.ByTag[0].Sample);
    }

    /// <summary>
    /// And when they are asked for, the limit is real: this is the line that
    /// gets through, and the page has to say so rather than imply otherwise.
    /// </summary>
    [Fact]
    public void A_name_in_a_shape_nobody_knows_survives_being_asked_for()
    {
        var raw = new Dictionary<string, (int, string)>
        {
            ["Legacy login response"] = (2, "User Login OK - Pilot{TestPilot42} - Time[1]"),
        };

        var health = Diagnostics.Health(2, raw, known: [], samples: true);

        Assert.Contains("TestPilot42", health.ByTag[0].Sample);
    }

    /// <summary>
    /// Counts and one sample per tag, ordered so the biggest problem is read
    /// first - and the sample scrubbed on the way through, since that is the
    /// only path by which a sample reaches a report.
    /// </summary>
    [Fact]
    public void Health_orders_by_count_and_scrubs_every_sample()
    {
        var raw = new Dictionary<string, (int, string)>
        {
            ["Rare"] = (2, "<Rare> Handle[nekron] did something"),
            ["Common"] = (57, "<Common> geid 12345678901 moved"),
        };

        var health = Diagnostics.Health(59, raw, Known, samples: true);

        Assert.Equal(59, health.Unread);
        Assert.Equal("Common", health.ByTag[0].Tag);
        Assert.Equal(57, health.ByTag[0].Count);
        Assert.Contains("geid <id>", health.ByTag[0].Sample);
        Assert.DoesNotContain("nekron", health.ByTag[1].Sample, StringComparison.OrdinalIgnoreCase);
    }
}
