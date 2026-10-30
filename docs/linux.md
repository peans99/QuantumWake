# Linux

Partly built. Quantum Wake on Linux, the line after 0.19.9 (`dev0190`).

The game runs on Linux under Wine or Proton and writes the same `Game.log` it
writes on Windows, just inside a Wine prefix. Everything Quantum Wake knows comes
from that file, the game's archive and screenshots the pilot saved, so on Linux
it can show almost everything the dashboard shows on Windows. The things that
cannot come over are the ones that need a Windows desktop.

## Scope

**In:** the server and the web dashboard, the CLI, reading the logs and the game
archive, the clipboard, and, as a later step, screenshots.

**Out:** the overlay, the MFD HUD and live joystick input. All three are WPF or
WinRT, and none of them is what a Linux pilot is missing. The Controls page
still works without a joystick reader: `IJoystickReader` is optional and the page
shows the bindings profile alone.

## What already holds

Measured 2026-10-29 on WSL Ubuntu (SDK 10.0.113), with the tree copied onto the
Linux filesystem rather than run from `/mnt/c`, so paths are case-sensitive the
way a real Linux disk is.

- **Server, Core, Data, CLI and LogSim target plain `net10.0`.** Only
  `Quantumwake.Ocr`, the Overlay and `Quantumwake.OcrTests` target
  `net10.0-windows10.0.19041.0`.
- **Screen reading, the clipboard and joysticks reach the server only as
  interfaces** (`IScreenReader`, `IClipboardReader`, `IJoystickReader`), passed
  into `ServerHost.Build` by the overlay. The standalone server already runs with
  none of them, and the screen endpoints already say "cannot read" instead of
  failing.
- **The NuGet packages are cross-platform:** `Microsoft.Data.Sqlite` ships Linux
  native libraries, and `ZstdSharp.Port` is fully managed.
- **Nothing in Server, Data or Core starts a process** or calls into a Windows
  DLL.
- **`Quantumwake.WebTests`: 1,265 of 1,265 pass.**
- **`Quantumwake.Tests`: 1,729 of 1,737 pass.** The eight failures have two
  causes, described next.

## The eight failures

Fixed in 0.19.10.

