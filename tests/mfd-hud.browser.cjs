// Run with Node and installed Chrome. Only fixture data is served; POSTs are intercepted.
// node tests/mfd-hud.browser.cjs <artifact-directory>
const fs = require('node:fs'), path = require('node:path'), http = require('node:http');
const { spawn } = require('node:child_process'), assert = require('node:assert/strict');
const output = path.resolve(process.argv[2] || 'artifacts/hud-menu');
fs.mkdirSync(output, { recursive: true });
const pause = ms => new Promise(r => setTimeout(r, ms));
const state = { connected: true, inGame: true, location: 'Baijini Point', locationBody: 'ArcCorp',
  locationSystem: 'Stanton', ship: 'Drake Cutlass Black', handle: 'Fixture pilot', sessionStarted: new Date().toISOString(),
  recentEvents: [{ kind: 'arrived', text: 'Arrived at Baijini Point', at: new Date().toISOString() }] };
const briefing = { tripId: 'fixture-trip', tripTitle: 'Medical supply run', stops: [{ id: 'stop-a', placeId: 'everus',
  place: 'Everus Harbor', actions: [
    { id: 'action-a', kind: 'load', quantity: 32, unit: 'SCU', text: 'Medical supplies', done: false },
    { id: 'action-b', kind: 'unload', quantity: 32, unit: 'SCU', text: 'Deliver supplies', done: false }] }] };
const atlas = { nodes: [{ rawId: 'everus', body: 'Hurston' }], positions: { stanton: {
  ArcCorp: { x: 2e10, y: 1e10 }, Hurston: { x: -1e10, y: 1e10 }, Crusader: { x: 0, y: -1e10 }, microTech: { x: 3e10, y: -3e10 } } } };
