using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>A link attached to a checklist line, kept as a reference rather than a copied snapshot.</summary>
public sealed record ChecklistAttachment(string Kind, string Label, string? Target = null, string? PlaceId = null);

/// <summary>One authored departure task. Nothing here is inferred from the game.</summary>
/// <param name="AddedAt">
/// When it was written down. Null on lines from before this was recorded, which
/// fall back to the list's own date - without it, "bought since you added this"
/// cannot be answered and a purchase from last month would tick today's line.
/// </param>
public sealed record ChecklistItem(
    string Id,
    string Text,
    DateTimeOffset? DueAt,
    string? Note,
    IReadOnlyList<ChecklistAttachment> Attachments,
    bool Done,
    DateTimeOffset? DoneAt,
    DateTimeOffset? AddedAt = null);

/// <summary>A reusable checklist, with at most one shown on Now at a time.</summary>
public sealed record Checklist(
    string Id,
    string Title,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ChecklistItem> Items,
    bool Pinned = false,
    DateTimeOffset? ModifiedAt = null) : IStamped<Checklist>
{
    public string StampId => Id;
    /// <remarks>Pinned is view state and does not travel - see <see cref="Trip.Bare"/>.</remarks>
    public Checklist Bare() => this with { ModifiedAt = null, Pinned = false };
    public Checklist Stamped(DateTimeOffset at) => this with { ModifiedAt = at };

    /// <summary>When this last changed - see <see cref="Job.ChangedAt"/>.</summary>
    public DateTimeOffset ChangedAt => ModifiedAt ?? CreatedAt;
}

/// <summary>
/// The pilot's own checklists, stored separately from log-derived facts.
/// </summary>
/// <remarks>
/// A browser-local list would disappear from the overlay's separate WebView
/// profile. Keeping this alongside jobs and trips makes the same list survive
/// restarts and appear in both places.
/// </remarks>
public sealed class ChecklistStore
{
    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Marks what actually changed, so no mutator has to remember to.</summary>
    private readonly ChangeStamp<Checklist> _stamp = new(r => JsonSerializer.Serialize(r));
    private List<Checklist> _lists = [];

    public ChecklistStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "checklists.json");
        Load();
    }

    public IReadOnlyList<Checklist> All()
    {
        lock (_gate) return [.. _lists];
    }

    public Checklist Add(string? title)
    {
        var list = new Checklist(NewId(), Clean(title, "Checklist"), DateTimeOffset.UtcNow, []);
        lock (_gate)
        {
            _lists.Insert(0, list);
            Save();
            return list;
        }
    }

    public Checklist? AddItem(string checklistId, string? text, DateTimeOffset? dueAt,
        string? note, IReadOnlyList<ChecklistAttachment>? attachments)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        lock (_gate)
        {
            var index = _lists.FindIndex(list => list.Id == checklistId);
            if (index < 0) return null;

            var item = new ChecklistItem(NewId(), Clean(text, "Task"), dueAt,
                CleanOptional(note), CleanAttachments(attachments), Done: false, DoneAt: null,
                AddedAt: DateTimeOffset.UtcNow);
            _lists[index] = _lists[index] with { Items = [.. _lists[index].Items, item] };
            Save();
            return _lists[index];
        }
    }

    public bool ToggleItem(string checklistId, string itemId)
    {
        lock (_gate)
        {
            var listIndex = _lists.FindIndex(list => list.Id == checklistId);
            if (listIndex < 0) return false;

            var items = _lists[listIndex].Items.ToList();
            var itemIndex = items.FindIndex(item => item.Id == itemId);
            if (itemIndex < 0) return false;

            var done = !items[itemIndex].Done;
            items[itemIndex] = items[itemIndex] with { Done = done, DoneAt = done ? DateTimeOffset.UtcNow : null };
            _lists[listIndex] = _lists[listIndex] with { Items = items };
            Save();
            return true;
        }
    }

    public bool TogglePin(string id)
    {
        lock (_gate)
        {
            var index = _lists.FindIndex(list => list.Id == id);
            if (index < 0) return false;

            var pin = !_lists[index].Pinned;
            for (var i = 0; i < _lists.Count; i++)
                if (_lists[i].Pinned) _lists[i] = _lists[i] with { Pinned = false };

            _lists[index] = _lists[index] with { Pinned = pin };
            Save();
            return true;
        }
    }

    public bool RemoveItem(string checklistId, string itemId)
    {
        lock (_gate)
        {
            var index = _lists.FindIndex(list => list.Id == checklistId);
            if (index < 0) return false;

            var items = _lists[index].Items.Where(item => item.Id != itemId).ToList();
            if (items.Count == _lists[index].Items.Count) return false;

            _lists[index] = _lists[index] with { Items = items };
            Save();
            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            var removed = _lists.RemoveAll(list => list.Id == id) > 0;
            if (removed) Save();
            return removed;
        }
    }

    private static IReadOnlyList<ChecklistAttachment> CleanAttachments(IReadOnlyList<ChecklistAttachment>? attachments) =>
        (attachments ?? [])
            .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Kind) && !string.IsNullOrWhiteSpace(a.Label))
            .Take(6)
            .Select(a => new ChecklistAttachment(Clean(a.Kind, "note"), Clean(a.Label, "Attachment"),
                CleanOptional(a.Target), CleanOptional(a.PlaceId)))
            .ToList();

    // Shared with the import path, which faces text somebody else wrote.
    private static string Clean(string? value, string fallback) => Sanitise.Clean(value, fallback);

    private static string? CleanOptional(string? value) => Sanitise.CleanOptional(value);

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Puts a record back exactly as given, replacing any with the same id.
    /// </summary>
    /// <remarks>
    /// For restoring a backup, and nothing else. Every other way in makes its
    /// own record so the store owns the id and the dates; this one deliberately
    /// does not, because a restore has to reproduce what was backed up rather
    /// than author something new that resembles it.
    /// </remarks>
    public void Put(Checklist list)
    {
        lock (_gate)
        {
            var index = _lists.FindIndex(x => x.Id == list.Id);

            // View state is this machine's and the preview promises to leave it
            // alone, so a replacement keeps the pin or the tracking it lands on.
            // The file never carried them - a backup strips both on the way out -
            // so taking the record verbatim silently unpins whatever it replaced.
            if (index >= 0) _lists[index] = list with { Pinned = _lists[index].Pinned };
            else _lists.Add(list);

            // The record keeps the change time it was backed up with.
            _stamp.Adopt(list);
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path)) _lists = JsonSerializer.Deserialize<List<Checklist>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _lists = [];
        }

        _stamp.Loaded(_lists);
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _stamp.Apply(_lists, DateTimeOffset.UtcNow);
            File.WriteAllText(_path, JsonSerializer.Serialize(_lists));
    }
}
