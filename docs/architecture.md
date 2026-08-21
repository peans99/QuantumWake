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
| `Verselog.Core` | `net10.0` | tailing, parsing, event model, state machines |
| `Verselog.Data` | `net10.0` | EF Core + SQLite, location graph, seed data |
| `Verselog.Server` | `net10.0` | ASP.NET Core minimal API + SignalR hub |
| `Verselog.Cli` | `net10.0` | backfill / verification harness |
| `Verselog.Overlay` | `net10.0-windows` | WPF shell hosting WebView2 |

Keeping Core/Data/Server portable leaves a Linux-hosted server mode open for
Phase 6 with no restructuring.

---

## The central decision: one UI, three hosts

```
┌─ Overlay (WPF + WebView2) ─┐   ┌─ Browser: 2nd screen / tablet ─┐   ┌─ Remote (Phase 6) ─┐
└──────────────┬─────────────┘   └───────────────┬───────────────┘   └─────────┬──────────┘
               └──────────────── HTTP + SignalR ─┴───────────────────────────  ┘
                                        │
                          Verselog.Server (ASP.NET Core)
                                        │
                     ┌──────────────────┴──────────────────┐
              Verselog.Core                      Verselog.Data
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
