<img src="web/assets/emblem.jpg" width="150" align="right" alt="">

# Quantum Wake

**A pilot's logbook for Star Citizen** — by nekron

[![CI](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml/badge.svg)](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Licence Apache 2.0](https://img.shields.io/badge/licence-Apache--2.0-blue)
![350 tests](https://img.shields.io/badge/tests-350%20passing-4fd48a)
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
from the Settings page or the tray - and the choice is remembered. The overlay
arrives ready to move; its **📌 pin** button puts it out of the way so clicks
reach the game, and the tray icon (or `Ctrl+Alt+O`) brings it back. The
dashboard is on <http://127.0.0.1:31337>.

Windows will warn that the publisher is unknown — the binary is not code signed,
which costs money a free fan tool does not have. **More info → Run anyway**.

From source instead:

```powershell
.\start.ps1
```

![The star map](docs/images/map.png)

*Stanton, Pyro and Nyx. Solid nodes are places this install has actually been —
67 of the 290 the resolver can place — sized by how often. Star Citizen logs no
player position, so this is a topology map built from location and quantum-travel
events, not a radar.*

---

## What it shows

| View | Contents |
|---|---|
| **Flight plan** | Where to go next and the rest of the run, on the Now page and drawn over the map as numbered stops. Built from a trade route, a shopping list, or by hand — and stops cross themselves off as you land |
| **Shopping** | Lists that hold cargo and ship parts alike, checked against your stashes. Pick what to add and where you mean to shop, then read the list as a set of landings: every counter that carries any of it, ranked by how much one stop covers, with *fewest stops* to pack the run |
| **Now** | Where you are with a confidence level, active ship, session clock, quantum destination in flight, live event feed |
| **Map** | Every place in the game across Stanton, Pyro and Nyx — visited ones solid, the rest hollow — with zoom, pan, follow-me mode, per-place detail cards, and a commodity search that lights everywhere a good sells. Picking one opens its cargo panel: best terminals now, your own prices over the last day, three days or seven, and a selling/buying toggle. Double-click a place for what it takes and offers |
| **Sessions** | Every session you have played, in-game time separated from menu time |
| **Fleet** | Ships owned over time, flights per ship, estimated time aboard — with role, crew and insurance-claim cost per ship when the community dataset is on. **Upgrades** opens the game's own port list for that ship: what fits each hole, what it costs, and the counter that stocks it |
| **Places** | Most-visited locations and quantum destinations |
| **Contracts** | Accepted → completed or abandoned, faceted by issuer and type |
| **Spending** | Confirmed purchases by shop, item and category |
| **Ledger** | Every transaction, back-tracked to the place it happened |
| **Cargo** | Commodity trades — named, with the opt-in community dataset — the only income the logs record |
| **Market** | The commodity catalogue joined onto your own trades: where each good sells, and UEX best prices when that integration is on. Click a commodity for every counter that trades it — price, what it costs you against the best, stock or demand, and whether it sits in policed or lawless space |
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
  <tr>
    <td width="50%"><a href="docs/images/upgrades.png"><img src="docs/images/upgrades.png" alt="Upgrades"></a><br><sub><b>Upgrades</b> — what fits a ship, from the game's own port list, priced and with the counter that stocks it</sub></td>
    <td width="50%"><a href="docs/images/market.png"><img src="docs/images/market.png" alt="Market"></a><br><sub><b>Market</b> — every counter that trades a commodity: price, what it costs against the best, stock, and whether the law reaches it</sub></td>
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

## Totals describe the account you are playing

A data wipe ends one account and starts another, so totals that reach past one
are answering about an account you no longer have. Sessions from before the last
wipe are kept and still parsed — they are simply not counted, and the Settings
page says how many that is.

Wipes come at different depths, so you say what this one took: money, ships,
inventory, play history. Tick only what it actually reset and the rest keeps its
whole history — after an aUEC-only wipe your ledger starts again while your
fleet and the places you have been carry on. The date defaults to Alpha 4.8 on
15 May 2026 and is yours to move, or to switch off entirely if you want the lot.

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
dotnet test Quantumwake.slnx      # 350 tests
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

---

## Release notes

Newest first. Each version's section is what the GitHub release says too — the
release workflow lifts it from here, so it is written once.

### 0.6.17

- **Fixed the stray character in 64 places.** A byte-level repair in 0.6.15
  matched the tail of every valid middot as well as the broken one it was aimed
  at, doubling the lead byte - so separators across the Settings feeds, the
  market and the map read as a replacement glyph. All 64 repaired, every text
  file checked as valid UTF-8.
- **A bed where there is no clinic was a hab bed.** With the UEX place
  directory enabled, a bed used somewhere with no clinic is ruled out of being
  medical - which the directory can do, while the reverse it cannot: Port
  Tressler has habs and a clinic, so a bed there is still either one, and the
  app says so rather than picking. On this install it rules out nothing, since
  every bed was used at a place that has a clinic; it earns its keep for anyone
  who beds down at outposts.
- **Waking up is not a hospital visit.** The game prints the same line for
  every bed - the clinic bed you crawl into and the hab bed you wake up in at
  login - so every login was being counted as a medical bed. They are told
  apart now by what surrounds them: a bed within three minutes of leaving the
  menus, with nothing having happened to you, is waking up. On this install
  that is 27 of them, and Port Tressler drops from 68 bed visits to 54. Beds
  used after a death or an incapacitation are marked as such, since that is the
  one case that is unambiguously medical. The section is called *Beds used*
  now, and the logins are still reported - just not counted as treatment.
- **Standing: who you have worked for.** A table on the Contracts page of every
  issuer, how many contracts you took, how many you finished and the rate, how
  many you walked away from, and the span you have been working for them. This
  install: 16 factions, 205 contracts, 62% finished. Two spellings of one
  faction are one row - the game writes both "Red Wind" and "Redwind", and BHG
  for the Bounty Hunters Guild.
- **What a contract pays, where anyone has written it down.** The game logs a
  contract's displayed title, and that title comes from the file the StarStrings
  mod replaces - so with the mod installed, titles carry `[150 Rep]` and `[BP]`
  and the app reads them straight off. Shown as chips on the contract and summed
  per faction. **This is not your reputation**: nothing in the logs carries a rep
  value, the tags cover a fraction of contracts, and a faction with no annotated
  title shows a blank rather than a zero.

### 0.6.13

- **StarStrings, installed from Settings if you want it.** A community text mod
  by **MrKraken** that rewrites the game's English text to read more usefully:
  contracts that award blueprints tagged `[BP]`, shorter item names, the
  reputation a contract pays shown on it, and the mining guide sorted by rarity.
  The app fetches their release, checks whether a newer build is out, and can
  take it back out again. It is the only thing here that writes into your game
  folder: two files, anything else in the download refused outright, whatever
  was there copied aside first and put back on removal. Entirely MrKraken's
  work - nothing of theirs is bundled or altered here.

### 0.6.12

The map learns what things are, the app learns what it cannot count, and the
buttons stop looking borrowed.

- **A shopping list can say where it is for.** The new-list form has a
  destination picker - every place the app knows, visited ones first - and a
  second picker for what to add: 203 tradeable commodities and 2,349 parts and
  pieces of gear that can actually be bought. Picking one writes the line into
  the box, where it can be given a quantity like anything else; the box is
  still free text and the pickers only spell things for you.
  The plan then starts every line at that place when it sells the thing, ranks
  it first among the stops, and says so at the top of the chooser. Leave it
  blank for anywhere, which is what it did before. It is a field on the card
  too, because the run you meant on Tuesday is not the run you fly on Friday.
- **The map declutters itself.** At system scale only the busiest places are
  named - thirteen of them, rather than every place you have ever visited
  fighting for the same air - and the budget grows with the square of the zoom,
  so names arrive as you ask for detail. Past the detail threshold every name
  that fits is drawn, as before. A searched-for place is never rationed.
- **A jump in progress is drawn.** When the drive spools, the map runs a
  marching dashed vector from where you are to where you are going, with a
  pulsing ring waiting at the far end. The live feed always knew the
  destination; the map never showed the journey.
- **A body groups its places when you look at it.** Hovering the space a planet
  occupies lights the bubble its outposts and stations sit in, and clicking the
  gaps between them frames that body. At rest the bubble stays a faint ground
  rather than another thing to read.
- **The kill counter is gone.** It could only ever read zero: 4.9 writes no
  kill or vehicle-destruction line at all, so a counter pinned at zero read as a
  broken feature rather than a missing one. The About page says where it went
  and what would bring it back.
- **The map was not clickable at all with a real mouse.** It captured the
  pointer the moment you pressed, and a captured pointer delivers the click to
  the element holding the capture - so every click meant for a place went to the
  map instead. Nothing opened: not the card, not the trade panel on a
  double-click, not a stop onto a plan. The capture now waits until you have
  actually moved four pixels, so a press is a click and a drag is still a drag.
  It had been that way since the map was written; it passed its own tests
  because a synthetic click aimed straight at a node bypasses pointer capture,
  which is the one thing a real mouse cannot do.
- **Your position pulses again.** The marker was drawn all along, but the live
  feed repeats your location every second and each repeat rebuilt the marker,
  restarting a 2.2-second animation a fifth of the way in. It is redrawn only
  when you actually move now.
- **A ship part on a list says where to buy it.** The card priced shields and
  coolers but left "where" blank, which is the half you can act on.
- **Upgrades works for every ship, not just some.** The panel asked by display
  name - "Drake Corsair" - while the ship data is keyed by class - DRAK_Corsair.
  Anything whose maker word is not its code (Drake, Anvil, Aegis) or that spells
  a variant differently (Mk II against Mk2) came back empty.
- **Half the map was unclickable.** Every mark carries an invisible pad so a
  small one is easy to hit, and in a crowded cluster those pads covered their
  neighbours — the topmost won every click, so most places would not open their
  card, would not open their trade panel on a double-click, and could not be
  added to a plan. The pad now stops at the halfway line to the nearest
  neighbour. 151 of 290 places were unreachable; now none are.
- **A commodity now colours the whole map.** Pick one and every mark takes the
  price grade or goes slate — a station left cyan among a scale running green to
  gold read as a value on that scale when it was not one.
- **Click a commodity on the Market page** for every counter UEX knows, not just
  the best one: what each pays, what that costs you against the best, how much
  it will actually take or sell you, and whether it sits in policed or lawless
  space — with a filter for monitored space only, and a click to find any of
  them on the map.
- **A shopping list can hold ship parts.** A line is whatever you wrote, so
  "Bulwark" now finds the shield and its shop counters the same way "Agricium"
  finds the trade good, and each seller says which system it is in and whether
  the law reaches it.
- **What fits your ship, and where to buy it.** Every ship on the Fleet page has
  an *Upgrades* button: the game's own port list — quantum drive, shields,
  coolers, power plants, guns, racks — each with what it flies with now, what is
  sold that fits, the maker and grade, the price and the cheapest counter. Any
  of it goes onto a shopping list in one click. This needs the community
  dataset; refresh it on the Settings page if the panel says so.
- **A shopping list, by location.** The chooser has a second view: every counter
  that carries any of your list, ranked by how much of it one landing covers,
  with what that landing costs and whether it is lawless. Tick the stops you
  mean to fly and the plan is built from them — later stops only pick up what
  earlier ones missed, and *Fewest stops* covers the whole list in as few
  landings as it can. Buying each thing where it is cheapest is one landing per
  thing, which is rarely the run you want.
- **The logbook says how many.** A kiosk logs one line per order, so buying
  two of something wrote one line reading the price of both: two orders of two
  read as two purchases of one at twice the price. The count is on the line now.
- **Shops at rest stops are on the map now.** "Platinum CRU-L4" and its like are
  named for the station code alone, which matched nothing in the atlas, so every
  item shop out there was a stop with no dot and no route.

- **The map draws what a place is.** Every kind has its own mark — a skyline for
  a city, a headframe for a mine, an orbit for a research station, a crate for a
  distribution centre — in the same colour it always had, so the legend is a
  reminder rather than something to memorise. And planets are finally on the
  map: they were labels with nothing drawn, which is why a station looked bigger
  than the world it orbits. Each body is a quiet disc with its places on it.
  The marks are single bold silhouettes rather than little drawings: they are
  read at six pixels, where a headframe and an orbit turn to mush. Sizes are
  levelled too — visits still nudge a mark, but gently, because a four-fold
  range made the map read as a jumble of sizes rather than a set of places.

- **A crowded cluster opens up when you zoom in.** microTech piled its sites on
  top of each other however far you zoomed; the cluster now spreads once the
  view is close enough to be naming everything, and stays compact at system
  scale where it should read as one place. Clicks near a cluster work again too
  — an invisible hover disc had been landing on its neighbour's dots.
- **Commodity search answers to part of a name.** "medical" finds Medical
  Supplies; before, only the exact full name matched, and nothing ever showed
  you what that name was. Shortest match wins, a place name still finds the
  place, and the suggestion list offers commodities beside places.

- **The map draws what a place is.** Every kind has its own mark now — a skyline
  for a city, a headframe for a mine, an orbit for a research station, a crate
  for a distribution centre — in the same colour it always had, so the legend is
  a reminder rather than something to memorise. And planets are finally on the
  map: they were labels with nothing drawn, which is why a station looked bigger
  than the world it orbits. Each body is a quiet disc with its places on it.
- **The overlay arrives usable.**
 It used to start click-through: a pane that
  could not be moved, resized or closed, with a hotkey nothing mentioned as the
  only way out. Now it starts ready to grab, and a **📌 pin** button in its header
  puts it out of the way. The tray icon brings it back — a pinned window passes
  every click to the game, so it cannot carry its own way out — and `Ctrl+Alt+O`
  still works.
- **It can tell you a new version is out**, if you let it. You are asked once at
  startup; Settings → *New versions* holds the switch and a Check now button.
  One request to GitHub's public release feed, carrying nothing about you, and
  nothing is downloaded or installed — you get the release page.
- **It notices patches.** Nothing in the logs says an account was wiped, but they
  date every version, so the app brings the date and asks the one question it
  cannot answer: did that patch wipe? Asked once per patch. The first-run wizard
  asks the same thing before anything is counted.
- **A FAQ on the About page**, including why pledged ships and armour are missing
  until you touch them — nothing that arrives in an account is written to the
  log — and where to report anything wrong: **nekhron** on Discord, or the
  GitHub issues page.
- **Medical beds are tracked.** The game does log something about regen after
  all: using a bed says so. A bed at a known place is a more direct hint at
  where you will wake than waiting for the next death, so it is shown beside the
  wake-up inference rather than instead of it — 191 bed visits against 22
  wake-ups here, so most bed use is just healing.
- **Every button looks like the app.** The HUD button style was written inside
  the map's toolbar, so the map had chamfered cyan buttons and the other
  twenty-nine were whatever Windows draws by default. Delete now reads as
  destructive, an on state reads as on, and keyboard focus is visible.
- **The overlay stops flickering on its tab strip.** Hovering the strip made the
  widget's dissolved menu groups spring back into floating boxes, which reflowed
  the strip, slid it left, took the pointer off the group, dropped the hover and
  flipped it straight back — a layout fighting the cursor.
- **An update no longer arrives half-applied.** The dashboard's stylesheet and
  script were served with no caching rule, so the browser guessed how long they
  stayed fresh: the version number came from the app and read new while the page
  around it was the previous release. Only Ctrl+F5 fixed that, which is not
  something anyone should have to know. Both are revalidated on every load now —
  still a 304, still instant, but always the build you are running.
- **This update re-reads your logs once.** A summary parsed by an older build
  knows nothing of fields added since — medical beds would have been invisible
  to everyone already running the app — so the cache retires itself and the
  first start after updating rebuilds it. Two seconds here for 149 backups.
  Nothing is lost: it all comes from the logs.

- **The map prices cargo.** Pick a commodity and every place it trades is graded
  by what it is worth there: the best terminals now, from UEX, beside your own
  receipts over the last day, three days, seven or longer. Selling and buying
  are a toggle rather than a search prefix nothing mentioned, and a third
  shading — *my own prices* — grades the map from your receipts alone, so it
  works with the price feed switched off.
- **Double-click a place** for what it takes and offers: what you have sold
  there, what you have bought, what the catalogue says it stocks, and the
  receipts behind all of it.
- **Flight plans.** A list of places in the order you mean to fly them, on the
  Now page as *jump next* and drawn over the map as numbered stops. Build one by
  hand, from a trade route in one click, or from a shopping list — which now
  asks which seller you meant, marks the ones too small to fill your order, and
  defaults to the cheapest that can. **Stops cross themselves off when you land**,
  because the app already knows where you are.
- **Totals describe the account you are playing.** Sessions from before the last
  wipe are kept and still parsed, but no longer counted. Wipes come at different
  depths, so you say what this one took — money, ships, inventory, play history —
  and anything it did not take keeps its whole history. Defaults to Alpha 4.8 on
  15 May 2026 and lives on the Settings page.
- **Stale prices offer to renew themselves.** The price feed is still fetched
  only on your click, but if the snapshot has turned a day old the app says so
  at startup and offers the one click that fixes it. "Not now" lasts the day.
- **The overlay can show the flight plan**, and a card added in a later version
  no longer arrives switched off for anyone who had touched those settings.
- Under the hood: UEX terminal names are matched to map places once, on the
  server, so the shading, the panel and the plan cannot disagree; and the
  dashboard's own JavaScript has tests for the first time.
