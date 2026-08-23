# Architecture

Decisions and their reasons. Companion to
[findings.md](findings.md) (why the app is shaped this way) and
[log-format-reference.md](log-format-reference.md) (what it parses).

Approved plan: `C:\Users\nicol\.claude\plans\sunny-finding-shannon.md`

---

## Constraints that drive everything

Two facts about SC 4.9 logs, both established empirically over 402.5 MB / 144
files. They are not preferences — they bound what is buildable.

1. **No combat telemetry.** Zero `<Actor Death>` / `<Vehicle Destruction>`.
   → A killboard is impossible. Combat parsing ships dormant (Phase 5).
2. **No live player position.** Zero `pos x:` / `PlayerPos` / `CurrentZone`.
   → The minimap is a **topology map**, not a radar. Position is *inferred* from
   discrete events by a state machine, and carries a confidence level.

A third constraint comes from the platform, not the logs:

3. **Easy Anti-Cheat.** The overlay must never inject a DLL or hook
   DirectX/WinAPI. OS-level transparent top-most window only.

---

## Stack

**C# 14 / .NET 10 (LTS)**. Verified installed: SDK `10.0.400`,
`Microsoft.AspNetCore.App 10.0.11`, `Microsoft.WindowsDesktop.App 10.0.11`.

Target frameworks are split so only the WPF shell is Windows-bound:

| Project | TFM | Role |
|---|---|---|
| `Quantumwake.Core` | `net10.0` | tailing, parsing, event model, state machines |
| `Quantumwake.Data` | `net10.0` | EF Core + SQLite, location graph, seed data |
| `Quantumwake.Server` | `net10.0` | ASP.NET Core minimal API + SignalR hub |
| `Quantumwake.Cli` | `net10.0` | backfill / verification harness |
| `Quantumwake.Overlay` | `net10.0-windows` | WPF shell hosting WebView2 |

Keeping Core/Data/Server portable leaves a Linux-hosted server mode open for
Phase 6 with no restructuring.

---

## The central decision: one UI, three hosts

```
┌─ Overlay (WPF + WebView2) ─┐   ┌─ Browser: 2nd screen / tablet ─┐   ┌─ Remote (Phase 6) ─┐
└──────────────┬─────────────┘   └───────────────┬───────────────┘   └─────────┬──────────┘
               └──────────────── HTTP + SignalR ─┴───────────────────────────  ┘
                                        │
                          Quantumwake.Server (ASP.NET Core)
                                        │
                     ┌──────────────────┴──────────────────┐
              Quantumwake.Core                      Quantumwake.Data
              tail → parse → state machine          SQLite + location graph
```

