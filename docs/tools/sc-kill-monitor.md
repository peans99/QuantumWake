# SC Kill Monitor — greluc/SC-Kill-Monitor

**Repo:** https://github.com/greluc/SC-Kill-Monitor ·
**Mirror:** https://gitlab.com/greluc-oss/sc/sc-kill-monitor · latest release v1.6.0

The narrowest tool of the set: it answers exactly one question — *who killed me?*

## What it does

> "SC Kill Monitor is an application to search the Star Citizen game.log file for
> the person who killed you."

The stated purpose is evidentiary rather than statistical: the README frames it
as a way to gather information for **reporting potential griefing or stream
sniping**. It isn't trying to be a killboard or a stats dashboard — it surfaces
the identity of your killer so you can file a report.

## Stack

**Java** with a **JavaFX** UI, per the repository's topic tags and logo assets.
Distributed as versioned releases; installation and operating manuals live in the
project [wiki](https://github.com/greluc/SC-Kill-Monitor/wiki) rather than the
README.

## Parsing

The README does not publish the regex patterns or log line formats it matches.
Given its single purpose, it necessarily keys on the `<Actor Death>` /
`CActor::Kill` line, filtered to entries where the victim is the local player —
extracting the `killed by '<name>'` capture. The archived format is in
[../log-format-reference.md](../log-format-reference.md).

## Status against SC 4.9

**Non-functional here.** It depends on a single event that this install never
emits — zero `<Actor Death>` and zero `killed by` lines across 402.5 MB and 144
files (see [../findings.md](../findings.md)).

This tool is the clearest illustration of the fragility the plan's "parser
health" panel is meant to catch. Because it parses exactly one line format, the
removal of that format takes the entire application with it — there is no
degraded mode. Compare all-slain, which parses a dozen event families and lost
only some of them.

## Relevance to a new build

Little directly reusable, but two points worth carrying forward:

- **Purpose-built beats general-purpose for user trust.** A tool that answers one
  question clearly is easier to reason about than a dashboard of twenty numbers.
  Worth remembering when designing the overlay's compact mode, where space forces
  the same discipline.
- **Never build on a single event.** Architect so that any one pattern going away
  degrades a feature rather than killing the app.