const server = http.createServer((req, res) => {
  const p = new URL(req.url, 'http://localhost').pathname;
  if (req.method !== 'GET') { res.writeHead(405); res.end(); return; }
  if (p.startsWith('/api/')) {
    res.setHeader('Content-Type', 'application/json');
    res.end(JSON.stringify(p === '/api/now' ? state : p === '/api/briefing' ? briefing : p === '/api/map' ? atlas
      : ['/api/jobs', '/api/ledger'].includes(p) ? [] : p === '/api/manufacturers' ? {} : { known: false })); return;
  }
  if (!/^\/[a-zA-Z0-9/._-]+$/.test(p) || p.includes('..')) { res.writeHead(404); res.end(); return; }
  const file = path.join(__dirname, '../web', p);
  if (!fs.existsSync(file)) { res.writeHead(404); res.end(); return; }
  res.setHeader('Content-Type', p.endsWith('.html') ? 'text/html; charset=utf-8' : p.endsWith('.js') ? 'text/javascript'
    : p.endsWith('.css') ? 'text/css' : p.endsWith('.svg') ? 'image/svg+xml' : p.endsWith('.png') ? 'image/png' : 'text/plain');
  res.end(fs.readFileSync(file));
});
let chrome, ws;
(async () => {
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const profile = fs.mkdtempSync(path.join(output, 'chrome-'));
  chrome = spawn(process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe', [
    '--headless=new', '--disable-gpu', '--no-first-run', '--remote-debugging-port=0', '--user-data-dir=' + profile, 'about:blank'
  ], { windowsHide: true, stdio: 'ignore' });
  chrome.on('error', e => { throw e; });
  let port;
  for (let n = 0; n < 80; n++) {
    try { port = fs.readFileSync(path.join(profile, 'DevToolsActivePort'), 'utf8').split('\n')[0]; break; } catch { await pause(250); }
  }
  assert.ok(port, 'Chrome started');
  const tabs = await (await fetch('http://127.0.0.1:' + port + '/json')).json();
  ws = new WebSocket(tabs.find(t => t.type === 'page').webSocketDebuggerUrl);
  await new Promise(r => ws.addEventListener('open', r, { once: true }));
  let serial = 0; const pending = new Map(), errors = [];
  ws.addEventListener('message', e => {
    const d = JSON.parse(e.data);
    if (d.method === 'Runtime.exceptionThrown') errors.push(d.params.exceptionDetails);
    if (d.id) { const p = pending.get(d.id); pending.delete(d.id); d.error ? p.reject(d.error) : p.resolve(d.result); }
  });
  const call = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++serial; pending.set(id, { resolve, reject }); ws.send(JSON.stringify({ id, method, params }));
  });
  const run = async expression => {
    const r = await call('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (r.exceptionDetails) throw Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text);
    return r.result.value;
  };
  await call('Page.enable'); await call('Runtime.enable');
  await call('Page.addScriptToEvaluateOnNewDocument', { source: `
    window.__posts=[];window.__delay=0;const realFetch=window.fetch;
    window.fetch=(url,options)=>options?.method==='POST' ? (window.__posts.push(url),new Promise(r=>setTimeout(()=>r({ok:true}),window.__delay))) : realFetch(url,options);
    window.chrome ||= {};window.chrome.webview={addEventListener:(_,fn)=>window.__native=fn};` });
  const size = (width, height = width) => call('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
  const shot = async name => {
    await pause(220); const r = await call('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
    fs.writeFileSync(path.join(output, name + '.png'), Buffer.from(r.data, 'base64'));
  };
  const checkTiles = async () => {
    const spilling = await run(`Array.from(document.querySelectorAll('.menu-tile')).flatMap(tile=>
      Array.from(tile.children).filter(c=>getComputedStyle(c).display!=='none' && c.getBoundingClientRect().bottom>tile.getBoundingClientRect().bottom-2)
        .map(c=>tile.dataset.screen+': '+c.textContent))`);
    // Named, not counted: "[Array]" tells you a tile overflowed and nothing
    // about which one, which is a whole run of guessing away from the answer.
    const room = await run(`(()=>{const t=document.querySelector('.menu-tile');const a=document.getElementById('action');
      return t ? {tile:+t.getBoundingClientRect().height.toFixed(1), strip: a && !a.hidden} : null;})()`);
    assert.deepEqual(spilling, [], 'Menu contents fit vertically; spilling: ' + JSON.stringify(spilling)
      + ' with ' + JSON.stringify(room));
  };
  await size(480);
  await call('Page.navigate', { url: `http://127.0.0.1:${server.address().port}/mfd.html?snapshot=1` });
  await pause(650);
  // Each frame opens where it is useful rather than at the top menu - the left
  // panel on Nav, the right on Task - since 0.10.23. The walk below starts from
  // the menu, so it asks for the menu instead of assuming it is already there.
  assert.equal(await run('screen'), 'nav', 'the left panel opens on Nav');
  await run("navigate('home')");
  assert.equal(await run('screen'), 'home');
  await shot('home-480');
  await run("__native({data:{type:'button',button:1}})");
  assert.equal(await run('screen'), 'flight'); await shot('flight-480');
  await run('press(1)'); assert.equal(await run('screen'), 'nav'); await shot('nav-480');
  await run('press(8);press(8)'); assert.deepEqual(await run('__posts'), []);
  await run('press(15)'); assert.equal(await run('screen'), 'flight');
  await run('press(2)'); assert.equal(await run('screen'), 'map'); await shot('map-480');
  await run('press(15)'); assert.equal(await run('screen'), 'flight');
  await run('press(15)'); assert.equal(await run('screen'), 'home');
  // A button bound to nothing leaves you where you are. 6 rather than 5: 5 is
  // Nav from the top menu now, so pressing it proved the opposite of the point.
  await run('press(6)'); assert.equal(await run('screen'), 'home');
  await run('press(2);press(1);press(2);press(8)'); assert.equal(await run('armed'), true); await shot('checklist-armed-480');
  await run('press(15)'); assert.equal(await run('armed'), false); assert.equal(await run('screen'), 'operations');
  await run('press(2);press(8);briefing.stops[0].actions.shift();render();press(8)');
  assert.deepEqual(await run('__posts'), []); assert.equal(await run('armed'), false);
  await run('refreshBriefing()'); await run('__delay=250;press(8);press(8);press(8)');
  await pause(350); assert.equal(await run('__posts.length'), 1);
  await run("navigate('cargo')"); assert.equal(await run('hasDetails'), true);
  assert.match(await run("byId('readings').textContent"), /Plan \+ counters · not a hold/);
  assert.equal(await run("byId('readings').querySelector('.status p').textContent"), 'Plan + counters · not a hold');
  assert.equal(await run("byId('readings').querySelector('.status h2').getBoundingClientRect().width <= 1"), true);
  await shot('cargo-compact-copy-480');
  await run('press(10)'); assert.equal(await run('details'), true);
  assert.match(await run("byId('readings').textContent"), /Plan \+ counters · not a hold/);
  await run('press(15)'); assert.equal(await run('details'), false); assert.equal(await run('screen'), 'cargo');
  await run('press(15)'); assert.equal(await run('screen'), 'resources');
  // A custom profile remains the whole answer, including cleared keys and rocker shortcuts.
  await run("__native({data:{type:'display',buttons:{1:'crew',21:'home',22:'menu-2'}}});press(1)");
  assert.equal(await run('screen'), 'crew');
  await run('press(13)'); assert.equal(await run('screen'), 'crew');
  await run('press(21);press(22)'); assert.equal(await run('screen'), 'operations');
  await run("__native({data:{type:'display',buttons:null}})");
  await run("navigate('home');document.querySelector('[data-screen=flight]').click()");
  assert.equal(await run('screen'), 'flight');
  await run("document.querySelector('[data-screen=nav]').click()");
  assert.equal(await run('screen'), 'nav');
  const pages = await run('QwMfd.pageIds');
  const menus = await run('Object.keys(QwMfd.menuNodes)');
  const layout = [];
  for (const width of [220, 480]) {
    await size(width); await pause(60);
    for (const id of ['home', ...menus, ...pages]) {
      await run(`navigate('${id}')`);
      const clipped = await run(`Array.from(document.querySelectorAll('.edge button:not(:disabled)')).filter(b=>b.scrollWidth>b.clientWidth+1).map(b=>b.textContent)`);
      assert.deepEqual(clipped, [], `${width} ${id}: captions fit`);
      assert.equal(await run("document.documentElement.scrollWidth <= innerWidth && document.documentElement.scrollHeight <= innerHeight"), true);
      if (await run('isMenu()')) await checkTiles();
      layout.push({ width, id, title: await run("byId('title').textContent") });
      if (width === 220 && ['home', 'flight', 'flight-route', 'nav', 'map', 'act'].includes(id)) await shot(id + '-220');
    }
  }
  await size(480); await run("__native({data:{type:'display',buttons:null,textScale:1.5}});navigate('home')"); await checkTiles(); await shot('home-large-text');
  await run("navigate('nav')"); await shot('nav-large-text');
  await size(720, 480); await run("__native({data:{type:'display',buttons:null,textScale:1}});navigate('flight')"); await checkTiles(); await shot('flight-wide');
  await run("navigate('resources')");
  await call('Page.reload'); await pause(450); assert.equal(await run('screen'), 'resources');
  await call('Page.navigate', { url: `http://127.0.0.1:${server.address().port}/mfd.html?snapshot=1&panel=right` });
  // Its own opening screen, and its own saved one: the left panel was left on
  // resources a moment ago, and the right knows nothing about that.
  await pause(450); assert.equal(await run('screen'), 'task', 'Each panel has independent navigation');
  await size(1100, 850);
  await call('Page.navigate', { url: `http://127.0.0.1:${server.address().port}/mfd-setup.html` });
  await pause(400); await shot('setup-1100');
  assert.deepEqual(errors, [], 'No runtime exceptions');
  fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ views: layout, errors }, null, 2));
  console.log(`PASS: ${layout.length} screen/size combinations; recursive menu, native input, custom mappings, details, and confirmation guards. Screenshots: ${output}`);
})().catch(e => { console.error(e); process.exitCode = 1; }).finally(() => {
  ws?.close(); chrome?.kill(); server.closeAllConnections(); server.close();
});
