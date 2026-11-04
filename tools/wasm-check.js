// WebAssembly runtime check: drives the Release browser build of TRACE in headless Chromium
// through one critical workflow, and saves a screenshot of each step.
//
//   dotnet publish Trace/Trace.csproj -f net10.0-browserwasm -c Release \
//     -p:TraceTargetFrameworks=net10.0-browserwasm -o <publish dir>
//   python3 -m http.server 8765 --bind 127.0.0.1 --directory <publish dir>/wwwroot &
//   NODE_PATH=<dir holding playwright> node tools/wasm-check.js http://127.0.0.1:8765/ <out dir>
//
// The workflow: open P-204 from Today, open the seal housing point from the strip under the
// machine, enter 7.6 mm/s with a note, call Fail, Complete point, then open History. The page
// is drawn on a canvas, so the steps press at window coordinates of the 1280 x 800 layout, the
// same layout the desktop head draws at that size. The note field is reached by keyboard first:
// in the browser, pressing it while it is mostly scrolled out of the inspector's middle closed
// the inspector (see docs/COMPLETENESS.md).
const { chromium } = require('playwright');
const path = require('path');

const url = process.argv[2] ?? 'http://127.0.0.1:8765/';
const out = process.argv[3] ?? '.';
const sleep = ms => new Promise(r => setTimeout(r, ms));

const steps = [
  { shot: '01-today' },
  { click: [600, 270], until: 'changed', shot: '02-asset' },
  { click: [300, 736], until: 'changed', shot: '03-point' },
  { click: [1055, 505] },
  { key: 'End' }, { key: 'Backspace' }, { key: 'Backspace' }, { key: 'Backspace' },
  { type: '7.6' },
  { key: 'Shift+Tab' }, { key: 'Shift+Tab', wait: 1200 },
  { click: [1053, 330] },
  { type: 'Seal weeping at the gland, drive end vibration up.' },
  { click: [1175, 566], wait: 2000, shot: '04-ready' },
  { click: [1055, 729], wait: 3000, shot: '05-completed' },
  { click: [481, 48], wait: 3000, shot: '06-history' },
];

(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

  await page.goto(url, { waitUntil: 'load', timeout: 120000 });
  await sleep(Number(process.env.SETTLE ?? 30000));

  for (const s of steps) {
    if (s.click) {
      const before = s.until ? await page.screenshot() : null;
      await page.mouse.move(s.click[0], s.click[1]);
      await page.mouse.down();
      await sleep(150);
      await page.mouse.up();
      if (before) {
        // A step that opens a page or a panel waits until the frame shows it: under load the
        // runtime can take several seconds, and the next press would land on the old screen.
        for (let i = 0; i < 60 && Buffer.compare(before, await page.screenshot()) === 0; i++) {
          await sleep(500);
        }
        await sleep(2500);
      } else {
        await sleep(s.wait ?? 1500);
      }
    }
    if (s.key) { await page.keyboard.press(s.key); await sleep(s.wait ?? 800); }
    if (s.type) { await page.keyboard.type(s.type, { delay: 40 }); await sleep(s.wait ?? 1500); }
    if (s.shot) {
      await page.screenshot({ path: path.join(out, `${s.shot}.png`) });
      console.log(`WASM shot ${s.shot}`);
    }
  }

  console.log(`WASM errors ${errors.length}${errors.length ? ': ' + errors.join(' | ') : ''}`);
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
