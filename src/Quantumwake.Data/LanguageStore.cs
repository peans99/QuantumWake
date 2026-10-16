using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>A language the dashboard can be read in.</summary>
/// <param name="Code">The catalogue's file name under <c>web/locales/</c>, and the page's <c>lang</c>.</param>
/// <param name="Name">What the language calls itself, which is how a reader who needs it will look for it.</param>
/// <param name="Covers">
/// What has been translated so far, in that language, or null when nothing is
/// left in English. The picker shows it beside the name so a partial language
/// never passes for a finished one.
/// </param>
public sealed record DashboardLanguage(string Code, string Name, string? Covers);

/// <summary>
/// Remembers which language the dashboard is read in.
/// </summary>
/// <remarks>
/// <para>
/// On the server rather than in the page's <c>localStorage</c>, because the
/// overlay's WebView2 keeps storage of its own: a choice made in a browser would
/// leave the overlay in English, and the overlay is where a pilot reads it
/// mid-flight.
/// </para>
/// <para>
/// English is the default, always, and the Windows language is never read. A
/// French Windows that updates gets the app it had; another language is
/// something the pilot chooses - see <c>docs/localization.md</c>.
/// </para>
/// </remarks>
public sealed class LanguageStore
{
    public const string English = "en";

    public static readonly IReadOnlyList<DashboardLanguage> Available =
    [
        new(English, "English", null),
        new("fr", "Français",
            "traduit pour l’instant : la navigation, la recherche, la palette de commandes, "
            + "les bandeaux d’avertissement, la page Direct, la Carte, le Journal, tous les Réglages et toutes les pages Opérations, Vol, Économie, Référence et Hangar ; l’aide et quelques phrases écrites par le serveur sont encore en anglais"),
    ];

    private readonly string _path;
    private readonly Lock _gate = new();
    private string _current = English;

    public LanguageStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "language.json");
        Load();
    }

    public string Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>The code as it will be used: a known one, or English.</summary>
    /// <remarks>
    /// The code ends up in a file path when the catalogue is read, so anything
    /// not on the list is refused here rather than trusted there.
    /// </remarks>
    public static string Clean(string? code) =>
        Available.FirstOrDefault(l => string.Equals(l.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase))?.Code
        ?? English;

    public string Save(string? code)
    {
        var settled = Clean(code);

        lock (_gate)
        {
            _current = settled;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(new Saved(settled)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Same bargain as the other small settings: a choice that fails
                // to save reverts on the next start, which beats refusing it.
            }

            return settled;
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            _current = Clean(JsonSerializer.Deserialize<Saved>(File.ReadAllText(_path))?.Code);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _current = English;
        }
    }

    private sealed record Saved(string Code);
}
