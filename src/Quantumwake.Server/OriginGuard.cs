using System.Net;

namespace Quantumwake.Server;

/// <summary>
/// What a browser acting for another website may do here.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LanGuard"/> answers for other machines; this answers for the
/// browser on this one. A dashboard on localhost with no login trusts its
/// callers by *address*, and a browser will lend that address to any page it
/// is showing: a site the pilot happens to visit can fire POSTs at
/// 127.0.0.1 without being able to read the answers - which is enough to
/// clear stores, replace UEX credentials or start a ninety-megabyte update -
/// and a DNS name that re-resolves to 127.0.0.1 makes the attacker's page
/// same-origin with the API, at which point it can read everything too.
/// </para>
/// <para>
/// Two rules close both doors, and each is aimed at exactly one of them. The
/// <c>Host</c> header must name this machine, because a rebound request is a
/// browser convinced it is talking to <c>attacker.example</c> - the one thing
/// it cannot lie about is the name it thinks it resolved. And a state-changing
/// request that declares a foreign <c>Origin</c> is refused, because a browser
/// always declares who a cross-site POST was sent for; requests with no Origin
/// at all - curl, PowerShell, the app's own process - pass, since anything
/// already running code on this machine has no need of the API to cause harm.
/// </para>
/// <para>
/// "null" is a real Origin value - sandboxed iframes and file: pages send it -
/// and it belongs to nobody, so it is treated as foreign rather than as
/// absent. Reads with a foreign Origin are allowed through: the browser
/// withholds the response from the page unless CORS headers invite it, none
/// are served here, and refusing would only break tools that set Origin
/// honestly.
/// </para>
/// </remarks>
public static class OriginGuard
{
    /// <summary>
    /// Whether a request that arrived over loopback may proceed.
    /// </summary>
    /// <param name="method">HTTP method, as written on the request.</param>
    /// <param name="host">The Host header's host part, port already stripped.</param>
    /// <param name="origin">The Origin header, or null when the request carried none.</param>
    public static bool Allows(string method, string host, string? origin)
    {
        if (!NamesThisMachine(host))
            return false;

        if (IsRead(method) || origin is null)
            return true;

        return Uri.TryCreate(origin, UriKind.Absolute, out var declared)
               && NamesThisMachine(declared.Host);
    }

    /// <summary>
    /// Whether a name is this machine speaking to itself.
    /// </summary>
    /// <remarks>
    /// <c>localhost</c> and the loopback addresses, which covers every URL the
    /// app hands out and everything a person would type. An empty host is
    /// allowed: no browser omits Host, so nothing rebinding-shaped is lost,
    /// and refusing it would break only hand-rolled clients.
    /// </remarks>
    private static bool NamesThisMachine(string host) =>
        host.Length == 0
        || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));

    private static bool IsRead(string method) =>
        method.Equals("GET", StringComparison.OrdinalIgnoreCase)
        || method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
        || method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase);

    /// <summary>What to tell whoever was refused.</summary>
    public const string Refusal =
        "Quantum Wake only answers its own dashboard. This request said it was "
        + "made for another website, so it was not carried out.";
}
