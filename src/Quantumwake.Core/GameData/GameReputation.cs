namespace Quantumwake.Core.GameData;

/// <summary>One rank on a reputation ladder.</summary>
/// <param name="Name">"Jr. Contractor", in English, or the record's own name when the key has none.</param>
/// <param name="Perk">"Special Wolf Barter Contract", or null when the rank unlocks nothing it names.</param>
/// <param name="MinReputation">The reputation the rank starts at. "Not Eligible" starts below zero, at -1000.</param>
public sealed record GameRepRank(string Name, string? Perk, long MinReputation);

/// <summary>
/// One reputation ladder - a scope, in the game's word - as the Rep app shows it
/// under a heading.
/// </summary>
/// <param name="Id">The record, <c>ReputationScope_Hauling</c>.</param>
/// <param name="Name">"Hauling" - the heading over the cards - or null when the key has no English.</param>
/// <param name="NameKey">
/// The heading's key, kept because the game shows the key itself where it has no
/// English: the Headhunters' page is headed <c>@REPSCOPE_SHIPCOMBAT_HEADHUNTERS_NAME</c>.
/// </param>
/// <param name="Ceiling">The most reputation the scope holds.</param>
/// <param name="Ranks">Lowest first.</param>
public sealed record GameRepScope(
    string Id, string? Name, string? NameKey, long Ceiling, IReadOnlyList<GameRepRank> Ranks);

/// <summary>An organisation as the Rep app lists it, and its mark.</summary>
/// <param name="Id">The record, <c>FactionReputation_Lawful_RedWindLinehaul</c>.</param>
/// <param name="Name">"Red Wind Linehaul", the name the Rep app prints.</param>
/// <param name="Logo">
/// The archive entry of its logo, <c>Data\UI\SharedAssets\ManufacturerLogos\rep_ui_icon_logo_redwind.dds</c>,
/// or null when the record names none or the archive does not hold it.
/// </param>
public sealed record GameRepOrganisation(string Id, string Name, string? Logo);

/// <summary>Every reputation ladder the install defines, and the organisations.</summary>
/// <param name="Organisations">Null in a cache written before they were read.</param>
public sealed record GameReputationCatalogue(
    IReadOnlyList<GameRepScope> Scopes, IReadOnlyList<GameRepOrganisation>? Organisations = null)
{
    public static GameReputationCatalogue Empty { get; } = new([], []);
}

/// <summary>
/// Reads the reputation ladders out of the DataCore.
/// </summary>
/// <remarks>
/// <para>
/// A ladder belongs to a scope, not to an organisation: Red Wind, Covalex, Ling
/// and FTL all rank a pilot on the one <c>ReputationScope_Hauling</c>, eight
/// ranks from Not Eligible at -1000 to Master at 237,750. Measured on 4.10.2:
/// 46 <c>SReputationScopeParams</c>, each with a <c>standingMap</c> holding a
/// <c>reputationCeiling</c> and its <c>standings</c>, each of those an
/// <c>SReputationStandingParams</c> with a <c>displayName</c>, a
/// <c>perkDescription</c> and a <c>minReputation</c>.
/// </para>
/// <para>
/// Which ladder an organisation's page shows is not read from here. The
/// organisation's own list of scopes (<c>FactionReputation</c> ->
/// <c>SReputationContextUI</c>) leaves out ones its contracts plainly use - the
/// Headhunters' Rank I to VI among them - so the page's ladder is found by its
/// cards instead: the scope whose rank names are the names the screenshot read.
/// See <c>ReputationLadders</c> in the data project.
/// </para>
/// <para>
/// Sorted by threshold rather than taken in stored order: Wikelo's ladder and
/// the Affinity one are stored highest first.
/// </para>
/// </remarks>
public static class GameReputation
{
    private const string ScopeStruct = "SReputationScopeParams";
    private const string OrganisationStruct = "FactionReputation";

