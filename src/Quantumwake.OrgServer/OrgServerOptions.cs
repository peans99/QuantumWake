using Quantumwake.OrgServer.Auth;
using System.Net;

namespace Quantumwake.OrgServer;

/// <summary>
/// Everything an org server instance is, held as instance state.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not the main server's <c>AppPaths</c> pattern: that one is a
/// process-wide static, and its own test fixture documents the consequence -
/// the server cannot be hosted twice in one process, so every endpoint test
/// queues behind a collection lock. This server is born multi-instance so its
/// tests can run in parallel and a machine can host two org spaces if it
/// wants to.
/// </para>
/// <para>
/// Configuration is arguments first, then <c>OrgServer__</c> environment
/// variables, then defaults in code. No configuration file ships - the same
/// promise the main server makes.
/// </para>
/// </remarks>
public sealed class OrgServerOptions
{
    /// <summary>Where org.db lives. The one setting with no default.</summary>
    public required string DataDirectory { get; init; }

    public int Port { get; init; } = 8321;

    /// <summary>
    /// Loopback unless told otherwise, the same safe-by-default posture as the
    /// main server's -Lan: opening a server to a network is a decision, not an
    /// accident of running it.
    /// </summary>
    public string Bind { get; init; } = "127.0.0.1";

    /// <summary>
    /// The address the outside world reaches this server at. Required for
    /// sign-in, because the OAuth redirect must be stated rather than guessed
    /// from a Host header somebody else chose.
    /// </summary>
    public string? PublicBaseUrl { get; init; }

    /// <summary>Provider subjects (Discord ids) that are server admins.</summary>
    public IReadOnlyList<string> Admins { get; init; } = [];

    /// <summary>
    /// SQLite journal mode. WAL where the filesystem is real; "delete" for
    /// SMB-backed storage such as Azure App Service's /home, where WAL's
    /// shared-memory files are not safe.
    /// </summary>
    public string Journal { get; init; } = "wal";

    /// <summary>Honour X-Forwarded-* from a TLS-terminating front.</summary>
    public bool BehindProxy { get; init; }

    /// <summary>Only these addresses may supply forwarded client/protocol headers.</summary>
    public IReadOnlyList<IPAddress> TrustedProxies { get; init; } = [];

    /// <summary>
    /// How people sign in - every provider this deployment configured, in the
    /// order the sign-in page offers them. Empty when none is configured: the
    /// server still runs, and the pages say what is missing instead of
    /// erroring. Tests inject a fake here; production wires the real ones from
    /// configuration. A seam rather than a flag, because a dev-mode flag ships
    /// in the binary and is one environment variable away from an open server.
    /// </summary>
    public IReadOnlyList<IOAuthProvider> OAuth { get; init; } = [];

    /// <summary>
    /// Everyone who can reach the port is the same signed-in person, and that
    /// person is a server admin. For a server on a home network that nobody
    /// outside it can reach, where three flatmates should not each need a
    /// Discord application to share a blueprint list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Off unless asked for, the same posture as the app's <c>-Lan</c>: opening
    /// a server is a decision, not an accident of running it. When it is on it
    /// wins outright - sign-in is refused rather than offered alongside, because
    /// two ways in would mean two identities for one person and a page that
    /// cannot say which one is sharing.
    /// </para>
    /// <para>
    /// This is the one mode the server cannot make safe, so it does the next
    /// best thing: it says so, in the log at startup and on a banner across
    /// every page, and it never becomes the default by omission.
    /// </para>
    /// </remarks>
    public bool LanMode { get; init; }

    /// <summary>The configured provider with that key, or null.</summary>
    public IOAuthProvider? Provider(string? key) =>
        key is { Length: > 0 }
            ? OAuth.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
            : null;

    public static OrgServerOptions FromArguments(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables("OrgServer__")
            .AddCommandLine(args)
            .Build();

        // Each provider appears only when its pair is present, so a server
        // offers exactly the buttons it can actually honour.
        var providers = new List<IOAuthProvider>();
        Add("Discord", (id, secret) => new DiscordOAuth(id, secret));
        Add("Google", (id, secret) => new GoogleOAuth(id, secret));
        Add("Microsoft", (id, secret) => new MicrosoftOAuth(
            id, secret, configuration["Microsoft:Tenant"] is { Length: > 0 } t ? t : "common"));

        void Add(string section, Func<string, string, IOAuthProvider> make)
        {
            if (configuration[$"{section}:ClientId"] is { Length: > 0 } id
                && configuration[$"{section}:ClientSecret"] is { Length: > 0 } secret)
            {
                providers.Add(make(id, secret));
            }
        }

        return new OrgServerOptions
        {
            DataDirectory = configuration["Data"]
                ?? Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "QuantumwakeOrg"),
            Port = configuration.GetValue("Port", 8321),
            Bind = configuration["Bind"] ?? "127.0.0.1",
            PublicBaseUrl = configuration["PublicBaseUrl"]?.TrimEnd('/'),
            Admins = (configuration["Admins"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Journal = configuration["Journal"] ?? "wal",
            BehindProxy = configuration.GetValue<bool>("BehindProxy"),
            TrustedProxies = (configuration["TrustedProxies"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(IPAddress.Parse).ToArray(),
            LanMode = configuration.GetValue<bool>("LanMode"),
            OAuth = providers,
        };
    }
}
