# Working on Quantum Wake

Standing agreements for anyone — human or Claude — picking up work here.
Everything else lives in [docs/](docs/README.md); this is the shortlist that
gets forgotten.

## Where the work happens

**`C:\Quantumwake` is the repository.** There is a stale snapshot of the same
project at `C:\claude` that looks convincing and is dozens of commits behind.
Anything built there is wasted work. Check `git log --oneline -1` against
`C:\Quantumwake` before writing code.

The main checkout usually has a branch in progress with uncommitted work, so do
not switch it. Take a worktree instead:

```powershell
git worktree add --detach C:\qw-<name> <branch>   # work here
git branch -f <branch> <new-commit>               # hand it back when done
git worktree remove C:\qw-<name>
```

Detached is deliberate: a branch checked out in a worktree cannot be checked out
in `C:\Quantumwake`, and the whole point is that the work can be tested there.
**Always remove the worktree when finished** and say the branch is free.

Branches are pushed freely. Pull requests and merges to `main` happen only when
Nicolas asks.

## Before a release

In this order, no steps skipped:

1. **Merge every outstanding branch together** into one integration branch named
   for the build being cut — `release/0.6.0` — so there is a single thing to
   test rather than a pile of branches.
2. **Check it out in `C:\Quantumwake`** and leave it there. Releases are tested
   from the real folder, against the real install, before they go anywhere.
3. **Run every test.** Both suites, green, no exceptions:
   ```powershell
   dotnet test Quantumwake.slnx -c Release
   ```
   A red parser fixture means a release that shows empty views, which is worse
   than no release.
4. **Write the release notes into `README.md`**, at the bottom, under the
   version being cut. Same words go in the GitHub release — the workflow reads
   that section out of the README, so it is written once.
5. Then follow [docs/releasing.md](docs/releasing.md): bump
   `Directory.Build.props`, merge, tag, push the tag. The pipeline refuses to
   build when the tag and the version disagree.

## Two version numbers, not one

`Directory.Build.props` is the release version, and the release workflow refuses
to build when the tag disagrees with it. That one is enforced and hard to
forget.

**Raise the patch with every change** — 0.7.1, 0.7.2, 0.7.3 — as part of the
same commit as the work. Major and minor move only when Nicolas says so; never
decide on your own that something is big enough to be 0.8.

Rename the release-notes heading in `README.md` as you go rather than opening a
second one, so there is always exactly one section and it always matches the
version in the props file. That is what keeps the workflow able to find it: it
lifts the section matching the tag and nothing else, and a version that quietly
grew its own heading is a feature nobody reads about.

`PayloadVersion` in `src/Quantumwake.Data/SessionStore.cs` is the one that gets
forgotten, and forgetting it fails **silently**. Backups are skipped by
fingerprint, so a session summarised before a field existed keeps that summary
for ever: the parser reads the new thing, the page asks for it, and every
install that has run before shows nothing. The installs with the most history
see the least. It has shipped that way twice — medical beds in 0.6, commodity
purchases in 0.7.

Touch the parser, the events, `Session.cs` or `SessionBuilder.cs`, and move
`PayloadVersion` in the same change. CI fails the pull request otherwise. When
nothing stored actually changed — a rename, a comment — waive it with a commit
trailer on its own line:

```
No-payload-bump: renamed a private field, nothing new is read
```

`SchemaVersion` beside it answers a different question: bump that only when a
stored payload can no longer be *read*, because a mismatch drops the table
rather than merely re-reading the logs.

## Release notes

Written for someone deciding whether to update, not for the commit log. What
changed and what it means for them; nothing about refactors they cannot see.
The newest version goes directly under the `## Release notes` heading at the
bottom of `README.md`, and the release workflow lifts that section verbatim.

**It lifts only the section matching the version being tagged.** Everything
shipping in a release has to be in that one section — a separate `### 0.6.13`
heading once meant a whole feature shipped unmentioned, because 0.6.13 never got
a tag of its own.

`README.md` is byte-sensitive: it carries em dashes and middots, and a
byte-level `sed` repair once mangled 64 of them. Splice whole lines with
`head`/`tail` rather than running regex over punctuation in place, and check
afterwards — `grep -c $'\xef\xbf\xbd' README.md` must print 0.

## Running the tests

```powershell
dotnet test Quantumwake.slnx -c Release        # both suites
```

`Quantumwake.Tests` covers the parser, resolvers and stores.
`Quantumwake.WebTests` runs `web/app.js` under a JavaScript engine with a stub
document, so the dashboard's own logic is tested rather than eyeballed. Add to
the second one when changing `web/` — it is the only thing standing between a
broken panel and a screenshot nobody took.

