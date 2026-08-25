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

# Create three clients and advance a coordinated org activity one checkpoint at a time
dotnet run --project src\Quantumwake.LogSim -c Release -- --scenario org-activity --step
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
| `--step` | off | Pause before every checkpoint in a multi-client scenario |
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
| `multi-stop-trader` | Move two commodities through three locations and two jumps |
| `spending` | One confirmed equipment purchase and one rejected purchase |
| `purchase-pairing` | Ignore the wrong kiosk and intermediate response before confirmation |
| `medical-respawn` | Incapacitation, inferred respawn, and an after-death medical bed |
| `medical-kinds` | Login wake, after-casualty treatment, and ordinary healing beds |
| `death-recovery` | Collapse a corpse-item burst into one death and recovery location |
| `revived-in-place` | Incapacitation followed by revival without inventing a respawn |
| `crew-flight` | Party arrivals, leader change, quantum flight, and a departure |
| `party-lifecycle` | Connect, lead, disconnect, reconnect, ignore chatter, then disband |
| `contract-complete` | Two visible mission steps completed, followed by a blueprint |
| `contract-abandoned` | One visible objective progresses and is then withdrawn |
| `loadout-swap` | Repeat an armour sighting and change the weapon in one slot |
| `stash-browse` | Browse two location inventories and one personal inventory |
| `fleet-growth` | Observe changing entitlement counts and retain the largest fleet |
| `ship-retrieval` | Join a duplicate spawn line to a ship model learned later |
| `location-resolution` | Replace a generic Rest Stop target with the actual arrival |
| `unexpected-disconnect` | Distinguish timeout, player request, and routine teardown |
| `combat` | One player kill, one death, and a destroyed vehicle |
| `all` | Every focused story composed into one session |
| `org-activity` | Three clients form a crew, trade, fight, recover, finish a contract, and stand down |

Scenario events are stable for a given `--start` and seed. The file’s own
timestamp is not: only the backups loop sets it, so a scenario `Game.log` is
always stamped with the moment it was written.

Automated tests generate every scenario, feed it back through `LogFileReader`
and `SessionBuilder`, assert the claimed session facts, and require zero
unmatched known tags.

## Walk three clients through one activity

`org-activity` is both a batch fixture and a visual walkthrough. It creates
three independent fake installs and handles:

| Client | Handle | What its log exercises | Dashboard |
|---|---|---|---|
| `captain` | `D-Rud` | Party, contract, blueprint, ship retrieval, quantum and combat | `http://127.0.0.1:31401` |
| `trader` | `astro_ice` | Cargo buy/sell, confirmed spend, fleet, ship and remote timeout | `http://127.0.0.1:31402` |
| `medic` | `Patchwork` | Loadout, stash, casualty, death, respawn and medical visits | `http://127.0.0.1:31403` |

Start the simulator in its own terminal:

```powershell
dotnet run --project src\Quantumwake.LogSim -c Release -- --scenario org-activity --step
```

It creates the three empty `Game.log` files first, prints one launch command
per client, and then waits before stage 1. Build the server once, run each
printed command in a separate PowerShell window, and open the three addresses.
Because each client has a fresh data directory, complete First Flight Setup and
press **Start Flying** on every dashboard before advancing stage 1.
Each command sets a different `QUANTUMWAKE_DATA` directory as well as a
different install and port, so caches and local settings cannot leak from one
simulated pilot to another.

Do not use `start.ps1` for this walkthrough: by design it stops an already
running Quantum Wake process before starting another. The printed commands run
the built server executable directly, allowing all three to stay up together.

Back in the simulator terminal, press Enter to write each checkpoint. After
every write it prints the exact facts to inspect before moving on:

1. all clients wake and expose fleet/loadout state;
2. the party forms and names its leader;
3. contract, cargo, spending and stash activity appears;
4. three ships launch and jump;
5. combat and a casualty land in separate clients;
6. the casualty recovers while the cargo sells;
7. the contract completes and awards a blueprint;
8. the party stands down and the sessions close.

For a complete fixture without pauses, omit `--step`. This is the form used by
the automated round-trip test: each file takes its own production parser and
session-builder path, then the aggregate assertions prove 3 jumps, 12 party
moments, fleet observations totalling 29 vehicles, and zero unmatched known
tags in all three logs.

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
