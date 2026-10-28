const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1300, height: 1000 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await p.goto('file://' + process.argv[2]);
  await p.click("text=Abilities").catch(()=>{});
  await p.click("#abilityList .item[data-id=\"Shockwave\"]");
  await p.click('#aMastery button[data-l="5"]');
  const t = await p.textContent('#aMastery');
  await (await p.$('#aMastery')).screenshot({ path: process.argv[3] });
  await p.click("#abilityList .item[data-id=\"Parry\"]");
  console.log(JSON.stringify({ t, parry: await p.textContent('#aMastery'), errs }));
  await b.close();
})();
