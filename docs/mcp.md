# AI assistants (MCP)

Since 1.2.48 Quantum Wake can be used by an AI assistant - Claude Code, Codex,
or any client of the [Model Context Protocol](https://modelcontextprotocol.io) -
on the same PC. It is **off until the pilot turns it on** in Settings → AI
assistants, and writing is a second switch under the first, also off.
Between them, since 1.2.51, a box for each part of the pilot's own data.

## Connecting

The app must be running. With access on, Settings shows the lines to paste,
with the port the app is actually on (31337 by default):

```
claude mcp add --transport http quantumwake http://localhost:31337/mcp
```

```toml
# ~/.codex/config.toml
[mcp_servers.quantumwake]
url = "http://localhost:31337/mcp"
```

Claude Desktop starts its servers itself and talks to them over stdio, so
since 1.2.53 the program is one: `QuantumWake.exe --mcp-stdio` relays each
line to the running app's `/mcp` (`McpStdioBridge.cs`). Settings shows the
config with this program's real path, from `/api/mcp`'s `stdio` field:

```json
{
  "mcpServers": {
    "quantumwake": {
      "command": "C:\\Users\\you\\Downloads\\QuantumWake.exe",
      "args": ["--mcp-stdio"]
    }
  }
}
```

`--Port <n>` is added when the app is not on 31337. A Store copy is named by
its App Execution Alias, `%LOCALAPPDATA%\Microsoft\WindowsApps\QuantumWake.exe`,
because the package folder's name carries the version and a path copied from
it would break at the next update.

Measured: Claude Code connected to a test copy on 2026-11-15, called
`find_places` and `where_to_buy`, and answered from the pilot's data; the
call log recorded it as `claude-code`. Codex was not installed on the machine,
so its line is the documented `url` form for a Streamable HTTP server and has
not been run here.

The stdio relay, measured on 2026-11-16 with the published single-file
`QuantumWake.exe` (a GUI-subsystem exe, so whether it had stdout at all was
the question): Claude Code 2.1.325 started it as a `stdio` server with
`--mcp-stdio --Port 31395` against a test copy, called `my_ships` and
`my_reputation` through it, and answered the Super Hornet Mk II's 12.4 hours
and 61 completed Head Hunters contracts; the app's call log recorded both
calls as `claude-code`. No `QuantumWake` process was left when the client
exited. With nothing on the port, a piped `ping` came back as an error saying
to start the app, and the exe exited 0 when stdin closed. Claude Desktop
itself is not installed on the machine, so its config is the documented
`mcpServers` form and has not been run here; nor has the Store alias, which
needs the package installed.

## The stdio relay

A relay, not a second server. A copy started by a client would have nothing
scanned, so it would parse every log again for each conversation and hold a
view of the pilot's plans that the dashboard does not see. Relayed, there is
one app, one set of switches and one call log.

- The flag is checked first in both entry points - `App.OnStartup` in the
  desktop app, before the trace, the tray and the server, and `Program.cs`
  in the bare server - so the relay never shows a tray icon or the "already
  running" toast a second copy would.
- One JSON-RPC message per line in, one per line out, in order; stdout
  carries nothing else, and the relay's own notes go to stderr, which clients
  keep as the server's log. A notification gets no line back.
- It never starts the app: a chat client starts its servers when it opens,
  and Quantum Wake appearing because Claude did would be something the pilot
  never asked for. A request while nothing answers gets the error "Quantum
  Wake is not running on port 31337. Start it, then ask again."; with access
  off it gets the app's own sentence pointing at Settings.
- The relay posts to 127.0.0.1 without an Origin, so `LanGuard` and
  `OriginGuard` see what they see from Claude Code. Tested in
  `McpStdioBridgeTests`.

## The transport

Streamable HTTP at `/mcp` on the dashboard's own server, written in
`McpServer.cs` rather than taken from an SDK: four methods are used
(`initialize`, `ping`, `tools/list`, `tools/call`), each request is answered
with one JSON object, a notification gets 202, and a GET is 405 because no
event stream is offered. There is no session id; the transport makes it
optional and nothing is kept between calls but the call log. Revisions
2025-11-25, 2025-06-18 and 2025-03-26 are answered in kind; a client asking for
one it does not list is offered 2025-11-25.

## Who can reach it

The same guards as the rest of the API, because it is the same server:

- **Another machine** cannot. `LanGuard` refuses every POST from off this
  machine, and an MCP call is a POST, so a dashboard opened to a tablet with
  `-Lan` does not open this door with it.
- **A web page** cannot. A page in the pilot's browser that POSTs to
  localhost declares its own Origin, and `OriginGuard` refuses it; a name that
  rebinds to 127.0.0.1 fails the Host check first. Tested in
  `McpEndpointTests`.
- **Software on this PC** can, while the switch is on. That is the point, and
  anything already running here has the files anyway.

What a tool returns is sent by the assistant to its own service, under that
service's terms. The Settings text says so.

## The tools

Every tool is a call onto the app's own API over loopback, then trimmed, so an
assistant reads exactly what the dashboard draws. Trimming is not optional:
on this install the routes table is 45 KB, the item catalogue 10 MB, the
mission list 1.5 MB and one ship's NPC variants 35 KB, so each tool searches
and cuts, and says how many there were.

Reading (with access on):

| Tool | From |
| --- | --- |
| `whats_happening` | `/api/now`: in game, place with confidence, ship, party, open contracts |
| `search` | `/api/search`: logs, what the pilot wrote, the catalogue |
| `item_details` | `/api/reference/item`, by id, else by name through search |
| `ship_details` | `/api/reference/ship`, NPC variants cut to a count and names |
| `where_to_buy` | `/api/shopping/sellers`, cheapest first |
| `trade_routes` | `/api/routes` with hold, budget, start and safety |
| `trade_here` | `/api/trade/advice` |
| `my_ships` | `/api/fleet`: flown ships and the Fleet Manager's owned count |
| `my_items` | `/api/stash`, by place or by item; presence, never counts |
| `my_plans` | `/api/trips`, `/api/jobs`, `/api/checklists`, the pilot's own only |
| `find_missions` | `/api/reference/missions`, unreleased ones left out |
| `find_places` | `/api/map` nodes, most visited first |
| `recent_sessions` | `/api/sessions` and `/api/earnings` |

Added in 1.2.50, the rest of the pilot's own pages:

| Tool | From |
| --- | --- |
| `my_spending` | `/api/spending`: shop purchases the logs record (71, 2,184,137 aUEC here) |
| `my_ledger` | `/api/ledger`, by kind and days, newest first |
| `my_earnings` | `/api/earnings` over a window |
| `garage` | `/api/garage`: the pilot's ships, or one ship's 35-odd slots cut to what is fitted, its grade and maker, stock or changed, plus the sheet's weapons, shield, power, cooling and quantum (9 KB for a Super Hornet, from 43 KB) |
| `garage_options` | `/api/garage/{ship}/options` for one slot, by portId or hardpoint |
| `my_loadout` | `/api/loadout`, slot by slot |
| `armoury` | `/api/armoury` (1 MB), searched by name, family or maker, pictures and finishes dropped, shops cut to five |
| `my_contracts` | `/api/contracts` (363 here, 180 KB), by outcome, contractor, system and days, with totals by outcome |
| `my_reputation` | `/api/reputation` ranks and `/api/standing` per contractor |
| `events` | `/api/events`: each track's points, tiers reached and points to the next |
| `wikelo` | `/api/wikelo`, by words in the trade or a needed item, or only trades ready |
| `logbook` | `/api/logbook`, by kind |
| `refinery` | `/api/mining/refinery`, each order with whether it is due |
| `deaths_and_medical` | `/api/casualties` |

Added in 1.2.51, behind a switch of its own that starts off:

| Tool | From |
| --- | --- |
| `my_crew` | `/api/crew` and `/api/crew/ships`: handles, sessions together, first and last seen, ships shared - a floor, as the Crew page says |

## Prompts

Since 1.2.52 the server offers prompts as well as tools: ready-made requests
a client shows by name. `McpPrompts.cs`:

| Prompt | Arguments | Needs |
| --- | --- | --- |
| `plan_trade_run` - Plan a trade run for my ship | ship, budget, from | nothing: without Ships it asks the pilot which ship, then reads `ship_details` |
| `fit_my_ship` - What should I fit on this ship | ship, goal | Ships |
| `debrief_last_session` - Debrief my last session | none | Sessions; contracts and ledger steps only with their areas open |

A prompt is instructions, not data: it tells the assistant which tools to call
and in what order, and the reads go through `tools/call`, where they are
checked against the switches and logged like any other. Pasting tool answers
into the prompt would have failed silently inside `prompts/get` when an area
was closed or a feed slow, and left no trace in the call log.

Each message is written against the switches as they stand. A prompt whose
areas are closed is not listed, and asked for anyway it is a protocol error
naming the area. A step needing a closed area is dropped or becomes a
question for the pilot. Saving (`create_flight_plan`, `create_shopping_list`)
is offered only with changes allowed, and then only on the pilot's yes. A
test builds every prompt under all 128 ways the six areas and the changes
switch can stand, and fails if a message names a tool those switches do not
offer: a model sent to a tool it was not given tries the call regardless.

Measured on 2026-11-16 against a copy of the data: `prompts/get` for
`plan_trade_run` with ship "Drake Cutter", handed to Claude Code (Haiku) as
the user's message, led to `ship_details` (4 SCU), `trade_routes` and
`find_places`. It answered with two routes, said the prices were UEX's
reports, flagged a sell price 29 hours old and, with changes off, listed the
stops rather than saving them. What was not checked: a client's own prompt
menu. Claude Code's `-p` mode does not expand `/mcp__quantumwake__...` (2.1.325
sent it to the model as text), and the interactive menu was not driven here.

