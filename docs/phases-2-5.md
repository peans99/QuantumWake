# Phases 2–5 — server, dashboard, map, overlay, dormant combat

Build log for the product itself. Phase 1 (the parser) is in
[phase-1-core.md](phase-1-core.md).

## What shipped

| Phase | Result |
|---|---|
| 2 | ASP.NET Core server + web dashboard on `127.0.0.1:31337` |
| 3 | Topology map of Stanton and Pyro |
| 4 | Transparent always-on-top WPF overlay hosting WebView2 |
| 5 | Dormant combat parser, tested and inert |

127 tests passing. Verified end to end against the real install: 145 sessions,
**6 d 5 h in game** vs 8 h 12 m in menus, 22 ships, 73 places, 112 contract
archetypes.

## The decision that paid off

**One web UI, three hosts.** The dashboard is plain HTML/CSS/JS served from
`web/`. A browser loads it for second-screen use; the overlay's WebView2 loads
the *same files* with `?overlay=1`, which switches the stylesheet to a narrow
translucent column; remote clients will load it again in Phase 6.

The overlay therefore cost one XAML window, one interop file, and ~25 lines of
CSS — not a second UI. A native WPF dashboard would have meant writing the
whole thing twice, then a third time for server mode.

## Choices worth recording

**SSE, not the SignalR JS client.** The SignalR hub stays mapped as the Phase 6
multi-client seam, but the dashboard streams over Server-Sent Events.
`EventSource` is built into every browser, so the page needs no bundled library
and no CDN — which matters because standalone mode makes *no* outbound requests
at all. Adding a CDN script would have quietly broken that promise.

**JSON blobs in SQLite, not a normalised schema.** The access pattern is "load
whole sessions, aggregate in memory" over a few hundred rows, not millions.
Shredding `SessionSummary` across relational tables would add migration churn
for no query benefit. Timestamps and handle are promoted to real indexed
columns; everything else is a JSON payload.

**Fingerprint, not content hash.** Idempotency uses file length plus last-write
time. Hashing 403 MB on every start would defeat the point of caching, and
rotated backups are immutable once written. Only the live `Game.log` is re-read
each scan. Cold backfill ~30 s; warm start effectively instant.

## The ship-metric correction

The first working dashboard displayed every ship at `00:00:00` time in seat.
Investigating rather than shipping the zeros found the cause:

```
     0  SetDriver
   497  ClearDriver
```

**Every one** of the 497 vehicle events is a control-token *release*. There is no
seat-entry event of any kind — `RequestEnterVehicle`, `OnEnterVehicle`,
`EnterSeat` and `CSCSeat` all return zero too. Time in seat is simply not
derivable from SC 4.9 logs.

The fix reflects that honestly rather than hiding it:

- **Flights are the headline metric** and they are exact — 126 Starlancer Max
  flights is real information.
- **Time aboard is an estimate**, measured from the last known ground anchor (a
  location visit, a spawn, or the previous sortie) to the release, and **capped
  at two hours** so one AFK stretch cannot dominate the totals.
- Every surface labels it: `~` in the UI, an explanatory caption on the Ships
  view, and XML docs on `ShipUsage.EstimatedTime`.

This is the general principle the whole app follows: where the logs are
imprecise, say so, rather than presenting a confident wrong number.

## Map design

A topology map, not a radar — the logs carry no player position.

- Stanton and Pyro as separate stars, bodies laid out on fixed rings in orbital
  order, locations clustered around the body they belong to.
- Node radius scales with visit count; colour encodes location kind.
- A dashed arc between the systems marks the jump point.
- **Unresolved ids are drawn in a tray at the bottom**, never dropped, so
  locations CIG adds later appear as a visible gap rather than vanishing.

## Overlay and anti-cheat

The overlay uses documented Win32 window styles only:
`WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED`,
plus `RegisterHotKey` for `Ctrl+Alt+O`. **No DLL injection and no
DirectX/WinAPI hooking** — the techniques that get tools flagged by Easy
Anti-Cheat are out of scope permanently.

Launching the overlay starts the server as a child process if one is not already
listening, so the user starts one thing. Verified: overlay launched, spawned the
server, server answered `200`.

The unavoidable cost of the safe approach: an always-on-top window is not
composited over an *exclusive fullscreen* swap chain, so Star Citizen must run
in **Borderless Windowed**. This is surfaced in the README, the launcher output,
and the overlay splash rather than left for the user to discover.

## Phase 5 — dormant by design

`<Actor Death>` and `<Vehicle Destruction>` are implemented from the archived
format, including StarLogs' classification tree (suicide → damage-type branch →
NPC/player matrix) and its NPC name heuristics. Ten tests cover parsing and
classification against the archived shapes.

On current logs it matches nothing, and the UI says so explicitly rather than
displaying a bare zero:

> Star Citizen 4.9 no longer writes kill or vehicle-destruction events to
> Game.log, so combat cannot be counted. Incapacitations are still reported. The
> parser is in place and will populate if CIG restores them.

If CIG restores the events, the killboard works with no further code.

## Still open

- **Phase 6 — server mode.** The seams are in place: the UI never touches the
  database, everything goes through the API, and local mode is just the server
  bound to loopback. Remaining work is identity, org channels, and a shared
  fleet map.
- **Contract completion.** Acceptance is detected; completion is not clearly
  logged and needs more investigation.
- **Location graph coverage.** All 72 observed ids resolve to a system, body and
  kind today. New content will need additions to `Universe` in
  `Locations/Location.cs` — unresolved ids stay visible in the map's unmapped
  tray so the gap is obvious.
- **Parser-health panel in the UI.** The data is collected and exposed by the
  CLI, but the dashboard does not surface it yet.
