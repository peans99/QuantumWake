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

if (options.ListScenarios)
{
    Console.WriteLine("Available deterministic scenarios:");
    Console.WriteLine();

    foreach (var scenario in ScenarioCatalogue.All)
        Console.WriteLine($"  {scenario.Name,-22} {scenario.Description}");

    foreach (var scenario in MultiClientScenarioCatalogue.All)
        Console.WriteLine($"  {scenario.Name,-22} {scenario.Description}  [multi-client]");

    return 0;
}

var selectedScenario = options.Scenario is null
    ? null
    : ScenarioCatalogue.Find(options.Scenario);
var selectedMultiClientScenario = options.Scenario is null
    ? null
    : MultiClientScenarioCatalogue.Find(options.Scenario);

if (options.Scenario is not null && selectedScenario is null && selectedMultiClientScenario is null)
{
    Console.Error.WriteLine($"Unknown scenario '{options.Scenario}'. Use --list-scenarios to see the available names.");
    return 2;
}

if ((selectedScenario is not null || selectedMultiClientScenario is not null) && options.Live)
{
    Console.Error.WriteLine("A named scenario cannot be combined with --live. Use --step with a multi-client scenario.");
    return 2;
}

if (options.Step && selectedMultiClientScenario is null)
{
    Console.Error.WriteLine("--step is available for multi-client scenarios such as org-activity.");
    return 2;
}

if (selectedMultiClientScenario is not null)
{
    var root = Path.Combine(options.InstallRoot, selectedMultiClientScenario.Name);
    var paths = selectedMultiClientScenario.Pilots.ToDictionary(
        pilot => pilot.Key,
        pilot => Path.Combine(root, pilot.Key, "LIVE"),
        StringComparer.OrdinalIgnoreCase);
    var writers = new Dictionary<string, LogWriter>(StringComparer.OrdinalIgnoreCase);

    Console.WriteLine("Quantum Wake log simulator  ·  by nekron");
    Console.WriteLine();
    Console.WriteLine($"Scenario     : {selectedMultiClientScenario.Name} - {selectedMultiClientScenario.Description}");
    Console.WriteLine($"Playback     : {(options.Step ? "manual, one visual checkpoint at a time" : "complete")}");
    Console.WriteLine($"Scenario root: {root}");
    Console.WriteLine();
    Console.WriteLine("Client installs:");

    try
    {
        foreach (var pilot in selectedMultiClientScenario.Pilots)
        {
            var live = paths[pilot.Key];
            var clientLogPath = Path.Combine(live, "Game.log");
            Directory.CreateDirectory(live);
            writers.Add(pilot.Key, new LogWriter(clientLogPath));
            Console.WriteLine($"  {pilot.Handle,-12} {pilot.Role,-27} {live}");
        }

        if (options.Step)
        {
            Console.WriteLine();
            Console.WriteLine("Before starting stage 1, build once and run each client command in its own PowerShell window:");
            Console.WriteLine("  dotnet build src\\Quantumwake.Server\\Quantumwake.Server.csproj -c Release");

            foreach (var pilot in selectedMultiClientScenario.Pilots)
            {
                var data = Path.Combine(root, pilot.Key, "data");
                Console.WriteLine();
                Console.WriteLine($"  # {pilot.Handle} · http://127.0.0.1:{pilot.SuggestedPort}");
                Console.WriteLine($"  $env:QUANTUMWAKE_DATA = \"{data}\"");
                Console.WriteLine($"  .\\src\\Quantumwake.Server\\bin\\Release\\net10.0\\Quantumwake.Server.exe --path \"{paths[pilot.Key]}\" --Port {pilot.SuggestedPort}");
            }

            Console.WriteLine();
            Console.WriteLine("Open the three dashboard addresses, complete First Flight Setup, and press Start Flying on each.");
            Console.WriteLine("Then return here to advance the logs.");
        }

        MultiClientScenarioRunner.Run(
            selectedMultiClientScenario,
            writers,
            options.Start ?? DateTimeOffset.Now.Date.AddHours(20),
            beforeStage: stage =>
            {
                Console.WriteLine();
                Console.WriteLine($"Stage {stage.Number}/{selectedMultiClientScenario.Stages.Count} · {stage.Name}");
                Console.WriteLine($"  {stage.Description}");
                if (options.Step)
                {
                    Console.Write("  Press Enter to write this stage... ");
                    Console.ReadLine();
                }
            },
            afterStage: stage =>
            {
                Console.WriteLine("  Look for:");
                foreach (var fact in stage.ExpectedFacts)
                    Console.WriteLine($"    - {fact}");
            });
    }
    finally
    {
        foreach (var writer in writers.Values)
            writer.Dispose();
    }

    Console.WriteLine();
    Console.WriteLine("Scenario complete. Generated logs:");
    foreach (var pilot in selectedMultiClientScenario.Pilots)
    {
        var clientLogPath = Path.Combine(paths[pilot.Key], "Game.log");
        Console.WriteLine($"  {pilot.Handle,-12} {new FileInfo(clientLogPath).Length / 1024.0,6:F0} KB  {clientLogPath}");
    }

    return 0;
}

