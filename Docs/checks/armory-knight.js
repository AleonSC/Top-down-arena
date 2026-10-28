const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1100 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  await p.waitForTimeout(500);
  await p.locator('.cell').filter({ hasText: 'Knight DPS' }).first().click();
  await p.waitForTimeout(300);
  await p.screenshot({ path: process.argv[3] + '/arm-classes.png', fullPage: false });
  await p.click('text=Weapons');
  for (const w of ['Greatsword', 'Lance', 'Censer', 'Rapier']) { await p.locator('#weaponList .item').filter({ hasText: w }).first().click(); await p.waitForTimeout(1500); }
  await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] + '/arm-rapier.png' });
  await p.locator('#weaponList .item').filter({ hasText: 'Greatsword' }).first().click(); await p.waitForTimeout(1800);
  await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] + '/arm-greatsword.png' });
  const dpsText = await p.textContent('#wStats');
  await p.click('text=Abilities');
  for (const a of ['Shield Wall', 'Leap Slam', 'Whirlwind', 'Hallowed Blade', 'Blade Tempest', 'Lay on Hands']) { const it = p.locator('#abilityList .item').filter({ hasText: a }).first(); await it.click(); await p.waitForTimeout(400); }
  const mastery = await p.textContent('#aMastery');
  await p.click('text=Upgrades');
  const flurry = await p.locator('.ucard').filter({ hasText: 'Flurry' }).count();
  console.log(JSON.stringify({ errs, dps: dpsText.slice(0, 160), mastery: mastery.slice(0, 120), flurry }));
  await b.close();
})();
