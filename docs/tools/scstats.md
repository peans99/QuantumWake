# SCStats — Maple33-hash/SCStats

**Repo:** https://github.com/Maple33-hash/SCStats

> "Local, read-only Star Citizen log analysis tool for gameplay insights and
> session statistics."

**One of only two tools in this set that still works against SC 4.9 logs** — and
it works precisely because it never touched combat data.

## What it does

Standalone desktop utility. Point it at a Star Citizen install directory and it
processes `Game.log` plus everything in `logbackups/` with no further
configuration. The game does not need to be running.

Metrics it produces:

- **Total playtime** across multiple sessions/launches
- **Loadout and equipment changes** over time
- **Favourite vehicle**, ranked by accumulated flight time
- **Purchases**, by correlating purchase *requests* with in-game *responses*

## Stack

Distributed as a prebuilt executable via GitHub Releases; the README does not
state the implementation language. No install step beyond download and selecting
the game directory.

## Design stance

The README leans hard on safety, and the claims are worth repeating because they
shape the design:

- read-only access to local log files
- no network communication
- no memory access, no code injection
- "does not interfere with AntiCheat software or modify the game process at all"

That is the correct posture for anything touching a live game install, and it's
the posture a new tool should adopt too.

## Why it survives 4.9

Its four metrics all derive from event families that are still abundant in
current logs:

| SCStats metric | Underlying signal | Count in this install's backups |
|---|---|---:|
| Playtime | session timestamps, loading screens | 421 loading-screen lines |
| Favourite vehicle | `<Vehicle Control Flow>` driver set/clear | 488 |
| Loadout changes | inventory/loadout-editor events | 1,386 location-inventory requests |
| Purchases | request/response correlation | — |

## Limitations

- **No live view.** Batch analysis only; nothing updates while you play.
- **No PU-specific detail** — no contracts, quantum routes, or locations.
- **Undocumented internals.** The README gives no parsing methodology, no
  patterns, and no explanation of how session boundaries are computed. Reverse
  engineering would require the source, which isn't described on the repo page.
- **Closed-ish distribution.** Binary-first; you're trusting the executable.

## Relevance to a new build

The closest existing tool to what's actually achievable on current logs. If the
goal is session statistics rather than combat, SCStats is the feature baseline to
match and then exceed — the obvious gaps being live tailing, contract tracking,
quantum route history, and location analytics.
