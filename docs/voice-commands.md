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
- **A closed grammar, not dictation.** The recogniser is told what may be
  said (`VoiceCommands.Grammar()`) and nothing else. Until 1.2.4 that was 50
  fixed sentences, so "power to shields" worked and "full power to the
  shields" did not. Since 1.2.5 each command is a few patterns
  (`PhrasePattern`), `(full|max) power to [the] (shield|shields)`, handed
  to the recogniser as structure, so it costs no more than one sentence.
  Any command may open with "copilot" and end with "please". See the
  vocabulary below.
- **Measured** with the Windows voice speaking into a WAV file, recognised
  against the 1.2.5 grammar (2026-11-07):
  - Long wordings came back at 0.99: "full power to the front shields",
    "divert shields to the rear please", "shields to the right", "full shield
    back", "copilot gear down".
  - Short commands came back at 0.92–0.99, except "mining mode" at 0.75
    (0.93 with the old list). It is still over the bar, but it is the one to
    watch.
  - "What a lovely day" matched nothing.
  - The old list was worse at the long wordings, not just deaf to them. It
    heard "full power to the front shields" as "full power to shields" (0.86),
    which is the wrong action.
  - **"Nav" is not recognised at all**, with the old list or the new one: the
    recogniser has no pronunciation for the word. "Navigation mode" comes back
    at 0.98, so say that.
  - **The cost of a closed grammar:** speech close to a command is pulled
    onto it. "Shields to the side" came back as "shields to the starboard"
    (0.86). A wording that is no command at all ("what a lovely day") is
    dropped; one that is nearly a command may not be.
  - A real voice over game noise will score lower, so the confidence bar
    exists.
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
  cancels rather than fires. Shields off waits on "confirm" too (1.2.5):
  "on" and "off" are a sound apart, and a misheard "on" must not drop the
  shields in a fight.
- **Abort self destruct** presses the same key again, which is how the game
  stops the countdown. With no countdown running, that key *starts* one. So
  abort presses at once only within five minutes of a self destruct this
  copilot confirmed (1.2.5); otherwise it asks "Confirm abort self
  destruct?" first. Five minutes is a generous guess at the countdown, not a
  measured figure. Saying "abort self destruct" while a self destruct waits
  on confirm is a no, not a second question.

## What it understands (1.2.5)

Each line is the main phrase, then the kinds of wording that also work. The
test `Every_wording_belongs_to_one_command_only` spells every pattern out,
more than 5,000 wordings, and fails if any belongs to two commands or is
"confirm" or "cancel".

| Command | Also heard as |
|---|---|
| power to shields | full / max / maximum / all power to (the) shield(s); shields to max; max shields |
| increase shields | increase / raise / boost (the) shields (power); more power to (the) shields; more shields; shields up |
| decrease shields | decrease / lower / reduce (the) shields (power); less power to (the) shields; shields down |
| minimum shields | min / minimum / minimize (the) shields; shields to minimum |
| front, rear, left, right, top, bottom shields | (raise / boost / full / max / more / divert / angle / shift / focus …) (power to) (the) front shields; shields (power) (to) (the) front. Front is also forward / fore / nose; rear is back / aft / stern; left is port; right is starboard; top is dorsal / upper; bottom is belly / ventral / underside |
| balance shields | reset / balance / equalize / center / level (the) shields (faces); even out the shields; shields reset |
| shields on, shields off, toggle shields | shields on / online; turn (the) shields on; enable / activate shields; and the same with off / disable / kill / drop |
| power to weapons, power to engines | the same shapes as shields, with weapons / guns and engines / thrusters; more / less power to …; balance power, reset power |
| gear down, gear up, toggle landing gear | (landing) gear down; deploy / lower / drop (the) (landing) gear; and up / retract / raise |
| gun, mining, salvage, scan, quantum, combat, navigation mode | (switch to / go to / enter / set / engage) … mode; switch to mining; S C M; weapons hot |
| ping, scan target | (radar) ping; send a ping; scan; scan the area · scan (the) target / object / contact / ship |
| mining: laser, switch laser, beam, modules, more / less power | fire / stop / start / toggle (the) (mining) laser; laser on; fracture / extraction mode; beam high; (activate / use) module one; first module; mining power up |
| doors, request landing, jettison, self destruct | open / close / shut (all) (the) doors · request landing / departure / takeoff / clearance, call the tower · jettison / dump / eject (the) cargo · (initiate / start …) self destruct (sequence), abort / stop / cancel self destruct |

"Up" and "down" are deliberately not shield-face words: "shields up" and
"lower shields" are power. "Lower" in "lower the gear" is landing gear,
because there is no "shields" in it.

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

As it was in 1.2.0, against this install's catalogue and Nicolas's
`actionmaps.xml` on 2026-11-07, 13 of the 24 commands pressed a key:

- **Can press:**
  - The mining set: mining and salvage mode, both on M; laser, switch laser,
    beam, modules 1–3 and jettison.
  - Scan mode, ping, power to shields, request landing, and self destruct and
    its abort.
- **Only on a stick:** guns, combat and nav mode, scan target, less power,
  and the doors. Each says which button it is on.
- **No key at all:** quantum mode.
- **On the mouse wheel:** more power.

With the 1.2.5 commands, against the same files, 27 of 47 press a key.
The power set (shields, weapons and engines on F5–F8) and toggle shields (O)
are all bound by default. More power is still on the mouse wheel. The other
19 have no keyboard key:

- The eight from 1.2.0: guns, quantum, combat and nav mode, scan target,
  less power, and both door commands.
- The six shield faces, shield reset, and shields on and off. They are on
  his sticks: js4's hat and buttons 11–13, and js6 buttons 16 and 18.
- Gear down and gear up, which are on nothing.

### Spare keys (1.2.5)

Settings → Voice commands lists the commands with no keyboard key and offers
a free key for each (`VoiceBinding.SpareKeys`):

- **Which keys:** right Ctrl, then right Alt, with numpad 1–0 then F1–F12.
  The game leaves these chords free by default.
- **What is skipped:** any chord already used, by any of the game's 1,103
  default actions or by the pilot's profile. A key given to a voice command
  would be taken off that action, because a binding moves an exact input. A
  modifier bound on its own anywhere is not used at all, since holding it
  would fire that action as well.
- **What gets a key:** only a command with no keyboard key, whether it was
  never bound, is on a stick only, or was cleared. A key the app cannot press
  (the mouse wheel) is left as the pilot set it, and so is a stick binding: a
  keyboard key is added beside it.
- **How it is written:** through `/api/controls/bindings`, the same path as
  the Controls page. With the game closed, the profile is backed up and then
  rewritten. With it running, an import file `quantumwake-voice` is written
  for the keybinding screen or `pp_rebindkeys`.
- **On his files** it proposes 19 keys: RCtrl + Numpad 1–0 and RCtrl +
  F1–F9. Guns, quantum, combat and nav mode, scan target, less power, the
  doors, gear, the faces, shield reset and shields on/off.

**Chords written modifier-last** are pressed modifier first since 1.2.5.
The game writes Shields Decrease as `f7+lalt`, and pressing in written
order put F7 down alone first, which is Shields Increase.

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
- **Shield faces on a given ship.** The actions are in the 4.10 controls
  file and Nicolas has them on a stick, but whether a ship has faces to raise
  is the game's to say.
- **Chords on RCtrl** pressed by SendInput: the game tells LAlt+F7 from F7,
  so RCtrl + F1 should not open the mobiGlas, but this has not been tried.
