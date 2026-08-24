using Microsoft.AspNetCore.Builder;
using Quantumwake.Server;
using System.Text;
using System.Text.Json;

namespace Quantumwake.Tests;

/// <summary>
/// The real server, running in the test process.
/// </summary>
/// <remarks>
/// <para>
/// ServerHost.cs is 3,640 lines of endpoint that nothing reached until this
/// existed: the stores underneath were tested and the wiring above them was
/// not, so a route bound to the wrong store, a body record whose defaults were
/// backwards, or a status code nobody returned would all have shipped green.
/// </para>
/// <para>
/// Two arguments make it safe to run here. <c>--data</c> moves every cache,
/// store and digest into a temporary folder, so no test can see or touch the
/// data of whoever is running them. <c>--path</c> pointed at a folder that is
/// not a game install makes install resolution return nothing, which stops the
/// server finding the real Star Citizen directory by scanning the drives - the
/// one thing here that could write outside its own sandbox.
/// </para>
/// <para>
/// Port 0 lets the OS choose, so a suite never fights the copy of the app the
/// author has open on 31337.
/// </para>
/// <para>
/// One fixture per test class rather than one for the whole assembly: these
/// tests write to stores, and a job created by one class must not turn up in
/// another's count.
/// </para>
/// <para>
/// They must not run at the same time, though, which is why every class using
/// this is in <see cref="ServerCollection"/>. <c>AppPaths.Root</c> is a static
/// field - one app, one data folder - so a second server built in the same
/// process points every store constructed after it at the newer folder, and two
/// concurrent fixtures quietly share one directory. That is a true statement
/// about the app rather than an inconvenience of the tests: the server cannot be
/// hosted twice in one process, and nothing said so until this was written.
/// </para>
/// </remarks>
public class ServerUnderTest : IAsyncLifetime
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-endpoints-{Guid.NewGuid():N}");

    private WebApplication? _app;

    /// <summary>Where this server keeps everything, for a test that wants to look.</summary>
    public string DataDirectory => Path.Combine(_root, "data");

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(DataDirectory);

        // Deliberately not an install. GameInstallLocator.FromPath returns null
        // for it, and ResolveInstall returns that without falling back to a
        // drive scan, so the real game folder is never opened.
        var notAnInstall = Path.Combine(_root, "no-game");
        Directory.CreateDirectory(notAnInstall);

        _app = ServerHost.Build(["--data", DataDirectory, "--path", notAnInstall, "--Port=0"]);
        await _app.StartAsync();

        Client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    /// <summary>A GET, parsed, failing the test rather than returning nonsense.</summary>
    public async Task<JsonElement> Get(string url)
    {
        var response = await Client.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode,
            $"GET {url} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    public Task<HttpResponseMessage> Post(string url, object? body = null) =>
        Client.PostAsync(url, Json(body));

    public Task<HttpResponseMessage> Delete(string url) => Client.DeleteAsync(url);

    /// <summary>A POST expected to work, with its answer parsed.</summary>
    public async Task<JsonElement> Posted(string url, object? body = null)
    {
        var response = await Post(url, body);
        Assert.True(response.IsSuccessStatusCode,
            $"POST {url} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var text = await response.Content.ReadAsStringAsync();
        return text.Length == 0
            ? default
            : JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>The message an endpoint refused with, for asserting on the wording.</summary>
    public static async Task<string> Refusal(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync();

    private static StringContent? Json(object? body) =>
        body is null
            ? new StringContent("{}", Encoding.UTF8, "application/json")
            : new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    public async Task DisposeAsync()
    {
        Client?.Dispose();

        if (_app is not null)
            await _app.DisposeAsync();

        // The live log tailer holds handles briefly after shutdown; a locked
        // temp folder is not worth failing a green suite over.
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

/// <summary>
/// Endpoint classes share this so they run one at a time.
/// </summary>
/// <remarks>
/// Not for speed and not for tidiness: see ServerUnderTest for why two servers
/// in one process cannot both be trusted.
/// </remarks>
[CollectionDefinition("server", DisableParallelization = true)]
public class ServerCollection;
