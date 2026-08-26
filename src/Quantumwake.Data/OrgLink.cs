using Quantumwake.Core;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Quantumwake.Data;

public sealed record OrgLinkState(
    string? ServerAddress, string? Token, string? DisplayName, string? Handle,
    DateTimeOffset? LinkedAt, string? ActiveOrgId, bool AllowInsecureHttp = false);

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
    public string? Configure(string? address, bool allowInsecureHttp = false)
    {
        address = address?.Trim().TrimEnd('/');
        var problem = ValidateAddress(address, allowInsecureHttp);
        if (problem is not null) return problem;

        lock (_gate)
        {
            if (_state.ServerAddress != address)
                _state = new OrgLinkState(address, null, null, null, null, null, allowInsecureHttp);
            else
                _state = _state with { AllowInsecureHttp = allowInsecureHttp };
            Save();
        }

        return null;
    }

    public static string? ValidateAddress(string? address, bool allowInsecureHttp = false)
    {
        address = address?.Trim().TrimEnd('/');
        if (address is not { Length: > 0 })
            return "An address is needed - ask your org which server it uses.";
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            return "That is not an http(s) address.";
        if (uri.Scheme == "http" && !IsLoopback(uri))
        {
            if (!IsLanHost(uri.Host))
                return "A public org server must use HTTPS. HTTP is only accepted for a local network address.";
            if (!allowInsecureHttp)
                return "That LAN address uses unencrypted HTTP. Tick the LAN-only exception if that is intentional.";
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
            _state = new OrgLinkState(_state.ServerAddress, null, null, null, null, null,
                _state.AllowInsecureHttp);
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
            {
                var stored = JsonSerializer.Deserialize<OrgLinkState>(File.ReadAllText(_path)) ?? Empty;
                return stored with { Token = LocalSecret.Unprotect(stored.Token) };
            }
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            // Falling back to unlinked is the safe direction: the person
            // relinks in a minute, whereas a guessed token acts as somebody.
        }

        return Empty;
    }

    private void Save()
    {
        var stored = _state with { Token = LocalSecret.Protect(_state.Token) };
        File.WriteAllText(_path, JsonSerializer.Serialize(stored));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static bool IsLoopback(Uri uri) =>
        uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private static bool IsLanHost(string host)
    {
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || !host.Contains('.'))
            return true;
        if (!System.Net.IPAddress.TryParse(host, out var ip))
            return false;
        var bytes = ip.GetAddressBytes();
        return ip.IsIPv6LinkLocal || (bytes.Length == 16 && (bytes[0] & 0xfe) == 0xfc)
            || (bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 127
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)));
    }

    private static readonly OrgLinkState Empty = new(null, null, null, null, null, null);
}

/// <summary>DPAPI on Windows; owner-only file permissions on Unix.</summary>
internal static class LocalSecret
{
    public static string? Protect(string? value)
    {
        if (value is not { Length: > 0 } || value.StartsWith("dpapi:", StringComparison.Ordinal))
            return value;
        if (!OperatingSystem.IsWindows())
            return value;

        var bytes = Encoding.UTF8.GetBytes(value);
        var input = Blob(bytes.Length, Marshal.AllocHGlobal(bytes.Length));
        Marshal.Copy(bytes, 0, input.Data, bytes.Length);
        try
        {
            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out var output))
                // Some service/test identities have no loadable DPAPI profile.
                // Their app-data directory ACL remains the available boundary;
                // refusing to save would strand the link after approval.
                return value;
            try
            {
                var protectedBytes = new byte[output.Length];
                Marshal.Copy(output.Data, protectedBytes, 0, output.Length);
                return "dpapi:" + Convert.ToBase64String(protectedBytes);
            }
            finally { LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(input.Data); }
    }

    public static string? Unprotect(string? value)
    {
        if (value is not { Length: > 0 } || !value.StartsWith("dpapi:", StringComparison.Ordinal))
            return value; // Older files migrate on the next save.
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var bytes = Convert.FromBase64String(value[6..]);
            var input = Blob(bytes.Length, Marshal.AllocHGlobal(bytes.Length));
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            try
            {
                if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out var output))
                    return null;
                try
                {
                    var clear = new byte[output.Length];
                    Marshal.Copy(output.Data, clear, 0, output.Length);
                    return Encoding.UTF8.GetString(clear);
                }
                finally { LocalFree(output.Data); }
            }
            finally { Marshal.FreeHGlobal(input.Data); }
        }
        catch (FormatException) { return null; }
    }

    private static DATA_BLOB Blob(int length, IntPtr data) => new() { Length = length, Data = data };

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB { public int Length; public IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DATA_BLOB input, string? description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DATA_BLOB output);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB input, IntPtr description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DATA_BLOB output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
