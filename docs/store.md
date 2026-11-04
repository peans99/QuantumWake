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

**The data folder is the same one.** A packaged app's writes to
`%LOCALAPPDATA%` are normally diverted into the package's private folder, and
merged back only for the app itself. Here that would mean Explorer, the CLI and
the downloaded exe not seeing what the Store copy wrote, and uninstalling the
Store copy deleting it, against what [privacy.md](privacy.md) promises. The
manifest turns that off (`desktop6:FileSystemWriteVirtualization` and
`RegistryWriteVirtualization` disabled), so the Store copy reads and writes
`%LOCALAPPDATA%\Quantumwake` exactly as the exe does. Moving from the download
to the Store keeps every session.

That needs the **restricted capability `unvirtualizedResources`**, which Partner
Center asks to have justified at submission. The justification:

> Quantum Wake keeps its history in %LOCALAPPDATA%\Quantumwake, the folder the
> same app has used since its first GitHub release. Without this capability a
> Store copy would see none of a user's existing history, its own files would
> be invisible to the app's command-line tool and to Explorer (where the app
> sends users to find their bug reports and exports), and uninstalling would
> delete a history our privacy policy says survives uninstalling. Apart from
> .NET unpacking its native libraries into the temporary folder, the app writes
> nowhere else in the profile, and nothing in the registry.

`runFullTrust` is the other restricted capability: every desktop (Win32) app
in an MSIX declares it.

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
  its own; `fr-fr` goes in when the French listing does.
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
   validates it against the reserved identity.
4. Answer the restricted-capability question with the justification above.

## Tested

On this machine, packing the published 1.0.1 exe: MakeAppx accepts the
manifest (it checks it against the schema while packing). Installing it has not
been tried: an unsigned package installs only through the Store or, on a
machine in Developer Mode, by registering the unpacked folder with
`Add-AppxPackage -Register AppxManifest.xml`. Before the first submission,
that registration is the check to run: the app starts, the banner offers the
Store, and new files land in `%LOCALAPPDATA%\Quantumwake` rather than under
`%LOCALAPPDATA%\Packages`.
