# Releasing

Every release is a version bump plus a tag. The pipeline does the rest.

## The rule

**The version is bumped in the same pull request as the work, or in one of its
own before tagging.** `Directory.Build.props` is the single declaration —
`Version`, `AssemblyVersion` and `FileVersion` — and everything else reads from
it.

This is enforced, not trusted: `release.yml` refuses to build when the tag and
`Directory.Build.props` disagree.

```
Tag v0.1.2 wants 0.1.2, but Directory.Build.props declares 0.1.1.
Bump the version, merge it, then tag the merge commit.
```

The check exists because the failure is quiet otherwise. Publishing passes
`-p:Version` from the tag, so a forgotten bump still produces a correctly
stamped binary — while the repository, and the About page of anyone who builds
from source, keep claiming the previous number.

## The steps

```powershell
# 1. Bump Directory.Build.props, on a branch, as its own pull request.
#    Never on main directly.

# 2. Merge it.

# 3. Tag the merge commit and push the tag.
git checkout main
git pull --ff-only origin main
git tag -a v0.1.2 -m "Quantum Wake 0.1.2

<what changed, in the words of someone who has to decide whether to update>"
git push origin v0.1.2
```

Pushing the tag is the trigger. Nothing else needs doing.

## What the pipeline does

[`.github/workflows/release.yml`](../.github/workflows/release.yml), on any
`v*` tag:

1. **Checks the tag against `Directory.Build.props`** and stops if they differ.
2. **Runs the tests.** This app is a log reader, so a red parser fixture means a
   release that shows empty views — worse than no release.
3. **Publishes `QuantumWake.exe`**: self-contained, single file, compressed, with
   the dashboard embedded. It asserts the publish directory holds exactly one
   file, so the "one file, no runtime" promise cannot quietly rot.
4. **Publishes the CLI** framework-dependent, for parser-health checks.
5. **Attaches both** to the release: the bare `.exe` for one-click download, and
   a zip with the exe, the CLI, `README.md`, `LICENSE` and `NOTICE`.
6. **Writes the release notes**, including the SmartScreen warning and what to
   click, since the binary is unsigned.

`workflow_dispatch` accepts a tag as input, for re-running a release that failed
after the tag was already pushed.

## If a tag needs moving

A squash or rebase merge leaves a tag pointing at a commit that is no longer in
`main`'s history. Re-cut it rather than releasing from a dangling commit:

```powershell
git tag -d v0.1.2
git push origin :refs/tags/v0.1.2
git tag -a v0.1.2 -m "..." <the merged commit>
git push origin v0.1.2
```

Merge commits — the default here — keep the tagged commit reachable, which is
why `v0.1.0` survived its pull request.
