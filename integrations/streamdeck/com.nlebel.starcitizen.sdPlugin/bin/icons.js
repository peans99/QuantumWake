// Renders a legible 144x144 key face with a function-colored accent.
const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

function darken(hex, amount) {
  const n = parseInt(hex.replace("#", ""), 16);
  const rgb = [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  return `rgb(${rgb.map((v) => Math.round(v * (1 - amount))).join(",")})`;
}

/** @param {{label:string, color:string, keyText:string, pressed?:boolean, warn?:boolean}} o */
export function renderKey(o) {
  const color = o.color || "#94a3b8";
  const lines = [];
  for (const original of String(o.label || "").split("\n")) {
    let line = "";
    for (const word of original.split(/\s+/)) {
      if (line && (line + " " + word).length > 13) { lines.push(line); line = word; }
      else line += (line ? " " : "") + word;
    }
    if (line) lines.push(line);
  }
  if (lines.length > 3) { lines.length = 3; lines[2] = lines[2].slice(0, 12) + "…"; }
  const hasKey = !!o.keyText;
  const longest = Math.max(...lines.map((l) => [...l].length), 1);
  const cap = lines.length === 1 ? 43 : lines.length === 2 ? 35 : 27;
  const size = Math.max(16, Math.min(cap, Math.floor(122 / (longest * 0.66))));
  const lh = size * 1.06;
  const centerY = hasKey ? 65 : 75;
  const y0 = centerY - ((lines.length - 1) * lh) / 2 + size * 0.35;
  const text = lines.map ( (l, i) =>
    `<text x="72" y="${(y0 + i * lh).toFixed(1)}" fill="#ffffff" font-size="${size}" font-weight="700" text-anchor="middle" font-family="Segoe UI, Arial">${esc(l)}</text>`).join("");
  const keySize = Math.min(16, Math.max(10, Math.floor(118 / (Math.max([...String(o.keyText || "")].length, 1) * 0.62))));
  const top = o.warn ? "#ef4444" : color;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144">
<defs><linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop stop-color="${darken(color, o.pressed ? 0.52 : 0.72)}"/><stop offset="1" stop-color="${darken(color, o.pressed ? 0.75 : 0.90)}"/></linearGradient></defs>
<rect width="144" height="144" rx="12" fill="#080e16"/>
<rect x="3" y="3" width="138" height="138" rx="10" fill="url(#bg)" stroke="${top}" stroke-opacity="${o.pressed || o.warn ? 1 : 0.65}" stroke-width="${o.pressed || o.warn ? 3 : 2}"/>
<path d="M15 10 H129" stroke="${top}" stroke-width="4" stroke-linecap="round"/>
${o.pressed ? `<path d="M15 134 H129" stroke="${color}" stroke-width="3" stroke-linecap="round"/>` : ""}
${text}
${hasKey ? `<path d="M28 109 H116" stroke="#ffffff" stroke-opacity="0.25"/><text x="72" y="129" fill="#ffffff" fill-opacity="0.9" font-size="${keySize}" font-weight="600" text-anchor="middle" font-family="Consolas, Segoe UI, monospace">${esc(o.keyText)}</text>` : ""}
</svg>`;
}

export const svgDataUrl = (svg) => "data:image/svg+xml;charset=utf8," + encodeURIComponent(svg);
