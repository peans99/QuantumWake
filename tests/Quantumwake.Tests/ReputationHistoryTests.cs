using System.Text.Json;
using System.Text.Json.Serialization;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The reputation history: kept apart from the bounded screen log, because a
/// trend is the point of it and three hundred readings is a few evenings.
/// </summary>
public sealed class ReputationHistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qw-rep-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static readonly DateTimeOffset T0 = new(2026, 9, 8, 19, 49, 0, TimeSpan.Zero);

    private static ScreenSighting Rep(string shot, DateTimeOffset at, string org = "RED WIND LINEHAUL") =>
        new(shot, at, ScreenKind.Reputation, $"reputation with {org}", [], null, null, null, null, [], 180,
            Reputation: new ReputationReading(org, "NEUTRAL", "MEMBER", [], "HAULING",
                [new RepRankCard("MEMBER", null, 0.54)], 0.54));

    private static ScreenSighting Map(string shot, DateTimeOffset at) =>
        new(shot, at, ScreenKind.Map, "the map", [], null, null, null, null, [], 170);

    [Fact]
    public void A_rep_reading_outlives_the_screen_logs_bound()
    {
        var store = new ScreenReadingStore(_root);
        store.Add(Rep("rep.jpg", T0));

        for (var i = 0; i < ScreenReadingStore.Keep + 5; i++)
            store.Add(Map($"map-{i}.jpg", T0.AddMinutes(i + 1)));

        Assert.False(store.Has("rep.jpg"));
        Assert.Equal("rep.jpg", Assert.Single(store.ReputationHistory()).Shot);

        // And from the file, on the next start.
        Assert.Equal("MEMBER", Assert.Single(new ScreenReadingStore(_root).ReputationHistory()).Reading.Rank);
    }

    /// <summary>
    /// Every install that read the Rep app before 0.18.22 has those readings
    /// only in the screen log; the first start after keeps them.
    /// </summary>
    [Fact]
    public void Readings_from_before_the_history_existed_are_taken_from_the_log()
    {
        Directory.CreateDirectory(_root);
        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        File.WriteAllText(Path.Combine(_root, "screen-readings.json"),
            JsonSerializer.Serialize(new[] { Rep("old.jpg", T0), Map("map.jpg", T0.AddMinutes(1)) }, options));

        var store = new ScreenReadingStore(_root);

        Assert.Equal("old.jpg", Assert.Single(store.ReputationHistory()).Shot);
        Assert.True(File.Exists(Path.Combine(_root, "screen-reputation.json")));
    }

    [Fact]
    public void A_reading_read_again_replaces_its_old_self_and_dismissing_it_carries_over()
    {
        var store = new ScreenReadingStore(_root);
        store.Add(Rep("rep.jpg", T0) with { Reputation = new ReputationReading("RED WIND LINEHAUL", "NEUTRAL", null, []) });
        store.Add(Rep("rep.jpg", T0));

        Assert.Equal("MEMBER", Assert.Single(store.ReputationHistory()).Reading.Rank);

        store.Dismiss("rep.jpg", true);
        Assert.True(Assert.Single(store.ReputationHistory()).Dismissed);
    }

    [Fact]
    public void A_shot_that_no_longer_reads_as_the_rep_app_leaves_the_history()
    {
        var store = new ScreenReadingStore(_root);
        store.Add(Rep("shot.jpg", T0));
        store.Add(Map("shot.jpg", T0));

        Assert.Empty(store.ReputationHistory());
    }
}
