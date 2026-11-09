# Quantum Wake release history

Older release notes live here so the [main README](../README.md) stays useful to
pilots installing and using Quantum Wake for the first time.

### 0.21.10

**The pages link to each other.** A name on one page now opens the page that knows more about it, where most were plain words. Commodities open their own page from the Ledger, Cargo, trade routes, Mining, the refinery, the radar, recipes and the kiosk readings; the Market's spelling is matched first, so "Laranite (Ore)" or a misread "Quantainium" still lands on the right one. Organisations open their Reputation card, and a Reputation card's count of their contracts opens those contracts. Contracts can be narrowed to one issuer or one contract, and the list says so above itself with the way back to all of them. Sessions open from Servers, the Log page's files, the Logbook, a blueprint's received date and a mission's runs, and Sessions can now be searched by server or by someone you flew with. The people in a session debrief, the party on Now and shared files open their row on Crew. The Reference ship card has Garage, Compare sizes and your own copy on Fleet; the item card opens the Armoury for a gun or a piece of armour, and Stash or Loadout for your own; the mission card links its organisations, places, runs and payouts. Shops and terminals across the Armoury, the crack calculator, salvage, Loot, the Reference price tables, Fleet and the Garage go to the map, and Events, haul plans and flight plans link their contracts to the mission window.

**Compare ship sizes in 3D.** A ship or vehicle open in the 3D view has a Compare sizes button: it stands the ship in a row, and a search box beside the row adds any other ship or vehicle the install can draw, owned or not, as many as you like. Each one has a ✕ to take it out again, and Remove all empties the row to start over; a tick box hides one for a moment without losing it. Each ship is listed with its length, beam and height in metres, measured on the model as drawn with its guns and landing gear, so it can read a little over the brochure figure: the Constellation Andromeda comes out 61.6 × 26.5 × 14.3 m against the 61 × 26 × 14 RSI gives, and the Carrack 126 m. The same search is in Fleet → 3D, so a ship you are thinking of buying can stand beside the ones you have.

**The 3D view's Interior box works on most hulls, and Effects start on.** Interior used to switch only parts whose material name begins `Int_`, which most ships do not use, so on an Avenger or a Cutlass it changed nothing. It now also takes the `_INT` and `internal_` names the studios use, and leaves out the inner canopy pane that drew as a grey sheet over the cockpit. Most cockpits and cabins are not in a hull's own files at all - the game loads them separately - so the box is greyed out, with a note saying so, on a model that has nothing inside to show. Effects (shading, glow and smoothed edges) are now on when the view opens; untick them on a slower machine and the choice is kept.

**More distinct surfaces in the 3D view.** Layered armour and clothing now draw each surface with its own texture where the game's blend map calls for it. Fabric, padding and other materials no longer all share the first layer's texture. The view still approximates the game's materials and does not draw wear and grime.

**Ships, vehicles, weapons and armour in 3D.** A new 3D view opens any ship, ground vehicle, personal weapon or piece of armour from your game install's own models, with its stock loadout fitted: gimbals and the guns on them, turrets, missile racks and their missiles, countermeasure launchers, thrusters, a rifle's magazine. Drag to turn it, scroll to zoom, jump to the front, profile, top or rear, and save a picture. Click a part on the model or in the list beside it to see which slot it sits in and what it carries. Reference → 3D models opens on the makers, Drake, RSI, Anvil and the rest, each with its logo; pick one to see its hulls in the game's own pictures of them in their paint, the one you picked on Fleet or the Garage where you did, or search across them all. A hull opens in that same paint. Armour opens as the whole suit: a helmet, torso, arms or legs brings the rest of its set, in the same colourway where the files have it, with a Full suit box to see the piece alone. The view is a click away wherever a ship or item is: a 3D view button in the Garage and the Armoury opens it with your own fit in place, marked as yours; Fleet and Hangar cards have a 3D button; and the item card that Loot, Loadout, Stash and Items open has one whenever the install holds a model for the item. The cockpit and cabin are hidden unless you tick Interior: the game lights them with the ship's own lamps, which the view does not have.

