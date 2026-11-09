// Render the shared vector art into the PNGs used by native Stream Deck folders.
// Command keys stay SVG: the plugin and the browser both call renderKey directly.
import { spawn } from 'node:child_process';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import '../integrations/streamdeck/com.nlebel.starcitizen.sdPlugin/bin/key-art.js';

const root = fileURLToPath(new URL('../', import.meta.url));
const deck = path.join(root, 'integrations/streamdeck');
const palette = JSON.parse(await fs.readFile(path.join(deck, 'com.nlebel.starcitizen.sdPlugin/data/appearance.json'), 'utf8'));
const chrome = process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const profile = await fs.mkdtemp(path.join(os.tmpdir(), 'qw-deck-art-'));
const browser = spawn(chrome, ['--headless=new', '--disable-gpu', '--remote-debugging-port=0', '--user-data-dir=' + profile, 'about:blank'], { windowsHide: true, stdio: 'ignore' });
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
let socket, sequence = 0;
const pending = new Map();
function send(method, params = {}) {
  return new Promise((resolve, reject) => {
    const id = ++sequence; pending.set(id, { resolve, reject }); socket.send(JSON.stringify({ id, method, params }));
  });
}
try {
  let port;
  for (let attempt = 0; attempt < 100; attempt++) {
    try { port = (await fs.readFile(path.join(profile, 'DevToolsActivePort'), 'utf8')).split('\n')[0]; break; }
    catch { await delay(100); }
  }
  if (!port) throw Error('Chrome did not start; set CHROME_PATH to a local Chrome executable.');
  const tabs = await (await fetch('http://127.0.0.1:' + port + '/json')).json();
  socket = new WebSocket(tabs[0].webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', reject, { once: true }); });
  socket.onmessage = event => {
    const message = JSON.parse(event.data), request = pending.get(message.id);
    if (request) { pending.delete(message.id); message.error ? request.reject(message.error) : request.resolve(message.result); }
  };
  await send('Page.enable');
  await send('Emulation.setDeviceMetricsOverride', { width: 144, height: 144, deviceScaleFactor: 1, mobile: false });
  await fs.mkdir(path.join(deck, 'folders'), { recursive: true });
  const images = [...palette.groups, palette.fallback].map(look => ({ ...look, file: 'folders/' + look.icon + '.png' }));
  images.push({ icon: 'back', color: '#a6bad0', file: 'back.png' }, { icon: 'next', color: '#a6bad0', file: 'next.png' });
  for (const image of images) {
    const art = QWDeckArt.renderFolder({ ...image, label: '' });
    // Black behind the art: the key's rounded corners are cut out of the PNG,
    // and on the page's default white they came out as four bright specks per key.
    await send('Page.navigate', { url: 'data:text/html;charset=utf-8,' + encodeURIComponent('<body style="margin:0;overflow:hidden;background:#000">' + art) });
    await delay(80);
    const shot = await send('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: 144, height: 144, scale: 1 } });
    await fs.writeFile(path.join(deck, image.file), Buffer.from(shot.data, 'base64'));
    console.log(image.file);
  }
} finally {
  if (socket?.readyState === WebSocket.OPEN) { await send('Browser.close').catch(() => {}); socket.close(); }
  else browser.kill();
  await delay(300);
  // This is the unique profile created above, never an existing browser profile.
  const resolved = path.resolve(profile);
  if (path.dirname(resolved) !== path.resolve(os.tmpdir()) || !path.basename(resolved).startsWith('qw-deck-art-')) throw Error('Unexpected temporary profile path');
  await fs.rm(resolved, { recursive: true, force: true, maxRetries: 10, retryDelay: 150 });
}
