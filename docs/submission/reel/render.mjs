// Renders reel.html frame by frame in headless Chrome.
//   node render.mjs stills 0.5 2.1 ...      → out/stills/t_<sec>.png
//   node render.mjs video [--sub 4] [--from 0 --to <dur>] [--out name.mp4]  → out/<name> (+ out/events.json)
// --sub N renders N sub-frames inside a 180° shutter and averages them (real motion blur).
import puppeteer from 'puppeteer-core';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.woff2': 'font/woff2', '.png': 'image/png', '.webp': 'image/webp', '.json': 'application/json' };
const argv = process.argv.slice(2);
const mode = argv[0] || 'stills';
const opt = (k, d) => { const i = argv.indexOf('--' + k); return i >= 0 ? argv[i + 1] : d; };

const server = http.createServer((req, res) => {
  const p = path.join(ROOT, decodeURIComponent(req.url.split('?')[0]));
  fs.readFile(p, (e, b) => { if (e) { res.writeHead(404); res.end(); return; } res.writeHead(200, { 'Content-Type': MIME[path.extname(p)] || 'application/octet-stream' }); res.end(b); });
}).listen(0);
const port = server.address().port;

const browser = await puppeteer.launch({ executablePath: CHROME, headless: true,
  args: ['--hide-scrollbars', '--force-device-scale-factor=1', '--disable-background-timer-throttling', '--font-render-hinting=none', '--force-color-profile=srgb'] });
const page = await browser.newPage();
await page.setViewport({ width: 1920, height: 1080, deviceScaleFactor: 1 });
page.on('console', m => console.log('[page]', m.text()));
page.on('pageerror', e => { console.error('[pageerror]', e.message); });
const query = opt('query', '');
await page.goto(`http://127.0.0.1:${port}/reel.html?render=1${query ? '&' + query : ''}`, { waitUntil: 'load' });
await page.evaluate(() => window.ready);
fs.mkdirSync(path.join(ROOT, 'out', 'stills'), { recursive: true });
const cues = await page.evaluate(() => ({ timescale: window.TIMESCALE || 1, duration: window.DUR, events: window.EVENTS }));
const cuesPath = path.join(ROOT, 'out', opt('events', 'events.json'));
fs.mkdirSync(path.dirname(cuesPath), { recursive: true });
fs.writeFileSync(cuesPath, JSON.stringify(cues, null, 1));

const shot = async (t) => {
  await page.evaluate(tt => window.seek(tt), t);
  return page.screenshot({ type: 'png', clip: { x: 0, y: 0, width: 1920, height: 1080 }, optimizeForSpeed: true });
};

if (mode === 'stills') {
  const times = argv.slice(1).filter(a => !a.startsWith('--')).map(Number).filter(Number.isFinite);
  for (const t of times) {
    const b = await shot(t);
    fs.writeFileSync(path.join(ROOT, 'out', 'stills', `t_${t.toFixed(2)}.png`), b);
  }
  console.log('stills', times.length);
} else if (mode === 'video') {
  const FPS = 60, SUB = +opt('sub', 1), from = +opt('from', 0), to = +opt('to', cues.duration);
  const out = path.join(ROOT, 'out', opt('out', 'video_silent.mp4'));
  fs.mkdirSync(path.dirname(out), { recursive: true });
  const vf = SUB > 1 ? `tmix=frames=${SUB},select='eq(mod(n\\,${SUB})\\,${SUB - 1})',setpts=N/(${FPS}*TB)` : 'null';
  const ff = spawn('ffmpeg', ['-y', '-v', 'error', '-f', 'image2pipe', '-framerate', String(FPS * SUB), '-c:v', 'png', '-i', '-',
    '-vf', vf, '-r', String(FPS), '-c:v', 'libx264', '-preset', 'slow', '-crf', '12', '-pix_fmt', 'yuv420p', '-colorspace', 'bt709', '-color_primaries', 'bt709', '-color_trc', 'bt709', out], { stdio: ['pipe', 'inherit', 'inherit'] });
  const n = Math.round((to - from) * FPS);
  const t0 = Date.now();
  for (let f = 0; f < n; f++) {
    for (let s = 0; s < SUB; s++) {
      // 180° shutter: sub-samples spread over the first half of the frame interval
      const t = from + f / FPS + (SUB > 1 ? (s / SUB) * (0.5 / FPS) : 0);
      const b = await shot(t);
      if (!ff.stdin.write(b)) await new Promise(r => ff.stdin.once('drain', r));
    }
    if (f % 60 === 0) console.log(`frame ${f}/${n}  ${((Date.now() - t0) / 1000).toFixed(0)}s`);
  }
  ff.stdin.end();
  await new Promise(r => ff.on('close', r));
  console.log('video', out, ((Date.now() - t0) / 1000).toFixed(0) + 's');
}
await browser.close();
server.close();
