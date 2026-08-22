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

## One executable, and somewhere to click

Added 2026-08-21, after the question "how do we simplify this for users who are
not technical?" The honest answer was that the release defeated them four times
over: install the .NET Desktop Runtime first, then pick the right file out of a
fifty-file zip, then get past SmartScreen, and then find no way to stop the
thing short of Task Manager.

**The server moved inside the overlay process.** `Program.cs` became a three-line
entry point over a new `ServerHost.Build`, which the WPF app calls at startup and
the standalone server still calls for itself. The overlay no longer hunts for
`Quantumwake.Server.exe` and starts it as a child, which also removes a failure
mode: an orphaned server surviving an overlay crash.

**The dashboard is embedded in the assembly.** `web/` still copies next to the
binary and that copy wins - editing a stylesheet should not need a rebuild - but
the same files are compiled in as resources, and `EmbeddedWeb` serves those when
no directory exists beside the executable. A middleware rather than an
`IFileProvider`: twenty lines against a class that would have to lie about
directory listings, range requests and change tokens.

The result is a self-contained single file of **87 MB** that needs no runtime.
Measured before committing to it - a self-contained WPF build was expected to be
far worse, and at 87 MB compressed the trade against "install .NET first" is not
close.

**A tray icon, because there was nowhere to click.** Open dashboard, show or hide
the overlay, quit. Hiding the overlay leaves the dashboard served, which is what
a second-monitor user wants, and the choice persists in `settings.json` - kept
separate from `overlay.json` deliberately: geometry is rewritten on every drag,
and a preference someone chose should not share a file with that.

Shutdown is explicit rather than tied to the window, so closing the overlay does
not kill the app.

What was deliberately not built: an installer, because a single exe does not need
one; and a code-signing certificate, because it costs a few hundred a year and a
free fan tool cannot justify it. The SmartScreen prompt is documented in the
release notes instead, which is what every tool in this niche does.

## 0.2.0 - the data the logs cannot carry

The release after the repository went public, and the point where the app
stopped being log-only. Everything below is opt-in and off by default; the
Settings page is where all of it lives, and the app still makes no request
anyone did not click for.

**The community dataset** (StarCitizenWiki/scunpacked-data, built with octfx's
ScDataDumper) closed the oldest open question in these docs: cargo sales are
now named. The resourceGUID that resolves against nothing in the local install
- verified through the whole DataCore in seven byte orders - resolves against
the community's table on the first try. The same download supplies where each
commodity trades, the ship reference (role, crew, insurance claim cost and
wait) and the item reference (size, grade, maker). Five files, ~110 MB,
digested to ~2.5 MB.

**UEX** joined as a second, separately-consented integration: crowd-sourced
best prices on the Market page, and - with the user's own UEX keys - the
option to report the sale prices the logs already recorded, previewed before a
byte is sent. A log reader turns out to be a natural datarunner: every kiosk
sale line is an exact price at a known terminal at a known time.

Around the data came the pages: Market, Loot (honest about being a first-seen
signal, not a pickup event), Settings, and the map grew commodity highlighting,
follow mode, per-place detail cards and keyboard zoom. The overlay stopped
starting by default, shrank its tab strip to the six glanceable views, and
became controllable from the dashboard through an in-process bridge. Tables
sort by their headers everywhere.

The pre-1.0 notice went into the README and the release notes in the same
breath: until 1.0 the ground will keep moving, and the cache schema version
plus the rebuild-from-logs design are what make that safe to say.

## 0.3.0 - the joins

With three datasets in hand - the logs, the community reference, UEX prices -
this release is what falls out of joining them. No new data was fetched;
every feature below is a join across what 0.2.0 already downloaded.

**The trade advisor** is logs x UEX: the tail already knows where the player
is, the price matrix knows what that place buys and what everywhere else
pays, so the Now page shows the best cargo to haul out of the current
station. Matching a log place to a UEX terminal is the fiddly part - stations
carry several terminals, so the join prefers the Admin/TDD one.

**Sale scoring** is the same join pointed backwards: every sell the logs
recorded, compared with the best price UEX knows anywhere. The Cargo page
totals the difference as "left on the table" and marks each row green when it
was within 3% of best - a post-hoc report card for trading instinct.

**The Assets page** is the three-way join: fleet from the logs priced through
UEX vehicle tables (their names drop the manufacturer, so "Drake Corsair"
falls back to "Corsair"), worn kit and stash priced item-by-item through the
community uuid into UEX item prices, and claim exposure - deaths times the
session's average expedite fee. Every total states its coverage, because a
sum over the recognised half of a fleet is an estimate and should say so.

