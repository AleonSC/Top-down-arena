// The three shooting-pattern enemies from Variety.cs: a screenshot of each in action; reports page errors.
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 1000 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  for (const [name, ms] of [['Pauser', 4200], ['Leader', 3300], ['Stitcher', 7000]]) {
    await p.locator('#groups button, #groups .item').filter({ hasText: name }).first().click();
    await p.waitForTimeout(ms);
    await (await p.$('#arena')).screenshot({ path: process.argv[3] + '/variety-' + name.replace(' ', '').toLowerCase() + '.png' });
  }
  console.log(JSON.stringify({ errs }));
  await b.close();
})();
