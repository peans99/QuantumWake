'use strict';
const setupElement = id => document.getElementById(id);
const host = window.chrome?.webview;
let monitors = [{ id: 'example', x: 0, y: 0, width: 1920, height: 1080, primary: true }];
let layout = { enabled: false, blackout: true, brightness: 1, textScale: 1, sleepAfterMinutes: 0, buttons: null, panels: [
  { id: 'left', monitor: 'example', x: 360, y: 280, width: 480, height: 480, cougar: 1 },
  { id: 'right', monitor: 'example', x: 960, y: 280, width: 480, height: 480, cougar: 2 }
] };
let selected = 0, transform, dragging, previewing = false;
const desktop = setupElement('desktop');
const selectedPanel = () => layout.panels[selected];
function send(type) { host?.postMessage({ type, layout }); }
function status(text, error = false) { setupElement('status').textContent = text; setupElement('status').classList.toggle('error', error); }
/* Coalesced to one send a frame. A drag raises pointermove far faster than a
   window can be moved, and each message crosses into the host, repositions two
   displays and recuts a full-screen backdrop; sending them all would queue up
   work the pilot has already dragged past. */
let previewQueued = false;
function pushPreview() {
  if (previewQueued || !previewing) return;
  previewQueued = true;
  requestAnimationFrame(() => { previewQueued = false; if (previewing) send('preview'); });
}
function changed() { draw(); pushPreview(); }
function fillEditor() {
  const panel = selectedPanel();
  setupElement('selected-title').textContent = panel.id === 'left' ? 'Left MFD' : 'Right MFD';
  const monitor = setupElement('monitor'); monitor.replaceChildren();
  for (const [i, m] of monitors.entries()) monitor.add(new Option(`Monitor ${i + 1} · ${m.width} × ${m.height}`, m.id));
  if (!monitors.some(m => m.id === panel.monitor)) monitor.add(new Option('Disconnected monitor', panel.monitor));
  monitor.value = panel.monitor;
  setupElement('placement-note').textContent = monitors.some(m => m.id === panel.monitor)
    ? 'This rectangle represents the visible screen opening, including the edge labels.'
    : 'This MFD stays hidden until its monitor returns or you choose another monitor.';
  for (const key of ['x', 'y', 'width', 'height', 'cougar']) setupElement(key).value = panel[key];
  setupElement('enabled').checked = layout.enabled;
  setupElement('blackout').checked = layout.blackout !== false;
  showScreen();
  setupElement('separate').disabled = monitors.length < 2;
}
function draw() {
  if (!monitors.length) return;
  const bounds = QwMfd.extent(monitors), margin = 24;
  const scale = Math.min((desktop.clientWidth - margin * 2) / bounds.width, (desktop.clientHeight - margin * 2) / bounds.height);
  transform = { scale, x: (desktop.clientWidth - bounds.width * scale) / 2 - bounds.x * scale,
    y: (desktop.clientHeight - bounds.height * scale) / 2 - bounds.y * scale };
  desktop.replaceChildren();
  const place = (el, x, y, width, height) => Object.assign(el.style, {
    left: (transform.x + x * scale) + 'px', top: (transform.y + y * scale) + 'px',
    width: width * scale + 'px', height: height * scale + 'px' });
  monitors.forEach((m, i) => {
    // A monitor showing a panel is drawn the way it will look: black, with
    // the openings the only thing on it. The switch below explains itself.
    const dark = layout.blackout !== false && layout.panels.some(p => p.monitor === m.id);
    const node = document.createElement('div');
    // Without the desktop app there is nothing to detect, and the stand-in is
    // drawn as one: an example that looked like a detected monitor read as
    // "Quantum Wake can only see one of my three".
    node.className = ['monitor', dark ? 'blacked' : '', host ? '' : 'example'].filter(Boolean).join(' ');
    const label = document.createElement('span');
    label.textContent = host
      ? `${i + 1} · ${m.width} × ${m.height}${m.primary ? ' · primary' : ''}`
      : `Example only · ${m.width} × ${m.height} · not one of your monitors`;
    node.append(label); place(node, m.x, m.y, m.width, m.height); desktop.append(node);
  });
  layout.panels.forEach((p, i) => {
    const monitor = monitors.find(m => m.id === p.monitor); if (!monitor) return;
    const node = document.createElement('button'); node.className = `mfd-area ${p.id}${selected === i ? ' selected' : ''}`;
    node.dataset.panel = i; node.textContent = `${p.id.toUpperCase()} · ${p.cougar}`;
    node.setAttribute('aria-label', `${p.id} MFD. Drag to move; use the corner to resize.`);
    const resize = document.createElement('span'); resize.className = 'resize'; node.append(resize);
    place(node, monitor.x + p.x, monitor.y + p.y, p.width, p.height); desktop.append(node);
  });
  fillEditor();
}
desktop.addEventListener('pointerdown', event => {
  const node = event.target.closest('[data-panel]'); if (!node || event.button !== 0) return;
  selected = Number(node.dataset.panel);
  const p = selectedPanel(), m = monitors.find(m => m.id === p.monitor);
  dragging = { pointer: event.pointerId, x: event.clientX, y: event.clientY, original: { ...p }, monitor: m,
    resize: event.target.classList.contains('resize') };
  desktop.setPointerCapture(event.pointerId); event.preventDefault(); draw();
});
desktop.addEventListener('pointermove', event => {
  if (!dragging || event.pointerId !== dragging.pointer) return;
  const d = dragging, dx = (event.clientX - d.x) / transform.scale, dy = (event.clientY - d.y) / transform.scale;
  layout.panels[selected] = d.resize
    ? QwMfd.fit({ ...d.original, width: d.original.width + dx, height: d.original.height + dy }, d.monitor)
    : QwMfd.move(layout.panels[selected], d.monitor.x + d.original.x + dx, d.monitor.y + d.original.y + dy, monitors);
  // Under the hand, not on release. Aligning a frame is a matter of a few
  // pixels, and a preview that only catches up when you let go means dropping
  // it, looking up, and starting again.
  draw(); pushPreview();
});
function endDrag(event) {
  if (!dragging || event.pointerId !== dragging.pointer) return;
  dragging = null;
  if (desktop.hasPointerCapture(event.pointerId)) desktop.releasePointerCapture(event.pointerId);
  changed(); desktop.querySelector(`[data-panel="${selected}"]`)?.focus();
}
desktop.addEventListener('pointerup', endDrag);
desktop.addEventListener('pointercancel', endDrag);
desktop.addEventListener('focusin', event => {
  const node = event.target.closest('[data-panel]'); if (!node) return;
  selected = Number(node.dataset.panel);
  desktop.querySelectorAll('[data-panel]').forEach(el => el.classList.toggle('selected', Number(el.dataset.panel) === selected));
  fillEditor();
});
desktop.addEventListener('keydown', event => {
  const node = event.target.closest('[data-panel]'); if (!node) return;
  selected = Number(node.dataset.panel);
  const delta = { ArrowLeft: [-1,0], ArrowRight: [1,0], ArrowUp: [0,-1], ArrowDown: [0,1] }[event.key];
  if (!delta) return;
  event.preventDefault(); const p = selectedPanel(), m = monitors.find(m => m.id === p.monitor), step = event.shiftKey ? 10 : 1;
  layout.panels[selected] = QwMfd.fit({ ...p, x: p.x + delta[0] * step, y: p.y + delta[1] * step }, m);
  changed(); desktop.querySelector(`[data-panel="${selected}"]`)?.focus();
});
setupElement('monitor').onchange = event => {
  layout.panels[selected] = QwMfd.fit({ ...selectedPanel(), monitor: event.target.value }, monitors.find(m => m.id === event.target.value)); changed();
};
for (const key of ['x', 'y', 'width', 'height']) setupElement(key).onchange = event => {
  const p = selectedPanel(); layout.panels[selected] = QwMfd.fit({ ...p, [key]: Number(event.target.value) }, monitors.find(m => m.id === p.monitor)); changed();
};
for (let n = 1; n <= 8; n++) setupElement('cougar').add(new Option('F16 MFD ' + n, n));
setupElement('cougar').onchange = event => { selectedPanel().cougar = Number(event.target.value); changed(); };
setupElement('enabled').onchange = event => { layout.enabled = event.target.checked; };
setupElement('blackout').onchange = event => { layout.blackout = event.target.checked; changed(); };
/* Percentages on the text slider, fractions in the file: a pilot reads 70%, and
   the display multiplies by .7. Brightness is four numbered levels instead,
   because a button on the frame has to be able to land on every one of them. */
