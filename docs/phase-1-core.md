# Phase 1 — Core parser

Status: **parsing complete and verified**. Persistence and the location state
machine are the remaining Phase 1 items.

## What exists

```
src/Quantumwake.Core/
  Logging/
    LogLine.cs          LogLine record + LogEnvelope.TryParse
    LogFileReader.cs    streaming reader, multi-line entry reconstruction, offset resume
    GameInstall.cs      LIVE/PTU/EPTU discovery across all fixed drives
  Events/
    GameEvent.cs        13 event records
  Parsing/
    LogEventParser.cs   tag dispatch + GeneratedRegex patterns + health stats
src/Quantumwake.Cli/
  Program.cs            backfill and verification harness
tests/Quantumwake.Tests/
  LogEnvelopeTests.cs   6 tests
  LogEventParserTests.cs 30 tests
  LogFileReaderTests.cs 10 tests
```

46 tests, all passing. Every fixture is a real line copied from a
4.9.188.23497 install rather than a synthesised example — synthetic fixtures
would have hidden all three quirks below.

## Design

**Envelope first, then dispatch on tag.** Most lines fail a dictionary lookup on
`<Tag>` and never touch a regex, which keeps a 403 MB pass cheap. Patterns use
`[GeneratedRegex]` source generators.

**One parser instance per file.** Session headers span several lines, so the
parser holds state; a truncated final line must not leak into the next file.

**Unknown tags are ignored, known tags that fail are counted.** That distinction
is the whole point: a tag CIG invents next patch is silently skipped, but a tag
we claim to handle failing its pattern is a defect and gets reported with a
sample line. `UnmatchedByTag` turns "1,928 lines failed" into a fixable list —
which is exactly how the three quirks below were found.

## The three quirks

Each was invisible to grepping and only appeared once a parser ran over the full
set. Details and samples in [log-format-reference.md](log-format-reference.md).

| Quirk | Cost if unhandled |
|---|---|
| Entries span multiple lines, continuations carry their own timestamp | 1,274 lost notifications (15%) |
| `<Calculate Route>` has a second, origin-less form | 652 lost routes (49%) |
| `<RequestLocationInventory>` has a "no inventory" failure variant | 2 false parse errors |

The multi-line one is worth dwelling on: because the continuation is stamped
with the *same* timestamp as its parent, no timestamp-based rule can separate
them. The working signal is an odd double-quote count, meaning the entry stopped
mid-string.

## Verification

Backfill over 144 backups + live `Game.log` (403 MB), ~30 s:

```
hud.notification    8325      session.spawned      483
location.inventory  1399      session.loading      429
quantum.route       1328      session.character    182
session.context     1316      session.login        149
contract.marker      742      session.start        145
quantum.target       641      ! unmatched            0
net.disconnect       591
vehicle.control      497
```

Counts were cross-checked against independent PowerShell greps and agree
exactly (see the reference doc). Ground truth from the plan also holds:

- handle resolves to `nekron`, GEID `204721322607`
- 145 session headers = 144 backup files + 1 live log
- ships include `DRAK Clipper` and `RSI Aurora_Mk2`
- **zero kill events**, as expected on 4.9 and 4.10

### Where the numbers differ from the plan, and why

The plan predicted `RR_MIC_LEO` at 518 visits and 11 incapacitation sessions.
Actual: **RR_MIC_LEO 518 → now counted only from `<RequestLocationInventory>`**,
and **22 incapacitations across 12 sessions**.

Both differences are the parser being more correct than the grep, not less:

- The original grep counted every `Location[...]` occurrence on any line. The
  parser counts only genuine inventory requests, and excludes the two
  `INVALID_LOCATION_ID` failures.
- The grep counted 95 raw `Incapacitated` string hits. Each notification fires
  3–5 times with differing `Action:` values, so after deduplicating on the
  notification id the real figure is 22 distinct incapacitations. The extra
  session over the grep's 11 comes from the live `Game.log`, which the earlier
  backup-only scan did not cover.

This is precisely the "notification triple-fire" trap the plan called out, and
it is why raw grep counts should never be shipped as statistics.

## Real data now available

- **22 ships flown** — MISC Starlancer_Max (126), ANVL Hornet_F7CM_Mk2 (58),
  DRAK Corsair (52), RSI Hermes (47), MISC Freelancer_MAX (46), ORIG 325a, …
- **73 locations** — RR_MIC_LEO, Stanton4_NewBabbage, Stanton2_Orison,
  Stanton1_Lorville, Pyro outposts, …
- **131 quantum destinations** — including `LOC_rs_ext_stan-pyro_jp1`,
  `OOC_Stanton_4_Microtech`, `NavPoint_Dynamic_*`
- **112 contract archetypes** — `Ling_Stanton_VeryEasy_RecoverCargo`,
  `Covalex_Stanton_VeryHard_RecoverCargo`,
  `HaulCargo_SingleToMulti2_RefinedOre_Titanium_Stanton4_SmallGrade`, …

The contract strings decompose cleanly into issuer, system, difficulty and type,
which is enough for the faceted contract view in Phase 2 with no extra data.

## Running it

```powershell
dotnet run --project src\Quantumwake.Cli -c Release -- --path "C:\Program Files\Roberts Space Industries\StarCitizen\LIVE"
dotnet run --project src\Quantumwake.Cli -c Release -- --live-only   # skip backups
dotnet test Quantumwake.slnx
```

Install detection is automatic when `--path` is omitted.

## Next

1. Location state machine (`Unknown → AtLocation → QuantumTravelling → AtLocation`)
   with confidence levels.
2. Location ID resolver over the 73 observed IDs and 131 destinations.
3. SQLite persistence with idempotent re-runs keyed on file content hash.
4. Live tailing on top of `LogFileReader.ReadFrom`, which already handles
   rotation.
