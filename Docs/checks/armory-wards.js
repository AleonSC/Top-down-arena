// Spirit Ward, Healing Mist and Arcane Ward: each ability demo runs without errors; a screenshot of each a few
// seconds after the cast. Usage: node armory-wards.js page.html outdir
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1000 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]); await p.click('nav.tabs a:has-text("Abilities")');
  const out = {};
  for (const n of ["Spirit Ward", "Healing Mist", "Arcane Ward"]) {
    await p.locator('#abilityList .item').filter({ hasText: new RegExp('^' + n) }).first().click();
    await p.waitForTimeout(2600);
    out[n] = await p.evaluate(() => document.getElementById('aReadout').innerText.replace(/\s+/g, ' '));
    await (await p.$('#aCanvas')).screenshot({ path: `${process.argv[3]}/a-${n.replace(/ /g, '')}.png` });
  }
  console.log(JSON.stringify({ errs, out }, null, 1)); await b.close();
})();
