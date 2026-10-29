const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1100 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  await p.waitForTimeout(500);
  await p.locator('.cell').filter({ hasText: 'Hemomancer DPS' }).first().click();
  await p.waitForTimeout(300);
  await p.screenshot({ path: process.argv[3] + '/arm-blood-class.png', fullPage: false });
  await p.click('text=Weapons');
  for (const w of ['Blood Lash', 'Leech Tendrils', 'Transfusion Darts', 'Sanguine Orb', 'Vein Tether', 'Blood Bolts', 'Implosion', 'Bone Spear']) { await p.locator('#weaponList .item').filter({ hasText: w }).first().click(); await p.waitForTimeout(900); }
  await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] + '/arm-bonespear.png' });
  await p.locator('#weaponList .item').filter({ hasText: 'Clot Bolt' }).first().click(); await p.waitForTimeout(1300);
  await (await p.$('#wCanvas')).screenshot({ path: process.argv[3] + '/arm-clot.png' });
  const dpsText = await p.textContent('#wStats');
  await p.click('text=Abilities');
  for (const a of ['Blood Shell', 'Crimson Pact', 'Exsanguinate', 'Blood Gift', 'Siphon Field', 'Martyr', 'Blood Boil', 'Frenzy Pact', 'Hemo Nova', 'Blood Rite', 'Sanguine Rain', 'Crimson Apotheosis']) { const it = p.locator('#abilityList .item').filter({ hasText: a }).first(); await it.click(); await p.waitForTimeout(400); }
  await p.locator('#abilityList .item').filter({ hasText: 'Siphon Field' }).first().click(); await p.waitForTimeout(900);
  await p.screenshot({ path: process.argv[3] + '/arm-blood-ult.png', fullPage: false });
  const mastery = await p.textContent('#aMastery');
  await p.click('text=Upgrades');
  const cards = await p.locator('.ucard').filter({ hasText: 'Crimson Tide' }).count();
  console.log(JSON.stringify({ errs, dps: dpsText.slice(0, 200), mastery: mastery.slice(0, 120), cards }));
  await b.close();
})();
