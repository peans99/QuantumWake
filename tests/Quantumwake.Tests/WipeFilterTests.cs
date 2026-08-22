using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The wipe applied to the library: what every page is allowed to count.
/// </summary>
/// <remarks>
/// The filter lives in one accessor precisely so a view added later cannot
/// forget it, and these tests say what that accessor must do - including that
/// nothing is destroyed, since a wipe date moved back has to bring the whole
/// history straight back with it.
/// </remarks>
public class WipeFilterTests : IDisposable
{
    private static readonly DateTimeOffset Wiped =
        new(2026, 5, 15, 0, 0, 0, TimeSpan.Zero);

    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;

    public WipeFilterTests()
    {
        _library = new LogLibrary(_store);

        Save("before-1", Wiped.AddDays(-30));
        Save("before-2", Wiped.AddDays(-1));
        Save("after-1", Wiped.AddHours(2));
        Save("after-2", Wiped.AddDays(20));
    }

    private void Save(string id, DateTimeOffset started) =>
        _store.Save(
            new SessionSummary
            {
                Id = id,
                SourceFile = $"{id}.log",
                StartedAt = started,
                EndedAt = started.AddHours(2),
                Handle = "nekron",
            },
            $"fingerprint:{id}");

    [Fact]
    public void With_no_wipe_every_session_counts()
    {
        Assert.Equal(4, _library.Sessions().Count);
        Assert.Equal(0, _library.SessionsBeforeWipe());
    }

    [Fact]
    public void Sessions_from_before_the_wipe_are_not_counted()
    {
        _library.CountFrom = Wiped;

        Assert.Equal(2, _library.Sessions().Count);
        Assert.All(_library.Sessions(), s => Assert.True(s.StartedAt >= Wiped));
    }

    /// <summary>A session that began before the wipe and ran into it is a
    /// pre-wipe session: the account it describes ended part way through.</summary>
    [Fact]
    public void A_session_is_judged_by_when_it_started()
    {
        _library.CountFrom = Wiped;

        Save("straddles", Wiped.AddMinutes(-30));

        Assert.DoesNotContain(_library.Sessions(), s => s.Id == "straddles");
    }

    [Fact]
    public void The_page_can_say_how_much_is_being_held_back()
    {
        _library.CountFrom = Wiped;

        Assert.Equal(2, _library.SessionsBeforeWipe());
        Assert.Equal(4, _store.Count());
    }

    /// <summary>Nothing is deleted, so moving the line back restores the lot.</summary>
    [Fact]
    public void Moving_the_line_back_brings_the_history_back()
    {
        _library.CountFrom = Wiped;
        Assert.Equal(2, _library.Sessions().Count);

        _library.CountFrom = null;
        Assert.Equal(4, _library.Sessions().Count);
    }

    /// <summary>
    /// The totals every page reads come from the same accessor, so they move
    /// with it - this is the check that the filter is not merely on the list.
    /// </summary>
    [Fact]
    public void The_totals_move_with_the_line()
    {
        var all = _library.Stats().Sessions;

        _library.CountFrom = Wiped;
        var counted = _library.Stats().Sessions;

        Assert.Equal(4, all);
        Assert.Equal(2, counted);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _store.Dispose();
    }
}
