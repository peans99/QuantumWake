using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Whether this copy may look for a newer one, and whether what it found is
/// actually newer. The promise being kept is that nothing reaches the internet
/// until somebody says it may.
/// </summary>
public class UpdateStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-updates-{Guid.NewGuid():N}");

    private UpdateStore NewStore() => new(_directory);

    [Fact]
    public void Nobody_is_checked_up_on_until_they_agree()
    {
        var fresh = NewStore().Current;

        Assert.False(fresh.Asked);
        Assert.False(fresh.Automatic);
        Assert.Null(fresh.LastCheckedAt);
    }

    [Fact]
    public void Agreeing_is_remembered()
    {
        NewStore().Answer(automatic: true);

        var reopened = NewStore().Current;
        Assert.True(reopened.Asked);
        Assert.True(reopened.Automatic);
    }

    /// <summary>
    /// "No" is an answer. Asking again next launch is asking someone to keep
    /// saying no.
    /// </summary>
    [Fact]
    public void Refusing_still_counts_as_answered()
    {
        NewStore().Answer(automatic: false);

        var reopened = NewStore().Current;
        Assert.True(reopened.Asked);
        Assert.False(reopened.Automatic);
    }

    [Fact]
    public void A_check_records_when_and_what_it_saw()
    {
        var store = NewStore();
        store.Answer(automatic: true);

        store.Checked("0.6.0");

        var reopened = NewStore().Current;
        Assert.Equal("0.6.0", reopened.LastSeenVersion);
        Assert.NotNull(reopened.LastCheckedAt);
    }

    /// <summary>A check that found nothing must not erase what the last one saw.</summary>
    [Fact]
    public void A_check_that_found_nothing_keeps_the_last_version_seen()
    {
        var store = NewStore();
        store.Checked("0.6.0");
        store.Checked(null);

        Assert.Equal("0.6.0", store.Current.LastSeenVersion);
    }

    /// <summary>
    /// Being wrong in the safe direction: a file that cannot be read leaves the
    /// app asking, never checking on its own.
    /// </summary>
    [Fact]
    public void An_unreadable_file_asks_again_rather_than_assuming_yes()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "updates.json"), "{ not json");

        var loaded = NewStore().Current;
        Assert.False(loaded.Asked);
        Assert.False(loaded.Automatic);
    }

    /// <summary>The classic: as text, "0.10.0" sorts below "0.9.0".</summary>
    [Theory]
    [InlineData("0.9.0", "v0.10.0", true)]
    [InlineData("0.5.1", "v0.6.0", true)]
    [InlineData("0.5.1", "v0.5.1", false)]
    [InlineData("0.6.0", "v0.5.1", false)]
    [InlineData("0.5.1+abc1234", "v0.5.2", true)]
    [InlineData("1.0.0", "v1.0.1", true)]
    public void Newer_is_a_version_comparison_not_a_string_one(
        string current, string published, bool expected)
    {
        Assert.Equal(expected, UpdateStore.IsNewer(current, published));
    }

    /// <summary>
    /// A tag nobody can read is no news: offering an update on the strength of
    /// it is how someone downloads a nightly by accident.
    /// </summary>
    [Theory]
    [InlineData("0.5.1", "nightly")]
    [InlineData("0.5.1", "")]
    [InlineData("0.5.1", null)]
    [InlineData(null, "v9.9.9")]
    public void An_unreadable_version_is_no_news(string? current, string? published)
    {
        Assert.False(UpdateStore.IsNewer(current, published));
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
