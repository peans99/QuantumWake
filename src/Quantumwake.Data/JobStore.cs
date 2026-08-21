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
public sealed record Job(
    string Id,
    string Title,
    string Kind,
    string? Source,
    DateTimeOffset CreatedAt,
    bool Done,
    IReadOnlyList<JobItem> Items,
    bool Pinned = false);

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
    private List<Job> _jobs = [];

    public JobStore(string? directory = null)
    {
        var folder = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Quantumwake");

        _path = Path.Combine(folder, "jobs.json");
        Load();
    }

    public IReadOnlyList<Job> All()
    {
        lock (_gate)
            return [.. _jobs];
    }

    public Job Add(string title, string kind, string? source, IReadOnlyList<JobItem> items)
    {
        var job = new Job(
            Guid.NewGuid().ToString("N")[..8],
            string.IsNullOrWhiteSpace(title) ? "Untitled job" : title.Trim(),
            kind == "craft" ? "craft" : "list",
            source,
            DateTimeOffset.UtcNow,
            Done: false,
            items);

        lock (_gate)
        {
            _jobs.Insert(0, job);
            Save();
        }

        return job;
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
    /// Pins a job to the Now page. Only one at a time: the overlay is a
    /// glance, and two jobs there is a list nobody reads mid-flight.
    /// </summary>
    public bool TogglePin(string id)
    {
        lock (_gate)
        {
            var index = _jobs.FindIndex(j => j.Id == id);
            if (index < 0)
                return false;

            var pin = !_jobs[index].Pinned;

            for (var i = 0; i < _jobs.Count; i++)
                if (_jobs[i].Pinned)
                    _jobs[i] = _jobs[i] with { Pinned = false };

            _jobs[index] = _jobs[index] with { Pinned = pin };
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
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_jobs));
    }
}
