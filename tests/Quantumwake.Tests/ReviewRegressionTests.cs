using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Quantumwake.Core.GameData;
using Quantumwake.Core.Logging;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// The four faults found reviewing dev090, each held down by the case that
/// showed it.
/// </summary>
/// <remarks>
/// Kept in one file rather than spread into the suites for the code they cover,
/// because what they have in common is how they were found: every one of them
/// was green under the existing tests and wrong in front of a user. Two are
/// arithmetic that looked plausible, and two are file-layer orderings that only
/// go wrong when both text mods are involved.
/// </remarks>
public class ReviewRegressionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "qw-review-" + Guid.NewGuid().ToString("N"));
    private readonly SessionStore sessions = new(":memory:");
    public ReviewRegressionTests() => Directory.CreateDirectory(root);
    public void Dispose() { sessions.Dispose(); Directory.Delete(root, true); }

    private UexData Prices()
    {
        File.WriteAllText(Path.Combine(root, "prices.json"), """{"Copper":{"BestSell":1000,"BestSellTerminal":"Buyer","BestBuy":900,"BestBuyTerminal":"Seller","Terminals":3}}""");
        File.WriteAllText(Path.Combine(root, "commodity-ids.json"), "{}");
        File.WriteAllText(Path.Combine(root, "terminals.json"), "[]");
        File.WriteAllText(Path.Combine(root, "matrix.json"), """{"Copper":[{"TerminalId":1,"Terminal":"Seller","Buy":900,"Sell":0},{"TerminalId":2,"Terminal":"Buyer","Buy":0,"Sell":1000},{"TerminalId":3,"Terminal":"Second buyer","Buy":0,"Sell":1000}]}""");
        return new UexData(root);
    }

    [Fact]
    public void Commodity_card_sends_sellers_to_buying_terminals()
    {
        using var library = new LogLibrary(sessions);
        var card = EntityCards.Build("commodity", "Copper", library, Prices(), new UexFeeds(root))!;
        Assert.Equal("2 buy it from you, 1 sell it", card.Facts.Single(f => f.Label == "Counters").Value);
        Assert.Contains(card.Places, p => p.Name == "Buyer");
        Assert.DoesNotContain(card.Places, p => p.Name == "Seller");
    }

    [Fact]
    public void Mining_ranking_weights_each_deposit_before_combining_the_ore()
    {
        var fake = Path.Combine(root, "Data.p4k");
        File.WriteAllText(fake, "");
        var spawns = new[] {
            new GameSpawn("Copper", "Poor", 10, 10, "mineable", "Test", "Stanton", "Rocks", 1, 0.9, null, null),
            new GameSpawn("Copper", "Rich", 90, 90, "mineable", "Test", "Stanton", "Rocks", 1, 0.1, null, null)
        };
        var version = typeof(GameCommodities).GetField("CacheVersion", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue();
        var cache = Path.Combine(root, "commodities.json");
        File.WriteAllText(cache, JsonSerializer.Serialize(new {
            Stamp = $"{version}:{File.GetLastWriteTimeUtc(fake).Ticks}",
            Commodities = new Dictionary<string,string>(), Items = new Dictionary<string,string>(),
            Facts = new Dictionary<string,GameItem>(), Blueprints = Array.Empty<GameBlueprint>(),
            Spawns = spawns, Places = new Dictionary<string,GamePlace>()
        }));
        using var library = new LogLibrary(sessions);
        typeof(LogLibrary).GetProperty("GameCommodities")!.SetValue(library, GameCommodities.Load(root, cache));
        var method = typeof(ServerHost).GetMethod("MiningPlaces", BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = Assert.Single((IEnumerable<MiningPlace>)method.Invoke(null, [library, Prices()])!);
        Assert.Equal(180m, result.PerRock);
        Assert.Equal(18, result.Ore);
    }

    private (TextOverlayService Overlay, TextOverlayStore Labels, StarStringsStore Strings, string Table) InstalledLabels()
    {
        var table = Path.Combine(root, "Data", "Localization", "english", "global.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(table)!);
        var original = "item_Name_behr_rifle_ballistic_01=P4-AR Rifle";
        var backup = Path.Combine(root, "original.ini");
        File.WriteAllText(backup, original);
        File.WriteAllText(table, TextOverlay.Build(original, _ => false).Content);
        var labels = new TextOverlayStore(root);
        labels.Record(new TextOverlayInstall(DateTimeOffset.UtcNow, root, 1, false,
            [new InstalledFile(table, backup)], TextOverlayStore.Fingerprint(table)));
        var strings = new StarStringsStore(root);
        var overlay = new TextOverlayService(new LogLibrary(sessions), new ItemLabelStore(root),
            new UexData(root), labels, strings, NullLogger<TextOverlayService>.Instance);
        return (overlay, labels, strings, table);
    }

    /// <summary>
    /// Installing StarStrings on top of the marks, then taking both out again,
    /// has to leave the game's own file.
    /// </summary>
    /// <remarks>
    /// Driven through <see cref="TextOverlayService.WhileLiftedAsync"/> - the
    /// coordination the endpoint now uses - rather than by calling the two mods
    /// in sequence, because calling them in sequence IS the defect: StarStrings
    /// backs up whatever it finds, so it recorded the marked file as the
    /// original and removing both put the marks back for ever, with both stores
    /// reporting nothing installed.
    /// </remarks>
    [Fact]
    public async Task Removing_both_mods_restores_original_after_labels_were_installed_first()
    {
        var (overlay, labels, strings, table) = InstalledLabels();
        var mod = new StarStrings(new ClientFactory(), strings, NullLogger<StarStrings>.Instance);
        var game = new GameInstall("LIVE", root);

        var (problem, relabelled) = await overlay.WhileLiftedAsync(game,
            async () => (await mod.InstallAsync(game)).Problem);

        Assert.Null(problem);
        Assert.True(relabelled);

        Assert.True(overlay.Remove());
        Assert.True(mod.Remove());
        Assert.Null(labels.Current);
        Assert.Null(strings.Current);
        Assert.Equal("item_Name_behr_rifle_ballistic_01=P4-AR Rifle", File.ReadAllText(table));
    }

    /// <summary>
    /// And the same the other way: taking StarStrings out from under live marks
    /// lifts them first, so neither store is left claiming something untrue.
    /// </summary>
    /// <remarks>
    /// Calling <c>mod.Remove()</c> straight out restores StarStrings' backup
    /// over the marked file while the label store still reports the marks
    /// installed - a page describing a file that is no longer there.
    ///
    /// The marks do not go back on here, and that is the fixture rather than the
    /// behaviour: with StarStrings gone the base table comes from Data.p4k,
    /// which this temporary game folder has no copy of. What matters is that the
    /// store says so instead of pretending - which is the assertion below.
    /// </remarks>
    [Fact]
    public async Task Removing_StarStrings_from_under_the_marks_lifts_them_first()
    {
        var (overlay, labels, strings, table) = InstalledLabels();
        var mod = new StarStrings(new ClientFactory(), strings, NullLogger<StarStrings>.Instance);
        var game = new GameInstall("LIVE", root);

        Assert.Null((await overlay.WhileLiftedAsync(game,
            async () => (await mod.InstallAsync(game)).Problem)).Problem);

        var (problem, relabelled) = await overlay.WhileLiftedAsync(game, () =>
        {
            Assert.True(mod.Remove());
            return Task.FromResult<string?>(null);
        });

        Assert.Null(problem);
        Assert.Null(strings.Current);

        // The game's own table is back - not StarStrings', and not the marked
        // file that the unfixed order left behind.
        Assert.Equal("item_Name_behr_rifle_ballistic_01=P4-AR Rifle", File.ReadAllText(table));

        // The marks could not be relaid, so nothing claims they are there.
        Assert.False(relabelled);
        Assert.Null(labels.Current);
    }

    /// <summary>
    /// A file that cannot be read is not a file that changed. Forgetting the
    /// record on a transient lock leaves a marked table in the game folder with
    /// nothing left that knows how to undo it.
    /// </summary>
    [Fact]
    public void A_temporarily_locked_label_file_does_not_destroy_the_removal_manifest()
    {
        var (overlay, labels, _, table) = InstalledLabels();
        using var held = new FileStream(table, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.False(overlay.Remove());
        Assert.NotNull(labels.Current);
    }

    private sealed class ClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new FakeGithub());
    }
    private sealed class FakeGithub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            HttpContent content;
            if (request.RequestUri!.AbsolutePath.EndsWith(".zip"))
            {
                using var bytes = new MemoryStream();
                using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
                using (var stream = zip.CreateEntry("Data/Localization/english/global.ini").Open())
                    stream.Write(Encoding.UTF8.GetBytes("item_Name_behr_rifle_ballistic_01=P4-AR Rifle StarStrings"));
                content = new ByteArrayContent(bytes.ToArray());
            }
            else content = new StringContent("""{"name":"test","assets":[{"name":"test.zip","browser_download_url":"https://example.invalid/test.zip"}]}""");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
