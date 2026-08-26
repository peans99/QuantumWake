using System.Net;

namespace Quantumwake.OrgServer.Tests;

public sealed class HostValidationTests
{
    [Fact]
    public void A_public_http_address_is_refused()
    {
        var options = new OrgServerOptions
        {
            DataDirectory = Path.Combine(Path.GetTempPath(), $"qw-org-invalid-{Guid.NewGuid():N}"),
            PublicBaseUrl = "http://org.example.net",
        };

        var error = Assert.Throws<InvalidOperationException>(() => OrgServerHost.Build(options));

        Assert.Contains("must use HTTPS", error.Message);
        Assert.False(Directory.Exists(options.DataDirectory));
    }

    [Fact]
    public void Forwarded_headers_need_an_explicit_trusted_proxy()
    {
        var options = new OrgServerOptions
        {
            DataDirectory = Path.Combine(Path.GetTempPath(), $"qw-org-invalid-{Guid.NewGuid():N}"),
            PublicBaseUrl = "https://org.example.net",
            BehindProxy = true,
        };

        var error = Assert.Throws<InvalidOperationException>(() => OrgServerHost.Build(options));

        Assert.Contains("TrustedProxies", error.Message);
        Assert.False(Directory.Exists(options.DataDirectory));
    }

    [Fact]
    public async Task A_named_trusted_proxy_is_accepted()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"qw-org-proxy-{Guid.NewGuid():N}");
        var app = OrgServerHost.Build(new OrgServerOptions
        {
            DataDirectory = directory,
            Port = 0,
            PublicBaseUrl = "https://org.example.net",
            BehindProxy = true,
            TrustedProxies = [IPAddress.Loopback],
        });

        try
        {
            await app.StartAsync();
        }
        finally
        {
            await app.DisposeAsync();
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }
}
