// Leech Tendrils' demo: the tendrils grab the three nearest dummies in the arc and drain them.
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1100 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  await p.waitForTimeout(500);
  await p.click('text=Weapons');
  await p.locator('#weaponList .item').filter({ hasText: 'Leech Tendrils' }).first().click();
  await p.waitForTimeout(1500);
  await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] + '/arm-leech.png' });
  const stats = await p.textContent('#wStats');
  console.log(JSON.stringify({ errs, stats: stats.slice(0, 220) }));
  await b.close();
})();
