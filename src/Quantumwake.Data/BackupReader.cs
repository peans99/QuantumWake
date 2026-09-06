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

        if (document.ContentVersion > BackupBuilder.Version)
        {
            return (null, null, new ImportProblem(
                "That backup holds things this build does not know how to put back. "
                + "Update Quantum Wake, then try again."));
        }

        // A share file and a backup are both valid documents; only one of them
        // can be restored, and saying so beats restoring three stores out of ten
        // and calling it done.
        if (document.Backup is not { } backup)
        {
            return (null, null, new ImportProblem(
                (document.Classes ?? []).Contains(ExportDocument.Authored)
                    ? "That is a shared export, not a backup. Bring it in from Settings → Import instead."
                    : "That file does not carry a backup."));
        }

        return (backup, RestorePlan.HashOf(text), null);
    }
}
