# Star Citizen Log Tooling — Research

Research notes for building a Star Citizen `Game.log` UI.

Compiled 2026-08-20 against a real install: **SC 4.9.188.23497**, handle `nekron`,
144 log backups spanning **2026-04-20 → 2026-08-17 (402.5 MB)**.

## Contents

| Doc | What's in it |
|---|---|
| [findings.md](findings.md) | **Read this first.** The kill-event problem, with evidence. The app stopped showing a kill counter in 0.6.8; the events are still absent and the parser still dormant. |
| [log-format-reference.md](log-format-reference.md) | Line formats verified present in the local logs, plus the removed ones. |
| [architecture.md](architecture.md) | Runtime design decisions: dashboard, overlay, paired MFD HUD, local screen reading, storage and the boundaries between them. |
| [interface.md](interface.md) | The dashboard's layout rules from the 0.20.24 consistency pass: one page header (the menu group over the menu's own word), explanations folded under About this page except a floor, cyan only for what can be clicked, and one detail view per kind of thing. |
| [phase-1-core.md](phase-1-core.md) | Build log: the Core parser, what it verified, and the quirks it uncovered. |
| [phases-2-5.md](phases-2-5.md) | Build log: server, dashboard, map, overlay, and the dormant combat parser. |
| [mfd-mode.md](mfd-mode.md) | Cougar MFD HUD: pages, paired-display setup, USB profile, the screen-reading summaries it can use and verification. |
| [screen-insight.md](screen-insight.md) | Saved-screenshot and clipboard reading: local OCR, what is kept, what is checked against logs, and what it deliberately cannot claim. |
| [log-simulator.md](log-simulator.md) | The fake-log generator: how to use it and why it reproduces the format's quirks. |
| [untapped-signals.md](untapped-signals.md) | Log signals we have not used yet, ranked, with formats and counts. |
| [commodity-names.md](commodity-names.md) | Why a cargo sale cannot be named, and where the mapping actually lives. |
| [commodity-catalogue.md](commodity-catalogue.md) | What the DataCore does and does not hold about commodities and shops. |
| [credits.md](credits.md) | Every external resource this app uses, and what was taken from each. |
| [naming.md](naming.md) | Why the project is called Quantum Wake, and what the name had to survive. |
| [landscape.md](landscape.md) | Who else builds on Game.log, what overlaps, and what is still ours. |
| [org-network.md](org-network.md) | Plan, unbuilt: pooling blueprints, stashes and prices across an org. |
| [screen-insight.md](screen-insight.md) | Plan, unbuilt: reading a screenshot to name an item, with what OCR can and cannot be asked to do. |
| [player-market.md](player-market.md) | Plan, unbuilt: what a pilot will pay rather than what a shop pays, from UEX's marketplace feed. |
| [precise-poi.md](precise-poi.md) | Plan, unbuilt: marking a place by coordinates the game hands over, and the line that keeps it out of macro territory. |
| [garage.md](garage.md) | Plan for 0.13: a ship's full stat sheet from the community dump, try a part and see what moves - DPS, shields, EM, IR, power - save the build, shop for it. Includes the signature model, re-derived from the dataset's generator and checked on 269 ships. |
| [mining.md](mining.md) | The 0.14 line: the mining model read from the install - constants, 42 minerals, 213 compositions, 18 lasers, 29 modules, 6 gadgets - and the rock calculator that runs the community's rule on it, with whose rule it is and what would replace it. |
| [armoury.md](armoury.md) | The personal armoury: 46 guns and 2,349 pieces of armour read from the install - where a gun's damage actually sits (on the ammunition), how the fire modes nest, why armour resistance is by class and not by piece - the derived figures and whose they are, and UEX prices by the game's own id. |
| [loot.md](loot.md) | Loot odds read from the install, loot-verse's question answered locally: 220 tables (V3 and older), 2,230 containers and bodies that roll them, tags that count for their ancestors, mission slots and empty rarity bands skipped, per-roll estimates and what makes them estimates; 126 of the 130 classes these logs saw in a container reachable; named places still to come from the object containers. |
| [joystick-mapping.md](joystick-mapping.md) | Research for a Controls page (0.15): where the game keeps joystick bindings and why they break when sticks re-enumerate, the 1,106-action catalogue in CryXmlB, the 17 reference layouts CIG ships, and a probe showing Windows.Gaming.Input reads all six devices here and composes the game's product GUID exactly - what is doable (tables, backups, diff, swap fix, live button press) and what is not (editing in place), and the community template library (Joystick Diagrams, 84 devices, a trivial SVG format) for the pictures. |
| [keybindings.md](keybindings.md) | Every action Star Citizen can bind on this install (Alpha 4.10: 1,103 in 50 groups) with its default keyboard, mouse, gamepad and joystick input and how it fires - generated by `--keys=md`, to regenerate and diff after a patch. |
| [cargo-fit.md](cargo-fit.md) | Does a load of crates fit a hull: the grid count the DataCore could not give is in the community dump's CargoGrids block (149 hulls, every sum matching), the crates are the install's (the 32 is a long box), a crate keeps its top up, and a first-fit packer that says when a miss is not proof. |
| [salvage.md](salvage.md) | The salvage probe: a hull's scrape yield is not in the DataCore (the 9 mm rule is, the geometry is not), nor in UEX, nor in the logs - and what is: every scraper module's speed, radius and efficiency, each salvage hull's controller, priced, with a full hold of RMC valued as the ceiling it is. |
| [wikelo.md](wikelo.md) | Wikelo's emporium read from the game files: every trade, its requirements, rewards and rank gates, what the guides got wrong, and what the logs add. |
| [events.md](events.md) | Event journals read from the game files: every bar's tiers and rewards, what each contract pays, how the logs name them, and what the totals cannot see. RSI Discovery Month in full. |
| [hauling.md](hauling.md) | Where a hauling contract's cargo goes: what the logs say (the toast's title names one end on 81 of 139 contracts here, the archetype spells the shape and cargo, the objective ids name their end), what only a screenshot of the card says (every leg, the body each pickup is on), and how the Operations command deck puts every open haul on one route without pretending to know a distance. |
| [missions.md](missions.md) | What a contract pays, from the game files: the log's `contractDefinitionId` is a generator contract's inline id (751 of 751 found), rep per contract and per ending (38/38 StarStrings tags and 145/145 ladders agree), blueprints (18 of 18 received predicted, the mod's tag 10), why a generator contract's aUEC is not in the files, why hauling routes still need a text table, and the catalogue behind the Missions page: 4,293 released contracts with contractor, type, title, briefing, systems, CrimeStat, rank gates and the game's difficulty ratings, all 751 logged contracts found. |
| [encounters.md](encounters.md) | The NPC encounters read from the install: 184 wave collections, what each ship's hold is spawned with (17 of 82 cargo manifests carried, all at 1 to 25%; the 65 others carried by nothing the client has), the hold the fill is a share of, and which mission spawns an encounter - named by a Subsumption script for 48, by nothing client-side for 136, so only the folder and the name speak for those. |
| [localization.md](localization.md) | The 0.18 line: the dashboard in other languages, French first - how much text there is, why English is the key, how the page gets its catalogue, the phases, and the part that can break the parser: the game ships a partial French text table (25,395 keys to English's 90,946), and a game set to it writes some of the notifications the parser matches in French. |
| [linux.md](linux.md) | The 0.19 line, partly built: the server and dashboard on Linux under Wine or Proton - what runs, the Linux tarball, install discovery measured against a LUG Helper prefix on Fedora (where the prefix, the game and the launcher's log sit), what is still unmeasured, the clipboard through wl-paste, and screenshot reading on Microsoft's ONNX Runtime with PaddleOCR's models - built, with what the 146 screenshots here read as. Starting and stopping from a terminal or a systemd user unit, with the start-up crash in the server collector, the inotify walk and the slow start on this machine, all measured. Overlay, MFD and joystick stay Windows-only. |
| [releasing.md](releasing.md) | Bump, tag, and what the pipeline does with it. |
| [installer.md](installer.md) | The Windows installer (1.0): per-user MSI built with WiX 5 around the same exe, why it is shaped as it is (HKCU key paths, two GUIDs that never change), and the install, upgrade, downgrade and uninstall it was tested through, leaving the data folder untouched. What it does not do yet: signing, the in-app updater, a running app. |
| [store.md](store.md) | The Microsoft Store package (1.0.1): the same exe as an unsigned MSIX the Store signs, why not the MSI, why file-system virtualization is off (one data folder for every copy) and the `unvirtualizedResources` justification, the identity file, and how to submit. Packing tested; installing not yet. |
| [privacy.md](privacy.md) | The privacy policy, for users and for the Store listing: what the app reads, what it keeps and where, and each of the eight occasions it reaches the internet with who it contacts, what is sent and the switch that controls it. UEX credentials are plain text in the profile, and it says so. |
| [bug-reports.md](bug-reports.md) | What the report Settings saves holds, what it deliberately leaves out, and the one thing it cannot promise. |
| [viewer3d.md](viewer3d.md) | The 0.21 line: ships, vehicles, weapons and armour drawn from the install's own models - how a hull resolves without its encrypted vehicle file (87 hardpoints on the Corsair), the `QWM1` packing and what is left out of it (nine `_damage` nodes, 75,000 triangles), DDS textures uploaded still compressed, the three-colour palette materials and what they approximate, the interior rule per studio, sizes measured on the model (Andromeda 61.6 � 26.5 � 14.3 m), and the frame cost that made the view draw on change only. |
| [suit-calculator.md](suit-calculator.md) | A suit added up from the files: mass, carrying capacity, speed (5.58 m/s run, 7.2 sprint, nothing worn changes it) and G resistance per piece (a heavy set exactly -1, an undersuit +0.9) against the player's five G profiles - which is the game's and which is the one estimate, and the 4.10 PTU agreeing on every figure. |
| [channels.md](channels.md) | LIVE, PTU and the other channels, one at a time: the four searches that find them (drive walk, the launcher's log, the remembered folder, its siblings), per-channel caches under `channels\<name>`, the ten-minute rule for which one the game is on, the switch and what it answered on this machine on 2026-11-04. |

The seven per-tool write-ups that used to sit in `docs/tools/` have been
removed. Everything worth keeping from them was already lifted into the docs
above — the archived combat patterns and StarLogs' classification tree into
[log-format-reference.md](log-format-reference.md), the per-patch event
availability into [findings.md](findings.md), and the design lessons into
[architecture.md](architecture.md). The summary and matrix below are the rest of
the residue. The originals are in git history if they are ever wanted back.

## The one-line summary

Six of the seven tools are built on `<Actor Death>` / `<Vehicle Destruction>`
log events. **Those events are not present in this install's logs** — not in the
current `Game.log`, and not in any of the 144 backups going back to April.
Any of them, run here today, shows an empty dashboard.

The two that still work (SCStats, SCPlay) are the two that never touched combat
data — they read session timestamps, ship usage, and loadout changes, all of
which are still logged in abundance.

## Comparison matrix

| Tool | Stack | Live tail | Reads backups | Depends on kill events | Works on this install |
|---|---|:---:|:---:|:---:|:---:|
| StarLogs | Python 3.8+ / Flask / SSE | ✅ | ✅ | ✅ | ❌ |
| AutoTrackR2 | C# / .NET 4.7.2 / XAML | ✅ | ❌ | ✅ | ❌ |
| SC-Kill-Monitor | Java / JavaFX | ✅ | ❌ | ✅ | ❌ |
| all-slain | Python 3.10+ | ✅ | ✅ | mostly | ⚠️ partial |
| citizenmon | Go | ✅ | ❌ | ✅ | ❌ |
| **SCStats** | desktop binary | ❌ | ✅ | ❌ | ✅ |
| **SCPlay** | Python/tkinter + C#/WinForms | ❌ | ✅ | ❌ | ✅ |

## Where that leaves a new build

The logs are rich — they're just not rich in *combat*. Four months of data
covers ships flown, locations visited, quantum routes, contracts accepted,
party activity, and session timing. See
[log-format-reference.md](log-format-reference.md) for the verified line
formats to build against.