function showScreen() {
  const level = QwMfd.brightnessLevel(layout.brightness ?? 1), text = Math.round((layout.textScale ?? 1) * 100);
  setupElement('brightness').value = level;
  setupElement('brightness-value').textContent =
    level + ' · ' + Math.round(QwMfd.brightnessAt(level) * 100) + '%';
  setupElement('text-scale').value = text;
  setupElement('sleep-after').value = String(layout.sleepAfterMinutes || 0);
  setupElement('text-scale-value').textContent = text + '%';
}
setupElement('sleep-after').onchange = event => {
  layout.sleepAfterMinutes = Number(event.target.value) || 0;
  if (previewing) send('preview');
  status(layout.sleepAfterMinutes
    ? 'Frames dim after ' + layout.sleepAfterMinutes + ' idle minutes. Save to keep it.'
    : 'Frames stay at full brightness. Save to keep it.');
};
setupElement('text-scale').oninput = event => {
  layout.textScale = Number(event.target.value) / 100;
  showScreen();
  // Under the hand, like a drag: the preview is what you are judging it by.
  pushPreview();
};
setupElement('brightness').oninput = event => {
  layout.brightness = QwMfd.brightnessAt(Number(event.target.value));
  showScreen();
  pushPreview();
};
setupElement('swap').onclick = () => { [layout.panels[0].cougar, layout.panels[1].cougar] = [layout.panels[1].cougar, layout.panels[0].cougar]; changed(); };
setupElement('together').onclick = () => {
  const m = monitors.find(m => m.id === selectedPanel().monitor) || monitors[0];
  const size = Math.min(480, Math.floor(m.width / 2), m.height);
  layout.panels = layout.panels.map((p, i) => QwMfd.fit({ ...p, monitor: m.id, x: i * size, y: 0, width: size, height: size }, m)); changed();
};
setupElement('separate').onclick = () => {
  if (monitors.length < 2) return;
  const first = monitors.find(m => m.id === selectedPanel().monitor) || monitors[0];
  const pair = [first, monitors.find(m => m.id !== first.id)];
  layout.panels = layout.panels.map((p, i) => QwMfd.fit({ ...p, monitor: pair[i].id, x: 0, y: 0 }, pair[i])); changed();
};
/* Rebuilt only when the whole profile changes, never on a single dropdown:
   redrawing the grid under the pilot's hand would take the focus off the row
   they were part-way through setting. */
