# Linux

Partly built. Quantum Wake on Linux, the line after 0.19.9 (`dev0190`).

The game runs on Linux under Wine or Proton and writes the same `Game.log` it
writes on Windows, just inside a Wine prefix. Everything Quantum Wake knows comes
from that file, the game's archive and screenshots the pilot saved, so on Linux
it can show almost everything the dashboard shows on Windows. The things that
cannot come over are the ones that need a Windows desktop.

## Scope

**In:** the server and the web dashboard, the CLI, reading the logs and the game
archive, the clipboard, and screenshots.

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

Done in 0.19.13: `LinuxClipboardReader` in the server runs `wl-paste
--no-newline`, else `xclip -o -selection clipboard`, else `xsel`, whichever is
on the path, Wayland's first because a Wayland session usually has an X bridge
and the X tools then read a clipboard the game never wrote to. When none is
installed the reader is not registered and the panel says the clipboard cannot
be read. `wl-paste` answering "Nothing is copied" (exit 1) reads as an empty
clipboard, not a failure. Measured 2026-10-30 on Fedora, KDE on Wayland:
`wl-paste` is installed, `xclip` and `xsel` are not. **Whether Wine hands what
the game copied to that clipboard is still unmeasured**; it needs the game
running.

### 3. Screenshots

Done in 0.19.13, in `src/Quantumwake.Ocr.Onnx`: `OnnxScreenReader` is the
`IScreenReader` the server falls back to when the host brought none - the
Linux server, and a Windows machine whose OCR has no English language pack -
on Microsoft's ONNX Runtime (1.31.1) running PaddleOCR's models, with
SkiaSharp (4.154.0, the NoDependencies Linux build, so no fontconfig is
needed) for decoding, cropping, scaling and shearing. Nothing else: the box
reading, the CTC decoding, the line grouping and the second look are this
project's code, written to the same contract as `WindowsScreenReader`, so
`ScreenInsightService` and every reader run unchanged.

**The models.** PP-OCRv4's detector (4.7 MB) and PP-OCRv3's English
recogniser (9.0 MB), as RapidOCR exports them, from
`huggingface.co/SWHL/RapidOCR` pinned to revision `1cfba2e9` and checked
against a SHA-256 on arrival (`OnnxModels.cs`). No PP-OCRv4 English export is
published; the multilingual PP-OCRv4 recogniser (10.9 MB, 6,623 characters) is
kept as an alternative for the comparison harness. **Fetched on first use**,
into `<data>/ocr` (`QUANTUMWAKE_OCR_MODELS` moves it), rather than shipped in
the binary or checked in - a departure from what this plan first said, for a
reason: fifteen megabytes of model in the repository's history for ever, and
in every Windows build that never loads them, against a download the app
already makes for StarStrings and the community dataset. Switching the
screenshots mode on starts the fetch, so it is there before the first frame;
`/api/screen/settings` reports `engine: { name, ready }` so a page can say so.
The character tables are PaddleOCR's and are embedded (Apache 2.0, in NOTICE).

**How it reads.** The frame goes into the detector at its own size, each side
rounded to a multiple of 32, up to a long side of 4,096 - not the reference
960, which would put the 12-pixel item names at four pixels. The probability
map is thresholded at 0.3, grouped into four-connected components, each
scored by its mean and kept above 0.5, and grown back out by `area × 1.6 /
perimeter`; axis-aligned, since the game writes nothing slanted but the one
line the second look stands up. Each box is cut from the original frame, drawn
48 pixels tall, read, and dropped below 0.4 confidence. Boxes that overlap in
height by half the shorter and sit within 1.2 heights of each other are one
line, left to right. **The geometry reported is the ink's, not the box's**:
rows and columns of the crop that vary by more than a fifth of its most
varied row are glyph, the rest the unclip margin, so a line's height is its
glyphs' as it is on Windows, where every distance the readers measure is in
those heights.

**Measured, 2026-10-30, on the 146 screenshots in this install's
`LIVE/screenshots`** (3440×1440, the same files the Windows readers were
tuned on), with `Quantumwake.Cli --ocr-check <folder>`, which runs the whole
`ReadShotAsync` - second looks, Rep bars, `Understand` - and prints what each
frame was taken for:

| Kind | Frames |
|---|---|
| Contracts | 37 |
| Reputation | 21 |
| Loadout | 20 |
| MobiGlas (an app without a reader) | 13 |
| Map | 7 |
| Mining | 4 |
| Fleet, Kiosk, Refinery, Tooltip | 3 each |
| Unknown | 32 |

