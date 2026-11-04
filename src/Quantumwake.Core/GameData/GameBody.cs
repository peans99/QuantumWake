namespace Quantumwake.Core.GameData;

/// <summary>One row of a G-force profile: how much the body takes before it starts to suffer.</summary>
/// <param name="Tolerance">G the body takes without building stress.</param>
/// <param name="MaxGees">G at which stress builds fastest; past it the pilot is on the way out.</param>
/// <param name="SafeStress">Stress, 0 to 1, below which nothing shows.</param>
/// <param name="StressMaxoutSeconds">How long sustained load takes to fill the stress bar.</param>
/// <param name="StressRecoverySeconds">How long an empty bar takes to come back from full.</param>
public sealed record GForceTier(
    double Tolerance,
    double MaxGees,
    double SafeStress,
    double StressMaxoutSeconds,
    double StressRecoverySeconds);

/// <summary>
/// One of the player's G-force profiles, at the worst, usual and best
/// resistance a suit can give.
/// </summary>
/// <remarks>
/// The files hold five of these in a plain array and do not say which
/// direction each is for. The numbers suggest it - one takes 10 G before it
/// strains and 18 at most, which is how bodies take G pushed into the chest,
/// and one only 4 and 6, which is how they take G pulled toward the head - but
/// that is an inference, and the page says so instead of naming them.
/// </remarks>
public sealed record GForceProfile(int Index, GForceTier Worst, GForceTier Default, GForceTier Best);

/// <summary>
/// The player's own body, as the files describe it: G-force profiles and the
/// speeds the character moves at with nothing to slow it.
/// </summary>
/// <param name="GForce">The player's profiles, from <c>ActorGForceComponent.PlayerGForce</c>.</param>
/// <param name="WalkSpeed">Metres a second at a fast walk.</param>
/// <param name="RunSpeed">Metres a second running.</param>
/// <param name="SprintSpeed">Metres a second sprinting.</param>
public sealed record GameBody(
    IReadOnlyList<GForceProfile> GForce,
    double WalkSpeed,
    double RunSpeed,
    double SprintSpeed)
{
    public static readonly GameBody Empty = new([], 0, 0, 0);

    /// <summary>
    /// Reads the player's G-force profiles and standing speeds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Suits move a player between the worst and best tiers: every clothing
    /// item carries a <c>Flight.gForceResistance</c>, and they add up to round
    /// figures - a heavy helmet, core, arms and legs are -0.125, -0.5, -0.125
    /// and -0.25, exactly -1; medium sums to -0.5, light to -0.25; an undersuit
    /// is +0.9 and a flight suit up to +1. Checked on 4.10.2 LIVE and the
    /// 4.10 PTU of 2026-11-03, which agree on every one of these figures.
    /// </para>
    /// <para>
    /// Speeds come from <c>ActorStanceSpeedsInfo.Stand</c>, whose filter
    /// matches any armour: no armour class has a stance of its own, and every
    /// armour piece's <c>wearMovementMultipliers</c> is empty, so nothing worn
    /// changes them in these builds.
    /// </para>
    /// </remarks>
    public static GameBody Read(DataCore core)
    {
        var profiles = new List<GForceProfile>();
        double walk = 0, run = 0, sprint = 0;

        foreach (var record in core.Records())
        {
            if (record.Name.Equals("ActorGForceComponent.PlayerGForce", StringComparison.Ordinal))
            {
                var at = core.InstanceAt(record, record.VariantIndex);
                var index = 0;
                foreach (var entry in core.ClassArrayAt(at, record.StructIndex, "gForceParams"))
                {
                    var eat = core.InstanceAt(entry);
                    GForceTier Tier(string name) =>
                        GameMining.Nested(core, eat, entry.StructIndex, name) is { } t
                            ? new GForceTier(
                                R(core.SingleAt(t.At, t.StructIndex, "tolerance")),
                                R(core.SingleAt(t.At, t.StructIndex, "maxGees")),
                                R(core.SingleAt(t.At, t.StructIndex, "safeGStress")),
                                R(core.SingleAt(t.At, t.StructIndex, "stressMaxoutTime")),
                                R(core.SingleAt(t.At, t.StructIndex, "stressRecoveryTime")))
                            : new GForceTier(0, 0, 0, 0, 0);

                    profiles.Add(new GForceProfile(++index, Tier("worstResistance"), Tier("defaultResistance"), Tier("bestResistance")));
                }
            }
            else if (record.Name.Equals("ActorStanceSpeedsInfo.Stand", StringComparison.Ordinal))
            {
                var at = core.InstanceAt(record, record.VariantIndex);
                if (GameMining.Nested(core, at, record.StructIndex, "speeds") is { } speeds)
                {
                    walk = R(core.SingleAt(speeds.At, speeds.StructIndex, "walkFastSpeed"));
                    run = R(core.SingleAt(speeds.At, speeds.StructIndex, "runFastSpeed"));
                    sprint = R(core.SingleAt(speeds.At, speeds.StructIndex, "sprintSpeed"));
                }
            }
        }

        return new GameBody(profiles, walk, run, sprint);
    }

    private static double R(float? value) => value is { } v ? Math.Round(v, 4) : 0;
}
