using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// What the LAN is allowed to do when the dashboard is opened up.
/// </summary>
/// <remarks>
/// <c>-Lan</c> binds every interface, and nothing behind it has a login. The
/// feature it exists for is a tablet showing the dashboard, so reads pass and
/// everything else does not - which matters because the API behind that port
/// can store UEX credentials, write into the game folder and move the line the
/// history is counted from.
/// </remarks>
public class LanGuardTests
{
    [Theory]
    [InlineData("GET", "/api/sessions")]
    [InlineData("GET", "/")]
    [InlineData("HEAD", "/app.js")]
    [InlineData("OPTIONS", "/api/market")]
    [InlineData("get", "/api/crew")]
    public void Reading_is_allowed(string method, string path)
    {
        Assert.True(LanGuard.AllowsFromElsewhere(method, path));
    }

    /// <summary>
    /// The endpoints this rule exists for. Every one of them changes something
    /// on the machine running the app, and none of them asks who is calling.
    /// </summary>
    [Theory]
    [InlineData("POST", "/api/uex/credentials")]
    [InlineData("POST", "/api/starstrings/install")]
    [InlineData("POST", "/api/wipe")]
    [InlineData("POST", "/api/scan")]
    [InlineData("POST", "/api/uex/push")]
    [InlineData("POST", "/api/uex/auto/answer")]
    [InlineData("PUT", "/api/jobs")]
    [InlineData("DELETE", "/api/jobs/1")]
    [InlineData("PATCH", "/api/trips")]
    // Export is a POST for exactly this line. It is the one endpoint that hands
    // over the whole history at once, in a file built for keeping, so it must
    // not be reachable from the tablet the read-only rule exists to serve.
    [InlineData("POST", "/api/export")]
    [InlineData("POST", "/api/imports")]
    public void Changing_anything_is_refused(string method, string path)
    {
        Assert.False(LanGuard.AllowsFromElsewhere(method, path));
    }

    /// <summary>
    /// The live feed is the other half of a second screen, and SignalR
    /// negotiates over POST. LiveHub declares no callable methods, so it only
    /// ever broadcasts outwards.
    /// </summary>
    [Theory]
    [InlineData("/hub")]
    [InlineData("/hub/live")]
    [InlineData("/hub/live/negotiate")]
    public void The_broadcast_hub_still_works(string path)
    {
        Assert.True(LanGuard.AllowsFromElsewhere("POST", path));
    }

    /// <summary>
    /// Matched on whole segments. A plain StartsWith would admit anything
    /// beginning with those four characters, which is how a path allow-list
    /// becomes the hole it was meant to close.
    /// </summary>
    [Theory]
    [InlineData("/hubbub")]
    [InlineData("/hub-admin/install")]
    [InlineData("/api/hub")]
    public void A_path_merely_starting_with_hub_is_not_the_hub(string path)
    {
        Assert.False(LanGuard.AllowsFromElsewhere("POST", path));
    }

    /// <summary>
    /// An unknown verb is not a read. The rule whitelists what is safe rather
    /// than blacklisting what is not, so anything unrecognised is refused.
    /// </summary>
    [Theory]
    [InlineData("TRACE")]
    [InlineData("CONNECT")]
    [InlineData("")]
    public void Anything_unrecognised_is_refused(string method)
    {
        Assert.False(LanGuard.AllowsFromElsewhere(method, "/api/sessions"));
    }
}
