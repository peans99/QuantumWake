namespace Quantumwake.Core;

/// <summary>
/// Where Quantum Wake keeps everything it owns: caches, digests, jobs and
/// settings.
/// </summary>
/// <remarks>
/// <para>
/// One place decides this so it can be moved wholesale. Passing
/// <c>--data &lt;folder&gt;</c> (or setting <c>QUANTUMWAKE_DATA</c>) sends every
/// store somewhere else, which makes a genuinely fresh first run testable
/// without disturbing the real one - the setup wizard, an empty cache, no
/// jobs, nothing enabled - and makes a portable copy on a stick possible for
/// free.
/// </para>
/// <para>
/// Set once, before anything reads it. Every store falls back to this when it
/// is not given a folder of its own.
/// </para>
/// </remarks>
public static class AppPaths
{
    private static string? _root;

    /// <summary>The data folder, defaulting to local app data.</summary>
    public static string Root => _root ??=
        Environment.GetEnvironmentVariable("QUANTUMWAKE_DATA") is { Length: > 0 } fromEnvironment
            ? fromEnvironment
            : DefaultRoot();

    /// <summary>The data folder when nothing has moved it.</summary>
    /// <remarks>
    /// <c>DoNotVerify</c>, because on Linux an unverified lookup answers an empty
    /// string when <c>~/.local/share</c> does not exist yet, which on a fresh
    /// account it may not. The data folder then came out as the relative
    /// <c>Quantumwake</c>, and the app kept a separate history in every folder
    /// it was started from. Windows always has a LocalAppData, so nothing moves
    /// there.
    /// </remarks>
    internal static string DefaultRoot() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            "Quantumwake");

    /// <summary>True when the data folder was moved off its default.</summary>
    public static bool IsRelocated { get; private set; }

    /// <summary>A folder inside the data folder, created on demand by its owner.</summary>
    public static string In(params string[] parts) =>
        Path.Combine([Root, .. parts]);

    /// <summary>
    /// Where what was read out of one game channel's files is kept: names,
    /// commodities, models, pictures, and what was written into its folder.
    /// </summary>
    /// <remarks>
    /// LIVE keeps the data folder itself, so nothing anyone already has moves.
    /// Every other channel gets <c>channels\&lt;name&gt;</c>. The caches are
    /// stamped with the archive's write time, so sharing one file between LIVE
    /// and PTU would not show wrong answers - it would throw the other's away on
    /// every switch, and rebuilding the commodity table alone reads 316 MB.
    /// </remarks>
    public static string ForChannel(string? installRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
            return Root;

        // Either separator: a Windows path read off the launcher reaches the
        // Linux build too, where GetFileName would not split it.
        var trimmed = installRoot.TrimEnd('\\', '/');
        var name = trimmed[(trimmed.LastIndexOfAny(['\\', '/']) + 1)..];
        var safe = new string([.. name.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')]);

        return safe.Length == 0 || safe.Equals("LIVE", StringComparison.OrdinalIgnoreCase)
            ? Root
            : In("channels", safe.ToUpperInvariant());
    }

    /// <summary>
    /// Points every store at another folder. Reads <c>--data &lt;folder&gt;</c>
    /// from the command line; anything else leaves the default alone.
    /// </summary>
    public static void UseFromArguments(string[] args)
    {
        var index = Array.IndexOf(args, "--data");

        if (index >= 0 && index + 1 < args.Length && args[index + 1].Length > 0)
            Use(args[index + 1]);
    }

    public static void Use(string folder)
    {
        _root = Path.GetFullPath(folder);
        IsRelocated = true;
        Directory.CreateDirectory(_root);
    }
}
