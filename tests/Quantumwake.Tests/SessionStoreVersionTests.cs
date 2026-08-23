using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The fingerprint that decides whether a log needs re-reading.
/// </summary>
public class SessionStoreVersionTests
{
    /// <summary>
    /// A summary parsed before a field existed keeps its stale payload for ever
    /// unless the fingerprint itself changes with the parser. Medical beds shipped
    /// invisible to every existing install for exactly this reason.
    /// </summary>
    [Fact]
    public void The_fingerprint_carries_a_payload_version()
    {
        var file = new FileInfo(Path.Combine(Path.GetTempPath(), $"qw-fp-{Guid.NewGuid():N}.log"));
        File.WriteAllText(file.FullName, "a line");
        file.Refresh();

        try
        {
            var fingerprint = SessionStore.Fingerprint(file);

            Assert.StartsWith("v", fingerprint);
            Assert.Contains($":{file.Length}:", fingerprint);
        }
        finally
        {
            file.Delete();
        }
    }

    /// <summary>Same file, same answer: an unchanged backup is never re-read.</summary>
    [Fact]
    public void An_unchanged_file_fingerprints_the_same_twice()
    {
        var file = new FileInfo(Path.Combine(Path.GetTempPath(), $"qw-fp-{Guid.NewGuid():N}.log"));
        File.WriteAllText(file.FullName, "a line");
        file.Refresh();

        try
        {
            Assert.Equal(SessionStore.Fingerprint(file), SessionStore.Fingerprint(file));
        }
        finally
        {
            file.Delete();
        }
    }
}
