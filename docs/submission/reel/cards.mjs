// Renders the caption set as 1920x1080 PNGs with alpha.
//   node cards.mjs                 -> out/cards/<n>.png (panel variant) and out/cards/<n>w.png (lower-third variant)
//   node cards.mjs --only 1,9 --pin '{"panel":[47,35],"wide":[60,46]}'   re-render some cards at the delivered sizes
import puppeteer from 'puppeteer-core';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const CAPTIONS = [
  'front camera only · camera images stay on the phone',
  'tilt to steer · tap to jump',
  'three lanes · one thumb-free run',
  "crash · tap · you're back",
  'DAILY · 2026-09-30 · same track for everyone',
  'camera mode is opt-in · the phone is speed-tested first',
  'RevenueCat · Paywall Builder · cosmetics only',
  'no lives · no boosts · no ads',
  'OneSignal · push notifications',
  'challenge a friend · Layers-instrumented',
];
const MIME = { '.html': 'text/html', '.woff2': 'font/woff2' };
const server = http.createServer((req, res) => {
  const p = path.join(ROOT, decodeURIComponent(req.url.split('?')[0]));
  fs.readFile(p, (e, b) => { if (e) { res.writeHead(404); res.end(); return; } res.writeHead(200, { 'Content-Type': MIME[path.extname(p)] || 'application/octet-stream' }); res.end(b); });
}).listen(0);
const browser = await puppeteer.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true,
  args: ['--hide-scrollbars', '--force-device-scale-factor=1', '--font-render-hinting=none', '--force-color-profile=srgb'] });
const page = await browser.newPage();
await page.setViewport({ width: 1920, height: 1080, deviceScaleFactor: 1 });
page.on('pageerror', e => console.error('[pageerror]', e.message));
await page.goto(`http://127.0.0.1:${server.address().port}/cards.html`, { waitUntil: 'load' });
await page.evaluate(() => window.ready);
const argv = process.argv.slice(2); const opt = k => { const i = argv.indexOf('--' + k); return i >= 0 ? argv[i + 1] : null; };
let size = await page.evaluate(list => window.setCaptions(list), CAPTIONS);
console.log('fitted sizes', JSON.stringify(size));
if (opt('pin')) { size = JSON.parse(opt('pin')); await page.evaluate(s => { SIZES = s; }, size); }
const only = opt('only') ? opt('only').split(',').map(Number) : null;
const outDir = path.join(ROOT, 'out', 'cards');
fs.mkdirSync(outDir, { recursive: true });
const boxes = [];
for (let i = 0; i < CAPTIONS.length; i++) {
  if (only && !only.includes(i + 1)) continue;
  for (const [variant, suffix] of [['panel', ''], ['wide', 'w']]) {
    const box = await page.evaluate((t, v, s) => window.renderCard(t, v, s), CAPTIONS[i], variant, 100 + i);
    const file = path.join(outDir, `${i + 1}${suffix}.png`);
    fs.writeFileSync(file, await page.screenshot({ type: 'png', omitBackground: true, clip: { x: 0, y: 0, width: 1920, height: 1080 } }));
    boxes.push(`${i + 1}${suffix}.png  x ${Math.round(box.x)}-${Math.round(box.x + box.w)}  y ${Math.round(box.y)}-${Math.round(box.y + box.h)}`);
  }
}
console.log('type sizes [headline, sub-line] px', JSON.stringify(size));
console.log(boxes.join('\n'));
await browser.close();
server.close();
