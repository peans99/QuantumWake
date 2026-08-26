using Quantumwake.Data;

namespace Quantumwake.Tests;

public sealed class OrgLinkTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"qw-org-link-{Guid.NewGuid():N}");

    [Fact]
    public void Public_addresses_require_https_and_lan_http_is_an_explicit_exception()
    {
        var link = new OrgLink(_directory);
        Assert.Null(link.Configure("https://org.example"));
        Assert.Contains("must use HTTPS", link.Configure("http://org.example", allowInsecureHttp: true));
        Assert.Contains("Tick the LAN-only exception", link.Configure("http://192.168.1.20:8321"));
        Assert.Null(link.Configure("http://192.168.1.20:8321", allowInsecureHttp: true));
        Assert.Null(link.Configure("http://localhost:8321"));
    }

    [Fact]
    public void A_saved_token_round_trips_through_the_local_secret_store()
    {
        var link = new OrgLink(_directory);
        Assert.Null(link.Configure("https://org.example"));
        link.CompleteLink("qwo_secret", "Pilot", "pilot");

        var reloaded = new OrgLink(_directory);
        Assert.Equal("qwo_secret", reloaded.Current.Token);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { }
    }
}
