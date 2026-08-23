# Agents

**Read [CLAUDE.md](CLAUDE.md) first.** It is the standing agreement for anyone
working here — human or otherwise — and it is not Claude-specific despite the
name. This file exists so agents that look for `AGENTS.md` find it.

Nothing below replaces that document. It is the short list of what goes wrong
most often, for an agent that is about to start writing.

## Before you write anything

- **`C:\Quantumwake` is the repository.** `C:\claude` is a convincing stale
  snapshot, dozens of commits behind. Check `git log --oneline -1`.
- The main checkout usually has work in progress. Do not switch its branch;
  take a detached worktree and remove it when you are done.
- Branches are pushed freely. Pull requests and merges to `main` happen only
  when Nicolas asks.

## Versioning

Raise the **patch** in `Directory.Build.props` with every change, in the same
commit as the work, and rename the release-notes heading in `README.md` to match
rather than opening a second one. Major and minor move only when Nicolas says
so.

## The three things that fail silently

1. **`PayloadVersion`.** Change the parser, the events, `Session.cs` or
   `SessionBuilder.cs`, and bump it in `src/Quantumwake.Data/SessionStore.cs` in
   the same change. Cached sessions are skipped by fingerprint, so without it
   the new field is invisible to every install that has run before — and nothing
   goes red. CI now catches it; `No-payload-bump: <reason>` waives it when
   nothing stored changed.

2. **The release-notes section.** The workflow lifts only the `###` section
   matching the version being tagged. Anything under a different heading ships
   unmentioned.

3. **A parser that passes its tests and does not read the install.** Run the CLI
   over the real backups and check `! unmatched known tags` is 0.

## Prove it, then say it

Every number in a comment, doc, release note or commit message came from running
something. Not "this should now handle purchases" — "13 purchases parse,
unmatched tags drop to zero, and each one's aUEC-per-SCU agrees with the field
the game printed beside it." Where a claim cannot be checked, it is not made.

The strongest check is one the code did not use: a unit conversion verified
against a field the parser never reads catches a hundredfold error that still
looks like a plausible integer.

## Do not disturb the machine you are working on

The app is usually running against `%LOCALAPPDATA%\Quantumwake`, and that is
Nicolas's real data. Copy it and set `QUANTUMWAKE_DATA` to the copy. If a build
fails because the Overlay DLLs are locked, build the individual projects — do
not close his app to unblock yourself.

## Look at the thing you built

Tests do not show a clipped button, a control that only appears on hover, or a
page that renders blank because a local variable shadowed `window.history`. All
three happened here and all three were caught by taking a screenshot and reading
it. CLAUDE.md has the headless-Chrome recipe and its two timing traps.

## House style, in one line each

- Comments explain the constraint that made the obvious approach wrong.
- A missing signal gets an explanation, never a bare zero.
- A number that is a floor is called a floor.
- Negative results get written down so nobody investigates them twice.
