# Cougar MFD mode

Feature branch: `codex/cougar-mfd`, based on `dev0100`.

## Plan

- [x] Keep MFD mode optional and separate from the regular overlay.
- [x] Give each MFD an independent window and selected page.
- [x] Use the default F16 MFD device numbers and clockwise OSB numbering.
- [x] Add a visual monitor layout with draggable, resizable MFD openings,
  exact pixel fields, shared-monitor and separate-monitor presets.
- [x] Add a full-size alignment preview, USB button tester and saved placement.
- [x] Start with focused Nav, Task and Status HUD pages, using the existing live
  stream and briefing API. Default left to navigation and right to the next task.
- [x] Extend the compact pages to flight-plan actions, cargo and contracts.
- [x] Make every button reassignable, with the rockers bindable rather than
  guessed at.
- [x] Black out the rest of a monitor carrying panels, so the desktop stops
  glowing around the edge of the frame.
- [ ] Verify physical frame alignment and default USB inputs on the cockpit.
- [ ] Name the four rockers once hardware says which number each one reports,
  and decide whether any of them earns a default.

## HUD design

These are cockpit instruments, not the dashboard reduced to a smaller window.
An MFD page must answer a question useful during flight, with a short hierarchy
and controls aligned to the physical buttons. Page changes stay under the
pilot's control; a detected ship must not silently rearrange the buttons.

- **Nav:** where am I, where am I going, and which ship did the logs identify?
  A quantum destination takes priority over the next planned stop.
- **Task:** what do I do next? Show the first outstanding stop and its next
  unfinished instruction, including the quantity and unit the pilot entered.
  Planned loads are never presented as detected cargo in the hold.
- **Act:** the same stop's outstanding work as a list, with a cursor, so one
  line can be ticked off from the frame. Up and down move the cursor here
  rather than scrolling. A stop with nothing left offers itself instead, since
  crossing the stop off is then the only thing to confirm there.
- **Cargo:** what did I mean to carry, and what did a counter actually record?
  Never a manifest — see below.
- **Contract:** which contract am I on, and how many objectives are left? This
  session only, because that is the only session the store cannot answer for.
- **Status:** can I trust the source? Keep session state and the latest
  screenshot reading with its capture time. No fabricated fuel or shields.

The default cockpit pairs Nav on the left with Task on the right. Fleet
catalogues, market browsing, historical tables, full party lists and settings
stay on the dashboard. Unused OSBs remain unassigned rather than offering
pages that are not useful at this size.

**Act writes, and says so.** Confirming marks a line in the pilot's own flight
plan and tells the game nothing, which the page states on every visit. A press
arms; a second press on the same button commits; any other button stands it
down. One button rather than two, and no timer: a confirmation that expires on
a clock is one that expires while the pilot is being shot at. The instruction
they are confirming stays on screen the whole time.

**Cargo cannot be a manifest.** Game.log never states what is in a hold, so the
page shows two things that are not that and labels both: what the plan still
says to load, and what the commodity counters recorded this session. Quantities
are added up only where the pilot wrote SCU on them, and anything else is
counted beside the total rather than folded into it — two units are two
numbers. A haul bought last session, transferred from another ship or blown out
of the back is invisible to all of it, and the page says so rather than
implying a full hold with a confident figure.

**Contract is session-scoped on purpose.** `/api/contracts` reads the store,
and the store gains a session when the log rotates — so the contract being
flown right now is the one thing that report cannot show. The page carries the
open ones from the live session and points at the dashboard for the rest. A
contract with no journal objectives says the journal was quiet rather than
showing "0 of 0", which would read as no work left.

## Use

Run the desktop build, then choose **MFD setup…** from the tray. Drag the left
and right MFD areas onto the monitor or monitors behind the frames. Resize
from the corner; the rectangles represent the visible screen openings. The
monitor dropdown and pixel fields also allow precise placement. Arrow keys
move a focused rectangle; Shift increases the step.

**Show alignment preview** displays the rectangles at their real positions.
Changes in the editor update that preview when a drag finishes. **Stop preview**
or closing setup restores the saved state. Tick **Enable MFD displays** and
choose **Save layout** to keep them. Settings live in `mfd.json` under the
Quantum Wake data directory. Each display remembers its own page, text size
and screen brightness in the WebView profile.

**Button assignments** below the tester rebinds any button. Changes reach a
running alignment preview immediately, so a rebinding can be tried on the frame
before it is saved; **Save layout** keeps them along with the placement.

## The backdrop

