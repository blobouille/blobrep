// Planche de test des familles de couleur des gumori.
// node Captures/bancs/palette.js  ->  Captures/palette/planche.html
const fs = require('fs');
const path = require('path');

const families = [
  { name: 'Orange', tones: ['#FFA63D', '#FFB75A', '#FF9A2C'] },
  { name: 'Violet', tones: ['#A048DE', '#8E38CC', '#B25DEA'] },
  { name: 'Rose', tones: ['#FFA4D1', '#FF94C8', '#FFB8DC'] },
  { name: 'Bleu', tones: ['#3F86EE', '#3274E0', '#5296F2'] },
  { name: 'Jaune', tones: ['#FFD444', '#FFC62A', '#FFDF66'] },
  { name: 'Rouge', tones: ['#E0303F', '#C82434', '#F04A47'] },
  { name: 'Crème', tones: ['#FFEFF3', '#FFF8F0', '#F6E2E6'] },
  { name: 'Chocolat', tones: ['#7A4524', '#663819', '#8C522C'] },
  { name: 'Vert', tones: ['#8BD63A', '#A2E05A', '#74C42A'] },
  { name: 'Noir', tones: ['#2E2638', '#221C2B', '#3A3046'] },
];
const N = families.length;
const backgrounds = [
  { name: 'Fond clair', color: '#E4EEF3' },
  { name: 'Fond nuit', color: '#141A33' },
];

const toLin = c => (c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4));
const toSrgb = c => (c <= 0.0031308 ? 12.92 * c : 1.055 * Math.pow(c, 1 / 2.4) - 0.055);
const clamp = v => Math.min(1, Math.max(0, v));
const parse = h => [1, 3, 5].map(i => toLin(parseInt(h.substr(i, 2), 16) / 255));
const hex = lin => '#' + lin.map(v => Math.round(clamp(toSrgb(clamp(v))) * 255).toString(16).padStart(2, '0')).join('');
const mul = (m, v) => m.map(r => r[0] * v[0] + r[1] * v[1] + r[2] * v[2]);

// Machado et al. 2009, sévérité 1, en RGB linéaire
const modes = [
  { name: 'Vision normale', f: v => v },
  { name: 'Deutéranopie (vert, ~5 % des hommes)', f: v => mul([[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.01182, 0.04294, 0.968881]], v) },
  { name: 'Protanopie (rouge, ~1 %)', f: v => mul([[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]], v) },
  { name: 'Tritanopie (bleu, rare)', f: v => mul([[1.255528, -0.076749, -0.178779], [-0.078411, 0.930809, 0.147602], [0.004733, 0.691367, 0.3039]], v) },
  { name: 'Niveaux de gris', f: v => { const y = 0.2126 * v[0] + 0.7152 * v[1] + 0.0722 * v[2]; return [y, y, y]; } },
];

function oklab(v) {
  const [r, g, b] = v.map(clamp);
  const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
  const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
  const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
  return [0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s,
    1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s,
    0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s];
}
const dE = (a, b) => { const p = oklab(a), q = oklab(b); return Math.hypot(p[0] - q[0], p[1] - q[1], p[2] - q[2]); };

const diameter = n => 1 + 0.37 * n;
const PX = 34;

function gumori(cx, cy, d, fillLin, bg) {
  const r = d * PX / 2;
  const fill = hex(fillLin);
  const dark = hex(fillLin.map(v => v * 0.35));
  const rim = hex(fillLin.map(v => v * 0.7 + 0.3 * 1));
  const eye = r * 0.11, ex = r * 0.3, ey = cy - r * 0.08;
  return `<g>
<ellipse cx="${cx}" cy="${cy + r * 0.92}" rx="${r * 0.8}" ry="${r * 0.12}" fill="#000" opacity="0.15"/>
<circle cx="${cx}" cy="${cy}" r="${r}" fill="${dark}"/>
<circle cx="${cx}" cy="${cy}" r="${r * 0.95}" fill="${rim}" opacity="0.75"/>
<circle cx="${cx}" cy="${cy}" r="${r * 0.88}" fill="${fill}"/>
<ellipse cx="${cx - r * 0.35}" cy="${cy - r * 0.45}" rx="${r * 0.22}" ry="${r * 0.13}" fill="#fff" opacity="0.55" transform="rotate(-30 ${cx - r * 0.35} ${cy - r * 0.45})"/>
<circle cx="${cx - ex}" cy="${ey}" r="${eye}" fill="#1a1222"/><circle cx="${cx + ex}" cy="${ey}" r="${eye}" fill="#1a1222"/>
<circle cx="${cx - ex + eye * 0.35}" cy="${ey - eye * 0.35}" r="${eye * 0.35}" fill="#fff"/><circle cx="${cx + ex + eye * 0.35}" cy="${ey - eye * 0.35}" r="${eye * 0.35}" fill="#fff"/>
<path d="M ${cx - r * 0.12} ${cy + r * 0.18} Q ${cx} ${cy + r * 0.3} ${cx + r * 0.12} ${cy + r * 0.18}" stroke="#1a1222" stroke-width="${Math.max(1.2, r * 0.05)}" fill="none" stroke-linecap="round"/>
</g>`;
}

