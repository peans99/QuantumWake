using Quantumwake.Data;
using Quantumwake.OrgShared;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quantumwake.Server;

/// <summary>
/// This install's voice on the org server: every outbound org call goes
/// through here, and none goes out on a timer.
/// </summary>
/// <remarks>
/// <para>
/// The dashboard never talks to the org server itself. Three reasons, each
/// sufficient: the token lives in <see cref="OrgLink"/>'s file and a page that
/// held it would hand it to every LAN viewer; every mutation is then a POST to
/// the <em>local</em> server, which LanGuard already refuses off-machine; and
/// rows coming back can be decorated with what this install knows before the
/// page sees them.
/// </para>
/// <para>
/// Failure shape follows <see cref="UpdateCheck"/>: network trouble is logged
/// quietly and reported as a sentence with a time on it, never thrown at a
/// page. The status line says "could not reach ... at 14:02", not a stack
/// trace, and never a green dot it did not earn - last contact is only ever
/// the last user-asked call that succeeded.
/// </para>
/// </remarks>
public sealed class OrgClient(IHttpClientFactory factory, OrgLink link, ILogger<OrgClient> logger)
{
    private readonly Lock _gate = new();

    // The pending link is the one piece of state that must never reach the
    // page whole: the device secret is what stops someone who glimpsed the
    // code from racing this machine for the token.
    private OrgLinkStartResponse? _pending;

    private IReadOnlyList<OrgMembershipRow> _orgs = [];
    private DateTimeOffset? _lastContactAt;
    private string? _lastError;

    /// <summary>What the page gets to know. No token, no device secret, ever.</summary>
    public object Snapshot()
    {
        var state = link.Current;
        lock (_gate)
        {
            return new
            {
                configured = link.Configured,
                serverAddress = state.ServerAddress,
                linked = link.Linked,
                displayName = state.DisplayName,
                handle = state.Handle,
                linking = _pending is null || _pending.ExpiresAt <= DateTimeOffset.UtcNow
                    ? null
                    : new
                    {
                        code = _pending.Code,
                        verifyUrl = _pending.VerifyUrl,
                        expiresAt = _pending.ExpiresAt,
                        pollSeconds = _pending.PollSeconds,
                    },
                orgs = _orgs,
                activeOrgId = state.ActiveOrgId,
                lastContactAt = _lastContactAt,
                lastError = _lastError,
            };
        }
    }

    /// <summary>Asks the configured server for a link code to show the user.</summary>
    public async Task<string?> StartLinkAsync(string appVersion, CancellationToken token = default)
    {
        if (link.Current.ServerAddress is not { Length: > 0 } address)
            return "Set the server address first.";

        try
        {
            using var client = Client();
            using var response = await client.PostAsJsonAsync($"{address}/api/link/start",
                new OrgLinkStartRequest($"{Environment.MachineName} (Quantum Wake {appVersion})", appVersion),
                OrgWire.Json, token);
            response.EnsureSuccessStatusCode();

            var started = await response.Content.ReadFromJsonAsync<OrgLinkStartResponse>(OrgWire.Json, token);
            if (started is null)
                return Trouble(address, "it answered with something unreadable");

            lock (_gate)
            {
                _pending = started;
                Reached();
            }
            return null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogDebug(e, "Org link could not start.");
            return Trouble(address, "nothing was set up");
        }
    }

    /// <summary>
    /// One poll of the pending code: pending, approved, denied, expired, or
    /// unreachable. Approved stores the token and forgets the code.
    /// </summary>
    public async Task<string> CheckLinkAsync(CancellationToken token = default)
    {
        OrgLinkStartResponse? pending;
        lock (_gate) pending = _pending;

        if (pending is null || link.Current.ServerAddress is not { Length: > 0 } address)
            return "expired";

        try
        {
            using var client = Client();
            using var response = await client.PostAsJsonAsync($"{address}/api/link/poll",
                new OrgLinkPollRequest(pending.Code, pending.DeviceSecret), OrgWire.Json, token);
            response.EnsureSuccessStatusCode();

            var poll = await response.Content.ReadFromJsonAsync<OrgLinkPollResponse>(OrgWire.Json, token);
            if (poll is null)
                return "unreachable";

            lock (_gate) Reached();

            if (poll.Status == "approved" && poll.Token is { Length: > 0 })
            {
                link.CompleteLink(poll.Token, poll.Account?.DisplayName, poll.Account?.Handle);
                lock (_gate) _pending = null;
                await RefreshAsync(token);
            }
            else if (poll.Status is "denied" or "expired")
            {
                lock (_gate) _pending = null;
            }

            return poll.Status;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogDebug(e, "Org link poll failed.");
            lock (_gate) _lastError = Trouble(address, "the code may still be waiting");
            return "unreachable";
        }
    }

