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
/// Fighter". Role is consulted first only for the two trades a career can bury,
/// and last for freight, which a Multi-Role hull with a hold is still doing.
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
        // The game files the Prospector under Industrial and the MISC Fortune
        // under Starter, and both are out there to fill a hopper.
        if (Mentions(role, "Mining") || Mentions(role, "Salvage"))
            return Mining;

        // Single-ship careers - Destroyer, Gunship, Snub Fighter - are the
        // dataset filing a role in the career column. They are still combat.
        var byCareer = career?.Trim() switch
        {
            "Industrial" => Mining,
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
