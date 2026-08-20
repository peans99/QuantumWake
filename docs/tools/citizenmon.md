# citizenmon — danieldeschain/citizenmon

**Repo:** https://github.com/danieldeschain/citizenmon · GPL-2.0 · "Killfeed Monitor for Star Citizen"

The smallest project of the seven — around 17 commits — and the only one written
in Go.

## What it does

Monitors `Game.log` and surfaces kill messages as a killfeed. The author's stated
motivation is that this should eventually be an in-game feature; the tool exists
to fill the gap until then.

## Stack

**Go**, with a conventional layout:

```
cmd/monitor/   # executable entry point
pkg/           # reusable packages
go.mod go.sum
```

That split is a deliberate choice worth noting: the parsing logic sits in `pkg/`
as a library, separate from the `cmd/` binary. Of all seven tools this is the
cleanest separation between "parse the log" and "present the results" — the same
separation our plan enforces with `SCCompanion.Core` vs the Server/Overlay
shells.

## Parsing

Regex patterns and line formats are not documented in the README. It keys on the
kill events described in [../log-format-reference.md](../log-format-reference.md).

## Status against SC 4.9

**Non-functional here** — kill events are absent from this install's logs
entirely (see [../findings.md](../findings.md)). A killfeed with no kills is a
blank window.

## Relevance to a new build

- **Go's file tailing** is a reasonable reference if we ever want a headless
  cross-platform collector, though .NET's `FileSystemWatcher` plus offset
  tracking covers our needs and matches the chosen stack.
- **Library/binary split.** `pkg/` vs `cmd/` is exactly the seam we want between
  `SCCompanion.Core` and its consumers, and citizenmon is a small enough codebase
  to be a readable example of it.
- **GPL-2.0** — note the licence before borrowing any code directly. The other
  tools in this set are more permissive (StarLogs is MIT).
