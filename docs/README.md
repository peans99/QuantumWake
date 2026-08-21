# Star Citizen Log Tooling — Research

Research notes for building a Star Citizen `Game.log` UI.

Compiled 2026-08-20 against a real install: **SC 4.9.188.23497**, handle `nekron`,
144 log backups spanning **2026-04-20 → 2026-08-17 (402.5 MB)**.

## Contents

| Doc | What's in it |
|---|---|
| [findings.md](findings.md) | **Read this first.** The kill-event problem, with evidence. |
| [log-format-reference.md](log-format-reference.md) | Line formats verified present in the local logs, plus the removed ones. |
| [architecture.md](architecture.md) | Design decisions for Verselog and the reasons behind them. |
| [phase-1-core.md](phase-1-core.md) | Build log: the Core parser, what it verified, and the quirks it uncovered. |
| [phases-2-5.md](phases-2-5.md) | Build log: server, dashboard, map, overlay, and the dormant combat parser. |
| [log-simulator.md](log-simulator.md) | The fake-log generator: how to use it and why it reproduces the format's quirks. |
| [untapped-signals.md](untapped-signals.md) | Log signals we have not used yet, ranked, with formats and counts. |
| [commodity-names.md](commodity-names.md) | Why a cargo sale cannot be named, and where the mapping actually lives. |
| [credits.md](credits.md) | Every external resource this app uses, and what was taken from each. |
| [naming.md](naming.md) | Why the project is called Verselog, and what the name had to survive. |

The seven per-tool write-ups that used to sit in `docs/tools/` have been
removed. Everything worth keeping from them was already lifted into the docs
above — the archived combat patterns and StarLogs' classification tree into
[log-format-reference.md](log-format-reference.md), the per-patch event
availability into [findings.md](findings.md), and the design lessons into
[architecture.md](architecture.md). The summary and matrix below are the rest of
the residue. The originals are in git history if they are ever wanted back.

## The one-line summary

Six of the seven tools are built on `<Actor Death>` / `<Vehicle Destruction>`
log events. **Those events are not present in this install's logs** — not in the
current `Game.log`, and not in any of the 144 backups going back to April.
Any of them, run here today, shows an empty dashboard.

The two that still work (SCStats, SCPlay) are the two that never touched combat
data — they read session timestamps, ship usage, and loadout changes, all of
which are still logged in abundance.

## Comparison matrix

| Tool | Stack | Live tail | Reads backups | Depends on kill events | Works on this install |
|---|---|:---:|:---:|:---:|:---:|
| StarLogs | Python 3.8+ / Flask / SSE | ✅ | ✅ | ✅ | ❌ |
| AutoTrackR2 | C# / .NET 4.7.2 / XAML | ✅ | ❌ | ✅ | ❌ |
| SC-Kill-Monitor | Java / JavaFX | ✅ | ❌ | ✅ | ❌ |
| all-slain | Python 3.10+ | ✅ | ✅ | mostly | ⚠️ partial |
| citizenmon | Go | ✅ | ❌ | ✅ | ❌ |
| **SCStats** | desktop binary | ❌ | ✅ | ❌ | ✅ |
| **SCPlay** | Python/tkinter + C#/WinForms | ❌ | ✅ | ❌ | ✅ |

## Where that leaves a new build

The logs are rich — they're just not rich in *combat*. Four months of data
covers ships flown, locations visited, quantum routes, contracts accepted,
party activity, and session timing. See
[log-format-reference.md](log-format-reference.md) for the verified line
formats to build against.