    /// <param name="logoEntries">
    /// The archive's listing of <see cref="GameMakerLogos.Folder"/>, where every
    /// organisation's mark lives beside the makers' - so a logo is offered only
    /// when the file is there to decode.
    /// </param>
    public static GameReputationCatalogue Read(
        DataCore core, IReadOnlyDictionary<string, string> text, IEnumerable<string>? logoEntries = null)
    {
        var scopeStruct = core.StructIndexOf(ScopeStruct);
        if (scopeStruct < 0) return GameReputationCatalogue.Empty;

        var organisationStruct = core.StructIndexOf(OrganisationStruct);
        var byId = new Dictionary<Guid, DataRecord>();
        var scopes = new List<DataRecord>();
        var organisations = new List<DataRecord>();

        foreach (var record in core.Records())
        {
            byId.TryAdd(record.Hash, record);
            if (record.StructIndex == scopeStruct) scopes.Add(record);
            if (record.StructIndex == organisationStruct) organisations.Add(record);
        }

        var read = new List<GameRepScope>();

        foreach (var scope in scopes)
        {
            var at = core.InstanceAt(scope, scope.VariantIndex);
            var (mapAt, map) = core.FieldAt(at, scope.StructIndex, "standingMap");
            if (map is null) continue;

            var ranks = new List<GameRepRank>();

            foreach (var id in core.ReferenceArrayAt(mapAt, map.StructIndex, "standings"))
            {
                if (!byId.TryGetValue(id, out var standing)) continue;

                var standingAt = core.InstanceAt(standing, standing.VariantIndex);
                var key = core.StringAt(standingAt, standing.StructIndex, "displayName");

                // The Affinity ladder pads its forty-one steps with unnamed
                // placeholders between the five it names.
                if (key is null || key.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)) continue;

                ranks.Add(new GameRepRank(
                    Localised(text, key) ?? Bare(standing.Name),
                    Localised(text, core.StringAt(standingAt, standing.StructIndex, "perkDescription")),
                    core.Int64At(standingAt, standing.StructIndex, "minReputation") ?? 0));
            }

            if (ranks.Count == 0) continue;

            var nameKey = core.StringAt(at, scope.StructIndex, "displayName");

            read.Add(new GameRepScope(
                Bare(scope.Name),
                Localised(text, nameKey),
                nameKey,
                core.Int64At(mapAt, map.StructIndex, "reputationCeiling") ?? 0,
                [.. ranks.OrderBy(r => r.MinReputation)]));
        }

        return new GameReputationCatalogue(
            [.. read.OrderBy(s => s.Id, StringComparer.Ordinal)],
            Organisations(core, text, organisations, logoEntries ?? []));
    }

    /// <summary>
    /// Every organisation with a name, and its logo where the archive has it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>FactionReputation</c>'s <c>logo</c> names a <c>.tif</c> under
    /// <c>UI/SharedAssets/ManufacturerLogos</c> - <c>rep_ui_icon_logo_redwind.tif</c>
    /// - and the archive ships the <c>.dds</c> it was cooked to, as it does for
    /// the makers. Measured on 4.10.2: 39 records, 37 with a name, 35 of those
    /// naming a logo the archive holds. Dead Saints and Highpoint Wilderness
    /// Specialists name none, and the Rep app shows Dead Saints' card with no
    /// mark either. Sizes vary - 256, 400 and 500 square, and eight (Adagio,
    /// Hockrow and Wikelo among them) as a split mip chain - all shapes the
    /// maker-logo route already decodes.
    /// </para>
    /// <para>
    /// Two records carry <c>&lt;= PLACEHOLDER =&gt;</c> for a name; they are not
    /// organisations anybody can be shown, and are left out.
    /// </para>
    /// </remarks>
    private static List<GameRepOrganisation> Organisations(
        DataCore core, IReadOnlyDictionary<string, string> text, List<DataRecord> records, IEnumerable<string> logoEntries)
    {
        var present = new HashSet<string>(logoEntries.Select(P4kArchive.EntryFileName), StringComparer.OrdinalIgnoreCase);
        var read = new List<GameRepOrganisation>();

        foreach (var record in records)
        {
            var at = core.InstanceAt(record, record.VariantIndex);
            var name = Localised(text, core.StringAt(at, record.StructIndex, "displayName"));
            if (name is null || name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)) continue;

            string? entry = null;

            if (core.StringAt(at, record.StructIndex, "logo") is { Length: > 0 } logo
                && logo.Contains("ManufacturerLogos", StringComparison.OrdinalIgnoreCase))
            {
                var file = P4kArchive.EntryStem(logo) + ".dds";
                if (present.Contains(file)) entry = GameMakerLogos.Folder + file;
            }

            read.Add(new GameRepOrganisation(Bare(record.Name), name, entry));
        }

        return [.. read.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// The English for a key, or null - including for <c>@blank_space</c>,
    /// which is how a rank with no perk says so.
    /// </summary>
    private static string? Localised(IReadOnlyDictionary<string, string> text, string? key)
    {
        if (key is not { Length: > 0 }) return null;

        var bare = key.TrimStart('@');
        if (!text.TryGetValue(bare, out var english) && !text.TryGetValue(bare + ",P", out english)) return null;

        english = english.Replace("\\n", " ").Trim();
        return english.Length > 0 ? english : null;
    }

    private static string Bare(string name) => name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
}
