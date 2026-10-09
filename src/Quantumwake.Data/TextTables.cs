namespace Quantumwake.Data;

/// <summary>
/// Compares a loose <c>global.ini</c> with the game's own, and fills the gaps.
/// </summary>
/// <remarks>
/// <para>
/// A loose localisation file replaces the game's table rather than adding to
/// it, and nothing updates it when the game patches. 4.10.2 added 521 strings -
/// every RSI Discovery Month contract title among them - and a text file written
/// before the patch has none of them. The game then has nothing to show for
/// those keys but the keys themselves.
/// </para>
/// <para>
/// So the comparison is by key, and the fill takes the game's own English for
/// whatever the file lacks. A text mod's wording is never replaced: only keys
/// it does not have at all are added.
/// </para>
/// </remarks>
public static class TextTables
{
    /// <summary>Every key a table defines: the text before the first '=', BOM and spaces trimmed.</summary>
    public static HashSet<string> Keys(string ini)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in Lines(ini))
            if (KeyOf(line) is { } key) keys.Add(key);

        return keys;
    }

    /// <summary>The game's lines whose key the table does not define, in the game's order.</summary>
    public static IReadOnlyList<string> Missing(string table, string game)
    {
        var have = Keys(table);
        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in Lines(game))
        {
            if (KeyOf(line) is { } key && !have.Contains(key) && seen.Add(key))
                missing.Add(line);
        }

        return missing;
    }

    /// <summary>The table with the game's missing lines added at the end.</summary>
    public static string WithMissing(string table, string game, out int added)
    {
        var missing = Missing(table, game);
        added = missing.Count;
        if (added == 0) return table;

        var joined = string.Join("\r\n", missing);
        return table.EndsWith('\n') ? table + joined + "\r\n" : table + "\r\n" + joined + "\r\n";
    }

    /// <summary>The key a line defines, or null for a blank, a comment or a line with no '='.</summary>
    public static string? KeyOf(string line)
    {
        var eq = line.IndexOf('=');
        if (eq <= 0) return null;

        var key = line[..eq].TrimStart('﻿').Trim();
        return key.Length == 0 || key.StartsWith(';') ? null : key;
    }

    private static IEnumerable<string> Lines(string ini) =>
        ini.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0);
}
