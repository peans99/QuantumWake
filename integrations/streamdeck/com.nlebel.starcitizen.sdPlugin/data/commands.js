// Star Citizen command catalog — shared by the plugin (bin/plugin.js) and the Property Inspector.
// Plain script (no import/export) so it loads both as an ES module in Node and as a <script> in the PI.
//
// Every key here is the game's STOCK DEFAULT keyboard/mouse binding, taken from
// Data/Libs/Config/defaultProfile.xml of Star Citizen LIVE (Nov 2026). Nothing needs binding in game.
//
// key  : SC key name(s) joined with "+", same spelling as the game's keybind files ("ralt+r", "lalt+mouse1").
// hold : minimum ms the key stays down even on a quick tap (for the game's long-press binds).
//        0 = the game key mirrors the Stream Deck button (press = down, release = up).
// tap  : always send a quick ~60 ms tap, however long the button is held (for binds where a
//        long press does something else, e.g. F5 tap = +1 pip but F5 hold = max).
// color: optional override of the group color.

globalThis.SC_GROUPS = {
  ship: { name: "Ship & flight", color: "#22d3ee" },
  defense: { name: "Countermeasures", color: "#ef4444" },
  targeting: { name: "Targeting", color: "#f97316" },
  weapons: { name: "Weapons", color: "#3b82f6" },
  power: { name: "Power pips", color: "#e2e8f0" },
  mining: { name: "Mining", color: "#eab308" },
  salvage: { name: "Salvage", color: "#14b8a6" },
  ground: { name: "Ground vehicle", color: "#84cc16" },
  utility: { name: "Utilities", color: "#a855f7" },
  misc: { name: "Other", color: "#94a3b8" },
};

