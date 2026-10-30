// The Storm Herald's entry: each stage locked in turn (screenshot and status line), then the cycle.
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1000 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  await p.locator('#groups button, #groups .item').filter({ hasText: 'Storm Herald' }).first().click();
  const lockBtn = async (t) => p.locator('#lock button').filter({ hasText: t }).first().click();
  const status = [];
  for (const [l, ms, name] of [['Gathering Storm', 9500, 'herald-gathering'], ['Gale', 10500, 'herald-gale'], ['Supercell', 16500, 'herald-supercell'], ['Last stand', 4000, 'herald-last'], ['Cycle', 2000, 'herald-cycle']]) {
    await lockBtn(l); await p.waitForTimeout(ms);
    await (await p.$('#arena')).screenshot({ path: process.argv[3] + '/' + name + '.png' });
    status.push(await p.evaluate(() => document.body.innerText.match(/(Gathering Storm|Gale|Supercell|Last stand|Changing)[^\n]*/)?.[0]));
  }
  console.log(JSON.stringify({ errs, status }));
  await b.close();
})();
