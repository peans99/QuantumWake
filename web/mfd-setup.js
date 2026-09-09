'use strict';
const setupElement = id => document.getElementById(id);
const host = window.chrome?.webview;
let monitors = [{ id: 'example', x: 0, y: 0, width: 1920, height: 1080, primary: true }];
let layout = { enabled: false, blackout: true, buttons: null, panels: [
  { id: 'left', monitor: 'example', x: 360, y: 280, width: 480, height: 480, cougar: 1 },
  { id: 'right', monitor: 'example', x: 960, y: 280, width: 480, height: 480, cougar: 2 }
] };
let selected = 0, transform, dragging, previewing = false;
const desktop = setupElement('desktop');
const selectedPanel = () => layout.panels[selected];
function send(type) { host?.postMessage({ type, layout }); }
function status(text, error = false) { setupElement('status').textContent = text; setupElement('status').classList.toggle('error', error); }
function changed() { draw(); if (previewing) send('preview'); }
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
  draw();
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
const groups = [
  { title: 'OPTICAL BUTTONS 1–' + QwMfd.OSBS, from: 1, to: QwMfd.OSBS,
    hint: 'The buttons around the screen, numbered clockwise from the top left.' },
  { title: 'ROCKERS ' + (QwMfd.OSBS + 1) + '–' + QwMfd.BUTTONS, from: QwMfd.OSBS + 1, to: QwMfd.BUTTONS,
    hint: 'The four two-way rockers. Press one to find out which number it is.' }
];
function drawButtons() {
  const map = QwMfd.buttons(layout.buttons);
  const panel = setupElement('buttons');
  panel.replaceChildren();
  for (const group of groups) {
    const title = document.createElement('div'); title.className = 'group'; title.textContent = group.title;
    const hint = document.createElement('p'); hint.className = 'hint muted'; hint.textContent = group.hint;
    const grid = document.createElement('div'); grid.className = 'bindings';
    for (let number = group.from; number <= group.to; number++) {
      const row = document.createElement('label');
      row.id = 'bind-' + number;
      row.append(String(number).padStart(2, '0'));
      const select = document.createElement('select');
      select.className = 'select';
      select.add(new Option('Unassigned', ''));
      for (const command of QwMfd.commands) select.add(new Option(command.label, command.id));
      select.value = map[number] || '';
      select.setAttribute('aria-label', 'Cougar button ' + number);
      select.onchange = () => {
        const next = QwMfd.buttons(layout.buttons);
        if (select.value) next[number] = select.value; else delete next[number];
        layout.buttons = next;
        if (previewing) send('preview');
        status('Button ' + number + ': ' + (select.selectedOptions[0].text) + '. Save to keep it.');
      };
      row.append(select);
      grid.append(row);
    }
    panel.append(title, hint, grid);
  }
}
setupElement('restore').onclick = () => {
  layout.buttons = null;
  drawButtons();
  if (previewing) send('preview');
  status('Shipped button profile restored. Save to keep it.');
};
setupElement('preview').onclick = () => { previewing = true; send('preview'); };
setupElement('stop-preview').onclick = () => { previewing = false; send('stopPreview'); status('Preview stopped. Saved placement restored.'); };
setupElement('save').onclick = () => { previewing = false; send('save'); };
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
    for (const lit of document.querySelectorAll('.bindings label.hit')) lit.classList.remove('hit');
    setupElement('bind-' + data.button)?.classList.add('hit');
  }
  if (data.type === 'result') status(data.message, !data.ok);
});
if (!host) {
  setupElement('host-note').hidden = false;
  for (const id of ['save', 'preview', 'stop-preview', 'enabled', 'blackout']) setupElement(id).disabled = true;
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