**Paints.** The paint picker lists every livery the game has for the hull (ten for the Corsair) and recolours the model from that livery's palette. The game paints a hull from a palette of three colours laid over tileable panel textures, and the view draws exactly that, with the panels' normal and gloss maps, image lighting and ambient occlusion. It does not draw the wear, dirt and decal stencils the engine adds, so a hull looks cleaner than it does in game.

**What it reads.** Everything comes from Data.p4k on your machine; nothing is downloaded. The first model you open takes a few seconds: the app indexes the archive's model folders (about 1.5 s here) and lists what it can draw (about 3 s, then kept). Each model is packed once and kept in the app's data folder until the next game patch. It needs a browser with WebGL 2, which current Chrome, Edge and Firefox all have. Textures go to the graphics card still compressed, at most 2048 pixels across.

**Reference → Items is readable again.** The list held every entity the game files type, 26,531 of them, and offered their 153 engine types as the filter, from NOITEM_Player to hair colours. A third of the entries had no name but their class, such as cds_combat_superheavy_legs_01_04_01. The page now lists the named items only, about 8,000 here, under thirteen kinds you would look for: ship weapons, missiles and countermeasures, ship components, mining and salvage, ship paints, personal weapons, weapon attachments, armour, clothing, food and drink, medical and gear, flair, and collectibles. The whole list is still there under "Everything in the files".

**Fleet → Fitted, as last photographed is laid out again.** Each photographed fit had come out as four thin columns side by side. It is a card again: the ship and the date on top, then each slot with the part in it and whether it is stock.

**Item labels show in game again, and contract titles say their rep.** The game reads a loose text table only when `user.cfg` in the game folder sets `g_language = english`. StarStrings shipped that line in a `user.cfg` of its own, so once it was gone, the labels' file sat in the game folder unread: the item marks did not show, and nor did the rep StarStrings used to write on contract titles. Installing the labels now adds that one line when `user.cfg` names no language (it creates the file if there is none), and removing them takes back only that line. The labels also write what a contract pays on its title, the way StarStrings did: `[100 Rep]`, with `[BP]*` when it can award a blueprint. A contract that also pays on a second ladder says both, `[200+60 Rep]`, where StarStrings showed the sum. On this install that is 774 titles, and of the 523 that StarStrings' last table marked, 491 show the same figure and the other 32 show it split. Install the labels again from Settings → Item labels to get both.

**A better-looking 3D view that rests when you do.** A lighting picker (Studio, Hangar, Deep space) and a brightness slider; Studio, the default, lights a hull brightly from every side. With Effects on, edges are smoothed, thruster glows, running lights and screens glow, and painted panels have a clear lacquer that catches the light. High or Ultra textures (2048 or 4096 pixels) for close looks, and a Turntable that turns the model on its own. The view draws only when something changes: on this machine's RTX 5080 a frame with effects costs about 12 ms, and drawing one for every display refresh, as it first did, kept up to 0.8 s of each second busy on the graphics card for a model that was not moving. Left still, it now draws nothing, which leaves the card to the game when both are running.

**Your fleet in 3D, and attachments you can change.** Fleet has a Fleet in 3D button: the ships the page is showing, owned and through its search and filters, stand side by side on one floor, smallest first, each in the paint picked for it and with its weapons on. A list beside the scene ticks ships in and out of the row, and clicking one opens it alone. The first eight are brought in and the rest wait in the list, so a large fleet does not have to load all at once. A personal weapon's view has pickers for its sight, barrel and underbarrel, listing what fits each slot by the game files' sizes and tags. A pick fits the piece onto the gun straight away, and it is the same choice the Armoury shows. Guns also look like guns now: the layers they and armour are painted with had been drawn without their surface textures, which left most ship guns a flat light grey that read as white paint.

