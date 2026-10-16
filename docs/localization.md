# Languages

The 0.18 line. Quantum Wake has been English-only since the first build; this is
the plan for making it readable in other languages, starting with French, and
the record of what each step actually covers.

Measured on `dev0180` at ee9e769 (0.17.7), against this install: SC LIVE,
`g_language = english`, 248 files in `logbackups`.

## How much text there is

| Where | Size | What it holds |
|---|---|---|
| `web/index.html` | 4,353 lines; 875 headings, buttons, labels and paragraphs; 201 `title` tooltips | Every page's fixed copy. The Help page alone is 356 lines of prose. |
| `web/app.js` | 27,587 lines; 1,815 template literals | Everything rendered from data: table headers, status lines, empty states, toasts. |
| MFD pages | `mfd-core.js` 1,073, `mfd.js` 717, `mfd-setup.js` 377 lines | Short labels, cramped for space - French runs about a fifth longer than English. |
| Server | ~142 English sentences in `Quantumwake.Server`, `Quantumwake.Data` and `Core` (a grep for four-word capitalised strings, so an estimate) | Explanations, "why is this empty" notes, problem lines. |
| Tray and overlay shell | ~60 strings in `Quantumwake.Overlay` | Menu items, the MFD window, first-run prompts. |

That is far more than one release, so it goes out in phases, and the language
picker says how far a language has got rather than implying it is done.

## What is deliberately not translated

