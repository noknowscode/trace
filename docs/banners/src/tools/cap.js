// TRACE capture driver (adapted from Cargo's src/tools/cap.js): drives the Release WebAssembly build in headless Chromium
// as a user would, and saves real captures.
//
//   node cap.js <url> <outdir> <WxH> <dpr> <steps.json>        replay a session (batch)
//   node cap.js <url> <outdir> <WxH> <dpr> --serve <port>      interactive: POST a JSON step list to http://127.0.0.1:<port>/;
//                                                              every batch is appended to <outdir>/session.json, so the
//                                                              session can be replayed with the first form
//
// steps: {click:[x,y], until:true}   press at CSS px; with until, wait until the frame changes (a page or panel opening)
//        {key:"End"} {type:"7.6"} {wait:ms} {move:[x,y]} {drag:[[x0,y0],[x1,y1]], steps:n} {wheel:[dx,dy], at:[x,y]}
//        {reload:true}                reload the page: the app starts again on the data and theme it saved on the device
//        {size:[w,h]}                 resize the viewport (the app re-lays out live, so one session serves every width)
//        {shot:"name", clip:{x,y,width,height}}  clip in CSS px; the file holds device pixels (CSS x dpr)
//        {peek:"name"}                a CSS-scale look at the whole page, for steering (not a deliverable)
//
// The page is one canvas (Uno Skia renderer), so presses go to window coordinates of the layout at that viewport.
// TRACE does not follow the browser's colour scheme: it starts Dark, and the Light/Dark switch is the button on Today.
// Before every shot the pointer is parked in the top-right corner (empty at every width) and the frame must hold still.
const { chromium } = require('playwright');
const fs = require('fs');
const http = require('http');
const path = require('path');
const sleep = ms => new Promise(r => setTimeout(r, ms));
(async () => {
  const [url, out, size, dpr, mode, port] = process.argv.slice(2);
  let [w, h] = size.split('x').map(Number);
  fs.mkdirSync(out, { recursive: true });
  const b = await chromium.launch({ args: ['--hide-scrollbars', '--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  const ctx = await b.newContext({ viewport: { width: w, height: h }, deviceScaleFactor: Number(dpr), colorScheme: 'dark' });
  const p = await ctx.newPage();
  const log = [];
  p.on('pageerror', e => log.push('pageerror ' + e.message));
  await p.goto(url, { waitUntil: 'load', timeout: 180000 });
  const frame = () => p.screenshot({ scale: 'css', timeout: 120000 });
  async function settle(min = 1500) {
    await sleep(min);
    let a = await frame();
    for (let i = 0; i < 40; i++) { await sleep(1200); const c = await frame(); if (Buffer.compare(a, c) === 0) return; a = c; }
  }
  // First render: the splash gives way to Today; wait for a steady frame, then a little more for fonts and the feeds.
  const t0 = Date.now();
  await sleep(20000);
  await settle();
  await sleep(5000);
  console.log('rendered after', Math.round((Date.now() - t0) / 1000), 's');
  await p.mouse.move(w - 2, 2);

  async function run(steps) {
    const res = [];
    for (const s of steps) {
      if (s.size) { [w, h] = s.size; await p.setViewportSize({ width: w, height: h }); await settle(2500); }
      if (s.reload) { await p.reload({ waitUntil: 'load', timeout: 180000 }); await sleep(20000); await settle(); await sleep(3000); }
      if (s.move) await p.mouse.move(...s.move);
      if (s.wheel) { await p.mouse.move(...(s.at || [w / 2, h / 2])); await p.mouse.wheel(...s.wheel); await sleep(800); }
      if (s.drag) { await p.mouse.move(...s.drag[0]); await p.mouse.down(); await p.mouse.move(...s.drag[1], { steps: s.steps || 20 }); await p.mouse.up(); }
      if (s.click) {
        const before = s.until ? await frame() : null;
        await p.mouse.move(...s.click); await p.mouse.down(); await sleep(120); await p.mouse.up();
        if (before) { let i = 0; for (; i < 60 && Buffer.compare(before, await frame()) === 0; i++) await sleep(500); res.push('changed after ' + i); await sleep(2500); }
        else await sleep(1200);
      }
      if (s.key) { await p.keyboard.press(s.key); await sleep(400); }
      if (s.type) { await p.keyboard.type(s.type, { delay: 40 }); await sleep(800); }
      if (s.wait) await sleep(s.wait);
      if (s.shot || s.peek) {
        await p.mouse.move(w - 2, 2);
        await settle(800);
        const file = path.join(out, (s.shot || s.peek) + '.png');
        await p.screenshot({ path: file, clip: s.clip, scale: s.peek ? 'css' : 'device', animations: 'disabled', timeout: 180000 });
        res.push((s.shot ? 'shot ' : 'peek ') + file);
      }
    }
    res.push(...log.splice(0));
    return res;
  }

  process.on('SIGTERM', async () => { await b.close().catch(() => {}); process.exit(0); });
  if (mode === '--serve') {
    const record = path.join(out, 'session.json');
    const all = fs.existsSync(record) ? JSON.parse(fs.readFileSync(record, 'utf8')) : [];
    http.createServer((req, res) => {
      let body = ''; req.on('data', d => body += d);
      req.on('end', async () => {
        try {
          const steps = JSON.parse(body);
          all.push(...steps.filter(s => !s.peek)); fs.writeFileSync(record, JSON.stringify(all, null, 1));
          res.end(JSON.stringify(await run(steps)) + '\n');
        } catch (e) { res.end('ERR ' + e.message + '\n'); }
      });
    }).listen(Number(port), '127.0.0.1', () => console.log('serving on', port));
  } else {
    console.log((await run(JSON.parse(fs.readFileSync(mode, 'utf8')))).join('\n'));
    await b.close();
  }
})();
