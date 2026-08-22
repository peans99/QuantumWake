using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Dating each patch from the logs, which is the evidence behind the app's one
/// question about wipes. Nothing here claims a wipe happened - only when a
/// version first appeared.
/// </summary>
public class PatchArrivalTests : IDisposable
{
    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;

    public PatchArrivalTests()
    {
        _library = new LogLibrary(_store);

        Save("a", "4.7.178.8917", new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero));
        Save("b", "4.7.178.50402", new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero));
        Save("c", "4.8.180.28520", new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero));
        Save("d", "4.8.180.31000", new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero));
        Save("e", "4.9.188.23497", new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero));
    }

    private void Save(string id, string? version, DateTimeOffset started) =>
        _store.Save(
            new SessionSummary
            {
                Id = id,
                SourceFile = $"{id}.log",
                StartedAt = started,
                EndedAt = started.AddHours(1),
                GameVersion = version,
            },
            $"fingerprint:{id}");

    /// <summary>
    /// 4.8.180 and 4.8.181 are the same patch to a player, and only the first
    /// of them is a date worth offering.
    /// </summary>
    [Fact]
    public void Each_patch_is_dated_by_its_first_session()
    {
        var arrivals = _library.PatchArrivals();

        Assert.Equal(["4.7", "4.8", "4.9"], arrivals.Select(a => a.Patch));
        Assert.Equal(new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero), arrivals[1].At);
    }

    [Fact]
    public void The_newest_patch_since_the_wipe_is_the_one_offered()
    {
        _library.Wipe = new Wipe(new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero), "Alpha 4.8");

        var offered = _library.PatchSinceWipe();

        Assert.Equal("4.9", offered?.Patch);
    }

    /// <summary>The patch that is already the line is not offered back.</summary>
    [Fact]
    public void The_patch_already_recorded_is_not_offered()
    {
        _library.Wipe = new Wipe(new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero), "Alpha 4.9");

        Assert.Null(_library.PatchSinceWipe());
    }

    [Fact]
    public void With_no_wipe_set_the_newest_patch_of_all_is_offered()
    {
        _library.Wipe = null;

        Assert.Equal("4.9", _library.PatchSinceWipe()?.Patch);
    }

    /// <summary>
    /// The line is drawn at midnight and the day's first session is not, so a
    /// wipe recorded from a patch must not immediately re-offer that patch.
    /// </summary>
    [Fact]
    public void A_patch_on_the_day_of_the_wipe_is_not_offered_again()
    {
        Save("f", "4.9.188.23497", new DateTimeOffset(2026, 7, 19, 9, 30, 0, TimeSpan.Zero));

        _library.Wipe = new Wipe(new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero), "Alpha 4.9");

        Assert.Null(_library.PatchSinceWipe());
    }

    [Fact]
    public void Sessions_with_no_version_are_not_a_patch()
    {
        Save("g", null, new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        Save("h", "not-a-version", new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(3, _library.PatchArrivals().Count);
    }

    /// <summary>
    /// Pre-wipe sessions are the ones this has to see: a patch is only worth
    /// offering because it landed after the line, and the line hides it from
    /// every other question.
    /// </summary>
    [Fact]
    public void Patches_are_dated_from_every_stored_session_not_the_counted_ones()
    {
        _library.Wipe = new Wipe(new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero), "Alpha 4.9");

        Assert.Equal(3, _library.PatchArrivals().Count);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _store.Dispose();
    }
}