- **Game names.** Ships, places, commodities and items are shown as the game's
  own text table writes them. That is what the pilot sees in the mobiGlas and
  what UEX and the community dataset are keyed on; a translated "Laranite"
  matches nothing. A pilot who plays the game itself in French is a different
  case - see [The game in French](#the-game-in-french).
- **Evidence.** Log excerpts, raw tags, class names and diagnostic output are
  quoted verbatim. They are there so a reader can find the line in `Game.log`,
  which is in English whatever the page is in.
- **Other people's text.** UEX notes, community dataset descriptions and
  release notes from GitHub arrive in English and stay so.
- **Numbers and dates.** All 153 `toLocale*String` calls in `app.js` already pass
  the browser's locale (`[]` or nothing), so a French Windows already shows
  `1 234 567` and `11 oct. 2026`. That follows regional settings, not the page
  language, which is right: a Québécois reader on an English page still wants
  their own date order. **Dates and times are the exception on a translated
  page** (0.18.2): a month name and an AM/PM are words, and "Oct 12" inside a
  French sentence is English on the page. The shared helpers - `dateOf`,
  `dayOf`, `timeOf`, `shortTimeOf` and the four other `[]` calls - pass
  `QW_DATE_LOCALES`, which is `[]` on an English page and the page language
  otherwise. Digit grouping still follows the region.

## The design

**English strings are their own keys.** `tx('Last wipe')` looks the English up
in the active catalogue and falls back to it. Three reasons, in order:

1. The 2,683 tests 0.17.7 shipped with assert English text. With English as the key the page is
   byte-identical in English and not one of them changes.
2. An untranslated string falls back to the English, so a partial language
   is a readable page rather than a page of key names.
3. Nobody has to invent and maintain a few thousand key names.

The cost is that editing the English orphans its translation, and the French
would silently revert to English. `LocaleCatalogueTests` closes that: every
catalogue entry must still exist as English somewhere - as a `tx()` literal in
the scripts or as text in the markup - or the build goes red naming it.

**Interpolation and plurals.** `tx('{have} of {total} items in hand', { have,
total })` substitutes named values after lookup, so a translation can move
them. `txn(count, '{count} log', '{count} logs')` picks the form by the page
language's own rule: French treats 0 as singular ("0 journal"), English does
not ("0 logs"), so the rule cannot be `count === 1`. The rules are written out
in `i18n.js` rather than asked of `Intl.PluralRules`, because the test engine
has no `Intl`. A plural entry in the catalogue is keyed on the English plural
and holds `{ "one": …, "other": … }`; an untranslated one keeps English's rule.

The names are `tx` and `txn`, not `t`: `app.js` declares a local `t` 52 times,
and each would quietly shadow the global inside its function.

**Markup is translated once, before `app.js` runs.** A walk over the document
replaces any element whose whole content - inline tags included - is a
catalogue key, so a paragraph with a `<code>` in it is one sentence for the
translator rather than three fragments; otherwise it translates text nodes and
the `title`, `placeholder`, `aria-label` and `alt` attributes one at a time. It
has to run first: `app.js` holds element references from its top level, and an
element replaced after that is one the script can no longer reach. The tests
check each entry carries the same tags and `{placeholders}` as its English, so
a translation cannot drop the `<code>` or the `{count}`.

**The server owns the choice.** It is kept in `language.json` in the data
folder, not in the page's `localStorage`, because the overlay's WebView2 keeps
its own storage: a choice made in the browser would leave the overlay in
English. The page asks for it with `<script src="/api/i18n.js">` in `<head>`,
which the server answers with the language and its catalogue inline. It blocks
on purpose - the first paint is already French, and an English page loads
nothing extra.

**Changing language reloads the page.** Dozens of renders have already written
text into the page; re-running all of them in place is a bug farm for a thing
done once.

**English is the default, for good.** The Windows language is never read: a
French Windows user who updates gets the app they had, and another language is
something the pilot picks. An earlier version of this plan had French follow
Windows once it had no English left; Nicolas ruled that out on 2026-10-13, so
there is no such phase.

## The game in French

This is the part that can break things rather than merely leave them English.
The parser matches some notification text, and notifications come out of the
game's text table - so a game running a translated table writes them in that
language, and the parser stops seeing them:

| What it reads | Matched on | Key in `global.ini` | Seen in this install's logs |
|---|---|---|---|
| Contract titles (`SessionBuilder.cs`) | `Contract Accepted:` / `Contract Complete:` | `mobiGlas_ui_MissionEvent_Activated` / `_Complete` | 536 / 296 |
| Party joins, leaves, leader, disband (`Party.cs`) | `New Member Joined`, `Member Left`, `New Party Leader`, `Party …` | `notification_member_joined_Title` and siblings | 100 / 124 / 18 / 30 |
| Ship channel (`ShipChannel.cs`) | `… the channel '…'` | | 372 |

**The game ships French itself, partly.** `Data.p4k` carries a `global.ini`
for ten languages beside English - French, German, Italian, Spanish (Spain and
Latin America), Brazilian Portuguese, Japanese, Korean, Chinese simplified and
traditional - and `g_language` in `user.cfg` picks one (this install:
`english`). The French table is far from whole: 25,395 keys against English's
90,946, with 66,752 English keys it has no line for. What it holds for the
lines above:

| Key | English | French |
|---|---|---|
| `mobiGlas_ui_MissionEvent_Activated` | `Contract Accepted:  %s` | absent |
| `mobiGlas_ui_MissionEvent_Complete` | `Contract Complete: %s` | absent |
| `notification_member_joined_Title` | `New Member Joined` | `Nouveau membre` |
| `notification_member_left_Title` | `Member Left` | `Un membre a quitté le groupe` |
| `notification_leader_changed_Title` | `New Party Leader` | `Nouveau leader du groupe` |
| `notification_group_disbanded_Title` | `Party Disbanded` | `Dissolution du groupe` |
| `notification_local_player_joined_Title` | `You have joined` | `Vous avez rejoint` |
| `mobiGlas_ui_notification_Party_Member_Joined` | `%S has joined the party.` | absent |
| `mobiGlas_ui_notification_Party_Member_Left` | `%S has left the party.` | absent |

So a French party notification is likely a French title over a body the table
does not have, and the ship-channel wording (`You have joined channel '…'`) is
not in any table at all - it is written by code, and probably stays English
whatever the language. Ship and commodity names were the same in both tables
in every one checked (`Aegis Avenger Titan`, `Laranite`).

**Unverified here**: this install is set to English and has never written a
French log. What the game puts in a line whose key the French table lacks - the
English, the key, or nothing - is the open question, and the contract titles
hang on it. Community translations exist as well; they install a loose
`global.ini` in their own language folder, which the game reads in place of
the one in `Data.p4k`.

The fix, once a French log exists to prove it against: read `g_language`, load
the table the game actually uses - the loose file when there is one, else that
language's table in `Data.p4k`, which the app already reads - fall back to
English key by key as the game does, and match on the text behind the key
rather than on English. Three neighbours follow the same thread:

- `GameNames` reads names from `english\global.ini` in `Data.p4k`. For the
  names checked the French table agrees, but wherever it translates a name a
  French player would see one thing in game and another here.
- The text overlay installs into `data\localization\english\global.ini`. With
  `g_language` set to anything else the game never reads it, and the page says
  it is installed.
- The screenshot readers match English words. A French mobiGlas reads as
  nothing.

**Needed before any of it: a `Game.log` from a game set to French** - by
`g_language = french_(france)` or by a community translation, ideally with a
contract accepted and a party joined. That is a tester's afternoon, and it is
the first thing to ask for.

## Phases

1. **0.18.0 - the machinery and a first slice of French.** `tx()`/`txn()`, the
   markup walk, the server's choice and `/api/i18n.js`, the picker under
   Settings → General, the catalogue tests. French covers the navigation,
   search, command palette, the focus rail and the fixed text of Settings →
   General; that page's live status lines are still English.
2. **The pages, one area at a time** - Now, then Operations, Flight, Economy,
   Reference, Hangar, the rest of Settings, Help last because it is the
   longest. Each area's WebTests gain a run in French so a regression shows as
   English in a French test rather than a screenshot nobody took.
3. **Server sentences.** The same catalogue, read in C#, so there is one file
   per language and one test guarding it.
4. **MFD pages and the tray app.** The tray reads `language.json` directly; the
   MFD labels need a width check, since a Cougar's screen does not stretch.
5. **Playing the game in French.** Everything under
   [The game in French](#the-game-in-french), starting from a tester's log.

## Adding a language

1. Copy `web/locales/fr.json` to `web/locales/<code>.json` and translate the
   values. Keys are the English and are never edited.
2. Add the code and its own name for itself to `LanguageStore.Available`.
3. `dotnet test` - the catalogue tests check the new file like the French one.

## Built

### 0.18.0

Phase 1 as described above: 188 French entries in `web/locales/fr.json`.
What French covers is listed beside it in the picker, so the page never
claims more than it does.

- **Keys come from the browser, not by hand.** A markup entry has to equal
  what the browser serialises - `&mdash;` comes back as the character,
  `&nbsp;` stays an entity - so the 160 markup keys were collected by running
  the same walk in headless Chrome over the real page, not retyped.
- **Checked rendered**, against a LogSim install with a second server on a
  copied data folder set to French: navigation, search, the Settings prose and
  the command palette all French; the update and wipe banners and Settings'
  status lines English, as the picker says.
- **One label had to change for width.** "En direct" for *Now* wrapped onto two
  lines in the tab strip; it is "Direct". The MFD pages will have this
  problem worse.
- **The Help page's Expand/Collapse button compared its own label to
  English** to decide what it did - the only place in `app.js` that reads its
  text back. It compares against `tx()` now, so translating Help cannot
  break it.

### 0.18.2

A second pass over what 0.18.1 claimed, because the claim was not quite true:
Settings → General's live half and the notices along the top were still
English. Found by rendering the French page in headless Chrome and listing
every visible text node and attribute the catalogue did not produce - on
Settings, and a banner and the footer in the shell - rather than by reading the
page. 246 entries added, 436 in all.

- **Server English, translated on the page.** The signal table's names,
  groups and notes and the UEX feeds' titles, descriptions and sizes come from
  the server. They go through `tx()` where the page draws them, keyed on the
  server's own words, so phase 3 has less to do and there is still one
  catalogue. The orphan test now also reads the C# string literals under
  `src/` (with a sentence split across `+` joined back up), so an edit to one
  of those strings still names the entry it orphans.
- **`ago()` and `dayUtc()` translate at the source**, so "3d ago" and
  "Oct 15, 2026" are French on every page that uses them, translated or not.
- **The clipboard diagnostic summary stays English.** It is pasted into a
  support thread and read against the code, the same reason log lines are
  quoted verbatim.
- **French spacing** matches the 0.18.0 entries: a no-break space before `:`,
  a narrow one before `;` and `?` and inside guillemets.
- `SettingsFrenchTests` renders the signal rows, the feeds, the wipe line and
  the date helpers in French.

### 0.18.3

The Routes page, after Nicolas found "Find a run worth taking" and "Cargo
opportunities" still English. The visible probe counted 68 English strings on
the page with a fighter selected; the key probe, which walks hidden parts too,
found 89 lookups the catalogue could not answer. Rendered again with a 288 SCU
hull and real UEX prices, what is left is commodity, terminal and ship names.
218 entries added, 654 in all.

- **Evidence marks are shared**, so `GAME LOG`, `COMMUNITY DATA` and the rest
  are now French on every page that shows them. The mark's CSS class is still
  built from the English label, so its colour does not depend on the language.
- **Server words translated for display, not for logic.** Security
  (`monitored`, `lawless`, `unknown`), what capped a run (`capital`, `stock`,
  `demand`, `hold`, `per SCU`) and capacity (`enough`, `limited`) are read in
  French; the classes and comparisons keep the server's word.
- **Saved plans are data.** A plan saved from Routes now gets a French title
  and stop notes, but one saved earlier keeps the English it was written in -
  "Hauling run · 6 contracts" on the active-plan card is the pilot's own plan,
  not page text.
- `tx()` trims its key, so a label with a trailing space (`'Space: '`) loses
  the space in translation; it is `${tx('Space:')} ` instead.
- `RoutesFrenchTests` runs `RouteReliabilityTests`' route in French.

### 0.18.4

The Now page - the page every launch lands on. Rendered against this install
with a live frame replayed into the page (40 feed entries, a party member, a
tracked plan, Discovery Month): what is left in English is game text -
contract titles, the event's own track names, a ship's community-data role -
the pilot's saved plan title, and the screenshot card's per-check lines.
279 entries added, 933 in all.

- **The live feed is translated by shape.** Its sentences are written by the
  parser, in English, and kept in every stored session; making the parser
  speak French would mean re-reading every log. They come in a fixed set of
  shapes - `Boarded {ship}`, `Paid {amount} aUEC`, `Quantum to {place}` - so
  `FEED_SENTENCES` in `app.js` matches the shape and translates it, keeping
  the names. Longer shapes are listed first (`Bought 12 SCU` before
  `Bought {item}`), and a sentence that matches nothing is shown as written,
  so a parser that rewords a line costs a translation, not the line.
- **French has no article before a name it cannot elide.** "Quitté le ANVL
  Hornet" and "vous avez sorti le Anvil…" read as errors; the ship sentences
  became "Débarqué : {ship}" and "votre {ship}" instead.
- **Routes read the Now page's text back.** "From here" skipped the location
  when it started with "In menus" - in French it never did, so a pilot in the
  menus would have sent "Dans les menus" as an origin. It compares against
  `tx('In menus')` now; a search for other English read-backs found none.
- **The status toggles' labels moved into a `<span>`.** The browser
  serialises `checked` as `checked=""`, so a key holding the `<input>` could
  not match both the page and the orphan test.
- **Screenshot summaries are half done.** The fixed ones ("a mobiGlas screen
  this app cannot read yet") translate; the ones built around a count or a
  name, and the per-check lines, wait for the server phase.
- `NowFrenchTests` covers the feed shapes, a sentence it was never taught,
  the kind column, status and party, and the menus read-back.

### 0.18.5

The rest of Operations: the command deck, Contracts, Checklists, Blueprints,
Wikelo and Events, so the whole group is French. The key probe over the six
pages found 126 lookups the catalogue could not answer before the change;
rendered again against this install with a shopping list, a build and a
checklist seeded into the copied data folder, what is left in English is
contract titles and types, issuers, Wikelo's trade names and blurbs, the
event's own bar names ("Your total", "Defense") and the pilot's own list
titles. 328 entries added, 1,261 in all.

- **Shared helpers translate at the source.** The period picker, the "Hide
  summary" toggle, the bar charts' empty line and the "why?" panel under a
  summary tile are used across the app, so they are French on every page
  that has them. The rule the server writes into the "why?" panel is still
  English: that is a server sentence, phase 3.
- **Difficulty is page text, not game text.** `ContractName` normalises the
  mission id's `VeryHard` to `Very Hard` from a fixed set of five, so the
  column and the live contract card show "Très difficile". The C# literals
  anchor those entries for the orphan test.
- **Builds and shopping runs are data once saved**, like Routes' plans: a
  build started from Blueprints (or from Reference → Crafting) is titled
  "Fabriquer …", and one started earlier keeps "Craft …".
- **French agreement versus a count in a sentence.** "3 failed" as "3
  échoués" would be "1 échoués" for one, and these are not plural keys, so
  they are worded around the count instead: "échecs : 3", "livrés : 2".
- **One label shortened for width.** The shopping list's place picker clipped
  "N'importe où — pas d'arrêt particulier"; it is "Partout — sans arrêt
  précis".
- **The probe's "MIXED" lines are not misses here.** A French value carrying
  a no-break space serialises as `&nbsp;`, so the key probe cannot match it
  back to the catalogue; the visible-text probe and the screenshots confirm
  those paragraphs render French.
- `OperationsFrenchTests` covers Contracts (outcome, difficulty, standing),
  an event's tiers and plurals, Wikelo's gate and counts, a shopping list
  and a checklist, and that the game's names and the pilot's titles stay as
  written.

### 0.18.9

The Flight group: Logbook, Sessions (each debrief included), Servers, Places,
Points, Crew and Casualties. Rendered against this install in French, the
visible-text probe finds nothing English on the seven pages but game and pilot
text - ships, places, shard ids, regions, items, handles - and the numbers.
274 entries added, 1,554 in all.

- **The markup keys were collected from an English page.** The key probe run
  over a French page reports a mixed paragraph after `txDocument` has already
  translated the inline parts it could, so the Crew caption came back with
  `<em>Abandonnés</em>` inside the "English". The same probe with the copied
  data folder set to English returns the browser's own serialisation of the
  untouched markup, which is the key.
- **English is the key, so a word with two meanings has to change.**
  "Dropped" was already the Contracts page's abandoned contract and showed on
  Crew as "Abandonnés" for a member's connection dropping; Crew now says "Went
  offline", beside "Came online". A shard stay ending "left" collided with the
  party feed's "left" (somebody else went, "est parti"); it is "you left".
  Neither was fixable in the catalogue.
- **The Logbook's session lines are the server's English**, `Session aboard
  {ship}` and `1h 20m · 3 jumps · 1 death`, and are translated on the page by
  shape like the live feed's, keeping the ship. A shop's name in the same
  column is not a sentence and is shown as sent. A debrief's highlights go
  through the live feed's `feedText`.
- **A single contract's outcome is not the filter's plural.** "Completed" is
  keyed as the Contracts filter's "Terminés"; the debrief uses the Contracts
  table's lower-case words instead and capitalises them, so one contract is
  "Terminé".
- **Stored values stay English.** A point's category is saved as `Meet point`
  and only its label reads "Point de rendez-vous", so a point saved on a
  French page lands under the same chip as the rest. A plan made with "Repeat
  these stops" is titled in the page's language when it is made, like Routes'.
- **The copied server report stays English**, like the clipboard diagnostic
  summary: it is pasted where other pilots read it.
- **Two placeholders were shortened for width**: the Servers and Sessions
  search boxes clipped "Chercher un shard ou une note…"; they are "Shard ou
  note…" and "Vaisseau ou lieu…".
- `FlightFrenchTests` covers the logbook's server lines, a debrief with a feed
  sentence and a contract outcome, the servers table and its English report,
  Crew's plurals, the beds worded around their counts, and a point whose
  category is stored in English. The Crew heading cannot be checked there -
  the test page has no static markup - so it was checked in the render.

### 0.18.10

The Economy group - Spending, Ledger, Cargo, Market and the one-commodity
page - with the two top-level tabs no phase had named, Map and Log. Rendered
against this install in French, the visible-text probe finds nothing English on
Map, Spending, Ledger or Market but commodities, terminals, shops, places and
the dataset's commodity groups. 587 entries added, 2,141 in all.

- **What the Log still shows in English is evidence or a server sentence.**
  The lines read off a screenshot are quoted as the game printed them, like log
  excerpts. A reading's checks ("1,912,941 aUEC from the ledger", "Contracts:
  0 accepted of 10", "Where you were: …") and the summaries built around a
  count are the server's, and wait for phase 3; the fixed summaries already
  translate.
- **The source has to be read as well as the page.** The key probe walks the
  DOM after the scripts ran, so markup a script replaces on load - the
  location chip's "Your location", the Cargo caption, the loading lines - never
  reached it. A second pass lists text nodes and attributes from the
  `index.html` source of the chosen sections; it is how those were found.
- **Stored values stay English, again.** Ledger kinds (`Item bought`, `Cargo
  sold`) are the filter's keys and the run review's claim kinds; a place's kind
  (`RestStop`) colours the map; a service (`shop`, `refuel`) filters it. Each is
  translated where it is drawn, through `PLACE_KIND_WORDS` and `serviceLabel`,
  and compared in English everywhere else. The kinds are written out as spaced
  literals rather than split from the key, so the orphan test can see them.
- **The game's facility names stay**: Refinery, Commodity Trading - Freight
  Elevator. The app's own shortening of the two cargo facilities is page text.
- **`tx()` trims its key**, so a word joined with spaces (`' or '`) loses them
  in French; it is `` ` ${tx('or')} ` ``.
- **Three keys were already taken.** "live", "stock" and "Optional note" had
  translations from earlier pages that suit these ones too, and the merge
  refuses a second meaning rather than overwrite one.
- **One placeholder was shortened for width**: Market's search box clipped
  "Chercher une marchandise…"; it is "Marchandise…".
- `EconomyFrenchTests` covers the ledger (kinds French, filter keys English,
  the wallet card), the cargo and spending tiles, the Log's file list, a
  refinery reading with its station as written, the map's closest-facility
  answer with the game's facility name kept, and an empty flight-plan panel.

### 0.18.11

The Reference group: Ships, Parts, Mining (all four panes - where to go, the
rock calculator, hauls and refinery orders, salvage) and Crafting, with the
item card every page opens. Rendered against this install in French, the
visible-text probe finds nothing English on the four pages but item, ship,
shop and mineral names and the server's recipe materials ("any 2 of: …").
361 entries added, 2,502 in all.

- **Labels that wrap a control were split.** "Mass", "Ore", "Sold for" and the
  others sat beside an `<input>` or `<select>` inside their `<label>`, so the
  key held the control's markup - its value, step and title included. The text
  is in a `<span>` now, as the status toggles were in 0.18.4.
- **A paragraph whose span a script fills is keyed on the empty span.** The
  deposit caption, the crafting caption and the calculator's rule line each
  hold a `<span id>` the page writes into later; txDocument runs first, so the
  key has the span empty (or with the markup's default), and the translation
  carries the same span back so the script still finds it.
- **The table headers' tooltips are not translated, because nobody sees
  them.** `makeTablesSortable` replaces every header's title with "Sort", in
  English as well; the explanations written into the markup never show. A
  finding for later, not a translation gap.
- **Server words the page keys on**: a mining run's stage, a crack verdict, a
  resource kind - translated where drawn, compared in English.
- **One English sentence was nearly reworded by accident**: the overcharge
  clause on a mineral's notes. A rock test caught it, and the English is as it
  was.
- **One placeholder was shortened for width**: "Chercher une ressource ou un
  lieu…" became "Ressource ou lieu…".
- `ReferenceFrenchTests` covers the ship catalogue (careers and roles kept),
  a blueprint, a mining run's stage and next step, the crack verdict with the
  mineral's name kept, and an item card's price age.

### 0.18.12

The Hangar group: Fleet, Hangar, Loadout, Stash, Loot, Garage and Armoury,
with the saved kits and the ship picture every one of them draws. Rendered
against this install in French, the visible-text probe finds nothing English
on the seven pages but item, ship, maker and shop names, the slot names the
server makes from port names, gun kinds and fire modes, text read off a
screenshot, and three server sentences (the Garage's notes, the cargo fit's
reasons, a fire mode's condition). 824 entries added, 3,326 in all.

- **The logo was translated by accident and is pinned now.** "Quantum" went in
  as the Garage's sheet heading, and the brand is `Quantum <b>Wake</b>` - a
  mixed node whose whole key was missing, so the walk fell through to the bare
  text node and the header read "QUANTIQUE WAKE". An identity entry for the
  whole key keeps it the name. Any new one-word key that matches the first word
  of a mixed node will do the same; the screenshot is what catches it.
- **Two English column labels were renamed rather than shared.** "Length" was
  already "Durée" for how long a session ran on the Log's file list; that
  header is now "Duration", and "Length" is a ship's. The Armoury's "Drop" was
  "Retirer", the UEX feed button; the column is now "Drop-off".
- **The Loadout watermark was CSS.** `content: "PERSONAL ARMOURY"` cannot be
  looked up, so the rule reads `attr(data-mark)` and `renderLoadout` sets it.
- **Counts that agree were moved to `txn()`** even where English needs no
  plural: "1 décoché" and "3 décochés" are different words, "1 unticked" and
  "3 unticked" are not. Same for worn, carried, active this week and still
  needed.
- **Holster sizes are one phrase each.** "{weight} holster" cannot carry
  "Petit étui" and "Étui moyen" - French puts the size word on either side and
  agrees it - so `armouryHolster` looks up "Small holster", "Medium holster"
  and "Large holster".
- **No article in front of a game name.** "du {ship}" reads "du Anvil" and "de
  {maker}" reads "de Origin"; those templates now put the name behind a colon
  or in brackets after a noun ("ce vaisseau ({ship})").
- **Server words the page keys on**: an item's category on Stash and Loot, a
  kit line's holding, a loadout slot's category - translated where drawn,
  compared in English. The Loot kind filter keeps the English value and shows
  the French text.
- **French headers that pushed a column off the table were shortened**:
  "Charg." and "Par charg." on the Armoury's guns.
- **Two Garage messages now use curly quotes in English** ("Added “Starlancer
  fit”"), as every other quoted name in the app does; one WebTest followed.
- `HangarFrenchTests` covers a prepared kit (the sighting still asks), the
  loadout's readiness strip and armour profile with slot labels kept, the Loot
  kind filter's French text over English values, and a gun's holster and
  damage.

### 0.18.13

The rest of Settings: Controls, Overlay, Item labels (with StarStrings), the
shareable recap, shared files and About. Rendered against this install in
French, the visible-text probe finds nothing English on those pages but the
game's keybinding text (action names, action groups and their categories),
stick product names, item names and the server's one sentence about why the
sticks cannot be read live. 448 entries added, 3,774 in all.

- **Two more English words had a second meaning.** "hold" was already
  "soute", a cargo hold; the activation mode now reads "hold down" in English.
  "left" was already "est parti"; hats are one phrase per direction
  (`CONTROLS_HAT_LABELS`, "Hat {number} left"), so the direction is never
  looked up alone. "Install" was the StarStrings button and the About line
  naming the game install; the line is now "Game install".
- **The recap is translated, not kept English like the server report.** Part
  of it had been wrapped in an earlier phase, and it is the pilot's own
  snapshot rather than a report for other pilots, so finishing it was the
  coherent choice. The saved file carries `lang` from the page and a French
  title. The copied server report stays English.
- **Item categories on the label preview** are translated where drawn, as on
  Stash and Loot.
- **A pre-existing bug fixed on the way**: `saveOverlayLayout` is attached
  straight to change events, so its "saved" message argument was the event
  and the status line read "[object Event]". It now falls back to "saved"
  when the argument is not a string.
- **The action search placeholder was shortened** - "Chercher, parcourir…" -
  because a longer one ran under the unbind button beside it.
- **Overlay card names** gained the four the label table was missing
  (briefing, trip, respawn, status), named as the Now page titles them.
- `SettingsPagesFrenchTests` covers the button table with the game's action
  names kept and the mode not read as a cargo hold, a shared-file card, the
  label count with a category, and a hat as one phrase.

### 0.18.14

Help & FAQ: the three quick answers, the data-source guide and all 32
questions. 84 entries added, 3,858 in all - the page is markup only, so the
English key probe collected every key and the French was written as a list
in the same order and zipped onto it, which keeps 30-odd long keys with
`<b>`, `<code>` and links exact rather than retyped.

- **Answers quote the French buttons.** Where an answer names a control in
  bold - "Lire la position copiée", "Dernier wipe", "Conserver", the Ledger
  as "Comptes" - the French uses the catalogue's own words for it, looked up
  rather than translated afresh, so the help and the page agree. The tray's
  "Pin overlay" is quoted as "Épingler l'overlay", ahead of the tray itself.
- **Search works on the French text.** `filterHelp` matches what is on the
  page, so a French reader searches in French; the result count is
  "{shown} sur {total}".
- No WebTest: the stub page has no static markup, so Help is checked by the
  render, as captions and headings have been since 0.18.9.

### 0.18.15

Phase 3, first half: what a screenshot reading says - its summary, the
checks against the logs (subject, claim, belief, note), a wallet or tooltip
trouble, the Fleet Manager's scope line. 90 entries added, 3,948 in all.

- **Translated on the page, by shape, not in C#.** A reading's sentences are
  written when the screenshot is read and stored in
  `screen-readings.json`, so translating them at the source would leave every
  reading already taken in English - and every new one in whatever language
  was on when it was read. `screenText()` matches the shapes ScreenChecks and
  the frame readers write ("{n} aUEC from the ledger", "the {ship} as it
  ships", "3 parts named, 1 port empty, …") and translates those, keeping the
  names and figures, as `feedText()` does for the Now feed. A sentence it does
  not know is shown as written.
- **Notes joined with "; " are split before the shapes**, or the first shape
  swallows the second as its list.
- **A list after a head takes the page's colon** through a `{label}: {value}`
  key, so French gets its space.
- `ScreenFrenchTests` holds the shapes as the server writes them, one row
  each; a change to the server's wording shows up there as English rather
  than on a reader's page.
- Still English, for the second half: sentences the server computes on each
  request rather than stores - Garage notes, cargo-fit reasons, a fire mode's
  condition, crafting's "any 2 of:", the haul plan's pickups clause, why the
  sticks cannot be read live. Those are what "the same catalogue, read in
  C#" is for.

### 0.18.16

Phase 3, second half: every other sentence the server writes. The Garage's
notes and the pilot-DPS caveat, the cargo fit's reasons, the rock
calculator's notes, a fire mode's condition, a recipe's "any 2 of:", the
item card's facts, holding line and subtitle, the haul plan's notes, the kit
rule, and the server's error messages. 123 entries added, 4,071 in all.
Probed against this install in French on every view, nothing English is
left but game text (item, ship, place and contract names, OCR lines, port
labels) and what the pilot typed.

- **Not "the same catalogue, read in C#" after all.** The plan was a C#
  reader for server sentences; most of them turned out not to be computed on
  request. A fire mode's condition and a recipe's materials come out of the
  game files once and are cached; screen readings are stored. Translating in
  C# would freeze each in whatever language was on when it was made. So
  `serverText()` - the same shape matcher as `screenText()` - translates them
  on the page, and the per-request ones (Garage, cargo, rock, item card, haul
  plan, messages) go the same way so there is one mechanism. It is still one
  catalogue, and the orphan test still reads the C# literals, so a reworded
  server sentence names the entry it orphans.
- **The orphan test now escapes backslashes** in a key before looking for it
  among the C# literals: a path in a message ("...\\StarCitizen\\LIVE") was
  unfindable. The one verbatim `@"…"` piece of a message became an ordinary
  literal so the `+` join reads it.
- **Shapes before the "; " split.** Several server sentences carry a "; " of
  their own; a list shape stops at ";" instead, so two joined notes still
  split.
- **English is untouched**: `screenText()` returns early in English, so a
  shape's corrected plural ("1 of your things is here") only exists in the
  catalogue's French.
- **Messages from JavaScript errors stay as they are** - "Failed to fetch" is
  the browser's, not the app's; only what the server sends is wrapped.
- `ServerTextFrenchTests` holds the server's wording one row per shape.

### 0.18.17

Phase 4: the Cougar MFD pages, their setup window, and the desktop app - the
tray menu and its notices, the overlay's header and splash. 481 entries
added, 4,552 in all. With this French is complete for everything the app
writes, and the picker carries no "translated so far" note: what stays
English is the game's own text, quoted screenshot lines and the copied
server report, each on purpose.

- **The MFD core reads the catalogue when it is there.** `mfd-core.js` runs
  on its own in the MFD tests, with no `tx`, so it has a guarded `say()` /
  `sayn()` that uses the page's lookup when i18n.js is loaded and fills the
  English otherwise. The pages load `/api/i18n.js` and `i18n.js` as the
  dashboard does and run `txDocument` first.
- **Row labels stay English in the rows** and are translated as they are
  drawn, because the glyph beside each is chosen from the English word.
  Labels with a number in them ("TASK 2 OF 3") are matched by shape, and a
  kind the server names (a feed entry, a ledger line) falls back to the
  catalogue's word in capitals.
- **The compact HUD's short forms** are looked up from both sides of the
  table through the catalogue, since the long value now arrives translated.
- **Bezel captions stay the cockpit's codes.** NAV, CRGO, CNTR, BRT: six
  characters under a physical button, and the game's own HUD uses the same
  English codes. Rendered at 480 px in French, nothing else on a frame wraps
  or clips.
- **The desktop app reads the same catalogue.** `DesktopText` looks words up
  in `locales/{code}.json` through `ServerHost.CatalogueText`, and rereads
  `language.json` when its timestamp moves - the picker is in the dashboard,
  and the tray only learns of a change by looking, so the menu is reworded
  each time it opens and the header each time it comes back.
- **MfdLayout's warnings stay English at source** because the screen tests
  compile that file on its own; the controller translates them where it shows
  them, the one with figures by shape.
- **The orphan test reads the MFD files** as well as `app.js`, and the MFD
  setup page and script are copied into the WebTests output for it.
- `MfdFrenchTests` loads the core with the French catalogue (rows, a plural)
  and without it (English, unchanged).

## The phases, as they ended

Phases 1 to 4 are done: every page, every server sentence, the MFD pages and
the desktop app read in French. Phase 5 - playing the game in French - still
waits on a tester's `Game.log` from a French game.
