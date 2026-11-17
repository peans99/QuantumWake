using Quantumwake.Core.GameData;

namespace Quantumwake.Data;

/// <summary>
/// Whether a hull carries a mining head (1.2.60), from either of the two
/// descriptions of it the reference data holds.
/// </summary>
/// <remarks>
/// Neither description is enough alone. The ports a pilot can change
/// (<see cref="ShipSlot"/>) have the Prospector's, the MOLE's and the Golem's
/// lasers but not the ROC's, whose arm is not marked changeable; the Garage's
/// fuller loadout has the ROC's. On this install the first knew 11 mining
/// hulls of 323. A hull neither knows - one newer than the data, the Hermes
/// and the Arrastra here - is unknown rather than no.
/// </remarks>
public static class MiningHulls
{
    private static readonly HashSet<string> Heads = new(StringComparer.OrdinalIgnoreCase) { "WeaponMining", "MiningArm" };

    /// <summary>True with a mining port, false when the hull is known and has none, null when it is not known.</summary>
    public static bool? CanMine(ShipBase? garage, IReadOnlyList<ShipSlot> slots)
    {
        if (slots.Any(s => Heads.Contains(s.Kind)) || (garage is not null && Any(garage.Loadout)))
            return true;

        return garage is null && slots.Count == 0 ? null : false;
    }

    private static bool Any(IReadOnlyList<FitPort> ports) =>
        ports.Any(p => (p.Type is { } t && Heads.Contains(t)) || p.Accepts.Any(Heads.Contains) || Any(p.Children));
}
