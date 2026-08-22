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

## Release notes

Written for someone deciding whether to update, not for the commit log. What
changed and what it means for them; nothing about refactors they cannot see.
The newest version goes directly under the `## Release notes` heading at the
bottom of `README.md`, and the release workflow lifts that section verbatim.

## Running the tests

```powershell
dotnet test Quantumwake.slnx -c Release        # both suites
```

`Quantumwake.Tests` covers the parser, resolvers and stores.
`Quantumwake.WebTests` runs `web/app.js` under a JavaScript engine with a stub
document, so the dashboard's own logic is tested rather than eyeballed. Add to
the second one when changing `web/` — it is the only thing standing between a
broken panel and a screenshot nobody took.

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

Two traps. The server serves `bin\Release\net10.0\web`, **not** the repo's
`web/` — rebuild after every asset edit, and stop the server first or the copy
is locked. And stub `window.EventSource` in any throwaway page you drive, or it
never settles.

## House style

- Comments say **why**, not what. The interesting comment is the one explaining
  the constraint that made the obvious approach wrong.
- Every doc claim is evidence from the logs in this install, with the number
  attached. If it cannot be checked, it is not stated.
- The app says what it cannot support: inferred locations carry a confidence,
  estimates are labelled, and a missing signal gets an explanation rather than a
  bare zero. Keep that up in anything new.
