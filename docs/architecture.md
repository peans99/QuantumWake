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
| `SCCompanion.Core` | `net10.0` | tailing, parsing, event model, state machines |
| `SCCompanion.Data` | `net10.0` | EF Core + SQLite, location graph, seed data |
| `SCCompanion.Server` | `net10.0` | ASP.NET Core minimal API + SignalR hub |
| `SCCompanion.Cli` | `net10.0` | backfill / verification harness |
| `SCCompanion.Overlay` | `net10.0-windows` | WPF shell hosting WebView2 |

Keeping Core/Data/Server portable leaves a Linux-hosted server mode open for
Phase 6 with no restructuring.

---

## The central decision: one UI, three hosts

```
┌─ Overlay (WPF + WebView2) ─┐   ┌─ Browser: 2nd screen / tablet ─┐   ┌─ Remote (Phase 6) ─┐
└──────────────┬─────────────┘   └───────────────┬───────────────┘   └─────────┬──────────┘
               └──────────────── HTTP + SignalR ─┴───────────────────────────  ┘
                                        │
                          SCCompanion.Server (ASP.NET Core)
                                        │
                     ┌──────────────────┴──────────────────┐
              SCCompanion.Core                      SCCompanion.Data
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

Static node data is seeded from public APIs
([api.star-citizen.wiki](https://api.star-citizen.wiki/locations),
[starcitizen-api.com](https://starcitizen-api.com/api.php)) and then
**committed as a local JSON file**. No runtime network dependency — the app keeps
SCStats' offline, read-only posture.

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
all-slain (many patterns, partial degradation). See [tools/](tools/).

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
