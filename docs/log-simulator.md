# Log simulator

`Quantumwake.LogSim` generates a fake Star Citizen install so the dashboard,
map and overlay can be exercised without launching the game — and, crucially,
so the **dormant combat parser can be tested at all**, since real 4.9 logs
contain no combat events.

## Use it

```powershell
# A fake install with 12 historical sessions and a finished Game.log
dotnet run --project src\Quantumwake.LogSim -c Release -- --backups 12

# Include kill and vehicle-destruction events
dotnet run --project src\Quantumwake.LogSim -c Release -- --backups 12 --combat

# Write one exact, repeatable story to LIVE\Game.log
dotnet run --project src\Quantumwake.LogSim -c Release -- --scenario cargo-run

# See every focused story and what it is meant to prove
dotnet run --project src\Quantumwake.LogSim -c Release -- --list-scenarios

# Append to Game.log in real time, 120 simulated seconds per real second
dotnet run --project src\Quantumwake.LogSim -c Release -- --live --speed 120
```

Then point the app at it:

```powershell
.\start.ps1 -Path "$env:TEMP\QuantumwakeFakeInstall\LIVE"
```

The cache is scoped per install, so a simulated install never blends into your
real LIVE totals.

## Options

| Flag | Default | Meaning |
|---|---|---|
| `--install <dir>` | `%TEMP%\QuantumwakeFakeInstall` | Where to build the fake install |
| `--backups <n>` | 10 | Historical sessions; 0 to skip |
| `--live` | off | Append to `Game.log` in real time instead of writing a finished file |
| `--speed <x>` | 60 | Live mode: simulated seconds per real second |
| `--legs <n>` | 6 | Trips per session |
| `--combat` | off | Emit `<Actor Death>` and `<Vehicle Destruction>` |
| `--list-scenarios` | off | List the focused deterministic stories |
| `--scenario <name>` | none | Write one focused story instead of random sessions |
| `--start <date>` | today at 20:00 | ISO 8601 timestamp for deterministic scenario entries |
| `--handle <name>` | `testpilot` | Player handle |
| `--seed <n>` | 1337 | Deterministic output |

## Focused scenarios

The random simulator remains useful for load and variety. Named scenarios fill
the other gap: reproducing one state exactly, then knowing what the app ought to
show. They write one completed `LIVE\Game.log` and print their expected facts.

| Scenario | Story |
|---|---|
| `cargo-run` | Buy 16 SCU at Port Tressler, fly, then sell it at New Babbage |
| `spending` | One confirmed equipment purchase and one rejected purchase |
| `medical-respawn` | Incapacitation, inferred respawn, and an after-death medical bed |
| `crew-flight` | Party arrivals, leader change, quantum flight, and a departure |
| `contract-complete` | Two visible mission steps completed, followed by a blueprint |
| `combat` | One player kill, one death, and a destroyed vehicle |
| `all` | Every focused story composed into one session |

For a stable file timestamp as well as stable event content, pass an explicit
start such as `--start 2026-08-24T20:00:00Z`. Automated tests generate every
scenario, feed it back through `LogFileReader` and `SessionBuilder`, assert the
claimed session facts, and require zero unmatched known tags.

## Why it reproduces the ugly parts

A generator that emitted tidy, well-behaved lines would prove nothing — every
hard case in this parser is a quirk. So the simulator deliberately writes:

- **Multi-line entries.** Notification text containing a newline, with the
  continuation carrying its own identical timestamp. This is the quirk that was
  silently dropping 15% of notifications.
- **Notification repeats.** Every notification fires four times with differing
  `Action:` values (`Next`, `StartFade`, `Remove`), so deduplication is
  genuinely exercised.
- **`[SPAM nnn]` duplicates** shadowing real lines.
- **Both `<Calculate Route>` forms** — one naming origin and destination, one
  naming only the destination — alternating between legs.
- **The `INVALID_LOCATION_ID` failure variant** of location inventory requests.
- **Untagged lines** such as `Loading screen for …` and the session header,
  which carry no `[Notice]` severity at all.
- **Realistic noise.** Terrain invalidation, loading platform chatter, physics
  errors — real logs are over 95% noise.
- **Cargo trades with no name and no place.** Kiosk lines carry the commodity
  only as `resourceGUID` and always report the same shop id, exactly as the game
  does, so both the catalogue lookup and the place back-track are exercised. The
  ids are real ones from the community digest, and prices are rolled from the
  place and the cargo together, so one terminal genuinely is the best place to
  sell a given commodity — otherwise the map's shading has nothing to show.

## Verified

Generating 12 sessions with `--combat` and parsing them back:

```
location.inventory  182      contract.marker   50
combat.death         79      session.context   39
quantum.target       78      session.loading   39
quantum.route        78      net.disconnect    26
vehicle.control      78      combat.vehicle    25
hud.notification     73      ! unmatched        0
```

Zero unmatched tags, and **79 deaths plus 25 vehicle destructions parsed** —
the first end-to-end proof that the dormant combat path works. With the app
pointed at that install the dashboard reports 59 player kills, where the real
install reports 0.

That difference is the point: the code is correct, and the zero on real logs is
the game's doing, not a defect.

## Limitations

The random simulator writes plausible content, not a faithful replay. Quantum
origins and destinations are drawn from observed pools rather than following a
coherent route graph, so a jump may "start" somewhere the previous leg did not
end. Session pacing is randomised within bounds. Named scenarios are coherent
and repeatable, but still synthetic parser and UI exercises rather than flight
recordings.