The UI is a web app served from `web\`. A browser consumes it for second-screen
use; WebView2 inside a transparent WPF window consumes the *same* UI for the
overlay; remote clients consume it later for server mode.

**Why not pure WPF/WinUI:** it would require writing the UI three times — once
for the overlay, once for the second screen, once for the web-based server mode.
The web-first approach writes it once.

**Why SignalR now, when we only have one local client:** multi-client is the
stated future direction. SignalR is inherently multi-client, so Phase 6 becomes
"add auth and change the bind address" rather than a rewrite. The cost of
adopting it on day one is close to zero.

### Seams to keep clean for Phase 6

- UI never touches the database directly — everything through the API/hub.
- Session and event models carry an explicit owner id from the first migration.
- Local mode is simply the server bound to loopback with auth disabled.

---

## Location: inference, not tracking

The hardest part of the design, and entirely a consequence of constraint #2.

Weak discrete signals are fused into a current-location estimate:

| Signal | Confidence | Notes |
|---|---|---|
| `<RequestLocationInventory> … Location[X]` | high | exact node; 1,386 observed |
| Quantum route destination reached | high | from `<Calculate Route>` |
| `OnClientSpawned` | high | resets to spawn node |
| `Loading screen for X : <gamerules>` | coarse | menu ↔ PU transition |
| gamerules change | coarse | in-game vs frontend |

State model:

```
Unknown ──► AtLocation(node) ──► QuantumTravelling(from, to) ──► AtLocation(to)
```

Emits `LocationChanged`; the minimap and timeline subscribe. **Confidence is part
of the public model** — the UI renders uncertainty (pulse ring) rather than
pretending to precision the logs cannot support.

Location IDs are structured and parseable, which is what makes a real map
feasible without position data:

```
Stanton4_NewBabbage    → Stanton · microTech (4) · New Babbage
RR_MIC_LEO             → Rest stop · microTech · LEO
RR_JP_StantonPyro      → Rest stop at the Stanton→Pyro jump point
LOC_rs_ext_stan-pyro_jp1 → Stanton↔Pyro jump point
```

Node data is not seeded from any web API. Display names come from the game's
own localisation table inside `Data.p4k`, and system/body/kind come from rules
over the id grammar above, so there is no data file to go stale and no runtime
network dependency - the app keeps SCStats' offline, read-only posture.
[api.star-citizen.wiki](https://api.star-citizen.wiki) and
[starcitizen-api.com](https://starcitizen-api.com) were used during research as a
cross-check on body and station names only; see [credits.md](credits.md).

### The atlas: the whole map, not just the visited part

Drawing only visited places gives a map of nowhere — dots with no context, and no
sense of how much of the system is left. The game's own localisation table names
about 1,300 locations, and running each id back through the resolver puts a
system, body and kind on it. That is everything the layout needs, so unvisited
places are drawn as hollow rings beside the solid ones, behind a **Visited only**
toggle.

Most of that table is interiors. `Pyro1_L2_03_Entrance` and its 800-odd siblings
are elevator landings inside one building; including them buries the 240 places
that are somewhere you actually fly to. The atlas therefore keeps only ids the
resolver can give a real category to — plus every visited place regardless, since
somewhere the player has stood earns its dot whatever its id looks like.

### The cargo panel: two answers to one question

Picking a commodity already lit the places that trade it and graded them by UEX
price. The panel beside the map answers the rest of it, in two sections that are
deliberately not merged:

- **Terminals**, from UEX, ranked by price for the side being shown. What the
  commodity is worth today, everywhere, whether or not the player has been.
- **Your receipts**, from this install's own kiosk lines, over a window of 24
  hours, 3 days, 7 days or longer. What the player was actually paid, and where.

Merging them would be a lie of averages: one is a live market, the other is a
logbook, and a run that paid well last week against a terminal that pays badly
today is exactly the discrepancy worth seeing. Selling and buying are a toggle
rather than the old `buy:` search prefix, which was undiscoverable; the toggle
still writes the prefix, so the search box stays the one thing driving the map.

**Shade by my own prices** grades the map from receipts instead of UEX. It needs
no network, works with the integration off, and lights places the catalogue has
never heard of — if the player sold there, it is an answer.

Double-clicking a place turns the panel round to that station: what it has taken,
what it has offered, and what the catalogue says it sells. The detail card
answers *what is this place*; a station with a dozen commodities on each side of
the counter needs more room than a card has.

Receipts carry the place's engine id (`TradeRecord.PlaceId`), so a sale lands on
the node the map already draws rather than being matched on a display name two
places can share. The arithmetic runs in the browser: a few hundred receipts is
nothing to loop over, and commodity, side and window change often enough that a
round trip per twiddle would be the slow part.

### Flight plans: the second thing the player authors

Everything else on the page is observed or downloaded, and can be rebuilt by a
rescan. A plan is the player's own work — where they mean to go, in the order
they mean to go — so it follows the shopping list's rules: its own file
(`trips.json`), never touched by a rescan, surviving every cache wipe.

One plan is *tracked* at a time. The Now card and the map both answer "where
next", and two plans have no single next. A stop carries the place's engine id
as well as its name, which is what lets the map draw it on a node it already has
and lets the live feed recognise an arrival.

**Arriving crosses a stop off.** The app knows where the player is standing, so
asking them to tick a box for it would be asking for data it already holds. The
live tail fires on the change rather than on every event, only the tracked plan
is touched, and only the *next* unfinished stop for that place — a run that
calls at Lorville twice is two stops, and one landing is one stop. Hand ticking
still works, in both directions.

A stop can come from anywhere that knows a place: the map's detail card, the
station panel, a trade route (two stops, buy then sell, carrying the SCU and the
prices as notes), or a shopping list (one stop per terminal, carrying what to
buy there — a list knows where its missing things sell).

A list asks before it plans. The cheapest seller UEX knows is a fine default and
a poor answer: for a common good it is routinely three jumps out of the way, and
only the player knows what else the run has to fit around. So the button opens a
chooser — one row per missing thing, its sellers cheapest first with price and
stock, each row skippable — and the stop count updates as the choices change.
Anything with no known seller is shown as such rather than quietly dropped.

### A list is not only cargo, and not only a set of things

A line on a list is whatever the player wrote, so the lookup behind it cannot
assume a kind. `/api/shopping/sellers` tries the commodity market first and the
item shops second, matching a written name against the reference catalogue
loosely enough that "Hydro Jet" finds the HydroJet cooler, and says which feed
answered. That single endpoint is what lets one list hold "Agricium 20 SCU" and
"Bulwark" and route both.

The chooser then offers the same decision from either end:

- **By item** — where do I get this? One row per line, sellers cheapest first,
  each carrying its system and whether the law reaches it. This is how a list is
  written.
- **By location** — what is this landing worth? One row per counter, ranked by
  how much of the list it covers, then by what that stop costs. Ticking a stop
  claims everything it can supply that an earlier tick has not; unticking hands
  those back to whatever is left. **Fewest stops** packs the list greedily,
  because buying each thing where it is cheapest is one landing per thing, and
  fuel and time cost more than the difference.

Both views write into one map of line → terminal, so switching never loses a
choice and the plan is built from a single answer.

A list can also say **where it is for**, chosen when it is written and
changeable afterwards. That is a preference rather than a filter: a line whose
destination stocks it starts there even when somewhere else is cheaper — which
is what naming a destination means — and a line it does not stock falls back to
the cheapest seller that can fill the order. The place also leads the location
view whatever it carries, because the landing is already decided.

Both halves of writing a list are pickers over data the app already holds: every
place it can name, visited ones first, and everything that can actually be
bought — commodities and priced items, nothing else, because a line no shop
sells is a line no plan can route. The free-text box remains the list; the
pickers only spell things.

### What fits a ship

The ships dataset carries each vessel's whole loadout tree, and every port in it
states what may replace what is fitted: a type, a size range, and whether the
game lets the player change it at all. That is the game's own answer to "what
fits", so the Fleet page can offer parts without guessing — each with what the
ship flies with today, what is sold that fits, and the counter that stocks it.

Two things in that data are not what they look like. A port accepting sizes 1
to 3 is three rows in the digest and one hole in the ship. And a gimbal mount
offers the same hole twice — once for a gun, once for the gimbal that then
holds one — so a port whose id extends another port's id is inside it, and only
the outermost of each chain is counted. Without that a Corsair reports twelve
size 2 gun ports instead of six.

The join is by class name (`DRAK_Corsair`), never by display name. "Drake
Corsair" cannot be turned back into the key: the display manufacturer is a word
where the class carries a code, and variants spell themselves differently
("Mk II" against `Mk2`). The raw log tokens *are* the class name, so a ship
carries it from the parser to the page and every reference lookup uses it.

**The two naming schemes are reconciled once, on the server.** UEX names the
counter ("Admin - Port Tressler", "Seraphim"), the game names the place ("Port
Tressler", "Seraphim Station"), and every feature that puts a price on the map
or a terminal on a plan needs the join. `TerminalPlaces` does it from the atlas
and hands the id out with the data - on `/api/uex/market` rows and on both ends
of a trade route - so the shading, the panel and the plan cannot disagree. The
rule is narrow on purpose: exact name, then the place named inside the terminal
(longest wins), then the terminal named inside a place, then the station code a
shop chain names itself after - "Platinum CRU-L4" is at "CRU-L4 Shallow Fields
Station", where neither name contains the other. A code is safe to match on
because it is never a word: only a leading token carrying a digit counts, which
stops "Port Tressler" claiming every counter with "port" in its name. An
ambiguous or short match resolves to nothing. A stop on the wrong dot sends someone to the
wrong moon; a stop with no dot is still a stop, with its name and notes intact.

That is also why arrival matches on the name when a stop has no id: those stops
are the ones the map cannot draw, and they must not also be the only ones that
never cross themselves off. An id is always tried first, so a name never wins
over one.

### Where a transaction happened

Kiosks name a vendor, not a place, and every commodity terminal in the game logs
itself as `SCShop_Admin_lt_base_g`. A ledger built on shop names reads "Admin lt
base g" for every cargo sale ever made.

The position is recoverable anyway. Arrivals and quantum jumps are both logged
with timestamps, so the last one before the transaction says where it happened —
which is how the ledger and Cargo pages report Port Tressler and Seraphim Station
rather than a kiosk id. Vendors keep a column of their own, resolved through the
game's `shop_name_*` table where it publishes a brand.

---

## The wipe: where a countable history begins

A data wipe resets money, ships and inventory. Every total here - what you own,
what you have earned, what your stashes hold - is answering a question about the
account being played now, and a wipe means the logs before it describe a
different one. Adding pre-wipe sales to post-wipe income does not make a bigger
number, it makes a wrong one.

So the library counts only sessions from the wipe onwards. One accessor does it,
`Counted()`, and every question goes through that accessor rather than the store
- which is the point: a view added next year cannot forget the rule. A session
is judged by when it *started*, since one that ran into the wipe belongs to the
account that ended.

Three things keep it honest:

- **Nothing is deleted.** Pre-wipe sessions are still parsed and still stored.
  Moving the date back brings the whole history straight back.
- **The page says what it is holding.** The Settings row reports how many
  sessions are kept but not counted, so a number that looks low has a visible
  cause rather than a mysterious one.
- **The date is a setting, not a constant.** CIG decides when wipes land. The
  default is Alpha 4.8 on 15 May 2026, dated from this install's own logs: the
  last 4.7 session was the 11th, the first 4.8 one the 15th. Evidence beats a
  remembered date, and the player can correct it or switch it off entirely.
- **So is the depth.** Wipes come at different depths: a patch may reset aUEC
  and leave the hangar, clear inventories without touching balances, or take
  the lot. Filing every one as "a wipe" and hiding all history is wrong in the
  common case, so the player ticks what this one actually took — money, ships,
  inventory, play history — and anything left unticked reaches back through the
  wipe as if it had not happened, because for that number it did not.

`Counted(WipeScope)` is the accessor, and every caller names what it is
counting: the ledger asks for money, the stash asks for inventory, the session
list asks for history. `Stats()` answers for all four at once, so it narrows one
list per kind — after a money-only wipe its spending starts again while its
fleet, stashes and places keep everything. Naming the wrong category is the one
way to get this wrong, which is why the parameter is not optional.

## Prices only get stale when nobody is looking

UEX is fetched on a click and never on a timer. That is the right default and it
has one cost: a table pulled a fortnight ago looks exactly like one pulled this
morning, while every margin computed from it is quietly wrong.

So the app looks once at startup, and if the snapshot has turned a day old it
says so and offers the single click that fixes it - prices and any enabled feeds
together, since renewing half of it leaves the same problem. It is an offer, not
an alert: nothing is blocked, "not now" lasts until tomorrow rather than for
ever, and the check still never runs on a schedule.

## The map is a control surface, not a picture

Four rules the star map learned the hard way. Each was invisible in a
screenshot, and three of them survived a green test suite.

**Do not take the pointer until it moves.** The map captures the pointer on
drag so a pan that leaves the window keeps working, and capturing on
`pointerdown` is the obvious place. It is also wrong: while an element holds
the capture the browser delivers the *click* to that element rather than to
whatever is under the cursor, so every click aimed at a place went to the
`<svg>` instead. Nothing on the map opened - not a detail card, not a trade
panel, not a stop onto a plan - and it had been that way since the map was
written. The capture now waits for four pixels of movement, which makes a press
a click and a drag still a drag.

**A click target must not reach into its neighbour's.** Marks carry an
invisible pad so a small one is easy to hit. In a cluster those pads overlapped,
and SVG gives a shared point to whatever was drawn last, so the covered node
became unclickable rather than merely ambiguous: 151 of 290 places. The pad now
stops at the halfway line to the nearest neighbour and never shrinks below the
mark itself.

**An animation restarted is an animation nobody sees.** The live feed repeats
the player's position every second, and the marker redrew on every message,
restarting a 2.2-second pulse a fifth of the way in. It looked like a dead dot.
Both the here-marker and the travel vector redraw only when what they describe
actually changes.

**Decluttering is a budget, not collision avoidance.** Avoiding collisions stops
names overlapping but never stops them being asked for, and one moon can want
twenty-two. The view has a label budget spent on the busiest places first,
growing with the square of the zoom: names arrive as detail is asked for. A
searched-for place is never rationed - a lit dot the user cannot name is not an
answer.

Grouping follows from the layout rather than fighting it. Sites are already
placed by golden angle around their body, so the bubble behind them only has to
say they belong together: faint at rest, lit while the pointer is inside it, and
clickable in the gaps to frame that body. A force simulation would settle
differently on every draw, which is precisely what a label placer cannot have.

## Testing the page, not just the server



The dashboard is a single script against the browser's globals - no framework,
no build step - which is why it stayed untestable while the C# grew two hundred
tests. It is not a thin layer: what a commodity fetched where, which stop comes
next, which seller can actually fill the order, what colour a price is. All of
it shipped on the strength of a screenshot.

`Quantumwake.WebTests` runs `web/app.js` in-process under Jint against `dom.js`,
a stub with enough of a document to satisfy the page. Three rules keep it
honest:

- **The stub answers every selector with an element**, because the assertions
  are about what the code puts into the page - rows, classes, numbers, colours -
  not about how `index.html` nests its markup.
- **The script is loaded without its last line.** `boot()` starts the dashboard
  and polls until the server answers, which a browser paces with timers; with
  the stub's timers inert that is an unbounded loop. Tests drive the page
  explicitly instead, and the harness fails loudly if that call is ever renamed.
- **Network is a routing table**, so a test says what UEX or the trip API
  returned, and can then read back exactly what the page sent.

What it does not cover is layout, CSS, and anything the browser itself decides.
For that there is still no substitute for looking.

### What a synthetic click cannot see

The page tests dispatch events straight at elements, which is enough for panels,
choosers and state - and structurally blind to anything the browser does between
a real input and a handler. Pointer capture is exactly that: a click dispatched
at a node bypasses it, so the suite reported a working map for as long as the
map was unclickable.

Where behaviour depends on real input, drive real input. Chrome's DevTools
protocol takes `Input.dispatchMouseEvent` over a websocket, which is how the
capture bug was proved (handler fired 0 times before the fix, 1 after) and how
panning was checked for regressions afterwards. It is not wired into
`dotnet test`; it lives in the session scratchpad as a script, and that gap is
known.

## Resilience: fail soft, and say so



CIG removes log events patch over patch — quantum travel in 4.0.1, death scope in
4.0.2, inter-system jumps in 4.1.0, combat entirely by 4.9. Assume more will go.

- An unmatched line is logged and skipped. **Never fatal.**
- No feature depends on a single pattern where a fallback exists.
- A **parser health panel** reports match rate per event type, so a patch that
  breaks parsing shows up as a visible red indicator rather than as silently
  empty charts.

This is the direct lesson of SC-Kill-Monitor (one pattern, total failure) versus
all-slain (many patterns, partial degradation).

---

## Read-only and safe by default

Adopted from SCStats' stance, which is the correct posture for anything touching
a live game install under anti-cheat:

- read-only access to log files; the game directory is never written to
- no memory access, no injection, no hooking, no process modification
- no outbound network calls in standalone mode
- open logs `FileShare.ReadWrite | FileShare.Delete` — the game holds the handle,
  and anything stricter fails

---

## Build order, and why

1. **Core + CLI** — parser and state machine, verified against ground truth from
   the backfill before any UI exists.
2. **Server + second screen** — no fullscreen constraint, fastest to validate.
3. **Minimap** — needs the location graph from step 1 to be trustworthy first.
4. **Overlay** — reuses steps 2–3 wholesale; only a compact layout is new.
5. **Dormant combat** — cheap, contingent value.
6. **Server mode** — deferred, but the seams above are kept clean from step 2.

Second screen ships before the overlay deliberately: the overlay is invisible
under exclusive fullscreen (SC must run Borderless Windowed), so it carries a
support burden the second screen does not.
