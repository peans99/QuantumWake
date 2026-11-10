// Retakes the README screenshots: node tools/retake-screenshots.mjs <out-folder> [name ...]
//
// Run a second server on a COPY of the data (never the real install's folder):
//   QUANTUMWAKE_DATA=<copy> Quantumwake.Server.exe --Port=31395
// The Itinerary shot wants a tracked plan whose stops are on the map; 1.2.18's
// was made on the copy with POST /api/trips (Bloom LEO Rest Stop RR_P3_LEO,
// Starlight Service Station RR_P3_L1, Checkmate RR_P2_L4) and /api/trips/{id}/track.
// The 3D shots render on SwiftShader and take tens of seconds each.
// Retake the README screenshots from a second server on a copy of the data.
// Real time over CDP, not --screenshot: the live stream never lets a virtual
// clock settle, and several shots need the page driven before the capture.
import { spawn } from 'node:child_process';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';

const base = 'http://127.0.0.1:31395/';
const out = process.argv[2];
const only = process.argv.slice(3);
const chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const delay = ms => new Promise(r => setTimeout(r, ms));

const pick = (sel, text) => `(() => { const s = document.querySelector('${sel}'); if (!s) return 'no ${sel}';
  const o = [...s.options].find(o => o.textContent.includes(${JSON.stringify(text)}));
  if (!o) return 'no option ' + ${JSON.stringify(text)} + ' in ' + [...s.options].map(o=>o.textContent).slice(0,40).join('|');
  s.value = o.value; s.dispatchEvent(new Event('change', { bubbles: true })); return 'ok'; })()`;
const tab = pane => `(() => { const b = document.querySelector('#armoury-tabs button[data-pane="${pane}"]'); if (!b) return 'no tab'; b.click(); return 'ok'; })()`;
const model = (find, title, extra = '') => `(async () => { const m = await getJson('/api/models/find?id=' + encodeURIComponent(${JSON.stringify(find)}));
  if (!m?.cls) return 'no model ' + ${JSON.stringify(find)}; openModelView({ cls: m.cls, title: ${JSON.stringify(title)}, paint: modelPaintFor(m.cls) ${extra} }); return 'ok ' + m.cls; })()`;
const compare = `(async () => { const roster = fleetRoster(libraryStats).filter(s => !(s.reference && !s.reference.isSpaceship) && !excludedShips.has(s.name));
  const found = []; for (const s of roster) { if (found.length >= 5) break;
    const m = await getJson('/api/models/find?id=' + encodeURIComponent(s.className || s.name)).catch(() => null);
    if (m?.cls) found.push({ cls: m.cls, name: s.name, paint: modelPaintFor(m.cls) }); }
  openModelView({ cls: found[0].cls, title: tx('Size comparison'), compare: true, fleet: found }); return 'ok ' + found.map(f => f.name).join(', '); })()`;