/* The frame, not a list of dropdowns.
 *
 * Twenty-eight selects of thirty-six options each is a thousand entries in
 * front of somebody whose actual question is "what does THIS button do" - and
 * they are looking at the button while they ask it. So the editor is shaped
 * like the thing in their hands: press a button, its position lights up, and
 * one grouped list assigns it.
 *
 * The rockers get a row of their own below the frame, because which rocker
 * reports which number is the one thing no datasheet answers - the tester is
 * how you find out, and the row is where the answer lands.
 */
const edges = { top: [1, 2, 3, 4, 5], right: [6, 7, 8, 9, 10], bottom: [15, 14, 13, 12, 11], left: [20, 19, 18, 17, 16] };
const rockers = Array.from({ length: QwMfd.BUTTONS - QwMfd.OSBS }, (_, i) => QwMfd.OSBS + 1 + i);
let picked = 1;

/* Four kinds of thing were sharing one flat list: screens to open, menus to
   step into, the five positions that follow the screen, and plain controls. */
function optionGroups() {
  const menus = QwMfd.groups.map(g => g.id);
  const slots = QwMfd.commands.filter(c => /^menu-[1-5]$/.test(c.id)).map(c => c.id);
  const screens = QwMfd.pageIds.slice();
  const rest = QwMfd.commands.map(c => c.id)
    .filter(id => !menus.includes(id) && !slots.includes(id) && !screens.includes(id));
  return [
    ['Menu positions · what the screen offers', slots],
    ['Open a screen directly', screens],
    ['Open a menu', menus.concat(['home', 'back'])],
    ['Controls', rest.filter(id => id !== 'home' && id !== 'back')]
  ];
}

function assign(number, id) {
  const next = QwMfd.buttons(layout.buttons);
  if (id) next[number] = id; else delete next[number];
  layout.buttons = next;
  drawButtons();
  if (previewing) send('preview');
  status('Button ' + String(number).padStart(2, '0') + ': '
    + (id ? labelOf(id) : 'unassigned') + '. Save to keep it.');
}

/* A saved file is allowed to name a command this build has never heard of —
   that is how a profile survives going back a version — so nothing here may
   assume the lookup succeeds. Showing the raw name is the honest answer: it
   is what the frame will act on, and it is not this page's to quietly drop. */
const labelOf = id => QwMfd.commands.find(c => c.id === id)?.label || id;

function keycap(number, map) {
  const cell = document.createElement('button');
  cell.type = 'button';
  cell.id = 'bind-' + number;
  cell.className = 'key' + (number === picked ? ' picked' : '') + (map[number] ? '' : ' empty');
  cell.onclick = () => { picked = number; drawButtons(); };
  const n = document.createElement('small'); n.textContent = String(number).padStart(2, '0');
  const what = document.createElement('span');
  // A caption this build has no word for still has a binding, so the cap
  // must not read as blank - blank here means nothing is bound.
  what.textContent = QwMfd.caption(map[number], true) || (map[number] ? '?' : '—');
  cell.append(n, what);
  cell.title = 'Cougar button ' + number
    + (map[number] ? ': ' + labelOf(map[number]) : ': unassigned');
  return cell;
}