function special(cx, cy, d, mode, id) {
  const r = d * PX / 2;
  const stops = ['#ff6fa8', '#ffd23f', '#5fe3a1', '#4a8bea', '#a46cf0'].map((c, i) =>
    `<stop offset="${i / 4}" stop-color="${hex(mode.f(parse(c)))}"/>`).join('');
  return `<defs><linearGradient id="${id}" x1="0" y1="0" x2="1" y2="1">${stops}</linearGradient></defs>
<ellipse cx="${cx}" cy="${cy + r * 0.92}" rx="${r * 0.8}" ry="${r * 0.12}" fill="#000" opacity="0.15"/>
<circle cx="${cx}" cy="${cy}" r="${r}" fill="#2a1d3a"/><circle cx="${cx}" cy="${cy}" r="${r * 0.9}" fill="url(#${id})"/>`;
}

let uid = 0;
function ladder(bg, mode) {
  const gap = 10;
  let x = gap, parts = [];
  const H = diameter(N) * PX + 40;
  for (let n = 0; n <= N; n++) {
    const d = diameter(n), r = d * PX / 2;
    const cx = x + r, cy = H - 30 - r;
    parts.push(n < N ? gumori(cx, cy, d, mode.f(parse(families[n].tones[0])), bg) : special(cx, cy, d, mode, 'sp' + uid++));
    parts.push(`<text x="${cx}" y="${H - 8}" text-anchor="middle" class="lbl" fill="${bg.color === '#141A33' ? '#c9d2ee' : '#3a4256'}">${n + 1}${n < N ? ' ' + families[n].name : ' ★'}</text>`);
    x += d * PX + gap;
  }
  return `<svg viewBox="0 0 ${x} ${H}" width="${x}" height="${H}" style="background:${bg.color}">${parts.join('')}</svg>`;
}

function tones(bg, mode) {
  const size = 1.6, gap = 8, R = size * PX / 2;
  let x = gap, parts = [];
  const H = size * PX + 34;
  families.forEach((f, n) => {
    f.tones.forEach(t => { parts.push(gumori(x + R, R + 8, size, mode.f(parse(t)), bg)); x += size * PX + 4; });
    parts.push(`<text x="${x - (size * PX + 4) * 1.5 - 2}" y="${H - 6}" text-anchor="middle" class="lbl" fill="${bg.color === '#141A33' ? '#c9d2ee' : '#3a4256'}">${n + 1}</text>`);
    x += gap * 2;
  });
  return `<svg viewBox="0 0 ${x} ${H}" width="${x}" height="${H}" style="background:${bg.color}">${parts.join('')}</svg>`;
}

// Pile réaliste : niveaux 1 à 5 mélangés, nuances au hasard (graine fixe)
let seed = 12345;
const rnd = () => { seed ^= seed << 13; seed >>>= 0; seed ^= seed >>> 17; seed ^= seed << 5; seed >>>= 0; return seed / 4294967296; };
const pileItems = [];
for (let i = 0; i < 26; i++) pileItems.push({ n: Math.floor(rnd() * rnd() * 6), t: Math.floor(rnd() * 3), x: rnd() });
function pile(bg, mode) {
  const W = 420, H = 300, parts = [];
  const placed = [];
  pileItems.forEach(it => {
    const r = diameter(it.n) * PX / 2;
    let cx = 10 + r + it.x * (W - 20 - 2 * r), cy = H - 10 - r;
    for (let k = 0; k < 400; k++) {
      const hit = placed.find(p => Math.hypot(p.cx - cx, p.cy - cy) < p.r + r - 2);
      if (!hit) break;
      cy -= 2;
    }
    if (cy - r < 0) return;
    placed.push({ cx, cy, r });
    parts.push(gumori(cx, cy, diameter(it.n), mode.f(parse(families[it.n].tones[it.t])), bg));
  });
  return `<svg viewBox="0 0 ${W} ${H}" width="${W}" height="${H}" style="background:${bg.color}">${parts.join('')}</svg>`;
}

