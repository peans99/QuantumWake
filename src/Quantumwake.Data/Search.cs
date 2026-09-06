namespace Quantumwake.Data;

/// <summary>One thing a search found.</summary>
/// <param name="Kind">
/// What the entity drawer would need to describe it - "place", "ship", "part",
/// "commodity" - or an authored kind that opens its own page.
/// </param>
/// <param name="Why">
/// The matched context, in the reader's terms: "3 sightings in storage",
/// "bought twice". A hit that has to be opened to find out why it is a hit
/// makes a list of ten results into ten clicks.
/// </param>
public sealed record SearchHit(string Kind, string Id, string Name, string Why);

/// <summary>Hits from one source, kept apart from the others.</summary>
/// <param name="Source">
/// Where these came from, worded as the app words sources elsewhere. Grouping
/// rather than interleaving is deliberate: one query crosses a catalogue from
/// the game files, sightings from the reader's own logs and prices from a third
/// party, and a single ranked list would be the one place this app stopped
/// saying which is which.
/// </param>
public sealed record SearchGroup(string Source, IReadOnlyList<SearchHit> Hits);

/// <summary>What a query found, and what it looked through.</summary>
/// <param name="Nothing">
/// True when no group holds anything. Said outright so a page can word "nothing
/// matched" rather than drawing an empty frame.
/// </param>
public sealed record SearchResults(string Query, IReadOnlyList<SearchGroup> Groups)
{
    public bool Nothing => Groups.All(group => group.Hits.Count == 0);
}

/// <summary>
/// One box across the catalogue, the logs and the reader's own work.
/// </summary>
/// <remarks>
/// <para>
/// The index is a few thousand names held in memory, so the interesting part is
/// not speed. It is that a single query crosses sources of very different
/// trust, and flattening them into one ranked list would undo the labelling
/// this app does everywhere else.
/// </para>
/// <para>
/// A thing the reader has never seen still answers. Its catalogue entry comes
/// back with nothing under "yours", because absence from somebody's logs is a
/// fact worth showing - and a search that returns nothing reads as a search
/// that is broken.
/// </para>
/// </remarks>
public static class Search
{
    /// <summary>Sources, worded as <see cref="ExplainedRecord"/> and the entity cards word them.</summary>
    public static class Sources
    {
        public const string Yours = "your logs";
        public const string Authored = "things you wrote";
        public const string Catalogue = "the catalogue";
    }

    /// <summary>Enough to be worth a search; fewer is every item in the game.</summary>
    public const int MinQuery = 2;

    /// <summary>Per group, so one crowded source cannot bury the others.</summary>
    public const int MaxPerGroup = 10;

    public static SearchResults Run(
        string? query,
        LogLibrary library,
        JobStore jobs,
        ChecklistStore checklists,
        TripStore trips,
        KitStore kits,
        MapNoteStore notes)
    {
        var q = (query ?? string.Empty).Trim();

        if (q.Length < MinQuery)
            return new SearchResults(q, []);

        var stats = library.Stats();

        return new SearchResults(q, [
            new SearchGroup(Sources.Yours, [.. Yours(q, stats).Take(MaxPerGroup)]),
            new SearchGroup(Sources.Authored,
                [.. Authored(q, jobs, checklists, trips, kits, notes).Take(MaxPerGroup)]),
            new SearchGroup(Sources.Catalogue, [.. Catalogue(q, library).Take(MaxPerGroup)]),
        ]);
    }

