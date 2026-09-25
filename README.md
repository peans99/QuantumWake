<img src="web/assets/emblem.jpg" width="150" align="right" alt="">

# Quantum Wake

**A private Star Citizen logbook for Windows** — by nekron

[![CI](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml/badge.svg)](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Licence Apache 2.0](https://img.shields.io/badge/licence-Apache--2.0-blue)
![1827 tests](https://img.shields.io/badge/tests-1827%20passing-4fd48a)
![Network](https://img.shields.io/badge/network-opt--in%20only-46617a)

Quantum Wake turns `Game.log` into a private local logbook for Star Citizen. It
joins sessions, ships, travel, contracts, crew, inventory sightings and
transactions with reference data from your own game install.

One local pipeline feeds the screens you use in different moments: the dashboard
for planning and history, the in-game overlay for a quick answer, and an
optional paired Cougar MFD HUD for the cockpit. Saved screenshots and copied
`/showlocation` coordinates can add facts the logs do not carry, but only after
you ask Quantum Wake to read them.

![Quantum Wake architecture: local inputs feed one desktop companion, then the dashboard, overlay and paired cockpit MFDs](docs/assets/quantumwake-architecture-hero.png)

*Logs, saved screenshots and game data stay on your PC. The same local service
feeds the dashboard, overlay and cockpit displays; the MFDs receive concise
answers rather than screenshots or raw OCR output.*

The app runs on your PC and keeps its database there. Network features are
optional. Most of Quantum Wake is read-only; item labels and StarStrings are the
two features that can replace the game's English text file, and both require an
explicit install click.

> **Pre-1.0:** pages and stored formats may still change. The database can be
> rebuilt from your logs after an update.

## Install

No installer, no separate .NET runtime, no account. One file.

### 1. Download it

**[Download `QuantumWake.exe`](https://github.com/peans99/QuantumWake/releases/latest)**
from the latest release. The `.zip` beside it holds the same executable plus the
command-line parser, the README and the licence, and is only worth taking if you
want those.

### 2. Get past the unknown-publisher warning

The executable is not code-signed, so Windows will not vouch for it. If you
downloaded it from the release page above, choose **More info → Run anyway**.

SmartScreen stops warning once enough people have run a given build, so a fresh
release warns and a fortnight-old one usually does not. That is a measure of the
release's age and not of its safety.

What the release pipeline does do is scan the files it is about to publish:
after the build, Microsoft Defender is updated to the day's definitions and run
over the executable and the command-line tool, and the release stops if it
finds anything or is unavailable. That is a malware check on the exact bytes
you download, not a statement about who made them — signing would be that.

### 3. Put it somewhere it can stay

Anywhere you can write to — `C:\Tools`, your user folder, a games drive. Two
things to avoid:

- **Not `Program Files`.** The app updates itself in place and cannot write
  there without a prompt every time.
- **Not the Downloads folder**, if you are the sort of person who empties it.

The app updates itself, so where you put it is where it stays.

### 4. Run it

It starts in the notification area rather than opening a window. Right-click the
tray icon to open the dashboard or overlay, check for updates, or quit. The
dashboard is also at <http://127.0.0.1:31337> in any browser on the same PC.

There is nothing to configure. Quantum Wake finds LIVE, PTU and EPTU installs on
fixed drives by itself. If yours is somewhere unusual, `QuantumWake.exe --path
"D:\...\StarCitizen\LIVE"` points it straight at one.

### 5. Wait for the first read

The first start reads every log the game has kept — around 180 files and 400 MB
on an install that has been played for a while. It takes a few seconds to a
couple of minutes depending on the drive, and the page says what it is doing
while it works.

Everything after that is incremental: only the current `Game.log` is watched,
and it is read as the game writes it.

**Where things end up:**

| | |
|---|---|
| Your data | `%LOCALAPPDATA%\Quantumwake` |
| Read from | `<StarCitizen>\LIVE\Game.log` and `\logbackups\` |
| Dashboard | <http://127.0.0.1:31337> |

Nothing is written inside the game folder unless you install item labels or
StarStrings, which are the two features that replace the game's English text
file and both need an explicit click.

### 6. Turn the overlay on, if you want one

It starts disabled. Star Citizen must use **Borderless Windowed** for it to
appear at all — in fullscreen it is behind the game and you will not see it.

Click the overlay's pin button to let mouse clicks pass through to the game;
the tray icon or `Ctrl+Alt+O` brings it back. `Ctrl+Alt+←/→` changes page and
`Ctrl+Alt+F` goes fullscreen.

### Updating

The tray icon checks for updates when you ask it to, downloads the new build and
restarts into it. Nothing is checked until you allow it, and the choice is
remembered.

**Some updates re-read your whole history on the first start afterwards**, which
makes that one start slow. This is deliberate and is how a fix reaches sessions
that were summarised before it existed — 0.9.57 and 0.9.58 both did it, to give
past contracts their real completion times. The release notes say so when it
applies.

### Uninstalling

Delete `QuantumWake.exe` and delete `%LOCALAPPDATA%\Quantumwake`. That is all of
it: nothing is written to the registry, no service is installed, and the game
folder is untouched unless you installed item labels — in which case remove
those from the app first, or the game keeps the marked text file.

## What it helps with

![The star map](docs/images/map.png)

*Visited places are solid and sized by visit count. Empty nodes are known but
unvisited. The map uses logged locations and quantum travel; it is not a live
position tracker.*

| Surface or view | What it answers |
|---|---|
| **Now and overlay** | Where am I, what am I flying, and what changed during this session? |
| **Map, places and points** | Where have I been, where is a place or commodity, and how far is a point from the last copied location? |
| **Flight plan and checklist** | What is my next stop, what has to happen there, and what can be checked off? |
| **Sessions, contracts and crew** | How long did I play, which contracts changed, and who did the log name? |
| **Fleet, loadout and stash** | Which ships and gear have appeared, what fits, and where something was last seen? |
| **Hangar and Garage** | How big is each ship against the others, how do two compare on every number, and what would a different part do to a ship's sheet - DPS, shields, signatures, power - before buying it? |
| **Ledger, cargo and market** | Which transactions were confirmed, what did a counter record, and where is a commodity traded? |
| **Mining, crafting and items** | What the installed game data says about deposits, recipes, parts and shops. |
| **Screen readings** | What a saved screenshot or copied `/showlocation` says, checked against the logbook where possible. |
| **Cockpit HUD** | Optional paired Cougar MFD pages for navigation, tasks, cargo, contracts, money and the live feed. |
| **Item labels** | Optional in-game marks for component size, grade, armour class and hard-to-buy gear. |

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

![Cockpit HUD, saved screenshot, dashboard and local data store](docs/assets/hud-screen-insight.png)

*The cockpit HUD stays focused on a pilot's next decision. Saved screenshots can
add facts that `Game.log` never writes, such as a kiosk balance or a ship's
fittings; they are read locally and kept as dated readings, not treated as live
telemetry.*

## Data sources

Quantum Wake reads three kinds of data:

1. **Your logs.** `Game.log` and its backups provide sessions, travel, ships,
   contracts, party activity, inventory sightings and confirmed transactions.
2. **Your game install.** `Data.p4k` provides names, items, commodities,
   crafting recipes, mining deposits, place descriptions and facilities. The
   first read after a game patch takes about half a minute and is then cached.
3. **Screenshots and copied locations, when you ask for them.** The optional
   screen reader reads the newest saved Star Citizen screenshot or a
   `/showlocation` copied by the game. It never captures the display, reads
   game memory or sends the image away.
4. **Optional community services.** UEX adds current prices and shop listings.
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
- **No complete wallet history.** A readable screenshot can establish a cash
  balance, and logged movements can carry it forward as an estimate. The logs
  still do not state contract or bounty payouts, so trading remains a floor on
  total income.
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

### One local pipeline, three ways to use it

`QuantumWake.exe` hosts the local ASP.NET Core server, the dashboard assets and
the Windows overlay. There is one source of truth: the parser and reading stores
write local data, then the server provides it through REST and the live event
stream. The dashboard is the full logbook; the overlay and MFD HUD are focused
views over the same state.

```text
  Star Citizen install                         Quantum Wake on this PC
  ────────────────────                         ───────────────────────────────
  Game.log + backups ──> Core parser ──────┐
  Data.p4k ─────────────> game-data reader ├──> Data stores + local SQLite
  saved screenshot ──────> optional OCR ───┤              │
  copied /showlocation ──> local reader ───┘              ▼
                                              ASP.NET Core API + live stream
                                                           │
                              ┌────────────────────────────┼──────────────────────────┐
                              ▼                            ▼                          ▼
                       browser dashboard            WPF overlay              paired Cougar MFDs
                       planning and history         in-game glance            cockpit decisions
```

Screen reading is intentionally a file-and-text feature, not live screen
capture. When enabled, it reads a saved screenshot locally and records a dated
result. The dashboard can show the complete reading and its checks; the overlay
and MFDs get only the short answer they need, such as a recent screen summary
or a kiosk balance carried forward by logged movement.

The release remains one executable. The dashboard and MFD pages are embedded
with it, the database stays under `%LOCALAPPDATA%\Quantumwake`, and network
services remain opt-in.

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

The repository currently has 1,815 tests. `Quantumwake.Tests` covers parsing,
session state, stores and game-data readers. `Quantumwake.WebTests` executes the
dashboard and MFD JavaScript against a stub DOM.

Parser fixtures are copied from real log lines. The CLI is then run against the
local backup corpus before a release to catch format changes that fixtures do
not contain.

## Documentation

- [Game-data reader](docs/datacore.md)
- [Log-format reference](docs/log-format-reference.md)
- [Missing combat-event findings](docs/findings.md)
- [Architecture decisions](docs/architecture.md)
- [Cockpit HUD / Cougar MFD mode](docs/mfd-mode.md)
- [Screenshot and clipboard reading](docs/screen-insight.md)
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

### 0.15.33

- **Mining and salvage heads appear in the Garage.** Mining lasers and
  salvage heads now have fitted slots and compatible replacement choices.
  Older reference caches explain when a Settings refresh is needed.
- **The Golem keeps its bespoke Pitman.** Garage and Mining label the fixed
  head and prevent ordinary laser swaps; mining modules remain configurable.
  Mining's fit calculator separates ship equipment and scanned-rock inputs
  into matching panels that stack on smaller screens.

- **Hauling runs start from your last known location.** A pickup where you
  already are comes first, with body and system used to group later stops.
  Deliveries still wait for their pickups. The run names its starting location
  and confidence, or explains when your location is unknown.

- **Cargo after each stop is labelled as a projection.** Unknown quantities
  explain when a card's total has no amount for each pickup, and show the
  known contract total alongside that explanation. A missing drop-off is
  called out when it leaves uncounted cargo on the plan.

- **Hauling contracts no longer disappear when the run cannot be worked
  out.** The cards and the run above them come from two different places,
  and a haul gave up its own card to a run that had not arrived — so if the
  run failed, Shopping showed an empty panel with no contracts, no run and
  no explanation, however many were open. Each haul now keeps its plain card
  unless the run actually put it on screen.

- **A drop-off is no longer listed twice under two spellings.** The
  objectives panel abbreviates where the letter above it spells out — *NB
  Int. Spaceport* against *New Babbage International Spaceport* — and the
  two read as different places, so the run gained a second stop for a
  destination already on it, and the flight plan gained one too. Nothing
  showed it, because the invented stop carried no SCU and the totals still
  added up.

- **"Make it the flight plan" now means the run on screen.** It used to
  work the route out again when pressed, so a card photographed — or a
  contract handed in — in between wrote stops nobody had looked at and
  reported them as though they had been chosen. It now says the run has
  changed, shows the new one, and waits to be asked again.

- **The cargo aboard is settled as soon as it can be.** A multi-pickup card
  prints the total to deliver and no share per source, so after the first
  pickup the amount is unknown — but after the last one the whole total is
  aboard, and after the delivery none of it is. The run used to say
  *unknown* from the first pickup to the end; it now says *293 SCU* once
  every source is behind, and drops the cargo once it is delivered. Two
  contracts of one commodity keep the settled one's figure and call the
  rest a floor.

- **A card the objectives panel cut short is still read whole.** The panel
  shows two or three lines and the rest are below the fold; the letter above
  it lists every pickup, and for a one-source contract every drop-off. Those
  lists now fill in the legs the panel did not reach, so a card no longer
  reads as one pickup short and gets refused as another contract's. A
  Lagrange station's tail — *at the L3 Lagrange of Pyro III*, *at Crusader's
  L5 Lagrange point* — is now the body, not part of the name, so the letter
  and the objective agree on the place. Two cards of the 21 Sep run were
  refused for this; both read now.

- **A card that fits only one contract goes to that contract.** Two open
  contracts with the same title and cargo, one card photographed before the
  second was accepted and one after: the first card can only be the older
  contract's, but the newest compatible card went to it and the other card
  was dropped. Cards that fit exactly one contract are handed out first.

- **A title that wraps onto a second line reads whole.** *Member | Stellar
  Medium Haul | from Ruin* then *Station*: the second line is now joined,
  so the card matches its contract. A screenshot read before this update
  keeps its old reading — *Read again* on the Log page re-reads it.

- **"Plan the run" lands on the run.** The Now page's button took you to the
  top of Shopping, where the hauling run sits below the lists — under
  *Contracts in progress*, off the bottom of the page whenever a list is
  open. With five contracts accepted that looked like an empty page. It now
  scrolls to the run once the contracts have rendered.

- **Cargo stays distinct through a hauling run.** A shared destination now
  keeps a separate unload for each commodity, with the amount still to
  deliver when the card reports partial progress. Every stop shows the cargo
  known aboard afterwards, and says when a multi-pickup card left a source's
  share unknown. The tracked flight plan retains each generated action's
  mission and card-leg identity; it still asks the pilot to confirm loads and
  unloads because the log never names the stop whose objective changed.

- **Hauling cards only guide their own contracts.** When two open contracts
  have the same title, a photographed card now has to agree with the logged
  cargo and pickup count before it supplies a route. A selected card whose
  objectives do not read falls back to the destination in the contract title
  instead of leaving the run empty.

- **Every open hauling contract on one route.** Shopping now has a *Hauling
  run* under the session's contracts: each haul with where it goes, where its
  cargo is collected and how much, then the stops in an order — every pickup
  before any delivery, stops on the same moon together, a place that is both
  ends visited twice — and a button that writes it into a tracked flight plan
  with a load or unload at every stop. The logs give the destination (with
  the StarStrings text mod: *Junior | Stellar Small Haul | to Stanton
  Gateway*), the cargo and how many pickups there are; a screenshot of the
  card on the Contracts app's Accepted tab gives every leg with its SCU and
  which moon each pickup is on. One screenshot per card, and the plan says
  how many are still to take. What it cannot see it says in words — *3
  pickups, places unknown until this card is photographed* — and the SCU
  total is called a floor until every card has been read. The Now page leads
  with the destination when a haul is open.

- **Pickups and drop-offs counted apart.** A hauling contract now reads *2 of
  3 pickups done · 0 of 1 drop-off done* rather than *2 of 4 objectives*,
  from the journal's own naming of its steps.

- **Contracts read as the game names them.** The Contracts and Jobs pages,
  the Now page and the screenshot checks now show the title the mobiGlas
  printed — *Junior | Stellar Small Haul | to Stanton Gateway* — instead of
  a name composed from the mission's internal id. The rep and blueprint chips
  on the Contracts page are read off that title and so had nothing to read;
  they light now.
  Two contracts of the same kind taken in one session are two rows rather than
  one. Every log is re-read once after updating.

- **Safer Controls pictures and more reliable backups.** Picture requests stay
  inside the template folders, and SVG pictures are cleaned before display so
  they cannot run scripts or load external resources. If the game's keybinding
  file is temporarily locked, automatic backups retry after it becomes readable.

- **A crash can leave a trail without exposing your flight.** Settings now has
  an opt-in detailed crash trace. Turn it on, restart, and it records the last
  completed startup stage — reading the game data, Windows OCR and joystick
  setup, WebView2, then the first log scan — plus scrubbed managed errors. It
  stays only on this computer, carries no Game.log lines, screenshots,
  clipboard contents or folder names, is capped at 1 MB, and can be saved only
  from the computer running Quantum Wake. A native crash can still stop the
  process before it logs an exception, but the final completed stage tells us
  where to look next.

- **A component merely seen in inventory does not close a Garage list.** The
  logs never say when a ship part was fitted elsewhere, so a Garage list now
  keeps that purchase open and says *seen in inventory — not counted*. A stash
  sighting is not proof that a loose spare remains.

- **Shopping starts with fewer stops.** Making a run from a list now groups
  what its known sellers can supply into a stop-efficient route before showing
  the choices. It breaks equal coverage ties by price, and every counter stays
  selectable when a different route suits the flight better.

- **A fitted ship stays in Fleet.** Opening a Vehicle Loadout Manager now keeps
  its named hull on the Fleet roster even if it has no logged flight or is
  absent from a later Fleet Manager photo. It is marked as a photographed fit,
  with no invented flight time.
- **The quietest compatible part is marked in Garage.** A *Stealth pick* chip
  identifies the lowest combined EM and IR option for the selected port, when
  the installed data can measure a real difference.

- **Controls has a flight-controls checklist.** Keep your own important
  actions together, see which do not have a joystick assignment, and compare
  them with the defaults the game recommends for your exact hardware.

- **Setting a curve no longer means knowing what an exponent is.** Drag a
  slider instead of typing `1.35`, or take one of five named curves —
  *sharper, straight, soft, softer, very soft* — and the one you are on lights
  up. Underneath, the number is translated into the only terms that matter at
  the stick: *"Half a push gives 29%, four fifths gives 67%."* The dead zone
  you have set is counted in that, because it moves both. The preview is
  bigger, and with the tray app reading your sticks a dot rides the curve as
  you push the axis — which explains what a curve does without any words at
  all.

- **Axes are drawn, not listed.** Every axis on a stick without a picture now
  gets a gauge — a track with its centre marked, the dead zone you have set
  shaded around it, and a needle that follows the axis when the tray app is
  reading your sticks. A rudder is three axes and nothing else, so a
  two-column list of letters was the whole of its picture; this is the same
  information with a shape. It costs a joystick nothing, and with the sticks
  live it shows every axis the device has rather than only the ones you have
  bound.

- **A stick with no picture is drawn as what it is, not as a joystick.** The
  stand-in always started with at least eight numbered buttons, which was a
  joystick talking: a pendular rudder has three axes and no buttons, and got
  eight empty ones with its pedals underneath as a footnote. Nothing bound to
  a button and nothing to read from the device means no button grid at all —
  the axes are the device, so the axes are what it draws, and the line
  underneath says so instead of describing buttons that are not there.

- **A Check tab: is anything missing from your sticks?** Your profile against
  the reference layouts the game ships for your exact hardware. Each thing it
  finds can be staged with one click, into the same pending list as any other
  change, or dismissed for good. It shows its working — how many bindings it
  looked at, how many you already have, how many you took off on purpose —
  because "nothing missing" out of 119 is an answer and "nothing missing" out
  of nothing is a bug.

  Three things stand between a useful list and a useless one, and on this
  install they take 119 bindings down to 2. Defaults you cleared yourself are
  not missing. Actions the game has **renamed** are not missing either — the
  shipped layouts are stale, and 22 of them name actions this patch no longer
  has. And a suggestion you say no to stays said no to.

- **"Beside it" is now "Compare with the game's own layout".** Same control,
  a name that says what it does.

- **A stick wearing the wrong picture now says so.** Pictures are picked by
  hand and nothing checked the pick: a pendular rudder — three axes, no
  buttons — was given a Virpil joystick and drew 31 empty buttons over it
  without comment. The page now compares the picture against the stick and
  says when they disagree, with a button to take the picture off and go back
  to the numbered grid. With the live read it is stated as fact; without it,
  as a doubt, because a stick can carry buttons nobody has bound.

- **Exported keybindings now land under a name the game will actually load.**
  The game lists a profile only when its file is named
  `layout_<name>_exported.xml`, and takes that whole filename at the console.
  The app wrote `<name>.xml` and printed `pp_rebindkeys <name>`, so every
  export made while the game was running — which is the route the app takes
  *because* the game is running — went somewhere the game never looks, and
  nothing said so. The file and the command now match the game's own
  convention.

- **Long action names stay inside their box on a stick's picture.** A name
  the box could not hold on two lines — *Landing System (Toggle)* on the
  throttle's button 26 — was left at full size and spilled a third line out
  of the bottom of the box. Names are now measured word by word, the way a
  browser breaks them, and shrunk until they fit; a name is only cut short
  when a single word is wider than the box.

- **"Take off" is now "unbind".** It removes what is on a control; on a page
  about flying, the old wording read like the other thing.

- **The Controls table no longer writes one column over another.** A long
  action name in *Bound to* ran straight under the *Change* column instead of
  wrapping inside its own — every row in the table wraps now, and the search
  box and *unbind* share a line rather than stacking three deep.

- **Controls pictures no longer repeat or collide with their device name.**
  Diagram labels now use the SVG's modern rich-text layer only, and the
  device subtitle is fitted to the space its template provides.

- **Help now answers the questions the Controls page raises.** Six new
  entries under *Sticks and keybindings*: where the bindings are read from,
  what is backed up before anything is written, why writing is refused while
  the game is running and what it offers instead, how to fix every binding
  landing on the wrong stick after a re-plug, why a button might not light
  up, and where the pictures come from.

- **Assigning an action is now searchable — and browsable.** Type two
  letters in a control's *Find action* field for a short, group-labelled
  match list; the search reads the group name too, so the area you remember
  finds the action you do not. Leave it empty and the same box lists the
  groups, with a count each, to open and read through — which is what the
  old every-action list was good for. Escape closes it.

- **A clearer Controls workspace.** The profile summary now anchors the page,
  every connected stick shares a tidy responsive rail, and the active stick is
  easier to find at a glance. Its picture and bindings now read as one focused
  workspace without changing how any binding is read, staged, or applied.

- **A Controls page, under Settings: your sticks and what is bound to
  them.** Read from the game's own keybinding profile — every joystick it
  knows, by the name and USB id the game recorded — with each stick's
  bindings written onto a picture of it and listed beside it, control by
  control, in the game's own words for the action (*Eject*, *Cycle Lock -
  Hostiles - Forward*) with how it fires (tap, hold, long press). The
  other way round too: every one of the 1,103 actions the game can bind,
  by the keybinding screen's categories, with what is on each stick and
  the keyboard — filter to the ones on no stick to see the sea of buttons
  as a list with gaps. Where the game ships a layout for your stick (the
  Warthog, X52/X55/X56, T.16000M, VKB, T.Flight), it can be shown beside
  yours. The pictures come from [Joystick Diagrams](https://github.com/Rexeh/joystick-diagrams)'
  template library — 45 sticks, throttles and panels — fetched only once
  you press *Fetch the pictures* where the picture would be (or under
  *Pictures*), kept, and credited; a stick the
  library knows (Warthog, T.16000M, X52, X56, VKB Gladiator) gets its
  picture on its own, any other is yours to pick, and a folder of your own
  SVGs in the same convention works too. A stick with no picture gets the
  Windows game-controller panel's numbered buttons instead, lit where
  something is bound, with the hats and axes beside them.

- **Every version of your keybindings is kept.** The game rewrites its
  profile whenever a binding changes and keeps no history; Quantum Wake
  now keeps a copy of each distinct version as it appears, and *Backups*
  lists them, shows what changed between any two ("Eject: was button 7,
  now button 4"), and writes any of them back as a file the game imports
  — with the sticks retargeted, for the day the throttle comes back as
  `js3` and every throttle binding points at the pedals. The export lands
  in Quantum Wake's own folder first; a second, explicit press copies it
  into the game's `controls\mappings` folder, from where Options →
  Keybindings imports it (or `pp_rebindkeys <name>` at the console). Any
  version can be downloaded as a file to keep anywhere, a file can be
  added back — one downloaded from here, another machine's
  `actionmaps.xml`, an export from the mappings folder — and *Restore*
  writes a version straight back over the game's profile: only with the
  game closed, since it reads the profile at start and rewrites it in
  play, and only after keeping the profile as it was, so a restore is
  itself undoable.

- **Mapping, and curves.** A control on a stick can be given any of the
  game's actions from its row, or have its binding taken off; an action
  can be bound to a stick and control from the Actions pane, or taken off
  one. The changes stage into one list — with the clash the game would
  flag named before it flags it — and go with one *Apply*. Each stick's
  axes are an editor too: the curve the game keeps per axis group (its
  exponent, with a preview), invert, and the dead zone per axis. Both are
  written the way a restore is: into the profile with the game closed,
  after keeping it as it was; as an import file for the keybinding screen
  with the game open. The activation mode (tap, hold…) stays the action's
  own unless you pick one on the picker — tap, press, hold, double tap,
  long press. The whole list of what the game can bind is in
  `docs/keybindings.md` (1,103 actions with their defaults, for Alpha 4.10),
  and `--keys` in the CLI prints it with your own bindings beside each.

- **Press it, see it.** Under QuantumWake.exe the sticks are read live
  while the Sticks pane is open: a pressed button lights on the picture
  and in the table, a held switch stays lit, the note names what is down,
  and the fallback grid shows the stick's true button and hat count as
  Windows reports it rather than a guess. A stick the profile names but
  is not plugged in says so; two of one product cannot be told apart and
  the pane says that too. The bare server has no way to read a stick and
  says who does.

### 0.14.17

- **Knives, grenades and attachments, each on its own Armoury tab.** The three
  the Armoury said it did not read yet, read from the install. Attachments
  are split by slot — sights, barrels, underbarrel — and
  shown as what they do to the gun they sit on, as the multipliers the
  files write — a Tacit suppressor is ×0.92 damage and ×0.66 sound, a Stark
  compensator ×1.175 damage for ×0.8 rate, a laser pointer ×0.885 spread — with
  a sight's zoom and its second setting, how far it zeroes and by what step;
  a flashlight says it changes nothing the files put a number on. Grenades
  read as what sets them off and what they do: the MK-4 Frag is a 5 s fuse
  and 120 physical to 4 m, the Scorch Plasma goes on impact and leaves a
  patch doing 10 thermal every 0.4 s within 4.25 m. **Every knife the game
  sells is the same knife in the files** — 30 physical a slash or a stab —
  and the tab says so once above the list rather than printing 30 down a
  column. Each has its finishes and UEX's cheapest price; the game-data
  cache is re-read once on first start. Hover the *Cheapest at* cell for
  the other terminals, or click the row for the wiki's picture, every
  figure the files give it, and every terminal UEX records selling it —
  guns get the full terminal list in their opened row too. An attachment's
  picture is the game's own icon where the install has one (35 of the 73),
  shown without the community dataset; the rest, and every gun, knife and
  grenade, stay the wiki's.

- **Mining names the job in front of you.** Its header now changes with the
  active workspace: *Prospecting*, *Mining fit* or *Haul & refinery*. The
  companion line reports only the current result count, fitted head and slots,
  or the number of refinery jobs awaiting an update; *Your runs* is now
  *Haul & refinery*.

- **The Log page shows which game logs were read.** A new *Game logs* tab
  lists every `Game.log` the install has — the live one and each rotated
  backup — with its size, the session it held, and whether the copy on disk
  is the one Quantum Wake summarised: *read*, *grown* since it was read,
  never read, or *gone* because the game deleted the backup while the app
  kept the session. Below it, every scan the app has run: when, how many
  files, how many actually parsed, and how long it took. When a page looks
  thin, this is where to check whether the log behind it was read at all.
  Scans are recorded from this version on, so the first one appears on the
  next start; logs read by an earlier build say so instead of showing a
  date. A *Scan now* button runs the routine pass without the full re-read
  Settings offers. While a scan runs, the row it is on reads *parsing…* and
  the pass under way sits at the top of the scans table with its count and
  the file it has reached, so a cold re-read can be watched file by file.

- **Read a kit's readiness before inspecting its parts.** The Armoury now
  summarizes the last observed kit's body-zone coverage, weapon and
  consumable attachments, and sighting age above the pilot. Missing entries
  are named as not observed rather than shown as live zeroes; searching a card
  does not alter the kit-wide briefing.

- **The Armoury is clearer and more tactile.** Equipped gear now frames a
  brighter pilot readout, shows exactly how many body zones the log observed,
  and keeps its details one inspect away. Stowed weapons and supplies form a
  visibly separate amber field-kit panel with its own slot count. The page is
  still an observed loadout rather than a claim about live game inventory.

- **Mining fit is the clearer name for the rock calculator.** The tab says
  *Mining fit* and its page asks *Will this fit crack it?*, keeping the focus
  on the ship's heads and modules as well as the rock in front of it.

- **Salvage, fit and refinery work now read as distinct operations.** Salvage
  no longer inherits the mining-only per-rock ranking; it names the location
  evidence the game actually has. The crack calculator shows how many heads
  and module slots its verdict uses, while refinery jobs have labeled handoff
  fields and a clearer waiting queue. Cargo trading remains an evidence-led
  history rather than a guessed hold or route.

- **Dinyx or Cormack, at which station.** A new optional UEX feed,
  *Refining methods* (Settings, ~2 KB): the nine methods with the game's own
  three-point ratings for yield, cost and speed, as a table on the Mining
  page's *Haul & refinery* pane. A run waiting at a refinery now carries a ceiling
  on what it comes back as — the SCU that went in at UEX's best refined
  price, the station's bonus on top where UEX reports one for that ore
  there (40 SCU of copper at 4,200 is 168,000; +9 % at MIC-L5 makes
  183,120) — and names the method it went in under with its ratings. It is
  called a ceiling because it is before the method's own yield and the
  refinery's fee, and neither is published anywhere: the install names the
  methods and no more, and UEX rates them 1 to 3.

- **Salvage, as far as the files go.** A fourth pane on the Mining page.
  Every salvage hull's controller — what its beam scrapes to, what its
  disintegration makes and at what rate per cubic metre, how many heads it
  runs — with its hold and what a full hold of RMC or construction material
  fetches at UEX's best sell, called the ceiling on a trip that it is; every
  scraper module's speed, radius and efficiency, priced; the heads and their
  slots. **What a given hull is worth scraped is not shown, because it is
  not in the game files**: the rule is there (a beam takes 9 mm of hull) and
  the hull's area and volume it would apply to are geometry the data core
  does not hold, nor UEX, nor the logs. `docs/salvage.md` records the probe
  so it is not repeated. (`--salvage` in the CLI prints the same tables.)

- **Does 4 × 32 + 2 × 16 fit in the Hermes?** A cargo-fit panel on the
  Garage page. Type how many crates of each size, and it says whether the
  hull's grids take them and draws where each one goes, layer by layer;
  under that, which of your ships take the load and the smallest hulls in
  the reference that do, each a link that tries the same load there. The
  grids are the community dataset's own placement — one entry per grid a
  hull carries, which is the count the game files withhold, and their sum
  matches the dataset's capacity on all 149 hulls that have one — and the
  crates are read from your install: the 16, 24 and 32 are long boxes one
  lane wide, a crate keeps its top up and turns on the spot, anything stacks
  on anything. A grid's own largest-box rule is honoured (the Corsair takes a
  24 and not a 32; the Cutlass Black's main grid takes nothing over 2 SCU),
  except where the dataset left it at one cell on a big hold, which the row
  says. **The packing is this app's**: a fit found is real; a fit not found
  within the volume is called "no packing found", not "does not fit".
  Refresh the community dataset once (Settings) for the grids to appear.
  (`docs/cargo-fit.md`; `--cargo` in the CLI prints the tables.)

- **An Armoury page, under Gear.** Which rifle, what armour, where to buy
  it — read from your own game files. Every gun the game sells (46 on this
  install, 327 counting the colours) with one projectile's damage by kind,
  every fire mode with its rate, the magazine, the projectile's speed and
  where its damage starts to fall, and every piece of armour (2,349 pieces
  in 384 sets) with the temperatures it keeps you comfortable in, the
  radiation it soaks, what its pockets hold, its signature and its mass.
  UEX's cheapest terminal sits beside each, as the Garage prices a part; a
  blank is no terminal recorded, not free. Click a gun for every mode's
  figures, its drop curve and its finishes with their own prices; click a
  set for its colours. **Two things the files say that the wikis do not
  make obvious**: a gun's damage is on the ammunition its magazine loads,
  not the gun, and armour resistance is by class, not by piece — every
  medium piece lets 70 % of a hit through, every heavy 60 % — so the page
  says that once above the table rather than repeating it down a column.
  Damage a second, per magazine and time to empty are derived by holding
  the trigger down and are called derived; the game publishes none of them.
  Open a gun or a set and the wiki's photograph of it sits beside the figures
  — every finish and colour has its own, a click on the chip swaps it in —
  fetched once and kept, as the Garage keeps a cooler's, and only with the
  community dataset on; the game files hold no picture of a gun beyond a
  64-pixel loadout glyph.
  Knives, grenades and attachments are not read yet, and the page says so.
  (`docs/armoury.md`; `--armoury` in the CLI prints the same tables.)

- **Mining is easier to work from at a glance.** The page now separates its
  prospecting, rock-fit and personal-log workspaces with a stronger operations
  header, clearer tab states, ranked prospect board, structured rock analysis
  and labelled haul form. It remains the same data and calculations, just less
  of a wall of tables when you are deciding what to do next.

- **A second copy of Quantum Wake closes itself.** Starting the app while it
  was already running used to leave the second copy up without a dashboard
  — its own tray icon, overlay, MFD frames and MFD setup window — and the two
  looked like one app. A layout saved in one copy's setup window did nothing
  to the other copy's frames, which read as "the MFD settings don't save".
  The second copy now says the first is running, opens it, and closes.

- **The Mining page is three pages.** *Where to go*, *Mining fit* and *Your runs* are tabs under the heading now, one at a time and remembered, with the kind, system and search filters on the first alone; the page had grown into one long scroll.

- **Mining fit.** A rock calculator on the Mining page. Pick the ship
  — the heads come from its own loadout, the Prospector's one S1, the MOLE's
  three S2, the Golem's Pitman — put the HUD's mass, resistance and
  instability in, choose the lasers, up to three modules a head and a gadget
  on the rock, and it says whether the fit breaks it, how much power reaches
  the rock against what it needs, the heaviest rock the fit breaks at that
  resistance, and the same rock on every other head of that size. The
  lasers, modules, gadgets and minerals are read from your own game files —
  18 heads, 29 modules, 6 gadgets, 31 ship minerals, the game's rock
  constants — and their figures agree with scminer.rocks's, which reads the
  same files. **The line itself is not the game's**: it does not publish how
  mass, resistance and power meet, so the verdict uses the community's rule
  (0.36 W per kilogram at zero resistance, solo from 115 %, with a gadget
  from 70 %), is called an estimate, and says whose it is. The game's own
  constants for the rock are quoted beside it. Each head takes as many
  modules as it has slots — one on the Arbor MH1, three on the Helix II,
  none on the Klein-S1 — read from the head itself. Pick a deposit and the
  game's mix for it is shown, share and chance per mineral with the refined
  price a SCU and a rough worth of a SCU of the mix; per rock is not given,
  because nothing in the files turns the HUD's kilograms into SCU, and it
  says so. Under the calculator, every head, module and gadget the game has
  with its figures and UEX's cheapest terminal and where; the deposit mix
  also shows the raw-ore price and the best refinery station bonus where
  those feeds are on, and values a SCU of the mix at refined prices (before
  the refinery's yield, which is in no file or feed) and raw.
  `docs/mining.md` has the whole model and the dump.

- **A scanned rock reads from a screenshot.** Screenshot the mining HUD's
  scan-results panel with a rock selected and the Log tab reads it — the
  primary mineral, mass, resistance, instability, the game's own SCU figure
  for the rock, and each mineral's share and quality — and offers *Can it be
  cracked?*, which opens the calculator with the rock in the form; the
  Mining page's *Use the last scanned rock* does the same. Written from the
  one public frame available (the wiki's 4.7 panel); the first scan you
  take is what the reader gets checked against. Instability is left as
  typed: the panel prints a figure and the rule wants a percentage, and
  nothing measured says they are the same scale.

- **What you brought back lists only minerals.** The Mining page infers what you mined from ore sold that was never bought; a mission reward or a found trinket leaves the hold the same way, and a Year of the Rat Envelope had been listed as a SCU of ore. The list is kept to the minerals the game's deposit tables name.

- **A ship you own but never flew has a card.** The Fleet page's cards are sorties, so a ship the Fleet Manager listed that the logs never saw aboard — the Ironclad, delivered and not yet flown — was a line in the berth list and on no card. It gets a card under the berths now, marked *never flown*, with its picture, the terminal's word on where it is, and the Garage.

- **The Fleet Manager reads the Ironclad.** The row glyph in the terminal's
  margin came back as a letter — "V Drake Ironclad" — and with two Ironclads
  in the game's list the row named neither. A one-letter first word is
  dropped, and a name the row carries whole is the ship.

- **The refinery figure on the Mining page is a bonus, not a yield.** UEX's
  refinery feed reports a station's percentage points on the method's yield
  — +9 at MIC-L5 for copper, −5 at Nyx Gateway for iron — and the deposit
  table had shown it as "Yield 9%", which read as nine percent of the ore
  coming back. It is now *Bonus*, signed.

### 0.13.33

- **The Garage counts your pips.** The HUD draws one lead indicator per
  projectile speed among the guns you fire, so guns at two speeds are two
  pips and a lead that lands one gun misses the other. The Weapons card now
  has a *Projectile speed* row — one speed, or *2 speeds · 2 pips* in amber
  with each speed's guns named — the sheet's notes say so, the Hangar
  comparison has a *Pips* row, and on the bench a gun that would add a pip
  wears a *+1 pip* chip, with its speed among its figures. The stock Gladius
  is two pips out of the box (Panthers at 1,480 m/s, Mantis at 1,332); the
  Hermes' four Rhinos are one.

### 0.13.32

- **The Garage opens a ship as its screenshot showed it.** With a loadout
  photograph read — a Vehicle Loadout Manager frame or the Fleet Manager's
  loadout estimate — the bench now starts from it the moment the ship is
  opened, dated, with what the screenshot did not settle listed under it;
  before, it opened at stock until you pressed *Start from the photographed
  fit*. A tick above the bench, *Start from the newest photograph*, turns
  that off and brings the button back; it also acts on the bench you are
  looking at, and it is remembered in this browser like the paint pick.

- **A ship wears the paint its screenshot showed.** A loadout screenshot
  names the livery — the Hermes' estimate listed *Hermes Keystone Livery* —
  and that paint now stands in on the Fleet card, the Hangar and the Garage
  until you pick one yourself, labelled as photographed and dated, in place
  of the first paint the game happens to list. Your own pick still wins.

- **Every screenshot in the folder is read, the ones already there
  included.** With *Watch screenshots* on, the app used to read only what
  landed after it was switched on, which meant a loadout photographed the
  evening before was never seen: on this install the only photographs of the
  Hermes' fit — eight Vehicle Loadout Manager frames and its loadout
  estimate — sat unread for a week while the Garage said nothing. The watch
  now reads the archive too, newest first, a few files at a time, and the
  Log tab says how many are still to come. Untick *Watch screenshots* to
  stop, or invalidate a reading you do not want believed. With the watch
  off, a **Read N older screenshots** button does a one-off read.

- **The Garage says when it has no photograph of a ship** instead of hiding
  the section. Reading the Fleet Manager's loadout estimate now fills every
  port of a counted kind (*Cooler ×2* is both coolers), and its *Quantum
  Drives* row — which the engine reads with an O — reaches the quantum drive
  port.

- **A paint picked anywhere shows everywhere.** Choosing a paint on the
  Garage, the Hangar or a Fleet card redraws every picture of that hull on
  the page; before, a pick made on one page reached the others only after a
  reload.

- **The Hangar's cards open the Garage too.** Each ship card in the Hangar
  gallery carries the same *Garage* button as its Fleet card, so a ship you
  are looking at can be taken to its numbers, what fits it and what a part
  would change without going back through Fleet. Ground vehicles get no
  button there, as on Fleet: nobody sells parts for their ports.

- **The Garage no longer shows one ship's sheet under another's name.** A
  slow answer for the ship you had just left could land after the one you
  picked and overwrite it - Ship B selected, Ship A displayed, and a build
  saved then carried A's ports under B's class. Every ask the page makes
  (the ship, a port's candidates, a refit) now carries a ticket, and an
  answer that is no longer the newest is dropped.

- **Changing one cooler no longer changes its twin.** Two ports that started
  alike and were fitted apart show as two rows, but the change was decided
  from the stock fit, so a part fitted to cooler 1 went on cooler 2 as well.
  Rows are now decided from the fit on screen, on the bench and in Auto-fit.

- **A swapped missile rack takes its missiles with it.** Replacing or
  emptying a rack left the stock missiles in the missile count and damage.
  They now leave with the rack; the reference says what the stock rack
  carried and nothing about what another would, so the sheet counts the new
  rack for itself and a note says why the missile row fell.

- **The bench filter keeps its focus.** Typing "cold" left "c": every
  keystroke rebuilt the box it was typed into. The rows redraw around it now.

- **A purchase undone on the bench leaves Shopping too.** *Back to stock* and
  *Reset to stock* now reconcile the list the Garage keeps for the fit; a
  bench back at stock takes its list off Shopping and says so. A bench that
  never made a list still makes none.

- **A destination you chose on Shopping is yours.** Updating a Garage list
  used to write the newly proposed shop over it, or clear it when nothing
  resolved. A destination you picked is kept, and the proposal is offered
  beside it instead.

- **The diagnostic summary redacts paths.** The one free-text field it
  carries - why the game data could not be read - is an exception's message,
  which on a file error names the file, user folder and all. Every Windows,
  UNC and Unix path in it is replaced with `<path>` before the text reaches
  the clipboard.

- **The pilot/turret DPS split says when the dataset disagrees.** On seven
  hulls - the Asgard, Cutlass Steel, Starlancer MAX and TAC and their Wikelo
  variants - remote turrets are named as pilot mounts in the loadout, so
  their guns sit in the pilot row where the dataset's own total puts them on
  turrets (the Starlancer MAX reads 7,373.6 pilot DPS here and 4,102 there).
  Those hulls now say so beside both rows, with both figures; the guns are on
  the sheet either way and the sum agrees.

- **A compared picture says what it is.** In a Hangar comparison each ship
  wears a small badge on its picture - *top-down icon* for the game's own
  vehicle icon, which is the hull's real footprint, or *gallery render* for
  the three-quarter paint render fitted into that footprint's box - so two
  pictures of different kinds side by side cannot be mistaken for the same
  kind of picture.

- **"Prices are getting old" is a chip, except where it matters.** The
  notice keeps its full wording and buttons on Market, the commodity page,
  Garage, Shopping and Routes, where an old price is a wrong number. On
  every other page it shrinks to one line: the fact stays in view, the
  paragraph and the buttons stop crowding a page that is not about prices.

- **Copy diagnostic summary.** Settings › Report a problem has a second
  button beside *Save a report*: one click puts a few lines of text on the
  clipboard - app version and build, whether the install was found and how
  many backup logs it has, the game-data read, the community dataset, UEX
  prices and feeds with their ages, sessions read and counted, game builds,
  parser health, the counts behind each page and the wipe line. Built from
  the same chosen lists as the full report, so no handle, id, folder or key
  can be in it; where the clipboard is not available the text is shown to
  copy by hand.

- **A backup with saved Garage builds now refuses an older build.** The
  backup format is 2: a build before 0.13.3 restoring a newer backup says to
  update rather than putting everything else back and dropping the builds
  without a word. Backups from older builds restore as before.

- **Comparing two ships in the Hangar compares their numbers now, not just
  their outlines.** Pick the pair in the Hangar's own *Compare … with …* bar
  (or on two Fleet cards, as before) and a side-by-side appears under the
  deck: size from your game files, your own sorties and hours from the logs,
  and the Garage's recomputed sheet for each hull - hull HP, mass, crew,
  cargo, fuel, SCM and boost, pitch · yaw · roll, shield HP and regen, pilot
  and turret DPS, alpha, missiles, quantum speed, range and spool, EM and IR
  signatures, power and cooling - with UEX buy and rental prices, claim wait
  and expedite fee where known. The better figure for each row is lit the
  right way round (less mass, less signature and less money win; more of
  everything else does) and the difference is given in the row's own unit.
  Size and use carry no verdict. The comparison can be cleared from the
  Hangar, which it could not be before, and picking the same ship twice is
  refused. A hull the community dataset cannot draw keeps its size and use
  and the note says why the rest is missing. The to-scale deck draws a
  compared pair to fit - both on one shelf whatever the zoom, and no bigger
  than the game's 256-pixel paint render can stand - where a hull wider than
  it is long (the Hermes is 73 m across) used to overrun half the deck and
  push the other ship under the fold, so the comparison seemed to show one
  ship, very large and blurred. If only one of the two has an installed
  top-down icon, it now stays just as compact while the other is explicitly
  named below as unavailable to draw. When the game has that missing hull's
  Gallery render, the comparison now uses it inside the hull's verified
  length × beam box, labelled *gallery render*, so both selected vessels are
  present without pretending a perspective picture is a top-down silhouette.

- **Start the Garage bench from a screenshot of the ship.** When the screen
  reader has read a Vehicle Loadout Manager frame of the open ship, the
  Garage offers it above the bench - *RSI Hermes, as photographed 12 Sep
  2026 21:55* - with how many ports it settled, how many differ from stock,
  and a list of what it did not settle and why: nothing read under a port, a
  line that named no one part, a reading two parts answer to. *Start from
  the photographed fit* puts those parts in the bench and the sheet moves to
  match; *Reset to stock* takes it back. It is offered, never applied on its
  own, and it is dated: a screenshot is a moment, not a state, and the ports
  are matched to the screen's labels by kind and number rather than read
  from it. The ship's name has to have read exactly - a frame that only
  looks like a Corsair is offered to no bench.

- **One Garage fit, one shopping list.** Fitting a component sold at a terminal
  now adds it to the ship's existing fit list instead of creating a second,
  one-part list. Using *Add changed parts to Shopping* refreshes that same
  list, and folds in the older automatic one-part list when it finds one.

- **Terminal aUEC only now filters an open Bench as well as Auto-fit.** Toggle
  it while comparing a port and the Bench immediately leaves only compatible
  parts with a recorded NPC-terminal seller, while retaining the fitted part
  as the useful baseline for comparison.

- **Half the parts that had no picture have one now.** The bench used to ask
  the Star Citizen Wiki for a part's picture by the name of its page, and
  about half the bench came back empty - most coolers, shields and power
  plants among them. It now asks by the game's own id first, through the
  wiki's item API, which also gathers the pictures cstone.space's item finder
  and the German star-citizen.wiki hold. On this install that filled 48 of
  the 78 blanks - 14 of 15 coolers, every plant and drive, 10 of 11 shields.
  Radars, missile racks and turrets mostly stay on the maker's mark: nobody
  has photographed them anywhere public, and the app does not pretend
  otherwise. Parts already recorded as "no picture" are asked about again
  once, so nothing needs clearing.

- **Who is selling a part on UEX's player marketplace, on the Garage.** A new
  optional feed under UEX in Settings - *Player marketplace*, off until you
  fetch it, and enabling UEX prices does not turn it on - reads the newest
  five hundred advertisements on uexcorp.space and joins them to the bench by
  item. A candidate with a player offering it shows the cheapest ask under
  the terminal price as a *Player listing* - who, where, how many, and a
  link to the advertisement on UEX, which is where the deal is made. Under
  the bench, *For sale by players* lists every advertised part that fits a
  port on the open ship at its size, newest first, with a Bench button that
  opens that port. Asks are shown as asks, never as prices: the note says
  how many of the feed's listings they are, when it was fetched, and that
  two days of advertisements are not a verdict on the market. The optimiser's
  *Terminal aUEC only* ignores them, as it says. Only what UEX alone knows is
  taken from it - the ask, the seller, the place, and which of its item ids
  the seller picked; what the item *is* comes from your install, as
  everywhere else on the bench.

- **Every component maker has a logo now, not a four-letter monogram - the
  game's own.** The Fankit only ships marks for the fifteen hull makers;
  Behring, Juno Starwerk, Klaus & Werner and the other forty-five that make
  coolers, shields and guns had none, which is why half the bench read ACOM,
  JUST, CHCO. Their marks were in `Data.p4k` all along: every manufacturer
  record names a 256-square logo texture under `UI\SharedAssets\ManufacturerLogos`,
  and the install has one for 127 makers - 57 of the 59 on the bench. They
  are decoded once into `maker-logos\` like the ship silhouettes, offline.
  The two the install lacks (Associated Sciences, and any future maker) fall
  back to the Star Citizen Wiki's manufacturer page, and a part with no maker
  keeps its monogram.
- **The Garage now starts with the ship, not a wall of numbers.** Its game
  picture is centred in a fitted-layout panel, with the components currently
  slotted into it arranged as clickable cards around it. Each card carries the
  component picture or maker mark, size and grade, plus the one or two figures
  that matter for that kind. Pick one there to open its compatible replacements
  on the Bench; the ship card, selected component and recomputed sheet all
  stay in step. The component positions are intentionally not drawn onto the
  hull: the data names real ports but supplies no 3D coordinates to guess at.

- **A component click now lands on its choices.** Choosing a card in the
  fitted layout smoothly brings the compatible-parts results into view and
  gives that result panel keyboard focus. The ship's centre readout also now
  includes remaining power capacity (or how far over budget the fit is), so a
  brown-out risk is visible before changing a part.

- **Fitting a buyable component now makes its shopping list automatically.**
  The list contains that new part (and every identical port fitted with it),
  names its best known destination, and is ready on Shopping. Parts the price
  data cannot place remain fit-able without creating an un-routable list.

- **Auto-fit answers a specific question instead of guessing at a generic
  “best.”** Optimise the whole loadout for stealth, alpha or sustained DPS,
  missile damage, shield capacity, quantum speed or range, cooling, or power
  headroom. Turn on **Buyable via UEX** to choose only parts with a known
  seller; the sheet shows exactly what the selected goal changed before you
  add anything to Shopping.

- **The Garage labels its ship-discipline marks and does not treat a missing
  game tag as a verdict on a component.** Its key names the civilian,
  industrial and military marks. The raw `flightReady` tag no longer labels a
  part or excludes it from Auto-fit: a missing tag is not proof that the part
  cannot be used.

- **Components now name their own class and acquisition route.** A Military,
  Industrial or Civilian mark comes from the installed game's description,
  rather than from the ship carrying it. Terminal aUEC, Blueprint recipe and
  no-terminal-seller states are distinct; Auto-fit's optional filter now says
  exactly what it can guarantee: a known NPC-terminal aUEC seller.

- **The ship sheet is now legible at a glance.** Hull, flight, weapons,
  defence, signature, systems and quantum read as distinct instrument cards:
  each has its own accent, mark and header, while the explanation for a figure
  is set apart as a small callout. The data is unchanged; it is simply easier
  to scan while choosing a fit.

- **The sheet uses the full display, and each system now feels like an
  instrument.** On a wide screen the seven system cards form one complete
  bank rather than leaving Quantum stranded below the others. Brighter system
  accents, a large card mark, subtle watermark and contained readouts give
  each card a clearer identity without hiding the data.

- **Each Garage system now has its own visual shorthand.** Hull is a frame,
  flight an arrow, weapons a reticle, defence a shield, signature a scanner,
  systems a circuit, and quantum a drive vector. They pair with the cards'
  coloured accents so the section you want is recognisable before reading it.

- **Ships now carry a small purpose badge in the Garage.** Industrial, military
  and civilian are readable beside the ship name, with a distinct mark and
  colour. The badge is derived from the community reference's career and role;
  its tooltip shows exactly what supplied that classification.

- **The part's own picture on the bench.** Where the Star Citizen Wiki has a
  cutout of a cooler, gun or drive, the bench and the candidate list show it
  instead of the maker's mark - the game files carry no picture of any part,
  so the wiki is the only place one exists. It has one for about half the
  bench (most guns and power plants, almost no radars or missile racks); the
  rest keep the mark. Pictures are fetched once, by the part's name, only
  after the community dataset is on, and kept under
  `community\part-pictures`.
- **Add the bench to your shopping list.** Once the bench differs from stock,
  **Add to shopping list** writes the changed parts - two coolers is one line
  with a two on it - as a job on the Shopping page, named after the open build
  or "*ship* fit", and proposes the one stop for it: the terminal that sells
  the most of the list, the cheapest such terminal when two tie, from the UEX
  prices you already have. It says what that stop lacks ("Not sold there:
  Endo") rather than sending you somewhere for half of it, and with UEX off
  the list is still written, just without a destination, and says why.
  Emptied ports and ports put back to stock are not purchases and are left off.
- **Save a build, open it later, set two side by side.** *Save build…* keeps
  the bench's fit under a name - "Starlancer, quiet fit" - in `builds.json`
  beside your jobs: it survives rescans, travels in a backup, and is
  recomputed from the reference every time it opens, so a dataset refresh
  that changes a part's figures changes the build's with it. The ship's
  builds sit as chips under the bench; open one, change it, *Update build*.
  *Compare against* measures the struck figures from a saved build instead
  of stock, which is how two fits are read against each other without a
  second sheet. The Fleet card's **Upgrades** button is now **Garage** and
  opens the page on that ship; the old panel is gone.

- **The bench: try a part and watch the sheet move.** Under the sheet, every
  port a shop sells parts for, grouped by kind with the maker's mark, the
  fitted part, its size and grade, and the figure that matters for its kind.
  Pick one and every part that fits is listed best-first, each figure shown
  beside how it compares with what is fitted now - more coolant in cyan,
  more IR in amber - with its price and where it is sold when UEX is on. Fit
  it and the sheet above redraws with the old figure struck beside the new,
  coloured by whether it got better *for that figure*. Identical ports fold
  into one row - sixteen missiles are one decision - and a fit goes on all of
  them. The game files carry no picture
  of a component, so a part's face is its maker: the Fankit mark where the
  app has one, a monogram of the maker's initials where it does not.

- **The Garage: every number about a ship, recomputed from its parts.** A
  new page under Gear. Pick a ship - yours first, most flown at the top, or
  any of the 269 in the reference - and read its sheet: hull, flight,
  weapons (pilot DPS with every gun named, turret DPS, missiles), defence
  (shield HP and regen, the pool cap when it bites, the armour's signal
  multipliers), **signature** (EM by component group and IR, shields up and
  in quantum), systems (power drawn against generated, cooling load) and
  quantum (speed, spool, range, fuel). Every figure is worked out from the
  fitted parts with the community dataset's own model, and checked against
  its published totals on every ship: EM, IR, power, cooling, shields and
  range agree on 269 of 269. Signatures are the shields-up,
  everything-at-maximum scenario, and the page says so. Needs a refresh of
  the community dataset, which now keeps the part figures it used to drop.

### 0.12.4

- **Give a server the name you saw in game, without losing its real id.** Add
  a local “Seen in game” label such as `amazing_view`; it is clearly your
  observation, not a claimed translation. The full log shard id remains beside
  it, searchable and one click away with **Copy ID**. The Now card and the MFD
  navigation page and the MFD Server page use the label when you have recorded one.

- **Keep a personal good/avoid call and a concise support line.** A server can
  be marked Good or Avoid for this install only. **Copy report** puts a
  privacy-safe line on the clipboard with the shard id, region, time and log
  ending - no account, IP address or log contents. A new Back-end failures
  filter and summary count only explicit back-end disconnects; an unclosed log
  still stays labelled as ambiguous.

- **A release is now scanned before it is published.** After the Windows build
  makes the exact app and command-line files that ship, the release workflow
  updates Microsoft Defender's definitions and runs it over both before making
  the download or announcing
  it. If Defender is unavailable or removes a release file, the release stops.
  This is a malware check on the published files, not a claim that Windows
  knows the publisher - code signing remains separate.

- **The app now knows which server you were on.** Every time the matchmaker
  places you, the game writes one line naming the shard -
  `pub_use1b_12545750_150` - and the app now reads it. A new **Servers** page
  under Flight lists every shard this install has been placed on: region,
  how many times, how long, when last, and how each stay ended - left by
  choice, kicked for idling, quit to desktop, or the log simply stopping.
  Across the 193 backups on the reference install that is 269 placements on
  152 distinct shards, 68 of them visited more than once.

- **Write a note on a server, and star the ones worth keeping.** Click the
  note cell to write it, the star to favourite it. Notes and favourites are
  yours: they live in `shards.json` beside your jobs, survive every rescan and
  cache wipe, and travel in a backup. Search reads the note as well as the
  name, so "the good one" finds the shard you called that.

- **The Now page tells you where you landed, and what you said last time.**
  A Server card appears the moment you are placed: the shard as a pilot says
  it - "US East 150" - how many times you have been here before, and your
  note if there is one. That is the moment the note is worth having: relogging
  is a decision at the hangar and a complaint at the bunker.

- **Servers from an earlier deployment are shown as history, not advice.** A
  shard lives only as long as the deployment it belongs to - the middle number
  in the name, which is not the client's build - and a new deployment retires
  every shard of the old one. Those stay listed, greyed and marked, one tick
  away from the default view; the note is still yours, the server is gone.

- **Each server lists where you went while on it.** A Visited column on
  Servers names the last place you landed during your stays on that shard,
  with a "+N more" button that unfolds the rest in place, and search finds a
  shard by a place. Arrivals made in the menu or on a previous shard of the
  same session are not credited. The game may show a friendlier name on
  screen - `amazing_view` - while writing only the id to the log. You can save
  that observation locally; Sessions and the debrief keep the canonical id in
  full.

- **The shard you are on is lit.** On the Servers page the row for the
  server the game has you on right now leads the table, highlighted and
  marked *on now*, and the light moves the moment the live feed sees a new
  placement. A summary tile says which it is, or that you are not on one.

- **A Server page on the MFD.** Under Pilot, beside Session: the shard you
  are on, the region, how long you have been on it, how many times before,
  and your note - the page to glance at right after the loading screen.

- **The server you just joined is on the list straight away.** The Servers
  page used to know only the sessions already summarised, so a shard joined
  minutes ago could be "on now" with no row to star or note; the live session
  is now folded in, its stay reading *on it now* rather than *log ended*.
  Rejoining the same shard within one session now counts the stay that just
  ended, and only the world's channel going down ends a stay - the menu's
  own channel never did in 195 logs, and now cannot.

- Sessions gain a Server column and the debrief lists every stay with how it
  ended. Sessions summarised before this build have no shards recorded, so
  the first scan after updating re-reads every log once.

### 0.11.32

- **MFD captions can be lined up with the frame's buttons without moving the
  opening.** The on-screen labels sat a fixed way in from each edge, so on a
  monitor smaller than the frame the first label missed the first button, and
  the only way to line them up was to drag the whole display out past the
  opening. Four new fields in MFD setup - from left, right, top and bottom, in
  pixels - move each row of captions on its own. Blank keeps the old placement.

- **"Screen needs review" no longer sits on the Now page with nothing to
  resolve.** The current screenshot is now the newest one taken, not the one
  read last - re-reading an old frame put a three-day-old kiosk on the Now
  page as the state of things. Its wallet was then checked against a baseline
  taken *after* it, and reported two million aUEC leaving the wallet "since" a
  moment the frame predates; each shot is now checked against the newest
  wallet read before it. And a disagreement between a screenshot and the
  logs is a finding, not a fault - the wallet check exists to find money the
  log never saw - so the hub now says what differs, and only for a shot from
  this session and the last half hour, instead of asking for a review that
  had nothing to do.

- **Turn a haul into a planning workspace.** Trade Routes now scores
  repeatability from report freshness, capacity and alternate buyers; keeps the
  cargo purchase cost visible as exposure; records known refuel/repair and pad
  amenities at each end; saves private presets and watches; and proposes
  price-report return-load circuits. When the optional ship catalogue is absent,
  a ship the install identifies can use its latest verified kiosk screenshot
  hold size (or a clearly marked local entry when none has been read) rather
  than being treated as incapable of hauling. Saved or alternate routes remain
  ordinary, editable flight plans — the local route history is not game
  telemetry.

- **Know whether a route is actionable before reading the table.** The route
  header now keeps the selected ship's SCU, evidence source and screenshot age
  visible, with a compact readiness badge. “Best safe route” retains your ship,
  wallet and pad preference, defaults an unset security preference to monitored
  space, and marks the most reliable available result.

- **Choose the kind of trade run you will accept.** Route filters can now
  prefer monitored space, avoid or require lawless systems, and require
  recorded landing-pad amenities at both ends. Every row names both security
  states and pad records; those records never claim a particular hull fits.
  Backup buyers are now individually saveable alternate flight plans.

- **Plan with the ship you chose.** The trade-route picker is a persistent
  choice from your flown roster, and it knows each hull's hold from the same
  fleet data the rest of the app uses - the Hermes reads 288 SCU the moment
  the page opens, not after the ship catalogue finishes loading. Routes and
  cargo leads are withheld only for a hull known to carry nothing: a fighter,
  or a ground vehicle whatever its grid says. A ship nobody has sized gets the
  per-SCU table with a note saying so, and "On foot / no ship" is a choice
  that stays chosen rather than snapping back to the last ship flown. Quantum
  range and vehicle-bay fit stay explicitly unknown until a source can prove
  them.

- **Filters look at every route, and a hangar counts as somewhere to land.**
  Security and pad choices used to sift only the thirty routes already picked,
  so "Monitored only" could show one row and claim no monitored route existed.
  They now run over the whole ranking. The four Stanton cities list only a
  hangar, never a landing pad, and were being dropped by "XL pad both ends"
  with every major city in the list. Return-load circuits honour the same
  filters as the table above them, and printing the recap no longer hijacks
  Ctrl+P on every other page.

- **Share a polished pilot recap without uploading anything.** Choose the
  latest session, fleet snapshot and/or 30-day trading snapshot in Settings,
  then review it, save a self-contained HTML page, or print it to PDF. The
  report keeps its source labels and says that log-recorded trading is a floor,
  not total income.

- **Answers now carry clear source labels.** The Now page, Hangar and
  commodity Market show whether a fact comes from your game log, installed
  game files, a locally read screenshot, or optional community data. Inferred
  figures say so too, and Help explains what each label can and cannot prove.

### 0.11.22

- **The wallet reads from screenshots that used to say it could not.** A
  mobiGlas frame would report the balance as "printed in a face this engine
  does not read" while the figure sat plainly beside your handle. That was a
  guess, and a wrong one: on four frames measured the engine read the same
  bold italic figure on two and dropped it on the other two, including two
  frames taken a second apart in the same scene. When the bar reads and the
  balance does not, the panel is now cropped, stood upright and read again,
  and a figure is accepted only when two readings agree on its digits - the
  engine either read every one of them right or returned nothing, and the
  one treatment that could fabricate a digit was left out. Both frames that
  failed now read; a frame that still misses says so plainly and suggests
  another screenshot, rather than blaming the font.

### 0.11.21

- **The HUD no longer prints the game's own markup at you.** Contract lines
  arrived wrapped in tags - `Gabriel Lassort Elimination <EM4>[100 Rep]
  [BP]*</EM4>` - and the in-game feed showed them as written. The dashboard had
  been hiding them all along, which is why only the HUD looked wrong. They are
  now taken off where the feed is served, so every surface gets the same clean
  line.

- **The activity feed names what you bought.** On the Now page and the in-game
  HUD it read `Bought cds_legacy_armor_heavy_helmet_01_01_12`, because the
  sentence is written while the log is read and nothing at that moment can name
  an item. The line now keeps the engine class beside it and the name is put in
  when the feed is shown, so it says what you actually bought. Sessions already
  summarised are read again once on this update to pick it up.

- **Things you bought, picked up, wear or have stored are named properly.**
  Some items came out as their raw engine class - `behr_gren_frag_01` rather
  than MK-4 Frag Grenade, `slaver_undersuit_01_01_01` rather than Stoneskin
  Undersuit. Two catalogues in the install know item names and they do not know
  the same items; only one was being asked. On one pilot's history that was 11
  of 124 bought lines. Anything neither catalogue names still shows its class,
  because a blank line would say less.

- **The Fleet page shows what your ships were last photographed carrying, and
  where they were parked.** Both sections were built and neither had ever
  appeared: the line that drew them was missing, so the page looked as though
  it had nothing to show. Photograph the Vehicle Loadout Manager and each
  ship's fitting is listed with the date it was taken; photograph the Fleet
  Manager and the page can say where each ship is, which nothing in the logs
  ever records.

- **Your balance is read from screenshots far more often.** The reader used to
  find the figure by looking for your handle beside it, which quietly failed
  twice over: on any map screen, where the game prints your name a second time
  in the opposite corner, and for any handle the text reader garbles. One
  measured frame reported the balance unreadable while it sat in the
  screenshot, correct to the digit. It is now found by the mobiGlas bar itself
  and your name is not involved.

- **Save the Hangar scale deck as a picture.** Export PNG makes a 2× image
  ready to share; Export SVG keeps the drawing sharp at any size. Both carry
  the currently visible game paint renders, so the saved picture still works
  after Quantum Wake is closed.

- **Choose how the scale deck is arranged.** Keep ships and ground vehicles
  separated, group them into useful size bands, or put the full fleet on one
  shared deck. Each arrangement keeps one physical scale.

- **To scale now wears the same chosen or default paint as Gallery.** The
  picture is fitted inside the game's exact bounding box, so the paint is
  visible without making a claim about the ship's real-world footprint.

- **Hangar paint renders no longer show a second, unpainted ship behind them.**
  The chosen game render now stands on its own; if it cannot load, the card
  falls back to the silhouette instead of layering both poses together.

- **The scale deck no longer reserves giant blank rectangles for hulls whose
  top-down icon is missing.** Their exact installed dimensions appear below the
  drawing, because a made-up marker is not a useful picture of a real ship.

- **The Hangar scale deck fits the visible window.** It measures after the
  Hangar opens and redraws on resize, so a narrow window does not start with a
  needless horizontal scrollbar.

- **A Hangar paint that cannot load falls back to its maker-tinted silhouette.**
  The fallback remains visible without placing two ship poses on the same card.

- **The ship catalogue shows every shop and rental desk on hover.** The buy
  price and "Cheapest at" cells list everywhere UEX has seen the ship sold,
  cheapest first, and the Rent cell lists every desk that rents it - with a
  "+N" beside the cell when there is more than one. The cheapest is still
  the number; the nearest is usually the one you want. The full shop list
  fills in at the next UEX refresh; until then a ship shows the one shop it
  already knew.

- **Ships show their default livery when the game has a picture of it.** The
  paint folder in the game files holds the stock finish of 26 hulls that no
  paint item points at - the Clipper, Hermes and Paladin among them. Those
  now come first in the Paint list as "Default livery", and a ship you have
  not picked a paint for wears that. For a hull without one - most of them,
  the Corsair included - there is no picture of the stock finish anywhere in
  the files; the first paint still stands in, and the card says why.

- **Wikelo has a current goal.** Set one of his trades as your current goal to
  keep it above the emporium while you browse other groups. The pin stays in
  this browser, like the roster tick and paint choices.

- **Fleet is easier to work from.** Favourite ships, filter to favourites,
  ships, ground vehicles, or flights from the last seven days, and see those
  states on each card. Click a ship picture for its flights, time aboard, last
  flown, and its size from the installed vehicle table. Choose whether unpicked
  ships show the game's first paint or the maker-tinted silhouette.

- **Compare two ships in the Hangar.** Select two Fleet cards, then open their
  shared to-scale Hangar view. The comparison is temporary and does not change
  your roster.

- **The Hangar's big ships share a shelf again.** In the to-scale view, the
  gap between ships is now included when the deck is sized, so two large ships
  fit side by side at the normal zoom instead of the second one wrapping below.

- **Ships wear a paint before you pick one.** A ship you have not chosen a
  paint for shows the first paint the game pictures for its hull, on Fleet
  and in the Hangar gallery, rather than the tinted silhouette. It is labelled
  as a stand-in - which paint yours wears is not in the logs - and the Paint
  list opens on it and says so. Choosing the silhouette is a choice too, and
  it is kept.

- **The Hangar keeps to your roster and shelves ships and vehicles apart.** A
  ship you unticked on Fleet is not in the Hangar either, and the count says
  how many it left out. In the to-scale view, ships sit on one shelf and
  ground vehicles on another - the game's own word for what moves each - at
  the same scale, so the Ursa beside the Starlancer is the real difference.
  A paint picked on the Hangar now shows at once rather than after a reload,
  and a render that fails to load once keeps your pick.

- **The Hangar opens as a gallery.** One card a ship - the game's render in
  the paint you chose on Fleet, or the tinted silhouette - with its size,
  sorties and hours under it, and a Paint button on each. **To scale** is a
  switch away for the plan view drawn at one scale; the gallery does not
  pretend to be, which is why the scale bar leaves with it.

- **Ships in colour.** The game's silhouettes are white, so on Fleet and Hangar
  they are now tinted in their maker's colour - a hint at whose ship, said to
  be one, not the finish. For the real thing, each Fleet card has a **Paint…**
  button listing the liveries the game pictures for that hull (the Corsair has
  ten, the Cutter twenty-six); pick yours and the card shows the game's own
  render of it. The app never picks - which paint a ship wears is not in the
  logs - and the choice stays in this browser, like the roster tick.

- **Your fleet, drawn to scale.** Flight → Hangar draws every ship your logs
  say you have flown at one scale, from your installed game files: the
  silhouette is the game's own vehicle icon and the size is the bounding box
  the game gives the ship, so a Pisces beside a Corsair is the real difference.
  Sort by length, sorties or last flown; zoom in when the small ones get small.
  A ship the install has no icon for is drawn as a box at its size; one it
  cannot size is named below rather than drawn as a guess. The same silhouette
  sits on each card on the Fleet page.

- **Wikelo's emporium, from your game files.** Jobs → Wikelo lists every trade
  the Banu collector offers - what he builds, what he wants for it, the rep it
  pays and which rank it needs - read from the installed patch rather than a
  guide, so the numbers are this build's (the Polaris is 50 Favors, whatever a
  page from 4.9 says). Each requirement is ticked against your stash sightings
  by the same rule the Jobs page uses: seen somewhere, never a count. **Track
  as a goal** makes a shopping list of everything a trade wants; pin it and it
  is on Now and the MFD like any other list. Retired trades still in the file
  are counted and hidden; your standing with him is not in the logs, so a rank
  gate is stated, not judged.

### 0.10.47

- **Help answers the questions people actually arrive with.** Twenty-six
  answers in five groups instead of eleven in three: the orange wipe banner,
  showing the dashboard on a tablet, which keys do anything, where the data
  lives; the trading rate, the ~ on the Ledger, how the wake-up card infers
  your regen; how to mark a point, why it says "believed to be", how far away
  it is, which screens the reader can and cannot read, what to do with a wrong
  reading; what a shared file carries, what happens to one you are sent, and
  what a backup holds and leaves out. Each answer links to the page it is
  about. A filter box narrows the page to the questions that mention a word
  - in the answer as well as the question - and **Expand all** opens them.

- **Share your points of interest the way you share prices.** Settings → Share
  what you have has a fourth box: your points, with the name, category and note
  you gave each - and which system your logs believed, or you said. A friend
  opening the file sees them on their Points page under **Shared by others**,
  with whose they are, whose word the system is, and the distance from wherever
  they last copied a location by the same rule as their own points. Nothing is
  merged: removing the file takes them away and leaves their own untouched.
  **Keep as mine** copies one across as their own point, with a note saying who
  shared it. Coordinates that are not numbers, dates in the future and notes
  past the cap are dropped or cut on the way in and counted, as with every
  other shared class.

- **Help is where the questions are.** About now opens a dedicated Help & FAQ
  page with setup, privacy and reporting guidance updated for the paired Cougar
  MFD HUD, saved-screen reading and cash estimates.

- **A README that reflects the whole current app.** The opening now explains
  the shared local pipeline behind the dashboard, overlay, paired MFD HUD and
  optional screen readings. The feature list is grouped by the questions each
  surface answers, and a new architecture image sits at the top of the page.

- **A clearer map of the new cockpit and screen tools.** The README and
  technical notes now explain what the paired Cougar MFD HUD reads, how saved
  screenshots and copied locations stay local, and how their small summaries
  reach the dashboard and cockpit displays.

- **Cash on hand where you fly.** The balance the Ledger shows - from the last
  screenshot that printed it, carried forward by the movements logged since -
  now leads the Cougar MFD's Money page and sits on the Now page's trading
  card, which the overlay shows in game. Carried forward it is called an
  estimate and says what it rests on; before any screenshot has shown a
  balance there is no line, not a zero.

- **How far are the things you marked?** Copy a `/showlocation` in the game and
  the reading on the Log now lists the nearest of your points with the
  distance - *Ruin mining shelf · 12.4 km* - and the Points page measures every
  card from wherever you last copied, saying when and where that was. A point
  in another system is named as not measurable rather than given a number:
  the same coordinates mean different places in Stanton and Pyro. Nothing
  decides how near counts as "there" - that differs between a cave mouth and a
  belt, and you can judge a distance better than the app can guess one.

- **A point says what its "believed to be" rests on, and you can set the
  system yourself.** A copied `/showlocation` names no system, so the app
  places it from the logs - and now says on what: *from a location signal 4
  min earlier*, or *from a quantum jump 40 min earlier - where the ship was
  going, not that it arrived*. Each card has a System selector; a copy the logs
  could not place asks for it rather than showing nothing, and a system you set
  is shown as yours from then on, with what the logs had said beside it.

- **Points of interest are in the backup now.** Every pin - its name, category
  and note - goes into the backup file and comes back through the same restore
  review as your jobs and kits: a point you removed on purpose is not handed
  back unasked, a newer note here is kept over an older one in the file, and
  the sentence on the Backup page says how many points a backup would carry.
  Backups taken before 0.10.38 hold no points; take a fresh one.

- **On the Points page, saving one card no longer discards what you were typing
  on another.** Only the saved card is redrawn.

- **Points of interest have a page of their own, with room for why.** Under
  Flight, next to Places: every `/showlocation` you pinned, as a card with its
  name, category, exact coordinates, when it was copied and which session that
  fell in - and a note. Write down why you were there, what is there and what
  to bring next time; nothing in the logs will ever say. Search reads the notes
  as well as the names, categories become filters once there is more than one,
  and a card with something typed and not yet saved shows an amber bar until
  it is. The Log's pinned-points aside shows the note and links to the page.

- **The Ledger now shows your cash on hand.** Game.log never states a balance,
  so the figure comes from the last screenshot that showed one, dated and named
  on the card, and the movements logged since carry it forward. The carried
  figure is labelled an estimate, with the count and net of the lines it rests
  on, because money that moved without a line in the log is not in it - the
  next screenshot says by how much. Until a screenshot has shown a balance the
  card says so rather than showing a zero.

- **A commodity kiosk that prints the balance in full is now the wallet.** The
  first kiosk photographed abbreviated it - `1,583M` - and no figure was ever
  taken from a kiosk. Your own kiosk prints every digit, and that reading is the
  only place any screen has shown the balance in a face the reader can read. A
  figure with a suffix is still kept as printed and never becomes a number. The
  reader also no longer mistakes the close button's `x` beside "Current
  balance" for the balance itself.

- **A reading you do not trust can be invalidated from the Log.** It stays in
  the list, struck through and saying so, but the wallet, the fleet, the
  fittings and the Now card stop using it; it can be believed again with one
  click. Each reading also has **Read again**, which puts the same screenshot
  through the reader as it is now and replaces the old reading - so a screen
  the app learned to read after you photographed it is read at last.

- **The Cougar frames now work as a cockpit pair.** The left frame opens as
  Navigation and the right as Mission. The Mission card keeps the active
  contract, destination, objective progress and a matching Contracts-screen
  reward together; a captured reward is shown only when its selected contract
  matches the live one. Its bezel becomes **Map**, **Done**, **Contract**,
  **List** and **Home**, making the next drill-down visible on the hardware.

- **The Cougar MFD map is now a radar instrument.** Its real body-centre
  geometry has range rings, a sweep and a lock reticle for the selected body.
  The sweep turns about the star and trails behind itself, rather than drifting
  outside the rings as a short bar attached to nothing.
  The five upper bezel buttons become **Nav**, **Previous**, **Next**, **POI**
  and **Here**, so the frame names what it can do on that screen. POI opens the
  named points kept in the MFD Log; positions are never invented from a name.

- **The MFD menu scrolls instead of cutting its labels off.** On a small frame,
  or with the text turned up, five tiles did not fit and each was squeezed into
  a box shorter than the word standing in it - so "Operations" was clipped and
  looked like a shorter word rather than a hidden one. The tiles keep their
  height and the menu scrolls to the ones below.

- **The HUD now puts one useful thing in front of you.** Its Focus strip opens
  the next stop, active contract, current location, or a screen that needs
  attention. The Log keeps repeated clipboard-watch readings as one location
  with a seen count, while a deliberate read remains a new entry.

- **Points and unfamiliar screens are easier to work with.** A saved location
  can have your own name and category, including on the MFD. Screenshots the
  app cannot yet interpret now collect in a review queue with the capture and
  extracted text together, ready to turn into the next reader.

- **A Log tab keeps what you showed the app in one place.** Screenshots and
  copied `/showlocation` readings now have a direct dashboard tab instead of
  living under Overlay settings, and the Cougar MFD has the same compact Log
  screen under Pilot. A copied location can be pinned as a point of interest;
  pins keep the exact coordinates even when routine readings are cleared.

- **The Log is clearer at a glance.** Reading controls, recent activity and
  pinned points are arranged as visual cards, with a small action on each
  copied coordinate rather than a separate configuration flow.

- **The ledger pages, instead of stopping after the first few.** It showed the
  newest handful, told you there were more and gave you no way to reach them.
  **UP** and **DOWN** now move a page at a time and the title says where you
  are — *Ledger · 4-6 of 95* — and it reads back thirty days rather than three,
  because paging through three days is not worth a button. Item names lose
  their underscores, so an entry wraps instead of taking a whole line as one
  unbreakable word.


- **The system map puts where you are at the top.** Location and next stop were
  under the plot, which on a small opening meant scrolling a map to find out
  where you were — the one thing you opened it for. They are above it now, and
  the plot is sized to leave room for them rather than to fill the frame.

- **The map caption says what it means.** It read "straight line, not a fix",
  and a fix is the word for a position — so it looked like a warning about
  where it had put you, which is not what was in doubt. It now says **direct
  line, not the route flown**: the distance is body centre to body centre, and
  the game never writes down the route it actually plots.


- **Brightness is four levels now, and a button can reach every one of them.**
  It used to nudge five percent at a time, which is fine for a mouse and no use
  at all on a frame — you press until it looks right, and next flight you do it
  again. Bind **Screen brightness · 1 to 4** and one button cycles 40, 60, 80,
  100 and round again, showing the level it is on. The one-level brighter and
  dimmer commands are still there for the rocker printed BRT. If you had set
  something in between, it moves to the nearest level once.


- **Setting up the buttons now shows the frame, not a form.** Binding a key
  meant reading past twenty-eight dropdowns of thirty-six options each, none
  of which said which key on the desk it meant. Setup draws the Cougar
  instead: the twenty face buttons in their real positions around the screen,
  the rockers in a row underneath, each carrying its number and what it does
  today. **Press a button on the frame and its position lights up and is
  selected**, so binding one is press, pick, done — and the picking is a
  single grouped list rather than a thousand entries.

- **Frames can dim themselves when you leave them alone.** Off unless you
  pick a time in **Screen** — five minutes to an hour. After that the frames
  drop to a third of your brightness, and the next press of anything brings
  them straight back before it does whatever it was bound to do. They never
  blank: a panel you can still glance at beats one that has to be woken
  first.

- **Each frame opens where it is useful again.** Both were starting on the menu,
  so a two-frame cockpit showed the same list twice. A frame you have not used
  yet opens on Navigation on the left and your flight plan on the right; once
  you move it, it remembers where you left it.

- **Two presses to any screen, one to Navigation.** The menu had a middle
  level — Flight → Route → Navigation — and every one of those middle entries
  split just two screens, so it cost a press on the way to everything and
  sorted nothing. Categories now hold their screens directly, which fits the
  five top buttons with room to spare, and **Navigation** sits on Home beside
  the four categories rather than behind one of them.


- **A real drill-down menu for Cougar MFDs.** Home opens Flight, Operations,
  Resources or Pilot; each category opens its own submenu before the page. For
  example: **Flight → Route → Navigation / System map**. Top buttons follow the
  visible level, **Back** follows the whole path up, and **Home** returns to the
  cockpit root. The menu is data-driven, so later features can add another level
  without adding a navigation rule.

- **Less prose on the frame.** State, source and caveat rows now use labelled
  icons with short operational text. Loading, disconnected plans, empty logs,
  no crew, counter state and similar conditions no longer fill the display with
  dashboard instructions. The detailed explanation remains in the dashboard.

- **The readings read like an instrument.** Every line is a glyph, a short label
  and the value, all on one line, at a smaller size — instead of a label stacked
  over a value that wrapped constantly. All four Nav readings now fit a 480 px
  panel without scrolling; they used to run off the bottom.

- **The disclaimers are gone from the frame.** A qualifier nobody reads is not
  doing its job, so the claim moved into the label that *is* read — the rate is
  **TRADING RATE**, a list says **2 of 5 seen** rather than "held", a claim is
  **CLAIM, PER THE TABLES**. The two that prevent a genuinely dangerous
  misreading stay: Cargo still says *Never the hold*, Crew still says *Absence
  means nothing*. The full explanations live in the docs and on the settings
  page, where there is room to read them.

- **Here lists what a place actually has**, rather than naming every service
  with its status — five facts to say "none of them", wrapping to four lines.
  It reads *Refuel · Repair*, or *None of 5 listed here*.

- **A MAP page**, on the button between **UP** and **DOWN**: the system plan at
  full size, with where you are and where you are going underneath it.

- **One strip of chrome instead of two.** The frame said which MFD it was at the
  bottom, next to a readout of the button you had just pressed with your own
  thumb — two things costing a whole reading on a 480 px panel. Which frame it
  is and which Cougar drives it are at the top with the rest of the identity now,
  and the footer is gone.

- **Shorter words on the frame.** The qualifiers that keep each page honest were
  turning into paragraphs. They say the same thing in a phrase — *"Counter
  receipts and your plan. Never the hold."*, *"Held: seen in a stash listing, not
  counted."* — and the lists are cut to what you read at a glance rather than
  what a report would show.

- **Four more pages, from data the panel was already downloading.** **Ship**
  says what you are flying, what it is for, and what the game’s own tables say
  losing it costs. **Here** is the moment after landing: what this place can do
  for you, what on your shopping list it stocks, and what you left here last
  time. **Ledger** is what the logs actually priced, confirmed only. **Mine**
  is where the deposit tables rank a rock highest.

  Six of the nine things the panel fetches every five seconds were being thrown
  away, so three of those four pages cost no extra request at all.

- **Done no longer acts where it looks dead.** It is dimmed on every page but
  Act, and dimming was all that happened: two presses elsewhere still marked a
  task off your flight plan, with the confirmation drawn on a page you were not
  looking at. Dimming a button and refusing its press are one rule now.

- **A confirmation stays attached to the task it was armed against.** Your plan
  is re-read every few seconds, so the line under the cursor when you armed
  **DONE** could be a different job by the time you pressed again — and the
  second press took whatever was there. It now stands down and says the plan
  moved. A second press while the first is still saving is ignored rather than
  sent twice, which would have put the line back where it started.

- **Nav leads with where you are going.** The destination and its distance were
  third, under the map, off the bottom of the panel. They are the headline now,
  with your location beneath and the map shrunk to a supporting picture.

- **The confirmation on Act has a fixed strip of its own.** It used to be the
  last row of the very list it was asking about, and scrolled out of sight. It
  reads *Confirm: load · 32 SCU · Titanium?*, then says what was marked.

- **A small opening gets a layout of its own.** Below 320 px the captions switch
  to short forms rather than clipping, the map and the corner marks step aside,
  and the spacing tightens.

- **The MFD section on the Overlay page is documentation now.** It was shaped
  like settings and could change nothing: everything that configures MFD mode is
  in the tray window, because that is the only place that can see your monitors
  or read a frame. It now says how to get there, in order.

- **Optional Cougar MFD displays.** Open **MFD setup…** from the tray to place
  independent left and right displays on a shared monitor or separate monitors.
  Drag and resize the areas, preview alignment behind the frames, and save the
  placement. Six pages: where you are, the next planned task, the outstanding
  work at that stop, what you planned to load against what the commodity
  counters recorded, the contract you accepted this session, and where all of
  it came from. MFD mode starts off; the regular overlay keeps its own
  settings.

- **Quantum Wake's own mark, small, in the corner of the frame.** Bottom left,
  beside the device line — the ship maker's mark already has the top corner.

- **A calmer frame.** Brightness and text size are sliders in **MFD setup** now
  rather than four buttons on the face, and previous/next page and Home ship
  unassigned — with a button for every page, cycling was a second way to do the
  same thing and a bottom row of five navigation keys was the most confusing
  part of it. Thirteen buttons instead of twenty, and the three that are left —
  up, down and **DONE** — dim on a page where they have nothing to act on. Every
  one of those commands is still there to bind if you want it.

- **The Now page, broken across the frame.** Ten pages now, each with its own
  button: Nav, Task, Act, Cargo, Contract, Status, and four new ones — **Feed**
  (what just happened, straight off the live log), **Crew** (who the party
  channel has named, and why that is a floor rather than a roster), **Money**
  (your trading rate and how long a goal will take at it) and **List** (shopping
  lists and how much of each you are holding). Session time, deaths, your handle
  and where you would wake up folded into Status; where a price is better folded
  into Cargo. Nothing on the frame is unassigned any more.

- **A system plan on the Nav page, with the whole route on it.** Small, above
  the words: the star, the bodies at their real coordinates, the one you are at
  ringed, and a dashed leg to every planned stop in the order you will fly them
  — with the distance beside it. It marks a body and says so; the logs name the
  place you are at, never where you are on it, and the distance is a straight
  line between body centres rather than a quantum route the game never writes
  down.

- **The maker's mark of the ship you are flying**, in the top-left corner of
  every page. Nothing shows for a maker with no logo, or before a ship has been
  identified.

- **Icons above the button captions**, and a blank where a button does nothing.
  The number used to sit there, labelling a button the pilot is looking straight
  at; a blank position now reads as blank, the way a real MFD's does.

- **The alignment preview follows the editor as you drag.** It updated only
  when you let go of a rectangle, and — worse — saving switched it off, so after
  one save nothing you changed reached the frames until you saved again. The
  preview now moves under your hand and keeps running after a save; **Stop
  preview** or closing setup puts the saved placement back.

- **MFD mode looks like the rest of Quantum Wake.** The placement editor had
  grown a palette and typeface of its own; it now uses the dashboard's own
  stylesheet, so it cannot drift again. The instrument moved off its phosphor
  green onto the same cyan HUD palette as everything else. And the placement
  editor opened in a browser draws one invented monitor that used to look
  exactly like a detected one — it now says it is an example, and that your own
  monitors are only found when you open setup from the tray.

- **The monitor behind the frames goes black.** A Cougar frame covers part of a
  monitor, and the rest of it keeps glowing around the bezel — wallpaper, the
  taskbar, whatever was there — which in a dark cockpit washes out the
  instrument inside the opening. Quantum Wake now fills those monitors with
  black around the openings. Turn it off with **Black out the rest of those
  monitors** if a panel is sitting in the corner of a screen you are still
  using. The monitor you have setup open on is left alone until you close it.

- **Tick work off from the frame.** The Act page lists what is still to be done
  at your next stop and marks one done with the **DONE** button — pressed once
  to arm it and again to confirm, so a glove on the wrong button costs nothing.
  It writes to your own flight plan and tells the game nothing.

- **Every button can be reassigned, rockers included.** Setup now carries the
  full button map, shared by both frames. The four rocker switches report as
  buttons 21–28 and ship unassigned, because which rocker is which number is
  not something a datasheet answers: press one, watch the tester name it, and
  bind it. **Restore defaults** puts the shipped profile back.

- **Cargo, honestly.** The game logs no cargo hold, so the Cargo page shows what
  your plan says to load and what the commodity counters actually recorded this
  session, each labelled as what it is. It never claims to know what is aboard.

- **Screenshots of the Contracts app are checked properly now.** Reading a
  mobiGlas Contracts screenshot compared it against a list that was always
  empty, so a photograph of five accepted contracts reported "the tab says 5,
  the logs say 0" and marked every one of them as unseen. It now compares
  against the contracts the logs actually carry.

- **Readings now reach the overlay, and tell you when something is off.**
  The last screenshot read appears on the Now card in the dashboard and in
  the in-game widget, without your having to open the settings first. Before
  this it only updated after a visit to the Overlay page, so the widget never
  showed a reading at all.

  **A toast when the screen and your logs disagree**, and only then. Taking
  six shots of a loadout should not put six notifications on your screen, so
  a reading that agrees updates the card quietly and says nothing.

- **A log of everything you have shown it**, on the Overlay page. Every
  screenshot read and every location pasted, newest first, with what your
  logs said at that moment beside it. A pasted location is kept with the
  place your logs had you at the time, because the reading itself names
  nowhere and three numbers are unreadable a week later. It stays on this
  machine, and there is a button to clear it.

- **Off means off.** Switching the screen panel off now clears the reading
  from the Now card and the widget as well, rather than leaving the last one
  you took sitting there.

- **Commodity kiosks read.** Point a screenshot at a shop terminal and the
  buy side comes back as a list: what it stocks, how much of it, and what it
  is asking. Your logs record what you paid and never what was asked, so
  every one of those prices is new.

  **The unit stays welded to the price.** One kiosk priced Hephaestanite per
  unit and Corundum per SCU on the same screen. Those are not the same
  quantity, nothing here converts one into the other, and each price is
  shown with the word the kiosk printed beside it.

  **What it will not do.** A kiosk abbreviates your balance — ¤1,583M — and
  that figure is shown as printed and never taken as a number, because a
  rounded balance used as a starting point would put every later reading out
  by whatever the rounding hid. A price that did not read is missing rather
  than borrowed from the row above. The sell side is kept whole but not read.

- **The inventory screen carries no names.** Photographed with a location’s
  stash open: the items are icons, and the only text on a tile is a stack
  count. So a stash can be confirmed one hovered item at a time, which
  already works, and not a screen at a time.

- **Four more screens read: the Contracts app, the Rep app, the Fleet
  Manager and its loadout estimate.** The Contracts tab’s own count and
  every card on it are checked against the contracts your logs have open,
  by number and by name. The Fleet Manager says where each of your ships is
  stored, which nothing in the logs has ever said, and a ship it lists that
  you have never flown is filed as new rather than as a mistake. The Rep app
  gives an organisation and your standing with it; the rank is drawn as a
  highlight and does not read, and the reading says so.

  **The wallet read.** On three of the twenty-two frames from the second
  night the balance came back, and two readings a minute apart reconciled
  against the ledger. On the other nineteen it did not, from the same bar,
  so a reading is a gift rather than a promise — and a screenshot taken
  while the game is logging is checked against where you actually were.

- **Every screenshot you take can be read as it lands, and checked against
  your logs.** Tick **Read each screenshot as it lands** on the Overlay page
  and each new one is sorted into the screen it is — an item’s tooltip, the
  Vehicle Loadout Manager, the mobiGlas map — and what it says is set beside
  what the app had worked out from your logs at that moment. Where the two
  disagree, it says so. Nothing already in the folder is read, and the setting
  names the folder it follows.

  **What your ship is actually carrying.** The logs record nothing about what
  is bolted to a ship, so until now the Fleet page showed the factory fit,
  which is the same for everyone. A loadout screenshot names the parts in each
  port — 39 of 47 ports across the six frames in this install — and the Fleet
  page shows them, dated, with the parts the factory did not fit marked as
  such. On this install that is Genoa power plants, a Parapet shield and Chaos
  missiles the Corsair did not ship with.

  **Where you were, from the map.** The map footer names the system and the
  place, and the reading is checked against where the logs had put you at the
  second the shot was taken. The same screen says whether you had any accepted
  contracts, and that is checked too.

  **What it will not claim.** A part two catalogue entries fit equally is shown
  as both, never guessed; a ship whose name half-read is offered as a
  resemblance and nothing is built on it; a moment no session covers is
  marked as unchecked rather than passed. The wallet balance is still printed
  in a face the reader cannot see, and the reading says so in those words.
  A screen this app has no reader for yet is kept with its text.

- **The overlay can read what you copy, and the screenshots you take.** New on
  the Overlay page, off until you switch it on, and in two steps rather than
  one.

  **Copy only** reads your clipboard. Type `/showlocation` in the game, press
  **Parse what I copied**, and it turns the coordinates into a distance you can
  use — the reading in this install works out at 15.0000 Gm from the system
  centre. It says which system it is in comes from your logs and not from the
  reading, because it does.

  **Copy and screenshots** adds **Read my last screenshot**, which says what an
  item is: name, manufacturer, type and volume, matched against the 26,028
  items in your game files. On the looting screen it names the weapon you are
  standing over with four separate facts agreeing.

  Nothing watches your screen. A screenshot is a file you chose to save, and it
  is only read when you press the button. Findings stay up for thirty seconds
  and then clear, so the panel is never showing something that has stopped
  being true.

  A dashboard opened in a browser against the bare server says so rather than
  offering buttons that cannot work — reading the clipboard and reading files
  are things the desktop app does.

### 0.9.58

- **A contract you lost is no longer filed as one you dropped.** Both endings
  were counted and labelled as abandonment, so a run where the timer beat you
  or the cargo was destroyed sat in your history as a decision you made. The
  game has always kept the two apart, and now so does this: 231 completed, 65
  abandoned and 5 failed across this install.

  The Contracts page shows failures as their own figure when there are any, and
  says nothing when there are none — a permanent zero would read as "nothing
  ever went wrong" on an install too new to know.

  **Your history is re-read on upgrade**, so past failures stop being counted
  as things you walked away from.

### 0.9.57

- **Contracts no longer finish before you do.** A contract was counted as
  complete the moment any one of its objectives finished — so a hauling run
  was done as soon as the cargo was loaded, with the delivery still ahead of
  you. Contracts with a single objective were right, which is why this looked
  like it happened for no reason.

  In this install 96 of 212 contracts complete more than one objective, and the
  last one finishes a median of nine minutes after the first. The worst was
  marked done three and a half hours early.

  The game says plainly when a contract is over, in two separate lines, and
  neither was being read. Both are now, and they agree on every one of the 231
  completions here. Objectives count steps, which is all they ever said.

  **Your history is re-read on upgrade**, so past contracts get their real
  completion times rather than keeping the early ones.

- Walking away from a contract is now noticed properly too. Sixty-four
  abandoned contracts in this install left no trace in the objective log at
  all, because abandonment is announced on the line that was not being read.

### 0.9.56

The first release since 0.8.35, and everything below is new since it. If you
are updating from there, this is a big one: contracts finally say what they
paid, the Ledger can be taken apart by kind, everything you have typed can be
backed up and restored, and a run can be reviewed against what your logs say
actually happened.

- **Labelling your items no longer wipes the game's text.** The file this
  writes is the one the game reads all of its English out of at startup, and it
  has to be UTF-8 with a byte order mark, exactly as the game's own is. Ours
  only carried the mark when the table underneath came straight out of
  `Data.p4k`. Every other case — StarStrings installed, or simply applying the
  marks a second time over our own backup — went through a reader that eats the
  mark, and wrote a file three bytes short of what the game writes. The result
  was labels falling back to the engine ids beneath them right across the game,
  the size and grade marks this feature exists to add among them. The mark is
  now written whichever table was used as the base, and both bases produce the
  same bytes.

- Contracts now show what they paid. Star Citizen states a payout exactly once,
  in a HUD toast — `Awarded 50250 aUEC` — that names no contract and carries an
  always-empty mission id, so Quantum Wake pairs it with the completion toast in
  front of it and books the result in the Ledger as a `Contract paid` row. This
  is the first money in the Ledger that did not come from selling something.

  It is a floor over hauling, not your contract income. Across the 179 backups
  in this install the game stated a payout 14 times against 230 completed
  contracts, and every one of the 14 followed a cargo run — the 90 Combat
  Gauntlet completions pay nothing the log admits to. Missing rows mean the game
  said nothing, not that a contract paid nothing.

- Completions and payouts now raise a toast while you play, on the dashboard and
  in the overlay, so a contract finishing is something you see rather than
  something you find afterwards. Nothing else toasts: arrivals, jumps and
  medical beds stay in the feed, because a notifier that fires on everything is
  one you stop reading. Click a toast to dismiss it, or leave it to fade.

- Sessions are re-read on upgrade, so past payouts appear in the Ledger without
  reflying anything.

- The Contracts page can fold its summary away. Four tiles, the standing table
  and both charts sit above the contract list, so the newest contract — usually
  the reason for opening the page — started below the fold. **Hide summary** in
  the page header collapses all of it and the list rises to the top; the choice
  is remembered per browser, so it stays folded until you unfold it.

- Four faults found in review, all of which were green under the tests and
  wrong in front of you.

  **A locked file no longer destroys the way back.** Removing item labels while
  something held the game's text file open — the game itself, an editor, a
  virus scanner mid-pass — reported success, changed nothing, and threw away the
  record of what to put back. The labels then could not be undone through the
  app at all. An unreadable file is now told apart from a replaced one: the
  record is kept and the removal asks you to try again.

  **Installing StarStrings over item labels no longer marks your game for
  ever.** StarStrings backs up whatever file it finds, so installing it while
  the marks were down recorded the *marked* file as your original — and
  removing both afterwards put the marks back permanently, with both pages
  reporting nothing installed. The marks are now lifted before either mod is
  installed or removed, and laid back on top afterwards.

  **Mining places are ranked on the rock you will actually break.** A place
  where nine rocks in ten are poor and the tenth is rich was scored as though
  every rock were the rich one — five times its real worth in the worked case,
  and the same inflation reached the mining suggestion on Now. Each deposit now
  counts for how often it occurs.

  **The commodity card sends you where the commodity is bought.** It listed the
  terminals that sell it to you under “buys it from you”, and had the two counts
  the wrong way round — so anyone with a hold full of cargo was pointed at the
  shops that stock it rather than the ones that pay for it.

- **You can take a complete backup of everything you have typed.** Settings →
  Export already offered a file to share with someone else, and it carried
  three of the ten things you author: jobs, checklists and flight plans. Your
  mining log, your goal, your map notes, your item-label settings and your wipe
  line were not in it. The new backup carries all of them.

  It does not carry sessions, trades, payouts or contracts, and it is not meant
  to — those come back by reading your logs again, and a copy in the file would
  be a second version that can disagree with the game. What it protects is the
  part rescanning cannot bring back: the things you typed.

  The file also records what you have deleted. That sounds odd in a backup and
  is the point of one: restoring a month-old file should not quietly hand back
  the eight plans you threw away last week, and without this it cannot tell
  them from plans you never had.

- **A backup can be put back, and it shows you what it will do first.**
  Restoring works out the whole change — what comes back, what gets
  overwritten, what it will not touch — and writes nothing until you approve
  it. Approving the plan as it stands is one click; every line can be turned
  around individually.

  Where a plan and this machine disagree, the newer work wins by default, and
  that means yours: a record you edited since the backup is kept unless you
  say otherwise. Anything you deleted stays deleted unless you ask for it
  back. Restoring never quietly overwrites something you did more recently
  than the file.

  If the file changes between the preview and the restore, the restore is
  refused rather than applied — a preview is only a promise if what it
  described is what runs.

- **Backup and restore have a page.** Settings now has a Back up what you
  typed block: one button saves the file, another reads one back. Restoring
  shows the whole change first — what comes back, what gets overwritten, what
  it is leaving alone — with a tick against every line, and writes nothing
  until you press Restore.

  Every row says why it is there in your terms rather than the code's: *not on
  this machine*, *yours is older*, *yours is newer*, *you deleted this*.
  Records that already match are counted but not listed, because they are not
  decisions. Pinned jobs and the tracked flight plan are left alone either
  way, and the summary says how many.

- **A flight plan can be a run: started, finished, and timed.** Press Start
  when you set off and Finish when you are done. Elapsed time is measured from
  Start, never from when you wrote the plan — a route drafted last week and
  flown tonight is a two-hour run, not a week-long one.

  Runs you finish move to a **Finished and filed** list, with Repeat to fly the
  same route again with nothing ticked off.

  A run you start and never come back to files itself after ten days, so the
  working list stays honest. That is reversible: those runs offer **Resume**,
  which puts them back and keeps the time they started, and the ten days is a
  setting you can change or switch off. Runs you finished yourself do not offer
  Resume — the app filing something and you finishing it are not the same
  decision, and the list should not pretend they are.

  Plans you have never started are left alone entirely: a backlog of routes is
  not a pile of abandoned flights.

- Six faults in backup and restore, found in review. Five were the app saying
  one thing and doing another.

  **A restore that fails now says so.** A write that refused partway used to
  report success, or report that nothing had happened when half of it had.
  Every store is now photographed first and put back if any write refuses, and
  the page says which of those two things occurred.

  **Pinned jobs stay pinned and the tracked plan stays tracked.** The preview
  promised to leave both alone; replacing a record was quietly clearing them.

  **A restore takes on the deletions the file remembers**, so setting up a new
  machine no longer walks back everything you had thrown away — unless the
  record is present here, in which case it is left exactly as it is.

  **A restored wipe line reaches the whole app.** It was moving the setting
  while every page carried on counting against the old cutoff.

  **The preview reads correctly.** Records that already matched were listed as
  choices and their reason column showed a raw internal name.

- **A finished run can be compared with what your logs recorded while it ran.**
  Money that moved between the moment you pressed Start and the moment you
  pressed Finish is matched to the stop you were at when it happened, and each
  stop shows what it planned beside what it took.

  Nothing is moved onto a stop to make a total come out. A sale somewhere the
  run never went, or at a stop you never ticked off, is listed on its own
  rather than added in or hidden — when a figure looks wrong, that list is
  usually the reason. Each figure also says what it left out and why, including
  how much of it is what a terminal was asked for rather than what it confirmed.

  The match rests on an inference and says so: a receipt names a kiosk and
  never a place, so the place comes from the last arrival before it.

  You can also record what a stop actually came to, beside what you planned.
  The estimate is kept — the two together are the point.

  **Review** on a finished run opens that comparison. Money in and money out
  lead; under each is a **Why this number?** that shows the rule it used, the
  records behind it, and what it left out. It starts folded — a figure nobody
  is questioning does not need three lines of provenance under it.

  Money the run moved that no stop can account for is listed at the bottom and
  is deliberately not coloured like the money that counted, because it is not
  in the totals above it. Beside each planned action you can record what it
  actually came to; the estimate stays on screen next to it.

- **Saved kits.** Keep a loadout you like, and ask what it would take to put it
  back together. Anything you are wearing is settled. Anything nothing has ever
  seen goes straight on the shopping list.

  Everything else is a question, and it has to be. The game logs an item being
  *seen* in a container and never one being taken out or used up, so anything
  you have ever stored looks present for ever — a kit that treated that as a
  stock level would report a full loadout to somebody standing in an empty
  hangar. So stored items are shown as sightings with where and when, and only
  you can say whether they are still there. Sightings older than a month are
  marked, which changes how loudly the page doubts them and never whether it
  asks.

  A sighting you say nothing about is left as held: the cost of that being
  wrong is a wasted trip, and the cost the other way is buying something you
  already own. Once you have answered, the replacements become a shopping list
  you can attach to a flight plan like any other.

  Kits are in the backup from the day they exist.

  Kits live at the bottom of the Character loadout page. **Save what I am
  wearing** makes one out of your current loadout; **Prepare** compares it with
  what you have and asks about the rest. Only the uncertain lines get a button:
  asking about something the game actually reported would invite an answer the
  app should not take.

- **A mining haul can be followed from the rock to the money.** A run used to
  be one row; it can now carry the refinery you sent it to, what the job cost,
  when you expect it back, what actually came back, and what it sold for.

  What went in stays beside what came back, because the difference is the
  number worth knowing. Before you collect, the loss is *unknown* rather than
  zero — those are different facts and only one of them is a number.

  A job only says it is ready once the time **you** entered has passed, and one
  with no expected time never claims to be ready at all: the game keeps that
  timer and logs nothing about it, so the app has nothing else to go on.

  All of it is typed, none of it is observed, and it stays on its own side of
  that line — mining revenue never joins the Ledger, which is what your logs
  actually recorded.

  The Mining page carries it: a **Waiting on a refinery** list at the top,
  soonest first, and a stage column on every haul with a button for whatever
  comes next. What went in and what came back are separate columns, so the
  difference is on screen rather than worked out in your head.

- **Figures can be asked where they came from.** Money in, money out, net,
  contracts paid and deaths each carry the rule that made them, the records
  behind them, and what was left out.

  What was left out is the part worth having. A net that looks wrong is almost
  never an arithmetic problem — it is mining and salvage the game never
  recorded. Contracts paid says outright that most completions were never
  priced, so a missing payout means the log said nothing rather than that a
  contract paid nothing.

  Deaths has no records at all, and says so instead of showing an empty list:
  the game stopped writing a death event, so the count is a pattern the app
  recognises rather than something it read. A figure this cannot explain says
  it does not know, rather than answering with a blank.

  The Ledger has it first: a small **?** on Money in, Money out and Net opens
  the answer under the row. Nothing is fetched until you ask, and the count of
  movements has no **?** at all — a control on every figure would promise an
  explanation for numbers nobody has written one for.

- **One search box, in the header, across everything.** Items, places, ships,
  your jobs, checklists, runs, kits and map notes.

  Results are grouped by where they came from — your logs, things you wrote,
  the catalogue — rather than mixed into one ranked list, because those are
  three very different kinds of answer and only the first is about you. Every
  hit says why it matched, so a list of ten results is not ten clicks.

  Something you have never seen still comes back, from the catalogue, with
  nothing under your own logs. Never having seen a thing is a fact worth
  showing, and a search that reports nothing for a real item reads as broken
  rather than as an honest answer.

- Seven faults found in review, three of them serious.

  **Older backups can be restored again.** A backup taken before saved kits
  existed has no kits in it, and restoring one failed outright — the files
  most worth keeping were the ones that could not be read.

  **A failed restore now really does undo itself.** It put back what it had
  overwritten but left behind anything it had added, while reporting that
  everything had been returned.

  **Run review counts sales made at a stop.** Landing is what ticks a stop
  off, so everything you do there happens afterwards — and the review was
  looking at the journey before it instead. A run where you arrived at 13:00
  and sold at 13:15 reported nothing earned.

  **A kit asks for the number of things you wrote down.** One medpen no
  longer satisfies a kit that wants four; the shopping list asks for the
  three you are short. It also says so in those words —
  “1 on you · 3 still needed” — rather than reporting an item it has
  half of as one it has never seen.

  **Repeating a run no longer carries the last run's figures.** The copy kept
  what you had recorded as the previous run's outcome.

  **Searching the catalogue finds things by name.** It was matching engine
  ids, so "P4-AR" found nothing, and it never looked at your install at all.

  **Hauls you already sold read as sold.** Anything recorded before refining
  stages existed, or entered with a price straight away, was offering to be
  sent to a refinery.

- Six more from review, and one of them changes what a kit tells you.

  **Your character loadout is a sighting too.** It is the last thing seen in
  each slot rather than what you are wearing now, so a helmet nothing has
  replaced since March read as settled and was quietly dropped from the
  shopping list. An old one is now asked about like anything else, and the
  page says why.

  **Search results from your own logs open properly.** Clicking one showed a
  blank drawer that closed itself, while the identical row under the
  catalogue worked.

  **A place opened from search shows its own details.** It was drawing the new
  place over the last one's services, lore and notes — and a note typed there
  was filed against the wrong place.

  **A shared file is recognised as one.** Opening someone else's export in the
  restore picker told you to update an app that was already up to date.

  **A new stop cannot land on a finished run**, where it would have been
  invisible and uneditable.

  **A failed restore that cannot undo itself says so** for mining hauls too,
  rather than only for the writes it makes on the way in.

- The rest of that review, eight more.

  **You can set a goal without having traded.** The goal lives inside the
  earnings card, and the card was hidden whenever there was no rate to show —
  so a new pilot could not set one, and an existing goal became invisible
  with no way to clear it.

  **A run planned from a trade route reviews properly.** Stops written from a
  route carry a terminal name while your logs carry the game place, and the
  review demanded they match exactly — stricter than the arrival that ticked
  the stop off, so the whole run reported nothing earned.

  **Large stations keep their description.** Where the game files describe a
  place twice, the two halves are merged rather than one replacing the other,
  which was dropping the star map paragraph for exactly the places that have
  amenity lists.

  **Installing StarStrings puts your item labels back if it fails.** A corrupt
  download left them off, with the record already gone.

  **A place id the app does not recognise no longer opens a card** titled from
  whatever it was handed.

  Also: item descriptions split their lines properly again; a live-feed
  reconnect no longer replays an hour of toasts at once; a locked file during
  a label install can no longer leave a record that outlives the file it
  describes; CI accepts a bump to either game-data cache rather than
  steering you to move the wrong one, and no longer reads a bump it was given
  as no bump at all once a change grows past a certain size.

- **The Ledger can be filtered by kind.** Cargo, purchases and contract
  payouts each get a toggle, built from what is actually in the list rather
  than a fixed set — so switching everything else off shows what your
  contracts paid, on its own, which was not previously possible.

  The filters reset when you choose a different time range. Left standing,
  a filter could hide the only kind present in the new range — a fetched
  transaction with nothing on screen to bring it back.

  The totals follow the filter, because that is the point of it. The
  **?** beside them does not: it explains the whole figure, and offering it
  over a filtered one would answer with records that do not add up to what is
  on screen. It comes back when everything is shown again, and the line under
  the toggles says so.

- **Checking for updates leaves the answer on screen.** Pressing **Check for
  updates** wrote what it found and then, a moment later, overwrote it with
  “last checked …” — or with “never checked”, on a copy that had never
  checked before, which read as though the check had not happened. The refresh
  of the Settings block now finishes first, so the answer is what stays.

- **The Contracts page says what the game said your contracts paid**, with the
  same **?** behind it. The figure and its explanation come from one call, so
  they cannot disagree.

  It is a floor and it says so: across this install the game priced 14
  completions out of 231, all of them hauling. If it has never priced one of
  yours the page says that outright rather than showing a zero, which would
  read as having earned nothing.

### 0.8.35

- **Re-reading every log now shows what it is doing.** It is the slowest thing
  the app does — a full re-parse of every backup, 160 files and about eleven
  seconds on this install — and it showed nothing but the word *rescanning*
  beside the button. It counts the files off there now. The bar at the top of
  the page follows it too: that bar used to stop watching the moment the
  startup scan finished, so the one scan long enough to be worth watching was
  the one it never drew.
- **The Settings block is called “Game logs”**, not “Log cache”, because that is
  what people look under when they want their logs read again.
- **A re-read that worked is no longer reported as failed.** If refreshing the
  views stumbled afterwards, the line beside the button said the rescan failed.
  The rescan had not failed.
- **The first-run wizard counts what it is actually reading.** It said *events*
  while counting log files, which on a full history read “148 events” for 400 MB
  of flying. It says files now, and the two bars agree with each other.

- **The item-label list is the whole list, and you can search it.** *Label your
  items* showed 25 renames out of the 4,388 it would actually make on this
  install — fine as an illustration, useless as an answer to "what would happen
  to mine?", which is the question anybody deciding whether to let it write into
  their game folder is asking. Every rewritten name is listed now, searchable by
  the item, by its kind, or by the mark itself, so "what does the star actually
  get put on?" takes one search rather than a scroll. The count always says how
  many matched out of how many there are.


- **One panel for everything, wherever you click it.** A place on the map, a
  component in the parts table — they now open the same drawer, and it answers
  the same questions each time: what is known and who says so, whether the thing
  is already yours, what it costs and how old that price is, where you would go
  for it, and what you can do about it now. Every line carries its source, so
  "58 visits recorded" from your own logs never sits unlabelled beside a price
  somebody else reported last week. Adding a place to a plan or a component to a
  shopping list is a button on the panel rather than a different page.

  Every component opens one now. It used to be only the ones the game happened
  to write a paragraph about; the rest had nothing to click, even though the app
  knew their size, grade, price and whether they were already in your kit.

  What it will not say is that something is not yours. An inventory line is a
  sighting, not a ledger — a pledged item sitting in a hangar nobody has opened
  is owned and has simply never been listed — so a component you have no record
  of reads as "not seen in your kit", with the reason attached.

- **"Pin to overlay" actually pins now.** The button on the Now briefing has
  never worked: the request was sent in a form the endpoint does not read, and a
  refused request is not an error in a browser, so it reported success every
  time and did nothing. It now asks the way the endpoint reads it, and says so
  when the answer is no — which it is whenever the dashboard is running without
  the overlay.


- **The Now page leads with whatever your ship is for.** Take a hauler out and
  the briefing opens with trade leads; take a mining ship and it opens with the
  best rocks it can see, ranked by what a rock there is actually worth; take a
  fighter and it opens with what a claim on that hull costs and how long the
  wait is. Nothing in the logs records mining, salvage or cargo, so the ship you
  retrieved is the only thing that can say what you came out to do — the card
  names the ship it read that from, and the chooser beside it overrules the
  guess or switches the whole idea off. It keeps up when you swap ships without
  going anywhere, which is how ships are usually swapped. Your own arrangement
  of the Now cards is left alone: only the sections inside the briefing move.

  A salvage ship gets the plain card rather than a lane of its own. The game
  files every salvage hull as Industrial alongside the mining ones, so guessing
  from that would send a Vulture to the best ore deposits in the system — and
  there is nothing salvage-specific worth showing yet. A wrong lane is worse
  than none.

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

- **The placeholder fix reaches installs that had already read their game files.**
  What the game files say is cached under a stamp that only moves when Star
  Citizen itself patches, so the previous build's rule cleared 8,149 unwritten
  names for new installs and for nobody else. The stamp moves now, and CI fails a
  change to the readers that forgets to move it - the same guard the session
  cache has had since that exact mistake shipped twice.

- **The map fills itself in when the read finishes too.** Its places come from
  the game's own gazetteer and the amenities filter is built entirely from it, so
  a cold start left it thin until the browser was reloaded. It was missed when
  Parts, Mining and Crafting learned to do this.

- **A shopping line naming cargo can be crossed off.** Gear is bought at a kiosk
  and cargo at a commodity terminal, and only the kiosk records were being read -
  so a line naming an ore stayed uncrossed however many SCU of it came home. The
  line had been accepting that attachment all along, which is what made it look
  supported.

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