A Cougar frame is a bezel with a square hole in it, screwed over part of a
monitor. Everything the frame does not cover still glows - wallpaper, the
taskbar, whatever window is behind it - and in a dark cockpit that light leaks
around the edge of the bezel and washes out the instrument inside. **Black out
the rest of those monitors** fills every monitor carrying a panel with black,
around the openings. It is on unless you turn it off, and the monitor layout in
setup draws itself the way the monitor will look, so the switch explains itself.

Off is for anyone who put a panel in the corner of a monitor they are still
using and would rather keep the desktop than the contrast.

The openings are cut out with a **window region** rather than left to z-order.
Two topmost windows have no guaranteed order between them, and if the backdrop
ever won that race the pilot would get a black square where the instrument
should be - a failure that looks exactly like a crash. A window with holes in it
is not in that race. They are holes to the mouse as well: the backdrop is
click-through, and an opening is not the backdrop at all.

**The monitor the setup window is on keeps its backdrop off while setup is
open**, and takes it back when setup is closed or dragged elsewhere. Somebody
placing a panel on the monitor they are working on would otherwise cover their
own setup window with the thing they had just switched on, and the way back out
would be underneath it.

Disconnected monitors retain their saved placement and their panels stay
hidden. They are not moved onto the primary monitor. Changes in monitor
resolution clamp the live placement within the available monitor. Windows
monitor device names identify saved displays; if Windows renames a display,
select its new entry in setup.

## Default profile