**The map got geometry**: starmap_positions places each body at its true
bearing and square-root-compressed distance. The catch is scale - a moon sits
on its planet's pixel and their site clusters collide - so bodies are grouped
by proximity and moons fan on a local ring around their planet, the way the
game's own starmap solves it.

One layout bug fell out of the new pages: sixteen tabs outgrew a 1600px
window, and the overflowing strip both put a horizontal scrollbar on every
page and let tab-centring drag the whole document sideways. The strip now
scrolls itself.

## 0.4.0 - the widget grows up, the map learns to answer

The overlay stopped needing a hotkey cheat-sheet: previous/next tab,
fullscreen and close are buttons on its interactive header, the how-to strap
became tooltips so the header fits a 230px widget, and Ctrl+Alt+F (or the
button) grows the widget to cover its monitor - where the six-tab whitelist
lifts and the whole dashboard is available - then restores the exact compact
bounds, which are the only geometry ever persisted.

The map got the UX pass its search deserved. Searching used to light dots on
a full-width map and leave the reader to find them by eye; now non-matches
recede to a deep dim, matches glow and name themselves (up to the point
where names would be a wall), and the view glides to frame the answer -
cancelled the instant the user pans, because a map must never fight its
reader. A suggestion list jumps to a place by name, a styled tooltip answers
hovers instantly, and the disc gained orbit rings and a still starfield.

Around it: Market grew a commodity-group dropdown, and the Assets fleet
gained an Owned tick per ship - the fleet is inferred from logs, so rentals
and ships since sold appear in it, and only the player knows which; untick
them and the totals recompute, remembered per-browser.

## 0.5.0 - jobs, catalogues, and a map that answers

The release where the app stopped only reporting and started helping.

**Jobs** is the first thing here authored rather than observed. A shopping
list or a blueprint build is written by the player, and then checks itself
against what the logs say they hold: each line shows which stash has it, or
what the missing part costs and where. That join is the whole point - UEX
knows every price and only these logs know your lockers. Contracts share the
page, taken from the running session alone, because contracts do not survive
leaving the game. Routes ranks the hauls UEX knows by what *this* run earns,
sized to a ship from the player's own fleet and the capital they name, and
says which cap bit first: hold, capital, or the shop's own stock.

**The map** learned to answer rather than decorate. A search stopped filtering
and started highlighting - dimming the rest, framing the matches, and naming
them, which meant labels finally had to stop overlapping. They now go through
a collision pass: five candidate positions each, most-visited placed first, a
label that fits nowhere dropped rather than piled on its neighbour. Zoomed in,
whole systems spread apart to give their names room. Commodity searches can
shade by price or by SCU capacity, and every place name in the app - ledger,
cargo, loot, stashes, charts - became a link to it.

**Three waves through scunpacked-data** turned files we already downloaded into
four reference catalogues: ship spec sheets, real item names and where each
part is stocked, the game's own resource deposit tables with spawn odds, and
1,597 crafting recipes weighed against simply buying the thing. The best find
was smallest: the game announces a received blueprint in a toast the parser
was already reading and throwing away, so the app knows which recipes a player
actually holds.

Two failures worth recording. A missing Star Citizen install crashed the whole
dashboard - `AddSingleton(install!)`, where the null-forgiving operator was
simply wrong - so a machine with the game in an unusual folder got a container
exception instead of the page explaining itself. Detection now knows more
layouts, removable drives and the launcher's own log, and asks for the folder
when all of that fails. And the first-flight wizard greeted established
installs, because its "done" marker arrived with the wizard itself and nobody
who came before had one.

`--data` moves every cache and setting elsewhere, so the next first run can be
rehearsed without destroying the real one to see it.

### 0.5.1

A patch. Stashes looked like they were losing things, and they were: only
the newest inventory listing counted, but a listing is only ever a page, so
glancing at one tab of a locker overwrote a full browse of it and the place
appeared emptied - two items where seventeen had been recorded. Both readings
are now offered, since each answers a different question honestly: the
default still says what is there now, and "Everything ever seen" unions every
listing for what has been left lying around.

Also here: the inferred respawn point, which the game never states and which
this works out from where the player turns up after dying, and a Now card
showing it - because where you will reappear is worth knowing before the
fight rather than after it.