## Then check it against the real logs

Green tests mean the fixtures still parse. They do not mean the app reads *this*
install. The CLI is the harness for that:

```powershell
dotnet run --project src\Quantumwake.Cli -c Release
```

It parses every backup — 151 files, ~420 MB, a few seconds — and prints
per-event counts and any unmatched tags. **`! unmatched known tags` should read
0.** A parser change is not finished until it has run against that corpus.

Then check the number it produced against something it did not use. When
commodity buying started parsing, the give-away was that price ÷ SCU matched
`shopPricePerCentiSCU`, a field the parser never reads — which is what caught
the centi-SCU unit and would have caught a hundredfold error that still looked
like a plausible integer.

## Seeing it actually run

The browser automation tools cannot reach this machine's localhost, and there is
no Node here. What works:

1. `dotnet run --project src\Quantumwake.LogSim -c Release -- --install <temp>`
   builds a fake install, so no game is needed.
2. Run the built server against it — or with no `--path` at all to pick up the
   real install.
3. Screenshot with headless Chrome and read the PNG:
   ```powershell
   chrome.exe --headless=new --disable-gpu --window-size=1700,1150 `
     --virtual-time-budget=12000 --screenshot=out.png <url>
   ```

Two traps about the screenshot itself, and both cost an afternoon each.

**The PNG lands seconds after Chrome exits zero.** Checking for the file
immediately afterwards reports nothing, and reports it convincingly — a whole
session was written off as "headless Chrome is broken on this machine" when
several of the shots had in fact been taken. Wait for the file, or retry until
it exists, rather than trusting the exit code and an `ls`.

**A `MutationObserver` left connected stops the virtual clock settling**, so no
screenshot is produced at all. To drive a page, wrap the render function once
and stand down; observing the document keeps it alive for ever.

Two more about the setup. The server serves `bin\Release\net10.0\web`, **not**
the repo's `web/` — rebuild after every asset edit, and stop the server
first or the copy is locked. And stub `window.EventSource` in any throwaway
page you drive, or it never settles.

Two more traps once a page has to be *driven* rather than merely loaded. Chrome's
virtual clock stalls while the live stream holds a request open, so `setTimeout`
never fires — hang the harness off a `MutationObserver` instead. And a table that
re-renders when its data lands will discard a panel you opened a moment earlier,
so reopen until it sticks rather than clicking once.

Rendering earns its keep. It has caught a button clipped off-screen by
`margin-left:auto` inside a table wider than its panel, a control left invisible
because it only appeared on hover, and a page that rendered completely blank
because a local named `history` shadowed `window.history` for a whole function.
None of those show up in a diff.

### Driving it against real data without touching Nicolas's

The most useful screenshots come from the real install, and the real install is
his: the app is usually running on 31337 against
`%LOCALAPPDATA%\Quantumwake`, and anything written there is his data, not a
fixture. Do not stop it to get a build through either — when the Overlay DLLs
are locked, build the individual projects rather than the solution. So copy the
data and point a second server at the copy:

```powershell
Copy-Item "$env:LOCALAPPDATA\Quantumwake\*" "$env:TEMP\claude\qw-data" -Recurse
$env:QUANTUMWAKE_DATA = "$env:TEMP\claude\qw-data"
.\src\Quantumwake.Server\bin\Release\net10.0\Quantumwake.Server.exe --Port=31395
```

Everything then behaves as it does for him — 148 logs, a real fleet, real
receipts — while jobs, trips and re-downloaded reference data land in the copy.
Re-digesting the community dataset (`POST /api/community/enable`) is a ~50 MB
download and the only way to regenerate a digest after changing one.

When a page has to be driven rather than merely loaded, stub `window.fetch` for
non-GET calls in the throwaway page and collect what it was asked to post: the
click path gets tested without writing anything anywhere.

## House style

- Comments say **why**, not what. The interesting comment is the one explaining
  the constraint that made the obvious approach wrong.
- Every doc claim is evidence from the logs in this install, with the number
  attached. If it cannot be checked, it is not stated.
- The app says what it cannot support: inferred locations carry a confidence,
  estimates are labelled, and a missing signal gets an explanation rather than a
  bare zero. Keep that up in anything new.
- A number that is a floor is called a floor. The Crew page leads with the fact
  that it cannot see anyone who never disconnected, because the alternative is a
  count that looks complete and is not.
- A negative result is worth writing down. Freight looked like six thousand
  lines of cargo tracking and turned out to be loading-platform noise; that is
  recorded in `docs/untapped-signals.md` so nobody spends the day again.
