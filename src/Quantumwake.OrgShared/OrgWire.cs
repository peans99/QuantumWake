using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quantumwake.OrgShared;

/// <summary>
/// The org network's wire format: what the desktop app and an org server agree
/// on, byte for byte.
/// </summary>
/// <remarks>
/// <para>
/// Grown from <c>ExportDocument</c>'s doctrine rather than invented fresh:
/// camelCase because the export file already chose it for anything crossing a
/// machine boundary, an explicit format version so a build can refuse what it
/// cannot read instead of half-reading it, and every shared row carrying when
/// it was <em>observed</em> rather than when it was uploaded.
/// </para>
/// <para>
/// These records live in a project of their own because the two consumers
/// share nothing else: the org server must not inherit the log parser, and the
/// desktop app must not inherit a second web stack. One copy of the shape,
/// compiled into both, is what keeps a cap or a field from drifting.
/// </para>
/// </remarks>
public static class OrgWire
{
    /// <summary>
    /// Can this be read at all. A peer declaring a higher number is refused
    /// whole - the same rule <c>ExportDocument.FormatVersion</c> states for
    /// files, because guessing at half a format corrupts quietly.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// One options object owned here, used by both ends, so the wire cannot
    /// disagree with itself about casing or nulls.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

/* ---------- linking a desktop app to an account ---------- */

public sealed record OrgLinkStartRequest(string? ClientName, string? AppVersion);

/// <param name="DeviceSecret">
/// Never shown to the user and never sent to the browser. The code is visible
/// in a URL and can be read over a shoulder; the token is only ever released
/// to the holder of this secret, which never leaves the machine that asked.
/// </param>
public sealed record OrgLinkStartResponse(
    string Code, string DeviceSecret, string VerifyUrl,
    DateTimeOffset ExpiresAt, int PollSeconds);

public sealed record OrgLinkPollRequest(string? Code, string? DeviceSecret);

/// <param name="Status">pending | approved | denied | expired.</param>
/// <param name="Token">Present exactly once, on the first approved poll.</param>
public sealed record OrgLinkPollResponse(string Status, string? Token, OrgAccount? Account);

/* ---------- who you are, where you belong ---------- */

/// <param name="Handle">
/// The self-declared Star Citizen handle. Nothing verifies it in 0.9, and
/// <paramref name="HandleVerified"/> travels on the wire precisely so that no
/// reading client can forget to say so.
/// </param>
public sealed record OrgAccount(
    string Id, string? Handle, bool HandleVerified, string DisplayName, bool ServerAdmin);

public sealed record OrgMembershipRow(
    string Id, string Name, string Status, string Role, IReadOnlyList<string> Modules);

public sealed record OrgMeResponse(OrgAccount Account, IReadOnlyList<OrgMembershipRow> Orgs);

public sealed record OrgMemberRow(
    string Id, string? Handle, bool HandleVerified, string DisplayName, string Role,
    DateTimeOffset JoinedAt, bool AppLinked);

public sealed record OrgProviderInfo(string Key, string Name);

public sealed record OrgServerMetadata(
    string Version, int FormatVersion, bool LanMode,
    IReadOnlyList<OrgProviderInfo> Providers, IReadOnlyList<string> Capabilities);

/* ---------- blueprint sharing ---------- */

public sealed record OrgBlueprintUploadRow(DateTimeOffset ObservedAt, string Name);

public sealed record OrgBlueprintUpload(
    int FormatVersion, IReadOnlyList<OrgBlueprintUploadRow> Blueprints);

public sealed record OrgBlueprintRow(
    string AccountId, string? Handle, bool HandleVerified, string DisplayName,
    DateTimeOffset ObservedAt, string Name, DateTimeOffset SharedAt);

public sealed record OrgBlueprintReceipt(int Rows, DateTimeOffset SharedAt);

public sealed record OrgModuleRequest(bool Enabled);

public sealed record OrgAuditRow(
    long Id, string? OrgId, string? AccountId, string Action, string? Target, string? Detail,
    DateTimeOffset At);

/* ---------- orgs, invites, admin ---------- */

public sealed record OrgRegisterRequest(string? Name, string? Note);

public sealed record OrgJoinRequest(string? Code);

public sealed record OrgInviteRequest(int ExpiresInDays, int MaxUses);

public sealed record OrgInviteRow(
    string Code, DateTimeOffset ExpiresAt, int MaxUses, int Uses, bool Revoked);

public sealed record OrgSummary(
    string Id, string Name, string? Note, string Status,
    DateTimeOffset CreatedAt, string CreatedBy, int Members);

/// <summary>Every refusal on the wire is a sentence a page can show.</summary>
public sealed record OrgProblem(string Message);
