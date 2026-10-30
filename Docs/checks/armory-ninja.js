const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1100 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  await p.waitForTimeout(500);
  await p.locator('.cell').filter({ hasText: 'Ninja DPS' }).first().click();
  await p.waitForTimeout(300);
  await p.screenshot({ path: process.argv[3] + '/arm-ninja-class.png', fullPage: false });
  await p.click('text=Weapons');
  for (const w of ['Kusarigama', 'Bo Staff', 'Medicine Darts', 'Spirit Fan', 'Ofuda', 'Katana', 'Kunai', 'Shuriken']) { await p.locator('#weaponList .item').filter({ hasText: w }).first().click(); await p.waitForTimeout(900); }
  await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] + '/arm-shuriken.png' });
  await p.locator('#weaponList .item').filter({ hasText: 'Fuma Shuriken' }).first().click(); await p.waitForTimeout(1300);
  await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] + '/arm-chainstar.png' });
  const dpsText = await p.textContent('#wStats');
  await p.click('text=Abilities');
  for (const a of ['Shadow Clone', 'Smoke Bomb', 'Iron Body', 'Healing Mist', 'Substitution', 'Spirit Ward', 'Shadow Step', 'Vanish', 'Blade Fan', 'Thousand Clones', 'Blossom Storm', 'Death Lotus']) { const it = p.locator('#abilityList .item').filter({ hasText: a }).first(); await it.click(); await p.waitForTimeout(400); }
  await p.locator('#abilityList .item').filter({ hasText: 'Thousand Clones' }).first().click(); await p.waitForTimeout(900);
  await p.screenshot({ path: process.argv[3] + '/arm-ninja-ult.png', fullPage: false });
  const mastery = await p.textContent('#aMastery');
  await p.click('text=Upgrades');
  const cards = await p.locator('.ucard').filter({ hasText: 'Storm of Steel' }).count();
  console.log(JSON.stringify({ errs, dps: dpsText.slice(0, 200), mastery: mastery.slice(0, 120), cards }));
  await b.close();
})();
