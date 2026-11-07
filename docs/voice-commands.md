# Voice commands

A light copilot built into the app (1.2.0): say a command and the app presses
the game's key for it. It exists because a pilot does not want to give a button
to everything they use once a session, such as self destruct, all the doors,
power to shields or the mining modules, and still wants those actions at hand.

Off until turned on, in Settings → Voice & callouts → Voice commands. Windows
only: the recogniser and the key presses are Windows APIs, and on Linux the
block says so.

## How it listens

- **The recogniser** is the desktop one Windows carries (SAPI, "Microsoft
  Speech Recognizer 8.0"), through `System.Speech`. On this PC it is
  `MS-1033-80-DESK`, en-US, found 2026-11-07. It runs on the machine with no
  network and needs no "online speech recognition" switch. It is also what
  VoiceAttack listens with.
- **A fixed grammar, not dictation.** The recogniser is told the exact phrases
  (`VoiceCommands.Grammar()`, 50 of them) and nothing else. Measured with the
  Windows voice speaking into a WAV file and the file recognised against the
  grammar: the ten commands tried came back at 0.91–0.99 confidence. "What a
  lovely day" and "pass me the salt" matched nothing. A real voice over game
  noise will score lower, so the confidence bar exists.
- **The bar** is 0.7 for a command (`VoiceCommandSettings.MinConfidence`).
  The wake and sleep phrases need 0.1 more: a wrongly heard command does one
  thing, but a wrongly heard wake leaves every command live.
- **Waking**: say "Quantum Wake copilot on" and commands are acted on until
  "Quantum Wake copilot off" or "copilot off". Waking needs the full phrase,
  so a stray "on" cannot wake it.
- **Push-to-talk** instead, or as well: a stick button (learned by pressing
  it on the page, read through `IJoystickReader`) or a keyboard key (polled
  with `GetAsyncKeyState`). A phrase still counts for 1.5 s after the button is
  let go, because the recogniser hands the result over a beat after the words
  end.
- **Confirm**: self destruct and jettison cargo are not pressed on their own
  phrase. The copilot asks "Confirm self destruct?" and presses only on
  "confirm" within 6 s. "Cancel", silence, or anything said while it is not
  listening stands the command down. With push-to-talk, "confirm" itself has
  to be said with the button held (1.2.1): one said with it up, to a wingman,
  cancels rather than fires. "Abort self destruct" presses the same key
  again with no confirm, since that is how the game stops the countdown.

Audio never reaches the app. It sees only the phrase matched and its score,
keeps the last 40 in memory for the page, and writes none of them to disk.

## What it says

Every answer is in the recorded callout voice, Bella (Kokoro `af_bella`), as
one clip a sentence (1.2.2):

- The wake and sleep replies.
- Each command's reply, and the two confirm prompts.
- "Cancelled", "Can't press that" and "Game not in front".

That is 31 clips, about 0.2 MB, recorded by `tools/voice-pack/make-pack.mjs`
from the list in `VoiceCommands.Spoken()`. A test fails if a sentence there
has no clip, so no reply drops to the Windows voice mid-session.

The recordings were checked by recognising each one back against the list of
replies. 25 of the 31 came back as written. The other six came back empty
rather than as another reply: "Copilot on", "Combat mode", "Nav mode",
"Ping", "Module one" and "More power". They are as long and as loud as the
rest. Dictation is no oracle either, since it heard "Module two", which
passed at 0.95, as "Don Schultz you". So the six are unconfirmed rather than
known bad, and each command's Hear it button is how to listen.

## How it presses

- **Which key.** `VoiceBinding.Plan` takes, in order:
  1. The pilot's own keyboard binding from the live `actionmaps.xml`.
  2. "Cleared" when the pilot took the key away.
  3. The game's default from `defaultProfile.xml`.
  4. "Not bound".

  A chord carries its device prefix once, on the first part (`kb1_rctrl+np_5`),
  so the device is read from there.
- **How long.** From the game's own activation rules, which 1.2.0 reads out
  of the `ActivationModes` block at the top of `defaultProfile.xml` (18 modes
  on this install) and from the rules some actions spell out inline:

  | Rule | Game's threshold | Held for |
  |---|---|---|
  | `press`, `tap` (release within 0.25 s) | — | 0.06 s |
  | `delayed_press_medium` (self destruct) | 0.5 s | 0.7 s |
  | inline `onHold=1 holdTriggerDelay=0.25` (shields to max) | 0.25 s | 0.45 s |
  | `double_tap` | two presses | twice, 80 ms apart |

  A command that runs while held, such as "scan target", holds for its own
  time instead. The 3 s for a scan is a first guess, not a measurement.
- **By scan code.** `SendInput` with `KEYEVENTF_SCANCODE`, not virtual keys.
  The game binds the key's place on the board, so on an AZERTY keyboard the
  game's `a` is the key printed Q, and a scan code presses the key the game
  means. The extended keys (Right Alt and Right Ctrl, the arrows, Insert,
  Delete, Home, End, Page Up and Down, Numpad / and Numpad Enter) share codes
  with numpad or left-hand keys and are sent with the E0 flag. Mouse buttons
  1 to 5 can be pressed. The mouse wheel and stick buttons cannot.
- **Only into the game.** The window in front is checked to be Star Citizen
  (title and the `CryENGINE` class) immediately before every key goes down.
  With the game not running the press is refused, as checked on this PC.
  Keys come up in reverse order in a `finally`, so a key is never left held.

## What it can press on this install

Planned against this install's catalogue and Nicolas's `actionmaps.xml` on
2026-11-07, 13 of the 24 commands press a key as things stand:

- **Can press:**
  - The mining set: mining and salvage mode, both on M; laser, switch laser,
    beam, modules 1–3 and jettison.
  - Scan mode, ping, power to shields, request landing, and self destruct and
    its abort.
- **Only on a stick:** guns, combat and nav mode, scan target, less power,
  and the doors. Each says which button it is on.
- **No key at all:** quantum mode.
- **On the mouse wheel:** more power.

Giving these spare keys, written as a profile the pilot imports once, is the
next step (1.2.1).

## The licence

The live-screen reading in [screen-insight.md](screen-insight.md) holds that
no input is ever generated. Voice commands are a separate switch, and they are
the one part of the app that does send input. So the ToS clause on "'auto'
software programs, 'macro' software programs" applies here in a way it does
not to the screen.

What the feature does to stay on the right side of it:

- One spoken command presses one action's key once, the way the pilot's hand
  would.
- Nothing is chained, repeated or timed to gain an edge.
- Nothing is pressed into any window but the game's.

VoiceAttack is used openly with Star Citizen to do exactly this. Whether CIG
has said anything about it was not checked when this was written. That is
open, and the switch's own text says what it does.

## Not yet measured

- **Whether the game takes the keys.** Nothing has been pressed into a
  running game yet. The first test is one command.
- **The keyboard layout.** Scan codes should be right on a French layout,
  but that is unverified.
- **Elevation.** If the game runs as administrator and the app does not,
  Windows drops the keys without saying so (UIPI), and nothing in the app can
  see that.
- **Recognition with a real voice** through a headset, with the game running.
