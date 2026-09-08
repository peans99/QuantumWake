using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// One box across the catalogue, the logs and the reader's own work.
/// </summary>
/// <remarks>
/// The index is small and the matching is deliberately dull, so nothing here is
/// about relevance. It is about the two things a search can get wrong in a way
/// that matters: flattening sources of very different trust into one list, and
/// answering "nothing found" for a real item the reader simply has never seen -
/// which reads as a broken search rather than as an honest answer.
/// </remarks>
public class SearchTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-search-{Guid.NewGuid():N}");

    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;

    public SearchTests()
    {
        Directory.CreateDirectory(_root);
        _library = new LogLibrary(_store);
    }

    public void Dispose()
    {
        _store.Dispose();
        Directory.Delete(_root, true);
    }

    private static readonly DateTimeOffset At = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private SearchResults Find(string query) =>
        Search.Run(query, _library,
            new JobStore(_root), new ChecklistStore(_root), new TripStore(_root),
            new KitStore(_root), new MapNoteStore(_root));

    private static IReadOnlyList<SearchHit> From(SearchResults results, string source) =>
        results.Groups.Single(group => group.Source == source).Hits;

    [Fact]
    public void A_query_too_short_to_mean_anything_searches_nothing()
    {
        Assert.Empty(Find("P").Groups);
        Assert.Empty(Find(" ").Groups);
    }

    /// <summary>
    /// Sources stay apart. One ranked list would be the one place this app
    /// stopped saying which of its answers it can stand behind.
    /// </summary>
    [Fact]
    public void Results_are_grouped_by_where_they_came_from()
    {
        var groups = Find("armour").Groups.Select(group => group.Source).ToList();

        Assert.Equal([Search.Sources.Yours, Search.Sources.Authored, Search.Sources.Catalogue], groups);
    }

    [Fact]
    public void Your_own_written_work_is_found_by_its_lines_as_well_as_its_name()
    {
        new JobStore(_root).Add("Refit", "list", null, [new JobItem("Pembroke helmet", 1)]);

        var hit = Assert.Single(From(Find("Pembroke"), Search.Sources.Authored));

        Assert.Equal("job", hit.Kind);
        Assert.Equal("Refit", hit.Name);
    }

    [Fact]
    public void A_kit_is_found_by_what_is_in_it()
    {
        new KitStore(_root).Add("Bounty kit", [new KitItem("MedPen", 4)]);

        var hit = Assert.Single(From(Find("MedPen"), Search.Sources.Authored));

        Assert.Equal("kit", hit.Kind);
        Assert.Contains("1 item", hit.Why);
    }

    [Fact]
    public void A_run_is_found_by_where_it_stopped()
    {
        var trips = new TripStore(_root);
        trips.Add("Ore run", [new TripStop("s1", "Stanton1", "Hurston", null, false, null)]);

        var hit = Assert.Single(From(Find("Hurston"), Search.Sources.Authored));

        Assert.Equal("run", hit.Kind);
        Assert.Equal("Ore run", hit.Name);
    }

    /// <summary>
    /// A hit that has to be opened to find out why it is a hit turns a list of
    /// ten results into ten clicks.
    /// </summary>
    [Fact]
    public void Every_hit_says_why_it_is_one()
    {
        new KitStore(_root).Add("Bounty kit", [new KitItem("MedPen")]);
        new MapNoteStore(_root).Add("Stanton1", "Hurston", "Good rocks", null, ["mining"]);

        foreach (var hit in Find("MedPen").Groups.SelectMany(group => group.Hits))
            Assert.NotEmpty(hit.Why);

        var note = Assert.Single(From(Find("Good rocks"), Search.Sources.Authored));

        Assert.Equal("note", note.Kind);
        Assert.Contains("Hurston", note.Why);
    }

    /// <summary>
    /// Nothing matching is a real answer and has to be said, rather than drawn
    /// as an empty frame the reader has to interpret.
    /// </summary>
    [Fact]
    public void A_query_that_matches_nothing_says_so()
    {
        Assert.True(Find("nothing-is-called-this").Nothing);
        Assert.False(Find("nothing-is-called-this").Groups.Count == 0);
    }

    [Fact]
    public void Matching_ignores_case_and_matches_inside_a_name()
    {
        new KitStore(_root).Add("Bounty kit", []);

        Assert.Single(From(Find("BOUNTY"), Search.Sources.Authored));
        Assert.Single(From(Find("ounty"), Search.Sources.Authored));
    }

    /// <summary>
    /// One crowded source must not bury the others - a query matching four
    /// hundred catalogue items should still show the reader their own kit.
    /// </summary>
    [Fact]
    public void No_single_source_can_crowd_the_others_out()
    {
        var kits = new KitStore(_root);

        for (var i = 0; i < Search.MaxPerGroup + 5; i++)
            kits.Add($"Bounty kit {i}", []);

        Assert.Equal(Search.MaxPerGroup, From(Find("Bounty"), Search.Sources.Authored).Count);
    }

    /// <summary>
    /// A finished run reads differently from a plan somebody is still flying,
    /// because clicking one of them means something different.
    /// </summary>
    [Fact]
    public void A_finished_run_says_that_rather_than_counting_its_stops()
    {
        var trips = new TripStore(_root);
        var trip = trips.Add("Ore run", [new TripStop("s1", "Stanton1", "Hurston", null, false, null)]);
        trips.Start(trip.Id, At);
        trips.Finish(trip.Id, At.AddHours(2));

        Assert.Equal("a finished run", Assert.Single(From(Find("Ore run"), Search.Sources.Authored)).Why);
    }
}
