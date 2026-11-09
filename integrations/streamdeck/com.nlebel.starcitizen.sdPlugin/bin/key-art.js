/* Shared by the builder and the plugin: the preview must use the same label
   wrapping and symbols as the physical buttons. No game or device access. */
(() => {
  const icons = {
    command: '<rect x="4" y="4" width="6" height="6" rx="1"/><rect x="14" y="4" width="6" height="6" rx="1"/><rect x="4" y="14" width="6" height="6" rx="1"/><rect x="14" y="14" width="6" height="6" rx="1"/>',
    flight: '<path d="m12 2 8 18-8-4-8 4Z"/><path d="M12 16v6M8 10h8"/>',
    engine: '<path d="M7 3h10v10l-3 3h-4l-3-3ZM7 8H3v7h4m10-7h4v7h-4M10 19l2 3 2-3M9 6h6"/>',
    navigation: '<circle cx="12" cy="12" r="10"/><path d="m15 6-1 8-8 4 4-8Z"/>',
    combat: '<circle cx="12" cy="12" r="7"/><path d="M12 1v6m0 10v6M1 12h6m10 0h6m-11-3v6m-3-3h6"/>',
    power: '<path d="m13 2-8 12h6l-1 8 9-12h-7Z"/>',
    mining: '<path d="m5 20 12-12M3 6q9-6 18 3l-6-1-4 5-2-6Z"/>',
    salvage: '<path d="M8 3 3 8l4 4m9-9 5 5-4 4M7 12l-2 6 7 4 7-4-2-6M9 8h6v7H9Z"/>',
    ground: '<path d="M3 15V9h4l3-5h7l4 7v4ZM7 9h11M12 4v5"/><circle cx="7" cy="17" r="3"/><circle cx="18" cy="17" r="3"/>',
    utilities: '<path d="M14 4a6 6 0 0 0-7 7l-5 5 4 4 5-5a6 6 0 0 0 7-7l-4 4-4-4Z"/>',
    extended: '<rect x="3" y="3" width="18" height="18" rx="3"/><path d="M12 7v10M7 12h10"/>',
    gear: '<path d="M12 3v9m-7-4 7 4 7-4M5 8v9m14-9v9"/><circle cx="5" cy="19" r="2"/><circle cx="19" cy="19" r="2"/><circle cx="12" cy="15" r="2"/>',
    radio: '<path d="M10 16a5 5 0 0 1 0-10m4 10a5 5 0 0 0 0-10M6 20a10 10 0 0 1 0-18m12 18a10 10 0 0 0 0-18M12 12v10m-4 0h8"/><circle cx="12" cy="11" r="1"/>',
    shield: '<path d="m12 2 9 4v6c0 5-9 10-9 10S3 17 3 12V6Z"/><path d="M12 7v10m-4-5h8"/>',
    target: '<circle cx="12" cy="12" r="6"/><path d="M12 1v5m0 12v5M1 12h5m12 0h5"/><circle cx="12" cy="12" r="1"/>',
    weapons: '<path d="m4 20 5-10 5 2-4 8Zm5-10 3-6 8 4-3 6Zm4-8 9 4m-5 8 4-8M3 20h9"/>',
    missile: '<path d="M8 16C8 7 15 3 21 3c0 6-4 13-13 13Zm0 0-5 1 2-5m3 4-1 5 5-2M4 20l-2 2"/><circle cx="16" cy="8" r="2"/>',
    countermeasures: '<path d="m12 3 2 5-2 5-2-5Zm-7 8 2 4-2 4-2-4Zm14 0 2 4-2 4-2-4ZM12 16v6M1 6l4 2m18-2-4 2"/>',
    quantum: '<path d="m3 12 9-10 9 10-9 10Z"/><path d="m8 12 4-5 4 5-4 5ZM1 12h6m10 0h6"/>',
    scan: '<path d="M3 12a9 9 0 0 1 18 0M6 12a6 6 0 0 1 12 0M12 12l6-8M4 19h16"/><circle cx="12" cy="12" r="2"/>',
    lights: '<path d="M4 7q8 0 8 5t-8 5ZM15 5l6-2m-6 6 7-1m-7 5h7m-7 4 6 2"/>',
    door: '<path d="M4 22V3h16v19M9 22V7l11-4M13 14v3M2 22h20"/>',
    lock: '<rect x="4" y="10" width="16" height="12" rx="2"/><path d="M8 10V6a4 4 0 0 1 8 0v4m-4 5v3"/>',
    seat: '<path d="m8 2 2 10h9l2 7H7L4 5M7 19l-2 3m13-3 2 3"/>',
    camera: '<path d="M3 7h5l2-3h5l2 3h4v14H3Z"/><circle cx="12" cy="13" r="4"/>',
    inventory: '<path d="M4 7h16v14H4ZM8 7V3h8v4m-8 5h8m-4 0v5"/>',
    brake: '<circle cx="12" cy="12" r="7"/><path d="M3 4a12 12 0 0 0 0 16M21 4a12 12 0 0 1 0 16M10 8v8m4-8v8"/>',
    cruise: '<path d="M3 18a10 10 0 1 1 18 0M12 14l6-6M5 12h2m10 5 2 1M12 4v2"/><circle cx="12" cy="14" r="2"/>',
    warning: '<path d="m12 2 10 19H2Z"/><path d="M12 8v6m0 3v1"/>',
    back: '<path d="m10 4-8 8 8 8M3 12h19"/>',
    next: '<path d="m5 4 8 8-8 8m9-16 8 8-8 8"/>'
  };
  const esc = value => String(value ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  const colorOf = color => /^#[0-9a-f]{6}$/i.test(color || '') ? color : '#a6bad0';
  function classify(command, palette) {
    const group = String(command.group || '').toLowerCase();
    const base = palette.groups.find(rule => rule.match.some(word => group.includes(word.toLowerCase()))) || palette.fallback;
    const text = (String(command.id || '').split('/').pop() + ' ' + command.label).replace(/\s+/g, ' ').toLowerCase();
    const action = palette.actions.find(rule => rule.match.some(word => text.includes(word.toLowerCase())));
    return { icon: action?.icon || base.icon, color: action?.color || base.color };
  }
  function prettyKey(combo) {
    const names = { lctrl:'Ctrl', rctrl:'R Ctrl', lalt:'Alt', ralt:'R Alt', lshift:'Shift', rshift:'R Shift',
      backspace:'Bksp', enter:'Enter', space:'Space', escape:'Esc', capslock:'Caps', scrolllock:'Scroll', numlock:'Num Lock',
      mouse1:'LMB', mouse2:'RMB', mouse3:'MMB', mwheel_up:'Wheel ↑', mwheel_down:'Wheel ↓',
      np_enter:'Num Enter', np_period:'Num .', np_multiply:'Num ×', np_divide:'Num /', np_add:'Num +', np_subtract:'Num −',
      pgup:'PgUp', pgdn:'PgDn', up:'↑', down:'↓', left:'←', right:'→' };
    const parts = String(combo || '').split('+').map(part => part.trim().toLowerCase());
    const modifier = key => /^(l|r)(ctrl|alt|shift)$/.test(key);
    return [...parts.filter(modifier), ...parts.filter(key => !modifier(key))].map(part => {
      const key = part.trim().toLowerCase();
      return names[key] || (/^np_\d$/.test(key) ? 'Num ' + key.slice(3) : key.toUpperCase());
    }).join(' + ');
  }
  // The device's SVG renderer has no canvas to measure with. These Segoe UI
  // advances were measured in Chrome; a small allowance keeps edges clear.
  const advances = [0.276,0.328,0.494,0.593,0.576,0.868,0.85,0.293,0.37,0.37,0.456,0.708,0.271,0.405,0.271,0.444,0.576,0.576,0.576,0.576,0.576,0.576,0.576,0.576,0.576,0.576,0.271,0.271,0.708,0.708,0.708,0.438,0.955,0.704,0.642,0.625,0.738,0.533,0.521,0.711,0.767,0.317,0.446,0.649,0.512,0.958,0.791,0.759,0.615,0.759,0.653,0.561,0.586,0.724,0.667,1.005,0.656,0.607,0.607,0.37,0.437,0.37,0.708,0.416,0.314,0.539,0.621,0.48,0.62,0.542,0.384,0.62,0.603,0.285,0.285,0.56,0.285,0.917,0.605,0.612,0.621,0.62,0.398,0.44,0.39,0.605,0.542,0.798,0.553,0.539,0.48,0.37,0.327,0.37,0.708];
  function width(text, size) {
    return [...text].reduce((sum, ch) => {
      const code = ch.normalize('NFD').charCodeAt(0) - 32;
      return sum + size * (code >= 0 && code < advances.length ? advances[code] * 1.02 : 1);
    }, 0);
  }
  function wrap(text, size, available, breakWords = true) {
    const result = [];
    for (const paragraph of String(text || '').replace(/\\n/g, '\n').split('\n')) {
      let line = '';
      for (let word of paragraph.trim().split(/\s+/).filter(Boolean)) {
        if (line && width(line + ' ' + word, size) > available) { result.push(line); line = ''; }
        while (breakWords && width(word, size) > available) {
          let chunk = '';
          for (const ch of word) { if (chunk && width(chunk + ch, size) > available) break; chunk += ch; }
          result.push(chunk); word = word.slice(chunk.length);
        }
        line += (line && word ? ' ' : '') + word;
      }
      if (line) result.push(line);
    }
    return result;
  }
  function textBlock(label, center, available = 122, maxSize = 36, twoLineSize = 28) {
    let size = maxSize, lines = [];
    // Shrink intact words first. Breaking a short word at a large size can
    // make it look worse than the same word fitting cleanly one size down.
    for (; size >= 18; size -= 2) {
      lines = wrap(label, size, available, false);
      if (lines.every(line => width(line, size) <= available)
          && (lines.length <= 1 || (size <= twoLineSize && lines.length <= 2) || (size <= 18 && lines.length <= 3))) break;
    }
    size = Math.max(18, size);
    if (lines.some(line => width(line, size) > available)) lines = wrap(label, size, available);
    if (lines.length > 3) {
      lines = lines.slice(0, 3); let last = lines[2];
      while (last && width(last + '…', size) > available) last = last.slice(0, -1);
      lines[2] = last + '…';
    }
    const step = size * 1.1, y = center - (lines.length - 1) * step / 2 + size * .34;
    return lines.map((line, index) => `<text x="72" y="${(y + index * step).toFixed(1)}" font-size="${size}">${esc(line)}</text>`).join('');
  }
  const glyph = (icon, color, x, y, size) => `<g transform="translate(${x} ${y}) scale(${size / 24})" fill="none" stroke="${color}" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${icons[icon] || icons.command}</g>`;
  const frame = (color, pressed, warn) => `<rect width="144" height="144" rx="12" fill="${pressed ? '#182938' : '#0a1018'}"/><rect x="2" y="2" width="140" height="140" rx="10" fill="none" stroke="${warn ? '#ffa08b' : color}" stroke-opacity="${pressed || warn ? 1 : .5}" stroke-width="${pressed || warn ? 3 : 1.5}"/><path d="M17 3h110" stroke="${color}" stroke-width="3"/>`;
  const svg = (body, title) => `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144" style="letter-spacing:0"><title>${esc(title)}</title>${body}</svg>`;
  function renderKey(options) {
    const color = colorOf(options.color), key = options.keyText || '', label = String(options.label || '');
    let keySize = width(key, 18) <= 124 ? 18 : 14;
    let hints = key ? wrap(key, keySize, 124) : [];
    if (hints.length > 2) { keySize = 12; hints = wrap(key, keySize, 124); }
    const center = hints.length > 1 ? 78 : 81, iconSize = hints.length > 1 ? 28 : 32;
    return svg(frame(color, options.pressed, options.warn)
      + glyph(options.warn ? 'warning' : options.icon, options.warn ? '#ffa08b' : color, 72 - iconSize / 2, 12, iconSize)
      + `<g fill="#f5f8fc" font-weight="650" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif">${textBlock(label, center, 122, 36, hints.length > 1 ? 24 : 28)}</g>`
      + (key ? `<path d="M16 115h112" stroke="#90a8c0" stroke-opacity=".24"/><g fill="#c9d6e5" text-anchor="middle" font-size="${keySize}" font-weight="600" font-family="Segoe UI, Arial, sans-serif">${hints.map((line, index) => `<text x="72" y="${hints.length > 1 ? 126 + index * 13 : 133}">${esc(line)}</text>`).join('')}</g>` : ''), label + (key ? ' · ' + key : ''));
  }
  function renderFolder(options) {
    const color = colorOf(options.color);
    return svg(frame(color, false, false) + glyph(options.icon || 'command', color, 49, 13, 46)
      + `<g fill="#f5f8fc" font-weight="650" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif">${textBlock(options.label, 107, 122, 26)}</g>`, options.label || '');
  }
  globalThis.QWDeckArt = Object.freeze({ icons, classify, prettyKey, renderKey, renderFolder,
    svgDataUrl: source => 'data:image/svg+xml;charset=utf8,' + encodeURIComponent(source) });
})();
