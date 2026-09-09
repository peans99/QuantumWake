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
- [ ] Verify physical frame alignment and default USB inputs on the cockpit.
- [ ] Extend the compact pages to contracts, cargo and flight-plan actions.
- [ ] Add configurable action bindings and rocker assignments after the initial
  profile has been tested on hardware.

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
- **Status:** can I trust the source? Keep session state and the latest
  screenshot reading with its capture time. No fabricated fuel or shields.

The default cockpit pairs Nav on the left with Task on the right. Fleet
catalogues, market browsing, historical tables, full party lists and settings
stay on the dashboard. Unused OSBs remain unassigned rather than offering
pages that are not useful at this size. Context actions such as confirming
work can be added once their input and feedback are tested on the hardware.

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
| 1–3 | Nav, Task, Status |
| 15 / 11 | Previous / next page |
| 14 / 12 | Scroll up / down |
| 13 | Home (Nav) |
| 6 / 7 | Increase / decrease text size |
| 20 / 19 | Increase / decrease screen brightness |
| Other OSBs and rockers | Unassigned; shown in the button tester |

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

Physical button presses while the game has focus, frame alignment, and
mixed-DPI monitor behavior still need a cockpit run. The native checks read
device state but did not synthesize a physical press or change the game.
