# Docs

Reference pages published as claude.ai artifacts. Their sources live here so future sessions can update them.

| Page | Source | Published at |
|---|---|---|
| **Bestiary**: every enemy and boss, animated, with what they do | `Bestiary/arena-bestiary.html` (one self-contained page; edit it directly) | https://claude.ai/artifact/SxsKDQBSXHmNN2QTM3f664 |
| **Armory**: every class, weapon (with live demos), ability, ultimate, mastery capstone and upgrade | `Armory/armory.src.html` + icons built from the game | https://claude.ai/artifact/Vx6Rkk2y7TyRd47ADoSnKz |

## The in-game Field guide

The pause menu's Field guide (`Assets/Scripts/Game/FieldGuide.cs`) is the in-game version of both pages. Its
Armory reads the game's definitions directly. Its Bestiary's words (appears, description, stages, attacks,
tips) come from this page's `E` table, turned into C# by

```
node Docs/Bestiary/build_data.js
```

which writes `Assets/Scripts/Core/BestiaryData.Generated.cs` (don't edit that by hand). Run it after editing a
Bestiary entry. It drops the page's remarks about its own preview and lists any text that still seems to
talk about the page. Boss adds the page doesn't cover are in `Assets/Scripts/Core/BestiaryData.cs`; the
`fieldguide` sim test checks every `EnemyType` has an entry.

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
