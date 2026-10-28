// Vein Tether's beam demo: aimed at a dummy, then with the aim sweeping (it should only hurt what it crosses).
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1100 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  await p.waitForTimeout(500);
  await p.click('text=Weapons');
  await p.locator('#weaponList .item').filter({ hasText: 'Vein Tether' }).first().click();
  await p.waitForTimeout(1500);
  await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] + '/arm-vein.png' });
  const aimed = await p.textContent('#wStats');
  await p.check('#wSweep');
  await p.waitForTimeout(1700);
  await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] + '/arm-vein-sweep.png' });
  console.log(JSON.stringify({ errs, stats: aimed.slice(0, 160) }));
  await b.close();
})();
