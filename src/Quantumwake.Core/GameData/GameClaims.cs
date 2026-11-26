namespace Quantumwake.Core.GameData;

/// <summary>A ship's own insurance wait, as its entity states it.</summary>
/// <param name="BaseMinutes"><c>baseWaitTimeMinutes</c>: the Cutlass Black reads 9.99.</param>
/// <param name="ExpeditedMinutes">
/// <c>mandatoryWaitTimeMinutes</c>: 3.33 on the same hull. The community dataset
/// publishes this as the expedited claim time, and the two agree on all of its ships.
/// </param>
/// <param name="ExpediteFee"><c>baseExpeditingFee</c>, aUEC: 6,251 on the Cutlass Black.</param>
public sealed record ShipClaimWait(double BaseMinutes, double ExpeditedMinutes, long ExpediteFee);

/// <summary>
/// One of the item-recovery override groups: a list of hulls whose claims are
/// scaled by these factors. A factor of 1 is no change.
/// </summary>
public sealed record ClaimGroup(double Cooldown, double Cost, double Delivery, IReadOnlyList<string> Classes)
{
    /// <summary>True when the group changes nothing - two of LIVE 4.10's seven, four of the 5.0 PTU's.</summary>
    public bool Neutral => Cooldown == 1 && Cost == 1 && Delivery == 1;
}

/// <summary>
/// The escalating-claims settings: how many claims chain, and what each step
/// multiplies. LIVE 4.10 reads a chain of 1 and factors of 1, which is none.
/// </summary>
public sealed record ClaimRepeat(int Chain, double Cooldown, double Cost, double Delivery)
{
    public bool Active => Chain > 1 && (Cooldown != 1 || Cost != 1 || Delivery != 1);
}

/// <summary>What the install says about claiming a ship back.</summary>
public sealed record GameClaimRules(
    Dictionary<string, ShipClaimWait> Waits,
    List<ClaimGroup> Groups,
    ClaimRepeat? Repeat)
{
    public static readonly GameClaimRules Empty = new([], [], null);

    /// <summary>
    /// The groups that list a hull and change something. More than one is
    /// possible: the 5.0 PTU puts the Carrack in a 0.8 group and a 0.6 one, and
    /// nothing in the files says which wins, so both are handed back.
    /// </summary>
    public IReadOnlyList<ClaimGroup> GroupsOf(string cls) =>
        [.. Groups.Where(g => !g.Neutral && g.Classes.Contains(cls, StringComparer.OrdinalIgnoreCase))];
}

/// <summary>
/// Insurance claim waits and the 5.0 claim rules.
/// </summary>
/// <remarks>
/// <para>
/// Two places. Each ship's <c>SEntityInsuranceProperties</c> carries
/// <c>shipInsuranceParams</c> with a base and a mandatory wait in minutes; those
/// are the numbers the community dataset republishes, and they did not move
/// between LIVE 4.10.3 and the 5.0 PTU.
/// </para>
/// <para>
/// The other is the single <c>ItemRecoveryConfigurationParams.GlobalItemRecovery</c>
/// record. On the 5.0 PTU (13284307) its economy block was reorganised into
/// <c>cooldownParams</c>, <c>deliveryParams</c> and <c>repairParams</c>, the
/// escalating-claims chain went from 1 to 5 (×2.4 wait, ×1.75 cost, ×1.5
/// delivery), and the seven override groups were re-weighted: starter hulls
/// gained 0.8 on wait, cost and delivery and the capital group 0.8 on wait and
/// delivery, while the industrial (0.8), touring (0.7) and military (0.7)
/// discounts went back to 1 and the large haulers kept 0.6 on the wait but lost
/// it on the cost. Those factors are read and shown as factors. The price-based cooldown and delivery
/// fields (<c>aUECperSecond</c>, <c>scalingPriceFloor</c>) are not turned into
/// minutes: no log records a claim's length, so a formula built from field
/// names could not be checked against anything.
/// </para>
/// </remarks>
public static class GameClaims
{
    private const string GlobalRecord = "ItemRecoveryConfigurationParams.GlobalItemRecovery";

