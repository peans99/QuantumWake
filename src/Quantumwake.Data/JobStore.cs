using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>One line of a job: a thing to gather, and how much of it.</summary>
/// <param name="Unit">"SCU" for bulk materials, empty for counted items.</param>
public sealed record JobItem(string Name, double Needed, string Unit = "");

/// <summary>
/// A plan the player is working towards: a blueprint to craft, or a shopping
/// list of their own.
/// </summary>
/// <param name="Kind">"craft" or "list" - only the wording differs.</param>
/// <param name="Source">The blueprint this came from, when it came from one.</param>
/// <param name="Pinned">
/// Shown on the Now page - and so in the overlay, where a job is worth having
/// while actually flying.
/// </param>
/// <param name="Destination">
/// Where the player means to do this shopping, when they have decided. A list
/// is usually written with a place already in mind - the stop you are flying to
/// anyway - and saying so up front is different from being told the cheapest
/// counter afterwards: it makes the plan prefer that place and leaves the rest
/// as the exception. Null means "anywhere", which is the honest default.
/// </param>
/// <param name="DestinationId">The map's id for it, so a plan can draw the stop.</param>
public sealed record Job(
    string Id,
    string Title,
    string Kind,
    string? Source,
    DateTimeOffset CreatedAt,
    bool Done,
    IReadOnlyList<JobItem> Items,
    bool Pinned = false,
    string? Destination = null,
    string? DestinationId = null,
    DateTimeOffset? ModifiedAt = null,
    bool DestinationChosen = false) : IStamped<Job>
{
    /*
     * DestinationChosen: the pilot pointed this list somewhere themselves, on
     * Shopping. A destination the Garage proposed is a suggestion and the next
     * proposal may replace it; one the pilot chose is theirs, and a rewrite of
     * the list keeps it and offers the new proposal beside it instead.
     */
    public string StampId => Id;
    /// <remarks>Pinned is view state and does not travel - see <see cref="Trip.Bare"/>.</remarks>
    public Job Bare() => this with { ModifiedAt = null, Pinned = false };
    public Job Stamped(DateTimeOffset at) => this with { ModifiedAt = at };

    /// <summary>When this last changed, falling back to when it was written.</summary>
    /// <remarks>
    /// Optional so files written before this existed still load; a record that
    /// has never been edited answers with its creation date, which is true.
    /// A restore compares these to decide which side of a conflict is newer,
    /// so a missing one must read as old rather than as now.
    /// </remarks>
    public DateTimeOffset ChangedAt => ModifiedAt ?? CreatedAt;
}

/// <summary>The single current list and any obsolete automatic Garage lists it replaced.</summary>
/// <param name="DestinationKept">True when the list kept a destination the pilot had chosen rather than taking the proposal.</param>
public sealed record ListReplacement(Job Job, bool Created, IReadOnlyList<string> ConsolidatedIds, bool DestinationKept = false);

/// <summary>
/// The player's own plans, kept in a file beside the caches.
/// </summary>
/// <remarks>
/// This is the first thing in the app that is authored rather than observed:
/// everything else is derived from logs or downloaded, and can be rebuilt at
/// will. A job is the user's own work, so it lives in its own file, is never
/// touched by a rescan, and survives every cache wipe.
/// </remarks>
public sealed class JobStore
{
    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Marks what actually changed, so no mutator has to remember to.</summary>
    private readonly ChangeStamp<Job> _stamp = new(r => JsonSerializer.Serialize(r));
    private List<Job> _jobs = [];

    public JobStore(string? directory = null)
    {
        var folder = directory ?? AppPaths.Root;

        _path = Path.Combine(folder, "jobs.json");
        Load();
    }

    public IReadOnlyList<Job> All()
    {
        lock (_gate)
            return [.. _jobs];
    }

