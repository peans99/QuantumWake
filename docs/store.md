# The Microsoft Store package

Added in 1.0.1. The Store edition is the same `QuantumWake.exe` the release
publishes, packed as an MSIX by `installer/build-msix.ps1` from
`installer/msix/AppxManifest.xml`. The release workflow builds it after the MSI,
lets Defender scan it with everything else, and keeps it as a workflow artifact
named `store-package-<version>` for uploading to Partner Center.

---

## Why MSIX and not the MSI

The Store takes a desktop app either way, but an MSI or EXE has to be signed
with a certificate chaining to a Microsoft-trusted root, for the installer and
the files in it, and hosted at a URL whose file never changes. A certificate is
a yearly or monthly bill, and the key has to live on a hardware token or a
cloud HSM. An MSIX submitted to the Store is signed by the Store and hosted by
it, and the Store updates it. So the Store gets MSIX, and the MSI
([installer.md](installer.md)) stays a GitHub download for anyone who wants a
Start menu entry.

The package is therefore **unsigned**, and an unsigned MSIX installs nowhere but
through the Store. That is why it is a workflow artifact and never a release
asset: on the release page it would be a download that cannot be opened.

## What is different about a Store copy

**Updates come from the Store.** Store policy has the Store, not the app,
update a packaged app, and the package folder under `WindowsApps` is read-only
anyway. `StoreInstall.Is` (in Core) asks Windows for the process's package
identity, which only a packaged copy has; when it is set, the one-click update
stands down, the update banner offers "Get it from the Store" (the Store's
downloads page, which needs no Store id) and says the Store offers a release
only after certification, a day or two after GitHub has it. The update check
itself still runs, so the banner still appears.

