using Quantumwake.Core.Events;

namespace Quantumwake.Data;

/// <summary>
/// Turns the game's own mission toasts into callouts: a new objective, one
/// complete or failed, a contract complete or failed.
/// </summary>
/// <remarks>
/// <para>
/// From the log, not the screen, because the log has them word for word and
/// reliably: across this install's backups there are 1,288 "New Objective",
/// 383 "Objective Complete", 7 "Objective Failed", 515 "Contract Complete" and
/// 40 "Contract Failed" toasts. What the log does not have, in 4.10, is any
/// count of kills or of ships left - objectives update without numbers - so
/// nothing here says one.
/// </para>
/// <para>
/// The objective's own name is not said. The recorded voice has fixed
/// sentences, and "New objective: Defend Tranquility" would fall back to the
/// Windows voice whole; the name is on the game's HUD at the same moment.
/// </para>
/// <para>
/// Each toast is logged three to five times as it is queued, shown and faded,
/// under one notification number, so a number and text heard in the last
/// minute is the same toast. And the app reads the whole log when it starts:
/// a toast older than <see cref="Fresh"/> is history being replayed, not news.
/// </para>
/// </remarks>
public sealed class MissionWatch
{
    /// <summary>How old a toast can be and still be said.</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromSeconds(30);

    private static readonly (string Prefix, string Kind, string Text)[] Toasts =
    [
        ("New Objective", "objective", "New objective"),
        ("Objective Complete", "objective-done", "Objective complete"),
        ("Objective Failed", "objective-failed", "Objective failed"),
        ("Contract Complete", "contract-done", "Contract complete"),
        ("Contract Failed", "contract-failed", "Contract failed"),
    ];

    private readonly Dictionary<string, DateTimeOffset> _heard = [];

    /// <summary>The callout an event makes, or null; <paramref name="now"/> is the wall clock.</summary>
    public CombatCallout? See(GameEvent ev, DateTimeOffset now)
    {
        if (ev is not NotificationEvent toast) return null;
        if (now - toast.Timestamp > Fresh) return null;

        foreach (var (prefix, kind, text) in Toasts)
        {
            if (!toast.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var key = $"{toast.NotificationId}|{toast.Text}";
            foreach (var old in _heard.Where(h => now - h.Value > TimeSpan.FromMinutes(1)).Select(h => h.Key).ToList())
                _heard.Remove(old);
            if (!_heard.TryAdd(key, now)) return null;

            return new CombatCallout(toast.Timestamp, kind, text);
        }

        return null;
    }
}
