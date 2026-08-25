namespace Quantumwake.Core.State;

/// <summary>What a party notification reported.</summary>
public enum PartyMoment
{
    /// <summary>Someone came online while partied with you.</summary>
    Connected,

    /// <summary>Someone dropped: logged out, crashed, or lost the server.</summary>
    Disconnected,

    /// <summary>Lead passed to someone - possibly you.</summary>
    BecameLeader,

    /// <summary>The party ended.</summary>
    Disbanded,

    /// <summary>Someone joined the party you were in.</summary>
    /// <remarks>
    /// Membership, not presence: <see cref="Connected"/> is a client coming
    /// online while already partied, which is a different fact and far more
    /// common. Never you - see the class remarks.
    /// </remarks>
    Joined,

    /// <summary>Someone left the party you were in.</summary>
    Left
}

/// <summary>One party notification, read.</summary>
/// <param name="Handle">
/// Who it was about, or null for <see cref="PartyMoment.Disbanded"/>, which
/// names nobody.
/// </param>
public sealed record PartyNote(DateTimeOffset At, string? Handle, PartyMoment Moment);

/// <summary>
/// Reads the party notifications the game puts on the HUD.
/// </summary>
/// <remarks>
/// <para>
/// The only place in a 4.9 log where another player is named. There is no party
/// roster event, no join event carrying a member list, and no id - just toasts
/// saying that somebody connected or dropped, printed while you happen to be
/// partied with them. So this cannot answer "who was in my party"; it answers
/// "who did the game mention", which is a smaller and different question, and
/// every count built on it has to be worded to match.
/// </para>
/// <para>
/// The toasts arrive over two log lines - the title, then the body - and reach
/// here already joined by <c>LogFileReader.ReadEntries</c>, which is why the
/// text reads "Party D-Rud disconnected.:" with the title glued to the front and
/// the notification's own trailing colon still attached.
/// </para>
/// <para>
/// The titles on this channel are not interchangeable. <c>Party</c> carries
/// clients coming online and dropping, <c>New Party Leader</c> the handover,
/// <c>Party Disbanded</c> the end, <c>New Member Joined</c> and
/// <c>Member Left</c> the membership changes, and <c>Party Launch</c> /
/// <c>Party Launch Accepted</c> are matchmaking queue chatter that names a
/// leader but says nothing about who is present. Disbanding is recognised by
/// its title rather than its body, because the body names nobody.
/// </para>
/// <para>
/// Joining and leaving are a different fact from connecting and disconnecting,
/// and both are worth having: on this install 187 clients came online and 64
/// dropped, against 22 joins and 33 departures. A member who logs out and back
/// in produces the first pair; one who actually left the group produces the
/// second, and reading only the first makes somebody who walked away look like
/// somebody with a poor connection.
/// </para>
/// <para>
/// <c>Member Left</c> is the one title that does not belong to one channel.
/// Most of its lines are ship comms - "X has left the channel 'RSI Ursa
/// Medivac : DeathStrokeo1'" - so the body has to be read rather than the
/// title trusted, or a passenger stepping out of a hired ship is recorded as
/// leaving a party they were never in.
/// </para>
/// <para>
/// Handles are matched as a single run of non-space characters because that is
/// what the game permits and what these logs contain - "D-Rud",
/// "Drafts-of-Singularity", "astro_ice", "LeonardCharette-SQsfKwqo". Anything
/// that does not fit a known sentence is left unread rather than guessed at:
/// the party channel also carries queue and matchmaking chatter naming nobody,
/// and one file here contains a line of mojibake where the game wrote a
/// half-decoded string.
/// </para>
/// </remarks>
public static class Party
{
    /// <summary>True when a notification came from the party channel at all.</summary>
    /// <remarks>
    /// Both shared titles are asked about their body rather than taken on their
    /// title, because a ship's comms channel uses the same two. On this install
    /// <c>New Member Joined</c> carries 22 party joins and 22 ship boardings,
    /// and <c>Member Left</c> 33 departures against 27 channel exits. Counting
    /// the other reader's lines here would not misread anybody -
    /// <see cref="Read"/> refuses them either way - but it would inflate "party
    /// notifications", and the gap between that number and the notes read is the
    /// only measure of what this reader is declining to guess at.
    /// </remarks>
    public static bool IsParty(string text) =>
        text.StartsWith("Party ", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("New Party Leader ", StringComparison.OrdinalIgnoreCase)
        || (text.StartsWith("New Member Joined", StringComparison.OrdinalIgnoreCase)
            && text.Contains(" has joined the party.", StringComparison.OrdinalIgnoreCase))
        || (text.StartsWith("Member Left", StringComparison.OrdinalIgnoreCase)
            && text.Contains(" has left the party.", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The note this notification carries, or null when it says nothing about
    /// who is present - a join queue opening, a broadcast being sent, a line
    /// the game garbled.
    /// </summary>
    public static PartyNote? Read(DateTimeOffset at, string text)
    {
        if (Tail(text, "New Party Leader ") is { } leader)
            return Ending(leader, " is now party leader.") is { } who
                ? new PartyNote(at, who, PartyMoment.BecameLeader)
                : null;

        // Matched on its title, which is where the game puts the fact. The body
        // ("The party has been disbanded.") names nobody, so it is checked
        // before anything that wants a handle - otherwise a reader looking for a
        // word in front of a verb finds "The".
        if (text.StartsWith("Party Disbanded", StringComparison.OrdinalIgnoreCase))
            return new PartyNote(at, null, PartyMoment.Disbanded);

        if (Tail(text, "New Member Joined ") is { } arrival)
            return Ending(arrival, " has joined the party.") is { } came
                ? new PartyNote(at, came, PartyMoment.Joined)
                : null;

        // Read for the ending rather than the title: most Member Left lines are
        // a ship's comms channel emptying, which says who was aboard whose ship
        // and nothing at all about a party.
        if (Tail(text, "Member Left ") is { } departure)
            return Ending(departure, " has left the party.") is { } went
                ? new PartyNote(at, went, PartyMoment.Left)
                : null;

        if (Tail(text, "Party ") is not { } body)
            return null;

        if (Ending(body, " connected.") is { } joined)
            return new PartyNote(at, joined, PartyMoment.Connected);

        if (Ending(body, " disconnected.") is { } left)
            return new PartyNote(at, left, PartyMoment.Disconnected);

        return null;
    }

    /// <summary>What follows a prefix, or null when the prefix is not there.</summary>
    private static string? Tail(string text, string prefix) =>
        text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? text[prefix.Length..]
            : null;

    /// <summary>
    /// The handle in front of a fixed ending, or null when the shape is wrong.
    /// </summary>
    /// <remarks>
    /// The notification's own trailing colon is trimmed first: the game appends
    /// ": " before the queue detail, and it survives into the joined text.
    /// A handle must be one unbroken word, so anything containing a space is a
    /// sentence that merely ends the same way, not a name.
    /// </remarks>
    private static string? Ending(string body, string ending)
    {
        var text = body.TrimEnd(' ', ':');

        if (!text.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            return null;

        var handle = text[..^ending.Length];

        return handle.Length > 0 && !handle.Contains(' ') ? handle : null;
    }
}
