using System.Runtime.InteropServices;

namespace Quantumwake.Core;

/// <summary>
/// Whether this copy was installed from the Microsoft Store.
/// </summary>
/// <remarks>
/// <para>
/// The Store copy is the same executable inside an MSIX package, and the
/// package is the only difference the process can see: Windows gives it a
/// package identity, which a downloaded exe or the MSI never has. Asking for
/// that identity is the documented test, and it needs no Windows Runtime
/// projection, which the server's cross-platform target does not have.
/// </para>
/// <para>
/// It matters because the package folder under <c>WindowsApps</c> is read-only
/// and Store policy keeps packaged apps from updating themselves, so the
/// one-click update has to stand down and send the reader to the Store.
/// </para>
/// </remarks>
public static class StoreInstall
{
    private const int ErrorInsufficientBuffer = 122;

    private static readonly Lazy<bool> Packaged = new(Ask);

    /// <summary>True when running from a Store (MSIX) package.</summary>
    public static bool Is => Packaged.Value;

    private static bool Ask()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            // A zero-length buffer: a packaged process is told its name does
            // not fit, an unpackaged one is told it has no package (15700).
            uint length = 0;
            return GetCurrentPackageFullName(ref length, IntPtr.Zero) == ErrorInsufficientBuffer;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Where Windows really keeps a file or folder this copy asked for, which
    /// for a Store copy is not always where it asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Store refused the capability that switches file-system
    /// virtualization off (policy 10.6.3, November 2026), so a packaged copy
    /// runs with it on. Probed on Windows 11 build 26300 with a registered
    /// test package: when a top-level folder under local app data already
    /// exists, as it does for anyone who ran the download first, every write
    /// into it lands in the real folder - new files, appends, replacements,
    /// deletes, new subfolders. When it does not exist, creating it makes it
    /// under <c>Packages\&lt;family&gt;\LocalCache\Local</c> instead, and only
    /// the app sees it at the path it asked for.
    /// </para>
    /// <para>
    /// So a path the app shows a Store-only user points at nothing in
    /// Explorer. This answers the one that does, and it asks the disk rather
    /// than guessing which of the two cases this is.
    /// </para>
    /// </remarks>
    public static string OnDisk(string path) =>
        Is ? Redirected(path, LocalAppData(), FamilyName.Value, p => Directory.Exists(p) || File.Exists(p)) : path;

    /// <summary>True when this copy's data folder is the package's own, which uninstalling deletes.</summary>
    public static bool Owns(string path) =>
        !string.Equals(OnDisk(path), path, StringComparison.OrdinalIgnoreCase);

    internal static string Redirected(string path, string localAppData, string? family, Func<string, bool> exists)
    {
        if (string.IsNullOrEmpty(family) || string.IsNullOrEmpty(localAppData))
            return path;

        var root = localAppData.TrimEnd('\\') + "\\";
        var packages = root + "Packages\\";

        // Only local app data is diverted, and the package folders in it are
        // already the real thing.
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(packages, StringComparison.OrdinalIgnoreCase))
            return path;

        var diverted = $"{packages}{family}\\LocalCache\\Local\\{path[root.Length..]}";
        return exists(diverted) ? diverted : path;
    }

    private static string LocalAppData() =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);

    private static readonly Lazy<string?> FamilyName = new(() =>
    {
        try
        {
            uint length = 0;
            if (GetCurrentPackageFamilyName(ref length, null) != ErrorInsufficientBuffer || length == 0)
                return null;

            // The length counts the terminating null.
            var buffer = new char[length];
            return GetCurrentPackageFamilyName(ref length, buffer) == 0 ? new string(buffer, 0, (int)length - 1) : null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    });

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFamilyName(ref uint packageFamilyNameLength, [Out] char[]? packageFamilyName);
}
