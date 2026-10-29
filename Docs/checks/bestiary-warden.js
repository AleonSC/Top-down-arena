// The Warden's preview: every stage runs (Cycle steps through them all), and screenshots of each one.
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1000 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  await p.locator('#groups button, #groups .item').filter({ hasText: 'The Warden' }).first().click();
  const lockBtn = async (t) => p.locator('#lock button').filter({ hasText: t }).first().click();
  // Cycle: run the sim fast and note every stage and turn it passes through.
  const seen = await p.evaluate(() => { const { S, step, restart, WD } = window.bestiaryDebug, D = window.bestiaryDebug;
    S.playing = false; restart();
    const out = []; let last = "";
    for (let f = 0; f < 60 * 160; f++) {
      step(1 / 60);
      const e = D.world.enemies.find(o => o.type === "Warden"), w = e.w;
      const k = w.trans > 0 ? "→" + WD.stages[w.to] : WD.stages[w.stage] + ":" + w.turn;
      if (k !== last) { out.push(k); last = k; }
    }
    S.playing = true;
    return out;
  });
  const shots = [['Count', 7600, 'warden-count'], ['Lockdown', 6000, 'warden-lockdown'], ['Riot', 5000, 'warden-riot'], ['Last stand', 4000, 'warden-last']];
  for (const [l, ms, name] of shots) { await lockBtn(l); await p.waitForTimeout(ms); await (await p.$('#arena')).screenshot({ path: process.argv[3] + '/' + name + '.png' }); }
  // The searchlight, by fast-forwarding the Count to its light turn.
  await lockBtn('Count');
  await p.evaluate(() => { for (let f = 0; f < 60 * 21; f++) window.bestiaryDebug.step(1 / 60); });
  await p.waitForTimeout(1500); await (await p.$('#arena')).screenshot({ path: process.argv[3] + '/warden-light.png' });
  // The riot's guards: fast-forward past 10 s.
  await lockBtn('Riot');
  await p.evaluate(() => { for (let f = 0; f < 60 * 11; f++) window.bestiaryDebug.step(1 / 60); });
  await p.waitForTimeout(500); await (await p.$('#arena')).screenshot({ path: process.argv[3] + '/warden-guards.png' });
  const status = await p.evaluate(() => window.bestiaryDebug.statusLine());
  console.log(JSON.stringify({ errs, seen, status }));
  await b.close();
})();
