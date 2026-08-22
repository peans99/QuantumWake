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

**The two naming schemes are reconciled once, on the server.** UEX names the
counter ("Admin - Port Tressler", "Seraphim"), the game names the place ("Port
Tressler", "Seraphim Station"), and every feature that puts a price on the map
or a terminal on a plan needs the join. `TerminalPlaces` does it from the atlas
and hands the id out with the data - on `/api/uex/market` rows and on both ends
of a trade route - so the shading, the panel and the plan cannot disagree. The
rule is narrow on purpose: exact name, then the place named inside the terminal
(longest wins), then the terminal named inside a place, and an ambiguous or
short match resolves to nothing. A stop on the wrong dot sends someone to the
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