**Swap a ship's guns in 3D.** A ship's view has a Loadout list: every gun, mount, missile rack and mining or salvage head, named by where it sits ("manned turret left › weapon left › class 2"), with what fits it from the Garage bench's own lists. Choosing one swaps it on the model. For the ship open in the Garage it is a real Garage swap, so the bench and the build follow; for any other ship it is a try-on. Effects are now off until you turn them on, and the view remembers your choice: they double what a frame costs and the glow is not to everyone's taste. Attachments whose material sits beside their model, such as the KLWE flashlights, no longer come out plain grey.

**LIVE and PTU.** Quantum Wake now knows every channel on the machine - LIVE, PTU, EPTU, Tech Preview - and switches between them from the install label at the top of the page, or Settings → Data sources → Game channel. It reads one at a time and keeps each one's history apart, so a PTU evening never lands in your LIVE totals; switching restarts the app on the other folder. When the game is running on a channel the app is not reading, a notice at the top says so and offers the switch. Each channel keeps its own game-file caches, so going back and forth does not re-read the archive every time, and installing the item labels on PTU no longer loses track of the ones on LIVE. A channel kept in a library folder of your own naming, such as `D:\rsiptu\StarCitizen\PTU`, is found too.

**Suit calculator.** A new Armoury tab adds up a suit from your game files: what it weighs, how much it carries, how fast you move in it and how much G you take. It starts from the loadout the log last saw; swap any helmet, core, arms, legs, undersuit, backpack or gun, strip it all, or copy the loadout back. G resistance is each piece's own figure, shown as a percentage the way the community wiki writes it, and the pieces add up - a full heavy set is exactly -100%, medium -50%, light -25%, an undersuit +90% - which the page sets against the player's worst, usual and best G profiles. The adding up is what the 4.8 patch notes describe, and it checks out against the guides: a Tailwind flight suit is +97.5% and its helmet -2.5%, and guides measure the pair at 95%. How the game blends between those three is not in the files, so the "this suit" column is an estimate and says so. Run speed is 5.58 m/s and sprint 7.2 in 4.10.2, and armour and weight change neither: no armour piece carries a movement multiplier yet. Anything the files leave empty counts as 0, and the page says which.

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

### 0.18.19

**Quantum Wake now reads in French.** Choose Français under Settings → General; English stays the default. Every page is translated, in the browser and in the overlay:

- **Every tab:** Now, Map, Log, Operations, Flight, Economy, Reference, Hangar, every Settings page and Help & FAQ, plus the item card that opens from anywhere.
- **The notes the app writes for you:** what each screenshot reading checked against your logs (readings taken before this update included), the rock calculator's notes, the Garage's warnings, the cargo fit's reasons, when a gun's second fire mode takes over, a recipe's choice of materials, the haul plan's notes and the app's error messages.
- **Outside the browser:** the Cougar MFD pages and their setup window, the tray icon's menu and notices, and the overlay's header. On the frames, the captions under each button stay the cockpit's codes (NAV, CRGO, BRT), because six characters is all a button has and the game's own HUD uses the same English codes.
- **A recap you save** is a French file.

**What stays as the game writes it:** ships, makers, places, commodities, minerals, items, blueprints, careers, terminals, shops, shard IDs, gun kinds and fire modes, keybinding actions, stick names, Loadout's slot names, the handles of the people you flew with, and the text read off a screenshot. Your own names and notes stay as you wrote them. A copied server report stays in English, because it is meant to be pasted where other pilots will read it. `Game.log` is always English whatever language the game is set to, so reading your logs works the same in a French game.

**Item labels say when your game is set to another language.** Star Citizen reads the text file of the language it is set to, and the labels go into the English one, so in a game set to French they would be installed and never shown. Settings → Item labels now says which language the game is set to and when the labels will not show, and warns before you install StarStrings that it sets the game back to English: its own user.cfg says `g_language = english`.

**Six English labels are worded more precisely**, because each old word also meant something else on another page and a translation could only pick one:

- Servers: a stay you ended yourself reads "you left".
- Crew: a member's connection dropping is "Went offline", next to "Came online".
- Log: how long a session ran is "Duration", since "Length" is a ship's length on Hangar.
- Armoury: where a gun's damage starts to fall is "Drop-off".
- Controls: the activation mode that fires while a button is held is "hold down", since "hold" is a ship's cargo hold elsewhere.
- About: the line naming your game install is "Game install".

