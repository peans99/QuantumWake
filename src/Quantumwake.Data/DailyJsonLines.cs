using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// Records kept as JSON lines, a file a local day - the shape the fight and
/// rock records share. Appended, so a crash loses at most the line being
/// written, and a torn last line is skipped on reading.
/// </summary>
/// <param name="folder">Where the day files go.</param>
/// <param name="at">When a record happened, for filing and for reading back by time.</param>
public sealed class DailyJsonLines<T>(string folder, Func<T, DateTimeOffset> at) where T : class
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Lock _gate = new();

    public void Add(T record)
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, $"{at(record).ToLocalTime():yyyy-MM-dd}.jsonl");
                File.AppendAllText(path, JsonSerializer.Serialize(record, Json) + "\n");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A record not written is a debrief with a gap, not a reason to
                // stop reading the screen mid-fight or mid-rock.
            }
        }
    }

    /// <summary>Every record between two times, oldest first.</summary>
    public IReadOnlyList<T> Between(DateTimeOffset from, DateTimeOffset to)
    {
        var found = new List<T>();
        if (!Directory.Exists(folder) || to < from) return found;

        // Files are local days; a session after midnight UTC can be the day
        // before locally, so a day either side is read and filtered.
        for (var day = from.ToLocalTime().Date.AddDays(-1); day <= to.ToLocalTime().Date.AddDays(1); day = day.AddDays(1))
        {
            var path = Path.Combine(folder, $"{day:yyyy-MM-dd}.jsonl");
            if (!File.Exists(path)) continue;

            string[] lines;
            lock (_gate)
            {
                try { lines = File.ReadAllLines(path); }
                catch (IOException) { continue; }
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                T? record;
                try { record = JsonSerializer.Deserialize<T>(line, Json); }
                catch (JsonException) { continue; } // the half line a crash left
                if (record is null) continue;
                var when = at(record);
                if (when >= from && when <= to) found.Add(record);
            }
        }

        return [.. found.OrderBy(at)];
    }

    /// <summary>Whether anything has been kept at all.</summary>
    public bool Any() => Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.jsonl").Any();

    /// <summary>Deletes every day kept; returns how many went.</summary>
    public int Forget()
    {
        lock (_gate)
        {
            if (!Directory.Exists(folder)) return 0;
            var files = Directory.GetFiles(folder, "*.jsonl");
            foreach (var file in files) File.Delete(file);
            return files.Length;
        }
    }
}
