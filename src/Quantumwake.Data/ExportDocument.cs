using Quantumwake.Core.State;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quantumwake.Data;

/// <summary>
/// The shape of a shared Quantum Wake file: what one pilot can hand another.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is the user's own — what their logs recorded and what they
/// typed. Deliberately absent: the UEX price matrix, which is a third party's
/// crowd-sourced data and not ours to move, and the crafting catalogue, which
/// <see cref="CommunityData"/> declines to redistribute for the same reason.
/// A blueprint in this file is a name and a date the game announced, not a recipe.
/// </para>
/// <para>
/// This is also the document the org network would send if it is ever built, so
/// it is written as a wire format rather than as a dump of the stores: camelCase,
/// explicit versions, and every class carrying when it was observed.
/// </para>
/// </remarks>
public static class ExportDocument
{
    /// <summary>The discriminator every file leads with, so a wrong file fails clearly.</summary>
    /// <remarks>
    /// A file picker accepts anything. Without this, feeding the importer an
    /// <c>overlay.json</c> produces a confusing complaint about a missing field
    /// instead of "this is not a Quantum Wake export".
    /// </remarks>
    public const string Format = "quantumwake.export";

    /// <summary>
    /// Can this file be read at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bump on a rename, a type change, or a restructure — anything that makes an
    /// older reader misunderstand rather than merely miss something. A file
    /// declaring a higher number than the reading build is refused whole, because
    /// the alternative is showing somebody half a document and calling it their
    /// friend's data.
    /// </para>
    /// <para>
    /// 1 — receipts, blueprints and authored work.
    /// </para>
    /// </remarks>
    public const int FormatVersion = 1;

    /// <summary>
    /// Is this file complete.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bump when a class gains a field. Older files still read; the batch keeps
    /// the number for ever, so a page can say "this came from a build that did
    /// not record resource ids" rather than showing a blank that looks like an
    /// answer. That is <c>PayloadVersion</c>'s lesson — see
    /// <see cref="SessionStore"/> — moved to a file boundary: the expensive
    /// failure is the one nothing goes red for.
    /// </para>
    /// <para>
    /// 1 — first release.
    /// </para>
    /// </remarks>
    public const int ContentVersion = 1;

    /// <summary>
    /// How a document is written and read, owned here rather than borrowed.
    /// </summary>
    /// <remarks>
    /// camelCase on purpose. The PascalCase in <c>jobs.json</c> and its siblings
    /// is not a decision anybody made — it is <c>JsonSerializer.Serialize</c>
    /// with no options — and an accident is not worth carrying across a machine
    /// boundary into a file people open in an editor. Reading is
    /// case-insensitive so a file from any build still loads.
    /// </remarks>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>The class names a document may declare, and the only ones a reader acts on.</summary>
    public const string Receipts = "receipts";
    public const string Blueprints = "blueprints";
    public const string Authored = "authored";
}

/// <summary>What wrote a file, so a reader can say where a surprise came from.</summary>
public sealed record ExportProducer(string App, string Version, string? Build = null);

/// <summary>
/// A shared file.
/// </summary>
/// <param name="Classes">
/// What the sender chose to include, listed rather than inferred from which keys
/// are present. "I exported receipts and the window was empty" and "I did not
/// share receipts" are different facts, and a reader that cannot tell them apart
/// reports somebody's privacy choice as a gap in their data.
/// </param>
/// <param name="ExportedAt">
/// When the file was written — for display only. Age is read from each class's
/// <c>ObservedTo</c> and from each row's own timestamp, because a file written
/// today can carry a price from March.
/// </param>
public sealed record ExportFile(
    string Format,
    int FormatVersion,
    int ContentVersion,
    DateTimeOffset ExportedAt,
    ExportProducer Producer,
    IReadOnlyList<string> Classes,
    string? Handle = null,
    string? Note = null,
    ExportReceipts? Receipts = null,
    ExportBlueprints? Blueprints = null,
    ExportAuthored? Authored = null);

/// <summary>
/// The user's own commodity trades.
/// </summary>
/// <param name="WipeAt">
/// The line the sender counts their history from. Without it, an empty block
/// from an active trader is unexplainable — a reader cannot tell a quiet week
/// from a wipe date set last month.
/// </param>
/// <param name="Caveats">
/// Stable keys, not sentences, so the reading build supplies the wording and a
/// later rewording does not have to reach back into files already sent. These
/// carry the app's own honesty forward: a receipt's place is inferred, and a
/// trade is what the kiosk was asked for rather than what it confirmed.
/// </param>
public sealed record ExportReceipts(
    int WindowDays,
    DateTimeOffset? ObservedFrom,
    DateTimeOffset? ObservedTo,
    IReadOnlyList<string> Caveats,
    IReadOnlyList<ExportReceiptRow> Rows,
    DateTimeOffset? WipeAt = null);

/// <param name="Commodity">
/// The sender's dataset's name for <paramref name="ResourceId"/>, null when they
/// had none. The id is the part the game wrote down, so a reader with a different
/// catalogue can name what the sender could not.
/// </param>
public sealed record ExportReceiptRow(
    DateTimeOffset At,
    bool IsSell,
    string Place,
    string? PlaceId,
    int Scu,
    decimal Amount,
    decimal UnitPrice,
    string? Mode = null,
    string? Commodity = null,
    string? ResourceId = null);

/// <summary>Blueprints the sender holds: what the game announced, and when.</summary>
public sealed record ExportBlueprints(
    DateTimeOffset? ObservedFrom,
    DateTimeOffset? ObservedTo,
    IReadOnlyList<string> Caveats,
    IReadOnlyList<ExportBlueprintRow> Rows);

public sealed record ExportBlueprintRow(DateTimeOffset At, string Name);

/// <summary>
/// The things the sender typed rather than the things their logs observed.
/// </summary>
/// <remarks>
/// Pinned and Tracked are deliberately not here. They are this machine's view
/// state, every one of them enforced-singular locally, so a file carrying one
/// would either fight the reader's own pin or be discarded on arrival. Done is
/// content and stays.
/// </remarks>
public sealed record ExportAuthored(
    DateTimeOffset? ObservedFrom,
    DateTimeOffset? ObservedTo,
    IReadOnlyList<Job> Jobs,
    IReadOnlyList<Checklist> Checklists,
    IReadOnlyList<Trip> Trips);

/// <summary>Caveat keys a receipts block can carry.</summary>
public static class ExportCaveats
{
    /// <summary>Place is back-tracked from the last arrival, not logged with the sale.</summary>
    public const string PlaceInferred = "place-inferred";

    /// <summary>The log records the request to trade, never the terminal's answer.</summary>
    public const string RequestedNotConfirmed = "requested-not-confirmed";

    /// <summary>History before the sender's wipe line is not in here at all.</summary>
    public const string AfterWipeLine = "after-wipe-line";

    /// <summary>A blueprint's date is the earliest sighting, not necessarily the grant.</summary>
    public const string EarliestSighting = "earliest-sighting";
}
