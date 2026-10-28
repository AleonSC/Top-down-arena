# Docs

Reference pages published as claude.ai artifacts. Their sources live here so future sessions can update them.

| Page | Source | Published at |
|---|---|---|
| **Bestiary**: every enemy and boss, animated, with what they do | `Bestiary/arena-bestiary.html` (one self-contained page; edit it directly) | https://claude.ai/artifact/SxsKDQBSXHmNN2QTM3f664 |
| **Armory**: every class, weapon (with live demos), ability, ultimate, mastery capstone and upgrade | `Armory/armory.src.html` + icons built from the game | https://claude.ai/artifact/Vx6Rkk2y7TyRd47ADoSnKz |

## Updating the Armory

```
Docs/Armory/build.sh
```

This runs `build_icons.py`, which reads the pixel-art icons, palette and upgrade-to-icon map straight from
`Assets/Scripts/Game/PixelArt.cs` into `icons.json`, then injects them into `armory.src.html` (at the
`/*ICONS*/null` placeholder) and writes `Docs/Armory/out/arena-armory.html`. Publish that file over the
artifact URL above (Artifact tool: publish with that `url`, after reading the live version).

The weapon demos are small simulations inside the page (`s.swing(...)`, `s.bullet(...)` per weapon in the `W`
table): their numbers are **copied by hand** from the game code, as are the descriptions. When a weapon,
ability, upgrade or enemy changes, update the matching entry here and in the Bestiary.

## Checking the pages

`checks/` holds Playwright scripts that open a page, click through it and take screenshots, e.g.

```
node Docs/checks/armory-lance.js "$PWD/Docs/Armory/out/arena-armory.html" /tmp/shots
node Docs/checks/bestiary-screens.js "$PWD/Docs/Bestiary/arena-bestiary.html" /tmp/shots
```

They print any page errors as JSON; an empty `errs` list means the page ran cleanly. (In the cloud container
Playwright and Chromium are pre-installed; run from a directory where `require('playwright')` resolves.)
