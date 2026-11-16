# AI assistants (MCP)

Since 1.2.48 Quantum Wake can be used by an AI assistant - Claude Code, Codex,
or any client of the [Model Context Protocol](https://modelcontextprotocol.io) -
on the same PC. It is **off until the pilot turns it on** in Settings → AI
assistants, and writing is a second switch under the first, also off.

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

Measured: Claude Code connected to a test copy on 2026-11-15, called
`find_places` and `where_to_buy`, and answered from the pilot's data; the
call log recorded it as `claude-code`. Codex was not installed on the machine,
so its line is the documented `url` form for a Streamable HTTP server and has
not been run here.

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

The crew list is left out on purpose: it is 131 other players' handles here,
and a tool's answer leaves this PC with the assistant.

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
wrote. Turning access off also turns changes off, so a later yes to access is
never silently a yes to writing.

## Not done

- No resources or prompts: tools only.
- No event stream or server-sent notifications; nothing here needs to push.
- Claude Desktop needs a local stdio server or a remote connector, and this is
  neither; Claude Code, Codex and other Streamable HTTP clients work.
