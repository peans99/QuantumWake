using Quantumwake.OrgServer.Store;

namespace Quantumwake.OrgServer;

internal static class OrgMaintenance
{
    public static bool TryRun(string[] args, OrgServerOptions options)
    {
        var backup = Value(args, "Backup");
        var restore = Value(args, "Restore");
        if (backup is not null && restore is not null)
            throw new ArgumentException("Choose --Backup or --Restore, not both.");
        if (backup is not null)
        {
            new OrgDb(options.DataDirectory, options.Journal).Backup(backup);
            Console.WriteLine($"Backed up org.db to {Path.GetFullPath(backup)}");
            return true;
        }
        if (restore is not null)
        {
            OrgDb.Restore(options.DataDirectory, restore);
            Console.WriteLine($"Restored {Path.GetFullPath(restore)} into {options.DataDirectory}");
            return true;
        }
        return false;
    }

    private static string? Value(string[] args, string key)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals($"--{key}", StringComparison.OrdinalIgnoreCase))
                return i + 1 < args.Length ? args[i + 1] : throw new ArgumentException($"--{key} needs a file path.");
            if (args[i].StartsWith($"--{key}=", StringComparison.OrdinalIgnoreCase))
                return args[i][(key.Length + 3)..];
        }
        return null;
    }
}
