using System.Text;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// Reads a backup file, or says why it will not.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately stricter than <see cref="ImportReader"/> in one place and
/// looser in another, and both differences follow from whose file it is. A
/// share arrives from somebody else and every row is sanitised on the way in;
/// a backup is the pilot's own and is meant to come back exactly as it went
/// out, so rewriting a title on the way in would corrupt the thing being
/// recovered. What it will not do is guess: a file that is not a backup, or is
/// from a newer build, is refused whole.
/// </para>
/// <para>
/// The size and shape limits stay, because a malformed file is a malformed file
/// whoever wrote it, and this one is chosen by a file picker.
/// </para>
/// </remarks>
public static class BackupReader
{
    /// <summary>The deepest legitimate path: envelope, backup, array, record, list, item.</summary>
    private const int MaxDepth = 16;

    /// <summary>Reads a backup, or says why not.</summary>
    public static (ExportBackup? Contents, string? Hash, ImportProblem? Problem) Read(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (null, null, new ImportProblem("That file is empty."));

        if (Encoding.UTF8.GetByteCount(text) > ImportReader.MaxBytes)
            return (null, null, ImportReader.TooBig(Encoding.UTF8.GetByteCount(text)));

        ExportFile? document;

        try
        {
            var options = new JsonSerializerOptions(ExportDocument.Json) { MaxDepth = MaxDepth };
            document = JsonSerializer.Deserialize<ExportFile>(text, options);
        }
        catch (JsonException)
        {
            return (null, null, new ImportProblem("That file is not readable as JSON."));
        }

        if (document is null)
            return (null, null, new ImportProblem("That file is empty."));

        if (!string.Equals(document.Format, ExportDocument.Format, StringComparison.Ordinal))
            return (null, null, new ImportProblem("That is not a Quantum Wake file."));

        if (document.FormatVersion > ExportDocument.FormatVersion)
        {
            return (null, null, new ImportProblem(
                $"That backup was written by a newer Quantum Wake (format {document.FormatVersion}; "
                + $"this build reads {ExportDocument.FormatVersion}). Update, then try again."));
        }

        /*
         * What kind of file it is comes before what version it is, and that
         * order is the fix rather than an ordering preference. ContentVersion
         * counts the share format, which is already past the backup format's
         * number - so a share file written by this very build failed the
         * version check and told the reader to update an app that was already
         * current, while the message that would have explained the real problem
         * sat below and could never be reached.
         */
        if (document.Backup is not { } backup)
        {
            return (null, null, new ImportProblem(
                (document.Classes ?? []).Contains(ExportDocument.Authored)
                    ? "That is a shared export, not a backup. Bring it in from Settings → Import instead."
                    : "That file does not carry a backup."));
        }

        if (document.ContentVersion > BackupBuilder.Version)
        {
            return (null, null, new ImportProblem(
                "That backup holds things this build does not know how to put back. "
                + "Update Quantum Wake, then try again."));
        }

        // A file written before a store existed has no key for it, and JSON
        // leaves the list null rather than empty - so every reader downstream
        // would have to remember to check. Normalised once, here, because the
        // next store added will have the same problem and nobody will be
        // thinking about three-month-old backups when they add it.
        var whole = backup with
        {
            Jobs = backup.Jobs ?? [],
            Checklists = backup.Checklists ?? [],
            Trips = backup.Trips ?? [],
            MiningRuns = backup.MiningRuns ?? [],
            Notes = backup.Notes ?? [],
            Deleted = backup.Deleted ?? [],
            Kits = backup.Kits ?? [],
        };

        return (whole, RestorePlan.HashOf(text), null);
    }
}
