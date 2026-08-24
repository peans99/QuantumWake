namespace Quantumwake.Server;

/// <summary>
/// What a machine other than this one may do, when the server is opened up.
/// </summary>
/// <remarks>
/// <para>
/// Binding to every interface binds the API with the dashboard, and neither has
/// a login. The purpose of <c>-Lan</c> is a tablet showing the dashboard, which
/// needs reads and the live feed and nothing else - so from off this machine,
/// that is all it gets.
/// </para>
/// <para>
/// The rule is stated by method rather than by a list of paths on purpose. A
/// deny-list of sensitive endpoints is a list somebody forgets to add to: the
/// endpoints that store UEX credentials, install StarStrings into the game
/// folder, move the wipe line and force a rescan are all POSTs, and so is
/// whatever gets added next week. Reads are the whitelist.
/// </para>
/// </remarks>
public static class LanGuard
{
    /// <summary>
    /// Whether a request from another machine may proceed.
    /// </summary>
    /// <param name="method">HTTP method, as written on the request.</param>
    /// <param name="path">Request path.</param>
    /// <remarks>
    /// The hub is allowed despite negotiating over POST: <c>LiveHub</c> declares
    /// no callable methods, so it only ever broadcasts outwards, and refusing it
    /// would take the live feed off the second screen - which is the feature.
    /// </remarks>
    public static bool AllowsFromElsewhere(string method, string path) =>
        IsRead(method) || IsHub(path);

    private static bool IsRead(string method) =>
        method.Equals("GET", StringComparison.OrdinalIgnoreCase)
        || method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
        || method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The SignalR endpoint, matched on whole segments.
    /// </summary>
    /// <remarks>
    /// StartsWith on the raw string would also admit "/hubbub" and anything else
    /// merely beginning those four characters, which is how a path allow-list
    /// turns into a hole.
    /// </remarks>
    private static bool IsHub(string path) =>
        path.Equals("/hub", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/hub/", StringComparison.OrdinalIgnoreCase);

    /// <summary>What to tell whoever was refused.</summary>
    public const string Refusal =
        "Quantum Wake is read-only over the network. Changes have to be made on "
        + "the machine running it.";
}