// Mesures : écart OKLab des niveaux voisins (et à 2 niveaux), pire des nuances
const rows = [];
for (let gapN = 1; gapN <= 2; gapN++) for (let n = 0; n + gapN < N; n++) {
  const cells = modes.map(m => {
    let worst = Infinity;
    for (const a of families[n].tones) for (const b of families[n + gapN].tones) worst = Math.min(worst, dE(m.f(parse(a)), m.f(parse(b))));
    return worst;
  });
  rows.push({ a: n, b: n + gapN, gapN, cells });
}
const cls = v => (v < 0.08 ? 'bad' : v < 0.14 ? 'mid' : 'ok');
const table = `<table><tr><th>Paire</th>${modes.map(m => `<th>${m.name.split(' (')[0]}</th>`).join('')}</tr>${rows.map(r =>
  `<tr${r.gapN === 2 ? ' class="far"' : ''}><td>${r.a + 1} ${families[r.a].name} ↔ ${r.b + 1} ${families[r.b].name}${r.gapN === 2 ? ' (à 2 niveaux)' : ''}</td>${r.cells.map(v =>
    `<td class="${cls(v)}">${v.toFixed(3)}</td>`).join('')}</tr>`).join('')}</table>`;

let html = `<!doctype html><html lang="fr"><head><meta charset="utf-8"><title>Planche des gumori</title><style>
body{font-family:system-ui,sans-serif;background:#f4f4f6;color:#222;margin:20px}
h1{font-size:22px}h2{font-size:17px;margin-top:34px}h3{font-size:14px;margin:14px 0 4px;color:#555}
svg{display:block;border-radius:10px;max-width:100%;height:auto}.lbl{font-size:12px;font-weight:600}
.grid{display:flex;flex-wrap:wrap;gap:12px}.grid>div{flex:0 0 auto}
table{border-collapse:collapse;font-size:13px}td,th{border:1px solid #ccc;padding:3px 8px;text-align:right}td:first-child{text-align:left}
.bad{background:#f7b4b4}.mid{background:#fbe3a6}.ok{background:#c8ecc8}.far td{color:#666}
</style></head><body><h1>Planche de test des 10 familles de gumori</h1>
<p>Tailles réelles relatives (diamètre = 1 + 0,37 × niveau). Simulations de Machado 2009 (sévérité totale, cas le plus dur).</p>
<h2>Écarts mesurés (OKLab ΔE, pire combinaison des 3 nuances)</h2>
<p>Rouge &lt; 0,08 : risque de confusion · orange 0,08–0,14 : à surveiller · vert &gt; 0,14 : sûr. Les paires à 2 niveaux ont en plus une nette différence de taille.</p>
${table}`;
for (const bg of backgrounds) {
  html += `<h2>${bg.name} : l'échelle complète</h2>`;
  for (const m of modes) html += `<h3>${m.name}</h3>${ladder(bg, m)}`;
}
for (const bg of backgrounds) {
  html += `<h2>${bg.name} : 3 nuances par famille</h2>`;
  for (const m of modes) html += `<h3>${m.name}</h3>${tones(bg, m)}`;
}
html += `<h2>Pile réaliste (niveaux 1 à 6 mélangés, nuances au hasard)</h2><div class="grid">`;
for (const bg of backgrounds) for (const m of modes) html += `<div><h3>${bg.name} · ${m.name.split(' (')[0]}</h3>${pile(bg, m)}</div>`;
html += `</div></body></html>`;

const out = path.join(__dirname, '..', 'palette', 'planche.html');
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, html);
for (const r of rows) console.log(`${r.a + 1}-${r.b + 1} ${families[r.a].name}/${families[r.b].name}`.padEnd(24), r.cells.map(v => v.toFixed(3)).join('  '));
console.log('->', out);
