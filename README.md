# SC Companion

A companion app for Star Citizen driven by `Game.log`. Second-screen dashboard,
transparent in-game overlay, and a map of where you've been — read-only, offline,
and anti-cheat safe.

```powershell
.\start.ps1
```

Dashboard on <http://127.0.0.1:31337>. `Ctrl+Alt+O` toggles overlay click-through.

---

## What it shows

| View | Contents |
|---|---|
| **Now** | Current location with a confidence level, active ship, session clock, quantum destination in flight, live event feed |
| **Map** | Topology map of Stanton and Pyro with every place you've visited, sized by visit count |
| **Sessions** | Every session, with in-game time separated from menu time |
| **Ships** | Flights per ship, with estimated time aboard |
| **Places** | Most-visited locations and quantum destinations |
| **Contracts** | Faceted by issuer and type |

## Two things it deliberately does not do

**It is not a killboard.** Star Citizen 4.9 does not write kill or
vehicle-destruction events to `Game.log`. This was verified, not assumed: a
line-by-line scan of 403 MB across 144 log backups found **zero** `<Actor Death>`
and `<Vehicle Destruction>` lines, despite 22 incapacitations proving combat
happened. The parser for those events is implemented and tested against the
archived format, sitting dormant — if CIG restores them, the counters populate
with no further work. See [docs/findings.md](docs/findings.md).

**The map is not a radar.** No player position is logged either, so location is
*inferred* from discrete signals — inventory requests, quantum routes, spawns —
and every estimate carries a confidence level rather than pretending to a
precision the logs cannot support.

## Safety

Modelled on SCStats' read-only stance, because this touches a live game install
running Easy Anti-Cheat:

- Reads log files only; nothing is ever written to the game directory
- No memory access, no DLL injection, no DirectX or WinAPI hooking
- No outbound network calls in standalone mode — no CDN, no telemetry
- The overlay is an ordinary top-most window using documented Win32 styles

The trade-off of doing it safely: an always-on-top window is **not** composited
over exclusive fullscreen, so Star Citizen must run in **Borderless Windowed**
for the overlay to be visible. The dashboard has no such limitation.

## Requirements

- Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download)
- WebView2 runtime (ships with Windows 11) — overlay only

## Usage

```powershell
.\start.ps1                     # dashboard + overlay
.\start.ps1 -NoOverlay          # dashboard only
.\start.ps1 -Lan                # allow a tablet as second screen
.\start.ps1 -Rescan             # force a full re-parse
.\start.ps1 -Path "D:\...\StarCitizen\LIVE"
```

Installs are auto-detected across all fixed drives (LIVE/PTU/EPTU).

The CLI is useful for verification without a UI:

```powershell
dotnet run --project src\SCCompanion.Cli -c Release
```

It prints per-event match counts and a **parser health** section. Because CIG
removes log events patch over patch, an unmatched line is always skipped rather
than fatal — and the health report names the tag and shows a sample so breakage
after a patch is visible immediately instead of appearing as silently empty
charts.

## Try it without playing

`SCCompanion.LogSim` builds a fake install whose logs match the real format,
quirks included:

```powershell
dotnet run --project src\SCCompanion.LogSim -c Release -- --backups 12 --combat
.\start.ps1 -Path "$env:TEMP\SCCompanionFakeInstall\LIVE"
```

`--combat` emits kill and vehicle-destruction events, which is the only way to
see the dormant killboard populate — real 4.9 logs never will. `--live` appends
to `Game.log` in real time so the Now view and overlay update as you watch.

The cache is scoped per install, so a simulated install never blends into your
real totals. Full options in [docs/log-simulator.md](docs/log-simulator.md).

## Architecture

One web UI, hosted three ways — a browser today, WebView2 in the overlay, and
remote clients when server mode lands. Writing it once is why the overlay cost
almost nothing to add.

```
Overlay (WPF + WebView2)   Browser / tablet        Remote clients (later)
            └──────────── HTTP + SSE / SignalR ───────────┘
                                  │
                    SCCompanion.Server (ASP.NET Core)
                                  │
              SCCompanion.Core          SCCompanion.Data
              tail → parse → state      SQLite + location graph
```

| Project | Target | Role |
|---|---|---|
| `SCCompanion.Core` | `net10.0` | Log tailing, parsing, location + session state |
| `SCCompanion.Data` | `net10.0` | SQLite cache, library aggregates |
| `SCCompanion.Server` | `net10.0` | REST + SSE + static UI |
| `SCCompanion.Overlay` | `net10.0-windows` | Transparent WPF shell |
| `SCCompanion.Cli` | `net10.0` | Backfill and verification |

Only the overlay is Windows-bound, leaving a Linux-hosted server mode open.

```powershell
dotnet test SCCompanion.slnx      # 127 tests
```

Parser fixtures are real log lines, not synthesised ones — which is how three
format quirks were caught that grepping had hidden, including multi-line entries
whose continuations carry their own timestamp (15% of notifications were being
silently dropped).

## Documentation

- [docs/findings.md](docs/findings.md) — the missing-combat-events evidence
- [docs/log-format-reference.md](docs/log-format-reference.md) — verified line formats and quirks
- [docs/architecture.md](docs/architecture.md) — decisions and why
- [docs/phase-1-core.md](docs/phase-1-core.md) — parser build log
- [docs/tools/](docs/tools/) — analysis of seven existing SC log tools

## Credits

Prior art studied while building this: [StarLogs](https://github.com/Ozy311/StarLogs)
(architecture and archived combat patterns), [all-slain](https://github.com/DimmaDont/all-slain)
(per-patch event availability), [SCStats](https://github.com/Maple33-hash/SCStats)
(read-only posture), [SCPlay](https://github.com/ckuma/scplay) (timestamp-span
playtime), [AutoTrackR2](https://github.com/BubbaGumpShrump/AutoTrackR2),
[SC-Kill-Monitor](https://github.com/greluc/SC-Kill-Monitor),
[citizenmon](https://github.com/danieldeschain/citizenmon).
