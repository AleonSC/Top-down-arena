const { chromium } = require('playwright');
(async () => { const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1000 } }); const errs = []; p.on('pageerror', e => errs.push(e.message));
  await p.goto('file://' + process.argv[2]); await p.click('text=Weapons');
  await p.locator('#weaponList .item').filter({ hasText: "Mender's Bow" }).first().click(); await p.waitForTimeout(2600);
  await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] }); console.log(JSON.stringify(errs)); await b.close(); })();
