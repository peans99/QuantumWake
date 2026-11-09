import streamDeck, { SingletonAction } from "@elgato/streamdeck";
import "../data/commands.js"; // defines globalThis.SC_COMMANDS / SC_GROUPS
import { readFileSync } from "node:fs";
import { parseCombo, press, release, foregroundProcessName } from "./keys.js";
import { renderKey, svgDataUrl, prettyKey, classify } from "./icons.js";

const log = streamDeck.logger.createScope("sc");
const COMMANDS = new Map(globalThis.SC_COMMANDS.map((c) => [c.id, c]));
const GROUPS = globalThis.SC_GROUPS;
const PALETTE = JSON.parse(readFileSync(new URL('../data/appearance.json', import.meta.url), 'utf8'));

/** Global settings (shared by all keys), edited from any key's Property Inspector. */
let globals = { onlyWhenFocused: true, layout: "physical" };

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** Resolve the effective command for a key's settings. */
function resolve(settings) {
  const cmd = COMMANDS.get(settings.command) ?? COMMANDS.get("flight_ready");
  const key = (settings.keyOverride || "").trim() || cmd.key;
  const holdRaw = settings.holdMs;
  const hold = holdRaw === undefined || holdRaw === "" ? cmd.hold : Math.max(0, Number(holdRaw) || 0);
  const label = (settings.label || "").replace(/\\n/g, "\n") || cmd.label;
  const appearance = classify({ id: cmd.id, group: GROUPS[cmd.group]?.name || cmd.group, label }, PALETTE);
  const color = /^#[0-9a-f]{6}$/i.test(settings.color || "") ? settings.color : appearance.color;
  return { cmd, key, hold, label, color, icon: settings.icon || appearance.icon, tap: settings.tap === undefined ? !!cmd.tap : !!settings.tap };
}

function face(settings, pressed = false, warn = false) {
  const r = resolve(settings);
  return svgDataUrl(renderKey({
    label: r.label, color: r.color, icon: r.icon,
    keyText: settings.hideKey ? "" : prettyKey(r.key), pressed, warn,
  }));
}

class ScCommand extends SingletonAction {
  manifestId = "com.nlebel.starcitizen.command";

  /** action id -> { inputs, downAt, releasing } for keys currently held down. */
  held = new Map();

  async onWillAppear(ev) {
    let settings = ev.payload.settings;
    if (!settings.command) {
      settings = { ...settings, command: "flight_ready" };
      await ev.action.setSettings(settings);
    }
    await ev.action.setImage(face(settings));
  }

  async onDidReceiveSettings(ev) {
    await ev.action.setImage(face(ev.payload.settings));
  }

  async onKeyDown(ev) {
    const settings = ev.payload.settings;
    const r = resolve(settings);
    if (this.held.has(ev.action.id)) return;
    try {
      if (globals.onlyWhenFocused) {
        const fg = foregroundProcessName();
        if (!fg || !/^starcitizen/i.test(fg)) {
          log.info(`Skipped ${r.cmd.id}: foreground is ${fg}`);
          await ev.action.showAlert();
          return;
        }
      }
      const inputs = parseCombo(r.key, globals.layout);
      const state = { inputs, downAt: Date.now(), hold: r.hold };
      this.held.set(ev.action.id, state);
      ev.action.setImage(face(settings, true));
      if (r.tap) {
        // Fixed quick tap, independent of how long the button is held (e.g. F5 tap = +1 pip, hold = max).
        state.tapDone = (async () => { await press(inputs); await sleep(60); await release(inputs); })();
        await state.tapDone;
      } else {
        await press(inputs);
      }
      log.debug(`Down ${r.cmd.id} (${r.key})`);
    } catch (err) {
      log.error(`${r.cmd.id}: ${err.message}`);
      this.held.delete(ev.action.id);
      await ev.action.showAlert();
    }
  }

  async onKeyUp(ev) {
    const state = this.held.get(ev.action.id);
    if (!state) return;
    if (state.tapDone) {
      await state.tapDone.catch(() => {});
      this.held.delete(ev.action.id);
      await ev.action.setImage(face(ev.payload.settings, false));
      return;
    }
    const elapsed = Date.now() - state.downAt;
    // Keep the key down for at least `hold` ms (long-press binds), and at least 60 ms so taps register.
    const wait = Math.max(state.hold, 60) - elapsed;
    if (wait > 0) await sleep(wait);
    try {
      await release(state.inputs);
    } catch (err) {
      log.error(err.message);
    } finally {
      this.held.delete(ev.action.id);
      await ev.action.setImage(face(ev.payload.settings, false));
    }
  }

  async onWillDisappear(ev) {
    // Never leave a key stuck down if the page changes mid-press.
    const state = this.held.get(ev.action.id);
    if (state) {
      this.held.delete(ev.action.id);
      await release(state.inputs).catch(() => {});
    }
  }

}

streamDeck.settings.onDidReceiveGlobalSettings((ev) => {
  globals = { ...globals, ...ev.settings };
  log.info(`Global settings: ${JSON.stringify(globals)}`);
});

streamDeck.actions.registerAction(new ScCommand());
await streamDeck.connect();
const g = await streamDeck.settings.getGlobalSettings();
globals = { ...globals, ...g };
