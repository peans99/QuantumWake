namespace Quantumwake.Core.GameData;

/// <summary>
/// Finds the class a hull was reworked into when the original has gone from the install.
/// </summary>
/// <remarks>
/// A "gold standard" rework arrives as a new class beside the old one -
/// RSI_Constellation_Andromeda_GS - and the old one is later deleted. 4.10.3
/// deleted the Andromeda, Aquila, Phoenix Emerald and Hammerhead originals, while
/// every log written before it names them: 450 lines of one install's history
/// named the Andromeda. The name survives in the game's text file, but its size,
/// hold, icon and model are read from the record, so without this those sessions
/// would lose them. The rework is the same hull, which is why it is a fair stand-in;
/// a class that still exists is always used as it is.
/// </remarks>
public static class GoldStandard
{
    /// <summary>
    /// The <c>_GS</c> class standing in for <paramref name="cls"/>, or null when
    /// there is none. NPC variants keep their suffix after the marker:
    /// RSI_Constellation_Andromeda_PU_AI_CRIM becomes
    /// RSI_Constellation_Andromeda_GS_PU_AI_CRIM, so every split point is tried,
    /// longest prefix first.
    /// </summary>
    public static string? Successor(string? cls, Func<string, bool> exists)
    {
        if (string.IsNullOrEmpty(cls) || IsRework(cls)) return null;

        for (var at = cls.Length; at > 0; at = cls.LastIndexOf('_', at - 1))
        {
            var candidate = string.Concat(cls.AsSpan(0, at), "_GS", cls.AsSpan(at));
            if (exists(candidate)) return candidate;
        }
        return null;
    }

    private static bool IsRework(string cls) =>
        cls.EndsWith("_GS", StringComparison.OrdinalIgnoreCase)
        || cls.Contains("_GS_", StringComparison.OrdinalIgnoreCase);
}
