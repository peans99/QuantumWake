using Microsoft.Extensions.Logging.Abstractions;
using Quantumwake.OrgServer.Store;

namespace Quantumwake.OrgServer.Tests;

public sealed class BackupTests(OrgServerUnderTest server) : IClassFixture<OrgServerUnderTest>
{
    [Fact]
    public void Backup_and_checked_restore_preserve_accounts()
    {
        var person = server.Person("backup-pilot");
        var root = Path.Combine(Path.GetTempPath(), $"qw-org-backup-{Guid.NewGuid():N}");
        var backup = Path.Combine(root, "backup.db");
        var restored = Path.Combine(root, "restored");
        try
        {
            server.Db.Backup(backup);
            OrgDb.Restore(restored, backup);
            var db = new OrgDb(restored);
            var accounts = new AccountStore(db, [], NullLogger<AccountStore>.Instance);
            Assert.NotNull(accounts.Get(person.AccountId));
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }
}
