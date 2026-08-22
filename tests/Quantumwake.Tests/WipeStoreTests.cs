using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The line a wipe draws under the history. Getting this wrong either hides
/// the player's real history or counts an account they no longer have, so the
/// rules are worth pinning.
/// </summary>
public class WipeStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-wipe-{Guid.NewGuid():N}");

    private WipeStore NewStore() => new(_directory);

    [Fact]
    public void The_default_is_the_last_wipe_we_know_of()
    {
        var wipe = NewStore().Current;

        Assert.Equal(WipeStore.Default.At, wipe.At);
        Assert.Equal("Alpha 4.8", wipe.Patch);
    }

    [Fact]
    public void A_date_can_be_moved_and_survives_a_restart()
    {
        var moved = new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

        NewStore().Set(moved, "Alpha 4.9");

        var reopened = NewStore().Current;
        Assert.Equal(moved, reopened.At);
        Assert.Equal("Alpha 4.9", reopened.Patch);
    }

    /// <summary>Counting everything is a real answer, and has to persist too.</summary>
    [Fact]
    public void Clearing_the_date_counts_everything()
    {
        NewStore().Set(null, null);

        var reopened = NewStore().Current;
        Assert.Equal(DateTimeOffset.MinValue, reopened.At);
        Assert.Equal("no wipe", reopened.Patch);
    }

    /// <summary>
    /// A wipe in the future hides every session ever played and leaves a
    /// dashboard of zeroes with nothing to explain it.
    /// </summary>
    [Fact]
    public void A_date_in_the_future_is_refused()
    {
        var store = NewStore();
        var before = store.Current.At;

        var after = store.Set(DateTimeOffset.UtcNow.AddDays(30), "Alpha 5.0");

        Assert.Equal(before, after.At);
    }

    [Fact]
    public void A_wipe_with_no_patch_named_says_where_it_came_from()
    {
        Assert.Equal("set by hand", NewStore().Set(DateTimeOffset.UtcNow.AddDays(-1), "   ").Patch);
    }

    /// <summary>
    /// Showing pre-wipe history as if it counted is the worse failure, so a
    /// file that cannot be read falls back to the known wipe, not to none.
    /// </summary>
    [Fact]
    public void An_unreadable_file_falls_back_to_the_known_wipe()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "wipe.json"), "{ not json");

        Assert.Equal(WipeStore.Default.At, NewStore().Current.At);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test run over.
        }
    }
}
