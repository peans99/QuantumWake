namespace Quantumwake.Data;

/// <summary>One sound the voice can make, as Settings → Callouts lists it.</summary>
/// <param name="Kind">The kind the watches tag it with - what a switch mutes.</param>
/// <param name="Group">combat, missions or mining: which of the Log page's three switches it sits under.</param>
/// <param name="Name">What the row is called.</param>
/// <param name="Sample">A sentence of the kind, said by the row's "Hear it".</param>
public sealed record CalloutKind(string Kind, string Group, string Name, string Sample);

/// <summary>
/// Every callout the app can say, so each one can be switched off on its own.
/// </summary>
/// <remarks>
/// The kinds are the ones the watches already tag their callouts with -
/// <see cref="CombatWatch"/>, <see cref="MiningWatch"/>, <see cref="HudWatch"/>
/// and <see cref="MissionWatch"/> - so muting is a lookup on the way to the
/// voice and no watch has to know about it. A callout still appears on the
/// page when its sound is off: the switch is about the headset.
/// </remarks>
public static class Callouts
{
    /// <summary>The ship itself: no switch of its own on the Log page, so only the voice and its own row rule it.</summary>
    public const string Ship = "ship";
    public const string Combat = "combat";
    public const string Missions = "missions";
    public const string Mining = "mining";

    public static readonly IReadOnlyList<CalloutKind> All =
    [
        new("online", Ship, "Ship HUD online", "Quantum Wake system online"),
        new("identified", Combat, "Target identified", "Target identified: Vanduul Blade"),
        new("hostile", Combat, "Hostile", "Hostile target identified: Vanduul Blade"),
        new("likely", Combat, "Unknown contact, likely hull", "Unknown contact, likely Anvil Arrow"),
        new("critical", Combat, "Target critical", "Target critical"),
        new("disabled", Combat, "Target disabled", "Target disabled"),
        new("missile", Combat, "Missile inbound", "Missile inbound"),
        new("territory", Combat, "Entering hostile territory", "Entering hostile territory"),
        new("objective-health", Missions, "Escort or objective health low", "Escort hull below 50 percent"),
        new("objective", Missions, "New objective", "New objective"),
        new("objective-done", Missions, "Objective complete", "Objective complete"),
        new("objective-failed", Missions, "Objective failed", "Objective failed"),
        new("contract-done", Missions, "Contract complete", "Contract complete"),
        new("contract-failed", Missions, "Contract failed", "Contract failed"),
        new("window", Mining, "In / out of the charge window", "In the window"),
        new("overcharge", Mining, "Overcharge", "Overcharge, back off"),
        new("cracked", Mining, "Rock cracked", "Rock cracked"),
        new("ping", Mining, "Radar ping", "three Riccite"),
    ];

    private static readonly Dictionary<string, CalloutKind> ByKind = All.ToDictionary(k => k.Kind, StringComparer.Ordinal);

    /// <summary>The kind's entry, or null for a kind this build does not know.</summary>
    public static CalloutKind? Find(string? kind) => kind is not null && ByKind.TryGetValue(kind, out var k) ? k : null;

    /// <summary>Which group a kind sits under; combat for one this build does not list, which is where every unlisted kind came from.</summary>
    public static string GroupOf(string kind) => Find(kind)?.Group ?? Combat;
}