**A small fix on the Overlay page.** Ticking a page or card for the widget now says "saved" under the list. It used to print a garbled line instead, because the save was handed the click itself where it expected a message.

### 0.18.8

**The README is now a player guide.** It starts with what Quantum Wake does,
three screenshots and a short first-run path. Optional features, privacy,
limits and contributor commands now sit where they are useful instead of
competing with setup. [Older release notes](docs/release-history.md) remain
available in full.

### 0.18.7

**Quantum Wake in French — the start of it.** Settings → General has a new **Language · Langue** card. Choose Français and the navigation, the search box, the command palette, the notices along the top (old prices, a new patch, a new version), the Now page, the whole of Settings → General and every Operations page — the command deck, Routes, Contracts, Checklists, Blueprints, Wikelo and Events — come up in French, in the browser and in the overlay alike — the live feed, status lines, price feeds, the table of what your logs still carry, every cargo run's evidence and every event tier included, with dates written the French way. Flight plans saved from Routes, and builds and shopping runs started from Operations, get French titles; the ones you saved before keep theirs. The rest of the app is still English for now, and the card says exactly what is covered so far; more pages follow through the 0.18 updates. Names from the game — ships, places, commodities, contract titles, Wikelo's trades — stay as the game writes them, so they still match your mobiGlas and the price sites.

**Nothing changes unless you choose it.** English is the default and stays the default, even on a French Windows: the app never switches language on its own, only when you pick one on that card.

**Playing Star Citizen itself in French?** The game has a partial French translation of its own (`g_language = french_(france)` in `user.cfg`), and there are community ones too. If you play with either, a `Game.log` from one of your sessions would help a lot — ideally one where you accepted a contract and joined a party. Some of what Quantum Wake reads is text the game translates: the game's French table translates party joins and leaves, so the Now page's party list most likely misses them, and there is no French log here to check that or the rest against. Open an issue on GitHub and attach one.

**You can now see when the app is still reading.** On a first start, after a patch or during a full re-read from Settings, the pages built from your logs or game files used to sit empty with nothing saying why, unless you happened to be looking at Settings. A small chip in the top-right corner now says what the app is busy with — reading the game files, reading your logs (with a count), or downloading the community dataset or UEX prices — on every page and in the overlay. Click it for the details and a link to where each one is shown. The page you are on also says when it is waiting for one of them, and the log pages fill themselves in when the read finishes, without a reload. The first-run window no longer says your history is ready before the logs have even started to be read.

**Loadout now fits the overlay.** The desktop’s character screen needs room; in a small in-game widget it left sparse side cards and nearly invisible empty states. The overlay now uses a compact Current kit manifest: worn and carried counts first, then one clear row per observed attachment. If the game has not written any equipment events yet, it says so plainly.

**The overlay is now a clearer flight instrument.** Glance mode keeps the active objective, a tracked event’s progress rail, and current status in one short stack. Its header has one named layout control instead of a row of competing controls, and every pin, layout, view, glance, or fullscreen change confirms itself on the overlay — including once it is click-through. High visibility is available under Settings → Overlay for brighter text and markers over the game, and Tiny density no longer drops below a readable type size.

### 0.17.9

**Event points no longer go missing when the game is relaunched with the app open.** The session you are playing is kept under one shared name until the game moves its log into the backups, and nothing read that backup until the app was next started. Launch the game twice in one sitting with the app running and the earlier session was overwritten. On one install that was a whole evening of Discovery Month: 2,123 points the in-game journal had and the Events page did not, and a tier already paid out still shown as not reached. The app now reads the backup as soon as the game starts a new log. A session already lost this way comes back the next time you start the app.

**Contracts that lose a step and still complete now count.** A patrol whose optional ace pilot escapes, or one whose "Return to Location" step is withdrawn, still ends complete and pays out, but the app filed it as lost. On one install that was two Discovery Month patrols worth 667 points, plus an older bounty and an escort. Your history is re-read once when the app starts; nothing needs replaying.

