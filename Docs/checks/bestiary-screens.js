const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1000 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  const pick = async (label) => { await p.locator('#groups button, #groups .item').filter({ hasText: label }).first().click(); };
  await pick('The Singularity');
  const lockBtn = async (t) => p.locator('#lock button').filter({ hasText: t }).first().click();
  const shots = [['Normal', 4200, 'sing-lasers'], ['Cage', 3500, 'sing-cage'], ['Cycle', 2500, 'sing-cycle']];
  for (const [l, ms, name] of shots) { await lockBtn(l); await p.waitForTimeout(ms); await (await p.$('#arena')).screenshot({ path: process.argv[3] + '/' + name + '.png' }); }
  await pick('Cage Well'); await p.waitForTimeout(2500); await (await p.$('#arena')).screenshot({ path: process.argv[3] + '/cagewell.png' });
  const info = await p.evaluate(() => document.body.innerText.match(/Cage Well[\s\S]{0,300}/)?.[0]);
  await pick('Gravity Well'); await p.waitForTimeout(1500);
  console.log(JSON.stringify({ errs, info: info && info.slice(0, 200) }));
  await b.close();
})();