function drawButtons() {
  const map = QwMfd.buttons(layout.buttons);
  const panel = setupElement('buttons');
  panel.replaceChildren();

  const frame = document.createElement('div'); frame.className = 'cougar';
  for (const [side, numbers] of Object.entries(edges)) {
    const row = document.createElement('div'); row.className = 'side ' + side;
    for (const number of numbers) row.append(keycap(number, map));
    frame.append(row);
  }
  const face = document.createElement('div'); face.className = 'face';
  face.textContent = 'MFD';
  frame.append(face);

  const rockerTitle = document.createElement('div'); rockerTitle.className = 'group';
  rockerTitle.textContent = 'ROCKERS ' + rockers[0] + '–' + rockers[rockers.length - 1];
  const rockerHint = document.createElement('p'); rockerHint.className = 'hint muted';
  rockerHint.textContent = 'Which rocker reports which number is not something a datasheet answers. '
    + 'Press one and its number lights up here.';
  const rockerRow = document.createElement('div'); rockerRow.className = 'rockers';
  for (const number of rockers) rockerRow.append(keycap(number, map));

  const editor = document.createElement('div'); editor.className = 'assign';
  const label = document.createElement('label');
  label.append('Button ' + String(picked).padStart(2, '0') + ' does');
  const select = document.createElement('select'); select.className = 'select';
  select.add(new Option('Nothing', ''));
  for (const [title, ids] of optionGroups()) {
    const set = document.createElement('optgroup'); set.label = title;
    for (const id of ids) {
      const command = QwMfd.commands.find(c => c.id === id);
      if (command) set.append(new Option(command.label, command.id));
    }
    select.append(set);
  }
  // An unknown binding needs an entry of its own, or selecting it back would be
  // impossible and the blank would read as 'unassigned' when it is not.
  const current = map[picked] || '';
  if (current && !Array.from(select.options).some(o => o.value === current)) {
    select.append(new Option(current + ' (from a newer version)', current));
  }
  select.value = current;
  select.onchange = () => assign(picked, select.value);
  label.append(select);
  editor.append(label);

  panel.append(frame, editor, rockerTitle, rockerHint, rockerRow);
}
setupElement('restore').onclick = () => {
  layout.buttons = null;
  drawButtons();
  if (previewing) send('preview');
  status('Shipped button profile restored. Save to keep it.');
};
setupElement('preview').onclick = () => { previewing = true; send('preview'); };
setupElement('stop-preview').onclick = () => { previewing = false; send('stopPreview'); status('Preview stopped. Saved placement restored.'); };
/* Saving keeps the preview running. Turning it off here was the bug that made
   the whole editor feel dead: after one save nothing else reached the displays,
   so every later change needed another save to be seen at all. Saving writes
   the file; it is not a reason to stop looking at the thing. */
setupElement('save').onclick = () => send('save');
function showDevices(devices) {
  setupElement('devices').textContent = devices.length ? devices.map(n => 'F16 MFD ' + n).join(' · ') : 'No default Cougar devices detected. Check the driver and USB connection.';
  if (new Set(devices).size !== devices.length) setupElement('devices').textContent += ' · Duplicate numbers: input is paused for those devices.';
}
host?.addEventListener('message', ({ data }) => {
  if (data.type === 'setup') {
    monitors = data.monitors; layout = data.layout; previewing = false;
    showDevices(data.devices); drawButtons(); draw();
  }
  if (data.type === 'monitors') {
    monitors = data.monitors;
    layout.panels = layout.panels.map(p => QwMfd.fit(p, monitors.find(m => m.id === p.monitor)));
    draw(); status('Monitor layout changed. Check placement before saving.');
  }
  if (data.type === 'devices') showDevices(data.devices);
  if (data.type === 'button') {
    setupElement('button-test').textContent = `F16 MFD ${data.cougar} → BUTTON ${String(data.button).padStart(2, '0')}`;
    // The only way to find out which rocker is which: press it and watch its
    // row light up, then bind the number that answered.
    if (data.button >= 1 && data.button <= QwMfd.BUTTONS) { picked = data.button; drawButtons(); }
    for (const lit of document.querySelectorAll('.key.hit')) lit.classList.remove('hit');
    setupElement('bind-' + data.button)?.classList.add('hit');
  }
  if (data.type === 'result') status(data.message, !data.ok);
});
if (!host) {
  setupElement('host-note').hidden = false;
  for (const id of ['save', 'preview', 'stop-preview', 'enabled', 'blackout', 'sleep-after']) setupElement(id).disabled = true;
  setupElement('devices').textContent = 'USB input is available in the desktop app.';
  // "Your monitors" over a single invented rectangle reads as a detection that
  // found one monitor, which is the wrong thing to be told when you have three.
  setupElement('monitors-title').replaceChildren('Example layout');
  const aside = document.createElement('span');
  aside.textContent = 'Your own monitors are only detected in the desktop app';
  setupElement('monitors-title').append(aside);
}
new ResizeObserver(draw).observe(desktop);
drawButtons();
draw();
