using System.Security.Cryptography;
using System.Text;

namespace Quantumwake.Data;

/// <summary>What a restore would do with one record.</summary>
public enum RestoreAction
{
    /// <summary>Not on this machine, and not deleted here. Comes back by default.</summary>
    Add,

    /// <summary>Here and older than the file's. Taken by default.</summary>
    Replace,

    /// <summary>Here and not older. Kept by default, because yours is the newer work.</summary>
    Conflict,

    /// <summary>You deleted this. Left out by default - see <see cref="TombstoneStore"/>.</summary>
    Deleted,

    /// <summary>Byte for byte what you already have. Nothing to do.</summary>
    Same,
}

/// <summary>One line of a restore preview.</summary>
/// <param name="Label">
/// What the record calls itself, so the preview names things rather than ids.
/// A person deciding whether to overwrite "Ore run 3" cannot do it from
/// <c>a3f19c22</c>.
/// </param>
public sealed record RestoreLine(
    string Store,
    string Id,
    string Label,
    RestoreAction Action,
    DateTimeOffset? Yours,
    DateTimeOffset? Theirs)
{
    /// <summary>Store and id together, since ids only mean anything inside a store.</summary>
    public string Key => $"{Store}:{Id}";

    /// <summary>What happens if the reader approves the plan untouched.</summary>
    public bool TakenByDefault => Action is RestoreAction.Add or RestoreAction.Replace;
}

/// <summary>
/// What restoring a particular file would change, worked out before anything is
/// written.
/// </summary>
/// <param name="Hash">
/// Of the file this was computed from. The apply step demands it back, so a
/// plan approved for one file cannot be applied to another - the preview is
/// only a promise if the thing it described is the thing that runs.
/// </param>
/// <param name="Ignored">
/// Fields deliberately not restored: which job is pinned, which plan is
/// tracked. Counted rather than listed, and never silent - "nothing happened
/// to my pins" is a question somebody will ask.
/// </param>
public sealed record RestorePlan(
    string Hash,
    IReadOnlyList<RestoreLine> Lines,
    int Ignored)
{
    public int Adds => Lines.Count(l => l.Action == RestoreAction.Add);
    public int Replaces => Lines.Count(l => l.Action == RestoreAction.Replace);
    public int Conflicts => Lines.Count(l => l.Action == RestoreAction.Conflict);
    public int Deleted => Lines.Count(l => l.Action == RestoreAction.Deleted);
    public int Unchanged => Lines.Count(l => l.Action == RestoreAction.Same);

    /// <summary>Whether approving this as it stands would change anything at all.</summary>
    public bool ChangesAnything => Lines.Any(l => l.TakenByDefault);

    /// <summary>A stable fingerprint of a backup file's text.</summary>
    /// <remarks>
    /// Of the bytes rather than of the parsed contents: what the reader
    /// approved was a file, and re-serialising to hash it would let two
    /// different files agree.
    /// </remarks>
    public static string HashOf(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
}

/// <summary>Which lines the reader chose to take, when they did not take the defaults.</summary>
/// <param name="Take">Keys to apply that would not have been applied by default.</param>
/// <param name="Leave">Keys to skip that would have been applied by default.</param>
public sealed record RestoreChoices(
    IReadOnlyList<string>? Take = null,
    IReadOnlyList<string>? Leave = null)
{
    /// <summary>Whether one line runs, given the plan's default and this choice.</summary>
    /// <remarks>
    /// Expressed as two exception lists rather than a decision per line, so a
    /// reader who approves a hundred-line plan untouched sends nothing at all -
    /// and so a line added by a newer build cannot arrive with no answer and be
    /// read as "no".
    /// </remarks>
    public bool Runs(RestoreLine line) =>
        (Take ?? []).Contains(line.Key) || (line.TakenByDefault && !(Leave ?? []).Contains(line.Key));
}

/// <summary>How much a restore actually did.</summary>
public sealed record RestoreResult(
    int Restored,
    int Skipped,
    int Ignored);