### 0.17.8

**The MFD displays no longer get lost when Windows renumbers your screens.** The MFDs remembered their monitor only by the number Windows gives it, and Windows changes those numbers after a driver update, a sleep or a replugged cable. When it did, the frames and their black backdrop could wake up on your main screen, covering the taskbar and the tray icon you needed to fix it. Now:

- **The MFDs find their screen by its size** when its number changes, so a renumbering puts them back behind the frames and the app tells you it happened. If no screen of that size is connected, they stay off and say so instead of guessing.
- **The black backdrop never covers your main monitor**, so the taskbar and tray stay reachable whatever happens.
- **Three ways into MFD setup:** the tray as before, **Ctrl+Alt+M** from anywhere (even with the overlay hidden), and a new **Open MFD setup** button under Settings → Overlay or in the command palette.

If your layout was saved before this version, open MFD setup and press **Save layout** once. That records the screen's size, and from then on it is found even when Windows renumbers. Until you do, the app warns you at start if your MFDs are sitting on your main screen.

### 0.17.7

**Discovery Month contracts you finish now count.** Most of the event's contracts come in variants, and the game logs a variant under its own name: `Iasi_DefendShip_Easy_2` rather than `Iasi_DefendShip_Easy`. The Events page only knew the parent names, so every Support Ship, Eliminate Known Outlaw and the like finished so far was listed as unrecognised and added nothing. A variant now counts as the contract it varies, on the Events page, the Now card, the overlay, the MFD, tier toasts and the points chip on contract rows. Your history is recounted when the app starts; nothing needs replaying.

**Event progress on the Cougar MFD.** Operations → Event progress follows the
campaign you track on the dashboard (or the event contract in your journal),
then shows your logbook-total floor, the next tier, the quickest recorded path
there, category bars and the contract currently in your journal. Event choice,
rewards and route planning remain on the dashboard, where there is room to use
them.

**Discovery Month still needs testers.** Mining and salvage orders and the scavenger box haven't turned up in a played log yet, so they're the part of the Events page nobody has checked. If the Events page and your in-game journal disagree, a problem report from Settings is the most useful thing you can send.

### 0.17.5

**0.17 is the events release, and it needs testers.** RSI Discovery Month started the day this shipped, so the Events page's contract names and point values come straight from the game files and haven't yet been checked against a played session. If the Events page and your in-game journal disagree, a problem report from Settings is the most useful thing you can send.

**A toast when you reach a tier.** Cross a tier on any event bar and the app says so, in the overlay too, with what the tier gives: "Defense tier 1 reached - Scorpius Echo Livery and Sovereign IP-20 Power Plant". The journal moves the bar and says nothing.

**A warning when your in-game text file is older than the game.** A text mod's file, or Quantum Wake's item labels, replaces the game's own text table, and nothing updates it when the game patches. 4.10.2 added 521 strings, every Discovery Month contract title among them, and a file written before the patch has none of them. The app now notices and says what fixes it. If the file is your item labels, reinstalling now adds the missing strings in the game's own words, even on top of an older StarStrings.

**A clearer campaign board.** Event plans now lead with a luminous next-tier bar, reward milestones, and compact Combat, Mining and Hauling lanes.

**Events now help you choose the next job.** Set a Combat, Mining or Hauling preference, see the points still needed for the next tier, and jump from the plan to contracts, routes or the map. Event state, recent evidence and detailed tiers remain available without turning the page into a wall of tables.

**Current status is back in the overlay, and the glance view is no longer empty.** When the location, ship and session cards were merged into the one Current status card, the overlay stopped recognising it and switched it off. So the glance view, which is that card and nothing else, came up blank. Your overlay layouts now show it again.

**Page headings' diamond sits beside the title** instead of below it and over the first letter.

**RSI Discovery Month, in points.** The event journal draws each bar as a percentage and never says what a contract is worth. The new **Events** page (Operations → Events) reads both from your installed game: what each tier of Your total, Transport, Collection and Defense costs, what it awards, and what each of the event's 31 contracts pays. It then counts the ones your logs show you finishing, and says which contracts reach the next tier in the fewest runs.

