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

    private const string Local = @"C:\Users\pilot\AppData\Local";
    private const string Family = "Nehkron.QuantumWake_6pqy8hx358xcy";
    private const string Diverted = Local + @"\Packages\" + Family + @"\LocalCache\Local\Quantumwake";

    /// <summary>
    /// Someone who only ever had the Store copy: Windows made the folder
    /// inside the package, and that is the one Explorer can open.
    /// </summary>
    [Fact]
    public void A_folder_windows_diverted_is_reported_where_it_really_is()
    {
        var shown = StoreInstall.Redirected(Local + @"\Quantumwake", Local, Family, p => p == Diverted);

        Assert.Equal(Diverted, shown);
    }

    [Fact]
    public void A_file_inside_a_diverted_folder_follows_it()
    {
        var file = Diverted + @"\controls\exports\layout_qw_exported.xml";

        var shown = StoreInstall.Redirected(Local + @"\Quantumwake\controls\exports\layout_qw_exported.xml", Local, Family, p => p == file);

        Assert.Equal(file, shown);
    }

    /// <summary>
    /// Someone who ran the download first: the folder already existed, Windows
    /// writes straight into it, and nothing is under the package to find.
    /// </summary>
    [Fact]
    public void A_folder_that_was_there_before_the_store_copy_is_left_alone()
    {
        var path = Local + @"\Quantumwake";

        Assert.Equal(path, StoreInstall.Redirected(path, Local, Family, _ => false));
    }

    /// <summary>Only local app data is diverted: <c>--data D:\qw</c> is where it says.</summary>
    [Theory]
    [InlineData(@"D:\qw")]
    [InlineData(@"C:\Users\pilot\AppData\LocalLow\Quantumwake")]
    [InlineData(Diverted)]
    public void A_path_windows_does_not_divert_is_left_alone(string path)
    {
        Assert.Equal(path, StoreInstall.Redirected(path, Local, Family, _ => true));
    }

    [Fact]
    public void An_unpackaged_copy_reports_the_path_it_asked_for()
    {
        var path = Local + @"\Quantumwake";

        Assert.Equal(path, StoreInstall.OnDisk(path));
        Assert.False(StoreInstall.Owns(path));
        Assert.Equal(path, StoreInstall.Redirected(path, Local, null, _ => true));
    }
}