    private static IEnumerable<SearchHit> Yours(string q, LibraryStats stats)
    {
        foreach (var place in stats.Locations.Where(p => Hits(p.Name, q)))
            yield return new SearchHit("place", place.RawId, place.Name, $"visited {Times(place.Visits)}");

        foreach (var ship in stats.Ships.Where(s => Hits(s.Name, q)))
            yield return new SearchHit("ship", ship.ClassName is { Length: > 0 } cls ? cls : ship.Name, ship.Name, $"flown on {Times(ship.Sorties)}");

        // Sightings are counted across places, because "three lockers have one"
        // is a different answer from "you have three".
        var seen = stats.Stash
            .SelectMany(place => place.Groups.SelectMany(group => group.Items)
                .Where(item => Hits(item.Name, q))
                .Select(item => (item.Name, place.Name)))
            .GroupBy(pair => pair.Item1, StringComparer.OrdinalIgnoreCase);

        foreach (var item in seen)
        {
            var places = item.Select(pair => pair.Item2).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            yield return new SearchHit("part", item.Key, item.Key,
                $"seen in storage at {places} place{(places == 1 ? "" : "s")}");
        }

        foreach (var worn in stats.Loadout
            .SelectMany(slot => slot.Items)
            .Where(item => Hits(item.Name, q))
            .DistinctBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            yield return new SearchHit("part", worn.Name, worn.Name, "on your character");
        }
    }

    private static IEnumerable<SearchHit> Authored(
        string q, JobStore jobs, ChecklistStore checklists, TripStore trips, KitStore kits, MapNoteStore notes)
    {
        foreach (var job in jobs.All().Where(j => Hits(j.Title, q) || j.Items.Any(i => Hits(i.Name, q))))
            yield return new SearchHit("job", job.Id, job.Title, $"{job.Items.Count} line{(job.Items.Count == 1 ? "" : "s")}");

        foreach (var list in checklists.All().Where(c => Hits(c.Title, q) || c.Items.Any(i => Hits(i.Text, q))))
            yield return new SearchHit("checklist", list.Id, list.Title, $"{list.Items.Count} item{(list.Items.Count == 1 ? "" : "s")}");

        foreach (var trip in trips.All().Where(t => Hits(t.Title, q) || t.Stops.Any(s => Hits(s.Place, q))))
        {
            yield return new SearchHit("run", trip.Id, trip.Title,
                trip.Archived != Archived.No ? "a finished run" : $"{trip.Stops.Count} stops");
        }

        foreach (var kit in kits.All().Where(k => Hits(k.Name, q) || k.Items.Any(i => Hits(i.Name, q))))
            yield return new SearchHit("kit", kit.Id, kit.Name, $"{kit.Items.Count} item{(kit.Items.Count == 1 ? "" : "s")}");

        foreach (var note in notes.All().Where(n => Hits(n.Title, q) || Hits(n.Note, q) || Hits(n.Place, q)))
            yield return new SearchHit("note", note.Id, note.Title, $"a note about {note.Place}");
    }

    /// <summary>
    /// What the game and the community know, whether or not it was ever seen.
    /// </summary>
    /// <remarks>
    /// The half that makes a search feel finished. Absence from somebody's logs
    /// is a fact worth showing rather than a reason to return nothing, and a
    /// search that answers "no results" for a real item reads as broken.
    /// </remarks>
    private static IEnumerable<SearchHit> Catalogue(string q, LogLibrary library)
    {
        foreach (var ship in library.Community.Ships.Values
            .Where(ship => Hits(ship.Name, q))
            .DistinctBy(ship => ship.Name, StringComparer.OrdinalIgnoreCase))
        {
            yield return new SearchHit("ship", ship.Name, ship.Name, "a ship the catalogue knows");
        }

        foreach (var item in library.Community.Items
            .Where(pair => Hits(pair.Key, q))
            .DistinctBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var kind = item.Value.Type is { Length: > 0 } type ? type : "gear";

            yield return new SearchHit("part", item.Key, item.Key, $"{kind} the catalogue knows");
        }
    }

    /// <summary>
    /// Substring, case-insensitive, and deliberately nothing cleverer.
    /// </summary>
    /// <remarks>
    /// Fuzzy matching sounds better and behaves worse here: engine ids and
    /// display names sit side by side in this index, and a scorer that ranks
    /// "P4-AR" near "P8-AR" is offering somebody the wrong rifle.
    /// </remarks>
    private static bool Hits(string? value, string q) =>
        value is not null && value.Contains(q, StringComparison.OrdinalIgnoreCase);

    private static string Times(int count) => count == 1 ? "once" : $"{count} times";
}
