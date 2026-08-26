namespace Quantumwake.Core.State;

/// <summary>What a ship's comms channel reported.</summary>
public enum ChannelMoment
{
    /// <summary>You boarded a ship - yours or somebody else's.</summary>
    YouBoarded,

    /// <summary>Somebody else boarded a ship you were already in.</summary>
    TheyBoarded,

    /// <summary>Somebody else left one.</summary>
    TheyLeft
}

/// <summary>
/// One ship comms notification, read.
/// </summary>
/// <param name="Ship">
/// The vehicle's display name, as the game writes it - "RSI Ursa Medivac".
/// </param>
/// <param name="Owner">Whose ship it is; possibly you.</param>
/// <param name="Handle">
/// Who the line is about, or null for <see cref="ChannelMoment.YouBoarded"/>,
/// which names nobody because the subject is the reader.
/// </param>
public sealed record ChannelNote(
    DateTimeOffset At, string Ship, string Owner, string? Handle, ChannelMoment Moment);

/// <summary>
/// Reads the ship comms channels the game opens when somebody boards.
/// </summary>
/// <remarks>
/// <para>
/// The only line in a 4.9 log that puts a person inside a particular vehicle.
/// The party channel says who was online while grouped with you; this says who
/// was actually aboard, and whose ship it was.
/// </para>
/// <para>
/// The volume is modest and worth stating plainly, because it is easy to
/// overcount by grepping: each notification is written to the log four or five
/// times as it is queued, faded and removed. Counting the queue entries only,
/// this install has 410 boardings - 388 of the reader's own ships and 21 of
/// somebody else's - 22 boardings by other people, and 24 departures, across 28
/// distinct ship-and-owner berths and five other pilots. Small, but it is the
/// only source for the fact at all.
/// </para>
/// <para>
/// Three shapes, and they are not symmetrical. Boarding it yourself is its own
/// notification whose whole text is the title; somebody else boarding arrives
/// under <c>New Member Joined</c> and leaving under <c>Member Left</c> - both
/// titles the party channel also uses, which is why each is decided on its body
/// rather than its title:
/// </para>
/// <code>
/// You have joined channel 'RSI Ursa Medivac : DeathStrokeo1'.
/// New Member Joined Sylosis has joined the channel 'Tumbril Cyclone MT : nekron'.
/// Member Left Sylosis has left the channel 'Drake Cutlass Black : Sylosis'.
/// </code>
/// <para>
/// What this cannot say is worth stating wherever it is used. There is no
/// leave line for the reader, so time aboard cannot be measured; a channel is
/// opened on boarding rather than on flying, so sitting in a friend's parked
/// ship counts the same as crossing a system in it; and the reader only ever
/// sees channels they were in themselves. Every figure built on this is a
/// floor, like everything else drawn from these logs.
/// </para>
/// </remarks>
public static class ShipChannel
{
    private const string Boarded = "You have joined channel ";
    private const string Joined = "New Member Joined ";
    private const string Left = "Member Left ";

    /// <summary>True when a notification is about a ship's comms channel.</summary>
    /// <remarks>
    /// Asked of the body rather than the title for the two shared ones, because
    /// <c>New Member Joined</c> and <c>Member Left</c> each carry party events
    /// too - 22 party joins against 22 boardings, and 33 party departures against
    /// 24 channel exits. Reading
    /// the title alone would have each reader counting the other's lines as its
    /// own unread remainder.
    /// </remarks>
    public static bool IsChannel(string text) =>
        text.StartsWith(Boarded, StringComparison.OrdinalIgnoreCase)
        || ((text.StartsWith(Joined, StringComparison.OrdinalIgnoreCase)
                || text.StartsWith(Left, StringComparison.OrdinalIgnoreCase))
            && text.Contains(" the channel '", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The note this notification carries, or null when it is not one.
    /// </summary>
    public static ChannelNote? Read(DateTimeOffset at, string text)
    {
        if (Tail(text, Boarded) is { } mine)
            return Berth(mine) is var (ship, owner) && ship is not null
                ? new ChannelNote(at, ship, owner!, null, ChannelMoment.YouBoarded)
                : null;

        foreach (var (prefix, ending, moment) in Shapes)
        {
            if (Tail(text, prefix) is not { } body)
                continue;

            var at_ = body.IndexOf(ending, StringComparison.OrdinalIgnoreCase);
            if (at_ <= 0)
                return null;

            var handle = body[..at_];

            // A handle is one unbroken word, for the same reason the party
            // reader insists on it: the channel also carries sentences.
            if (handle.Length == 0 || handle.Contains(' '))
                return null;

            return Berth(body[(at_ + ending.Length)..]) is var (ship, owner) && ship is not null
                ? new ChannelNote(at, ship, owner!, handle, moment)
                : null;
        }

        return null;
    }

    private static readonly (string Prefix, string Ending, ChannelMoment Moment)[] Shapes =
    [
        (Joined, " has joined the channel ", ChannelMoment.TheyBoarded),
        (Left, " has left the channel ", ChannelMoment.TheyLeft),
    ];

    /// <summary>
    /// The ship and its owner out of <c>'RSI Ursa Medivac : DeathStrokeo1'</c>.
    /// </summary>
    /// <remarks>
    /// Split on the last " : " rather than the first, because a ship name may
    /// contain a colon and a handle may not contain a space.
    /// </remarks>
    private static (string? Ship, string? Owner) Berth(string text)
    {
        var open = text.IndexOf('\'');
        if (open < 0) return (null, null);

        var close = text.IndexOf('\'', open + 1);
        if (close < 0) return (null, null);

        var inside = text[(open + 1)..close];
        var split = inside.LastIndexOf(" : ", StringComparison.Ordinal);
        if (split <= 0) return (null, null);

        var ship = inside[..split].Trim();
        var owner = inside[(split + 3)..].Trim();

        return ship.Length > 0 && owner.Length > 0 && !owner.Contains(' ')
            ? (ship, owner)
            : (null, null);
    }

    private static string? Tail(string text, string prefix) =>
        text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? text[prefix.Length..]
            : null;
}