    /// <summary>Re-reads who this install is and which orgs it belongs to.</summary>
    public async Task<string?> RefreshAsync(CancellationToken token = default)
    {
        var state = link.Current;
        if (state.ServerAddress is not { Length: > 0 } address || state.Token is not { Length: > 0 })
            return "Not linked.";

        try
        {
            using var client = Client(state.Token);
            using var response = await client.GetAsync($"{address}/api/me", token);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // The token died on the server - revoked from the account
                // page, or the account was forgotten. Said plainly rather
                // than silently unlinking: deleting local state over one 401
                // would let a server hiccup log everyone out.
                lock (_gate) _lastError = "The server no longer recognises this link. Link the app again.";
                return _lastError;
            }

            response.EnsureSuccessStatusCode();
            var me = await response.Content.ReadFromJsonAsync<OrgMeResponse>(OrgWire.Json, token);
            if (me is null)
                return Trouble(address, "it answered with something unreadable");

            lock (_gate)
            {
                _orgs = me.Orgs;
                Reached();
            }

            // One org needs no ceremony; the switcher appears with the second.
            if (link.Current.ActiveOrgId is null && me.Orgs.Count == 1)
                link.SetActiveOrg(me.Orgs[0].Id);
            else if (link.Current.ActiveOrgId is { } active && me.Orgs.All(o => o.Id != active))
                link.SetActiveOrg(me.Orgs.Count == 1 ? me.Orgs[0].Id : null);

            return null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogDebug(e, "Org refresh failed.");
            return Trouble(address, "showing nothing rather than a stale guess");
        }
    }

    public async Task<string?> JoinAsync(string? code, CancellationToken token = default)
    {
        var state = link.Current;
        if (state.ServerAddress is not { Length: > 0 } address || state.Token is not { Length: > 0 })
            return "Link the app before joining an org.";

        try
        {
            using var client = Client(state.Token);
            using var response = await client.PostAsJsonAsync($"{address}/api/orgs/join",
                new OrgJoinRequest(code), OrgWire.Json, token);

            if (!response.IsSuccessStatusCode)
            {
                var problem = await response.Content.ReadFromJsonAsync<OrgProblem>(OrgWire.Json, token);
                return problem?.Message ?? "The server refused the invite code.";
            }

            lock (_gate) Reached();
            return await RefreshAsync(token);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogDebug(e, "Org join failed.");
            return Trouble(address, "nothing was joined");
        }
    }

    /// <summary>The active org's members, or the sentence saying why not.</summary>
    public async Task<(IReadOnlyList<OrgMemberRow>? Members, string? Problem)> MembersAsync(
        CancellationToken token = default)
    {
        var state = link.Current;
        if (state.ServerAddress is not { Length: > 0 } address || state.Token is not { Length: > 0 })
            return (null, "Not linked.");
        if (state.ActiveOrgId is not { Length: > 0 } org)
            return (null, "No org chosen.");

        try
        {
            using var client = Client(state.Token);
            var members = await client.GetFromJsonAsync<IReadOnlyList<OrgMemberRow>>(
                $"{address}/api/orgs/{Uri.EscapeDataString(org)}/members", OrgWire.Json, token);

            lock (_gate) Reached();
            return (members ?? [], null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogDebug(e, "Org members fetch failed.");
            return (null, Trouble(address, "showing nothing rather than a stale guess"));
        }
    }

    public void Unlink()
    {
        link.Unlink();
        lock (_gate)
        {
            _pending = null;
            _orgs = [];
            _lastError = null;
        }
    }

    private HttpClient Client(string? bearer = null)
    {
        // Not the "community" client: that one's registration promises none of
        // its requests carry an identifier, and every call here carries one.
        var client = factory.CreateClient("org");
        if (bearer is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return client;
    }

    private void Reached()
    {
        _lastContactAt = DateTimeOffset.UtcNow;
        _lastError = null;
    }

    private string Trouble(string address, string consequence)
    {
        var sentence = $"Could not reach {address} at {DateTimeOffset.Now:HH:mm} - {consequence}.";
        lock (_gate) _lastError = sentence;
        return sentence;
    }
}
