// The second batch of reworked weapons (Oct 2026): each runs in the firing range (with and without extra
// projectiles and Barrage) without errors and deals damage; a screenshot of each.
// Usage: node armory-reworks2.js page.html outdir ["Name,Name" to check only those]
const { chromium } = require('playwright');
const names = ["Scattergun", "Flak Cannon", "Riot Repeater", "Kusarigama", "Bo Staff", "Blood Lash", "Prism Beam", "Sprite Staff", "Treant Rod", "Totem Staff",
  "Lightbringer", "Censer", "Medicine Darts", "Spirit Fan", "Ofuda", "Twin Rifle", "Railgun", "Burst SMG", "Arcane Missiles", "Wolf Whistle", "Wisp Lantern",
  "Imp Caller", "Twin Blades", "Greatsword", "Shuriken", "Kunai", "Blood Bolts", "Bone Spear"];
if (process.argv[4]) names.splice(0, names.length, ...process.argv[4].split(','));
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1000 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]); await p.click('nav.tabs a:has-text("Weapons")');
  const out = {};
  for (const n of names) {
    await p.locator('#weaponList .item').filter({ hasText: new RegExp('^' + n) }).first().click();
    for (const [x, f] of [[0, 0], [2, 2]]) {
      await p.locator('#wExtra button').nth(x).click(); await p.locator('#wFan button').nth(f).click();
      await p.waitForTimeout(n === "Imp Caller" ? 9000 : 3200);
      const r = await p.evaluate(() => document.getElementById('wReadout').innerText.replace(/\s+/g, ' '));
      out[n + (x ? " +2/+2" : "")] = r.match(/Measured damage \/ s ([\d.,]+)/)?.[1];
      if (!x) await (await p.$('#wCanvas')).screenshot({ path: `${process.argv[3]}/w-${n.replace(/ /g, '')}.png` });
    }
    await p.locator('#wExtra button').nth(0).click(); await p.locator('#wFan button').nth(0).click();
  }
  console.log(JSON.stringify({ errs, out }, null, 1)); await b.close();
})();
