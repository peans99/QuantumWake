# The Windows installer

Added in 1.0. Every release carries `QuantumWake-<version>-win-x64.msi`
beside the bare `QuantumWake.exe`: the same file, byte for byte, installed. The
source is `installer/QuantumWake.wxs`, built by `installer/build.ps1` with WiX
5.0.2 from the repository's tool manifest (`dotnet-tools.json`). The release
workflow runs the script after publishing; CI runs it on every pull request
around a stand-in exe, so a source that stops building or validating is found
before a tag.

It exists for the Microsoft Store, which takes a desktop app as an MSI or EXE
installer that installs silently from a fixed URL, and for anyone who would
rather have an app in the Start menu than an exe in a folder.

---

## What it does

- Installs per-user into `%LOCALAPPDATA%\Programs\Quantum Wake`, never
  per-machine, so it never asks for an administrator. The app keeps all of its
  data in the profile already, and the in-app updater replaces the exe where it
  stands, which it can only do in a folder the user owns.
- Adds a Start menu shortcut, and an Apps and features entry named
  "Quantum Wake" with the version, the publisher, the install folder and links
  to the project and its issues.
- Replaces an older version on install (a major upgrade on a fixed
  `UpgradeCode`), and refuses to install an older version over a newer one.
- On uninstall, removes the exe, the shortcut and its own two registry values
  under `HKCU\Software\Quantum Wake\Installer`, and nothing else. In particular
  it leaves `%LOCALAPPDATA%\Quantumwake`, the user's history, which
  docs/privacy.md promises.

## Why it is built the way it is

**Per-user directories need registry key paths.** A file in the user's profile
cannot be its component's key path (ICE38), so each component is keyed on a
value under HKCU, and each folder it creates is named for removal on uninstall
(ICE64). `RemoveFolder` only removes an empty folder, so `Programs` survives
other apps in it.

**The install folder is spelled out.** `ProgramFilesFolder` redirects to
`%LOCALAPPDATA%\Programs` only for dual-purpose packages; this one is per-user
and nothing else, so the folder is under `LocalAppDataFolder` by name.

**Two GUIDs never change.** The `UpgradeCode` (66EBDDB8-...) is what makes each
version replace the last, and the main component's GUID (46812A73-...) is fixed
because WiX cannot generate one for a component holding both a file and a
registry key path. Changing either makes the next version install beside the
old one.

**WiX 5, not 6.** WiX 6 moved to a licence with a maintenance fee for some
users; 5.0.2 is MS-RL and does everything this needs.

**The version must be three numbers.** Windows Installer compares only
major.minor.build, so the script refuses anything else.

**One validation warning is expected.** ICE91 says the exe "won't be copied to
each user's profile even if a per machine installation is desired". That is
true of every per-user-only package and does not apply to this one.

## What was tested, on 2026-11-04

On this machine, with the 0.22.3 exe published the way the release publishes
it (127,120,198 bytes; the MSI is 115,494,912):

| step | result |
|---|---|
| `msiexec /a` (administrative extract, no install) | payload byte-identical to the published exe |
| `msiexec /i 0.22.3 /quiet` | exit 0; exe, Start menu shortcut, HKCU values; Apps and features shows 0.22.3, nekron, the folder |
| `msiexec /i 0.22.4 /quiet` over it | exit 0; one entry, now 0.22.4 |
| `msiexec /i 0.22.3 /quiet` over that | refused (1603); still 0.22.4 |
| `msiexec /x /quiet` | exit 0; exe, folder, shortcut, registry key and entry all gone |
| `%LOCALAPPDATA%\Quantumwake` | the same 37 entries, sizes and times before and after |

The installed copy was never started, so no data of the machine's own was
read or written by it.

## What it does not do yet

- **It is not signed.** Windows warns about an unknown publisher, as it does
  for the bare exe. The Store requires a certificate chaining to a
  Microsoft-trusted root, for the MSI and the files in it.
- **The in-app updater does not know about it.** Updating from inside the app
  replaces the installed exe in place, which works, but Apps and features keeps
  showing the version the MSI installed until the next MSI goes over it. A
  repair from Apps and features would put the installed version's exe back.
  Updating through the MSI keeps both in step.
- **It does not close a running app.** Uninstalling or upgrading while the app
  is in the tray leaves Windows Installer to deal with a file in use, which a
  silent install answers by scheduling the replacement for the next restart.
  Quit the app first.
- **It has no screens.** Double-clicking it shows Windows Installer's own
  progress bar and nothing else: no licence page, no choice of folder, no
  "launch now".
