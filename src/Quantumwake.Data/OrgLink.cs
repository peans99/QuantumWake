using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

public sealed record OrgLinkState(
    string? ServerAddress, string? Token, string? DisplayName, string? Handle,
    DateTimeOffset? LinkedAt, string? ActiveOrgId);

/// <summary>
/// This install's relationship with an org server: which one, and as whom.
/// </summary>
/// <remarks>
/// <para>
/// One file, <c>org/link.json</c>, beside the UEX credentials and with the
/// same threat model: local app data, holding a key that acts as the user
/// somewhere else. Linked means the token is present in that file - the same
/// "the file is the state" honesty as the UEX feeds, with no separate flag to
/// fall out of step.
/// </para>
/// <para>
/// The token never leaves this store except in an Authorization header to the
/// configured server. It is never handed to the browser: a page that held it
/// would hand it to every LAN viewer too.
/// </para>
/// </remarks>
public sealed class OrgLink
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private OrgLinkState _state;

    public OrgLink() : this(AppPaths.In("org")) { }

    public OrgLink(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "link.json");
        _state = Load();
    }

    public OrgLinkState Current
    {
        get { lock (_gate) return _state; }
    }

    public bool Configured => Current.ServerAddress is { Length: > 0 };
    public bool Linked => Current.Token is { Length: > 0 };

    /// <summary>Points at a server. Changing servers unlinks - the token was minted by the old one.</summary>
    public string? Configure(string? address)
    {
        address = address?.Trim().TrimEnd('/');

        if (address is not { Length: > 0 })
            return "An address is needed - ask your org which server it uses.";

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            return "That is not an http(s) address.";

        lock (_gate)
        {
            if (_state.ServerAddress != address)
                _state = new OrgLinkState(address, null, null, null, null, null);
            Save();
        }

        return null;
    }

    public void CompleteLink(string token, string? displayName, string? handle)
    {
        lock (_gate)
        {
            _state = _state with
            {
                Token = token,
                DisplayName = displayName,
                Handle = handle,
                LinkedAt = DateTimeOffset.UtcNow,
            };
            Save();
        }
    }

    /// <summary>Forgets the token locally. The server's copy dies from its account page.</summary>
    public void Unlink()
    {
        lock (_gate)
        {
            _state = new OrgLinkState(_state.ServerAddress, null, null, null, null, null);
            Save();
        }
    }

    public void SetActiveOrg(string? orgId)
    {
        lock (_gate)
        {
            _state = _state with { ActiveOrgId = orgId };
            Save();
        }
    }

    private OrgLinkState Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<OrgLinkState>(File.ReadAllText(_path)) ?? Empty;
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            // Falling back to unlinked is the safe direction: the person
            // relinks in a minute, whereas a guessed token acts as somebody.
        }

        return Empty;
    }

    private void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_state));

    private static readonly OrgLinkState Empty = new(null, null, null, null, null, null);
}
