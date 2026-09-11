using Quantumwake.Core.Locations;
using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// What a copied location's "believed to be" rests on.
/// </summary>
/// <remarks>
/// The points plan said the system on a saved point "carries a confidence",
/// and the stored session keeps none on a visit. What it does keep is the
/// line that placed the pilot and when - so the grade the page can show is
/// that evidence: an arrival four minutes before the copy, or a quantum jump
/// forty minutes before it. These tests hold the two apart, and hold the
/// pilot's own word about the system above both.
/// </remarks>
public class PlaceBeliefTests : IDisposable
{
    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), $"qw-belief-{Guid.NewGuid():N}");

    private static readonly DateTimeOffset At = new(2026, 9, 9, 2, 0, 0, TimeSpan.Zero);

    public PlaceBeliefTests()
    {
        _library = new LogLibrary(_store);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        _store.Dispose();
        Directory.Delete(_dir, true);
    }

    private void Save(IReadOnlyList<LocationVisit> visits, IReadOnlyList<QuantumJump> jumps) =>
        _store.Save(
            new SessionSummary
            {
                Id = "s1",
                SourceFile = "s1.log",
                StartedAt = At.AddHours(-1),
                EndedAt = At.AddHours(1),
                Locations = visits,
                Jumps = jumps,
            },
            "fingerprint:s1");

    private static LocationVisit Arrived(DateTimeOffset at, string name = "Ruin Station") =>
        new(at, "RuinStation", name, "Pyro", null, LocationKind.Station);

    [Fact]
    public void An_arrival_before_the_moment_is_the_belief_and_says_when_it_was()
    {
        Save([Arrived(At.AddMinutes(-4))], []);

        var belief = new LibraryBeliefs(_library).Placed(At);

        Assert.NotNull(belief);
        Assert.Equal("Ruin Station", belief.Name);
        Assert.Equal("Pyro", belief.System);
        Assert.Equal(PlaceSignal.Arrival, belief.Signal);
        Assert.Equal(At.AddMinutes(-4), belief.SignalAt);
    }

    /// <summary>
    /// A jump after the last arrival is the newer word, and is marked as a jump
    /// - it says where the ship was going, not that it got there.
    /// </summary>
    [Fact]
    public void A_later_jump_outranks_the_arrival_and_is_called_a_jump()
    {
        Save([Arrived(At.AddMinutes(-40))], [new QuantumJump(At.AddMinutes(-10), "RuinStation", "Ruin Station", "Stanton1_Hurston", "Hurston")]);

        var belief = new LibraryBeliefs(_library).Placed(At);

        Assert.Equal(PlaceSignal.Jump, belief?.Signal);
        Assert.Equal("Hurston", belief?.Name);
        Assert.Equal(At.AddMinutes(-10), belief?.SignalAt);
    }

    [Fact]
    public void A_moment_no_session_covers_has_no_belief()
    {
        Save([Arrived(At.AddMinutes(-4))], []);

        Assert.Null(new LibraryBeliefs(_library).Placed(At.AddDays(3)));
    }

    /// <summary>
    /// The pilot can say which system a point is in - filling a blank or
    /// overriding the logs - and the point remembers it was the pilot who said
    /// so. A blank leaves the belief alone.
    /// </summary>
    [Fact]
    public void The_pilot_can_name_the_system_and_the_point_says_it_was_them()
    {
        var store = new ScreenReadingStore(_dir);
        store.AddClipboard(new ClipboardSighting(At, 1, 2, 3, 0.1, null, null));
        store.Pin(At);

        var unnamed = store.UpdatePin(At, null, null, null, "   ");
        Assert.Null(unnamed?.System);
        Assert.False(unnamed?.SystemByPilot);

        var named = store.UpdatePin(At, null, null, null, "Nyx");
        Assert.Equal("Nyx", named?.System);
        Assert.True(named?.SystemByPilot);

        // Another save with nothing said about the system keeps the pilot's word.
        var again = store.UpdatePin(At, "A name", "Mining", "a note");
        Assert.Equal("Nyx", again?.System);
        Assert.True(again?.SystemByPilot);

        Assert.True(Assert.Single(new ScreenReadingStore(_dir).Pinned()).SystemByPilot);
    }

    [Fact]
    public void A_pin_carries_what_its_belief_rested_on()
    {
        var store = new ScreenReadingStore(_dir);
        store.AddClipboard(new ClipboardSighting(At, 1, 2, 3, 0.1, "Ruin Station", "Pyro",
            BelievedBy: PlaceSignal.Jump, BelievedAt: At.AddMinutes(-10)));

        var pin = store.Pin(At);

        Assert.Equal(PlaceSignal.Jump, pin?.BelievedBy);
        Assert.Equal(At.AddMinutes(-10), pin?.BelievedAt);
    }
}