The official [Cougar default diagram](https://ts.thrustmaster.com/download/accessories/pc/mfd/Cougar-MFD-leaflet.jpg)
numbers the OSBs clockwise: top 1–5, right 6–10, bottom 11–15 from right to
left, and left 16–20 from bottom to top. The [user manual](https://ts.thrustmaster.com/download/accessories/pc/mfd/MFD_COUGAR_Pack_User%20Manual.pdf)
names the default devices F16 MFD 1 and F16 MFD 2. Quantum Wake assigns them
to the left and right displays respectively; setup can swap or change those
assignments without changing the firmware.

| Buttons | Quantum Wake action |
| --- | --- |
| 1–5 | Nav, Task, Act, Cargo, Contract |
| 16 | Status |
| 15 / 11 | Previous / next page |
| 14 / 12 | Up / down — the cursor on Act, the panel elsewhere |
| 13 | Home (Nav) |
| 6 / 7 | Increase / decrease text size |
| 8 | Done — arm, then confirm, the selected flight-plan line |
| 20 / 19 | Increase / decrease screen brightness |
| 9, 10, 17, 18 | Unassigned |
| 21–28 (rockers) | Unassigned; see below |

Every one of those is a default rather than a rule. **Button assignments** in
setup rebinds any of the 28 buttons to any page or action, shared by both
frames — they are the same physical device, and the one thing that differs
between them, the page each is showing, the displays already remember for
themselves. **Restore defaults** puts the shipped profile back.

A saved map is the whole answer, not a patch over the defaults: a button the
pilot clears stays cleared, and clearing every one of them leaves a blank frame
with every dropdown in setup saying so. Falling back per button would mean an
unassignment quietly undoing itself on the next reload, which is the one thing
that would make a custom profile untrustworthy.

**The four rockers ship unassigned, deliberately.** They report as buttons
21–28, and nothing in the leaflet or the manual says which rocker is which
number — so a default here would be a guess printed as a fact. Press one, watch
the setup tester name the number that answered, and bind it. The row lights up
as it is pressed, which is the whole discovery procedure.

Screen brightness changes the rendered page, not the Cougar LEDs. Native
USB reads use Windows `joyGetPosEx` and do not consume game inputs or send
keystrokes. A button also bound in Star Citizen can therefore affect both.
No T.A.R.G.E.T. profile is needed. A virtual controller replacing the default
devices, renamed devices, and devices outside the Windows joystick API's
slots 1–16 are not supported by this initial reader. Setup reports missing
devices; duplicate MFD numbers pause input for those numbers. Held buttons
do not repeat, and buttons held during connection are ignored until released.

The browser links on the Overlay page preview the layout and data without
native USB input. The placement editor labels its example monitor clearly
and cannot save desktop settings outside the desktop host.

## Verification

Verified on 2026-09-09:

- Release build of the Overlay succeeds. The server reports its existing
  unused `Owned` local-function warning.
- The full solution test run passed: 1,096 core/data tests, 498 web tests and
  6 OCR tests. After narrowing the HUD pages, the expanded web suite passed
  with 500 tests.
- Headless Chrome checks passed for page buttons, the task's SCU unit,
  pointer dragging, independent monitors, assignment swapping, preview
  updates and save messages. Screenshots were inspected at 480 × 480 and
  220 × 220 for the MFD and 1100 × 850 and 760 × 640 for setup. Save and
  preview actions remain visible at the minimum setup size.
- An offscreen native WebView run against a separate server with generated
  logs detected three monitors and both F16 MFD 1 and F16 MFD 2. It verified
  the monitor message, save-message round trip, and a requested 480 × 500
  native window at (-9975, 35) against `GetWindowRect`.
- Native lifecycle checks passed for preview, cancellation, enable, disable
  and saved-layout reload in an isolated data directory.

The native run exposed a negative result worth retaining: `joyGetDevCapsW`
returned **Microsoft PC-joystick driver** for every occupied slot on this
machine. Matching that field alone detected no Cougars. Resolving the slot's
OEM registry entry produced the actual MFD names and detected both devices.

### The Act, Cargo and Contract pages, and the button map

Verified on 2026-09-09, against two servers: one on a copy of this install's
real data, one on a generated install so the counters and journal had something
in them.

- All three suites pass: 1,105 core/data tests, 511 web tests, 6 OCR tests.
  `Quantumwake.Cli` against the real corpus reports **0 unmatched known tags**.
- Headless Chrome drove the real page against the real server. Act listed the
  two outstanding instructions at Baijini Point from a tracked plan, moved its
  cursor, armed on **DONE** and stood down again on any other button. The
  second **DONE** posted
  `/api/trips/778bd611/stops/86f33f3a/actions/73efdfb7/toggle` - the right trip,
  stop and instruction. That exact request was then sent for real: HTTP 200, and
  the briefing dropped the instruction from its outstanding list.
- Cargo read "96 SCU across 2 stops · planned, not detected" from the plan, and
  "Sold 80 SCU · Admin lt base g · 132,509 aUEC" with its capture time from the
  parsed logs, over "The game logs no cargo hold". With neither, it says which
  one is missing rather than showing a zero.
- Contract read a real parsed contract and said "The journal reported no
  objective steps for this one" rather than "0 of 0".
- The setup editor round-tripped a profile through the host message channel: a
  rocker press lit row 21, binding it saved `"21": "bright-up"`, clearing button
  8 removed the key rather than restoring its default, and **Restore defaults**
  posted `buttons: null`. A profile delivered from the host relabelled the frame
  live - OSB 1 read `CNTRCT`, unmapped buttons went to `—` and disabled.
- Rendered at 220 × 220, the minimum panel, inside an iframe. Headless Chrome
  will not open a window narrower than about 500 px on this machine, so a
  `--window-size=220,220` screenshot is a crop of a 500-wide page and looks like
  a broken layout when nothing is wrong. Frame it instead.

### The backdrop

Verified on 2026-09-09, with an offscreen native run: an `MfdBlackout` covering
a monitor placed at (-9000, 40), so nothing appeared on any real screen.

- `GetWindowRect` returned the requested `-9000,40 1920x1080`, and
  `GetWindowRgnBox` the full 1920 × 1080 the region spans.
- `WindowFromPoint` answered the backdrop everywhere except inside the two
  openings - including the gap between them, and one pixel outside an opening's
  corner while one pixel inside was clear. The holes are exact, and they are
  holes to the mouse as well as to the eye.
- Moving a panel moved its hole: the old opening came back under the backdrop
  and the new one was clear.
- The setup editor round-tripped the switch: only the monitor carrying panels
  drew as blacked, never the desk monitor beside it, and saving carried
  `blackout` both ways.

**The negative result from this round:** `ContractRecord.Accepted` is never set
by anything. Filtering on it - which both this page and
`LibraryBeliefs.OpenContractsAt` did - returns an empty list for every session
ever recorded, which is why the first live run found no contracts in a log
carrying 24 acceptance toasts. The acceptance toast and the objective marker
name a contract in two vocabularies that do not join, so nothing ever fills it
in. Being in the list is already what "taken" means: the game raises an
objective marker for a mission in the journal. Both call sites now say so, and
the field carries a warning rather than being removed, since dropping it would
retire every cached session to change nothing that is stored.

That bug was also reaching the screenshot cross-check: the mobiGlas Contracts
app was compared against a permanently empty list, so a photograph of five
accepted contracts read "the tab says 5, the logs say 0" and filed all five as
"on screen but not in the logs".

Physical button presses while the game has focus, frame alignment, and
mixed-DPI monitor behavior still need a cockpit run. The native checks read
device state but did not synthesize a physical press or change the game. The
four rockers cannot be given defaults until that run says which number each
one reports.
