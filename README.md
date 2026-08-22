<img src="web/assets/emblem.jpg" width="150" align="right" alt="">

# Quantum Wake

**A pilot's logbook for Star Citizen** — by nekron

[![CI](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml/badge.svg)](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Licence Apache 2.0](https://img.shields.io/badge/licence-Apache--2.0-blue)
![154 tests](https://img.shields.io/badge/tests-154%20passing-4fd48a)
![Network](https://img.shields.io/badge/network-opt--in%20only-46617a)

Star Citizen writes everything you do to `Game.log` and then rotates it away.
Quantum Wake reads it — the live file and every backup — and gives you back the
flight it recorded: where you have been, what you flew, what you hauled and what
it cost you. A second-screen dashboard, a transparent in-game overlay, and a map
of the whole 'verse with your own trail across it.

It is read-only and never touches the game. It connects to the internet only
when you ask it to - the optional integrations on the Settings page - and never
on its own.

> **Pre-1.0.** Until version 1.0 this product will keep changing significantly:
> pages appear and move, data formats shift, and an update may re-read your logs
> or ask you to re-enable an integration. Nothing you care about is at risk -
> everything is rebuilt from the logs - but expect the ground to move.

**[Download `QuantumWake.exe`](https://github.com/peans99/QuantumWake/releases/latest)
and double-click it.** That is the whole installation — one file, no runtime to
install, nothing to unpack or configure. It finds your Star Citizen install
itself, across every fixed drive.

It then sits in the notification area. Right-click to open the dashboard, show
or hide the overlay, or quit. The in-game overlay is off until you turn it on -
from the Settings page or the tray - and the choice is remembered. `Ctrl+Alt+O`
toggles overlay click-through, and the dashboard is on
<http://127.0.0.1:31337>.

Windows will warn that the publisher is unknown — the binary is not code signed,
which costs money a free fan tool does not have. **More info → Run anyway**.

From source instead:

```powershell
.\start.ps1
```

![The star map](docs/images/map.png)

*Stanton, Pyro and Nyx. Solid nodes are places this install has actually been —
72 of the 292 the resolver can place — sized by how often. Star Citizen logs no
player position, so this is a topology map built from location and quantum-travel
events, not a radar.*

---

## What it shows

| View | Contents |
|---|---|
| **Flight plan** | Where to go next and the rest of the run, on the Now page and drawn over the map as numbered stops. Built from a trade route, a shopping list, or by hand — and stops cross themselves off as you land |
| **Now** | Where you are with a confidence level, active ship, session clock, quantum destination in flight, live event feed |
| **Map** | Every place in the game across Stanton, Pyro and Nyx — visited ones solid, the rest hollow — with zoom, pan, follow-me mode, per-place detail cards, and a commodity search that lights everywhere a good sells. Picking one opens its cargo panel: best terminals now, your own prices over the last day, three days or seven, and a selling/buying toggle. Double-click a place for what it takes and offers |
| **Sessions** | Every session you have played, in-game time separated from menu time |
| **Fleet** | Ships owned over time, flights per ship, estimated time aboard — with role, crew and insurance-claim cost per ship when the community dataset is on |
| **Places** | Most-visited locations and quantum destinations |
| **Contracts** | Accepted → completed or abandoned, faceted by issuer and type |
| **Spending** | Confirmed purchases by shop, item and category |
| **Ledger** | Every transaction, back-tracked to the place it happened |
| **Cargo** | Commodity trades — named, with the opt-in community dataset — the only income the logs record |
| **Market** | The commodity catalogue joined onto your own trades: where each good sells, and UEX best prices when that integration is on |
| **Loot** | When each item first appeared in your inventories, with the place |
| **Loadout** | The kit you are wearing, by slot, with size, grade and maker |
| **Stash** | What is in your inventory and where you left it |
| **Settings** | The overlay switch, the community dataset, UEX, the log cache |

Every table sorts by its headers. Optional, off-by-default integrations add
what the logs alone cannot: the **community dataset**
(StarCitizenWiki/scunpacked-data) names your cargo and supplies the ship and
item reference, and **UEX** brings live crowd-sourced prices in — and, with
your own UEX keys, lets you report the sale prices your logs already recorded
back to the community.

<table>
  <tr>
    <td width="50%"><a href="docs/images/fleet.png"><img src="docs/images/fleet.png" alt="Fleet"></a><br><sub><b>Fleet</b> — ships owned over time, from the entitlement query the game runs each session</sub></td>
    <td width="50%"><a href="docs/images/ledger.png"><img src="docs/images/ledger.png" alt="Ledger"></a><br><sub><b>Ledger</b> — every confirmed transaction, back-tracked to the place it happened</sub></td>
  </tr>
  <tr>
    <td width="50%"><a href="docs/images/sessions.png"><img src="docs/images/sessions.png" alt="Sessions"></a><br><sub><b>Sessions</b> — in-game time kept separate from time spent in menus</sub></td>
    <td width="50%"><a href="docs/images/stash.png"><img src="docs/images/stash.png" alt="Stash"></a><br><sub><b>Stash</b> — what you are carrying and where you left the rest</sub></td>
  </tr>
</table>

Names are real names — New Babbage, not `Stanton4_NewBabbage`; a Genoa power
plant, not `POWR_JUST_S02_Genoa_SCItem` — read from your own `Data.p4k` at
runtime with no external lookup service involved.

## Why another one

It exists because I went looking for it and it was not there: good `Game.log`
tools, but none of them a whole product built around an org and the things I
wanted to keep track of. So this is the one I had been looking for, and it is
free for the community to use, and meant to stay that way.

[docs/landscape.md](docs/landscape.md) surveys the dozen that came before it
honestly, including the one that overlaps this heavily. Three things here are
not in the others:

- **The whole map, not just your trail.** Others plot where you went. This draws
  every place it can resolve — 292 of them, against the 72 this install has
  actually visited — so the map shows how much 'verse is left, not just a trail.
- **Offline by default, all the way down.** Every other tool that shows real
  item names fetches them from UEX, the wiki or scunpacked. This reads
  `Data.p4k` directly with its own ZIP64 + ZStd reader. The single exception is
  commodity names, which exist nowhere in the local install - naming them is an
  opt-in, one-file community download that never happens without a click.
- **It says what the logs cannot support.** Inferred locations carry a
  confidence level, estimates are labelled and capped, and an event CIG removed
  produces an explanation rather than a bare zero.

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
- No CDN, no telemetry, no phoning home. The app connects to the internet only
  at your request, from the Settings page - the optional community dataset - and
  never on its own
- The overlay is an ordinary top-most window using documented Win32 styles

The trade-off of doing it safely: an always-on-top window is **not** composited
over exclusive fullscreen, so Star Citizen must run in **Borderless Windowed**
for the overlay to be visible. The dashboard has no such limitation.

## Requirements

**To run the release:** Windows 10 or 11. Nothing else — the executable is
self-contained, so no .NET install is needed. The overlay additionally wants the
WebView2 runtime, which ships with Windows 11; on Windows 10 the dashboard works
regardless and the overlay stays blank until
[the runtime](https://developer.microsoft.com/microsoft-edge/webview2/) is
installed.

**To build from source:** the [.NET 10 SDK](https://dotnet.microsoft.com/download).

## Usage

```powershell
.\start.ps1                     # the app: tray icon, dashboard, overlay
.\start.ps1 -NoOverlay          # the bare server, no tray and no overlay
.\start.ps1 -Lan                # allow a tablet as second screen
.\start.ps1 -Rescan             # force a full re-parse
.\start.ps1 -Path "D:\...\StarCitizen\LIVE"
```

Installs are auto-detected across all fixed drives (LIVE/PTU/EPTU).

The CLI is useful for verification without a UI:

```powershell
dotnet run --project src\Quantumwake.Cli -c Release
```

It prints per-event match counts and a **parser health** section. Because CIG
removes log events patch over patch, an unmatched line is always skipped rather
than fatal — and the health report names the tag and shows a sample so breakage
after a patch is visible immediately instead of appearing as silently empty
charts.

## Try it without playing

`Quantumwake.LogSim` builds a fake install whose logs match the real format,
quirks included:

```powershell
dotnet run --project src\Quantumwake.LogSim -c Release -- --backups 12 --combat
.\start.ps1 -Path "$env:TEMP\QuantumwakeFakeInstall\LIVE"
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

It is also **one process**. `QuantumWake.exe` runs the web server inside itself
and carries the dashboard as embedded resources, which is what lets the whole
application ship as a single file with no runtime to install. The server is
still its own project and its own executable, for headless use and for the
Linux-hosted server mode later.

```
        QuantumWake.exe  (one process, one file)
   ┌──────────────────────────────────────────┐
   │  tray icon      overlay (WPF+WebView2)   │      Browser / tablet
   │            ASP.NET Core, in-process ─────┼───── HTTP + SSE / SignalR
   └──────────────────────┬───────────────────┘      Remote clients (later)
                          │
        Quantumwake.Core        Quantumwake.Data
        tail → parse → state    SQLite + location graph
```

| Project | Target | Role |
|---|---|---|
| `Quantumwake.Core` | `net10.0` | Log tailing, parsing, location + session state |
| `Quantumwake.Data` | `net10.0` | SQLite cache, library aggregates |
| `Quantumwake.Server` | `net10.0` | REST + SSE + static UI |
| `Quantumwake.Overlay` | `net10.0-windows` | The application: tray icon, transparent WPF overlay, and the server hosted in-process |
| `Quantumwake.Cli` | `net10.0` | Backfill and verification |
| `Quantumwake.LogSim` | `net10.0` | Fake-install generator for testing without the game |

Only the overlay is Windows-bound, leaving a Linux-hosted server mode open.

```powershell
dotnet test Quantumwake.slnx      # 154 tests
```

Parser fixtures are real log lines, not synthesised ones — which is how three
format quirks were caught that grepping had hidden, including multi-line entries
whose continuations carry their own timestamp (15% of notifications were being
silently dropped).

## Tests

```powershell
dotnet test Quantumwake.slnx -c Release
```

Two suites. `Quantumwake.Tests` covers the parser, the session builder, the
resolvers and the stores, against fixtures copied from real log lines.
`Quantumwake.WebTests` runs `web/app.js` itself under a JavaScript engine with a
stub document, so the dashboard's own logic - prices, plans, panels - is tested
rather than eyeballed.

## Documentation


- [docs/findings.md](docs/findings.md) — the missing-combat-events evidence
- [docs/log-format-reference.md](docs/log-format-reference.md) — verified line formats and quirks
- [docs/architecture.md](docs/architecture.md) — decisions and why
- [docs/phase-1-core.md](docs/phase-1-core.md) — parser build log
- [docs/commodity-names.md](docs/commodity-names.md) — why a cargo sale cannot be named
- [docs/commodity-catalogue.md](docs/commodity-catalogue.md) — what the game data can and cannot tell us about trade
- [docs/credits.md](docs/credits.md) — every external resource used, and what came from where
- [docs/naming.md](docs/naming.md) — why the project is called Quantum Wake
- [docs/releasing.md](docs/releasing.md) — how a release is cut, and what enforces the version bump
- [docs/landscape.md](docs/landscape.md) — who else is doing this, and what is still ours

## Licence

Code is [Apache 2.0](LICENSE). Fork it, sell it, close your fork — the only ask
is attribution.

Two things it deliberately does not cover, both spelled out in [NOTICE](NOTICE):

- **The manufacturer artwork is Cloud Imperium's**, used under the Fankit
  Agreement, which does not permit commercial use. No licence this project grants
  can extend to it. Delete `web/assets/manufacturers/` if that matters for your
  fork — the UI falls back to a text badge.
- **The name and logo are not licensed.** Apache §6 grants no trademark rights,
  and none are granted here. Take the code; ship it under your own name.

No game data is contained in this repository. Names are read at runtime from your
own `Data.p4k`.

## Credits

Built by **nekron**, on top of work that was not mine. Full attribution — what
was taken from whom — is in [docs/credits.md](docs/credits.md). The short
version:

- **Community log tooling.** [StarLogs](https://github.com/Ozy311/StarLogs) by
  Ozy311 is owed the most: the archived combat line formats, the
  kill-classification tree and the NPC-name heuristics in this repo are its
  `event_parser.py` logic re-implemented in C#.
  [all-slain](https://github.com/DimmaDont/all-slain) (DimmaDont) supplied the
  per-patch record of which events CIG removed and when;
  [SCStats](https://github.com/Maple33-hash/SCStats) (Maple33) the read-only
  posture and the purchase-correlation idea;
  [SCPlay](https://github.com/ckuma/scplay) (ckuma) timestamp-span playtime.
  [AutoTrackR2](https://github.com/BubbaGumpShrump/AutoTrackR2),
  [SC-Kill-Monitor](https://github.com/greluc/SC-Kill-Monitor) and
  [citizenmon](https://github.com/danieldeschain/citizenmon) were studied too.
- **`Data.p4k` format.** The ZIP64-with-ZStd-method-100 structure is community
  knowledge from the [scdatatools](https://github.com/ventorvar/scdatatools)
  lineage. The reader here is our own and ships no extractor, but the format was
  not worked out here.
- **Artwork.** Manufacturer marks and the *Made by the Community* badge are from
  the official [Star Citizen Fankit](https://robertsspaceindustries.com/fankit),
  used under the Fankit Agreement.
- **Packages.** [ZstdSharp.Port](https://github.com/oleg-st/ZstdSharp) (Oleg
  Stepanischev), Microsoft.Data.Sqlite, WebView2, xUnit and coverlet. Nothing on
  the client side — no framework, no bundler, no CDN.

Star Citizen®, Roberts Space Industries® and Cloud Imperium® are registered
trademarks of Cloud Imperium Rights LLC. This is an unofficial fan tool, not
affiliated with or endorsed by Cloud Imperium Games.
