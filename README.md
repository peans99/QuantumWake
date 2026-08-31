<img src="web/assets/emblem.jpg" width="150" align="right" alt="">

# Quantum Wake

**A private Star Citizen logbook for Windows** — by nekron

[![CI](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml/badge.svg)](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Licence Apache 2.0](https://img.shields.io/badge/licence-Apache--2.0-blue)
![1043 tests](https://img.shields.io/badge/tests-1043%20passing-4fd48a)
![Network](https://img.shields.io/badge/network-opt--in%20only-46617a)

Quantum Wake turns `Game.log` into a local dashboard of your flights. It keeps
your sessions, ships, trades, contracts, crew, inventory sightings and travel
history together, then adds reference data from your own Star Citizen install.

The app runs on your PC and keeps its database there. Network features are
optional. Most of Quantum Wake is read-only; item labels and StarStrings are the
two features that can replace the game's English text file, and both require an
explicit install click.

> **Pre-1.0:** pages and stored formats may still change. The database can be
> rebuilt from your logs after an update.

## Install

**[Download `QuantumWake.exe`](https://github.com/peans99/QuantumWake/releases/latest)**
and run it. There is no installer and no separate .NET runtime to add.

Quantum Wake finds LIVE, PTU and EPTU installs on fixed drives. It starts in the
notification area; right-click the tray icon to open the dashboard or overlay,
check for updates, or quit. The dashboard is also available at
<http://127.0.0.1:31337>.

Windows may show an unknown-publisher warning because the executable is not
code-signed. Choose **More info → Run anyway** if you downloaded it from this
repository's release page.

The overlay starts disabled. Star Citizen must use **Borderless Windowed** for
it to appear. Click the overlay's pin button to let mouse input pass through;
use the tray icon or `Ctrl+Alt+O` to bring it back.

![The star map](docs/images/map.png)

*Visited places are solid and sized by visit count. Empty nodes are known but
unvisited. The map uses logged locations and quantum travel; it is not a live
position tracker.*

## What is included

| View | What it answers |
|---|---|
| **Now** | Where am I, what am I flying, and what has happened this session? |
| **Map** | Where have I been, and where can I find a place, service or commodity? |
| **Flight plan** | What is my next stop? Plans can come from a route, shopping list or manual entry |
| **Sessions** | How long did I play, excluding time left in menus? |
| **Fleet** | Which ships have appeared on my account, and what fits each component port? |
| **Places** | Which locations and quantum destinations do I use most? |
| **Contracts** | What did I accept, finish or abandon, and for whom? |
| **Crew** | Who has appeared in party and ship-comms events? |
| **Spending and Ledger** | What confirmed transactions were logged, and where? |
| **Cargo and Market** | What did I buy or sell, and where is each commodity traded? |
| **Mining** | What spawns where, how rich the rocks are, their quality and respawn time |
| **Crafting** | What can be made, from which materials, and where its blueprint drops |
| **Loot, Loadout and Stash** | What gear has appeared, what is equipped, and where it was last seen |
| **Item labels** | Optional in-game marks for component size, grade, armour class and hard-to-buy gear |

Tables can be sorted by their column headings. Dashboard cards can be hidden,
collapsed and rearranged.

<table>
  <tr>
    <td width="50%"><a href="docs/images/fleet.png"><img src="docs/images/fleet.png" alt="Fleet"></a><br><sub><b>Fleet</b> — ships seen on the account and how often they flew</sub></td>
    <td width="50%"><a href="docs/images/ledger.png"><img src="docs/images/ledger.png" alt="Ledger"></a><br><sub><b>Ledger</b> — confirmed transactions with their source</sub></td>
  </tr>
  <tr>
    <td width="50%"><a href="docs/images/sessions.png"><img src="docs/images/sessions.png" alt="Sessions"></a><br><sub><b>Sessions</b> — game time separated from menu time</sub></td>
    <td width="50%"><a href="docs/images/stash.png"><img src="docs/images/stash.png" alt="Stash"></a><br><sub><b>Stash</b> — gear and its last recorded location</sub></td>
  </tr>
  <tr>
    <td width="50%"><a href="docs/images/upgrades.png"><img src="docs/images/upgrades.png" alt="Upgrades"></a><br><sub><b>Upgrades</b> — compatible parts, prices and shops</sub></td>
    <td width="50%"><a href="docs/images/market.png"><img src="docs/images/market.png" alt="Market"></a><br><sub><b>Market</b> — commodities and the counters that trade them</sub></td>
  </tr>
</table>

## Data sources

Quantum Wake reads three kinds of data:

1. **Your logs.** `Game.log` and its backups provide sessions, travel, ships,
   contracts, party activity, inventory sightings and confirmed transactions.
2. **Your game install.** `Data.p4k` provides names, items, commodities,
   crafting recipes, mining deposits, place descriptions and facilities. The
   first read after a game patch takes about half a minute and is then cached.
3. **Optional community services.** UEX adds current prices and shop listings.
   StarCitizenWiki's scunpacked dataset adds ship specifications and wider map
   and mining coverage. Both integrations are off until enabled in Settings.

No game data is committed to this repository.

## Known limits

- **No live position.** The logs record arrivals, inventory locations, spawns
  and quantum routes, not player coordinates. Map locations are inferred and
  carry a confidence level.
- **No complete killboard.** Star Citizen 4.9 and 4.10 do not emit the old actor
  death and vehicle-destruction events. The parser still understands the
  archived format, but current counters remain empty.
- **No automatic mining history.** The game logs no extraction, scan or refinery
  job. Ore sold without a recorded purchase is shown as likely mined, and a
  separate manual mining log is available.
- **No wallet balance.** Trading income is visible because commodity sales are
  logged. Contract and bounty payouts are not, so earnings are labelled as a
  trading floor rather than total income.
- **Crew is a floor, not a roster.** A player who was already connected may
  produce no join event.

Account wipes can be recorded in Settings. Older sessions stay available, but
totals exclude data from the reset categories you select.

## Privacy and safety

- Logs and `Data.p4k` are read locally.
- Item labels and StarStrings write only after you request an install. Each
  keeps a manifest and backup so it can restore what it replaced.
- There is no process injection, memory reading, graphics hooking or telemetry.
- Version checks, UEX and the community dataset are opt-in.
- LAN mode is off by default.

**Settings → Report a problem** creates a small diagnostic file with parser
counts, game builds and integration state. It does not include logs, account
identifiers, handles, folder names or API keys. You can read the file before
attaching it to an issue.

## Requirements

- Windows 10 or 11
- WebView2 for the overlay; it is included with Windows 11 and available from
  [Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/) for
  Windows 10
- The [.NET 10 SDK](https://dotnet.microsoft.com/download) only when building
  from source

## Run from source

```powershell
.\start.ps1                     # tray icon, dashboard and overlay
.\start.ps1 -NoOverlay          # server only
.\start.ps1 -Lan                # allow another device on the network
.\start.ps1 -Rescan             # rebuild the log cache
.\start.ps1 -Path "D:\...\StarCitizen\LIVE"
```

The command-line parser is useful for verification and automation:

```powershell
dotnet run --project src\Quantumwake.Cli -c Release
dotnet run --project src\Quantumwake.Cli -c Release -- --events
dotnet run --project src\Quantumwake.Cli -c Release -- --events --kind commodity.sell,commodity.buy
```

`--events` writes newline-delimited JSON to stdout. Progress and the summary go
to stderr, so the output can be piped directly into another tool.

## Try it without playing

`Quantumwake.LogSim` creates a fake install with logs in the real format:

```powershell
dotnet run --project src\Quantumwake.LogSim -c Release -- --backups 12 --combat
.\start.ps1 -Path "$env:TEMP\QuantumwakeFakeInstall\LIVE"
```

`--live` appends events while the app is open. `--combat` includes archived
kill and vehicle-destruction events so those views can be exercised. Simulated
installs have their own cache and do not mix with real account data. See
[docs/log-simulator.md](docs/log-simulator.md) for all options.

## Architecture

The dashboard is a single web UI hosted by the standalone server and by the WPF
overlay. The release executable embeds the server and web assets, so the normal
Windows download is one file and one process.

```text
        QuantumWake.exe
   ┌──────────────────────────────────────────┐
   │  tray icon      overlay (WPF + WebView2) │      Browser / tablet
   │            ASP.NET Core, in process ─────┼───── HTTP + SSE
   └──────────────────────┬───────────────────┘
                          │
        Quantumwake.Core        Quantumwake.Data
        tail → parse → state    SQLite + game-data readers
```

| Project | Purpose |
|---|---|
| `Quantumwake.Core` | Log tailing, parsing, game-data readers and session state |
| `Quantumwake.Data` | SQLite cache, aggregates and local settings |
| `Quantumwake.Server` | REST API, event stream and static dashboard |
| `Quantumwake.Overlay` | Windows tray application and transparent overlay |
| `Quantumwake.Cli` | Parser verification and JSON event export |
| `Quantumwake.LogSim` | Fake-install generator |

Only the overlay is Windows-specific. The other projects target `net10.0`.

## Tests

```powershell
dotnet test Quantumwake.slnx -c Release
```

The repository currently has 1,043 tests. `Quantumwake.Tests` covers parsing,
session state, stores and game-data readers. `Quantumwake.WebTests` executes the
dashboard JavaScript against a stub DOM.

Parser fixtures are copied from real log lines. The CLI is then run against the
local backup corpus before a release to catch format changes that fixtures do
not contain.

## Documentation

- [Game-data reader](docs/datacore.md)
- [Log-format reference](docs/log-format-reference.md)
- [Missing combat-event findings](docs/findings.md)
- [Architecture decisions](docs/architecture.md)
- [Problem-report contents](docs/bug-reports.md)
- [Release process](docs/releasing.md)
- [Credits and external sources](docs/credits.md)

## Licence and credits

The code is licensed under [Apache 2.0](LICENSE). The name and logo are not
licensed. Manufacturer artwork comes from the official Star Citizen Fankit and
has separate terms; see [NOTICE](NOTICE) before redistributing it.

Quantum Wake builds on community knowledge and tools from StarLogs, all-slain,
SCStats, SCPlay, scdatatools, unp4k and others. StarStrings is made by MrKraken.
The complete attribution list is in [docs/credits.md](docs/credits.md).

Star Citizen®, Roberts Space Industries® and Cloud Imperium® are registered
trademarks of Cloud Imperium Rights LLC. Quantum Wake is an unofficial fan
project and is not affiliated with or endorsed by Cloud Imperium Games.

## Release notes

### 0.9.20

- **Shield generators get their size and grade like everything else.** 203 item
  names were coming out unmarked, and silently: the localisation table keys the
  Lorica shield as `SHLD_BEHR_S02_7MA` while the entity describing it is
  `SHLD_BEHR_S02_7MA_SCItem`, so the lookup missed. Separately, size 1 was being
  treated as "no size" — it is a real size for something fitted to a ship, and
  24 of the game's 73 shields are S1. Together those are 303 more items marked.

- **A shopping line says when you have bought the thing.** Purchases are logged
  by class and lists are written in names, so nothing joined them up. Lines now
  show as bought, struck through, with the date. Deliberately not ticked for
  you: the list is yours, and a name matching a receipt is something the logs
  noticed about it rather than licence to edit it. Only purchases made *after*
  the line was written count, or something bought last month would tick off a
  line added this morning.

- **Mining uses both sources instead of choosing one.** Turning on the community
  dataset used to replace the install's deposit tables outright, which silently
  took away every richness, quality and respawn figure the install supplies.
  They are merged now: 3,163 rows across 255 places, 232 of them described by
  both. Matching needs care, because the install writes "Copper Ore" where
  the download writes "Copper" — joining on the raw names found 164 pairs, and
  joining on the ore found 259. Spelling is left alone: the install says
  Aluminium and the download says Aluminum, and treating those as one would be a
  guess rather than a normalisation.

- **Settings says whether your game files have been read.** The first read after
  a patch takes about half a minute, and until now every page backed by it sat
  empty and several suggested downloading 110 MB to fix what was a wait. Settings
  now shows reading, ready or failed, with what came out: commodities, items,
  recipes, deposits and places. Pages with nothing to show say which of those it
  is, because "not yet" and "never will" had looked identical.

- **Placeholder names stay out of the catalogues.** The game wraps text nobody
  has written yet in angle brackets. One arrived on the Mining page as an ore
  called `<= PLACEHOLDER =>`, and 8,149 of the install's 26,028 items carry the
  same marker in place of a name — a third of the Parts catalogue, every row of
  it reading identically. They fall back to the item's class name now, which is
  at least something you can search for.

- **A rich deposit is no longer advertised at trace concentration.** The same ore
  sits in different rocks at one place: at Fuego, borase is 9.7-74.3% of a Borase
  deposit and 2-5% of a Bexalite one. Ore and place were being treated as enough
  to identify a deposit, so one of those figures was picked arbitrarily, stamped
  onto the row, and the other quietly dropped. Where the install describes a
  place more than one way every variant is kept now and none is guessed at; where
  it does not, the row is filled in as before. Deduping at the same time took
  1,311 rows down to the 783 distinct deposits they actually describe.

- **Parts, Mining and Crafting fill themselves in when the read finishes.** They
  were fetched once, as the page opened — on a cold install that is half a minute
  before there is anything to fetch, so they came back empty and stayed empty
  until the browser was reloaded. Those three were also still recommending the
  110 MB download for tables they now read from your own install.

- **An unreadable backup no longer reports your game files as unreadable.**
  Reading the game files and parsing the logs shared a failure path, so one bad
  log claimed the install could not be read and sent you off to check something
  that was never at fault.

- **Cargo stopped saying its commodity ids cannot be resolved.** They are read
  back into names from your own install — all 20 of this install's cargo receipts
  resolve with no download present at all.

- **The install figures Settings quotes are measured rather than typed in.**
  Three had gone stale: it claimed 1,321 deposit rows across 50 places where this
  build reads 783 across 49.

- **Corrected two claims that had stopped being true.** The release notes said
  the app reads `Game.log` only and never writes to the game directory, which
  item labels made false. The Cargo page said cargo needs no download and then
  offered one, ending mid-sentence in a line left from an older draft.

- **More data comes directly from the game.** Quantum Wake now reads commodity
  names, item details, crafting recipes, mining deposits, place descriptions
  and facilities from `Data.p4k`. The optional community dataset remains useful
  for ship specifications and broader map and mining coverage.

- **Cargo, Market, Loot, Stash and Fleet need less downloaded reference data.**
  The game install supplies commodity names and item identifiers, while UEX can
  add current prices and shop listings when enabled.

- **Parts and Crafting use the live install.** Items include type, size, grade,
  manufacturer, description, volume and shipped-state where the game provides
  them. Crafting includes 1,606 recipes, material quality requirements, craft
  time and blueprint reward pools.

- **Mining has a place ranking and better deposit details.** The page shows ore
  share, quality, respawn time and richness for the locations described by the
  install. UEX adds value estimates; without it, the richness ranking still
  works. Likely mined sales and manually entered runs remain separate.

- **The map can highlight facilities.** Place cards include the game's own
  description, parent location and listed services. Facility filters show where
  to refine, repair, buy equipment or find other services.

- **Item labels have their own page.** Labels can add component size and grade,
  armour class, and a star for gear with no known seller. They can be layered
  over StarStrings. Reinstall and removal now preserve the correct underlying
  file, keep recovery state after a failed restore, and report failure instead
  of claiming success.

- **Grades use the game's A–D notation.** Parts, Crafting, Upgrades and Loadout
  no longer show internal values such as `G3`.

- **Trading earnings can be planned against a goal.** The Now page shows recent
  and lifetime trading rates using in-game time. It does not present those
  figures as total income or claim to know the wallet balance.

- **The CLI can export parsed events.** `--events` produces newline-delimited
  JSON, and `--kind` limits the output to selected event types.

- **Alpha 4.10 wording and smaller fixes are included.** Current logs still do
  not contain actor-death, vehicle-destruction or seat-entry events. Placeholder
  commodity rows are filtered without hiding legitimate names, stash volume is
  totalled, and item-label state detects when another mod replaced its file.

- **The README is shorter.** Setup, limits, privacy and data sources are now at
  the front. Detailed research stays in `docs/`, and older changelogs stay on
  the [GitHub Releases page](https://github.com/peans99/QuantumWake/releases).