**Archive entry names are Windows paths, and `Path` stops treating `\` as a
separator on Linux** (3 failures, real bugs). Names inside `Data.p4k` look
like `Data\UI\SharedAssets\PaintColorLogos\...`. On Linux,
`Path.GetFileNameWithoutExtension` reads that whole string as a single file
name, so every stem comes out wrong. As a result:

- `GamePaintsTests`: 2 failures. The default liveries vanish.
- `ArmouryKitTests`: 1 failure. Attachments lose their icons.

Nine call sites in `Core/GameData` and `Core/Controls` handed an archive name
to `Path`: in `GameArmoury`, `GameMakerLogos` (2), `GamePaints` (2),
`GameReputation` (2), `GameVehicles` and `GameControls`. The other two `Path`
calls there are on real disk paths and stay. All nine now go through
`P4kArchive.EntryFileName` and `EntryStem`, which split on both separators. On
Windows `Path` already split on both, so nothing stored changes there and no
cache version moves.

**A test fixture roots itself at `C:\Games\StarCitizen\LIVE`** (all 5
`StarStringsArchiveTests.The_two_things_the_mod_is_are_allowed` cases). On
Linux that path is not rooted, so `GetFullPath` resolves it against the
working directory. This is a test-only fault: the fixture now builds its root
from `Path.GetTempPath()`. The installer itself was already safe, since
`StarStringsArchive.TargetFor` turns `\` into `/` before anything else.

## Work, in order

### 1. It runs and reads the logs

- **Archive-name helper and fixture fix** (above). Done in 0.19.10: on WSL
  Ubuntu, `Quantumwake.Tests` passes 1,747 of 1,747 and `Quantumwake.WebTests`
  1,265 of 1,265.
- **A Linux CI job.** Done in 0.19.10: the `linux` job in `ci.yml` runs
  `Quantumwake.Tests` and `Quantumwake.WebTests` on `ubuntu-latest`, so a
  Windows-only assumption fails the pull request instead of a Linux pilot's
  dashboard.
- **Install discovery in Wine prefixes.** Done in 0.19.12, in
  `GameInstall.Wine.cs`, and measured on 2026-10-30 against a LUG Helper
  (4.17) install on Fedora. On Linux `Discover()` walks prefixes instead of
  drives, and under each prefix's drives the same `RelativeRoots` list as on
  Windows, with the slashes turned round. The prefixes, surest first, every one
  checked for a `drive_c` before it counts:

  | Where | Why |
  |---|---|
  | `$WINEPREFIX` | The one the game is launched with, when the server inherits it |
  | `~/.config/starcitizen-lug/winedir.conf` | The LUG Helper writes the prefix it made there. **Measured:** `/home/user/Games/star-citizen`. `gamedir.conf` beside it names the `StarCitizen` folder itself, and is searched as a game folder outright |
  | `~/Games/star-citizen`, then every folder under `~/Games` | The LUG Helper's default, and the Lutris installer's. **Measured** for the helper |
  | `~/.wine` | Wine's own default |
  | `steamapps/compatdata/<appid>/pfx` under `~/.local/share/Steam`, `~/.steam/steam` (followed as a link, so one install is not listed twice) and the Flatpak Steam | Proton. From Valve's layout, not measured here |
  | `~/.local/share/bottles/bottles/*` and the Flatpak Bottles | From Bottles' layout, not measured here |
  | `~/Games/Heroic/Prefixes/default/*` | From Heroic's layout, not measured here |

  A prefix's drives are `drive_c` and every `dosdevices/<letter>:` link that
  points at a folder, each named as the folder the link resolves to rather
  than as the link: `c:` is a link to `drive_c`, `z:` is `/`, and an install
  reached two ways would otherwise carry two paths and, since the session
  cache is keyed on the path, two caches. A player who told the launcher to
  install on `D:` has that letter pointed at a Linux folder, and the walk goes
  there too.

  The launcher-log fallback reads the same `log.log` from inside each prefix
  (`drive_c/users/*/AppData/Roaming/rsilauncher/logs/log.log`) and turns the
  `C:\...` paths in it into prefix paths through `dosdevices`, matching each
  segment case-insensitively when the exact name is missing, since Wine looks
  names up the way Windows does and Linux does not. On Windows nothing moves:
  the log is read from `%APPDATA%` as before, and every path is used as
  written.

  `FromPath` - `--path` on the CLI, and the dashboard's "No Star Citizen logs
  found" box - accepts three more spellings on Linux: the prefix itself, a
  `C:\...` path copied out of the launcher, and `~`. The box itself now says
  which prefixes it searched, or that it found none in the usual places, and
  shows a prefix path as its example, instead of claiming every drive was
  searched. `/api/install/path` carries `windows` and `prefixes` for that.
- **The data folder.** Fixed in 0.19.11. On Linux, .NET maps
  `LocalApplicationData` to `$XDG_DATA_HOME` (default `~/.local/share`), so
  `AppPaths.Root` is `~/.local/share/Quantumwake`, but only when it asks with
  `SpecialFolderOption.DoNotVerify`. The default lookup answers an empty string
  when that folder does not exist yet, and the smoke run below found the data
  folder sitting in the directory the server was started from. Windows always
  has a LocalAppData, so nothing moved there. `AppPathsTests` points
  `XDG_DATA_HOME` at a missing folder and fails without the fix.
  `GameInstall.cs:140` asks for `ApplicationData` the same unverified way; it
  belongs to install discovery and changes with it.
- **Packaging.** Done in 0.19.11. `release.yml` cross-publishes `linux-x64` from
  the same Windows runner, so the Defender scan covers the Linux files too: the
  bare server as one self-contained file renamed `quantumwake` (70.6 MB), and
  the CLI framework-dependent beside it. It ships as
  `QuantumWake-<version>-linux-x64.tar.gz` (62.6 MB), written with
  `System.Formats.Tar` rather than `tar.exe` because nothing on a Windows disk
  carries an execute bit: `quantumwake` and `Quantumwake.Cli` go in as 0755,
  everything else 0644. The web SDK leaves an empty
  `staticwebassets.endpoints.json` beside the binary, which the one-file check
  caught; it is removed like the Windows build's stray runtimeconfig. AppImage
  and Flatpak would collide with Wine prefixes kept under `$HOME`, and nothing
  so far asks for them.

  Smoke run on WSL Ubuntu, from the tarball as built by those steps, against a
  LogSim install laid out as `prefix/drive_c/Program Files/Roberts Space
  Industries/StarCitizen/LIVE`: the CLI reads it with `! unmatched known tags`
  at 0; the server serves the embedded dashboard; with no install found it
  answers `"detected":[]`, and a folder posted to `/api/install/path` survives
  a restart. In-place updating stays Windows-only: `SelfUpdate` replaces only a
  file named `QuantumWake.exe`, so on Linux the update notice offers the
  release page instead of an install button.

### 2. The clipboard

Worth more than the OCR. Clipboard-only is the narrow screen mode a pilot picks
first, and `/showlocation` → *points from here* runs entirely on the clipboard.
A Linux `IClipboardReader` runs `wl-paste --no-newline` and falls back to
`xclip -o -selection clipboard`. When neither is installed the reader is not
registered, so the panel says the clipboard cannot be read instead of showing
nothing. To check on the Linux install: does Wine hand what the game copied to
the desktop clipboard at all, under both X11 and Wayland?

### 3. Screenshots

On Linux the screenshots mode says "not on Linux yet" until this step lands.

The engine is the smaller part of the work. Every screen reader (the wallet's
second look, the Rep bars, Contracts, refinery, scan) was tuned against the
particular misreads of `Windows.Media.Ocr`, and any other engine misreads
differently.

**Decided (2026-10-29): Microsoft's ONNX Runtime running PaddleOCR's models,
with our own code around it.** That means the `Microsoft.ML.OnnxRuntime` NuGet
package (MIT, with the native runtime for `win-x64` and `linux-x64` included),
plus PaddleOCR's text detection and recognition models exported to ONNX
(Apache 2.0, as published by the RapidOCR project). We write the image
preparation, the step that turns detection output into line boxes, and the
text decoding ourselves. No Python, no distro package, and no community .NET
wrapper: `Sdcb.PaddleOCR` and the RapidOCR C# ports were set aside so the
only third-party code in the reading path is Microsoft's runtime and a model
file.

The comparison that led there:

| | Tesseract CLI (call `tesseract … tsv`) | PaddleOCR models via ONNX Runtime |
|---|---|---|
| Effort | Small: parse the TSV and group words into lines | Bigger: ~500 lines to post-process detection and decode recognition |
| What the pilot installs | Tesseract from their distro | Nothing; ~15 MB of models ship with the app |
| Light HUD text on dark game backgrounds | Weak without inverting, upscaling and thresholding | Built for on-screen and photographed text |
| Output | Words, which need grouping | Text lines, which match `ScreenTextLine` directly |
| Extra benefit | — | Could also back Windows installs with no English language pack, so `OcrTests` would stop passing by doing nothing |

**The engine is chosen; whether the readers survive it is not.** This install
has 146 files in `LIVE\screenshots`. Run every reader over them with
`Windows.Media.Ocr` and with the ONNX reader, and count where the answers
differ, the same way the CLI checks the parser against the backups. The ONNX
reader is done when every disagreement is either fixed or written down here as
one it cannot help.

The two Windows-only image classes also need a cross-platform image library:
`WindowsScreenReader` (crop, scale, shear, `PixelsAsync`) and
`WindowsTileImageLoader` both decode through `Windows.Graphics.Imaging`.
SkiaSharp is MIT-licensed, maintained under the .NET Foundation by Microsoft's
.NET team, and ships Linux native libraries. `System.Drawing` is not an option:
since .NET 6 it is Windows-only.

## Four tests that fail on this machine, and not because of Linux

Measured 2026-10-30 on the Fedora VirtualBox guest above, on `main` with no
change applied, three runs each: `MiningLogStoreTests.The_newest_run_is_first`,
`ChangeStampTests.Editing_one_record_does_not_stamp_its_neighbours` and the two
`ControlsStoreTests` snapshot tests fail in three, four, three of three runs.
Each writes two rows a few microseconds apart and expects their stamps to
differ, and on this guest `DateTimeOffset.UtcNow` answered the same value for
both (`20:17:41.2101456` twice, in the one failure read in full). The same
suite passed whole on WSL Ubuntu, and these four pass on Windows, so the clock
is this virtual machine's and not a Linux fault; they are listed so the next
run here is not spent on them.

## What the Linux install has shown

Measured 2026-10-30 on Fedora (kernel 7.2, KDE on Wayland), on an install made
by the LUG Helper 4.17 with its `lug-wine-tkg-git-11.18-1` runner, one game
session in. The game was not running while this was measured, so the last
three questions stay open.

1. **Where the game is.** The prefix is `/home/user/Games/star-citizen`. The
   game is at `drive_c/Program Files/Roberts Space Industries/StarCitizen/LIVE`,
   in that case, with `Bin64/`, `Data.p4k` (156 GB), `Game.log`, `logs/`,
   `screenshots/` and `EasyAntiCheat/` beside it. No `logbackups/` yet: the game
   makes it on its second start, and this install has had one. No `user/`
   folder either, so there is no `actionmaps.xml` for the Controls page to read
   until the game writes one; the page already says so. Answered by the
   `winedir.conf` and `gamedir.conf` the helper writes under
   `~/.config/starcitizen-lug/`, which name both folders outright.
2. **The launcher's log** is at
   `drive_c/users/user/AppData/Roaming/rsilauncher/logs/log.log` (207 KB after
   a day). Its paths are `C:\\Program Files\\Roberts Space Industries\\StarCitizen\\LIVE`,
   JSON-escaped, so the Windows regex matches them as it stands; 13 lines in
   this log name the LIVE folder, and none names any other drive letter. The
   prefix's `dosdevices/` holds `c:` → `drive_c` and `z:` → `/`.
3. **`Game.log` is the Windows one.** It starts with the same
   `BackupNameAttachment` line, names the executable as
   `C:\Program Files\...\Bin64\StarCitizen.exe` and reports Build 12913470,
   FileVersion 4.10.197.2878. The CLI run from this tree, with no `--path`,
   finds the install through the helper's config and reads it with
   `! unmatched known tags` at 0 (one log; there are no backups yet to make
   the count mean more).
4. **The clipboard.** `wl-paste` is installed and the session is Wayland;
   `xclip` and `xsel` are not. Whether Wine hands what the game copied to that
   clipboard is unmeasured: it needs the game running.
5. **The localisation mod** - unmeasured, for the same reason.
6. **Screenshots.** `LIVE/screenshots` holds 146 JPEGs at 3440×1440, named
   `ScreenShot-2026-09-07_21-00-35-8E4.jpg` and so on. They predate the
   prefix, so they were copied in from the Windows install and say nothing
   about what the game writes under Wine. Unmeasured.
7. **The process name** - unmeasured. `GameProcess.IsRunning()` in the server
   accepts `StarCitizen` and `StarCitizen.exe` on Linux, because the kernel
   cuts a process name at 15 characters and `StarCitizen.exe` is exactly that
   long, so the cut name and the full one are the same string. `ps -eo
   comm,args | grep -i starcitizen` while the game runs is what confirms or
   corrects it.
