namespace Quantumwake.Core.Logging;

/// <summary>
/// Remembers a Star Citizen folder the user pointed us at.
/// </summary>
/// <remarks>
/// Detection scans every drive for the layouts the launcher produces, but the
/// launcher will install into any folder a player names, and nothing on disk
/// has to look familiar. When the search comes up empty the only honest answer
/// is to ask - and to remember the answer, which is what this file is.
/// </remarks>
public static class InstallPathStore
{
    private static string Path0 => Path.Combine(AppPaths.Root, "install-path.txt");

    /// <summary>The remembered folder, or null when none was ever set.</summary>
    public static string? Load()
    {
        try
        {
            if (!File.Exists(Path0))
                return null;

            var path = File.ReadAllText(Path0).Trim();
            return path.Length > 0 ? path : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Remembers a folder, or forgets it when given nothing. Returns the
    /// install the path resolves to, so a caller can refuse a bad answer
    /// before it is written.
    /// </summary>
    public static GameInstall? Save(string? path)
    {
        var directory = Path.GetDirectoryName(Path0)!;
        Directory.CreateDirectory(directory);

        if (string.IsNullOrWhiteSpace(path))
        {
            if (File.Exists(Path0))
                File.Delete(Path0);

            return null;
        }

        var install = GameInstallLocator.FromPath(path.Trim());
        if (install is null)
            return null;

        File.WriteAllText(Path0, path.Trim());
        return install;
    }
}
