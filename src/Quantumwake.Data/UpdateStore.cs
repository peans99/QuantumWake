using System.Text.Json;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>What the player has said about update checks, and what one found.</summary>
/// <param name="Asked">
/// Whether they have been asked at all. The question is put once, not on every
/// launch: a prompt that returns after being answered is nagging.
/// </param>
/// <param name="Automatic">Check at startup, without asking again.</param>
/// <param name="LastCheckedAt">When a check last ran, so the page can say.</param>
/// <param name="LastSeenVersion">The newest release seen, remembered between runs.</param>
public sealed record UpdatePreference(
    bool Asked = false,
    bool Automatic = false,
    DateTimeOffset? LastCheckedAt = null,
    string? LastSeenVersion = null);

/// <summary>
/// Whether this copy is allowed to look for a newer one.
/// </summary>
/// <remarks>
/// <para>
/// The app's standing promise is that it connects to the internet only when
/// asked. An update check is a connection, so it is a choice rather than a
/// default: the player is asked once, on a start, and their answer is kept
/// here. Nothing is sent - the check is a plain GET of a public release feed -
/// but "we only send a version number" is still a sentence nobody should have
/// to take on trust when they can simply say no.
/// </para>
/// <para>
/// Kept beside the other authored settings, in its own file, for the same
/// reason: a preference deliberately chosen should not share a file with
/// something rewritten dozens of times a session.
/// </para>
/// </remarks>
public sealed class UpdateStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private UpdatePreference _preference = new();

    public UpdateStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "updates.json");
        Load();
    }

    public UpdatePreference Current
    {
        get { lock (_gate) return _preference; }
    }

    /// <summary>Records the answer to the question, whichever way it went.</summary>
    /// <remarks>
    /// Answering at all sets <see cref="UpdatePreference.Asked"/>, including a
    /// refusal: "no" is an answer, and asking again next launch would be asking
    /// someone to say no repeatedly.
    /// </remarks>
    public UpdatePreference Answer(bool automatic)
    {
        lock (_gate)
        {
            _preference = _preference with { Asked = true, Automatic = automatic };
            Save();
            return _preference;
        }
    }

    /// <summary>Notes that a check ran, and what it found.</summary>
    public UpdatePreference Checked(string? latestVersion)
    {
        lock (_gate)
        {
            _preference = _preference with
            {
                LastCheckedAt = DateTimeOffset.UtcNow,
                LastSeenVersion = latestVersion ?? _preference.LastSeenVersion,
            };

            Save();
            return _preference;
        }
    }

    /// <summary>
    /// Whether a published tag is actually ahead of what is running.
    /// </summary>
    /// <remarks>
    /// A version comparison, never a string one: "0.10.0" sorts below "0.9.0"
    /// as text, and a check that says "you are current" the day after a release
    /// is worse than no check. Tags carry a leading v and local builds carry a
    /// +commit suffix, so both fall away first. Anything unparseable is treated
    /// as "no news" - offering an update on the strength of a tag nobody can
    /// read is how someone ends up downloading a nightly.
    /// </remarks>
    public static bool IsNewer(string? current, string? published)
    {
        var here = ParseVersion(current);
        var there = ParseVersion(published);

        return here is not null && there is not null && there > here;
    }

    /// <summary>"v0.6.0" or "0.6.0+abc1234" as a version, or null.</summary>
    public static Version? ParseVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim().TrimStart('v', 'V').Split('+', '-')[0];

        return Version.TryParse(trimmed, out var version) ? version : null;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _preference = JsonSerializer.Deserialize<UpdatePreference>(File.ReadAllText(_path)) ?? new();
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // A corrupt file leaves the app asking again, which is the safe way
            // to be wrong: it never turns a check on that nobody agreed to.
            _preference = new UpdatePreference();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_preference));
    }
}