## Areas

What the pilot owns is split into parts, each a box in Settings under the
access switch (`McpArea` in `McpSettings.cs`; which tool reads which is one
table, `McpTools.Areas`):

| Area | Tools | Also cut from other answers when off |
| --- | --- | --- |
| Sessions and history | `whats_happening`, `recent_sessions`, `logbook`, `deaths_and_medical` | visits and last visit on `find_places` (which then sorts by name, since visit order would tell what the field was cut to hide); "your logs" in `search` |
| Money | `my_spending`, `my_ledger`, `my_earnings` | the week's earnings on `recent_sessions`; bought and sold lines in `logbook` |
| Ships, gear and stashes | `my_ships`, `my_items`, `garage`, `garage_options`, `my_loadout` | `holding` on `item_details`; `flown` on `ship_details`; "have" and where on `wikelo`; loot in `logbook`; "your logs" in `search` |
| Contracts, reputation and events | `my_contracts`, `my_reputation`, `events`, `refinery` | open contracts on `whats_happening`; taken and completed on `find_missions`; a blueprint held on `item_details` |
| Plans and lists | `my_plans`, and every writing tool | "things you wrote" in `search`; a Wikelo trade's tracked list |
| People you have played with | `my_crew` | the party on `whats_happening`; the `crew` handles on each `recent_sessions` row |

