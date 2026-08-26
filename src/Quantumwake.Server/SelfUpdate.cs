using System.Security.Cryptography;

namespace Quantumwake.Server;

/// <summary>Why an update could not be applied, in the words the page shows.</summary>
public sealed record UpdateRefusal(string Message, int Status = 400);

/// <summary>What an install attempt did.</summary>
/// <param name="Restarting">
/// True when the shell agreed to restart. False means the swap is done and the
/// new version arrives when the app is next started by hand.
/// </param>
public sealed record UpdateInstalled(string Version, bool Restarting);

/// <summary>
/// Replaces the running application with the published one.
/// </summary>
/// <remarks>
/// <para>
/// Updating used to be six steps, two of which people get wrong: Windows warns
/// that an unsigned download may be malware, and the executable cannot be
/// overwritten while it is running, so it has to be quit first and replaced by
/// hand. This does all of it from one click.
/// </para>
/// <para>
/// It works because of two facts. The release is a single self-contained file -
/// the release workflow asserts exactly that - so there is one thing to swap.
/// And Windows lets a running executable be <em>renamed</em> even though it
/// refuses to overwrite or delete one, so the live file can be moved aside and
/// the new one put in its place while the app is still running from it.
/// </para>
/// <para>
/// A downloaded file is checked against the SHA-256 GitHub publishes for the
/// asset before anything is moved. That matters more here than anywhere else in
/// the app: a truncated download that replaced a working executable would leave
/// somebody with no way to start the thing that could fix it.
/// </para>
/// <para>
/// One quieter benefit, and the reason this is worth building rather than
/// buying a certificate. The "Windows protected your PC" screen comes from the
/// mark browsers write onto a file they downloaded; a file fetched by the app's
/// own HttpClient carries no such mark, so the replacement starts without it.
/// The warning is not suppressed - it never applies.
/// </para>
/// </remarks>
public sealed class SelfUpdate(IHttpClientFactory factory, ShellBridge shell, ILogger<SelfUpdate> logger)
{
    /// <summary>The published single file, and the only name this will replace.</summary>
    private const string ExeName = "QuantumWake.exe";

    private const string Staged = ".new";
    private const string Retired = ".old";

    /// <summary>
    /// Whether this build can replace itself at all.
    /// </summary>
    /// <remarks>
    /// A source build runs from a directory of loose assemblies under a
    /// different name, and swapping one file there would achieve nothing except
    /// breaking it. Offering the button in that case would be a promise the app
    /// cannot keep, so the page never sees it.
    /// </remarks>
    public bool Possible => Executable is not null;

    /// <summary>The running single file, or null when this is not one.</summary>
    public static string? Executable
    {
        get
        {
            var path = Environment.ProcessPath;

            return path is not null
                   && Path.GetFileName(path).Equals(ExeName, StringComparison.OrdinalIgnoreCase)
                ? path
                : null;
        }
    }

    /// <summary>
    /// Removes the file the last update moved aside.
    /// </summary>
    /// <remarks>
    /// Called on startup rather than after the swap, because at the moment of
    /// the swap the old file is still the one this process is running from and
    /// Windows will not delete it. A failure here is not worth reporting: the
    /// leftover is inert, and it will be tried again next time.
    /// </remarks>
    public static void TidyPreviousVersion()
    {
        if (Executable is not { } exe)
            return;

        try
        {
            if (File.Exists(exe + Retired))
                File.Delete(exe + Retired);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Downloads the published release and puts it in place.</summary>
    public async Task<(UpdateInstalled? Installed, UpdateRefusal? Refused)> InstallAsync(
        UpdateResult update, CancellationToken token = default)
    {
        if (Executable is not { } exe)
        {
            return (null, new UpdateRefusal(
                "This build cannot replace itself. Updating in place works from the "
                + "downloaded QuantumWake.exe, not from a source build."));
        }

        if (!update.Newer || update.Asset is null || update.Latest is null)
            return (null, new UpdateRefusal("There is no newer release to install."));

        // Asked before the download rather than after: forty megabytes fetched
        // into a folder that cannot be written to is forty megabytes wasted, and
        // the honest answer is available up front.
        if (!CanWriteBeside(exe))
        {
            return (null, new UpdateRefusal(
                $"Quantum Wake cannot write to {Path.GetDirectoryName(exe)}. Move it somewhere "
                + "it owns - a folder under your user account - or update by hand."));
        }

        var staged = exe + Staged;
        var retired = exe + Retired;

        try
        {
            await DownloadAsync(update.Asset, staged, token);
            Verify(staged, update.Asset);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException)
        {
            Discard(staged);
            logger.LogWarning(e, "Update download failed.");

            return (null, new UpdateRefusal(e is InvalidDataException
                ? "The download did not match what GitHub says it published, so nothing was "
                  + "replaced. Try again, or update by hand."
                : "The download did not finish, so nothing was replaced. Try again."));
        }

        try
        {
            Swap(exe, staged, retired);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Discard(staged);
            logger.LogError(e, "Update swap failed.");

            return (null, new UpdateRefusal(
                "The new version downloaded but could not be put in place, so the one you are "
                + "running is untouched. Something may be holding the file - antivirus, or a "
                + "second copy of Quantum Wake."));
        }

        logger.LogInformation("Updated to {Version}. Restarting.", update.Latest);

        return (new UpdateInstalled(update.Latest, shell.TryRestart()), null);
    }

    private async Task DownloadAsync(ReleaseAsset asset, string staged, CancellationToken token)
    {
        Discard(staged);

        using var client = factory.CreateClient("community");
        using var response = await client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(token);
        await using var target = File.Create(staged);
        await source.CopyToAsync(target, token);
    }

    /// <summary>
    /// Refuses anything that is not byte-for-byte what GitHub published.
    /// </summary>
    /// <remarks>
    /// The digest comes from the release API rather than from a file beside the
    /// asset, so there is nothing extra to publish and nothing to forget. Size
    /// is checked first because it is free and catches the ordinary failure - a
    /// connection dropped part way - without hashing ninety megabytes to learn
    /// what a comparison of two numbers already knew.
    /// </remarks>
    private static void Verify(string staged, ReleaseAsset asset)
    {
        var actual = new FileInfo(staged).Length;

        if (asset.Size > 0 && actual != asset.Size)
            throw new InvalidDataException($"expected {asset.Size} bytes, got {actual}");

        if (asset.Digest is not { Length: > 0 } expected)
        {
            // Older releases carry no digest. The size still had to match, and
            // refusing to update at all would be worse than the check GitHub
            // did not give us.
            return;
        }

        using var stream = File.OpenRead(staged);
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));

        var wanted = expected.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? expected["sha256:".Length..]
            : expected;

        if (!hash.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("sha256 mismatch");
    }

    /// <summary>
    /// Moves the live executable aside and the new one into its place.
    /// </summary>
    /// <remarks>
    /// Two renames, in this order, and the order is the whole safety argument.
    /// Windows permits renaming a running executable but not overwriting one, so
    /// the live file has to leave the name before the new file can take it. The
    /// gap between the two is a single move; if the second fails, the first is
    /// undone and the caller still has the application it started with.
    /// </remarks>
    private static void Swap(string exe, string staged, string retired)
    {
        if (File.Exists(retired))
            File.Delete(retired);

        File.Move(exe, retired);

        try
        {
            File.Move(staged, exe);
        }
        catch
        {
            File.Move(retired, exe);
            throw;
        }
    }

    private static bool CanWriteBeside(string exe)
    {
        var probe = exe + ".probe";

        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Discard(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
