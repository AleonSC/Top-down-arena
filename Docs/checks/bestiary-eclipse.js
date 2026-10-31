// The Eclipse's entry: each stage locked in turn (a screenshot of each), then the cycle; reports page errors.
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1000 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  await p.locator('#groups button, #groups .item').filter({ hasText: 'The Eclipse' }).first().click();
  const lockBtn = async (t) => p.locator('#lock button').filter({ hasText: new RegExp('^' + t + '$') }).first().click();
  for (const [l, ms, name] of [['Corona', 8500, 'eclipse-corona'], ['Penumbra', 11000, 'eclipse-penumbra'], ['Umbra', 9000, 'eclipse-umbra'], ['Echoes', 10000, 'eclipse-echoes'],
    ['Last Light', 16000, 'eclipse-lastlight'], ['Totality', 5000, 'eclipse-totality'], ['Cycle', 2000, 'eclipse-cycle']]) {
    await lockBtn(l); await p.waitForTimeout(ms);
    await (await p.$('#arena')).screenshot({ path: process.argv[3] + '/' + name + '.png' });
  }
  console.log(JSON.stringify({ errs }));
  await b.close();
})();
