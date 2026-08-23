using Quantumwake.Core.State;

namespace Quantumwake.Tests;

/// <summary>
/// Party toasts are the only lines in a 4.9 log that name another player, and
/// they arrive as free text on a channel that also carries chatter naming
/// nobody. The risk is not failing to read one - it is reading a word out of a
/// sentence and calling it a person.
/// </summary>
public class PartyTests
{
    private static readonly DateTimeOffset At =
        new(2026, 8, 23, 3, 14, 21, TimeSpan.Zero);

    /// <summary>
    /// The text as it reaches the parser: the game prints the title and the body
    /// on separate lines, and LogFileReader joins them with a space, leaving the
    /// title glued to the front and the notification's colon on the end.
    /// </summary>
    [Theory]
    [InlineData("Party D-Rud connected.:", "D-Rud", PartyMoment.Connected)]
    [InlineData("Party D-Rud disconnected.:", "D-Rud", PartyMoment.Disconnected)]
    [InlineData("Party Drafts-of-Singularity connected.:", "Drafts-of-Singularity", PartyMoment.Connected)]
    [InlineData("Party astro_ice connected.:", "astro_ice", PartyMoment.Connected)]
    [InlineData("Party KR105 disconnected.:", "KR105", PartyMoment.Disconnected)]
    [InlineData("New Party Leader Craven is now party leader.:", "Craven", PartyMoment.BecameLeader)]
    public void Reads_who_and_what_happened(string text, string handle, PartyMoment moment)
    {
        var note = Party.Read(At, text);

        Assert.NotNull(note);
        Assert.Equal(handle, note.Handle);
        Assert.Equal(moment, note.Moment);
        Assert.Equal(At, note.At);
    }

    /// <summary>
    /// Disbanding names nobody, and must not invent somebody. Note the title:
    /// this one is "Party Disbanded", not "Party", so the body arrives as
    /// "Party Disbanded The party has been disbanded." - which is why the title
    /// is what gets matched.
    /// </summary>
    [Fact]
    public void A_disbanded_party_has_no_handle()
    {
        var note = Party.Read(At, "Party Disbanded The party has been disbanded.:");

        Assert.NotNull(note);
        Assert.Null(note.Handle);
        Assert.Equal(PartyMoment.Disbanded, note.Moment);
    }

    /// <summary>
    /// The rest of the party channel. None of these say who is present, and
    /// each would yield a plausible-looking handle to a looser reader - "The",
    /// "Initiated", "Join". The mojibake is real: one file in this corpus has a
    /// line where the game wrote a half-decoded string.
    /// </summary>
    [Theory]
    [InlineData("Party Launch Initiated by party leader KR105.:")]
    [InlineData("Party Launch Join queue canceled by party leader KR105.:")]
    [InlineData("Party Launch Accepted Initiated by party leader KR105.:")]
    [InlineData("Party Notifications sent to party members.:")]
    [InlineData("Party ��潍楳s���:")]
    [InlineData("Party :")]
    [InlineData("Party")]
    public void Chatter_that_names_nobody_is_left_unread(string text)
    {
        Assert.Null(Party.Read(At, text));
    }

    /// <summary>
    /// A handle is one unbroken word. Without that rule any sentence ending in
    /// the right verb becomes a player - and the party channel is full of
    /// sentences.
    /// </summary>
    [Fact]
    public void A_sentence_ending_the_right_way_is_still_not_a_handle()
    {
        Assert.Null(Party.Read(At, "Party Everyone in the group connected.:"));
        Assert.Null(Party.Read(At, "New Party Leader nobody at all is now party leader.:"));
    }

    /// <summary>Notifications from every other channel are not ours to read.</summary>
    [Theory]
    [InlineData("Contract Accepted: Recover Cargo")]
    [InlineData("Medical Bed: You have been healed.")]
    [InlineData("Incapacitated: While incapacitated, ask others in your party...")]
    public void Only_the_party_channel_counts(string text)
    {
        Assert.False(Party.IsParty(text));
        Assert.Null(Party.Read(At, text));
    }

    /// <summary>
    /// "Incapacitated" mentions the word party in its body, which is exactly the
    /// kind of thing a substring check would swallow.
    /// </summary>
    [Fact]
    public void The_channel_is_matched_at_the_front_not_anywhere()
    {
        Assert.False(Party.IsParty(
            "Incapacitated: While incapacitated, ask others in your party, in chat, "
            + "or through rescue service beacons to revive you."));
    }
}
