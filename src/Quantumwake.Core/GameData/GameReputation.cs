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

/// <summary>Every reputation ladder the install defines.</summary>
public sealed record GameReputationCatalogue(IReadOnlyList<GameRepScope> Scopes)
{
    public static GameReputationCatalogue Empty { get; } = new([]);
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

    public static GameReputationCatalogue Read(DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var scopeStruct = core.StructIndexOf(ScopeStruct);
        if (scopeStruct < 0) return GameReputationCatalogue.Empty;

        var byId = new Dictionary<Guid, DataRecord>();
        var scopes = new List<DataRecord>();

        foreach (var record in core.Records())
        {
            byId.TryAdd(record.Hash, record);
            if (record.StructIndex == scopeStruct) scopes.Add(record);
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

        return new GameReputationCatalogue([.. read.OrderBy(s => s.Id, StringComparer.Ordinal)]);
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
