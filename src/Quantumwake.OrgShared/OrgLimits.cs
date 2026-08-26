namespace Quantumwake.OrgShared;

/// <summary>
/// Caps for what crosses the org wire, shared so the client's preview and the
/// server's validation cannot disagree about what fits.
/// </summary>
/// <remarks>
/// Two copies of a cap drift, and the one that drifts is never the one being
/// read carefully - the same reasoning that put <see cref="Sanitise"/> here.
/// The row caps for data classes arrive with their classes; the ones below
/// govern the spine.
/// </remarks>
public static class OrgLimits
{
    /// <summary>Any request body. Share uploads get their own, larger cap.</summary>
    public const int MaxBodyBytes = 1024 * 1024;

    /// <summary>A class share upload - far smaller than a full export file.</summary>
    public const int MaxShareBytes = 2 * 1024 * 1024;

    /// <summary>A link code lives this long; the flow is a human at a browser.</summary>
    public const int LinkCodeMinutes = 10;

    /// <summary>How often the desktop app may ask whether its code was approved.</summary>
    public const int LinkPollSeconds = 3;

    /// <summary>An invite may not be minted to outlive a season.</summary>
    public const int MaxInviteDays = 90;

    /// <summary>A Star Citizen handle; RSI itself stops well short of this.</summary>
    public const int Handle = 60;

    /// <summary>A member's complete blueprint snapshot.</summary>
    public const int MaxBlueprints = 5000;

    /// <summary>A blueprint name after trimming control characters.</summary>
    public const int BlueprintName = 240;
}