    public Job Add(
        string title,
        string kind,
        string? source,
        IReadOnlyList<JobItem> items,
        string? destination = null,
        string? destinationId = null)
    {
        var job = new Job(
            Guid.NewGuid().ToString("N")[..8],
            string.IsNullOrWhiteSpace(title) ? "Untitled job" : title.Trim(),
            kind == "craft" ? "craft" : "list",
            source,
            DateTimeOffset.UtcNow,
            Done: false,
            items,
            Pinned: false,
            Destination: string.IsNullOrWhiteSpace(destination) ? null : destination.Trim(),
            DestinationId: string.IsNullOrWhiteSpace(destinationId) ? null : destinationId.Trim());

        lock (_gate)
        {
            _jobs.Insert(0, job);
            Save();
        }

        return job;
    }

    /// <summary>
    /// Writes the current contents of one authored list, reusing its open
    /// source-and-title match rather than creating a second version of it.
    /// </summary>
    /// <remarks>
    /// A garage fit changes one component at a time. Treating every change as
    /// a new list makes a useful automatic shopping reminder look like the
    /// pilot wants the same component twice.
    /// </remarks>
    public ListReplacement ReplaceOpenList(
        string title,
        string source,
        IReadOnlyList<JobItem> items,
        string? destination = null,
        string? destinationId = null,
        IReadOnlySet<string>? supersededTitles = null)
    {
        lock (_gate)
        {
            var index = _jobs.FindIndex(j =>
                !j.Done
                && j.Kind == "list"
                && string.Equals(j.Source, source, StringComparison.Ordinal)
                && string.Equals(j.Title, title, StringComparison.Ordinal));
            var created = index < 0;

            if (created)
            {
                _jobs.Insert(0, new Job(
                    Guid.NewGuid().ToString("N")[..8],
                    title,
                    "list",
                    source,
                    DateTimeOffset.UtcNow,
                    Done: false,
                    [.. items],
                    Pinned: false,
                    Destination: string.IsNullOrWhiteSpace(destination) ? null : destination.Trim(),
                    DestinationId: string.IsNullOrWhiteSpace(destinationId) ? null : destinationId.Trim()));
                index = 0;
            }
            var kept = false;
            if (!created)
            {
                var existing = _jobs[index];
                kept = existing.DestinationChosen && !string.IsNullOrWhiteSpace(existing.Destination);
                _jobs[index] = existing with
                {
                    Items = [.. items],
                    Destination = kept ? existing.Destination : string.IsNullOrWhiteSpace(destination) ? null : destination.Trim(),
                    DestinationId = kept ? existing.DestinationId : string.IsNullOrWhiteSpace(destinationId) ? null : destinationId.Trim(),
                };
            }

            var consolidated = new List<string>();
            if (supersededTitles is { Count: > 0 })
            {
                for (var i = _jobs.Count - 1; i >= 0; i--)
                {
                    var candidate = _jobs[i];
                    if (i == index
                        || candidate.Done
                        || candidate.Kind != "list"
                        || !string.Equals(candidate.Source, source, StringComparison.Ordinal)
                        || !supersededTitles.Contains(candidate.Title))
                        continue;

                    _jobs.RemoveAt(i);
                    if (i < index) index--;
                    consolidated.Add(candidate.Id);
                }
            }

            Save();
            return new ListReplacement(_jobs[index], created, consolidated, kept);
        }
    }

    /// <summary>The open list a source keeps under a title, or null.</summary>
    public Job? OpenList(string source, string title)
    {
        lock (_gate)
        {
            return _jobs.FirstOrDefault(j =>
                !j.Done
                && j.Kind == "list"
                && string.Equals(j.Source, source, StringComparison.Ordinal)
                && string.Equals(j.Title, title, StringComparison.Ordinal));
        }
    }

    /// <summary>Points a list at a place, or at nowhere in particular.</summary>
    public bool SetDestination(string id, string? place, string? placeId)
    {
        lock (_gate)
        {
            var index = _jobs.FindIndex(j => j.Id == id);
            if (index < 0)
                return false;

            _jobs[index] = _jobs[index] with
            {
                Destination = string.IsNullOrWhiteSpace(place) ? null : place.Trim(),
                DestinationId = string.IsNullOrWhiteSpace(placeId) ? null : placeId.Trim(),
                // Pointing it somewhere is a choice; clearing it hands the
                // choice back to whatever proposes next.
                DestinationChosen = !string.IsNullOrWhiteSpace(place),
            };

            Save();
            return true;
        }
    }

