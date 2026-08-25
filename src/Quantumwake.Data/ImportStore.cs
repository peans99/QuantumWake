using Quantumwake.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Quantumwake.OrgShared;

namespace Quantumwake.Data;

/// <summary>
/// One file, as it arrived.
/// </summary>
/// <param name="ImportedAt">When it landed here, which is not when it was written.</param>
/// <param name="Fingerprint">
/// A hash of the bytes, so the same file is recognised whatever it was renamed
/// to between one machine and the next.
/// </param>
/// <param name="ContentVersion">
/// Remembered for ever rather than compared once: a batch from a build that did
/// not record something can say so, instead of showing a blank that reads like
/// an answer.
/// </param>
/// <param name="Hidden">
/// Muted, not deleted. "Stop cluttering my page" and "erase this person's data"
/// are different intentions, and a page that only offers the second one gets
/// used for the first.
/// </param>
public sealed record ImportBatch(
    string Id,
    DateTimeOffset ImportedAt,
    DateTimeOffset ExportedAt,
    string? Handle,
    string? Note,
    string SourceName,
    string Fingerprint,
    int FormatVersion,
    int ContentVersion,
    string ProducerVersion,
    IReadOnlyList<string> Classes,
    ImportCounts Counts,
    ImportCounts Rejected,
    ImportCounts Truncated,
    ExportReceipts? Receipts = null,
    ExportBlueprints? Blueprints = null,
    ExportAuthored? Authored = null,
    bool Hidden = false)
{
    /// <summary>
    /// Whether this build can still make sense of what is inside.
    /// </summary>
    /// <remarks>
    /// The header - dates, handle, note, source, fingerprint, versions, counts -
    /// is strings and dates only, and must stay that way across every format
    /// version. It is what lets a batch this build can no longer read still
    /// describe itself instead of vanishing.
    /// </remarks>
    public bool Readable => FormatVersion <= ExportDocument.FormatVersion;
}

/// <summary>
/// The files other people have shared, kept apart from the pilot's own work.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here is ever merged into <c>jobs.json</c>, <c>checklists.json</c> or
/// <c>trips.json</c>. Those files are the user's own, and the whole point of a
/// batch is that removing it takes the import away and leaves the authoring
/// untouched. Copying an imported row into your own lists goes through the
/// ordinary authoring endpoint, deliberately, so it is a thing somebody chose.
/// </para>
/// <para>
/// Two things here break the pattern every other store follows, and both are on
/// purpose - see <c>Save</c> and <c>Load</c>.
/// </para>
/// </remarks>
public sealed class ImportStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private List<ImportBatch> _batches = [];

    /// <summary>Set when a file was found unreadable and moved aside, so a page can say so.</summary>
    public string? Quarantined { get; private set; }

    public ImportStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "imports.json");
        Load();
    }

    public IReadOnlyList<ImportBatch> All()
    {
        lock (_gate) return [.. _batches];
    }

    public ImportBatch? Find(string id)
    {
        lock (_gate) return _batches.FirstOrDefault(b => b.Id == id);
    }

    /// <summary>The batch this file was already read into, if it was.</summary>
    public ImportBatch? Matching(string fingerprint)
    {
        lock (_gate) return _batches.FirstOrDefault(b => b.Fingerprint == fingerprint);
    }

    public ImportBatch Add(ImportReading reading, string fingerprint, string? sourceName, DateTimeOffset now)
    {
        var document = reading.Document;

        var batch = new ImportBatch(
            NewId(),
            now,
            document.ExportedAt,
            document.Handle,
            document.Note,
            Sanitise.Clean(sourceName, "a file", 120),
            fingerprint,
            document.FormatVersion,
            document.ContentVersion,
            Sanitise.Clean(document.Producer?.Version, "unknown", 32),
            document.Classes,
            reading.Counts,
            reading.Rejected,
            reading.Truncated,
            document.Receipts,
            document.Blueprints,
            document.Authored);

        lock (_gate)
        {
            _batches.Add(batch);
            Save();
        }

        return batch;
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            if (_batches.RemoveAll(b => b.Id == id) == 0)
                return false;

            Save();
            return true;
        }
    }

    /// <summary>
    /// Drops one class out of a batch, keeping the batch itself.
    /// </summary>
    /// <remarks>
    /// An emptied batch is still the record that this file was read, and it
    /// still holds the fingerprint, so the same file is recognised if it comes
    /// round again rather than silently arriving twice.
    /// </remarks>
    public bool RemoveClass(string id, string @class)
    {
        lock (_gate)
        {
            var at = _batches.FindIndex(b => b.Id == id);
            if (at < 0) return false;

            var batch = _batches[at];

            var updated = @class switch
            {
                ExportDocument.Receipts => batch with { Receipts = null, Counts = batch.Counts with { Receipts = 0 } },
                ExportDocument.Blueprints => batch with { Blueprints = null, Counts = batch.Counts with { Blueprints = 0 } },
                ExportDocument.Authored => batch with
                {
                    Authored = null,
                    Counts = batch.Counts with { Jobs = 0, Checklists = 0, Trips = 0 },
                },
                _ => null,
            };

            if (updated is null) return false;

            _batches[at] = updated with
            {
                Classes = [.. updated.Classes.Where(c => c != @class)],
            };

            Save();
            return true;
        }
    }

    public bool ToggleHidden(string id)
    {
        lock (_gate)
        {
            var at = _batches.FindIndex(b => b.Id == id);
            if (at < 0) return false;

            _batches[at] = _batches[at] with { Hidden = !_batches[at].Hidden };
            Save();
            return true;
        }
    }

    public int Clear()
    {
        lock (_gate)
        {
            var count = _batches.Count;
            _batches = [];
            Save();
            return count;
        }
    }

    /// <summary>A hash of the bytes as they arrived, before anything reads them.</summary>
    public static string FingerprintOf(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Written to a temporary file and moved into place.
    /// </summary>
    /// <remarks>
    /// The only store here that bothers, and the reason is the difference in
    /// what is at stake. The others hold a handful of hand-typed jobs somebody
    /// could retype; this one can be megabytes and is the only copy of data from
    /// another machine. A torn write while removing one batch would take every
    /// other batch with it.
    /// </remarks>
    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_batches));
        File.Move(temporary, _path, overwrite: true);
    }

    /// <summary>
    /// Reads what is there, and keeps what it cannot read.
    /// </summary>
    /// <remarks>
    /// Every other store falls back to empty on a corrupt file, with a comment
    /// saying which way it is safe to be wrong. Here that direction is not safe:
    /// starting empty and then saving would erase every import irrecoverably,
    /// and the files they came from are on other people's machines. So the
    /// unreadable file is moved aside and said out loud instead.
    /// </remarks>
    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _batches = JsonSerializer.Deserialize<List<ImportBatch>>(File.ReadAllText(_path)) ?? [];
        }
        catch (JsonException)
        {
            _batches = [];

            try
            {
                var aside = $"{_path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
                File.Move(_path, aside, overwrite: true);
                Quarantined = Path.GetFileName(aside);
            }
            catch (IOException)
            {
                // Could not move it either; leaving it alone is still better
                // than overwriting it with nothing.
                Quarantined = Path.GetFileName(_path);
            }
        }
        catch (IOException)
        {
            _batches = [];
        }
    }
}
