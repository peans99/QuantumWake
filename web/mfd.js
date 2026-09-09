'use strict';
const mfdParams = new URLSearchParams(location.search);
const panelId = mfdParams.get('panel') === 'right' ? 'right' : 'left';
const mfdKey = 'qw-mfd-' + panelId;
let preferences = {};
try { preferences = JSON.parse(localStorage.getItem(mfdKey) || '{}') || {}; } catch { }
let page = Number.isInteger(preferences.page) ? QwMfd.clamp(preferences.page, 0, QwMfd.pages.length - 1) : (panelId === 'right' ? 1 : 0);
let brightness = Number.isFinite(preferences.brightness) ? QwMfd.clamp(preferences.brightness, .3, 1) : 1;
let textScale = Number.isFinite(preferences.textScale) ? QwMfd.clamp(preferences.textScale, .8, 1.5) : 1;
let bindings = QwMfd.buttons(null);
let state = null;
let briefing = null, briefingUnavailable = false, briefingBusy = false;
let selected = 0, armed = false;
let lastRows = '';
let stream;
const byId = id => document.getElementById(id);
byId('identity').textContent = panelId.toUpperCase() + ' MFD';
// The frame's own numbering. The four rockers carry no printed button, so they
// are input only: setup names them, and this face has nothing to draw for them.
const edges = { top: [1, 2, 3, 4, 5], right: [6, 7, 8, 9, 10], bottom: [15, 14, 13, 12, 11], left: [20, 19, 18, 17, 16] };
const osbs = Object.values(edges).flat();
for (const [edge, numbers] of Object.entries(edges)) {
  for (const number of numbers) {
    const button = document.createElement('button');
    button.id = 'osb-' + number;
    const index = document.createElement('span');
    index.textContent = String(number).padStart(2, '0');
    button.append(index, document.createElement('b'));
    button.onclick = () => press(number);
    byId(edge).append(button);
  }
}
function drawLabels() {
  for (const number of osbs) {
    const button = byId('osb-' + number);
    const caption = QwMfd.caption(bindings[number]);
    button.querySelector('b').textContent = caption || '—';
    button.disabled = !caption;
    button.title = 'Cougar button ' + number + (caption ? ': ' + caption : ': unassigned');
  }
}
function savePreferences() {
  try { localStorage.setItem(mfdKey, JSON.stringify({ page, brightness, textScale })); } catch { }
}
function render() {
  byId('mfd').style.filter = `brightness(${brightness})`;
  byId('mfd').style.setProperty('--text-scale', textScale);
  byId('title').textContent = QwMfd.pages[page];
  const showing = QwMfd.pageIds[page];
  for (const number of osbs) byId('osb-' + number).classList.toggle('selected', bindings[number] === showing);
  const rows = QwMfd.rows(page, state, briefing, briefingUnavailable, { selected, armed });
  const key = JSON.stringify(rows);
  if (key === lastRows) return;
  lastRows = key;
  const readings = byId('readings');
  const scroll = readings.scrollTop;
  readings.replaceChildren();
  for (const [label, value, at, mark] of rows) {
    const row = document.createElement('article'); row.className = 'reading';
    if (mark) row.classList.add(mark);
    const heading = document.createElement('h2'); heading.textContent = label;
    const text = document.createElement('p'); text.textContent = value || 'Not recorded';
    row.append(heading, text);
    if (at) { const time = document.createElement('time'); time.textContent = new Date(at).toLocaleString(); row.append(time); }
    readings.append(row);
  }
  readings.scrollTop = scroll;
  // Instant, not smooth: a running scroll animation is one of the things that
  // keeps a headless render from ever settling, and this fires on every press.
  readings.querySelector('.cursor, .armed')?.scrollIntoView({ block: 'nearest' });
}
function note(text) { byId('last-input').textContent = text; }
function press(number) {
  note('BTN ' + String(number).padStart(2, '0'));
  const button = byId('osb-' + number);
  if (button) { button.classList.add('pressed'); setTimeout(() => button.classList.remove('pressed'), 180); }
  const action = QwMfd.action(number, bindings);
  if (!action) return;
  // Anything but DONE stands a live confirmation down. A pilot reaching for
  // another page must not leave one armed behind them.
  if (!action.confirm && armed) { armed = false; lastRows = ''; }
  if (action.confirm) { confirmSelected(); return; }
  if (action.page !== undefined || action.cycle) {
    page = action.page ?? (page + action.cycle + QwMfd.pages.length) % QwMfd.pages.length;
    selected = 0; lastRows = ''; byId('readings').scrollTop = 0;
  }
  if (action.text) textScale = QwMfd.clamp(textScale + action.text * .1, .8, 1.5);
  if (action.brightness) brightness = QwMfd.clamp(brightness + action.brightness * .1, .3, 1);
  if (action.scroll) step(action.scroll);
  savePreferences(); render();
}
/* Up and down move the cursor on Act, which has something to point at, and
   scroll the panel everywhere else. One pair of buttons either way: a cockpit
   frame has no spare ones, and a rocker nobody can name yet is not a plan. */
function step(direction) {
  if (QwMfd.pageIds[page] !== 'act') {
    byId('readings').scrollBy({ top: direction * byId('readings').clientHeight * .7, behavior: 'smooth' });
    return;
  }
  const count = QwMfd.tasks(briefing).length;
  if (!count) return;
  selected = (selected + direction + count) % count;
  lastRows = '';
}
/* Press once to arm, again to commit. A single press that ticks work off is a
   glove-width away from a page button, and the plan is the pilot's own record. */
async function confirmSelected() {
  const list = QwMfd.tasks(briefing);
  const target = list[QwMfd.clamp(selected, 0, list.length - 1)];
  if (!target?.tripId) { note('NOTHING TO CONFIRM'); return; }
  if (!armed) { armed = true; lastRows = ''; render(); return; }
  armed = false; lastRows = '';
  render();
  const path = target.kind === 'stop'
    ? `stops/${encodeURIComponent(target.stopId)}/toggle`
    : `stops/${encodeURIComponent(target.stopId)}/actions/${encodeURIComponent(target.actionId)}/toggle`;
  try {
    const response = await fetch(`/api/trips/${encodeURIComponent(target.tripId)}/${path}`,
      { method: 'POST', signal: AbortSignal.timeout(8000) });
    if (!response.ok) throw new Error('Not saved');
    note('MARKED DONE');
    selected = 0;
    await refreshBriefing();
  } catch { note('NOT SAVED · CHECK THE DASHBOARD'); }
}
window.chrome?.webview?.addEventListener('message', ({ data }) => {
  if (data.type === 'button') press(data.button);
  if (data.type === 'buttons') { bindings = QwMfd.buttons(data.buttons); drawLabels(); lastRows = ''; render(); }
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
  drawLabels();
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
