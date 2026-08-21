using System.Globalization;
using Quantumwake.LogSim;

// Quantum Wake log simulator.
//
// Builds a fake Star Citizen install whose logs are byte-for-byte plausible, so
// the dashboard, map and overlay can be exercised without launching the game.
//
// It deliberately reproduces the awkward parts of the real format - multi-line
// entries, [SPAM] duplicates, notification repeats, both quantum route forms -
// because those are the cases worth testing. A tidy generator would prove
// nothing.

var options = Args.Parse(args);

if (options.ShowHelp)
{
    Args.PrintHelp();
    return 0;
}

var liveDirectory = Path.Combine(options.InstallRoot, "LIVE");
var backupsDirectory = Path.Combine(liveDirectory, "logbackups");

Directory.CreateDirectory(backupsDirectory);

Console.WriteLine("Quantum Wake log simulator  ·  by nekron");
Console.WriteLine();
Console.WriteLine($"Fake install : {liveDirectory}");
Console.WriteLine($"Handle       : {options.Handle}");
Console.WriteLine($"Combat       : {(options.Combat ? "enabled (exercises the dormant parser)" : "off, matching real 4.9 logs")}");
Console.WriteLine();

var simOptions = new SimOptions
{
    Handle = options.Handle,
    Legs = options.Legs,
    Combat = options.Combat
};

// ---- historical backups ----

if (options.Backups > 0)
{
    Console.WriteLine($"Generating {options.Backups} backup sessions…");

    // Walk backwards from yesterday, one session most evenings.
    var day = DateTimeOffset.Now.Date.AddDays(-1);

    for (var i = 0; i < options.Backups; i++)
    {
        var start = new DateTimeOffset(day, TimeSpan.Zero)
            .AddHours(20)
            .AddMinutes(Random.Shared.Next(0, 90));

        var name = $"Game Build(12344265) {start:dd MMM yy} ({start:HH mm ss}).log";
        var path = Path.Combine(backupsDirectory, name);

        using (var writer = new LogWriter(path))
        {
            var simulation = new Simulation(writer, simOptions, start, options.Seed + i);
            simulation.Run();
        }

        // Match the real rotation naming, where the file's timestamp matters.
        File.SetLastWriteTime(path, start.LocalDateTime.AddHours(2));

        Console.Write($"\r  {i + 1}/{options.Backups}");
        day = day.AddDays(-Random.Shared.Next(1, 4));
    }

    Console.WriteLine("\r  done." + new string(' ', 20));
}

// ---- the live Game.log ----

var gameLog = Path.Combine(liveDirectory, "Game.log");

if (options.Live)
{
    Console.WriteLine();
    Console.WriteLine($"Writing live session to {gameLog}");
    Console.WriteLine($"Speed: {options.Speed}x  (in-game seconds per real second)");
    Console.WriteLine();
    Console.WriteLine("Point the app at this install, then watch the Now view:");
    Console.WriteLine($"  .\\start.ps1 -Path \"{liveDirectory}\"");
    Console.WriteLine();
    Console.WriteLine("Ctrl+C to stop.");
    Console.WriteLine();

    using var writer = new LogWriter(gameLog);

    var simulation = new Simulation(writer, simOptions with { Legs = 1000 }, DateTimeOffset.Now, options.Seed);

    // Sleep in proportion to simulated time so the tailer sees a real trickle.
    simulation.Beat += span =>
    {
        var delay = TimeSpan.FromSeconds(span.TotalSeconds / Math.Max(0.001, options.Speed));

        if (delay > TimeSpan.FromSeconds(10))
            delay = TimeSpan.FromSeconds(10);

        Thread.Sleep(delay);
        Console.Write($"\r  simulated {simulation.Elapsed:hh\\:mm\\:ss}   ");
    };

    try
    {
        simulation.Run();
    }
    catch (Exception e) when (e is not OperationCanceledException)
    {
        Console.WriteLine($"\nStopped: {e.Message}");
    }
}
else
{
    Console.WriteLine();
    Console.WriteLine($"Writing a completed session to {gameLog}");

    using (var writer = new LogWriter(gameLog))
    {
        var simulation = new Simulation(writer, simOptions, DateTimeOffset.Now.AddHours(-3), options.Seed + 999);
        simulation.Run();
    }

    var info = new FileInfo(gameLog);
    Console.WriteLine($"  {info.Length / 1024.0:F0} KB");
    Console.WriteLine();
    Console.WriteLine("Try it:");
    Console.WriteLine($"  .\\start.ps1 -Path \"{liveDirectory}\"");
    Console.WriteLine($"  dotnet run --project src\\Quantumwake.Cli -c Release -- --path \"{liveDirectory}\"");
}

return 0;

/// <summary>Minimal command-line parsing, kept here so the tool has no dependencies.</summary>
internal sealed record Args
{
    public string InstallRoot { get; init; } = Path.Combine(Path.GetTempPath(), "QuantumwakeFakeInstall");
    public string Handle { get; init; } = "testpilot";
    public int Backups { get; init; } = 10;
    public int Legs { get; init; } = 6;
    public int Seed { get; init; } = 1337;
    public double Speed { get; init; } = 60;
    public bool Live { get; init; }
    public bool Combat { get; init; }
    public bool ShowHelp { get; init; }

    public static Args Parse(string[] args)
    {
        var result = new Args();

        for (var i = 0; i < args.Length; i++)
        {
            var next = i + 1 < args.Length ? args[i + 1] : null;

            switch (args[i].ToLowerInvariant())
            {
                case "--install" when next is not null:
                    result = result with { InstallRoot = next };
                    i++;
                    break;

                case "--handle" when next is not null:
                    result = result with { Handle = next };
                    i++;
                    break;

                case "--backups" when next is not null:
                    result = result with { Backups = int.Parse(next) };
                    i++;
                    break;

                case "--legs" when next is not null:
                    result = result with { Legs = int.Parse(next) };
                    i++;
                    break;

                case "--seed" when next is not null:
                    result = result with { Seed = int.Parse(next) };
                    i++;
                    break;

                case "--speed" when next is not null:
                    result = result with { Speed = double.Parse(next, CultureInfo.InvariantCulture) };
                    i++;
                    break;

                case "--live":
                    result = result with { Live = true };
                    break;

                case "--combat":
                    result = result with { Combat = true };
                    break;

                case "-h":
                case "--help":
                    result = result with { ShowHelp = true };
                    break;
            }
        }

        return result;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            Quantum Wake log simulator - generates a fake Star Citizen install.

              --install <dir>   Where to create the fake install (default: %TEMP%\QuantumwakeFakeInstall)
              --backups <n>     Historical sessions to generate (default: 10, 0 to skip)
              --live            Append to Game.log in real time instead of writing a finished file
              --speed <x>       Live mode: simulated seconds per real second (default: 60)
              --legs <n>        Trips per session (default: 6)
              --combat          Emit kill and vehicle-destruction events.
                                Real 4.9 logs contain none; this exercises the dormant parser.
              --handle <name>   Player handle (default: testpilot)
              --seed <n>        Deterministic output (default: 1337)

            Examples:
              dotnet run --project src\Quantumwake.LogSim -- --backups 20
              dotnet run --project src\Quantumwake.LogSim -- --backups 20 --combat
              dotnet run --project src\Quantumwake.LogSim -- --live --speed 120

            Then point the app at the generated install:
              .\start.ps1 -Path "%TEMP%\QuantumwakeFakeInstall\LIVE"
            """);
    }
}
