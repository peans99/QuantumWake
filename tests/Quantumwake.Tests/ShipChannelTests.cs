using Quantumwake.Core.State;

namespace Quantumwake.Tests;

/// <summary>
/// Ship comms channels: who was aboard which vehicle, and whose it was.
/// </summary>
/// <remarks>
/// The only lines in a 4.9 log that put a person inside a particular ship. Two
/// of the three shapes arrive under titles the party channel also uses, so the
/// hazard here is not misreading a line but claiming one that belongs to the
/// other reader - which would leave each of them reporting the other's lines as
/// its own unread remainder.
/// </remarks>
public class ShipChannelTests
{
    private static readonly DateTimeOffset At = new(2026, 5, 10, 1, 25, 46, TimeSpan.Zero);

    /// <summary>
    /// The text as it reaches the reader: title glued to the front by
    /// LogFileReader, and the notification's own colon still on the end.
    /// </summary>
    [Theory]
    [InlineData("You have joined channel 'RSI Ursa Medivac : DeathStrokeo1'.:",
        "RSI Ursa Medivac", "DeathStrokeo1", null, ChannelMoment.YouBoarded)]
    [InlineData("You have joined channel 'Tumbril Cyclone MT : nekron'.:",
        "Tumbril Cyclone MT", "nekron", null, ChannelMoment.YouBoarded)]
    [InlineData("New Member Joined Sylosis has joined the channel 'Tumbril Cyclone MT : nekron'.:",
        "Tumbril Cyclone MT", "nekron", "Sylosis", ChannelMoment.TheyBoarded)]
    [InlineData("New Member Joined Vhailor-5 has joined the channel 'Drake Cutlass Black : Sylosis'.:",
        "Drake Cutlass Black", "Sylosis", "Vhailor-5", ChannelMoment.TheyBoarded)]
    [InlineData("Member Left Sylosis has left the channel 'Drake Cutlass Black : Sylosis'.:",
        "Drake Cutlass Black", "Sylosis", "Sylosis", ChannelMoment.TheyLeft)]
    [InlineData("Member Left Drafts-of-Singularity has left the channel 'RSI Perseus : Drafts-of-Singularity'.:",
        "RSI Perseus", "Drafts-of-Singularity", "Drafts-of-Singularity", ChannelMoment.TheyLeft)]
    public void Reads_the_ship_its_owner_and_who_the_line_is_about(
        string text, string ship, string owner, string? handle, ChannelMoment moment)
    {
        Assert.True(ShipChannel.IsChannel(text));

        var note = ShipChannel.Read(At, text);

        Assert.NotNull(note);
        Assert.Equal(ship, note.Ship);
        Assert.Equal(owner, note.Owner);
        Assert.Equal(handle, note.Handle);
        Assert.Equal(moment, note.Moment);
        Assert.Equal(At, note.At);
    }

    /// <summary>
    /// Both of these titles belong to the party channel as well - 22 party joins
    /// against 22 boardings here, and 33 departures against 27 exits. Each
    /// reader has to leave the other's lines alone, or the count of what it
    /// declined to read stops meaning anything.
    /// </summary>
    [Theory]
    [InlineData("New Member Joined Vhailor-5 has joined the party.:")]
    [InlineData("Member Left Sylosis has left the party.:")]
    [InlineData("Party D-Rud connected.:")]
    [InlineData("New Party Leader Craven is now party leader.:")]
    [InlineData("Party Disbanded The party has been disbanded.:")]
    public void A_party_line_is_not_a_ship_channel(string text)
    {
        Assert.False(ShipChannel.IsChannel(text));
        Assert.Null(ShipChannel.Read(At, text));
    }

    /// <summary>The other direction: the party reader must leave these alone.</summary>
    [Theory]
    [InlineData("You have joined channel 'RSI Ursa Medivac : DeathStrokeo1'.:")]
    [InlineData("New Member Joined Sylosis has joined the channel 'Tumbril Cyclone MT : nekron'.:")]
    [InlineData("Member Left Sylosis has left the channel 'Drake Cutlass Black : Sylosis'.:")]
    public void A_ship_channel_is_not_a_party_line(string text)
    {
        Assert.False(Party.IsParty(text));
        Assert.Null(Party.Read(At, text));
    }

    /// <summary>
    /// Anything that does not fit is left unread rather than guessed at, the
    /// same way the party reader treats queue chatter.
    /// </summary>
    [Theory]
    [InlineData("You have joined channel 'no owner here'.:")]
    [InlineData("You have joined channel ''.:")]
    [InlineData("You have joined channel 'Ship : two words'.:")]
    [InlineData("New Member Joined  has joined the channel 'Ship : owner'.:")]
    [InlineData("New Member Joined two words has joined the channel 'Ship : owner'.:")]
    [InlineData("You have joined channel")]
    public void A_line_that_does_not_fit_is_left_unread(string text)
    {
        Assert.Null(ShipChannel.Read(At, text));
    }

    /// <summary>
    /// A ship name can carry a colon and a handle cannot carry a space, so the
    /// split is the last " : " rather than the first.
    /// </summary>
    [Fact]
    public void A_ship_whose_name_contains_a_colon_still_splits_correctly()
    {
        var note = ShipChannel.Read(At,
            "You have joined channel 'MISC Starlancer MAX : Special : nekron'.:");

        Assert.NotNull(note);
        Assert.Equal("MISC Starlancer MAX : Special", note.Ship);
        Assert.Equal("nekron", note.Owner);
    }
}
