<img src="web/assets/emblem.jpg" width="150" align="right" alt="">

# Quantum Wake

**A private Star Citizen logbook for Windows** — by nekron

[![CI](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml/badge.svg)](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Licence Apache 2.0](https://img.shields.io/badge/licence-Apache--2.0-blue)
![1124 tests](https://img.shields.io/badge/tests-1124%20passing-4fd48a)
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

The repository currently has 1,124 tests. `Quantumwake.Tests` covers parsing,
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

### 0.9.45

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
