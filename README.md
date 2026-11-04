<img src="web/assets/emblem.jpg" width="120" align="right" alt="Quantum Wake emblem">

# Quantum Wake

**A private Star Citizen logbook for Windows.**

Quantum Wake reads the logs Star Citizen already writes and turns them into a
personal record of your flights: where you went, what you flew, contracts,
trades, ships, gear and the things you choose to plan next. It runs on your PC;
there is no account and no required online service.

[![CI](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml/badge.svg)](https://github.com/peans99/QuantumWake/actions/workflows/ci.yml)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Local data](https://img.shields.io/badge/data-stays%20local-46617a)

> **Your game, your data.** Quantum Wake reads your logs and game files locally.
> Network features such as current market prices are off until you turn them on.

## See it in use

<table>
  <tr>
    <td width="33%"><a href="docs/images/now.png"><img src="docs/images/now.png" alt="The Now page"></a><br><sub><b>Now</b><br>Start with the last known location, ship, wake-up point and this session.</sub></td>
    <td width="33%"><a href="docs/images/map.png"><img src="docs/images/map.png" alt="The map"></a><br><sub><b>Map</b><br>Find places you have visited and nearby refuel, repair or clinic services.</sub></td>
    <td width="33%"><a href="docs/images/itinerary.png"><img src="docs/images/itinerary.png" alt="The Itinerary page"></a><br><sub><b>Itinerary</b><br>Lay out a flight plan stop by stop, and merge a hauling run into it.</sub></td>
  </tr>
</table>

<details>
<summary>More pages</summary>

<table>
  <tr>
    <td width="33%"><a href="docs/images/operations.png"><img src="docs/images/operations.png" alt="The Command deck"></a><br><sub><b>Command deck</b><br>Shopping lists, checklists, projects and the contracts open in this session.</sub></td>
    <td width="33%"><a href="docs/images/routes.png"><img src="docs/images/routes.png" alt="The Routes page"></a><br><sub><b>Routes</b><br>Find a cargo run sized to your ship, or a rep run to the next rank.</sub></td>
    <td width="33%"><a href="docs/images/reputation.png"><img src="docs/images/reputation.png" alt="The Reputation page"></a><br><sub><b>Reputation</b><br>Your rank with each organisation, read from Rep app screenshots.</sub></td>
  </tr>
  <tr>
    <td><a href="docs/images/events.png"><img src="docs/images/events.png" alt="The Events page"></a><br><sub><b>Events</b><br>The points behind each event bar, counted from contracts you finished.</sub></td>
    <td><a href="docs/images/logbook.png"><img src="docs/images/logbook.png" alt="The Logbook"></a><br><sub><b>Logbook</b><br>Sessions, purchases and discoveries, newest first.</sub></td>
    <td><a href="docs/images/sessions.png"><img src="docs/images/sessions.png" alt="The Sessions page"></a><br><sub><b>Sessions</b><br>Every session with its ship, time, jumps, contracts and server.</sub></td>
  </tr>
  <tr>
    <td><a href="docs/images/ledger.png"><img src="docs/images/ledger.png" alt="The Ledger"></a><br><sub><b>Ledger</b><br>Money in and out as the logs record it, with cash on hand from a screenshot.</sub></td>
    <td><a href="docs/images/market.png"><img src="docs/images/market.png" alt="The Market page"></a><br><sub><b>Market</b><br>Every commodity, where it sells, and your own trading against it.</sub></td>
    <td><a href="docs/images/fleet.png"><img src="docs/images/fleet.png" alt="The Fleet page"></a><br><sub><b>Fleet</b><br>The ships you fly, with flight counts, claim times and prices.</sub></td>
  </tr>
  <tr>
    <td><a href="docs/images/garage.png"><img src="docs/images/garage.png" alt="The Garage"></a><br><sub><b>Garage</b><br>A ship's fitted parts and the figures they add up to.</sub></td>
    <td><a href="docs/images/armoury.png"><img src="docs/images/armoury.png" alt="The Armoury"></a><br><sub><b>Armoury</b><br>Guns, armour and attachments from your installed game files.</sub></td>
    <td><a href="docs/images/loot.png"><img src="docs/images/loot.png" alt="The Loot page"></a><br><sub><b>Loot</b><br>Where any item drops, read from the game's own loot tables, beside what you have found.</sub></td>
  </tr>
</table>
</details>

The dashboard is for planning and history. The optional overlay gives the same
short answers over the game, and the optional Cougar MFD mode keeps navigation,
tasks and cargo close to the cockpit. The dashboard, overlay and MFD pages are
in English or French; choose under **Settings → General**.

## Start here

### 1. Download and run

**[Download QuantumWake.exe](https://github.com/peans99/QuantumWake/releases/latest)**,
move it somewhere you keep applications, then open it. There is no installer,
account or separate .NET download.

Quantum Wake starts in the notification area. Right-click its tray icon to open
the dashboard or overlay. The dashboard is also available at
<http://127.0.0.1:31337> while the app is running.

On Linux there is a separate download and no tray icon; see [Linux](#linux)
below.

<details>
<summary>Windows shows an unknown-publisher warning</summary>

The executable is not code-signed. If you downloaded it from the release page
above, choose <b>More info → Run anyway</b>. Every release is scanned by
Microsoft Defender during the release build; that is a malware scan of the
published files, not a publisher identity check.
</details>

### 2. Let it read your history

Quantum Wake finds LIVE, PTU and EPTU installations on fixed drives. On first
start it reads `Game.log` and its backups, then watches only the current log as
you play. A large log history can take a few minutes; the app shows the reading
progress.

If your installation is in an unusual location, run:

```powershell
QuantumWake.exe --path "D:\...\StarCitizen\LIVE"
```

### 3. Pick the view you need

| When you want to… | Open… |
|---|---|
| Check your last location, ship or wake-up point | **Now** |
| Find a station, city, outpost or nearby service | **Map** |
| Lay out a flight plan, or merge a hauling run into one | **Itinerary** |
| Read the live feed, or a screenshot you just took | **Log** |
| Plan the work: the Command deck, a cargo or rep run, contracts, reputation, checklists, blueprints, Wikelo trades or an event | **Operations** |
| Review a flight, session, place, server, crewmate or death | **Flight** |
| Follow money in and out, your cargo, or commodity prices | **Economy** |
| Look up ships, components, items, loot odds, mining or crafting | **Reference** |
| See your ships, fittings, loadout and stash, bench a part in the Garage, or compare guns in the Armoury | **Hangar** |
| Keep a small answer visible in Star Citizen | **Overlay** |

## Optional extras

### Overlay

The overlay starts off. Star Citizen needs **Borderless Windowed** mode for it
to sit above the game. Use the tray icon or `Ctrl+Alt+O` to show it. Its pin
button makes it click-through; `Ctrl+Alt+←/→` changes page, `Ctrl+Alt+F`
expands it and `Ctrl+Alt+C` switches to a small glance view.

### Cockpit HUD

If you use paired Cougar MFDs, Quantum Wake can show navigation, tasks, cargo,
contracts, money and the live feed on them. Read the [Cockpit HUD guide](docs/mfd-mode.md)
when you are ready to set it up; `Ctrl+Alt+M` opens MFD setup from anywhere.

### Screenshots and copied locations

You can ask Quantum Wake to read a saved Star Citizen screenshot or a copied
`/showlocation` result. This can add facts the log does not carry, such as a
kiosk balance, fitted ship parts, your rank with an organisation in the Rep
app, or the pickups and SCU on a hauling contract in the Contracts app. It reads
the saved file locally; it never captures your screen or reads game memory.

## Linux

Quantum Wake runs on Linux for pilots who play Star Citizen under Wine or
Proton. The dashboard, the log and game-data readers, screenshots and copied
locations all work there. The overlay, the cockpit HUD and joystick input stay
on Windows: all three are built on Windows desktop components, and they are
not what a Linux pilot is missing.

### Before you start

- **A working Star Citizen install under Wine.** Quantum Wake reads the game's
  own files, so the game has to be installed and started at least once. The
  Linux Users Group is the reference for that: its
  [LUG Helper](https://github.com/starcitizen-lug/lug-helper) installs the
  game, and its wiki's
  [recommended distributions](https://wiki.starcitizen-lug.org/Tips-and-Tricks#recommended-distros)
  are Fedora, Arch, EndeavourOS, openSUSE Tumbleweed, Bazzite, the latest
  Ubuntu release, Debian Testing and Gentoo; it advises against LTS releases
  such as Debian Stable, Ubuntu LTS, Mint, Pop!_OS, Zorin, openSUSE Leap and
  Manjaro. Its preflight check raises `vm.max_map_count` to 16777216 and the
  open-file hard limit to 524288, which the game needs and Quantum Wake does
  not touch. Quantum Wake was built and measured against an install the LUG
  Helper made on Fedora 44.
- **An x86-64 distribution with glibc 2.27 or later**, which is every
  distribution above. The Linux download carries its own copy of .NET, so
  nothing is installed for the dashboard; it needs `libicu` (the dashboard's
  French and date formats come from it) and `libstdc++` (the screenshot
  reader), both present on a desktop install. Microsoft lists Fedora 43 and
  44, Ubuntu 22.04 to 26.04, Debian 13, openSUSE Leap 16 and RHEL 8 to 10 as
  [supported by .NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md);
  Arch is not on the list and runs it.
- **For copied `/showlocation` results:** `wl-clipboard` on Wayland (Fedora KDE
  ships it as `wl-paste`), or `xclip` or `xsel` on X11. Without one the
  dashboard says the clipboard cannot be read; screenshots still can.
- **The command-line tool** in the same archive is not self-contained: it needs
  the .NET 10 runtime (`dotnet-runtime-10.0` on Fedora and Ubuntu). The
  dashboard does not.

### Start

Download `QuantumWake-<version>-linux-x64.tar.gz` from the
[release page](https://github.com/peans99/QuantumWake/releases/latest), then:

```bash
tar xzf QuantumWake-*-linux-x64.tar.gz
cd QuantumWake
./quantumwake
```

Open <http://127.0.0.1:31337> once the terminal prints `Now listening on` and
`Application started. Press Ctrl+C to shut down`. Nothing opens a browser for
you and there is no tray icon. It finds the game inside the Wine prefix on its
own: a LUG Helper install is read from the helper's settings, and otherwise it
looks in `$WINEPREFIX`, `~/Games`, `~/.wine`, Steam's Proton prefixes, Bottles
and Heroic. If it comes up empty, the dashboard says which prefixes it searched
and asks for the folder, which can be the prefix itself, the `LIVE` folder
under its `drive_c`, or the `C:\...` path copied from the RSI launcher.

```bash
./quantumwake --path ~/Games/star-citizen     # the prefix, or .../StarCitizen/LIVE
./quantumwake --Port 31395                    # another port
./quantumwake --Lan true                      # reachable from another device
```

Its data lives in `~/.local/share/Quantumwake` (or under `$XDG_DATA_HOME`), and
the .NET host unpacks its native libraries to `~/.net/quantumwake/` (42 MB) on
the first start. The first time you switch screenshot reading on it fetches
the two text-recognition models (14 MB) into that data folder.

On the machine this was built on, a Fedora 44 virtual machine, the start took
anywhere from 2 to 90 seconds before the ready line, all of it inside .NET's
own host start-up: an empty ASP.NET application shows the same spread there.
Wait for the ready line rather than reloading the page.

### Stop

`Ctrl+C` in the terminal stops it, within two seconds in every measurement;
closing the terminal stops it too. From anywhere else, `pkill quantumwake`. A
second `./quantumwake` while one is running prints
`Quantum Wake is already running at http://127.0.0.1:31337` and exits.

To have it start with your session and run in the background, a systemd user
service does it:

```bash
mkdir -p ~/.config/systemd/user
cat > ~/.config/systemd/user/quantumwake.service <<'EOF'
[Unit]
Description=Quantum Wake dashboard

[Service]
ExecStart=%h/QuantumWake/quantumwake
Restart=on-failure

[Install]
WantedBy=default.target
EOF
systemctl --user enable --now quantumwake      # start now and at every login
systemctl --user stop quantumwake              # stop; journalctl --user -u quantumwake shows its log
```

Adjust `ExecStart` to wherever you unpacked it.

### Update and remove

The dashboard still checks for new versions and tells you, but it does not
replace itself on Linux; unpack the new archive over the old folder. To remove
Quantum Wake, delete the unpacked folder, `~/.local/share/Quantumwake` and
`~/.net/quantumwake`, and disable the user service if you created one.


## What Quantum Wake reads

| Source | What it can add |
|---|---|
| **Your logs** | Sessions, travel, ships, contracts, party activity, inventory sightings and confirmed transactions. |
| **Your game install** | Names, items, commodities, recipes, mining deposits, place descriptions and facilities. |
| **Saved screenshots or copied locations** | Optional dated readings when you explicitly ask for them. |
| **Optional community services** | Current prices, shop listings, ship details and wider map/mining coverage. |

The data database lives in `%LOCALAPPDATA%\Quantumwake`. Nothing is written
inside Star Citizen unless you explicitly install **item labels** or
**StarStrings**. Both make a backup before replacing the game's English text
file.

## A few useful limits

- It is a logbook, not a live position tracker. Locations come from arrivals,
  inventory and quantum-travel records, and say when they are inferred.
- Current Star Citizen logs do not provide a complete killboard, automatic mining
  history or a complete wallet history. The app labels estimates and partial
  totals instead of filling gaps with zeroes.
- Crew and event totals are floors: players or contracts without the necessary
  log entries cannot be counted.


## Updates, removal and help

The tray icon checks for updates when you ask it to, downloads a new build and
restarts the app. Some updates re-read your history; release notes call that out.

To remove Quantum Wake, delete `QuantumWake.exe` and
`%LOCALAPPDATA%\Quantumwake`. Remove item labels from the app first if you
installed them. Linux removal is under [Linux](#linux).

**Settings → Report a problem** makes a small diagnostic file. It contains
parser counts, game builds and integration state, but not logs, account
identifiers, handles, folder names or API keys. You can inspect it before
attaching it to an issue.

## For contributors

The desktop app runs on Windows 10 or 11, and the dashboard and command-line
tool also run on Linux (see [Linux](#linux)). WebView2 is required for the
overlay and is
included with Windows 11; Windows 10 users can install it from
[Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/).

<details>
<summary>Build and test from source</summary>

```powershell
.\start.ps1                     # tray icon, dashboard and overlay
.\start.ps1 -NoOverlay          # server only
.\start.ps1 -Lan                # allow another device on the network
.\start.ps1 -Rescan             # rebuild the log cache
.\start.ps1 -Path "D:\...\StarCitizen\LIVE"

dotnet test Quantumwake.slnx -c Release
```

The [.NET 10 SDK](https://dotnet.microsoft.com/download) is only needed to build
from source. The command-line parser can export newline-delimited JSON with
`dotnet run --project src\Quantumwake.Cli -c Release -- --events`.

See [all documentation](docs/README.md) for the game-data reader, log format,
architecture, screen reading, simulator, data sources and release process.
</details>

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

### 0.21.2

**Ships, vehicles, weapons and armour in 3D.** A new 3D view opens any ship, ground vehicle, personal weapon or piece of armour from your game install's own models, with its stock loadout fitted: gimbals and the guns on them, turrets, missile racks and their missiles, countermeasure launchers, thrusters, a rifle's magazine. Drag to turn it, scroll to zoom, jump to the front, profile, top or rear, and save a picture. Click a part on the model or in the list beside it to see which slot it sits in and what it carries. Reference → 3D models opens on the makers, Drake, RSI, Anvil and the rest, each with its logo; pick one to see its hulls in the game's own pictures of them in their paint, the one you picked on Fleet or the Garage where you did, or search across them all. A hull opens in that same paint. Armour opens as the whole suit: a helmet, torso, arms or legs brings the rest of its set, in the same colourway where the files have it, with a Full suit box to see the piece alone. The view is a click away wherever a ship or item is: a 3D view button in the Garage and the Armoury opens it with your own fit in place, marked as yours; Fleet and Hangar cards have a 3D button; and the item card that Loot, Loadout, Stash and Items open has one whenever the install holds a model for the item. The cockpit and cabin are hidden unless you tick Interior: the game lights them with the ship's own lamps, which the view does not have.

**Paints.** The paint picker lists every livery the game has for the hull (ten for the Corsair) and recolours the model from that livery's palette. The game paints a hull from a palette of three colours laid over tileable panel textures, and the view draws exactly that, with the panels' normal and gloss maps, image lighting and ambient occlusion. It does not draw the wear, dirt and decal stencils the engine adds, so a hull looks cleaner than it does in game.

**What it reads.** Everything comes from Data.p4k on your machine; nothing is downloaded. The first model you open takes a few seconds: the app indexes the archive's model folders (about 1.5 s here) and lists what it can draw (about 3 s, then kept). Each model is packed once and kept in the app's data folder until the next game patch. It needs a browser with WebGL 2, which current Chrome, Edge and Firefox all have. Textures go to the graphics card still compressed, at most 2048 pixels across.

**Reference → Items is readable again.** The list held every entity the game files type, 26,531 of them, and offered their 153 engine types as the filter, from NOITEM_Player to hair colours. A third of the entries had no name but their class, such as cds_combat_superheavy_legs_01_04_01. The page now lists the named items only, about 8,000 here, under thirteen kinds you would look for: ship weapons, missiles and countermeasures, ship components, mining and salvage, ship paints, personal weapons, weapon attachments, armour, clothing, food and drink, medical and gear, flair, and collectibles. The whole list is still there under "Everything in the files".

**Fleet → Fitted, as last photographed is laid out again.** Each photographed fit had come out as four thin columns side by side. It is a card again: the ship and the date on top, then each slot with the part in it and whether it is stock.

**Item labels show in game again, and contract titles say their rep.** The game reads a loose text table only when `user.cfg` in the game folder sets `g_language = english`. StarStrings shipped that line in a `user.cfg` of its own, so once it was gone, the labels' file sat in the game folder unread: the item marks did not show, and nor did the rep StarStrings used to write on contract titles. Installing the labels now adds that one line when `user.cfg` names no language (it creates the file if there is none), and removing them takes back only that line. The labels also write what a contract pays on its title, the way StarStrings did: `[100 Rep]`, with `[BP]*` when it can award a blueprint. A contract that also pays on a second ladder says both, `[200+60 Rep]`, where StarStrings showed the sum. On this install that is 774 titles, and of the 523 that StarStrings' last table marked, 491 show the same figure and the other 32 show it split. Install the labels again from Settings → Item labels to get both.

**A better-looking 3D view that rests when you do.** A lighting picker (Studio, Hangar, Deep space) and a brightness slider; Studio, the default, lights a hull brightly from every side. With Effects on, edges are smoothed, thruster glows, running lights and screens glow, and painted panels have a clear lacquer that catches the light. High or Ultra textures (2048 or 4096 pixels) for close looks, and a Turntable that turns the model on its own. The view draws only when something changes: on this machine's RTX 5080 a frame with effects costs about 12 ms, and drawing one for every display refresh, as it first did, kept up to 0.8 s of each second busy on the graphics card for a model that was not moving. Left still, it now draws nothing, which leaves the card to the game when both are running.

**Your fleet in 3D, and attachments you can change.** Fleet has a Fleet in 3D button: the ships the page is showing, owned and through its search and filters, stand side by side on one floor, smallest first, each in the paint picked for it and with its weapons on. A list beside the scene ticks ships in and out of the row, and clicking one opens it alone. The first eight are brought in and the rest wait in the list, so a large fleet does not have to load all at once. A personal weapon's view has pickers for its sight, barrel and underbarrel, listing what fits each slot by the game files' sizes and tags. A pick fits the piece onto the gun straight away, and it is the same choice the Armoury shows. Guns also look like guns now: the layers they and armour are painted with had been drawn without their surface textures, which left most ship guns a flat light grey that read as white paint.

**Swap a ship's guns in 3D.** A ship's view has a Loadout list: every gun, mount, missile rack and mining or salvage head, named by where it sits ("manned turret left › weapon left › class 2"), with what fits it from the Garage bench's own lists. Choosing one swaps it on the model. For the ship open in the Garage it is a real Garage swap, so the bench and the build follow; for any other ship it is a try-on. Effects are now off until you turn them on, and the view remembers your choice: they double what a frame costs and the glow is not to everyone's taste. Attachments whose material sits beside their model, such as the KLWE flashlights, no longer come out plain grey.

**LIVE and PTU.** Quantum Wake now knows every channel on the machine - LIVE, PTU, EPTU, Tech Preview - and switches between them from the install label at the top of the page, or Settings → Data sources → Game channel. It reads one at a time and keeps each one's history apart, so a PTU evening never lands in your LIVE totals; switching restarts the app on the other folder. When the game is running on a channel the app is not reading, a notice at the top says so and offers the switch. Each channel keeps its own game-file caches, so going back and forth does not re-read the archive every time, and installing the item labels on PTU no longer loses track of the ones on LIVE. A channel kept in a library folder of your own naming, such as `D:siptuStarCitizenPTU`, is found too.

### 0.20.66

**Headhunters no longer look like they dropped a rank.** Headhunters have two ladders in the Rep app, Standing and Ship Combat, and screenshots of both were filed as one history. A Ship Combat Rank I screenshot followed by a Standing Jr. Contractor one read as a rank lost, and the trend line mixed the two. Each ladder now has its own card, named for its ladder ("HEADHUNTERS · Standing", "HEADHUNTERS · Ship Combat"), with its own history and change. The rep-run planner lets you pick which one you are climbing. The header still counts an organisation once.

**What a contract pays, for organisations that pay by difficulty.** The Rep page and the rep-run planner said what one more contract pays at your rank by taking the largest figure among the contracts that open there. That suits Red Wind, which pays one figure a rank. Some open several contracts at one rank that pay different figures. Headhunters pay by difficulty: at Jr. Contractor, Headhunters contracts run from +25 to +1,200, and the page priced every one at the two Hard outpost raids' 1,200. It now gives the figure most of them pay (+100 for Headhunters, matching the 22 in these logs; InterSec and Adagio also move from 200 to 100) and says the range beside it.

**Events no longer bury you in notifications.** Opening the Events page while your logs were still being read, or after an older log was read for the first time, raised a notification for every tier a past event had ever reached. A tier is now announced only when a contract you just handed in crosses it. Several at once are one notification.

**Item labels by kind.** Settings → Item labels has a picker beside the search to show one kind at a time, with how many each holds. Ship parts, which were all listed under Other (about 650 of them), are now listed as ship weapons, coolers, shields, power plants, quantum drives, missiles, missile racks, turrets, countermeasures, ship armour and so on. Hauling titles show what the game fills in, such as ‹rank›, ‹pickup› and ‹drop-off›, instead of tokens like `~mission(ReputationRank)` that looked like a broken rep figure. What gets written into the game is unchanged.

**A new face for the Armoury test bench.** The spread target is now drawn as an instrument: corner brackets, banded zones, a ticked bezel and range marks on the cross. A faint line joins each sample point to where the fit moves it. A narrower fit stays cyan with a green verdict under the target; a wider one turns the fitted pattern and the verdict amber. The handling meters fill green below 100 and amber above it. The scope preview has a proper reticle (rings, a crosshair broken at the centre, range marks) that stays sharp at every zoom, in place of the plain cross, and the shot marks shrink as you zoom in so they stay points rather than blobs.

### 0.20.63

The dashboard looks more like one instrument panel. Page headers, panels and pop-ups now have rounded corners and a soft top highlight. On your own history pages (Sessions, Fleet, Logbook and the like) the page header and summary figures are edged in gold; reference and planning pages stay cyan. Search boxes, drop-downs and text fields are rounded and show a clear ring when focused. Table headers are a little taller, the row under the pointer is highlighted, and numbers line up in columns. Feed tags are tinted by kind, and the main action button is filled.

Fleet cards lift slightly under the pointer. Rows, cards and links that happen to be buttons, such as itinerary plans, briefing links and command results, no longer appear in spaced-out capitals. The in-game overlay is unchanged.

### 0.20.62

**A quieter, easier-to-read dashboard.** The redesign's look stays; the clutter around it goes. Fleet cards drop the boxes around their status, logo, paint picker and star. Links in tables are told apart by colour and underline only when you point at them. Market's map actions and "+ list" stay grey until you point at their row. The source tags on every card (game log, screenshot, community data, UEX) are now words with a coloured mark instead of bordered chips.

Small labels were as small as 7 px and some were too faint for small text. Everything on the page is now at least 10 px, and the faintest grey is brighter. The in-game overlay keeps its compact sizes.

Section headings on Contracts, Loot, Sessions, Mining and Operations now use the page title's face, so a page reads as one design. The Armoury's three summary cards became a summary line above the tabs, Reference pages no longer split their header into a large empty half, the Fleet value no longer wraps at laptop widths, and the busy indicator in the top corner no longer cuts its own words off ("Reading game fil…").

### 0.20.61

**An attachment test bench in Armoury.** Pick a weapon and compatible attachments to compare a bare and fitted spread illustration, handling indices and derived firing figures. A gun's “Test this fit” shortcut carries its attachments into the bench. The target is a relative comparison of file multipliers: it does not claim measured shot placement, base spread angles or recoil paths.

The Armoury bench shows damage falloff and the fitted projectile travel limit beside the spread target. Beam modes show their full-damage distance and where the beam ends.

The Armoury bench previews sight magnification through a generic reticle, including alternate zoom levels, aim time and available zeroing settings. The attachment breakdown shows which pieces contribute to each modifier and the combined result used by the bench.

The Armoury test bench now lets you isolate either spread pattern and shows how much narrower or wider the fitted spread is. When a compatible spread-changing attachment can fill an empty slot, the bench offers it as a one-click example.

**The calmer Items colours across Reference.** Restored the original subtle row stripes instead of the stronger solid catalogue fills. Loot now shares the Reference header and table frames, including its item-location results. Encounter names use the same cyan link treatment as Items and can be expanded from the keyboard. Source, ownership and price colours keep their meaning.

**A complete Reference header frame.** The divider and filter-panel background now extend through the ships' flight lane to the bottom of the header, instead of stopping part-way down.

**Updated Components controls.** Component categories now use framed selectors with count badges and a clear selected state. The shopping-list action beside catalogue names also has a compact frame and retains its green confirmation after adding an item.

**Consistent Reference catalogues.** Ships, Components, Items, Missions, Encounters and Crafting now share the same table spacing, alternating rows, hover colours and pinned name column. Crafting also uses the shared Reference toolbar and results heading, with its filtered blueprint count visible above the grid.

**More room to fly.** The header's ships now use its available width while keeping their size. Each header picks a pursuit, overtaking or evasive flight with a varied starting position; some chases include a brief laser shot. A separate lower lane keeps the flight below the title and caption, and the reduced-motion version stays still.

**A smoother dashboard background.** Removed the cockpit-glass stripes, reflection layer, crossing route lines and background light pools, leaving a consistent dark blue surface as you scroll. On ultrawide displays, matching circular Quantum Wake emblems sit in both outer margins, clear of the content.

**A little flight across each header.** Two small ships follow a curved course along the page header, trailing short cyan and gold wakes. They fade at the ends for a quiet repeating chase and stay clear of the header's controls. This replaces the session-history strip and is decoration, not a reading of your ship or logs. Reduced-motion settings show a stationary pair instead.

**What a ping on the mining radar is.** Mining has a new **Radar** tab. Type the number your mining radar shows over a contact and it says what makes it: 6,340 is two Quantainium rocks, 9,400 is two C-type asteroids. The figures come from your installed game's own files. Every ship ore has its own, 15 apart within a tier (Quantainium 3,170, Stileron 3,185, Savrilium 3,200), so a ping names both the ore and how many rocks. Asteroid classes, hand-mining deposits and ground-vehicle deposits read one figure per kind of rock whatever ore is inside (every hand-mining rock reads 3,000, a C-type asteroid 4,700), so the tab lists what they might hold instead of guessing one. When two readings give the same number, as 19,200 is six Savrilium or five Aslarite, both are listed and the tab says the number cannot choose; nothing under 14,160 is ambiguous among ship rocks. A number nothing matches shows the nearest readings and how far off each is. Below sits every rock's figure with two to six of them worked out. The game files give one rock's figure: that a ping over a cluster reads it times the number of rocks is how pilots read the radar, and it has not been checked against a ping of yours yet, which the tab says.

**Every mission, and a window for each.** Reference has a new **Missions** page listing the 4,293 contracts your installed game offers, read from its own files: who offers each, its title, what kind of work it is, where it is offered (named as you would say it: Hurston, Pyro I and Monox, Stanton), the rank it needs and what it pays in rep and blueprints. The ones your logs show you taking come first, with how many you finished, and you can filter by contractor, type, system or "Flown by me". Click any mission, there or wherever a contract appears (the Contracts page, the Command deck's contract cards, a session's debrief, an event's contract list and a blueprint's "where to get it"), and it opens in the same window as items and ships. The window gives the mobiGlas briefing, the CrimeStat it is offered at, the ranks and earlier contracts it needs, the game's own estimate of how long it takes beside your own median, whether it can be taken again after abandoning or failing it, the game's four difficulty ratings, every rep change it makes on each ladder and when, the blueprints it can give, every time your logs show you taking it, and its other versions, such as the difficulty steps and the copies for other places. Checked against your logs: all 751 logged contracts are found in the files with the same name, and the files give a title for 737 of them. A title the game fills in when it shows the contract, such as Red Wind's "‹Reputation Rank› Hauler Needed", gives way to the words your logs saw. Contracts whose title the game asks of their contractor, such as every black-box, package and item recovery run for Bit Zeros, Hockrow, Dead Saints, FTL and Red Wind, show their real title and briefing (Bit Zeros’ Medium black-box run is “Too Smart For Our Own Good”) rather than an internal name; 37 contracts the game gives no title at all, mostly tutorial and first-time Hockrow facility jobs, still show their internal name. What a generated contract pays in aUEC is worked out by the game server when it is offered, so the files have no figure, and the window says so; the older Covalex hauls show their fixed sum, and a payout your logs printed is shown against the run it paid for.

**Who you meet out there, and what their holds carry.** Reference has a new **Encounters** page: every group of NPC ships your game files spawn in waves, 184 on this patch, with each wave's ships, how many of each, who flies them and their pilot skill, and what each one's hold is spawned with. A hold is filled to a share of the ship, so the page works out the most it can carry: a Nine Tails Cutlass Black in a hard bounty wave spawns with a quarter of its 46 SCU in distilled spirits, 11 SCU, priced at the best UEX sell when UEX prices are on, and each encounter adds its ships up, every wave and every ship at its fullest. The loads are small, the biggest being a Nine Tails Caterpillar with 144 SCU of spirits, and a fighter given a cargo list it has no hold for is said to carry nothing rather than given a figure. Which mission spawns an encounter is only in the files for some of them: 37 of the 172 in the universe are named by a mission script the game ships (the Stanton law missions' wanted policing, security patrols and bounty hunters, the service-beacon combat assist and the derelict freighter), and the page says which. The other 135, the bounty, steal, recovery and assassination waves among them, are named by no script on your machine, because the game server picks them, so the page shows only how the game files each one (Bounty missions, Pirate raids, Reinforcements) and what its name says ("hard · Crusader"), and labels it that way. Arena Commander's 12 are kept apart under their own filter. An NPC ship's window now lists the encounters that spawn it and what its hold carries in them. 65 of the game's 82 cargo lists, including the blockade runners' and the illegal-cargo loads, are carried by nothing your install has, and the page lists them apart rather than guessing who carries them. Your logs never mention an NPC's cargo, so none of this is checked against your play.

**A gun with its attachments on.** Open a gun on the Armoury and it now has a picker for each attachment slot it has (sight, barrel, underbarrel), listing only the pieces that fit it. That is decided by your game files’ own rule: the slot’s size and the tags it asks for, which is why stabilisers only go on energy guns and compensators only on ballistic ones, while suppressors and flash hiders go on both. Pick one and the gun’s figures are worked out with it fitted, beside the bare ones: the P4-AR with a Tacit Suppressor2 goes from 162 to 149 damage a second and from 480 to 442 a magazine. What the figures don’t carry, such as spread, recoil, sound, zoom and aim-down time, is listed under them. Six sniper rifles ship with a scope already on and open with it chosen. Two things the files don’t say are assumed, and the page says so: two pieces that change the same figure are multiplied, and the damage drop with distance is left as it is, so the comparison is at the muzzle. A saved kit on the Loadout page with a gun in it now has a button that opens that gun on the Armoury with the kit’s own attachments fitted.

**Which suit copes where.** The Armoury has a Climate tab: every planet and moon's baseline temperature, pressure and humidity, read from your game files, with whether an unprotected body copes there (the game puts that at 4 to 35 °C) and how many armour sets are rated for it. Open a body to see those sets, the ones with the most room either side first, and an armour set's details now list the bodies its range takes in. It answers questions such as what to wear on Euterpe at -73 °C (16 sets) or Terminus at -133 °C (only the Stirling and Novikov exploration suits), and it says plainly that nothing in the game is rated for Aberdeen's 250 °C or Pyro I's 226 °C. The figures are a baseline, not the weather on the ground: biome, altitude, caves and night all move them, and Daymar's reads 160 °C. A set is judged by its narrowest piece and only counts when it covers your body, so a helmet alone is listed apart. How the game combines pieces of different sets is not in the files, so a mixed outfit is not judged.

**Rep and blueprints straight from the game.** Every contract's reputation now comes from your installed game's own files instead of the StarStrings mod's title tags. The game knows exactly which contract you took, so a Red Wind haul shows the figure for that haul (250, 500 or 1,000) where the mod could only print the whole ladder, and the game also gives the 50 on Red Wind's Affinity ladder that the mod never showed. Checked against your logs: the game files know all 751 contracts logged on this install, and they agree with all 38 single-figure tags and all 145 ladders the mod wrote. The rep run's figure for one delivery is now what a contract made for your rank pays. Blueprints come from the game too, with their chance: all 18 blueprints this install received followed a contract the game marks as awarding one, where the mod's [BP] tag caught 10 of them, and the Covalex hauls the mod tagged, which gave none in 51 completions here, are no longer marked. A contract the game says pays no rep, such as an Iasi patrol, no longer shows one. StarStrings is now only a fallback, for a contract the game files do not know. The Standing table's rep column adds up finished contracts only, since an abandoned haul pays nothing. The first start after updating re-reads your logs once, so contracts you flew before get the game's figures as well.

**Each rep ladder counted on its own.** Some organisations have two progressions in the Rep app. Headhunters have Standing and Ship Combat, and the game files say their ambushes and patrols pay +100 on Standing, while only their three Hijacked Ship contracts move Ship Combat, at +500 each. A rep run on your Ship Combat screenshot used to price each delivery with Standing's +100; it now uses the 500 that actually moves that bar, and the contracts counted as done since a screenshot only add the rep that lands on the ladder shown. When an organisation's contracts do not move the ladder on screen at all, the rep run says which ladder they do move and asks you for the figure instead of borrowing one from the other ladder. When contracts pay the same at every rank but differ from one another, as Wikelo's trades do from 10 to 1,000, the figure is the most common one and the range is shown beside it.

**Hauling places without StarStrings.** The game never logs where a haul goes; the only way the logs ever named a place was a contract title the StarStrings mod had rewritten to ask the game for it. The item-label overlay in Settings now does that itself: when installed, hauling contract titles end in the places, such as "| Port Tressler > NB Int. Spaceport", "| to Ruin Station" or "| from Canard View", filled in by the game when it shows the contract, so the logs record them and the hauling planner can route your open hauls. It is worded exactly as StarStrings words it. On this install's game files that is 28 titles; StarStrings places 20 of them, and the overlay names the same ends on all 20, plus 8 the mod never covered, such as Red Wind's first haul and the Discovery Month hauls. If StarStrings is installed, the overlay keeps the mod's titles and only adds the missing ones. A title names only the end the contracts under it really have one of. It is on by default and has its own checkbox, "Name the places on hauling contract titles". Reinstall the overlay and restart the game to get it.

**StarStrings is optional now.** With rep, rep ladders and blueprints read from your game files and hauling places written by the item-label overlay, Quantum Wake no longer needs the StarStrings mod for anything, and its card in Settings says so. You can still install it from there for what it does in game, and the app still reads its tags for a contract your game files do not know.

**Settings → What was read.** One page that says what this app knows and where it came from. Each block is one place it reads from: your game logs (how many files, how many read, changed or never read, the sessions and game builds they hold, the last scan and your wipe), your game files (how long the read took and what came out of it: items, vehicles, places, recipes, deposits, paints, cargo grids, Wikelo trades, guns and armour), the community dataset and UEX feeds with their dump and fetch dates, your screenshots by kind, and what you wrote yourself (lists, checklists, plans, notes, builds, kits, keybinding versions). The table of game signals that still arrive moved here from General. At the end, the data folder entry by entry with its size, so you can see exactly what is kept on this machine and back it up or remove it knowingly.

### 0.20.35

**A new look for the logbook.** The dashboard has its own flight-deck style: calmer panels, angular buttons, and a small geometric glyph for every workspace and menu group, in the menu and beside its page title, and a refresh mark on Check for updates. A telemetry line keeps your system, location, ship and connection in view, and the current system and ship tint the quiet details around the page. Completed contracts earn small mission patches, and the live feed reads as a flight recorder. Update, patch and data notices now share one compact strip, with their detail and choices one click away instead of a full row each above your work. The Star map keeps its own star field and markers. Page changes make one short scan, and the reduced-motion setting is respected. The menu fits a laptop-width window down to 1280 pixels, in English and in French, and the new labels, from the telemetry line to the notices, are translated.

**Every page reads the same way.** Each page now opens with the same header: where it sits in the menu above the name you clicked, so Sessions is called Sessions and the Loadout page is no longer titled Armoury. The explanation of how a page works is folded under **About this page**, so the first screen is the page itself; Crew and Events keep theirs open because they lead with "every number is a floor". Anything cyan can be clicked: names in tables are plain text unless they open something, and every link looks the same. Row buttons, such as Market's "sell on map", "buy on map" and "+ list", are quiet words instead of bordered buttons, and the Items table's "+ list" no longer hides until you hover. A checklist line that names its item is now the link itself, with no second chip repeating the name, and it opens the item's Reference window. Items and ships open in that window from everywhere, including the search box, and the side drawer is kept for places and commodities beside the map.

**A look of its own.** Gold is now the colour of your own record (flights, sessions, what you sold, what you picked up), separate from the orange that warns a price is stale. Cyan stays for the game's own files, green marks the community dataset and violet marks UEX prices, on the source badges, on the column headings they cover, and in a shared report. The label above a page's title turns gold on the pages that are your own history, such as Sessions, Logbook, Ledger, Crew and Fleet, and stays cyan on reference pages. Under every page header runs your wake, drawn from your own sessions: the last 60 days, widest on the days you played most and fading into the past, with a mark at today that fills in once you have flown today. Point at the mark for the numbers behind it. Page titles are set in Big Shoulders Display, an open-licence typeface bundled with the app, so nothing is fetched to draw it. The in-game overlay keeps its compact style.

**Now knows when the game is closed.** The game keeps its log after you quit, so the Now page used to show your last session as if you were still flying it, with the session clock counting up for days. With the game closed, the page now says so at the top: when your last session ended, how long it ran, where you left off and aboard what, with your wake beneath it and a button to open it in Sessions. The clock stops at the session's real length, the cards are labelled as your last session, a jump the log ended in is no longer shown as under way, and the telemetry line reads Game closed. Everything goes live again when the game starts. The game counts as running while its process is open or its log was written to in the last two minutes.

**Every item opens the same way.** An item or ship component used to be a link on some pages and plain words on others: clickable in the Items table and on a checklist, not in your Loadout, your Stash, the Ledger or the Logbook, and a farm list sent you to the Loot page while the Armoury only opened a row of figures. Now every item name, wherever you meet it, opens the same Reference window: what it is, whether you own it, every shop and price, its blueprint, where it drops and who carries it. That includes the Loadout and Stash, the Ledger and the Logbook, a session's debrief and the live feed's "Bought" lines, the Spending chart, the Loot page, event rewards, the Armoury's guns, armour, sights and knives, the Garage bench and a checklist's "Where to get these". On the Loot page the name now opens the window and the odds open the where-it-drops panel, so an item no table rolls is no longer dead text. Ships follow the same rule: a ship's name on a Fleet card, in the Sessions and Crew tables, on the Casualties chart, in a Hangar comparison and on the Now page opens the ship's own window, with its spec sheet, shops, stock parts and NPC versions. A Wikelo trade now says what each thing it wants or gives is, so a hull opens the ship window, an ore the commodity drawer and a favour the item window. In the Garage the fitted part on each slot opens its window too, while the slot itself still opens the bench. One kind of name stays as words on purpose: a fitting read off a screenshot, which has no item behind it.

**Where to find it: loot odds, read from the game.** The Loot page opens with a search over every item the game's own loot tables can produce. Pick one and it lists every kind of place and every kind of body that can drop it: the container it comes in, the chance one roll of that container lands on it, how often a slot of that kind holds that container, the crate's refill timer where the game gives one, and the table entry it comes through. Bodies are corpses, prisoners and named bosses. Nothing is downloaded: it is all read from your install, so it follows each patch. On this install that is 220 tables, 5,665 items and 2,230 containers and bodies. The odds are estimates and the page says so: the game's files give the weights but not how the draw is made, a crate draws several times depending on its size, and each colour of an armour set is its own item, which is why armour odds look small.

**Named places.** Above the kinds of place, the same panel now lists the actual places: the star map's own names, such as Dupree Industrial Manufacturing Facility on Hurston or Onyx Facility S1B1 on Aberdeen, with the planet or moon, the best chance per crate slot there, and how many crate sets the place holds. The best twelve show first, with a button for the rest and a picker to keep to one planet or moon. A spot the star map does not mark, usually a cave, is listed as an unmarked spot on its body. It comes from the game's map files on your install, read once per patch in a couple of seconds. On this install that is 529 named places, and 5,045 of the 5,665 items have at least one. Some kinds of place are in the loot tables but no map file sets them down, including the station crates and the occupied caves. Those rows now say "not placed" rather than suggesting you will find them. Checked against these logs: every crate pickup that followed a course to a planet or moon, 13 of them, was an item the map files place on that body.

**Parts off NPC ships.** Search a ship part, such as a gun, power plant, cooler, shield, quantum drive or missile, and the panel lists the NPC ships that fly with it fitted as standard, one row per ship model, with who flies them (criminals, civilians, the UEE Navy, Nine Tails and so on) and how many the best-armed version carries. It is read from the ships' own loadouts on your install: 614 NPC ships carry 413 kinds of part. It shows what a disabled ship has aboard, not how often you'll meet one; the game's files don't say that, and the page says so. Your logs can't confirm this one either, because the game never logs taking a part off a ship.

**What NPCs wear and carry.** The same panel now lists the kinds of NPC that spawn wearing or carrying the item, which is what their bodies hold when you loot them. For example, Hurston's guards always carry the Gallant rifle, and wear the Hurston Security edition of the Balor helmet in one of their three outfits. The share column is an estimate: the game's files list the outfits an NPC can be given but not how one is picked, so each is counted as equally likely, and the column is labelled that way. These loadout files are stored encrypted in the game's archive. Quantum Wake now reads them, and only them, using the key the community extraction tools published. On this install that is 2,571 items across 778 kinds of NPC. Checked against these logs: 46 of the 130 item classes you have seen in a crate, body or pack are worn by some NPC, including 20 of the 34 armour pieces.

**Farm my list.** Every checklist has a **Where to get these** button. For each open line it shows the best place to find the item, the NPC likeliest to carry it, how many NPC ships fly with it, the cheapest shop when UEX prices are on, and how long it takes to craft if the game has a recipe. Above that is a ranking of the places that yield the most of your list at once. When many places are equally good, which is common because the same crate set stands at dozens of outposts, the panel says how many tie rather than pretending the first ten are better. Lines it can't match to an item, such as a commodity, are named, not dropped. The item names open their full breakdown on the Loot page.

**Where to buy it, too.** An item's panel on the Loot page, and each line of a checklist's "Where to get these", now say where to buy it: the cheapest shop in the UEX prices when that feed is on, and what you paid the last time you bought one, at which shop, from your own purchases, which needs no feed at all. The game's own shop price files were checked and are years out of date, so they are not used.

**Cargo fit without the download.** The Garage's cargo fit now reads each ship's cargo grids from your own install: how many grids the ship carries, their size, and the largest box each takes. It no longer needs the community dataset, and on an install whose dataset predates cargo grids it no longer asks you to refresh. Checked against that dataset, 145 of the 149 ships it gives a cargo figure for match to the SCU. The four the install doesn't cover, the Hammerhead, the Nomad, the 135c and part of the 890 Jump's hold, still come from the dataset when you have it, and the page says which source drew the hold.

**Reference: look anything up and dig into it.** The Reference menu now holds Ships, Items, Loot, Mining and Crafting, so looking things up lives in one place (Loot moved here from Hangar). Click any item or ship and it opens in a window with everything known about it. For an item: what it is, whether your logs have seen it in your kit, every shop that sells it in the UEX prices with what you paid, its crafting recipe, the ships that come with it fitted, and where it spawns: places, crates, the NPCs who carry it and the NPC ships that fly with it. For a ship: its spec sheet and size, the shops and rental desks, the parts it comes with, and every NPC version of it with who flies it and what it carries. Every part and ship inside opens in turn, and **Back** walks the trail, so you can go from a ship to its gun, to the NPCs who drop that gun, and back. The Items table gains a **Drops** column and a **Can be found** filter: 5,840 of the 26,531 items the install describes can be found somewhere, 5,304 of them in crates. Ships lists 299 hulls, 11 of them read from your install because the community dataset does not describe them yet, such as the Constellation Mk IV and the Skylark.

**Track what you're hunting.** An item's panel on the Loot page has a **Track it** button that puts the item on a checklist, your pinned one by default or a new "Looking for" list, linked so the list's "Where to get these" can find it. It won't add an item that's already an open line on that list. A checklist line now also notes when the item shows up in your inventory after you wrote it, in amber, without crossing the line off, because the game's inventory listing doesn't say whether you looted, bought or were given it. If it had shown up before you wrote the line, the hover says it may be one you already had.

**Your pickups beside the odds.** The New items table gains a Best roll column, the best single roll any container or body gives for that item, with where on hover. The item's name opens the same breakdown. Items no table produces, such as hangar decorations and things you bought, show a dash rather than a zero. Checked against these logs: of the 130 item classes seen inside a crate, body or pack, 126 come out of some table, and the other four are a goldfish and three ship models.

**For the command line,** `--loot` prints every table and source with which presets the map files place, and `--loot=<item class>` lists every container, body and named place that can roll one item, every NPC ship that flies with it and every NPC that wears it.

**Ship components, by what they do.** Reference has a new **Components** page: every ship component a pilot can fit, 672 on this patch, with a tab for each kind (weapons, missiles, missile racks, shields, power plants, coolers, quantum drives, radars, and mining and utility). Each tab shows the figures that kind is chosen on: DPS, alpha, fire rate, range and projectile speed for guns; points, regeneration and downed delay for shields; output for power plants and coolers; speed, spool and cooldown for quantum drives; damage, lock time and tracking for missiles. Next to those are size, grade, maker, the cheapest UEX price, where the game files drop it, and how many ships come with it. Click any heading to sort by it, and click a name to open the part's Reference window. The figures come from the community dataset; without it the page lists the parts your install names and says the figures need it.

**From an item to its blueprint, and where to get the blueprint.** An item's Reference window now has a **Blueprint** section: the recipe, whether your logs show the blueprint in your library, how many ways there are to get it, and a link to the blueprint itself. The blueprint opens in the same window and says where it comes from, read from the game files: each reward pool it is in, what share of that pool's payouts it is, and every contract that pays out the pool, with who offers it, the contract's title, the systems it runs in and the chance it pays. Event tiers that award it are listed too. For example, the CF-337 Panther is one in seven of what Foxwell Enforcement's Orange and Red level contracts in Nyx pay out. A pool that nothing on this build pays is said to be unpaid rather than guessed at. Blueprint names on the Crafting page open the same view. Only the blueprints on the game's starting list are called known from the start, and there are 8 of them. The Crafting page used to imply more, and its "Known by default" filter showed nothing on an install; it now shows those 8.

**Fixes.** The favourite star on the Now page's Server card no longer spills the word "Favourite" out under an empty box: it is the star alone, as on the Servers page, with the words on hover. Every button that is a lone symbol, such as the × that removes a checklist line or the ↑ and ↓ that reorder a plan, is now drawn the same way: the symbol alone, outlined when you point at it, rather than lost in an empty frame. Number boxes and dropdowns now draw their arrows dark instead of in the browser's light theme, and the savings goal on Now, the capital on Routes and the rock mass on Mining take any amount: a goal of 250,500 aUEC was refused because the box only accepted whole thousands. On a fast machine, two keybinding versions kept within the same millisecond could be filed in the wrong order, and the Controls watch would then refuse a version the game had genuinely gone back to; each kept version now gets its own moment.

### 0.20.0

**A first Linux download.** Alongside `QuantumWake.exe` the release now carries `QuantumWake-0.20.0-linux-x64.tar.gz`: the dashboard and the command-line tool, for pilots who run Star Citizen under Wine or Proton. Unpack it, run `./quantumwake` and open http://localhost:31337. It finds the game inside a Wine prefix on its own: a LUG Helper install is read from the helper's own settings, and otherwise it looks in `$WINEPREFIX`, `~/Games`, `~/.wine`, Steam's Proton prefixes, Bottles and Heroic, in the RSI launcher's log inside each prefix, and on any drive letter the prefix maps. If it still comes up empty the dashboard says which prefixes it searched and asks for the folder, which can be the prefix itself, the `LIVE` folder under its `drive_c`, or the `C:\...` path copied from the launcher; it remembers the answer. Reading screenshots works on Linux too: where Windows OCR is not available, Quantum Wake reads them with PaddleOCR's text models on Microsoft's ONNX Runtime, fetching the two model files (14 MB) the first time you switch screenshots on. Run over this install's 146 saved screenshots on Linux, the same readers named contracts on 37 frames, loadouts on 20, reputation on 21, maps on 7 and the wallet on 103; a 3440×1440 frame takes about a second and a half on four cores. The same engine steps in on a Windows PC whose OCR has no English language pack, which until now left the screenshots mode dark. The clipboard is read on Linux as well, through wl-paste or xclip, where one is installed. The overlay, the MFD HUD and joystick input stay Windows-only. Two things that would have gone wrong there are fixed: ship paints and weapon-attachment icons went missing, and on a fresh Linux account the app kept a separate history in every folder it was started from.

**Linux, start to finish, in the README.** A new [Linux](#linux) section says what the game install needs first, which distributions .NET 10 and the Linux Users Group recommend, how to start and stop the dashboard from a terminal or as a systemd user service, where its data goes and how to remove it. Running the Linux build from a terminal also turned up three things, all fixed: it could crash at start with a floating-point exception, a divide by zero inside .NET 10's server garbage collector that struck four times in about twenty-five starts on the test machine, and the dashboard now uses the workstation collector the Windows app always had, with no crash since; a second copy started while one is running says so in one line instead of printing a stack trace; and the server no longer registers a file watch on every folder beneath wherever it was started from, which from `/` was 49,829 watches and from a home folder with a game library under it could reach the account's limit.

**On Windows.** `QuantumWake.exe` is 12 MB larger, 125 MB from 113 MB, because it now carries the second screen reader. Nothing else changes on a PC whose Windows OCR reads English; on one without an English language pack, switching screenshot reading on now fetches the two model files and reads them instead of leaving the mode dark.

### 0.19.9

**Security update: built on .NET 10.0.14.** Quantum Wake carries its own copy of .NET inside `QuantumWake.exe`, so updating .NET on your PC does not update the copy the app uses. Only a new build does. 0.19.7 and earlier were built on 10.0.13, which predates Microsoft's 25 October out-of-band fix for CVE-2026-111497 (Brotli decompression) and CVE-2026-70197 (socket memory corruption). Nothing in Quantum Wake is known to reach either flaw: it decompresses no Brotli, and its dashboard only listens on your own PC unless you turn on LAN access. Updating is still recommended, and it matters most if you use the dashboard from a tablet over LAN.

**Ended contracts you remove from a plan can be put back.** On the Itinerary, "Remove ended lines" took a dropped contract's lines off the plan, along with any stop that held nothing else, and there was no way to undo it. The lines it removes are now listed in a folded "Removed with ended contracts" section under the warning, one contract to a row. **Put back** returns that contract's lines to the stops they came from and rebuilds, in its old place, any stop that went with them. Nothing else on the plan changes. If the game has dropped the contract, its lines are marked as ended again.

### 0.19.7

**Shops and gear are called what the game calls them.** Purchases at food carts, Pyro's rest-stop stalls and a few other counters showed the shop's internal code: "Pyro RStop stall med 001 rund weaponsmith", "HotDogBar Food RestStop". They now read "Weaponsmith Stall", "Hot Dogs", "Ellroy's Coffee", "Chuan Cart" and so on, using the game's own names where it has them. "Conscientious Objects" is recognised too; the game misspells it in its own files, which is why it never matched. The live feed on the Now page had shown that code for every purchase, even at shops like Casaba Outlet that the Ledger always named, and now shows the name. About 180 items the game files under a variant of the usual key, among them the ORC-mkX SecondWind helmet, showed their code in your stash; they are named now, as is a variant flashlight that read "volt_ubarrel_flsh_s1_Sniperglint" (it is a Lux Flashlight). The beam inside a fitted flashlight no longer appears in your loadout as a piece of gear of its own. The first start after updating takes a little longer while the item names are read from your game files again.

**Quantum jumps say where you were heading instead of showing the game's internal code.** A jump to a mission beacon, a party member, your parked ship, an orbital laser platform or one of the surface outpost clusters used to appear in the live feed, the Now page and the logbook under an id such as `orbtl_002_plat_001_util_a_orbital_001_occu_a_final`. In one long history that was 54 of the 203 places ever targeted. They now read "Distress Beacon", "Party Member", "Unattended Ship", "Rayari Research Cluster" and so on. The asteroid mining bases, which read like "ab mine med 006, Crusader", are now "Mining Base, Crusader", and a wake-up point at one of Nyx's numbered QV extraction stations reads "QV Extraction Station 028" instead of "Nyx_TSG_QVExtractionStation_028". Once you land, a jump to a party member or a cluster takes the name of the place you actually arrived at. Orbital laser platforms are named from your game files. The game numbers them without saying which moon they circle, and the same number is used above more than one moon, so where it could be either the jump says "Attritus OLP or Vivere OLP" rather than guessing. Your history is read again once after updating, so older sessions get the names too.

### 0.19.5

**Merge a hauling run into a plan, and put a plan in order, from the Itinerary page.** While you have hauls open, the Itinerary page shows the live run beside the plan you have open, with a tick box for every load and drop. Take the whole run, one contract, or a single drop, and press Add to put those lines on the plan. Each line joins a stop the plan already has for that place, or becomes a new stop. A line the plan already carries for the same contract is marked "on this plan" and is never added a second time, so merging the same run twice does not give you two sets of the same contracts. With no plan open, the same ticks make a new one. **Put in order** (also at the top of every plan) reorders the stops you have not yet flown. Each contract's pickups come before its drop, stops on the same moon or planet are grouped, and the order starts from where you are. It is an order that holds together rather than the shortest route, because the app knows no distances between stations. A trade run's purchases always stay ahead of their sales.

**Contracts screenshots read far more reliably, so a hauling run lists the right stops.** Of nine cards photographed in one evening, the run took pickups and SCU from only three. The rest were placed from their titles, and one card showed a pickup as a drop-off, listed twice. Several separate problems were behind it, and all are fixed:

- When a Collect line lost its diamond, it was joined onto the line above. It is now read as a pickup of its own.
- A card photographed before the game had finished printing its objectives ("Collect Carbon fro") now takes its pickups from the contract's own text beside them, and its destination from the title.
- A rep bracket on the line under the title no longer stops the card matching its contract.
- "Dudley & Daughters" is recognised however the "&" comes out of the reader.
- A card you photograph straight after accepting a contract stays with that contract. Before, two look-alike Carbon hauls could swap cards once one of them was handed in.

When the game's own text names a place "Location2Address" or "Destination2Address", the run says the game left that place unnamed, rather than inventing a stop. Screenshots already read by an older version are read again on their own once, while screenshot reading is on.

**An old hauling plan no longer crosses off stops for contracts that have ended.** A plan made from a hauling run stays tracked until you finish it, and landing at one of its stops used to tick that stop whatever had become of the contracts behind it. A plan from a month ago could be crossed off while you flew new hauls past the same stations. Now a stop is only ticked on arrival if one of its contracts is still open in your session. Stops you added yourself tick as before. The Itinerary page says when some or all of a plan's contracts have ended, marks their unticked lines "contract ended", and has a button to remove those lines along with any stop left with nothing else. Lines you already ticked stay, because a contract you handed in has ended too and its drop is a record of the work. The Now card says so too.

**Itinerary protects run-sheet drafts and makes its controls easier to use.** A live refresh keeps an open line form, deleting a plan or stop asks first, and the line handles support arrow keys. The page also reports when a change cannot be saved.

**Itinerary keeps unfinished plan names and stops while it refreshes.** Your draft and cursor stay in place when updates arrive.

**An Itinerary page: your flight plans with room to work on them.** It sits next to Map in the top bar and shows the same plans as the map's flight-plan panel, with each stop as a full card: where it is (moon and system, from the map), its note, and every line of its run sheet. Drag a stop by its handle to put it anywhere in the run, or use the arrow keys on the handle. Drag a run-sheet line onto another stop to move it there, with its tick, its quantity and its price intact, or drop it between two lines to change their order. Add a stop by typing a place: a name the map knows is placed on it, and anything else is kept as you typed it, with the card saying it is a name only. The plans you have are listed on the left, and opening one there does not stop the map following the plan you are tracking; Show on the map tracks it first, because the map only draws the tracked plan, and the button's tooltip says so. Start, Finish, Track and Delete are at the top, and your finished runs are below the list with Resume, Review and Repeat. The map's flight-plan panel has an Itinerary button that opens the same plan here.

### 0.18.29

**A rep run on the Flight plan page: one small delivery, flown until the next rank.** Pick an organisation you have a Rep app screenshot of, say where the crate is picked up and where it goes, and the page works out how many deliveries take you to the next rank. Make it the flight plan and you get a pickup and a drop for each delivery, with the reputation you should be at after every drop and the rank named on the drop that reaches it. What one delivery pays is filled in from the rep tag StarStrings puts on that organisation's contract titles, at the rank you hold: 500 for a Red Wind Member haul. That figure is StarStrings' research rather than the game's, so the page says where it came from and you can type over it. A long climb is written out 25 deliveries at a time; take a fresh Rep screenshot when they are done and plan the next batch from there.

**A saved trade run can be changed once you are flying it.** The kiosk had less stock than the plan, or the buyer's price moved: open the flight plan, press Change on the buy or the sale, and type the SCU or the price you actually got. The profit is worked out again, and the figures you first planned stay beside the new ones, with what your changes did to the profit. Cutting the buy short also cuts the sale, when there is one sale for the whole load, so "I only got 80" is one change rather than two. If the sales add up to more than you bought, the plan says so. This works for trade runs saved from this version on; older ones were saved as notes, without prices, and stay as they were.

**A Rep app screenshot whose bars look paler is read properly.** On some screenshots the filled part of the rank bars is drawn closer to cyan than teal, and the app took those bars for empty: a Red Wind screenshot three-quarters through Member was recorded as Member 0%. Those bars now read correctly (that one reads 78%, which matches the four hauls finished since the screenshot before). Rep screenshots already recorded at exactly 0% are read again on their own while screenshot reading is on.

### 0.18.26

**A Rep app screenshot taken through glare is no longer missed.** The app used to need both of the Rep app's tabs, CAREER and DOSSIER, to recognise it, and a flare that hid one made the whole screenshot unreadable. One tab and the rank cards under it are now enough, and screenshots passed over that way are read again on their own while screenshot reading is on. The estimate also passed its first real test: seven Red Wind hauls worth 500 each moved the bar by 15 points of Member, within a pixel of the 15.6 they should.

**Each organisation on the Reputation page wears its own logo**, the same mark the Rep app shows, taken from your installed game files. Nearly every organisation has one; the few that have none in the game, such as Dead Saints, simply show their name.

**The app knows its own version again, and stops offering you the update you already have.** Since 0.18.11 every build has called itself 0.18.11, whatever release it was: one of the version numbers stamped into the program had stopped being updated. The About page and footer showed 0.18.11, and the update check kept announcing the latest release as new, even straight after installing it. This version reports its real number, so the update prompt goes quiet once you have it.

### 0.18.23

**Reputation makes its current position and history easier to read.** Each organisation now separates its rank, progress, estimated reputation and distance to the next rank into a compact summary. The rank path and history chart carry visible labels, so the page still explains itself on a narrow or touch display where hover details are unavailable.

**A Reputation page: your rank with each organisation, and how it moves.** The game never writes reputation to its logs, but the mobiGlas Rep app shows it, and Quantum Wake now reads it from your screenshots. Open an organisation in the Rep app and press Print Screen. The new Reputation page, under Operations, shows the rank you hold, how far through it you are, and an estimate of the reputation behind that, worked out from the rank thresholds in your installed game files. Each new screenshot adds a point, so the page shows how a standing has changed since the last one, next to the contracts for that organisation your logs show you finishing in between.

The estimate assumes the bar in the Rep app fills evenly through a rank, which nothing has confirmed yet, so it is labelled as an estimate and shows its margin: one pixel of the bar, a few reputation on a low rank and over a hundred on a high one. The contract count between two screenshots is a floor, because the logs only hold what they kept.

Rep app screenshots you took before this version had their rank left out. While screenshot reading is on, they are read again on their own, so an existing history gets its ranks back.

### 0.18.20

**Plugging in a USB device no longer closes Quantum Wake.** With the Cougar MFDs turned on, connecting any USB device (a stick, a headset, a memory stick) could make the app vanish. The MFD buttons were read through an old Windows joystick interface whose device list is rebuilt whenever something is plugged in, and that rebuild crashed inside Windows, taking the app with it. The MFD buttons are now read a different way that never touches that code. Nothing changes in how they work: the same buttons do the same things, including while the game has focus.

One small difference: a button you are already holding down when an MFD is connected now counts as a press when you next press or release anything on that MFD. Before, it was ignored until you let go.