**The data folder depends on what was there first.** A packaged app runs with
file-system virtualization on, and since 1.2.40 so does this one. What that
does was measured rather than read (see [Tested](#tested)), and it comes down
to whether `%LOCALAPPDATA%\Quantumwake` exists when the Store copy first runs:

| | someone who ran the download first | someone who only ever had the Store copy |
|---|---|---|
| the folder | already exists | does not |
| where files land | in `%LOCALAPPDATA%\Quantumwake` itself: new files, appends, replacements, deletes, new subfolders | in `%LOCALAPPDATA%\Packages\Nehkron.QuantumWake_6pqy8hx358xcy\LocalCache\Local\Quantumwake` |
| seen by Explorer, the CLI, the downloaded exe | yes | only at the `Packages` path |
| uninstalling the Store copy | leaves it | deletes it |

So moving from the download to the Store keeps every session and carries on in
the same folder. A Store-only history lives inside the package and goes when
the package does, which [privacy.md](privacy.md) says in as many words.

The app itself is shown the folder at `%LOCALAPPDATA%\Quantumwake` in both
cases, so a path it printed would point a Store-only user at nothing.
`StoreInstall.OnDisk` asks the disk which case this is and answers the path
Explorer can open; Settings → What was read and the controls export use it, and
the first adds that uninstalling deletes the folder when it is the package's.

### The capability the Store refused

1.0.1 to 1.2.39 turned virtualization off
(`desktop6:FileSystemWriteVirtualization` and `RegistryWriteVirtualization`
disabled) so that every copy shared the one folder. That needs the restricted
capability `unvirtualizedResources`, and certification denied it in November
2026 under policy 10.6.3, with this justification:

> Quantum Wake keeps its history in %LOCALAPPDATA%\Quantumwake, the folder the
> same app has used since its first GitHub release. Without this capability a
> Store copy would see none of a user's existing history, its own files would
> be invisible to the app's command-line tool and to Explorer (where the app
> sends users to find their bug reports and exports), and uninstalling would
> delete a history our privacy policy says survives uninstalling. Apart from
> .NET unpacking its native libraries into the temporary folder, the app writes
> nowhere else in the profile, and nothing in the registry.

The denial offered reconsideration with a fuller justification and a support
contact that is not a GitHub link. Removing the capability was chosen instead,
and the measurement above is why it costs less than that justification feared:
its first claim, that existing history would not be seen, turned out to be
wrong.

`runFullTrust` is now the only restricted capability: every desktop (Win32)
app in an MSIX declares it.

## Identity

`installer/msix/store-identity.json` holds the package's `Name`, `Publisher`
and publisher display name. They are not chosen here: Partner Center assigns
them when the name is reserved (*Product management → Product identity*), and
a package that disagrees is refused at upload. Reserved 2026-11-04:

| field | value |
|---|---|
| Name | `Nehkron.QuantumWake` |
| Publisher | `CN=D89B6871-849F-42BD-BB41-E382E98406F9` |
| Publisher display name | `Nehkron` |
| Package family name | `Nehkron.QuantumWake_6pqy8hx358xcy` |
| Store ID | `9NVQ4KN17KB8`, <https://apps.microsoft.com/detail/9NVQ4KN17KB8> |

The family name's suffix is a hash of the Publisher, so recomputing it checks
a Publisher copied by eye: SHA-256 of the UTF-16LE string, first 8 bytes, as
13 characters of `0123456789abcdefghjkmnpqrstvwxyz`. The value above gives
`6pqy8hx358xcy`, as Partner Center shows. `build-msix.ps1 -Release` still
refuses the placeholders, should the file ever be reset to them.

## Choices in the manifest

- **Version** is the release's three numbers plus `.0`: the Store requires the
  fourth field to be zero, and each submission must be higher than the last.
- **Minimum Windows 10.0.19041**, the overlay's own build target.
- **English only.** Every language a package declares needs a Store listing of
  its own, and the app itself is English only since French was retired in 1.2.29.
- **Logos are drawn at build time** from the 256-pixel frame of
  `src/Quantumwake.Overlay/app.ico`, so they cannot drift from the icon the exe
  and tray use.
- **MakeAppx** comes from Microsoft's `Microsoft.Windows.SDK.BuildTools` NuGet
  package, pinned to 10.0.28000.2705 by version and by the SHA-512 NuGet's
  catalog publishes for it, so neither a developer machine nor the runner needs
  the Windows SDK.

## Submitting

1. Reserve the name in Partner Center and copy the three identity values into
   `store-identity.json`.
2. Tag a release as usual ([releasing.md](releasing.md)). Download the
   `store-package-<version>` artifact from the release run.
3. In the submission's *Packages* page, upload the `.msix`. Partner Center
   validates it against the reserved identity. With `unvirtualizedResources`
   gone there is no restricted-capability question to answer.

## Tested

On this machine, packing the published 1.0.1 exe: MakeAppx accepts the
manifest (it checks it against the schema while packing).

An unsigned package installs only through the Store or, on a machine in
Developer Mode, by registering the unpacked folder with
`Add-AppxPackage -Register AppxManifest.xml`. For 1.2.40 the manifest was
registered that way on Windows 11 build 26300, under a test identity
(`Nehkron.QuantumWake.VirtProbe`) and around a stand-in exe, and commands were
run inside its container with `Invoke-CommandInDesktopPackage -PreventBreakaway`
from Windows PowerShell 5.1 (from PowerShell 7 the cmdlet hung):

- **A folder that existed beforehand**, holding three files: a new file, an
  append, a temp-file-then-move replacement, a delete and a new subfolder all
  landed in the real folder. Nothing appeared under `Packages`.
- **A folder that did not exist**: it and two files, one of them two folders
  deep, were created under `Packages\<family>\LocalCache\Local`, and the real
  path still did not exist afterwards.
- **The 1.2.40 server itself**, run inside the container against each kind of
  folder with `QUANTUMWAKE_DATA`: `/api/data` answered the `Packages` path
  with `packaged: true` for the new folder, and the real path with
  `packaged: false` for the existing one. The database, `names.json` and
  `controls` were where it said in both.
- **Removing the package** deleted its `Packages` folder and left the
  pre-existing one.

What that does not cover: the real `QuantumWake.exe` (the tray app and its
WebView2 cache) inside the package, the real identity, and any Windows older
than build 26300. Microsoft's description of virtualization for Windows 10 has
new files in an existing folder diverted too, which would split a download
user's folder between the two places; `OnDisk` reports each path where it is
either way, but that case is unmeasured here.