globalThis.SC_COMMANDS = [
  // --- Ship ---------------------------------------------------------------
  { id: "flight_ready", group: "ship", label: "Flight\nReady", key: "ralt+r", hold: 0 },
  { id: "power", group: "ship", label: "Power\nAll", key: "u", hold: 0 },
  { id: "power_engines", group: "ship", color: "#eab308", label: "Engines", key: "i", hold: 0 },
  { id: "power_shields", group: "ship", color: "#3b82f6", label: "Shield\nPower", key: "o", hold: 0 },
  { id: "power_weapons", group: "ship", color: "#ef4444", label: "Weapon\nPower", key: "p", hold: 0 },
  { id: "power_reset", group: "ship", label: "Power\nReset", key: "f8", hold: 0 },
  { id: "gear", group: "ship", label: "Landing\nGear", key: "n", hold: 0 },
  { id: "autoland", group: "ship", label: "Auto\nLand", key: "n", hold: 450,
    note: "Long press on N; the plugin holds it 450 ms for you." },
  { id: "vtol", group: "ship", label: "VTOL", key: "k", hold: 0 },
  { id: "transform", group: "ship", label: "Wings /\nConfig", key: "lalt+k", hold: 0, tap: true,
    note: "Cycle configuration (fold/unfold wings on ships that have it). Default LAlt+K, tap." },
  { id: "atc", group: "ship", label: "Request\nLanding", key: "lalt+n", hold: 0 },
  { id: "loading_area", group: "ship", label: "Loading\nArea", key: "ralt+n", hold: 0,
    note: "Same key requests docking when in docking mode." },
  { id: "master_mode", group: "ship", label: "NAV /\nSCM", key: "b", hold: 450,
    note: "Long press on B; the plugin holds it 450 ms for you." },
  { id: "quantum", group: "ship", label: "Quantum\nJump", key: "mouse1", hold: 450,
    note: "In NAV mode with a destination set: long left-click engages quantum." },
  { id: "decoupled", group: "ship", label: "Decouple", key: "c", hold: 0 },
  { id: "spacebrake", group: "ship", label: "Space\nBrake", key: "x", hold: 0, note: "Hold the button." },
  { id: "lights", group: "ship", label: "Lights", key: "l", hold: 0 },
  { id: "speed_up", group: "ship", label: "Speed\nLimit +", key: "lalt+mwheel_up", hold: 0 },
  { id: "speed_down", group: "ship", label: "Speed\nLimit −", key: "lalt+mwheel_down", hold: 0 },
  { id: "exit_seat", group: "ship", label: "Exit\nSeat", key: "y", hold: 0, note: "Hold the button to exit." },

  // --- Combat: countermeasures (red), targeting (orange), weapons (blue) ---------------------------------------------------
  { id: "decoy", group: "defense", label: "Decoy", key: "h", hold: 0 },
  { id: "noise", group: "defense", label: "Noise", key: "j", hold: 0 },
  { id: "gimbal", group: "weapons", label: "Gimbal", key: "g", hold: 0 },
  { id: "operator_cycle", group: "weapons", label: "Guns /\nMissile", key: "mouse3", hold: 0,
    note: "Cycles operator mode (middle mouse)." },
  { id: "target_hostile", group: "targeting", label: "Hostile", key: "5", hold: 0, tap: true },
  { id: "target_attacker", group: "targeting", label: "Attacker", key: "4", hold: 0, tap: true },
  { id: "target_friendly", group: "targeting", label: "Friendly", key: "6", hold: 0, tap: true },
  { id: "target_all", group: "targeting", label: "Next\nTarget", key: "7", hold: 0, tap: true },
  { id: "target_view", group: "targeting", label: "In View", key: "t", hold: 0, tap: true,
    note: "Always sent as a quick tap (holding T in game would toggle auto-targeting)." },
  { id: "target_subitem", group: "targeting", label: "Sub-\nComp", key: "r", hold: 0, tap: true },
  { id: "target_unlock", group: "targeting", label: "Unlock", key: "lalt+t", hold: 0, tap: true },
  { id: "pins_clear", group: "targeting", label: "Clear\nPins", key: "0", hold: 0, tap: true },
  { id: "target_hail", group: "targeting", label: "Hail", key: "9", hold: 0, tap: true },
  { id: "missiles_more", group: "weapons", label: "Missile\n+1", key: "g", hold: 0, tap: true,
    note: "In missile mode, G arms one more missile." },
  { id: "missiles_reset", group: "weapons", label: "Missile\nReset", key: "lalt+g", hold: 0, tap: true },

  // --- Power pips (F5 weapons, F6 engines, F7 shields: tap = +1, LAlt+tap = −1, hold = max) ----
  { id: "pip_wpn_up", group: "power", color: "#ef4444", label: "WPN\n+", key: "f5", hold: 0, tap: true },
  { id: "pip_wpn_down", group: "power", color: "#ef4444", label: "WPN\n−", key: "lalt+f5", hold: 0, tap: true },
  { id: "pip_wpn_max", group: "power", color: "#ef4444", label: "WPN\nMAX", key: "f5", hold: 450 },
  { id: "pip_wpn_min", group: "power", color: "#ef4444", label: "WPN\nMIN", key: "lalt+f5", hold: 450 },
  { id: "pip_eng_up", group: "power", color: "#eab308", label: "ENG\n+", key: "f6", hold: 0, tap: true },
  { id: "pip_eng_down", group: "power", color: "#eab308", label: "ENG\n−", key: "lalt+f6", hold: 0, tap: true },
  { id: "pip_eng_max", group: "power", color: "#eab308", label: "ENG\nMAX", key: "f6", hold: 450 },
  { id: "pip_eng_min", group: "power", color: "#eab308", label: "ENG\nMIN", key: "lalt+f6", hold: 450 },
  { id: "pip_shd_up", group: "power", color: "#3b82f6", label: "SHD\n+", key: "f7", hold: 0, tap: true },
  { id: "pip_shd_down", group: "power", color: "#3b82f6", label: "SHD\n−", key: "lalt+f7", hold: 0, tap: true },
  { id: "pip_shd_max", group: "power", color: "#3b82f6", label: "SHD\nMAX", key: "f7", hold: 450 },
  { id: "pip_shd_min", group: "power", color: "#3b82f6", label: "SHD\nMIN", key: "lalt+f7", hold: 450 },
  { id: "pip_reset", group: "power", label: "Reset\nPips", key: "f8", hold: 0, tap: true },
  { id: "power_up", group: "power", label: "Power\n+", key: "f10", hold: 0,
    note: "Raises total power output; hold to keep raising." },
  { id: "power_down", group: "power", label: "Power\n−", key: "f9", hold: 0,
    note: "Lowers total power output; hold to keep lowering." },

  // --- Mining ---------------------------------------------------------------
  { id: "mining_mode", group: "mining", label: "Mining\nMode", key: "m", hold: 0,
    note: "M toggles the ship's tool mode (mining, salvage or refuel)." },
  { id: "mining_fire", group: "mining", label: "Laser\nOn/Off", key: "mouse1", hold: 0,
    note: "Left click; a tap toggles the laser." },
  { id: "mining_switch", group: "mining", label: "Laser\nType", key: "lalt+mouse1", hold: 0,
    note: "Switches between fracture and extraction laser." },
  { id: "mining_power_up", group: "mining", label: "Laser\nPower +", key: "mwheel_up", hold: 0,
    note: "Uses the mouse wheel: one notch per press." },
  { id: "mining_power_down", group: "mining", label: "Laser\nPower −", key: "mwheel_down", hold: 0,
    note: "Uses the mouse wheel: one notch per press." },
  { id: "mining_mod1", group: "mining", label: "Module\n1", key: "lalt+1", hold: 0 },
  { id: "mining_mod2", group: "mining", label: "Module\n2", key: "lalt+2", hold: 0 },
  { id: "mining_mod3", group: "mining", label: "Module\n3", key: "lalt+3", hold: 0 },
  { id: "mining_perm", group: "mining", label: "Perm.\nModule", key: "lalt+4", hold: 0 },
  { id: "jettison", group: "mining", label: "Jettison\nCargo", key: "lalt+j", hold: 0 },
  { id: "scan_mode", group: "mining", label: "Scan\nMode", key: "v", hold: 0 },
  { id: "ping", group: "mining", label: "Ping", key: "tab", hold: 0, note: "Hold the button to charge the ping." },

  // --- Salvage --------------------------------------------------------------
  { id: "salvage_mode", group: "salvage", label: "Salvage\nMode", key: "m", hold: 0,
    note: "M toggles the ship's tool mode (mining, salvage or refuel)." },
  { id: "salvage_fire_left", group: "salvage", label: "Fire\nLeft", key: "ralt+a", hold: 0 },
  { id: "salvage_fire_right", group: "salvage", label: "Fire\nRight", key: "ralt+d", hold: 0 },
  { id: "salvage_fire_fracture", group: "salvage", label: "Fire\nFracture", key: "ralt+w", hold: 0 },
  { id: "salvage_fire_disint", group: "salvage", label: "Fire\nDisinteg.", key: "ralt+s", hold: 0 },
  { id: "salvage_focus_all", group: "salvage", label: "Focus\nAll", key: "lalt+s", hold: 0 },
  { id: "salvage_focus_left", group: "salvage", label: "Focus\nLeft", key: "lalt+a", hold: 0 },
  { id: "salvage_focus_right", group: "salvage", label: "Focus\nRight", key: "lalt+d", hold: 0 },
  { id: "salvage_focus_fracture", group: "salvage", label: "Focus\nFracture", key: "lalt+w", hold: 0 },
  { id: "salvage_gimbal", group: "salvage", label: "Gimbal\nMode", key: "g", hold: 0 },
  { id: "salvage_gimbal_reset", group: "salvage", label: "Reset\nGimbal", key: "lalt+g", hold: 0 },
  { id: "salvage_beam_axis", group: "salvage", label: "Beam\nAxis", key: "lalt+mouse2", hold: 0,
    note: "Toggles the beam spacing axis (Alt + right click)." },

  // --- Ground vehicle ---------------------------------------------------------
  { id: "gv_start", group: "ground", label: "Start\nVehicle", key: "ralt+r", hold: 0 },
  { id: "gv_power", group: "ground", label: "Power", key: "u", hold: 0 },
  { id: "gv_lights", group: "ground", label: "Head-\nlights", key: "l", hold: 0 },
  { id: "gv_horn", group: "ground", label: "Horn", key: "space", hold: 0, note: "Hold the button." },
  { id: "gv_boost", group: "ground", label: "Boost", key: "lshift", hold: 0, note: "Hold the button." },
  { id: "gv_brake", group: "ground", label: "Brake", key: "x", hold: 0, note: "Hold the button." },
  { id: "gv_autobrake", group: "ground", label: "Brake on\nIdle", key: "c", hold: 0 },
  { id: "gv_lock_rot", group: "ground", label: "Lock\nRotation", key: "rshift", hold: 0 },
  { id: "freelook", group: "ground", label: "Free\nLook", key: "z", hold: 0, note: "Hold the button." },
  { id: "camera", group: "ground", label: "Camera\nView", key: "f4", hold: 0 },
  { id: "gv_weapons", group: "ground", label: "Weapon\nPower", key: "p", hold: 0 },
  { id: "gv_exit", group: "ground", label: "Exit\nVehicle", key: "y", hold: 0, note: "Hold the button to exit." },

  // --- Utilities ----------------------------------------------------------------
  { id: "mobiglas", group: "utility", label: "mobiGlas", key: "f1", hold: 0 },
  { id: "starmap", group: "utility", label: "Star\nMap", key: "f2", hold: 0 },
  { id: "night_vision", group: "utility", label: "Night\nVision", key: "ralt+l", hold: 0 },
  { id: "visor_wipe", group: "utility", label: "Visor\nWipe", key: "lalt+x", hold: 0 },
  { id: "flashlight", group: "utility", label: "Flash-\nlight", key: "t", hold: 0, note: "On foot only (in a seat, T targets)." },
  { id: "look_behind", group: "utility", label: "Look\nBehind", key: "comma", hold: 0, note: "Hold the button." },
  { id: "chat", group: "utility", label: "Chat", key: "f12", hold: 0 },
  { id: "cursor", group: "utility", label: "Mouse\nCursor", key: "f11", hold: 0 },
  { id: "screenshot_view", group: "utility", label: "Camera\nView", key: "f4", hold: 0 },
  { id: "emergency_exit", group: "utility", label: "Emerg.\nExit", key: "lshift+u", hold: 0 },
  { id: "eject", group: "utility", label: "EJECT", key: "ralt+y", hold: 0 },
  { id: "self_destruct", group: "utility", label: "Self\nDestruct", key: "backspace", hold: 0,
    note: "Hold the button — the game requires a long press." },

  // --- Other ------------------------------------------------------------------------
  { id: "custom", group: "misc", label: "Custom", key: "", hold: 0, note: "Type any key combo in Key override." },
];
