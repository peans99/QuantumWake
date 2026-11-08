// Keyboard/mouse injection for Star Citizen via user32 SendInput.
// Star Citizen reads raw keyboard input, so we send hardware scan codes (KEYEVENTF_SCANCODE)
// rather than virtual keys. Keys are named exactly like in SC keybind files ("ralt+r", "np_8"...).
import koffi from "koffi";

const user32 = koffi.load("user32.dll");
const kernel32 = koffi.load("kernel32.dll");

const KEYBDINPUT = koffi.struct("KEYBDINPUT", {
  wVk: "uint16", wScan: "uint16", dwFlags: "uint32", time: "uint32", dwExtraInfo: "uintptr",
});
const MOUSEINPUT = koffi.struct("MOUSEINPUT", {
  dx: "int32", dy: "int32", mouseData: "uint32", dwFlags: "uint32", time: "uint32", dwExtraInfo: "uintptr",
});
const INPUT = koffi.struct("INPUT", { type: "uint32", u: koffi.union("INPUT_U", { mi: MOUSEINPUT, ki: KEYBDINPUT }) });
const INPUT_SIZE = koffi.sizeof(INPUT);

const SendInput = user32.func("uint32 __stdcall SendInput(uint32 cInputs, INPUT *pInputs, int cbSize)");
const MapVirtualKeyExW = user32.func("uint32 __stdcall MapVirtualKeyExW(uint32 uCode, uint32 uMapType, void *dwhkl)");
const VkKeyScanExW = user32.func("int16 __stdcall VkKeyScanExW(uint16 ch, void *dwhkl)");
const GetKeyboardLayout = user32.func("void * __stdcall GetKeyboardLayout(uint32 idThread)");
const GetForegroundWindow = user32.func("void * __stdcall GetForegroundWindow()");
const GetWindowThreadProcessId = user32.func("uint32 __stdcall GetWindowThreadProcessId(void *hWnd, _Out_ uint32 *pid)");
const OpenProcess = kernel32.func("void * __stdcall OpenProcess(uint32 access, bool inherit, uint32 pid)");
const CloseHandle = kernel32.func("bool __stdcall CloseHandle(void *h)");
const QueryFullProcessImageNameW = kernel32.func(
  "bool __stdcall QueryFullProcessImageNameW(void *h, uint32 flags, _Out_ uint8 *name, _Inout_ uint32 *size)",
);

const INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
const KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2, KEYEVENTF_SCANCODE = 0x8;
const EXT = 0x100; // marker: extended (E0-prefixed) scan code

// Scan codes (set 1) for SC key names, by PHYSICAL position on a US/QWERTY board —
// that is what Star Citizen binds against.
const SCAN = {
  escape: 0x01, "1": 0x02, "2": 0x03, "3": 0x04, "4": 0x05, "5": 0x06, "6": 0x07, "7": 0x08, "8": 0x09, "9": 0x0a, "0": 0x0b,
  minus: 0x0c, equals: 0x0d, backspace: 0x0e, tab: 0x0f,
  q: 0x10, w: 0x11, e: 0x12, r: 0x13, t: 0x14, y: 0x15, u: 0x16, i: 0x17, o: 0x18, p: 0x19,
  lbracket: 0x1a, rbracket: 0x1b, enter: 0x1c, lctrl: 0x1d,
  a: 0x1e, s: 0x1f, d: 0x20, f: 0x21, g: 0x22, h: 0x23, j: 0x24, k: 0x25, l: 0x26,
  semicolon: 0x27, apostrophe: 0x28, grave: 0x29, lshift: 0x2a, backslash: 0x2b,
  z: 0x2c, x: 0x2d, c: 0x2e, v: 0x2f, b: 0x30, n: 0x31, m: 0x32, comma: 0x33, period: 0x34, slash: 0x35,
  rshift: 0x36, np_multiply: 0x37, lalt: 0x38, space: 0x39, capslock: 0x3a,
  f1: 0x3b, f2: 0x3c, f3: 0x3d, f4: 0x3e, f5: 0x3f, f6: 0x40, f7: 0x41, f8: 0x42, f9: 0x43, f10: 0x44,
  numlock: 0x45, scrolllock: 0x46,
  np_7: 0x47, np_8: 0x48, np_9: 0x49, np_subtract: 0x4a, np_4: 0x4b, np_5: 0x4c, np_6: 0x4d, np_add: 0x4e,
  np_1: 0x4f, np_2: 0x50, np_3: 0x51, np_0: 0x52, np_period: 0x53, f11: 0x57, f12: 0x58,
  // extended keys
  np_enter: EXT | 0x1c, rctrl: EXT | 0x1d, np_divide: EXT | 0x35, ralt: EXT | 0x38,
  home: EXT | 0x47, up: EXT | 0x48, pgup: EXT | 0x49, left: EXT | 0x4b, right: EXT | 0x4d,
  end: EXT | 0x4f, down: EXT | 0x50, pgdn: EXT | 0x51, insert: EXT | 0x52, delete: EXT | 0x53,
};
const ALIASES = { esc: "escape", return: "enter", ctrl: "lctrl", alt: "lalt", shift: "lshift", del: "delete", ins: "insert",
  pageup: "pgup", pagedown: "pgdn", altgr: "ralt" };