    /// <summary>
    /// Adds one thing to the list the player is filling: the pinned job if
    /// there is one - the top one on Shopping when several are, which is the
    /// newest - else the newest open list, else a new "Shopping list".
    /// Returns the job it landed in, so the page can say where it went.
    /// </summary>
    public Job Collect(JobItem item)
    {
        lock (_gate)
        {
            var index = _jobs.FindIndex(j => j.Pinned && !j.Done);

            if (index < 0)
                index = _jobs.FindIndex(j => j.Kind == "list" && !j.Done);

            if (index < 0)
            {
                var created = new Job(
                    Guid.NewGuid().ToString("N")[..8],
                    "Shopping list",
                    "list",
                    null,
                    DateTimeOffset.UtcNow,
                    Done: false,
                    [item]);

                _jobs.Insert(0, created);
                Save();
                return created;
            }

            var job = _jobs[index];

            // Asking for the same thing twice adds up rather than repeating.
            var items = job.Items.ToList();
            var existing = items.FindIndex(i =>
                string.Equals(i.Name, item.Name, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
                items[existing] = items[existing] with { Needed = items[existing].Needed + item.Needed };
            else
                items.Add(item);

            _jobs[index] = job with { Items = items };
            Save();
            return _jobs[index];
        }
    }

    /// <summary>Flips a job between open and done. False when the id is unknown.</summary>
    public bool Toggle(string id)
    {
        lock (_gate)
        {
            var index = _jobs.FindIndex(j => j.Id == id);
            if (index < 0)
                return false;

            _jobs[index] = _jobs[index] with { Done = !_jobs[index].Done };
            Save();
            return true;
        }
    }

    /// <summary>
    /// Pins a job to the Now page, or takes it off. Several can be pinned:
    /// one run often buys for more than one list, and keeping only the last
    /// one pinned left the others to be remembered. The Now card keeps the
    /// glance by showing only what is missing, cut to a share per list.
    /// </summary>
    public bool TogglePin(string id)
    {
        lock (_gate)
        {
            var index = _jobs.FindIndex(j => j.Id == id);
            if (index < 0)
                return false;

            _jobs[index] = _jobs[index] with { Pinned = !_jobs[index].Pinned };
            Save();
            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            var removed = _jobs.RemoveAll(j => j.Id == id) > 0;
            if (removed)
                Save();

            return removed;
        }
    }

    /// <summary>
    /// Puts a record back exactly as given, replacing any with the same id.
    /// </summary>
    /// <remarks>
    /// For restoring a backup, and nothing else. Every other way in makes its
    /// own record so the store owns the id and the dates; this one deliberately
    /// does not, because a restore has to reproduce what was backed up rather
    /// than author something new that resembles it.
    /// </remarks>
    public void Put(Job job)
    {
        lock (_gate)
        {
            var index = _jobs.FindIndex(x => x.Id == job.Id);

            // View state is this machine's and the preview promises to leave it
            // alone, so a replacement keeps the pin or the tracking it lands on.
            // The file never carried them - a backup strips both on the way out -
            // so taking the record verbatim silently unpins whatever it replaced.
            if (index >= 0) _jobs[index] = job with { Pinned = _jobs[index].Pinned };
            else _jobs.Add(job);

            // The record keeps the change time it was backed up with.
            _stamp.Adopt(job);
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _jobs = JsonSerializer.Deserialize<List<Job>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // A corrupt file must not stop the app; the user starts with none.
            _jobs = [];
        }

        _stamp.Loaded(_jobs);
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _stamp.Apply(_jobs, DateTimeOffset.UtcNow);
            File.WriteAllText(_path, JsonSerializer.Serialize(_jobs));
    }
}
