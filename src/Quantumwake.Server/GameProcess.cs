using System.Diagnostics;

namespace Quantumwake.Server;

/// <summary>
/// Whether Star Citizen is running on this machine.
/// </summary>
/// <remarks>
/// On Windows the process is <c>StarCitizen</c>, the way .NET names one. Under
/// Wine the name Linux sees is the executable's with its extension, and the
/// kernel cuts a process name at 15 characters: <c>StarCitizen.exe</c> is
/// exactly that long, so both the full and the cut name are the same string.
/// Not measured with the game running (docs/linux.md); the match is written
/// for what /proc is documented to hold.
/// </remarks>
internal static class GameProcess
{
    public static bool IsRunning()
    {
        if (OperatingSystem.IsWindows())
            return Process.GetProcessesByName("StarCitizen").Length > 0;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = process.ProcessName;

                if (name.Equals("StarCitizen", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("StarCitizen.exe", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch (InvalidOperationException)
            {
                // Gone between the listing and the question.
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }
}
