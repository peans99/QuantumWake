namespace Quantumwake.Core.GameData;

public static class IndustrialFit
{
    public static bool IsMiningCandidate(string head) =>
        !head.Contains("TEST", StringComparison.OrdinalIgnoreCase)
        && !head.Contains("Template", StringComparison.OrdinalIgnoreCase)
        && !head.Contains("MPUV", StringComparison.OrdinalIgnoreCase);

    // The dump marks the Pitman's slot editable, which is not permission to
    // fit a generic S1 head. RSI's Golem Q&A describes a bespoke head with
    // configurable modules: /comm-link/engineering/20509-Q-A-Drake-Golem.
    public static bool IsBespoke(string? head) =>
        string.Equals(head, "Mining_Laser_DRAK_Golem_S1", StringComparison.OrdinalIgnoreCase);

    public static string? FixedReason(string? stock) => IsBespoke(stock)
        ? "Fixed Pitman head — bespoke to the Golem. Mining modules remain configurable."
        : null;

    public static bool Allows(string? stock, bool? editable, int size, string? candidate, int candidateSize) =>
        string.Equals(stock, candidate, StringComparison.OrdinalIgnoreCase)
        || (editable == true && !IsBespoke(stock) && !IsBespoke(candidate) && size == candidateSize);
}
