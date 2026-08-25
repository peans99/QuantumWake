using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Rolling ship channels up into who you flew with, in what.
/// </summary>
/// <remarks>
/// A pairing needs the reader and somebody else in the same channel, and there
/// are two ways for that to happen: they board a ship the reader is in, or the
/// reader boards one they own. Boarding your own ship alone is not flying with
/// anybody, and counting it would put the reader at the top of their own list.
/// </remarks>
public class SharedShipTests : IDisposable
{
    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;

    private static readonly DateTimeOffset At = new(2026, 5, 10, 1, 0, 0, TimeSpan.Zero);

    public SharedShipTests() => _library = new LogLibrary(_store);

    private void Save(string id, params ChannelNote[] notes) =>
        _store.Save(
            new SessionSummary
            {
                Id = id,
                SourceFile = $"{id}.log",
                StartedAt = At,
                EndedAt = At.AddHours(3),
                Handle = "nekron",
                ChannelNotes = notes,
            },
            $"fingerprint:{id}");

    [Fact]
    public void Somebody_boarding_your_ship_is_a_pairing()
    {
        Save("s1", new ChannelNote(At, "Tumbril Cyclone MT", "nekron", "Sylosis", ChannelMoment.TheyBoarded));

        var shared = Assert.Single(_library.SharedShips());

        Assert.Equal("Sylosis", shared.Handle);
        Assert.Equal("Tumbril Cyclone MT", shared.Ship);
        Assert.Equal("nekron", shared.Owner);
        Assert.Equal(1, shared.Times);
    }

    /// <summary>
    /// Their name is on the berth even when no arrival line ever named them, so
    /// crewing for somebody is recoverable from your own boarding alone.
    /// </summary>
    [Fact]
    public void Boarding_somebody_elses_ship_is_a_pairing_with_its_owner()
    {
        Save("s1", new ChannelNote(At, "RSI Ursa Medivac", "DeathStrokeo1", null, ChannelMoment.YouBoarded));

        var shared = Assert.Single(_library.SharedShips());

        Assert.Equal("DeathStrokeo1", shared.Handle);
        Assert.Equal("DeathStrokeo1", shared.Owner);
        Assert.Equal("RSI Ursa Medivac", shared.Ship);
    }

    /// <summary>
    /// Getting into your own ship is the overwhelming majority of these lines -
    /// 388 of 410 on this install - and it is not flying with anybody.
    /// </summary>
    [Fact]
    public void Boarding_your_own_ship_is_not_a_pairing()
    {
        Save("s1",
            new ChannelNote(At, "MISC Starlancer MAX", "nekron", null, ChannelMoment.YouBoarded),
            new ChannelNote(At, "Drake Corsair", "nekron", null, ChannelMoment.YouBoarded));

        Assert.Empty(_library.SharedShips());
    }

    /// <summary>A rename must not put an older self in the list beside friends.</summary>
    [Fact]
    public void Your_own_earlier_handle_is_not_somebody_you_flew_with()
    {
        _store.Save(
            new SessionSummary
            {
                Id = "old", SourceFile = "old.log", StartedAt = At.AddDays(-40),
                EndedAt = At.AddDays(-40).AddHours(1), Handle = "nekron-old",
                ChannelNotes = [],
            },
            "fingerprint:old");

        Save("s1", new ChannelNote(At, "Drake Corsair", "nekron", "nekron-old", ChannelMoment.TheyBoarded));

        Assert.Empty(_library.SharedShips());
    }

    [Fact]
    public void The_same_pilot_in_the_same_ship_is_one_row_with_a_count_and_a_span()
    {
        Save("s1",
            new ChannelNote(At, "Drake Cutlass Black", "Sylosis", null, ChannelMoment.YouBoarded),
            new ChannelNote(At.AddHours(2), "Drake Cutlass Black", "Sylosis", null, ChannelMoment.YouBoarded));
        Save("s2", new ChannelNote(At.AddDays(9), "Drake Cutlass Black", "Sylosis", null, ChannelMoment.YouBoarded));

        var shared = Assert.Single(_library.SharedShips());

        Assert.Equal(3, shared.Times);
        Assert.Equal(At, shared.First);
        Assert.Equal(At.AddDays(9), shared.Last);
    }

    [Fact]
    public void The_same_pilot_in_two_ships_is_two_rows()
    {
        Save("s1",
            new ChannelNote(At, "Drake Cutlass Black", "Sylosis", null, ChannelMoment.YouBoarded),
            new ChannelNote(At, "RSI Perseus", "Sylosis", null, ChannelMoment.YouBoarded));

        var shared = _library.SharedShips();

        Assert.Equal(2, shared.Count);
        Assert.All(shared, s => Assert.Equal("Sylosis", s.Handle));
    }

    /// <summary>
    /// A departure says somebody left, not that you were ever aboard together -
    /// the boarding line is the one that carries that.
    /// </summary>
    [Fact]
    public void A_departure_alone_is_not_a_pairing()
    {
        Save("s1", new ChannelNote(At, "Drake Cutlass Black", "Sylosis", "Vhailor-5", ChannelMoment.TheyLeft));

        Assert.Empty(_library.SharedShips());
    }

    [Fact]
    public void A_window_counts_only_what_falls_inside_it()
    {
        Save("old", new ChannelNote(At.AddDays(-90), "RSI Perseus", "Drafts-of-Singularity", null, ChannelMoment.YouBoarded));
        Save("new", new ChannelNote(DateTimeOffset.UtcNow.AddDays(-2), "Drake Corsair", "Sylosis", null, ChannelMoment.YouBoarded));

        var shared = Assert.Single(_library.SharedShips(7));
        Assert.Equal("Sylosis", shared.Handle);

        Assert.Equal(2, _library.SharedShips(0).Count);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _store.Dispose();
    }
}
