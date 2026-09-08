using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The rate trading makes money at.
/// </summary>
/// <remarks>
/// A goal store with nothing worth saving for in it is no goal. The rest of the
/// rate is arithmetic over stats that already exist, so what is worth pinning
/// here is the edge where the division stops meaning anything.
/// </remarks>
public class EarningsTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-goal-{Guid.NewGuid():N}");

    public EarningsTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void A_goal_survives_a_restart()
    {
        var goal = new Goal("Drake Corsair", 3_360_000, DateTimeOffset.UtcNow);
        new GoalStore(_directory).Save(goal);

        Assert.Equal("Drake Corsair", new GoalStore(_directory).Current?.Name);
    }

    /// <summary>
    /// Saving for nothing, or for something free, is not a goal - and must
    /// clear rather than store an entry no page can show sensibly.
    /// </summary>
    [Theory]
    [InlineData("", 3_360_000)]
    [InlineData("   ", 3_360_000)]
    [InlineData("Drake Corsair", 0)]
    [InlineData("Drake Corsair", -100)]
    public void Something_that_is_not_a_goal_is_not_stored(string name, decimal target)
    {
        var store = new GoalStore(_directory);

        Assert.Null(store.Save(new Goal(name, target, DateTimeOffset.UtcNow)));
        Assert.Null(store.Current);
    }

    [Fact]
    public void Clearing_a_goal_leaves_nothing_behind()
    {
        var store = new GoalStore(_directory);
        store.Save(new Goal("Drake Corsair", 3_360_000, DateTimeOffset.UtcNow));

        store.Save(null);

        Assert.Null(store.Current);
        Assert.Null(new GoalStore(_directory).Current);
    }

    /// <summary>
    /// The name is what somebody typed, so it is trimmed rather than stored with
    /// whatever whitespace came with it.
    /// </summary>
    [Fact]
    public void A_name_is_kept_as_it_would_be_read()
    {
        var stored = new GoalStore(_directory)
            .Save(new Goal("  Drake Corsair  ", 3_360_000, DateTimeOffset.UtcNow));

        Assert.Equal("Drake Corsair", stored?.Name);
    }
}
