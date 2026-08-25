using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Quantumwake.OrgServer.Store;

namespace Quantumwake.OrgServer.Tests;

/// <summary>
/// The first-run problem: a standalone server with no configured admins would
/// have nobody able to approve the first org.
/// </summary>
/// <remarks>
/// Hosts its own server with an empty admin list, because the shared fixture
/// deliberately configures one - the fallback must only ever fire when nothing
/// was configured.
/// </remarks>
public class BootstrapTests : IAsyncLifetime
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-orgboot-{Guid.NewGuid():N}");

    private WebApplication _app = null!;

    public async Task InitializeAsync()
    {
        _app = OrgServerHost.Build(new OrgServerOptions
        {
            DataDirectory = _directory,
            Port = 0,
            Admins = [],
        });
        await _app.StartAsync();
    }

    private AccountStore Accounts => _app.Services.GetRequiredService<AccountStore>();

    [Fact]
    public void With_nothing_configured_the_first_account_is_the_admin_and_the_second_is_not()
    {
        var first = Accounts.UpsertIdentity("discord", "snowflake-1", "first");
        var second = Accounts.UpsertIdentity("discord", "snowflake-2", "second");

        Assert.True(Accounts.IsServerAdmin(first.Id));
        Assert.False(Accounts.IsServerAdmin(second.Id));
    }

    [Fact]
    public async Task A_configured_admin_list_disables_the_fallback()
    {
        // A sibling server whose config names an admin: its first walk-in is
        // nobody special.
        var directory = Path.Combine(Path.GetTempPath(), $"qw-orgboot2-{Guid.NewGuid():N}");
        var app = OrgServerHost.Build(new OrgServerOptions
        {
            DataDirectory = directory,
            Port = 0,
            Admins = ["snowflake-the-real-admin"],
        });
        try
        {
            var accounts = app.Services.GetRequiredService<AccountStore>();
            var walkIn = accounts.UpsertIdentity("discord", "snowflake-stranger", "stranger");
            var admin = accounts.UpsertIdentity("discord", "snowflake-the-real-admin", "the admin");

            Assert.False(accounts.IsServerAdmin(walkIn.Id));
            Assert.True(accounts.IsServerAdmin(admin.Id));
        }
        finally
        {
            await app.DisposeAsync();
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }
}
