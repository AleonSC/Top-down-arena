// Opens the Wave Almanac, clicks through a few waves and settings, and takes screenshots.
// node Docs/checks/almanac-screens.js "$PWD/Docs/Almanac/out/wave-almanac.html" /tmp/shots
const { chromium } = require("playwright");
const [, , page_, out = "."] = process.argv;
(async () => {
  const b = await chromium.launch(), errs = [];
  for (const [name, vp] of [["desk", { width: 1360, height: 900 }], ["phone", { width: 400, height: 860 }]]) {
    const p = await b.newPage({ viewport: vp });
    p.on("pageerror", e => errs.push(String(e)));
    p.on("console", m => m.type() === "error" && errs.push(m.text()));
    await p.goto("file://" + page_);
    await p.waitForTimeout(400);
    await p.screenshot({ path: `${out}/almanac-${name}-w1.png`, fullPage: true });
    await p.click('.cell[data-w="27"]');
    await p.click('#party button[data-n="3"]');
    await p.screenshot({ path: `${out}/almanac-${name}-w27.png`, fullPage: name === "desk" });
    await p.click('.cell[data-w="15"]');
    await p.screenshot({ path: `${out}/almanac-${name}-w15.png` });
    const wide = await p.evaluate(() => document.documentElement.scrollWidth > innerWidth);
    if (wide) errs.push(name + ": page scrolls sideways");
    await p.close();
  }
  console.log(JSON.stringify({ errs }));
  await b.close();
})();
