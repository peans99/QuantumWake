using Quantumwake.Core;

namespace Quantumwake.Tests;

/// <summary>
/// The Store-copy test runs on every start, on Windows and Linux, and decides
/// whether the one-click update is offered. A test run is never packaged, so
/// this pins the ordinary answer and that asking never throws - a missing
/// entry point or a Linux host must read as "not the Store", not as a crash.
/// </summary>
public class StoreInstallTests
{
    [Fact]
    public void An_unpackaged_process_is_not_a_store_install()
    {
        Assert.False(StoreInstall.Is);
    }
}