    public static GameClaimRules Read(DataCore core)
    {
        var waits = ReadWaits(core);
        var record = core.Records().FirstOrDefault(r => r.Name == GlobalRecord);
        if (record is null) return new GameClaimRules(waits, [], null);

        var names = new Dictionary<Guid, string>();
        foreach (var r in core.Records())
        {
            if (r.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal))
                names.TryAdd(r.Hash, r.Name["EntityClassDefinition.".Length..]);
        }

        var at = core.InstanceAt(record, record.VariantIndex);
        var (economy, economyField) = core.FieldAt(at, record.StructIndex, "economyParams");
        if (economy < 0 || economyField is null) return new GameClaimRules(waits, [], null);
        var economyStruct = economyField.StructIndex;

        ClaimRepeat? repeat = null;
        var (chain, chainField) = core.FieldAt(economy, economyStruct, "escalatingClaimsParams");
        if (chain >= 0 && chainField is not null)
        {
            var s = chainField.StructIndex;
            repeat = new ClaimRepeat(
                core.Int32At(chain, s, "escalatingClaimChainLimit") ?? 1,
                core.SingleAt(chain, s, "escalationTimerMultiplier") ?? 1,
                core.SingleAt(chain, s, "escalatingClaimCostMultiplier") ?? 1,
                core.SingleAt(chain, s, "escalatingDeliveryTimerMultiplier") ?? 1);
        }

        var groups = new List<ClaimGroup>();
        // An inline class array on both builds; a pointer array is tried too so a
        // re-typed field reads rather than silently giving no groups.
        var overrides = core.ClassArrayAt(economy, economyStruct, "overrides");
        if (overrides.Count == 0) overrides = core.PointerArrayAt(economy, economyStruct, "overrides");
        foreach (var o in overrides)
        {
            var g = core.InstanceAt(o);
            var s = o.StructIndex;
            // A class the record names but the install no longer defines is
            // dropped: the PTU lists two such guids, and a guid is no hull name.
            var classes = core.ReferenceArrayAt(g, s, "classes")
                .Select(id => names.GetValueOrDefault(id)).OfType<string>().ToList();
            groups.Add(new ClaimGroup(
                Round(core.SingleAt(g, s, "claimCooldownMultiplier") ?? 1),
                Round(core.SingleAt(g, s, "claimCostMultiplier") ?? 1),
                Round(core.SingleAt(g, s, "deliveryTimeMultiplier") ?? 1),
                classes));
        }

        if (repeat is not null)
            repeat = repeat with { Cooldown = Round(repeat.Cooldown), Cost = Round(repeat.Cost), Delivery = Round(repeat.Delivery) };

        return new GameClaimRules(waits, groups, repeat);
    }

    // The file stores singles: 0.8 arrives as 0.800000011920929.
    private static double Round(double v) => Math.Round(v, 3);

    private static Dictionary<string, ShipClaimWait> ReadWaits(DataCore core)
    {
        var waits = new Dictionary<string, ShipClaimWait>(StringComparer.OrdinalIgnoreCase);
        var insurance = core.StructIndexOf("SEntityInsuranceProperties");
        if (insurance < 0) return waits;

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal)) continue;

            // Static class data, not Components: it is a fact about the entitlement, not the running ship.
            foreach (var component in core.PointerArray(record, "StaticEntityClassData"))
            {
                if (component.StructIndex != insurance) continue;

                var (ship, shipField) = core.FieldAt(core.InstanceAt(component), insurance, "shipInsuranceParams");
                if (ship < 0 || shipField is null) break;

                var wait = core.SingleAt(ship, shipField.StructIndex, "baseWaitTimeMinutes") ?? 0;
                var minimum = core.SingleAt(ship, shipField.StructIndex, "mandatoryWaitTimeMinutes") ?? 0;
                var fee = core.UInt32At(ship, shipField.StructIndex, "baseExpeditingFee") ?? 0;
                // Components and modules carry the block too, with zeros.
                if (wait > 0)
                    waits[record.Name["EntityClassDefinition.".Length..]] = new ShipClaimWait(Math.Round(wait, 2), Math.Round(minimum, 2), fee);
                break;
            }
        }

        return waits;
    }
}
