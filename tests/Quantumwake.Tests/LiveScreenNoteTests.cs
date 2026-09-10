using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// The screen note on the live snapshot: when it reaches the feed, and when it
/// stops being anybody's business.
/// </summary>
/// <remarks>
/// <para>
/// A screenshot is not something that happened in the game, so its note is kept
/// beside the session's timeline rather than in it and merged only when the
/// feed is built. That leaves two things easy to get wrong and invisible in a
/// diff, which is why they are pinned here rather than eyeballed.
/// </para>
/// <para>
/// Both go through <c>Snapshot()</c> directly instead of by running the
/// service. The background loop rebuilds every two seconds, so a test that
/// waited for one could not tell "the note reached the snapshot it was written
/// for" from "the note reached the one after it" - which is exactly the bug.
/// </para>
/// </remarks>
public class LiveScreenNoteTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "qw-live-screen-" + Guid.NewGuid().ToString("N"));

    private readonly SessionStore _store = new(":memory:");
    private readonly ScreenReadingStore _screen;
    private readonly LiveSessionService _live;

    public LiveScreenNoteTests()
    {
        _screen = new ScreenReadingStore(_dir);
        _live = Live();
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private LiveSessionService Live(ScreenSettingsStore? settings = null) =>
        new(new SilentHub(),
            new LogLibrary(_store),
            NullLogger<LiveSessionService>.Instance,
            screen: _screen,
            screenSettings: settings);

    private static readonly DateTimeOffset At = new(2026, 9, 10, 14, 0, 0, TimeSpan.Zero);

    /// <summary>A reading whose check says the screen and the logs disagree.</summary>
    private static ScreenSighting Differing(string shot, DateTimeOffset at) =>
        new(shot, at, ScreenKind.Loadout, "ARGO MOLE",
            [new ScreenCheck("Ship", "Argo MOLE", "Drake Cutlass Black", "differs", null)],
            null, null, null, null, [], 120);

    private static bool HasNote(NowState state) =>
        state.RecentEvents.Any(entry => entry.Kind == "screen-differs");

    /// <summary>
    /// The note is appended while the snapshot is being built, and the feed is
    /// built in the same pass. Reading the newest screenshot after the feed
    /// meant the note was always one snapshot late for the feed it belonged to -
    /// hidden by the two-second tick that followed, and waiting for whoever
    /// reordered those lines next.
    /// </summary>
    [Fact]
    public void The_note_is_in_the_same_snapshot_that_first_reports_the_reading()
    {
        _screen.Add(Differing("ScreenShot-a.jpg", At));

        var snapshot = _live.Snapshot();

        Assert.Equal("ScreenShot-a.jpg", snapshot.Screen?.Shot);
        Assert.True(HasNote(snapshot), "the reading is on the card but its note is not in the feed");
    }

    /// <summary>
    /// A reading that agrees updates the card and says nothing, because a pilot
    /// photographing a loadout takes several frames in a row.
    /// </summary>
    [Fact]
    public void A_reading_that_agrees_puts_nothing_in_the_feed()
    {
        _screen.Add(new ScreenSighting(
            "ScreenShot-a.jpg", At, ScreenKind.Loadout, "ARGO MOLE",
            [new ScreenCheck("Ship", "Argo MOLE", "Argo MOLE", "agrees", null)],
            null, null, null, null, [], 120));

        var snapshot = _live.Snapshot();

        Assert.NotNull(snapshot.Screen);
        Assert.False(HasNote(snapshot));
    }

    /// <summary>
    /// A rotation is the game being relaunched. The notes belong to the session
    /// that ended, and left in place they were the whole of the next session's
    /// feed - on top of a timeline correctly reporting that nothing had
    /// happened yet.
    /// </summary>
    [Fact]
    public void A_rotation_leaves_the_last_sessions_notes_behind()
    {
        _screen.Add(Differing("ScreenShot-a.jpg", At));
        Assert.True(HasNote(_live.Snapshot()));

        _live.OnRotated();

        Assert.False(HasNote(_live.Snapshot()));
    }

    /// <summary>
    /// The readings did not rotate, so the newest one is still the newest one.
    /// Clearing the last-seen shot alongside the notes would announce it a
    /// second time, which is the same mistake the constructor's seeding of that
    /// field exists to prevent.
    /// </summary>
    [Fact]
    public void A_rotation_does_not_announce_the_newest_reading_again()
    {
        _screen.Add(Differing("ScreenShot-a.jpg", At));
        _live.Snapshot();

        _live.OnRotated();

        var after = _live.Snapshot();

        Assert.Equal("ScreenShot-a.jpg", after.Screen?.Shot);
        Assert.False(HasNote(after));
    }

    /// <summary>A screenshot taken after the relaunch is news like any other.</summary>
    [Fact]
    public void A_reading_taken_after_a_rotation_is_still_announced()
    {
        _screen.Add(Differing("ScreenShot-a.jpg", At));
        _live.Snapshot();
        _live.OnRotated();

        _screen.Add(Differing("ScreenShot-b.jpg", At.AddMinutes(5)));

        var after = _live.Snapshot();

        Assert.Equal("ScreenShot-b.jpg", after.Screen?.Shot);
        Assert.True(HasNote(after));
    }

    /// <summary>
    /// Off however the reading arrives - the rule the scan and clipboard
    /// endpoints already enforced and the snapshot did not, so the last
    /// screenshot read stayed on the card and in the widget for good.
    /// </summary>
    [Theory]
    [InlineData(ScreenMode.Off)]
    [InlineData(ScreenMode.CopyOnly)]
    public void A_panel_that_is_switched_off_puts_nothing_on_the_card(ScreenMode mode)
    {
        var settings = new ScreenSettingsStore(_dir);
        settings.Save(mode, false, false);

        var live = Live(settings);
        _screen.Add(Differing("ScreenShot-a.jpg", At));

        var snapshot = live.Snapshot();

        Assert.Null(snapshot.Screen);
        Assert.False(HasNote(snapshot));
    }

    [Fact]
    public void Switching_it_back_on_puts_the_reading_back()
    {
        var settings = new ScreenSettingsStore(_dir);
        settings.Save(ScreenMode.Off, false, false);

        var live = Live(settings);
        _screen.Add(Differing("ScreenShot-a.jpg", At));
        Assert.Null(live.Snapshot().Screen);

        settings.Save(ScreenMode.Screenshots, false, true);

        Assert.Equal("ScreenShot-a.jpg", live.Snapshot().Screen?.Shot);
    }
}

/// <summary>A hub that goes nowhere.</summary>
/// <remarks>
/// Building a snapshot pushes nothing - the broadcast loops do that - so
/// nothing here is ever reached. It exists only because the service takes a hub
/// to be constructed at all, and standing up SignalR to obtain one would be a
/// great deal of machinery around two method calls.
/// </remarks>
file sealed class SilentHub : IHubContext<LiveHub>
{
    public IHubClients Clients => throw new NotSupportedException("these tests never broadcast");
    public IGroupManager Groups => throw new NotSupportedException("these tests never broadcast");
}
