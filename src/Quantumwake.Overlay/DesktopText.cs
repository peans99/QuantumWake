using System.Globalization;
using System.IO;
using System.Text.Json;
using Quantumwake.Core;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Overlay;

/// <summary>
/// The desktop app's own words - the tray menu and its notices, the overlay's
/// header - in the language the dashboard is read in.
/// </summary>
/// <remarks>
/// <para>
/// The dashboard's catalogue, keyed by the English, so a language is still one
/// file and one test guards it: the orphan check reads these C# literals the
/// same way it reads the server's.
/// </para>
/// <para>
/// The choice is made in the dashboard and saved to <c>language.json</c>, and
/// the tray has no other way to hear about it, so the file's timestamp is
/// checked on every lookup and the catalogue reread when it moves. A lookup is
/// a menu opening or a notice, not a hot path.
/// </para>
/// </remarks>
internal static class DesktopText
{
    private static readonly Lock Gate = new();
    private static DateTime _stamp = DateTime.MinValue;
    private static bool _loaded;
    private static string _code = LanguageStore.English;
    private static Dictionary<string, JsonElement> _strings = [];

    /// <summary>The English, or the catalogue's word for it, with its placeholders filled.</summary>
    public static string T(string english, params (string Name, object? Value)[] values)
    {
        Refresh();
        var text = _strings.TryGetValue(english, out var found) && found.ValueKind == JsonValueKind.String
            ? found.GetString() ?? english
            : english;
        return Fill(text, values);
    }

    /// <summary>A count's sentence, chosen by the language's own plural rule.</summary>
    public static string N(long count, string one, string other, params (string Name, object? Value)[] values)
    {
        Refresh();
        (string, object?)[] all = [("count", count), .. values];

        if (_strings.TryGetValue(other, out var found) && found.ValueKind == JsonValueKind.Object
            && (found.TryGetProperty(Form(count), out var form) || found.TryGetProperty("other", out form))
            && form.ValueKind == JsonValueKind.String)
        {
            return Fill(form.GetString() ?? other, all);
        }

        return Fill(count == 1 ? one : other, all);
    }

    // French counts nought as one, as the page's own rule does.
    private static string Form(long count) => _code == "fr"
        ? (count is 0 or 1 ? "one" : "other")
        : (count == 1 ? "one" : "other");

    private static string Fill(string text, (string Name, object? Value)[] values)
    {
        foreach (var (name, value) in values)
            text = text.Replace("{" + name + "}", Convert.ToString(value, CultureInfo.CurrentCulture), StringComparison.Ordinal);
        return text;
    }

    private static void Refresh()
    {
        var path = Path.Combine(AppPaths.Root, "language.json");
        var stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

        lock (Gate)
        {
            if (_loaded && stamp == _stamp)
                return;

            _loaded = true;
            _stamp = stamp;
            _code = new LanguageStore().Current;
            _strings = [];

            // A catalogue that is missing or will not parse leaves the app in
            // English rather than throwing out of a menu.
            if (ServerHost.CatalogueText(_code) is not { } json)
                return;

            try
            {
                using var document = JsonDocument.Parse(json);
                foreach (var entry in document.RootElement.EnumerateObject())
                    _strings[entry.Name] = entry.Value.Clone();
            }
            catch (JsonException)
            {
                _strings = [];
            }
        }
    }
}
