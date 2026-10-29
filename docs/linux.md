# Linux

Plan, unbuilt. Quantum Wake on Linux, for the line after 0.19.9 (`dev0190`).

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
- **Install discovery in Wine prefixes.** `GameInstallLocator.RelativeRoots`
  (`GameInstall.cs:37`) is built from backslash paths and walked across drive
  letters with `DriveInfo`. Both are meaningless on Linux. A Linux branch has to
  look inside the usual prefixes for `drive_c/Program Files/Roberts Space
  Industries/StarCitizen/<channel>`. The launcher-log fallback
  (`GameInstall.cs:139`) reads `ApplicationData`, which on Linux is the native
  `~/.config` and not the prefix's `AppData/Roaming`. The `C:\...` paths it finds
  there also have to be mapped back through the prefix's `drive_c`.
  `--path` and `QUANTUMWAKE_DATA` already work as escape hatches.

  **The prefix locations are not measured yet.** The Wine installer helper,
  Lutris and Proton each put the prefix somewhere different, and the list
  belongs here only once the Linux install has shown them.
- **The data folder needs no change.** On Linux, .NET maps
  `LocalApplicationData` to `$XDG_DATA_HOME` (default `~/.local/share`), so
  `AppPaths.Root` becomes `~/.local/share/Quantumwake`.
- **Packaging.** `release.yml` publishes `win-x64` only. Cross-publish
  `linux-x64` (self-contained single-file server, plus the CLI) from the same
  Windows runner, so the Defender scan covers the Linux files too, and ship a
  `.tar.gz`. AppImage and Flatpak would collide with Wine prefixes kept under
  `$HOME`, and nothing so far asks for them.

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

## What the Linux install has to show

Nothing in phase 1 beyond the test fixes should be built before these are
answered:

1. Where each launcher's prefix puts the game, as a directory listing down to
   `LIVE/Game.log`, `logbackups/` and `screenshots/`, with the names in their
   real case.
2. Where the RSI launcher's `log.log` lands inside the prefix, and how the paths
   in it are written (`C:\...`, `Z:\...`, or something else).
3. That `Game.log` lines are byte-identical to Windows ones. Run the CLI over
   the Linux backups: `! unmatched known tags` should read 0.
4. Whether `/showlocation` reaches `wl-paste` or `xclip`, and on which display
   server.
5. Whether the localisation mod installs into the prefix, and the game reads
   it.
6. Whether F12 screenshots come out as the same JPEGs, at the same resolution
   the readers expect.
