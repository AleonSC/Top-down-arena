const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1100 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]); await p.waitForTimeout(500);
  await p.click('text=Weapons');
  await p.locator('#weaponList .item').filter({ hasText: /^\s*Lance/ }).first().click();
  for (let i = 0; i < 6; i++) { await p.waitForTimeout(110); await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] + '/arm-lance' + i + '.png' }); }
  await p.waitForTimeout(3000);
  console.log(JSON.stringify({ errs, stats: (await p.textContent('#wStats')).slice(0, 200) }));
  await b.close();
})();
