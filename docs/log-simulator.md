# Log simulator

`SCCompanion.LogSim` generates a fake Star Citizen install so the dashboard,
map and overlay can be exercised without launching the game — and, crucially,
so the **dormant combat parser can be tested at all**, since real 4.9 logs
contain no combat events.

## Use it

```powershell
# A fake install with 12 historical sessions and a finished Game.log
dotnet run --project src\SCCompanion.LogSim -c Release -- --backups 12

# Include kill and vehicle-destruction events
dotnet run --project src\SCCompanion.LogSim -c Release -- --backups 12 --combat

# Append to Game.log in real time, 120 simulated seconds per real second
dotnet run --project src\SCCompanion.LogSim -c Release -- --live --speed 120
```

Then point the app at it:

```powershell
.\start.ps1 -Path "$env:TEMP\SCCompanionFakeInstall\LIVE"
```

The cache is scoped per install, so a simulated install never blends into your
real LIVE totals.

## Options

| Flag | Default | Meaning |
|---|---|---|
| `--install <dir>` | `%TEMP%\SCCompanionFakeInstall` | Where to build the fake install |
| `--backups <n>` | 10 | Historical sessions; 0 to skip |
| `--live` | off | Append to `Game.log` in real time instead of writing a finished file |
| `--speed <x>` | 60 | Live mode: simulated seconds per real second |
| `--legs <n>` | 6 | Trips per session |
| `--combat` | off | Emit `<Actor Death>` and `<Vehicle Destruction>` |
| `--handle <name>` | `testpilot` | Player handle |
| `--seed <n>` | 1337 | Deterministic output |

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

The simulator writes plausible content, not a faithful replay. Quantum origins
and destinations are drawn from observed pools rather than following a coherent
route graph, so a jump may "start" somewhere the previous leg did not end.
Session pacing is randomised within bounds. It is a parser and UI exercise, not
a flight recorder.