The game's catalogue - `search`'s catalogue group, `item_details`,
`ship_details`, `where_to_buy`, `trade_routes`, `trade_here`, `find_missions`,
`find_places`, `armoury`, `wikelo` - is in no area: it is not the pilot's.

A closed area's tools leave the list and are refused at the call with the
area's name, the same double check as writing. Writing needs Plans open as
well as changes allowed: an assistant should not add to lists it may not see.

The setting is stored as the areas that are open, not the ones that are
closed, and null until the pilot first changes one. That way a part added in
a later version starts closed for anyone who has already chosen, while a new
install gets the defaults.

The crew list starts closed: on this install it is 131 other players'
handles, and a tool's answer leaves this PC with the assistant. Like changes,
it is a grant for an occasion, so turning access off closes it again; a part
the pilot closed stays closed, since keeping a "no" is never the risk. Before
1.2.51 the `crew` field on each `recent_sessions` row went out regardless.
Tested in `McpEndpointTests` and `McpSettingsTests`.

Writing (with changes allowed as well):

| Tool | Does |
| --- | --- |
| `create_flight_plan` | A new plan with stops and run-sheet actions; becomes the tracked plan. Places are matched to the map by exact name, and any that are not are kept as written and reported back. |
| `create_shopping_list` | A new list, its source "an AI assistant", so Shopping shows where it came from. |
| `add_to_shopping_list` | One line on a named list, else the list being filled. |
| `create_checklist` | A new checklist with its tasks. |
| `pin_to_now` | A shopping list or checklist on Now, or off it (1.2.50). |
| `tick` | A shopping line in hand or a checklist task done, or undone, by its words (1.2.50). |
| `add_stop` | One stop with actions at the end of an existing plan (1.2.50). |
| `track_flight_plan` | The plan the map, Now and overlay follow, or none (1.2.50). |
| `track_event` | An event kept on Now, or Now following play again (1.2.50). |
| `plan_haul_run` | The live session's hauling contracts as a plan, as the Contracts button does (1.2.50). |

The app's own routes for pin, tick and track toggle, because a button is
pressed once; these tools read the state first and change it only when it
differs, so an assistant asked twice does not undo itself. Tested in
`McpPilotToolTests`.

Nothing deletes and nothing rewrites what the pilot wrote. A writing tool is
refused at the call as well as left out of the list, so a client that listed
the tools before the switch went off still cannot write; the refusal says
which switch. A refusal from the app reaches the assistant as the app's own
sentence ("Nothing to plan: no open hauling contract names a place yet."),
not as JSON.

Measured on 2026-11-15 with Claude Code against a copy of the data: asked
which shield generator is on the Super Hornet Mk II and how many Head Hunters
contracts were completed, it called `garage` and `my_contracts` and answered
three AllStop generators and 61.

## What the pilot sees

Settings → AI assistants lists the last calls - in memory only, 40 kept, 12
shown - with the client's name from `initialize`, and marks the ones that
wrote. Turning access off also turns changes and the crew list off, so a
later yes to access is never silently a yes to writing or to other players'
names.

## Not done

- No resources: tools and prompts only.
- No event stream or server-sent notifications; nothing here needs to push.
- No remote connector: Claude's web and mobile apps reach servers on the
  internet, and this one answers only on this PC.
