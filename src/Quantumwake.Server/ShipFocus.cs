namespace Quantumwake.Server;

/// <summary>The work a ship is built for, as the Now page words it.</summary>
public sealed record ShipFocusChoice(string Key, string Label);

/// <summary>
/// What the ship in the hangar says the pilot came to do.
/// </summary>
/// <remarks>
/// <para>
/// There is nothing else to read. Mining is 15 lines across this install's 151
/// backups and salvage is 592 lines of <c>SetSalvageRepairAmmoCount</c>
/// warnings, so neither says anybody ever mined or salvaged anything — the
/// negative result in <c>docs/untapped-signals.md</c>. The ship that was
/// retrieved is the only honest signal of intent the app has, and it is a good
/// one: nobody takes a Prospector out to dogfight.
/// </para>
/// <para>
/// Career leads because it is the clean field — 14 values, against 60-odd roles
/// that arrive as "Starter / Light Mining" and "Light Freight / Medium
/// Fighter". Role is consulted first for mining, which a career can bury, and
/// last for freight, which a Multi-Role hull with a hold is still doing.
/// </para>
/// <para>
/// Industrial is deliberately not read as mining. Of its 24 ships, 10 are
/// salvage — Vulture, Reclaimer and kin — one is a freighter and one is a
/// science hull, so the career is right about the work for barely half the
/// hulls filed under it. A salvage pilot sent to the best ore deposits is the
/// exact failure this whole feature exists to avoid, so mining is claimed only
/// where the role says the word. Salvage gets no focus at all until there is
/// something salvage-specific to put on the card; a wrong lane is worse than
/// none.
/// </para>
/// <para>
/// A career with nothing to offer answers null rather than guessing. Multi-Role
/// and Competition are exactly where a wrong guess would rearrange the page
/// around work the pilot is not doing, and the page is better left plain.
/// </para>
/// </remarks>
public static class ShipFocus
{
    public static readonly ShipFocusChoice Mining = new("mining", "Mining");
    public static readonly ShipFocusChoice Freight = new("freight", "Freight");
    public static readonly ShipFocusChoice Combat = new("combat", "Combat");
    public static readonly ShipFocusChoice Explore = new("explore", "Exploration");

    /// <summary>The focus a ship's reference data implies, or null for none.</summary>
    public static ShipFocusChoice? Of(string? career, string? role)
    {
        // Salvage before everything, and it answers nothing: every salvage hull
        // in the dataset is filed Industrial, so without this they would all
        // fall through to a career arm and be told where the ore is.
        if (Mentions(role, "Salvage"))
            return null;

        // The game files the Prospector under Industrial and the MISC Fortune
        // under Starter, and both are out there to fill a hopper. The role is
        // what says so; see the note above on why the career does not.
        if (Mentions(role, "Mining"))
            return Mining;

        // Single-ship careers - Destroyer, Gunship, Snub Fighter - are the
        // dataset filing a role in the career column. They are still combat.
        var byCareer = career?.Trim() switch
        {
            "Transporter" or "Transport" => Freight,
            "Combat" or "Destroyer" or "Gunship" or "Snub Fighter" => Combat,
            "Exploration" => Explore,
            _ => null
        };

        return byCareer ?? (Mentions(role, "Freight") ? Freight : null);
    }

    private static bool Mentions(string? role, string word) =>
        role is not null && role.Contains(word, StringComparison.OrdinalIgnoreCase);
}
