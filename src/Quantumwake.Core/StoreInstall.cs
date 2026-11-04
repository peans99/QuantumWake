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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);
}
