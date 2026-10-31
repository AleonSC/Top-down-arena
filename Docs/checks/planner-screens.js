// Opens the Build Planner, changes the loadout and stacks some cards, and takes screenshots.
// node Docs/checks/planner-screens.js "$PWD/Docs/Planner/out/build-planner.html" /tmp/shots
const { chromium } = require("playwright");
const [, , page_, out = "."] = process.argv;
(async () => {
  const b = await chromium.launch(), errs = [];
  for (const [name, vp] of [["desk", { width: 1440, height: 1000 }], ["phone", { width: 400, height: 860 }]]) {
    const p = await b.newPage({ viewport: vp });
    p.on("pageerror", e => errs.push(String(e)));
    p.on("console", m => m.type() === "error" && errs.push(m.text()));
    await p.goto("file://" + page_);
    await p.waitForTimeout(400);
    await p.screenshot({ path: `${out}/planner-${name}-sample.png`, fullPage: name === "desk" });
    // A Knight Tank with Shield Wall: melee card text, a Requires card, a class ability card.
    await p.click('#kit button[data-kit="3"]');
    await p.click('#role button[data-role="0"]');
    await p.click('[data-plus="c1"]');          // Leap Slam on Q
    await p.click('[data-filter="Rare"]');
    for (let i = 0; i < 4; i++) await p.click(`[data-plus]:not([disabled]) >> nth=${i}`);  // the page re-renders after each click
    const hash = await p.evaluate(() => location.hash);
    await p.screenshot({ path: `${out}/planner-${name}-knight.png`, fullPage: name === "desk" });
    // The hash brings the same build back.
    await p.reload(); await p.waitForTimeout(300);
    const again = await p.evaluate(() => location.hash);
    if (again !== hash) errs.push(`${name}: hash ${hash} came back as ${again}`);
    const wide = await p.evaluate(() => document.documentElement.scrollWidth > innerWidth);
    if (wide) errs.push(name + ": page scrolls sideways");
    console.log(name, hash);
    await p.close();
  }
  console.log(JSON.stringify({ errs }));
  await b.close();
})();
