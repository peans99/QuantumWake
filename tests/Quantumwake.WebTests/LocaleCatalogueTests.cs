using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Quantumwake.WebTests;

/// <summary>
/// Every translation still belongs to some English, and carries what the
/// English carries.
/// </summary>
/// <remarks>
/// <para>
/// The English is the key, so editing a sentence on the page silently orphans
/// its translation: the French reader gets the new English and nothing says
/// why. The first test here is what says why, naming the entry.
/// </para>
/// <para>
/// The others are about what a translator can break without meaning to. Markup
/// entries replace an element's whole content, so a dropped tag is a dropped
/// element - and a dropped <c>id</c> is a control the script can no longer
/// find. A dropped <c>{placeholder}</c> is a number that never appears.
/// </para>
/// </remarks>
public class LocaleCatalogueTests
{
    private static string Web(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "web", .. parts]);

    public static TheoryData<string> Catalogues()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Web("locales"), "*.json"))
            data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    private static Dictionary<string, JsonElement> Read(string language) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(Web("locales", $"{language}.json")))!;

    /// <summary>Each form an entry can take: one string, or a plural's forms.</summary>
    private static IEnumerable<(string Form, string Text)> Forms(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
            ? [("", value.GetString()!)]
            : value.EnumerateObject().Select(p => (p.Name, p.Value.GetString()!));

    /// <summary>Whitespace collapsed and entities decoded, as both the page and the catalogue are compared.</summary>
    private static string Plain(string text) =>
        Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();

    private static readonly Regex Tag = new("<[^>]+>");
    private static readonly Regex Placeholder = new(@"\{(\w+)\}");

    private static string Tags(string text) => string.Join("", Tag.Matches(text).Select(m => m.Value));

    private static string Placeholders(string text) =>
        string.Join(",", Placeholder.Matches(text).Select(m => m.Groups[1].Value).Order());

    /// <summary>
    /// The server's C# string literals, with a sentence split across <c>+</c>
    /// joined back up.
    /// </summary>
    /// <remarks>
    /// Some of what the page shows is the server's English - a feed's title, a
    /// signal's name - translated on the page through the same catalogue, so
    /// those entries are anchored here rather than in a script. Read from the
    /// source tree, since the test output carries only the web folder.
    /// </remarks>
    private static string ServerLiterals()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Quantumwake.slnx")))
            dir = dir.Parent;

        Assert.True(dir is not null, "the repository root (Quantumwake.slnx) was not found above the test output");

        var source = string.Join("\n", Directory
            .GetFiles(Path.Combine(dir!.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));

        return Regex.Replace(source, "\"\\s*\\+\\s*\"", "");
    }

    [Theory]
    [MemberData(nameof(Catalogues))]
    public void Every_entry_still_has_its_english(string language)
    {
        var markup = Plain(File.ReadAllText(Web("index.html")));
        var scripts = File.ReadAllText(Web("app.js")) + File.ReadAllText(Web("i18n.js"));
        var server = ServerLiterals();

        // As a script literal, in either quote, with any apostrophe escaped.
        bool InScript(string key) =>
            scripts.Contains($"'{key.Replace("'", "\\'")}'", StringComparison.Ordinal)
            || scripts.Contains($"\"{key}\"", StringComparison.Ordinal);

        // Escaped as C# writes it: a path's backslashes are doubled in the source.
        bool InServer(string key) =>
            server.Contains($"\"{key.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"", StringComparison.Ordinal);

        var orphans = Read(language).Keys
            .Where(key => !markup.Contains(Plain(key), StringComparison.Ordinal) && !InScript(key) && !InServer(key))
            .ToList();

        Assert.True(orphans.Count == 0,
            $"{language}.json translates English that is no longer anywhere on the page:\n  "
            + string.Join("\n  ", orphans));
    }

    [Theory]
    [MemberData(nameof(Catalogues))]
    public void Every_entry_keeps_its_markup_and_placeholders(string language)
    {
        var broken = new List<string>();

        foreach (var (key, value) in Read(language))
        {
            foreach (var (form, text) in Forms(value))
            {
                if (Tags(text) != Tags(key))
                    broken.Add($"{key} [{form}]: tags {Tags(text)} for {Tags(key)}");

                if (Placeholders(text) != Placeholders(key))
                    broken.Add($"{key} [{form}]: placeholders {{{Placeholders(text)}}} for {{{Placeholders(key)}}}");
            }
        }

        Assert.True(broken.Count == 0, string.Join("\n", broken));
    }

    /// <summary>
    /// A plural needs both forms. One missing falls back to <c>other</c>, which
    /// in French is "1 journaux".
    /// </summary>
    [Theory]
    [MemberData(nameof(Catalogues))]
    public void Every_plural_has_both_forms(string language)
    {
        var incomplete = Read(language)
            .Where(entry => entry.Value.ValueKind == JsonValueKind.Object)
            .Where(entry => !entry.Value.TryGetProperty("one", out _) || !entry.Value.TryGetProperty("other", out _))
            .Select(entry => entry.Key)
            .ToList();

        Assert.Empty(incomplete);
    }

    /// <summary>
    /// The picker says French covers the navigation, so it has to: a page
    /// added to the strip without a translation would make that untrue.
    /// </summary>
    [Fact]
    public void The_whole_navigation_is_french()
    {
        var french = Read("fr");
        var markup = File.ReadAllText(Web("index.html"));
        var strip = markup[markup.IndexOf("<nav class=\"tabs\"", StringComparison.Ordinal)..markup.IndexOf("</nav>", StringComparison.Ordinal)];

        var labels = Regex.Matches(strip, @"<button[^>]*>([^<]+)</button>")
            .Select(m => Plain(m.Groups[1].Value))
            .Where(label => label != "Wikelo")
            .Distinct()
            .ToList();

        Assert.NotEmpty(labels);
        Assert.Empty(labels.Where(label => !french.ContainsKey(label)));
    }
}