The wallet figure was read on 103 frames. Twelve of the 32 unknowns are one
identical all-black 136 KB JPEG the game wrote twelve times; the other twenty
are in-flight HUD frames whose reads are noise, as a HUD over a planet is.
The three tooltips name `Arlington Rifle` twice and `Sion Corpeneator2`
(the catalogue's `Sion Corp Eneator 2` would be a confusable, not a miss). A
first read costs about 5 s for loading the sessions; after that the median
frame takes 1.3 s on this four-core VirtualBox guest, with 0.9 s the fastest
and 7 s the slowest, against 170 ms for the Windows engine. Fast enough for a
file the pilot saved; it is not a live reader and does not need to be.

**What the comparison still lacks is the other half.** The same command on
Windows, over the same folder, with `WindowsScreenReader` behind it, is what
says where the two engines disagree frame by frame; this machine has no
Windows engine. Run it there, diff the two outputs, and write the
disagreements here. Known differences already: the English recogniser drops
the space after some labels (`Volume:13000 SCU`, `Item Type:Rifle`,
`Effective Range50m`), which the tooltip reader survives because it splits on
the colon, and it reads `W` for the mobiGlas glyphs the Windows engine skips.
The readers were not retuned for this engine: anything that reads here reads
on the rules written for Windows.

**English or multilingual recogniser.** Both were run over every ninth frame
(17 frames) with `--ocr-check`, at the same speed (1.46 s a frame each).
Fourteen frames read the same. The multilingual PP-OCRv4 model read a Fleet
Manager frame the English one left unknown (five ships) and a map footer's
`SYSTEM` without the trailing `I` the English model added; the English model
read a Rep card's `RED WIND LINEHAUL: MEMBER 78%` where the multilingual one
answered `☆`, a glyph from its 6,623-character table that the readers have no
rule for. English stays the default: a table that cannot emit a stray CJK or
symbol character cannot hand the readers one, and the two frames it lost are
the kind of marginal read the Windows comparison will say more about.

**`--ocr <file-or-folder> [--out <dir>] [--show] [--rec ch]`** is the other
harness: the lines each frame read, their boxes, and the time, written in the
probe's own `[ left top hNN]  text` format so `--screen` reads them back.

**Tests.** `tests/Quantumwake.OnnxOcrTests` (11, on the Linux CI job and the
Windows one) draws its fixtures with SkiaSharp through a font opened by file,
reads them, and checks the geometry: a line sits where it was drawn at its
glyphs' height, a second look maps its boxes back into frame pixels, a shear
drawn and undone leaves the box in place, a patch off the frame is clamped,
pixels come back in colour, ink is measured inside a padded crop, and words
group into lines by row and gap. The models are fetched once into the app's
folder and cached by CI; a runner with no network stands the reading tests
down and says why.

### 4. Starting and stopping

Done in 0.19.14, measured on the Fedora machine above with the server
published the way `release.yml` publishes it (self-contained single file,
renamed `quantumwake`, 164.8 MB uncompressed) and started from a terminal, a
script and a systemd user unit. What a Linux pilot does is in the README's
[Linux](../README.md#linux) section; this is the evidence behind it.

- **Start.** `./quantumwake` from the unpacked folder. The first log line is
  `Quantum Wake by nekron - http://127.0.0.1:31337`, then Kestrel's
  `Now listening on` and `Application started. Press Ctrl+C to shut down`.
  Nothing opens a browser; there is no tray. The process name in `ps` is
  `quantumwake`. The .NET host unpacks the four native libraries
  (`libonnxruntime.so`, `libonnxruntime_providers_shared.so`, `libSkiaSharp.so`,
  `libe_sqlite3.so`) to `~/.net/quantumwake/<id>/` on the first start, 42 MB,
  which is why the removal instructions name that folder. Each of them links
  only `libstdc++` beyond glibc (`ldd`), hence the one named prerequisite. The
  data folder is `~/.local/share/Quantumwake`, confirmed by a start with
  `QUANTUMWAKE_DATA` unset.
- **Stop.** Measured from the signal to the process being gone: `SIGINT`
  (Ctrl+C) 1.6 s, with `Application is shutting down...` logged; `SIGTERM`
  (`pkill quantumwake`, `systemctl --user stop`) 0.2 s and 1.5 s; `SIGHUP`
  (the terminal closed) 3.0 s. A systemd user unit with `ExecStart` pointing at
  the binary starts, serves and stops as expected; the README's unit is the one
  tested, minus a test name and port.
- **A second copy** used to print Kestrel's `AddressInUseException` with its
  stack trace and abort (exit 134). `Program.cs` now probes the port first and
  prints one line, `Quantum Wake is already running at http://127.0.0.1:31337 -
  open that one, or start this copy with another --Port.`, exit 1; the catch
  around `Run()` covers the race. Verified against a running copy.
- **A trap in the test harness, recorded so it is not found twice:** a
  non-interactive bash ignores `SIGINT` for a command it starts with `&`, and
  .NET keeps an inherited ignore, so the first measurement looked as if Ctrl+C
  did nothing at all. `set -m` in the script (job control) is what makes the
  background copy receive the signal like a terminal's.

**The crash on start, and the fix.** Four starts in about twenty-five died
with `Floating point exception (core dumped)`. `coredumpctl` kept the cores;
with the host's symbols fetched by `dotnet-symbol` (the single-file host is
stripped), gdb reads the faulting instruction as `div %esi` with `esi` zero in

```
SVR::gc_heap::dynamic_heap_count_data_t::get_hc_change_freq_factors
SVR::gc_heap::dynamic_heap_count_data_t::should_change_hc
SVR::gc_heap::calculate_new_heap_count   gc.cpp:26164
SVR::gc_heap::process_datas_sample       gc.cpp:27006
SVR::gc_heap::gc1                        gc.cpp:23283   (.NET 10.0.13)
```

on a GC thread: the server collector's dynamic heap-count adaptation (DATAS),
a runtime bug, not ours, and nothing on GitHub names it yet. The web SDK
turns the server collector on by default; the overlay never had it, because
WPF hosts the same server under the workstation collector. `ServerGarbageCollector`
is now `false` in the server project, which is the right collector for one
pilot's PC anyway, and 23 starts with it have not crashed. `DOTNET_GCDynamicAdaptationMode=0`
alone would also avoid the faulting code, but keeps four heaps that nothing
here needs.

**The file watch, and the fix.** `WebApplication.CreateBuilder(args)` takes
the working directory as content root and watches `appsettings*.json` under
it for changes; on Linux that watch is an inotify walk of the whole content
root. Started from `/` the server held 49,829 inotify watches when it became
ready, against this account's limit of 103,570 (`fs.inotify.max_user_watches`),
and from the home folder 3,199; a home with a Steam library beneath it could
reach the limit and the watch would silently fail. The content root is now
`AppContext.BaseDirectory`, the folder beside the binary, and the count is 1
from any working directory. The walk was not the slow start below: it was the
first suspect because a stack sampled nine seconds in sat in
`FileSystemWatcher.AddDirectoryWatchUnlocked`, but starts from the app's own
folder with one watch were just as slow.

**The slow start on this machine, unresolved and not ours.** Between the
process starting and `Now listening on`, this machine takes anywhere from
2 to 104 s, varying run to run with nothing changed, all of it CPU time on the
main thread (`ready 66.5 s, cpu 63.5 s` is a typical pair; user time, not
system). It is not the JIT: a ReadyToRun publish took 52, 67 and 94 s on
three starts. It is not the game or the data folder: the gap is between
`DiagnosticTrace.Start` and `ResolveInstall`, before either is touched. It is
not the collector, tiering, PGO, W^X, spin-wait or diagnostics settings: each
was switched off in turn with no change in the spread (`DOTNET_EnableDiagnostics=0`
looked like the answer at three starts of 4 to 6 s, and gave 17 to 128 s at the
next five, which is the sample size this needs). Native stacks sampled through the
stall (gdb, every 2 s) show the main thread in the runtime's thread-static
slow path and class-initialisation lock - `GetThreadLocalStaticBase`,
`MethodTable::DoRunClassInitThrowing`, `DeadlockAwareLock::TryBeginEnterLock`,
`sched_getcpu` - and the managed profile puts 92% under
`StaticsHelpers.GetNonGCThreadStaticsByIndexSlow` beneath
`SharedArrayPool<char>.Return`. The decisive measurement is that **an empty
`dotnet new web` application shows the same spread here**: five starts of a
host that does nothing but `CreateBuilder` and `Build` took 57.0, 54.9, 23.1,
2.0 and 20.8 s. So it is the .NET 10.0.13 runtime on this host (Fedora 44,
kernel 7.2, a KVM guest with four vCPUs, `[Firmware Bug]: TSC doesn't count
with P0 frequency!` in its boot log), and nothing in Quantum Wake. The CLI and
the test host, which build the same server in under a second in
`ServerUnderTest`, do not show it, which is also why no test caught it. What a
real Linux PC does is unmeasured; the README says to wait for the ready line
and gives the range, no more. The repro is two files in
`scratchpad/repro/web/` on this machine: `dotnet new web`, and a `Stopwatch`
around `CreateBuilder` and `Build`.


## Four tests that fail on this machine, and not because of Linux

Measured 2026-10-30 on the Fedora virtual machine above, on `main` with no
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
   `xclip` and `xsel` are not. The reader is built (step 2). Whether Wine hands
   what the game copied to that clipboard is unmeasured: it needs the game
   running.
5. **The localisation mod** - unmeasured, for the same reason.
6. **Screenshots.** `LIVE/screenshots` holds 146 JPEGs at 3440×1440, named
   `ScreenShot-2026-09-07_21-00-35-8E4.jpg` and so on. They predate the
   prefix, so they were copied in from the Windows install and say nothing
   about what the game writes under Wine; what they say about reading them on
   Linux is in step 3 above. Twelve of them are the same all-black 136 KB
   file, so the game does sometimes write a black screenshot.
7. **The process name** - unmeasured. `GameProcess.IsRunning()` in the server
   accepts `StarCitizen` and `StarCitizen.exe` on Linux, because the kernel
   cuts a process name at 15 characters and `StarCitizen.exe` is exactly that
   long, so the cut name and the full one are the same string. `ps -eo
   comm,args | grep -i starcitizen` while the game runs is what confirms or
   corrects it.
