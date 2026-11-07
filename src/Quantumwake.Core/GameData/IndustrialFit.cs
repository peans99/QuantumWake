namespace Quantumwake.Core.GameData;

public static class IndustrialFit
{
    public static bool IsMiningCandidate(string head) =>
        !head.Contains("TEST", StringComparison.OrdinalIgnoreCase)
        && !head.Contains("Template", StringComparison.OrdinalIgnoreCase)
        && !head.Contains("MPUV", StringComparison.OrdinalIgnoreCase);

    // The Golem's own port takes any S1 head: the dump marks it editable, and a
    // pilot fitted a Helix I in game (reported 2026-11-07), overturning RSI's
    // Q&A that called the Pitman bespoke. Nobody has yet fitted the Pitman to
    // another hull, so it is offered only where it is the stock head rather
    // than as a generic S1 upgrade everywhere.
    public static bool IsGolemOnly(string? head) =>
        string.Equals(head, "Mining_Laser_DRAK_Golem_S1", StringComparison.OrdinalIgnoreCase);

    public static bool Allows(string? stock, bool? editable, int size, string? candidate, int candidateSize) =>
        string.Equals(stock, candidate, StringComparison.OrdinalIgnoreCase)
        || (editable == true && !IsGolemOnly(candidate) && size == candidateSize);
}
