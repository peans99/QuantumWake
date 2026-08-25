using Quantumwake.OrgServer.Auth;

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

    /// <summary>
    /// How people sign in. Null when no provider is configured - the server
    /// still runs, and the sign-in page says what is missing instead of
    /// erroring. Tests inject a fake here; production wires Discord from
    /// configuration. A seam rather than a flag, because a dev-mode flag ships
    /// in the binary and is one environment variable away from an open server.
    /// </summary>
    public IOAuthProvider? OAuth { get; init; }

    public static OrgServerOptions FromArguments(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables("OrgServer__")
            .AddCommandLine(args)
            .Build();

        var discordId = configuration["Discord:ClientId"];
        var discordSecret = configuration["Discord:ClientSecret"];

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
            OAuth = discordId is { Length: > 0 } && discordSecret is { Length: > 0 }
                ? new DiscordOAuth(discordId, discordSecret)
                : null,
        };
    }
}
