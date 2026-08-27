using System.Text.Json;

namespace Quantumwake.Tests;

/// <summary>
/// The report the Settings page offers to save, as the endpoint actually
/// builds it.
/// </summary>
/// <remarks>
/// The unit tests cover the scrubber; these cover the promise made around it.
/// The block tells a pilot there is no handle, no account id, no folder name
/// and no UEX key in the file - and that promise is only worth the test that
/// reads the whole document back and looks.
/// </remarks>
[Collection("server")]
public class DiagnosticsEndpointTests : IClassFixture<ServerUnderTest>
{
    private readonly ServerUnderTest _server;

    public DiagnosticsEndpointTests(ServerUnderTest server) => _server = server;

    /// <summary>
    /// The counts a bug report is answered with: how much was read, over what
    /// span, and what the parser could not make sense of.
    /// </summary>
    [Fact]
    public async Task The_report_carries_what_a_bug_report_needs()
    {
        var report = await _server.Get("/api/diagnostics");

        Assert.True(report.TryGetProperty("producer", out _));
        Assert.True(report.TryGetProperty("takenAt", out _));
        Assert.True(report.GetProperty("library").TryGetProperty("sessions", out _));
        Assert.True(report.GetProperty("parser").TryGetProperty("unread", out _));
        Assert.True(report.GetProperty("views").TryGetProperty("ships", out _));
        Assert.True(report.GetProperty("wipe").TryGetProperty("at", out _));
    }

    /// <summary>
    /// Whether keys are stored is a fact worth reporting - "UEX is on but has no
    /// keys" explains a page of blanks. The keys themselves never are.
    /// </summary>
    [Fact]
    public async Task Whether_uex_keys_exist_is_reported_but_never_the_keys()
    {
        await _server.Posted("/api/uex/credentials", new
        {
            token = "tok_live_do_not_leak_me",
            secret = "sec_live_do_not_leak_me",
        });

        var response = await _server.Client.GetAsync("/api/diagnostics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"uexKeysStored\":true", body);
        Assert.DoesNotContain("tok_live_do_not_leak_me", body);
        Assert.DoesNotContain("sec_live_do_not_leak_me", body);
    }

    /// <summary>
    /// The install path is left out entirely. It names a user folder on plenty
    /// of machines, and it answers no question a parser bug asks.
    /// </summary>
    [Fact]
    public async Task The_install_path_is_not_in_the_report()
    {
        var response = await _server.Client.GetAsync("/api/diagnostics");
        var body = await response.Content.ReadAsStringAsync();

        var install = JsonDocument.Parse(body).RootElement.GetProperty("install");

        Assert.True(install.TryGetProperty("found", out _));
        Assert.False(install.TryGetProperty("rootPath", out _));
        Assert.DoesNotContain("Users", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_server.DataDirectory, body);
    }

    /// <summary>
    /// No field anywhere in the document is named for the things the block
    /// promises are absent. A field added later that carries one arrives here as
    /// a failure rather than as a quiet leak.
    /// </summary>
    [Fact]
    public async Task No_field_in_the_report_is_named_for_an_identity()
    {
        var response = await _server.Client.GetAsync("/api/diagnostics");
        var body = await response.Content.ReadAsStringAsync();

        foreach (var forbidden in new[] { "handle", "geid", "accountId", "secret", "token" })
            Assert.DoesNotContain($"\"{forbidden}\":", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An install with nothing parsed answers with zeroes rather than failing -
    /// somebody whose logs were never read is exactly who needs to send a
    /// report.
    /// </summary>
    [Fact]
    public async Task An_install_with_no_logs_still_produces_a_report()
    {
        var report = await _server.Get("/api/diagnostics");

        Assert.False(report.GetProperty("install").GetProperty("found").GetBoolean());
        Assert.Equal(0, report.GetProperty("parser").GetProperty("unread").GetInt32());
    }
}
