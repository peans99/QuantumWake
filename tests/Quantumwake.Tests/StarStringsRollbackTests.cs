using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Quantumwake.Core.Logging;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// What the game folder looks like when an install does not finish.
/// </summary>
/// <remarks>
/// This is the only code in the app that writes outside its own data folder, so
/// the interesting cases are all the ones where it stops halfway. A rollback
/// that misses a file leaves the player with a game that starts with missing
/// text and an app with no memory of having touched it.
/// </remarks>
public class StarStringsRollbackTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-ss-{Guid.NewGuid():N}");

    private string Game => Path.Combine(_root, "game");
    private string Data => Path.Combine(_root, "data");

    private string Ini => Path.Combine(Game, "Data", "Localization", "english", "global.ini");
    private string Cfg => Path.Combine(Game, "USER.cfg");

    public StarStringsRollbackTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Ini)!);
        Directory.CreateDirectory(Data);

        File.WriteAllText(Ini, "the game's own text");
        File.WriteAllText(Cfg, "the game's own config");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);

        GC.SuppressFinalize(this);
    }

    /// <summary>Serves the GitHub release JSON, then whatever archive is given.</summary>
    private sealed class Github(byte[] archive) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();

            HttpContent content = url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? new ByteArrayContent(archive)
                : new StringContent("""
                    {"name":"StarStrings 1.0","published_at":"2026-08-01T00:00:00Z",
                     "html_url":"https://example.invalid/release",
                     "assets":[{"name":"starstrings.zip",
                                "browser_download_url":"https://example.invalid/starstrings.zip"}]}
                    """, Encoding.UTF8, "application/json");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class OneClient(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>A well-formed archive holding both files the mod is allowed to write.</summary>
    private static byte[] GoodArchive()
    {
        using var buffer = new MemoryStream();

        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "Data/Localization/english/global.ini", "the mod's text");
            Write(zip, "USER.cfg", "the mod's config");
        }

        return buffer.ToArray();

        static void Write(ZipArchive zip, string name, string body)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(Encoding.UTF8.GetBytes(body));
        }
    }

    private StarStrings Mod(byte[] archive) =>
        new(new OneClient(new Github(archive)), new StarStringsStore(Data),
            NullLogger<StarStrings>.Instance);

    /// <summary>
    /// The manifest is the only record of what was displaced, so an install that
    /// cannot be written down cannot be taken out again either. Undoing it is
    /// the honest end: the alternative is a modded game folder the app has no
    /// memory of touching.
    /// </summary>
    [Fact]
    public async Task An_install_that_cannot_be_recorded_is_undone()
    {
        // The store writes starstrings.json into its folder. A directory of that
        // name in the way makes the write fail without touching anything else.
        var store = new StarStringsStore(Data);
        Directory.CreateDirectory(Path.Combine(Data, "starstrings.json"));

        var mod = new StarStrings(
            new OneClient(new Github(GoodArchive())), store, NullLogger<StarStrings>.Instance);

        var (install, problem) = await mod.InstallAsync(new GameInstall("LIVE", Game));

        Assert.Null(install);
        Assert.NotNull(problem);

        // The game is as it was, not as the mod left it.
        Assert.Equal("the game's own text", File.ReadAllText(Ini));
        Assert.Equal("the game's own config", File.ReadAllText(Cfg));
    }

    /// <summary>
    /// A clean install still has to work - the rollback paths must not be so
    /// eager that nothing ever lands.
    /// </summary>
    [Fact]
    public async Task A_good_archive_installs_both_files()
    {
        var mod = Mod(GoodArchive());

        var (install, problem) = await mod.InstallAsync(new GameInstall("LIVE", Game));

        Assert.Null(problem);
        Assert.NotNull(install);
        Assert.Equal("the mod's text", File.ReadAllText(Ini));
        Assert.Equal("the mod's config", File.ReadAllText(Cfg));

        // And it can be taken back out again, leaving the game as found.
        Assert.True(mod.Remove());
        Assert.Equal("the game's own text", File.ReadAllText(Ini));
        Assert.Equal("the game's own config", File.ReadAllText(Cfg));
    }

    /// <summary>
    /// Nothing in the archive may land if any entry is refused, and the check
    /// happens before a single byte is written.
    /// </summary>
    [Fact]
    public async Task An_archive_holding_anything_unexpected_writes_nothing()
    {
        using var buffer = new MemoryStream();

        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var stream = zip.CreateEntry("Bin64/StarCitizen.exe").Open();
            stream.Write("not text"u8);
        }

        var (install, problem) = await Mod(buffer.ToArray())
            .InstallAsync(new GameInstall("LIVE", Game));

        Assert.Null(install);
        Assert.Contains("does not expect", problem);
        Assert.Equal("the game's own text", File.ReadAllText(Ini));
        Assert.False(Directory.Exists(Path.Combine(Game, "Bin64")));
    }
}
