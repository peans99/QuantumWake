using Quantumwake.Core.GameData;
using Quantumwake.Core.State;

namespace Quantumwake.Data;

/// <summary>
/// What a logged contract pays in reputation and blueprints, from the installed
/// game first and a text mod's title tags only when the game does not know it.
/// </summary>
/// <remarks>
/// <para>
/// The game's figure is the contract's own. StarStrings, the mod this used to
/// read, could only annotate a title, and one title covers every rank of a Red
/// Wind haul - so it wrote the whole ladder. The install names the contract the
/// pilot actually took: across this install's logs all 734 contracts were found
/// by id, and the 46 single-figure tags agreed with the files to the point.
/// </para>
/// <para>
/// The tag stays as the fallback for a contract the install has no word on - a
/// session from a patch whose contract ids have since moved, or a machine whose
/// game files could not be read. When the install does know the contract, its
/// silence is an answer: an Iasi patrol pays event points and no rep, and a
/// tag cannot overrule that.
/// </para>
/// </remarks>
public static class ContractPay
{
    /// <summary>Where a figure came from: the installed game's files.</summary>
    public const string Game = "game";

    /// <summary>Where a figure came from: a StarStrings tag on the title.</summary>
    public const string Tag = "tag";

    /// <summary>The install's word on this contract, or null when it has none.</summary>
    public static GameMissionPay? Known(ContractRecord contract, GameReputationCatalogue catalogue) =>
        catalogue.Mission(contract.DefinitionId);

    /// <summary>
    /// The reputation finishing it pays the organisation offering it, or null
    /// when nothing says - which is not the same as zero.
    /// </summary>
    public static int? Rep(ContractRecord contract, GameReputationCatalogue catalogue) =>
        Known(contract, catalogue) is { } pay
            ? pay.OnCompletion is > 0 and var rep ? rep : null
            : ContractTags.RepFrom(contract.Name);

    /// <summary><see cref="Game"/> or <see cref="Tag"/> for a contract <see cref="Rep"/> has a figure for, else null.</summary>
    public static string? RepSource(ContractRecord contract, GameReputationCatalogue catalogue) =>
        Rep(contract, catalogue) is null ? null : Known(contract, catalogue) is not null ? Game : Tag;

    /// <summary>Whether it can award a blueprint.</summary>
    public static bool Blueprint(ContractRecord contract, GameReputationCatalogue catalogue) =>
        Known(contract, catalogue) is { } pay ? pay.Blueprint is > 0 : ContractTags.AwardsBlueprint(contract.Name);

    /// <summary>
    /// The chance of that blueprint, 0 to 1, when the install says; a tag only
    /// ever said "yes", so it gives none.
    /// </summary>
    public static double? BlueprintChance(ContractRecord contract, GameReputationCatalogue catalogue) =>
        Known(contract, catalogue)?.Blueprint is > 0 and var chance ? chance : null;
}
