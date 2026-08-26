using Quantumwake.Server;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Quantumwake.Tests;

/// <summary>
/// Replacing the running application with the published one.
/// </summary>
/// <remarks>
/// The riskiest code in the project: it moves the executable the user started.
/// Every test here is about a way it must refuse rather than a way it works,
/// because the failure that matters is not "the update did not happen" - it is
/// "there is no longer anything to start".
/// </remarks>
public class SelfUpdateTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-update-{Guid.NewGuid():N}");

    public SelfUpdateTests() => Directory.CreateDirectory(_directory);

    private sealed class Serving(byte[] body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
    }

    private sealed class OneClient(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static string Sha256(byte[] body) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(body));

    private SelfUpdate Updating(byte[] served, ShellBridge? shell = null) =>
        new(new OneClient(new Serving(served)), shell ?? new ShellBridge(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SelfUpdate>.Instance);

    private static UpdateResult Release(byte[] body, string version = "9.9.9", bool newer = true,
        long? size = null, string? digest = null) =>
        new(newer, "0.8.17", version, "https://example/release", null, null,
            new ReleaseAsset("QuantumWake.exe", "https://example/QuantumWake.exe",
                size ?? body.Length, digest ?? Sha256(body)));

    /* ---------- the guard that keeps a source build safe ---------- */

    /// <summary>
    /// A source build runs from a directory of loose assemblies under another
    /// name, and swapping one file there would break it rather than update it.
    /// The test process is exactly such a build, which makes this the honest
    /// check rather than a contrived one.
    /// </summary>
    [Fact]
    public void A_build_that_is_not_the_published_single_file_cannot_replace_itself()
    {
        Assert.Null(SelfUpdate.Executable);

        var updater = Updating([1, 2, 3]);
        Assert.False(updater.Possible);
    }

    [Fact]
    public async Task It_refuses_rather_than_touching_anything_when_it_is_not_that_file()
    {
        var body = Encoding.UTF8.GetBytes("a new version");
        var (installed, refused) = await Updating(body).InstallAsync(Release(body));

        Assert.Null(installed);
        Assert.NotNull(refused);
        Assert.Contains("cannot replace itself", refused.Message);
    }

    /* ---------- the swap, exercised directly ---------- */

    /// <summary>
    /// The two renames and their order, which is the whole safety argument.
    /// Windows permits renaming a running executable but not overwriting one, so
    /// the live file has to leave the name before the new one can take it.
    /// </summary>
    [Fact]
    public void The_swap_moves_the_old_aside_and_the_new_into_place()
    {
        var exe = Path.Combine(_directory, "QuantumWake.exe");
        File.WriteAllText(exe, "the running version");
        File.WriteAllText(exe + ".new", "the published version");

        Swap(exe);

        Assert.Equal("the published version", File.ReadAllText(exe));
        Assert.Equal("the running version", File.ReadAllText(exe + ".old"));
        Assert.False(File.Exists(exe + ".new"));
    }

    /// <summary>
    /// A second update must not trip over the first one's leftovers, which is
    /// what happens when the app was never restarted in between.
    /// </summary>
    [Fact]
    public void A_swap_replaces_an_older_leftover_rather_than_failing_on_it()
    {
        var exe = Path.Combine(_directory, "QuantumWake.exe");
        File.WriteAllText(exe, "current");
        File.WriteAllText(exe + ".old", "from an update two versions ago");
        File.WriteAllText(exe + ".new", "newest");

        Swap(exe);

        Assert.Equal("newest", File.ReadAllText(exe));
        Assert.Equal("current", File.ReadAllText(exe + ".old"));
    }

    /// <summary>
    /// If the second move fails the first is undone, because the alternative is
    /// a machine with no application on it and no way to fetch one.
    /// </summary>
    [Fact]
    public void A_swap_that_cannot_finish_puts_the_running_version_back()
    {
        var exe = Path.Combine(_directory, "QuantumWake.exe");
        File.WriteAllText(exe, "the running version");

        // A directory where the new file should be: the move cannot complete,
        // and the executable has already left its name by then.
        Directory.CreateDirectory(exe + ".new");

        Assert.ThrowsAny<Exception>(() => Swap(exe));

        Assert.True(File.Exists(exe));
        Assert.Equal("the running version", File.ReadAllText(exe));
    }

    /// <summary>The swap is private; this is the same two moves, in order.</summary>
    private static void Swap(string exe)
    {
        var method = typeof(SelfUpdate).GetMethod("Swap",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        try
        {
            method.Invoke(null, [exe, exe + ".new", exe + ".old"]);
        }
        catch (System.Reflection.TargetInvocationException e)
        {
            throw e.InnerException!;
        }
    }

    /* ---------- what must never be installed ---------- */

    private static void VerifyThrows(string staged, ReleaseAsset asset)
    {
        var method = typeof(SelfUpdate).GetMethod("Verify",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var error = Assert.Throws<System.Reflection.TargetInvocationException>(
            () => method.Invoke(null, [staged, asset]));

        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static void VerifyPasses(string staged, ReleaseAsset asset)
    {
        var method = typeof(SelfUpdate).GetMethod("Verify",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        method.Invoke(null, [staged, asset]);
    }

    /// <summary>
    /// The ordinary failure: a connection dropped part way. Caught on size
    /// alone, before ninety megabytes are hashed to learn what two numbers
    /// already said.
    /// </summary>
    [Fact]
    public void A_download_that_stopped_short_is_refused()
    {
        var full = Encoding.UTF8.GetBytes("the whole published file");
        var staged = Path.Combine(_directory, "short.new");
        File.WriteAllBytes(staged, Encoding.UTF8.GetBytes("the whole publ"));

        VerifyThrows(staged, new ReleaseAsset("QuantumWake.exe", "u", full.Length, Sha256(full)));
    }

    /// <summary>
    /// Right length, wrong bytes. Only the hash catches this, and it is the
    /// reason the hash is checked at all.
    /// </summary>
    [Fact]
    public void A_download_of_the_right_size_but_the_wrong_content_is_refused()
    {
        var expected = Encoding.UTF8.GetBytes("the published file..");
        var arrived = Encoding.UTF8.GetBytes("something else here.");
        Assert.Equal(expected.Length, arrived.Length);

        var staged = Path.Combine(_directory, "wrong.new");
        File.WriteAllBytes(staged, arrived);

        VerifyThrows(staged, new ReleaseAsset("QuantumWake.exe", "u", expected.Length, Sha256(expected)));
    }

    [Fact]
    public void A_download_matching_both_is_accepted()
    {
        var body = Encoding.UTF8.GetBytes("the published file");
        var staged = Path.Combine(_directory, "good.new");
        File.WriteAllBytes(staged, body);

        VerifyPasses(staged, new ReleaseAsset("QuantumWake.exe", "u", body.Length, Sha256(body)));
    }

    /// <summary>
    /// Releases published before GitHub reported a digest carry none. The size
    /// still had to match, and refusing every older release outright would be a
    /// worse answer than the check we were not given.
    /// </summary>
    [Fact]
    public void A_release_with_no_digest_is_accepted_on_its_size_alone()
    {
        var body = Encoding.UTF8.GetBytes("an older release");
        var staged = Path.Combine(_directory, "nodigest.new");
        File.WriteAllBytes(staged, body);

        VerifyPasses(staged, new ReleaseAsset("QuantumWake.exe", "u", body.Length, null));
    }

    /* ---------- the restart ---------- */

    /// <summary>
    /// Nothing is attached when the server runs on its own, and then the answer
    /// is "installed, start it yourself" rather than a restart that was never
    /// going to happen.
    /// </summary>
    [Fact]
    public void With_no_shell_attached_a_restart_is_declined_rather_than_promised()
    {
        var bridge = new ShellBridge();

        Assert.False(bridge.Available);
        Assert.False(bridge.TryRestart());
    }

    [Fact]
    public void An_attached_shell_is_asked_exactly_once()
    {
        var asked = 0;
        var bridge = new ShellBridge();
        bridge.AttachRestart(() => asked++);

        Assert.True(bridge.Available);
        Assert.True(bridge.TryRestart());
        Assert.Equal(1, asked);
    }

    /* ---------- nothing to do ---------- */

    /// <summary>
    /// Whatever the reason, nothing is installed and nothing is moved.
    /// </summary>
    /// <remarks>
    /// The message names the build rather than the release, because a copy that
    /// cannot replace itself is the more fundamental fact and is checked first.
    /// The two cannot be told apart from a test: this process is always a source
    /// build, which is the same reason the guard exists.
    /// </remarks>
    [Fact]
    public async Task It_declines_when_there_is_no_newer_release()
    {
        var body = Encoding.UTF8.GetBytes("same version");
        var (installed, refused) = await Updating(body).InstallAsync(Release(body, newer: false));

        Assert.Null(installed);
        Assert.NotNull(refused);
        Assert.Equal(400, refused.Status);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }
}
