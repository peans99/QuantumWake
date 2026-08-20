# StarLogs — Ozy311/StarLogs

**Repo:** https://github.com/Ozy311/StarLogs · MIT · Tagline: "FOR THE CUBE!"

The most complete of the open-source log tools, and the best architectural
reference for a new dashboard — even though its event model no longer matches
current logs.

## What it does

Tails `Game.log` in real time, classifies combat events, and serves a dark-themed
web dashboard on `localhost:3111` with live updates, filtering, statistics, and a
historical browser for `logbackups/`.

## Stack

- **Python 3.8+**, Windows 10/11
- **Flask** backend, **Server-Sent Events** for live push
- Vanilla JS/HTML/CSS frontend
- Optional **TUI** console alongside the web UI
- Packaged with **Nuitka** (recommended) or **PyInstaller**

## Structure

```
starlogs.py         # entry point
web_server.py       # Flask server & API
log_monitor.py      # polling-based real-time file tail
event_parser.py     # regex pattern matching       ← the valuable file
game_detector.py    # LIVE/PTU/EPTU auto-detection
config_manager.py   # starlogs_config.json
static/  templates/ # frontend
```

## Run

```bash
git clone https://github.com/Ozy311/StarLogs.git
cd StarLogs
pip install -r requirements.txt
python starlogs.py
# → http://localhost:3111
```

CLI flags: `--version LIVE`, `--port 5000`, `--debug`.
Config is written to `starlogs_config.json` on first run (detected installs,
active version, web port, poll interval, custom paths, debug toggle).
TUI keys: `Q` quit, `O` options, `R` restart monitoring.

## Events it detects

| Event | Detail captured |
|---|---|
| PvE / PvP kills | weapon, weapon class, damage type, ship, attack direction vector |
| Deaths | killer, damage type, location |
| Vehicle destruction | soft death (level 1) vs full (level 2), **crew correlation within 200 ms** |
| FPS combat | on-foot PvE/PvP kills and deaths |
| Disconnects | network disconnection |
| Actor stalls | freeze/crash incidents, with stall length |

Full regexes and the classification decision tree are reproduced in
[../log-format-reference.md](../log-format-reference.md).

## Notable engineering details worth stealing

- **Crew correlation.** Actor deaths within 200 ms of a vehicle destruction get
  attached to it, so a ship kill shows its passenger list rather than five
  unrelated death rows. Good idea; applies equally to non-combat correlation.
- **NPC heuristics.** Substring matching (`PU_`, `AI_`, `Kopion_`,
  `NPC_Archetypes`, …) plus fallbacks on name length > 40 and ≥ 3 hyphens.
  Crude but effective, and reusable for any entity-name classification.
- **Multi-install detection.** Handles LIVE/PTU/EPTU with a custom-path escape
  hatch — worth copying, since hardcoding the LIVE path is a common failure.
- **Reprocess + historical browse.** Same parser runs over `logbackups/` for
  backfill, not just the live tail.
- **HTML export** produces a standalone offline report.

## Status against SC 4.9

**Non-functional for its primary purpose here.** Every combat event type it
detects is absent from this install's logs (see [../findings.md](../findings.md)).
Its disconnect handling would still fire, but the dashboard's reason for existing
would be empty.

What survives is the *architecture*: file tailing, SSE push, multi-version
detection, config management, backfill, export. That skeleton with a rewritten
`event_parser.py` targeting the events that still exist is a genuinely good
starting point.