var liveDirectory = Path.Combine(options.InstallRoot, "LIVE");
var backupsDirectory = Path.Combine(liveDirectory, "logbackups");

Directory.CreateDirectory(selectedScenario is null ? backupsDirectory : liveDirectory);

Console.WriteLine("Quantum Wake log simulator  ·  by nekron");
Console.WriteLine();
Console.WriteLine($"Fake install : {liveDirectory}");
Console.WriteLine($"Handle       : {options.Handle}");
if (selectedScenario is null)
    Console.WriteLine($"Combat       : {(options.Combat ? "enabled (exercises the dormant parser)" : "off, matching real 4.9 logs")}");
else
    Console.WriteLine($"Scenario     : {selectedScenario.Name} - {selectedScenario.Description}");
Console.WriteLine();

var simOptions = new SimOptions
{
    Handle = options.Handle,
    Legs = options.Legs,
    Combat = options.Combat
};

// ---- historical backups ----

if (selectedScenario is null && options.Backups > 0)
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

if (selectedScenario is not null)
{
    Console.WriteLine($"Writing deterministic scenario to {gameLog}");

    using (var writer = new LogWriter(gameLog))
    {
        ScenarioRunner.Run(
            writer,
            selectedScenario,
            options.Start ?? DateTimeOffset.Now.Date.AddHours(20),
            options.Handle,
            simOptions.Geid);
    }

    var info = new FileInfo(gameLog);
    Console.WriteLine($"  {info.Length / 1024.0:F0} KB");
    Console.WriteLine();
    Console.WriteLine("Expected parser facts:");
    foreach (var fact in selectedScenario.ExpectedFacts)
        Console.WriteLine($"  - {fact}");
    Console.WriteLine();
    Console.WriteLine("Open it in Quantum Wake:");
    Console.WriteLine($"  .\\start.ps1 -Path \"{liveDirectory}\"");
}
else if (options.Live)
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
    public string? Scenario { get; init; }
    public bool Step { get; init; }
    public bool ListScenarios { get; init; }
    public DateTimeOffset? Start { get; init; }
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

                case "--scenario" when next is not null:
                    result = result with { Scenario = next };
                    i++;
                    break;

                case "--step":
                    result = result with { Step = true };
                    break;

                case "--list-scenarios":
                    result = result with { ListScenarios = true };
                    break;

                case "--start" when next is not null:
                    result = result with
                    {
                        Start = DateTimeOffset.Parse(
                            next,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal)
                    };
                    i++;
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
              --list-scenarios  List focused, deterministic test stories
              --scenario <name> Write one focused scenario instead of random sessions
              --step            Pause before each stage of a multi-client scenario
              --start <date>    Scenario timestamp (ISO 8601; default: today at 20:00)
              --handle <name>   Player handle (default: testpilot)
              --seed <n>        Deterministic output (default: 1337)

            Examples:
              dotnet run --project src\Quantumwake.LogSim -- --backups 20
              dotnet run --project src\Quantumwake.LogSim -- --backups 20 --combat
              dotnet run --project src\Quantumwake.LogSim -- --live --speed 120
              dotnet run --project src\Quantumwake.LogSim -- --scenario cargo-run
              dotnet run --project src\Quantumwake.LogSim -- --scenario org-activity --step
              dotnet run --project src\Quantumwake.LogSim -- --scenario all --start 2026-08-24T20:00:00Z

            Then point the app at the generated install:
              .\start.ps1 -Path "%TEMP%\QuantumwakeFakeInstall\LIVE"
            """);
    }
}