const FKEYS_HI = { f13: 0x64, f14: 0x65, f15: 0x66, f16: 0x67, f17: 0x68, f18: 0x69, f19: 0x6a, f20: 0x6b, f21: 0x6c, f22: 0x6d, f23: 0x6e, f24: 0x76 };
const MODIFIERS = new Set(["lctrl", "rctrl", "lalt", "ralt", "lshift", "rshift"]);
const MOUSE = {
  mouse1: [0x0002, 0x0004], // left down/up
  mouse2: [0x0008, 0x0010], // right
  mouse3: [0x0020, 0x0040], // middle
};
// Mouse wheel is a single pulse (one notch per button press), not a held input.
const MOUSEEVENTF_WHEEL = 0x0800;
const WHEEL = { mwheel_up: 120, mwheel_down: -120 };

// Keys whose character differs between layouts (used only in "typed" layout mode).
const TYPED_CHARS = new Set("abcdefghijklmnopqrstuvwxyz0123456789".split(""));

/**
 * Parse an SC key string like "rctrl+np_8" or "u+lshift" into an ordered list of inputs
 * (modifiers first). Throws on unknown key names.
 * @param {string} combo
 * @param {"physical"|"typed"} layout physical = QWERTY position (SC default); typed = key that types that char on your layout
 */
export function parseCombo(combo, layout = "physical") {
  const parts = String(combo).toLowerCase().split("+").map((s) => s.trim()).filter(Boolean)
    .map((s) => ALIASES[s] ?? s);
  if (!parts.length) throw new Error("No key set");
  const mods = [], keys = [];
  for (const name of parts) {
    let input;
    if (MOUSE[name]) input = { name, mouse: MOUSE[name] };
    else if (WHEEL[name]) input = { name, wheel: WHEEL[name] };
    else if (FKEYS_HI[name]) input = { name, scan: FKEYS_HI[name] };
    else if (SCAN[name] !== undefined) {
      let scan = SCAN[name];
      if (layout === "typed" && TYPED_CHARS.has(name)) scan = typedScan(name) ?? scan;
      input = { name, scan };
    } else throw new Error(`Unknown key "${name}"`);
    (MODIFIERS.has(name) ? mods : keys).push(input);
  }
  return [...mods, ...keys];
}

function typedScan(ch) {
  const hkl = GetKeyboardLayout(0);
  const r = VkKeyScanExW(ch.charCodeAt(0), hkl);
  if (r === -1) return null;
  const sc = MapVirtualKeyExW(r & 0xff, 0 /* MAPVK_VK_TO_VSC */, hkl);
  return sc || null;
}

function send(input, up) {
  let ev;
  if (input.wheel) {
    if (up) return; // nothing to release
    ev = { type: INPUT_MOUSE, u: { mi: { dx: 0, dy: 0, mouseData: input.wheel >>> 0, dwFlags: MOUSEEVENTF_WHEEL, time: 0, dwExtraInfo: 0 } } };
  } else if (input.mouse) {
    ev = { type: INPUT_MOUSE, u: { mi: { dx: 0, dy: 0, mouseData: 0, dwFlags: input.mouse[up ? 1 : 0], time: 0, dwExtraInfo: 0 } } };
  } else {
    let flags = KEYEVENTF_SCANCODE | (up ? KEYEVENTF_KEYUP : 0);
    if (input.scan & EXT) flags |= KEYEVENTF_EXTENDEDKEY;
    ev = { type: INPUT_KEYBOARD, u: { ki: { wVk: 0, wScan: input.scan & 0xff, dwFlags: flags, time: 0, dwExtraInfo: 0 } } };
  }
  const n = SendInput(1, ev, INPUT_SIZE);
  if (n !== 1) throw new Error("SendInput was blocked (is Star Citizen running as administrator?)");
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const GAP = 25; // ms between modifier and key events so the game sees the chord

/** Press all inputs in order (modifiers first). */
export async function press(inputs) {
  for (let i = 0; i < inputs.length; i++) {
    send(inputs[i], false);
    if (i < inputs.length - 1) await sleep(GAP);
  }
}

/** Release all inputs in reverse order. */
export async function release(inputs) {
  for (let i = inputs.length - 1; i >= 0; i--) {
    send(inputs[i], true);
    if (i > 0) await sleep(GAP);
  }
}

/** Name of the foreground process executable, e.g. "StarCitizen.exe" (or null). */
export function foregroundProcessName() {
  const hwnd = GetForegroundWindow();
  if (!hwnd) return null;
  const pid = [0];
  GetWindowThreadProcessId(hwnd, pid);
  if (!pid[0]) return null;
  const h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid[0]);
  if (!h) return null;
  try {
    const buf = Buffer.alloc(1024);
    const size = [512];
    if (!QueryFullProcessImageNameW(h, 0, buf, size)) return null;
    const full = buf.toString("utf16le", 0, size[0] * 2);
    return full.split("\\").pop();
  } finally {
    CloseHandle(h);
  }
}

/** Pretty label for a combo, e.g. "RCTRL+NUM8". */
export function prettyCombo(combo) {
  return String(combo).toUpperCase().split("+").map((p) => p.trim()
    .replace(/^NP_(\d)$/, "NUM$1").replace("NP_PERIOD", "NUM.").replace("NP_MULTIPLY", "NUM*")
    .replace("NP_DIVIDE", "NUM/").replace("NP_ADD", "NUM+").replace("NP_SUBTRACT", "NUM-")
    .replace("BACKSPACE", "BKSP").replace(/^MOUSE1$/, "LMB").replace(/^MOUSE2$/, "RMB").replace(/^MOUSE3$/, "MMB")
    .replace("MWHEEL_UP", "WHEEL▲").replace("MWHEEL_DOWN", "WHEEL▼")).join("+");
}