- **An Event card on the Now page**, and in the overlay, for the event you're playing: your points against the next tier, and what the contract in your journal adds when you finish it. An open event contract also leads the focus strip: "+417 pts to Your total, Defense".
- **Track an event on the Now page.** **Track on Now** on the Events page keeps that event on the Now card and the overlay whatever you're playing, including one you haven't started. In the overlay's glance view it's a single line above Current status: your points and every bar against its next tier.
- **See what each tier gives.** Click a tier and it opens into the items it awards. Each one shows its picture (every livery uses the game's own render), what it is (a size 2 power plant, a flight blade, a tractor beam) and its UEX price if anything sells it. Where the game's wording fits more than one item, as with the Zeus Mk II blades, every match is shown, marked "One of".
- **Contract rows carry a pts chip** for any contract that pays into an event.
- **Older journals are there too.** Orison Relief, Alliance Aid and Return of XenoThreat are still in the game files, so your Orison Relief history is added up as well.
- **Every total is a floor.** A contract counts when the game put up an objective marker for it and later ended it as complete, so the in-game journal stays the final word. The page says what it can't see.
- The game data is read again once after updating, which takes a few seconds on the first start.

**Things that should have gone away now do.** Nine parts of the dashboard stayed on screen after the app had hidden them. You could see it as an empty strip at the top of the Now page, a blank gap beside your ship's name when it has no maker logo, a cash line with nothing in it, the earnings-goal form still open under a goal you had already set, the rock-crack calculator next to its own "not ready yet" note, and an empty controls panel. Each one now disappears when it should.

**Find any workspace or overlay layout with Ctrl+K.** The new Command palette jumps directly to a page, selects a Flight, Mining or Combat overlay layout, or brings the overlay back without adding another permanent control.

**Switch overlay layouts for what you are flying.** Flight, Mining and Combat now choose a focused set of overlay pages and Current-status readings. Pick one from Settings or the new compact ▦ menu in the overlay header; Trading, Minimal and Full remain available there too.

### 0.16.24

**The in-game overlay can now become a glance view.** Use the new header control or <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>C</kbd> to collapse it to the current status card; use it again to restore the resizable widget. The choice survives a restart.

**MFD setup now protects drafts.** The setup window marks every placement, display, screen or button-map edit as unsaved, keeps that warning visible until **Save layout**, and asks before closing would discard a live preview.

### 0.16.21

**Mining screenshots that read, and a refinery that tells you when it's done.** Built from one evening's Golem run at Daymar and MIC-L5.

- **Scans read at last.** The scan panel's reader had been written from a wiki picture of an older patch, and filed every real scan as nothing. It now reads today's panel — mass, instability, SCU (a gem cluster's in thousandths), every share and the qualities it can see — by reading the panel a second time at several sizes and keeping only what two of them agree on. A figure none of them read is left blank, and when the shares don't add up to 100 the Log says one of them is misread.

- **The refinery terminal is read.** A screenshot of a station's refinery shows on the Log with the station, method, what each lot gives back, the cost, the time and the station's load, and its balance is checked like any other.

- **Refinery orders, with a timer and the game's word.** *Mining → Haul & refinery* lists every order you photographed: counting down on the terminal's own clock, then **ready** — in green when the game's log said so, in amber when only the clock has run out. Choose how you're told: the game's toast, then the timer a minute later if the game hasn't said (the default); the game only; the timer only; or nothing. Screenshot the PROCESSING screen for a sure start; a quote counts down "if you confirmed it".

- **Where your ore is waiting.** The game says once that an order is done and never logs you picking it up, so the app now keeps track: *Haul & refinery* opens with "Waiting for you at MIC-L5…" and lists what is waiting, what is still refining and what you have collected. Press **Collected** when you pick an order up (and **Not collected** if you pressed it by mistake). A new **Refinery** card on the Now page — and in the overlay — shows the same thing at a glance, so the station with your ore is never more than one look away.

- **Your refinery's actual yields.** Each quote you photograph adds what went in and what came back, per ore and quality — the figure no game file or feed publishes. Pyrometric Chromalysis at MIC-L5 gave back 45% of silicon and agricium on the first one.

- **The refining-methods table had cost upside down.** It said three pips of cost was the dearest; the refinery terminal itself calls a three "LOW COST". It now sorts and explains cost the right way round.

- **Refinery jobs now read as a queue.** The Mining log brings jobs waiting at a refinery into one compact status panel, with ready collections in amber and remaining processing work in cyan. Each haul keeps its station, due estimate, refining method and value ceiling close at hand.

- Existing logs are read again once after updating, so the refinery order that finished before you updated is there too.

**Also in this release**

- **One button now has one direct action.** Reassigning a joystick button moves its old direct action out of the way, so the profile never leaves the game to choose between two competing assignments. Button chords remain available for deliberate modifier combinations.

### 0.16.18

- **The top bar says how many logs it read.** The status beside the live dot was cut to "LIVE · 60 LO…" on every install with more than nine logs; it now reads in full, and hovering it says so in words.

### 0.16.17

- **The star map's first search opens its place.** On a first visit the map starts zoomed right out, and searching for a place could stop the map mid-glide with an error and leave the place panel shut. It now glides in and opens every time.

### 0.16.16

**Pages that fit, and say things once.** A round of fixes to text that overlapped, was cut off or showed the game's own markup, found by filming every page at 125% Windows scaling.

- **Session highlights read cleanly.** A contract in *Latest highlights* no longer shows StarStrings' `<EM3>`/`<EM4>` tags, and its title no longer prints over the contract name beside it.

- **Wikelo's trades read as prose.** Their descriptions come straight from the game files, and printed the game's `<EM4>` emphasis tags and `\n` line breaks as text; they now read as the game shows them.

- **The favourite star on Servers and Now is a plain star**, dim until you pick it and amber after, instead of a boxed button that turned green around an amber star.

- **A session flown in several ships lists them one per line**, under "Ships", instead of one long run of text in a narrow tile.

- **Routes, Ships, Parts and Labels fit their panels.** On a scaled or narrower window their columns now wrap instead of running off the edge, so the evidence on a cargo run, a ship's claim time and where a part is cheapest stay in view.

- **Servers keeps your note visible.** The "+N more" places badge no longer covers the note box.

- **Search no longer offers engine class names** such as `Controller_Flight_MISC_Freelancer`, and a long result wraps rather than running into the next column.

- **Log and Controls headings sit above their subtitles** rather than on top of them.

- **An empty shopping list says so once**, not in two stacked messages.

- **A truer Fleet picture on the project page.** The README's Fleet screenshot showed the 315p as a flat silhouette — the game files hold no picture of any 300-series ship — so it now shows ten hulls the game does picture.

### 0.16.15

**Keep dense workspaces clear at any width.** Map controls and place panels now fold into a phone-width layout; Routes, Contracts, and Market retain their row identity while scrolling. Map, Routes, Shopping, Contracts, Market, and Logbook also distinguish loading, an empty result, and an unavailable local source.

- **The star map opens in a moment.** It used to sit on *Loading known places…* for 25 to 35 seconds on a well-travelled install, because every shop, fuel and clinic listing re-read your whole session history to find its place. That history is now read once and kept until a new session lands or the wipe line moves, and the map draws in well under a second.

- **The map remembers the system you were looking at.** A system you picked is where it reopens, instead of always jumping back to the one you are in. The line under the toolbar now names the system on screen; it used to keep the previous one's name after switching.

- **Keep the current operation with you.** Selecting a map place, planning ship, pinned shopping list, live contract, or new flight plan now opens a compact “In focus” rail. It remains available as you move through the app, returns directly to the relevant workspace, and clears when the work is no longer useful.

- **Choose a destination from one map readout.** Selecting a place now groups its UEX-listed services, flight-plan state and next route action together. When refuel, clinic or repair is not listed there, the same panel can find the closest known support from your last recorded location; market facts, game facilities and personal notes stay available without crowding that decision.

- **Choose personal gear by role, then compare the details.** Armoury now opens with a compact equipment board for firearms, protection and field kit. It keeps the number of families, pieces and available price data in view, puts the reading-method explanation away until needed, and turns the five detailed categories into a clearer workspace selector.

- **A cleaner Armoury heading.** Its title now follows the equipment eyebrow directly, without a stray marker beside the wordmark.

- **Kicker-led headings now use one cue.** Flight, Operations, Logbook, Fleet, Economy, Reference, Mining and Help drop the repeated heading marker when their own kicker or workspace icon already establishes the title.

- **Global notices give workspaces more room.** A patch-wipe prompt stays expanded on Now and Settings, where its context matters; elsewhere it becomes a compact, still-actionable status line.

- **Settings now opens as a control room.** Sharing and update choices sit together in a compact start deck, while the history-wipe boundary has its own distinct panel before the data and maintenance controls below.

- **Settings detail now stays out of the way until you need it.** Data sources, account care, and log maintenance each have a concise summary rail that opens its controls and explanations on demand.

- **Start each operation from one place.** The Operations menu now opens a command deck for supplies, routes, contracts, departure checks, blueprints, and Wikelo trades, with your active shopping and live contract state shown at a glance.

- **Prepare the full flight, then choose the cargo run.** Routes now opens as a flight plan: choose a ship, see the active plan's next stop and progress, find nearby refuel support, and keep cargo filters out of the way until you need them.

- **Review the run without wading through the timeline.** The Logbook now leads with its newest recorded event, the selected range's sessions, trades and discoveries, and its recorded trade net before the detailed timeline.

- **Start a session review with the flight that matters.** Play history now opens as a session-debrief workspace: the latest flight's ship, duration, progress and health lead into the retained full history and detailed route, activity and economy evidence.

A dashboard redesign. Nothing about how logs are read has changed, so your history, fleet and receipts carry over as they are. Pages have moved, though, so expect a moment of looking for things.

- **Navigation is organised around what you are doing.** Operations holds shopping, routes, contracts and planning tools; Flight holds journeys and location history; Hangar holds ships and equipment. About now lives with Settings, leaving Now, Map and Log one click away.

- **The dashboard has a calmer shared visual language.** Page headers, filters, tables, status and empty results use matching spacing, hierarchy and visual cues, so dense screens are easier to scan without hiding information.

- **Put the current situation in one place.** The Now page leads with a configurable Current status card: location, ship, session, pilot, session health and now where you will wake up.

- **The star map is easier to read.** A muted palette, with search and location controls leading the toolbar and display settings under View options. Places use purpose-drawn icons — docks, landing pads, headframes, research flasks, cargo stacks, jump gates, and stronger tower and orbital-hub silhouettes for cities and stations — that stay distinct in a dense cluster and recognisable at ordinary zoom. Filled and outlined markers still show where you have been.

- **Find nearby support from the cockpit.** The MFD Flight menu lists the closest refuel, clinic and refinery in the current system, using the same facility evidence as the star map, and says that distances are based on bodies rather than travel time.

- **Make the Fleet useful at a glance.** The roster leads the page with clearer ship cards, recent-flight signals and a dedicated action row; the historical fleet trace is there when you want it.

- **Bring the Garage back to the ship.** Clearly labelled fleet and library pickers, a compact hull console and a collapsible explanation make the fitted layout the first thing you see.

- **Read the evidence without wading through it.** Log keeps the newest capture open, compacts earlier screenshots into an expandable timeline, and folds capture and saved-point controls away until needed.

- **Find reference data faster.** Ships and parts keep their filters, live match count and table together, with names anchored while you inspect prices and specifications.

- **Directions point to the new menus.** Help and the Wikelo page now send you to Operations for blueprints and shopping lists, rather than the old Jobs page.

### 0.15.34

- **Select the closest facility on the map.** Choose a facility or service,
  then select closest from your last known location. Body-based estimates are
  labelled approximate, tied places remain a choice, and missing coordinates
  are explained. Selecting a result centres the map and opens its place card.

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