const shots = [
  { name: 'now', hash: 'now' },
  { name: 'map', hash: 'map', height: 1400, wait: 6000 },
  { name: 'itinerary', hash: 'itinerary' },
  { name: 'operations', hash: 'jobs' },
  { name: 'routes', hash: 'routes', height: 1500, setup: pick('#routes-ship', 'Hermes'), after: 5000 },
  { name: 'reputation', hash: 'reputation' },
  { name: 'events', hash: 'events' },
  { name: 'logbook', hash: 'logbook' },
  { name: 'sessions', hash: 'sessions' },
  { name: 'ledger', hash: 'ledger' },
  { name: 'market', hash: 'market' },
  { name: 'fleet', hash: 'fleet', wait: 6000, after: 800, setup: `(() => { let n = 0;
    for (const t of document.querySelectorAll('#view-fleet *')) if (!t.children.length && t.textContent.trim() === '315p') {
      let c = t; while (c && !/card/.test(c.className || '')) c = c.parentElement; if (c) { c.remove(); n++; } }
    return 'removed ' + n; })()` },
  { name: 'garage', hash: 'garage', setup: pick('#garage-mine', 'F7C-M Super Hornet Mk II'), after: 6000 },
  { name: 'armoury', hash: 'armoury' },
  { name: 'climate', hash: 'armoury', setup: tab('climate'), after: 3000 },
  { name: 'suit', hash: 'armoury', setup: tab('suit'), after: 3000 },
  { name: 'loot', hash: 'loot' },
  { name: 'models', hash: 'models', wait: 6000 },
  { name: 'missions', hash: 'missions' },
  { name: 'encounters', hash: 'encounters' },
  { name: 'ship3d', hash: 'models', setup: model('ANVL_Hornet_F7CM_Mk2', 'Anvil F7C-M Super Hornet Mk II'), after: 25000 },
  { name: 'fleet3d', hash: 'fleet', setup: '(async () => { await openFleetInModels(); return "ok"; })()', after: 40000 },
  { name: 'compare', hash: 'fleet', setup: compare, after: 30000 },
  { name: 'armour3d', hash: 'armoury', setup: '(() => { openModelView({ cls: "rrs_specialist_heavy_core_01_01_12", title: "Morozov-SH Aftershock" }); return "ok"; })()', after: 20000 },
].filter(s => !only.length || only.includes(s.name));

const profile = await fs.mkdtemp(path.join(os.tmpdir(), 'qw-shots-'));
const browser = spawn(chrome, ['--headless=new', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--hide-scrollbars',
  '--remote-debugging-port=0', '--user-data-dir=' + profile, 'about:blank'], { stdio: ['ignore', 'ignore', 'pipe'] });
let port;
browser.stderr.on('data', d => { const m = /ws:\/\/127\.0\.0\.1:(\d+)\//.exec(String(d)); if (m) port = m[1]; });
for (let i = 0; i < 100 && !port; i++) await delay(100);
const tabs = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
const socket = new WebSocket(tabs.find(t => t.type === 'page').webSocketDebuggerUrl);
await new Promise(r => socket.addEventListener('open', r, { once: true }));
let seq = 0; const pending = new Map();
socket.onmessage = e => { const m = JSON.parse(e.data); const p = pending.get(m.id); if (p) { pending.delete(m.id); m.error ? p.reject(new Error(JSON.stringify(m.error))) : p.resolve(m.result); } };
const send = (method, params = {}) => new Promise((resolve, reject) => { const id = ++seq; pending.set(id, { resolve, reject }); socket.send(JSON.stringify({ id, method, params })); });
const run = async expr => (await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true })).result?.value;

await send('Page.enable'); await send('Runtime.enable');
// The 315p has no paint render in the game files (no 300-series hull does),
// so its card is the one flat silhouette among renders: left out, as in 333f936.
await send('Page.navigate', { url: base + 'assets/mark.svg' }); await delay(800);
await run(`localStorage.removeItem('qw-assets-excluded'), 'ok'`);
try {
  for (const shot of shots) {
    const height = shot.height || 1150;
    await send('Emulation.setDeviceMetricsOverride', { width: 1700, height, deviceScaleFactor: 1, mobile: false });
    await send('Page.navigate', { url: 'about:blank' }); await delay(300);
    await send('Page.navigate', { url: base + '#' + shot.hash });
    for (let i = 0; i < 60; i++) { await delay(500); if (await run(`document.body?.dataset.view === ${JSON.stringify(shot.hash)} && typeof libraryStats !== 'undefined' && !!libraryStats`)) break; }
    await delay(shot.wait || 4500);
    let note = '';
    if (shot.setup) { note = await run(shot.setup); await delay(shot.after || 3000); }
    await run('window.scrollTo(0, 0)');
    const shotData = await send('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: 1700, height, scale: 1 } });
    await fs.writeFile(path.join(out, shot.name + '.png'), Buffer.from(shotData.data, 'base64'));
    console.log(shot.name, height, note || '');
  }
} finally {
  try { await send('Browser.close'); } catch { browser.kill(); }
  await delay(500);
  await fs.rm(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 }).catch(() => {});
}
