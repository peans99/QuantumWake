# Findings: the kill-event problem

## Claim

`<Actor Death>` and `<Vehicle Destruction>` events — the foundation of nearly
every Star Citizen log tool — do not appear anywhere in this install's logs.

## Evidence

A line-by-line, case-insensitive scan of **all 144 backup files (402.5 MB)** in
`LIVE\logbackups`, covering 2026-04-20 through 2026-08-17:

```
      95  Incapacitated
       0  damage type
       0  destroy level
       0  killed by
       0  CVehicle::
       0  CActor::Kill
       0  OnAdvance
       0  Vehicle Destruction
       0  Actor Death
       0  IsHeadshot
       0  Actor stall
       0  ActorState] Corpse
```

The current `Game.log` (4.0 MB, 2026-08-20) agrees: the only `<Actor …>` tag
present anywhere is `<Actor Physics>` — 860 instances of
`CSCActorPhysicsController::Physicalize: Failed to physicalize …`, an unrelated
error.

## "I just didn't have combat yesterday"

Reasonable objection, but the scan wasn't limited to yesterday — it covered four
months. And the logs contain positive proof that combat *did* happen: 95
`Incapacitated` notifications, spread across **11 distinct play sessions**:

```
  12  Game Build(11715810) 01 May 26 (21 50 15).log
  11  Game Build(11715810) 08 May 26 (21 42 21).log
  10  Game Build(11875683) 04 Jun 26 (21 35 48).log
  10  Game Build(11875683) 23 May 26 (20 31 44).log
   4  Game Build(11875683) 29 May 26 (20 59 53).log
   3  Game Build(12030094) 17 Jun 26 (22 11 49).log
  10  Game Build(12302499) 25 Jul 26 (14 24 03).log
   8  Game Build(12344265) 06 Aug 26 (20 43 07).log
  10  Game Build(12344265) 08 Aug 26 (21 41 57).log
   8  Game Build(12344265) 15 Aug 26 (21 40 04).log
   9  Game Build(12344265) 17 Aug 26 (21 06 16).log
```

The notification text is:

> `Incapacitated: While incapacitated, ask others in your party, in chat, or
> through rescue service beacons to revive you before the 'Time to Death' timer
> expires.`

So on at least eleven separate evenings the player was downed in combat, and
**not one** of those sessions produced a single `<Actor Death>` line. That is
the opposite of what "no combat" would look like.

Note what these events *are*, though: HUD notification text, surfaced through
`<SHUDEvent_OnNotification>` and `<UpdateNotificationItem>`. They carry no
killer, no weapon, no damage type, no position — just the on-screen tooltip.
That is not a substitute for a death event; it only proves a death occurred.

## Why this happened

CIG has been progressively stripping combat telemetry from client-side logs.
The all-slain project documents the trend patch by patch, noting that since
**4.0.2** only events involving the client player were logged at all — and
listing quantum-travel and inter-system-jump events as already lost in 4.0.1
and 4.1.0 respectively.

Community reporting on Spectrum ([Keep Kill Trackers Alive
Please](https://robertsspaceindustries.com/spectrum/community/SC/forum/4/thread/keep-kill-trackers-alive-please/))
covers the resulting breakage of third-party killboards. On this install, at
4.9, even the client-player death events are gone.

## Consequences for a build

- **A killboard is not buildable from these logs.** Not "harder" — the source
  data does not exist.
- Cloning StarLogs or AutoTrackR2 produces a working application with an empty
  event feed.
- Web killboards that still appear to function (sc-killboard.com, sckillboard.com)
  either rely on older clients, server-side data, or user-submitted reports —
  worth verifying before depending on any of them.
- The parsers in these repos remain valuable as **format documentation** for
  historical logs, and their architecture (file tailing, SSE dashboards,
  multi-install detection) is entirely reusable for non-combat events.

## What's still there

Plenty. Verified counts across the backups:

| Signal | Count | Gives you |
|---|---:|---|
| `Elevator` / `Transit` | 158,665 / 44,307 | movement through stations |
| `Mission` | 99,306 | objective markers, mission IDs |
| `Spawn` | 35,396 | spawn/respawn points |
| `Quantum` | 31,968 | routes, destinations, fuel requests |
| `Contract` | 31,450 | contract names, definition IDs |
| `Salvage` | 4,318 | salvage activity |
| `Party` | 2,554 | party membership, leaders |
| `RequestLocationInventory` | 1,386 | locations visited |
| `Wallet` | 1,499 | balance-adjacent events |
| `VehicleListQuery` | 1,080 | fleet queries |
| `Vehicle Control Flow` | 488 | **ships actually flown** |
| `Loading screen for …` | 421 | **session boundaries + timings** |
| `Legacy login response` | 148 | handle |

Gamerules split across the backups: `SC_Frontend` 37,182 / `SC_Default` 15,421 /
`EA_FreeFlight` 155 — i.e. menu/hangar vs. PU vs. Arena Commander free flight,
which is enough to separate "in game" from "in menus" when computing playtime.

See [log-format-reference.md](log-format-reference.md) for exact line shapes.
