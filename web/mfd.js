'use strict';
const mfdParams = new URLSearchParams(location.search);
const panelId = mfdParams.get('panel') === 'right' ? 'right' : 'left';
const mfdKey = 'qw-mfd-' + panelId;
let preferences = {};
try { preferences = JSON.parse(localStorage.getItem(mfdKey) || '{}') || {}; } catch { }
let page = Number.isInteger(preferences.page) ? QwMfd.clamp(preferences.page, 0, QwMfd.pages.length - 1) : (panelId === 'right' ? 1 : 0);
let brightness = Number.isFinite(preferences.brightness) ? QwMfd.clamp(preferences.brightness, .3, 1) : 1;
let textScale = Number.isFinite(preferences.textScale) ? QwMfd.clamp(preferences.textScale, .8, 1.5) : 1;
let state = null;
let briefing = null, briefingUnavailable = false, briefingBusy = false;
let lastRows = '';
let stream;
const byId = id => document.getElementById(id);
byId('identity').textContent = panelId.toUpperCase() + ' MFD';
const labels = { 1: 'NAV', 2: 'TASK', 3: 'STATUS', 6: 'TEXT +', 7: 'TEXT −',
  11: 'NEXT', 12: 'DOWN', 13: 'HOME', 14: 'UP', 15: 'PREV', 19: 'DIM', 20: 'BRIGHT' };
for (const [edge, numbers] of Object.entries({ top: [1,2,3,4,5], right: [6,7,8,9,10], bottom: [15,14,13,12,11], left: [20,19,18,17,16] })) {
  for (const number of numbers) {
    const button = document.createElement('button');
    button.id = 'osb-' + number;
    const caption = document.createElement('span');
    caption.textContent = String(number).padStart(2, '0');
    button.append(caption, labels[number] || '—');
    button.disabled = !labels[number];
    button.title = 'Cougar button ' + number + (labels[number] ? ': ' + labels[number] : ': unassigned');
    button.onclick = () => press(number);
    byId(edge).append(button);
  }
}
function savePreferences() {
  try { localStorage.setItem(mfdKey, JSON.stringify({ page, brightness, textScale })); } catch { }
}
function render() {
  byId('mfd').style.filter = `brightness(${brightness})`;
  byId('mfd').style.setProperty('--text-scale', textScale);
  byId('title').textContent = QwMfd.pages[page];
  for (let n = 1; n <= QwMfd.pages.length; n++) byId('osb-' + n).classList.toggle('selected', page === n - 1);
  const rows = QwMfd.rows(page, state, briefing, briefingUnavailable);
  const key = JSON.stringify(rows);
  if (key === lastRows) return;
  lastRows = key;
  const readings = byId('readings');
  const scroll = readings.scrollTop;
  readings.replaceChildren();
  for (const [label, value, at] of rows) {
    const row = document.createElement('article'); row.className = 'reading';
    const heading = document.createElement('h2'); heading.textContent = label;
    const text = document.createElement('p'); text.textContent = value || 'Not recorded';
    row.append(heading, text);
    if (at) { const time = document.createElement('time'); time.textContent = new Date(at).toLocaleString(); row.append(time); }
    readings.append(row);
  }
  readings.scrollTop = scroll;
}
function press(number) {
  byId('last-input').textContent = 'BTN ' + number;
  const button = byId('osb-' + number);
  if (button) { button.classList.add('pressed'); setTimeout(() => button.classList.remove('pressed'), 180); }
  const action = QwMfd.action(number);
  if (!action) return;
  if (action.page !== undefined || action.cycle) {
    page = action.page ?? (page + action.cycle + QwMfd.pages.length) % QwMfd.pages.length;
    lastRows = ''; byId('readings').scrollTop = 0;
  }
  if (action.text) textScale = QwMfd.clamp(textScale + action.text * .1, .8, 1.5);
  if (action.brightness) brightness = QwMfd.clamp(brightness + action.brightness * .1, .3, 1);
  if (action.scroll) byId('readings').scrollBy({ top: action.scroll * byId('readings').clientHeight * .7, behavior: 'smooth' });
  savePreferences(); render();
}
window.chrome?.webview?.addEventListener('message', ({ data }) => {
  if (data.type === 'button') press(data.button);
  if (data.type === 'device') byId('device').textContent = data.connected
    ? 'F16 MFD ' + data.cougar + ' · USB' : 'F16 MFD ' + data.cougar + ' · not available';
  if (data.type === 'alignment') {
    byId('alignment').hidden = !data.enabled;
    byId('alignment-name').textContent = panelId.toUpperCase() + ' MFD';
  }
});
async function refreshBriefing() {
  if (briefingBusy) return;
  briefingBusy = true;
  try {
    const response = await fetch('/api/briefing', { signal: AbortSignal.timeout(8000) });
    if (!response.ok) throw new Error('Plan unavailable');
    briefing = await response.json(); briefingUnavailable = false;
  } catch { briefingUnavailable = true; }
  finally { briefingBusy = false; render(); }
}
async function bootMfd() {
  render();
  refreshBriefing();
  if (mfdParams.has('snapshot')) {
    try {
      const response = await fetch('/api/now');
      if (!response.ok) throw new Error('No snapshot');
      state = await response.json();
      byId('connection').textContent = 'SNAPSHOT'; render();
    } catch { byId('connection').textContent = 'SERVER UNAVAILABLE'; }
    return;
  }
  stream = new EventSource('/api/stream');
  const briefingTimer = setInterval(refreshBriefing, 5000);
  stream.onmessage = event => {
    try {
      state = JSON.parse(event.data);
      byId('connection').textContent = state.connected ? (state.inGame ? 'LIVE LOG' : 'GAME MENUS') : 'WAITING FOR GAME';
      render();
    } catch { byId('connection').textContent = 'INVALID UPDATE'; }
  };
  stream.onerror = () => { byId('connection').textContent = state ? 'DISCONNECTED · LAST DATA' : 'SERVER UNAVAILABLE'; };
  window.addEventListener('beforeunload', () => { stream.close(); clearInterval(briefingTimer); });
}
bootMfd();
