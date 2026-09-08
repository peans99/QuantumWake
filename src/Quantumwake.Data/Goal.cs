using System.Text.Json;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>Something the player is saving towards.</summary>
/// <param name="Name">What it is, in their own words or a ship's name.</param>
/// <param name="Target">What it costs.</param>
/// <param name="SetAt">When it was set, which is when progress starts counting.</param>
public sealed record Goal(string Name, decimal Target, DateTimeOffset SetAt);

/// <summary>
/// How fast trading is making money, per hour actually spent in the game.
/// </summary>
/// <param name="Earned">
/// Sale revenue less what the cargo cost. Trading profit, not everything earned.
/// </param>
/// <param name="InGame">Time in the game itself, menus excluded.</param>
/// <param name="PerHour">Zero when there is not enough flying to divide by.</param>
/// <param name="Days">The window this covers, or 0 for everything.</param>
public sealed record EarningRate(decimal Earned, TimeSpan InGame, decimal PerHour, int Days);

/// <summary>
/// Remembers what the player is saving for.
/// </summary>
/// <remarks>
/// One goal, not a list. The question this answers is "how far off is the thing
/// I want", and a list of five things nobody is actively saving for answers a
/// different and less useful one.
/// </remarks>
public sealed class GoalStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private Goal? _current;

    public GoalStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "goal.json");
        Load();
    }

    public Goal? Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>Sets a goal, or clears it when given nothing worth saving for.</summary>
    public Goal? Save(Goal? goal)
    {
        var settled = goal is null || goal.Target <= 0 || string.IsNullOrWhiteSpace(goal.Name)
            ? null
            : goal with { Name = goal.Name.Trim() };

        lock (_gate)
        {
            _current = settled;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

                if (settled is null) File.Delete(_path);
                else File.WriteAllText(_path, JsonSerializer.Serialize(settled));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A goal that fails to save is a goal that reverts next start,
                // which beats refusing to set one.
            }
        }

        return settled;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            _current = JsonSerializer.Deserialize<Goal>(File.ReadAllText(_path));
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // An unreadable goal is no goal, not a broken start.
        }
    }
}
