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
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Quantumwake");

    /// <summary>True when the data folder was moved off its default.</summary>
    public static bool IsRelocated { get; private set; }

    /// <summary>A folder inside the data folder, created on demand by its owner.</summary>
    public static string In(params string[] parts) =>
        Path.Combine([Root, .. parts]);

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
