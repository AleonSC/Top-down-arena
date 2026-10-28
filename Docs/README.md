# Docs

Reference pages published as claude.ai artifacts. The sources live here so they can be updated.

- **Bestiary** (`Bestiary/arena-bestiary.html`): every enemy and boss with animated previews.
  Published at https://claude.ai/artifact/SxsKDQBSXHmNN2QTM3f664
- **Armory** (`Armory/armory.src.html` + `Armory/icons.json`): every class, weapon, ability, ultimate and
  upgrade, with live weapon demos. Build the publishable page by injecting the icons:

  ```
  python3 -c "import json; s=open('armory.src.html').read(); open('arena-armory.html','w').write(s.replace('/*ICONS*/null', json.dumps(json.load(open('icons.json'))), 1))"
  ```

  Published at https://claude.ai/artifact/Vx6Rkk2y7TyRd47ADoSnKz.
  `screenshot-lance.js` is an example Playwright script for checking a weapon demo.

The numbers in these pages are copied from the game code by hand: when weapons, abilities or enemies change,
update the pages too.
