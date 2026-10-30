// The Singularity's preview: Cycle steps through every stage and turn without errors; screenshots of each stage.
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1000 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  await p.locator('#groups button, #groups .item').filter({ hasText: 'The Singularity' }).first().click();
  const seen = await p.evaluate(() => {
    const D = window.bestiaryDebug; D.S.playing = false; D.restart();
    const out = []; let last = "";
    for (let f = 0; f < 60 * 120; f++) {
      D.step(1 / 60);
      const e = D.world.enemies.find(o => o.type === "Singularity"); if (!e || !e.g) continue;
      const k = e.g.trans > 0 ? "→" + e.g.to : e.g.stage + ":" + e.g.turn;
      if (k !== last) { out.push(k); last = k; }
    }
    D.S.playing = true; return out;
  });
  const lockBtn = async (t) => p.locator('#lock button').filter({ hasText: t }).first().click();
  const shots = [['Event Horizon', 9500, 'sing-1'], ['Accretion', 13000, 'sing-2'], ['Spaghettify', 9000, 'sing-3'], ['Last stand', 3500, 'sing-4']];
  for (const [l, ms, name] of shots) { await lockBtn(l); await p.waitForTimeout(ms); await (await p.$('#arena')).screenshot({ path: process.argv[3] + '/' + name + '.png' }); }
  for (const n of ['Gravity Well', 'Cage Well']) { await p.locator('#groups button, #groups .item').filter({ hasText: n }).first().click(); await p.waitForTimeout(1500); }
  console.log(JSON.stringify({ errs, seen }));
  await b.close();
})();
