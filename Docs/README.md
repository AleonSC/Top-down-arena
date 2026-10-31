# Docs

Reference pages published as claude.ai artifacts. Their sources live here so future sessions can update them.

| Page | Source | Published at |
|---|---|---|
| **Bestiary**: every enemy and boss, animated, with what they do | `Bestiary/arena-bestiary.html` (one self-contained page; edit it directly) | https://claude.ai/artifact/SxsKDQBSXHmNN2QTM3f664 |
| **Armory**: every class, weapon (with live demos), ability, ultimate, mastery capstone and upgrade | `Armory/armory.src.html` + icons built from the game | https://claude.ai/artifact/Vx6Rkk2y7TyRd47ADoSnKz |
| **Wave Almanac**: every wave of a run (enemy mix, counts, health, bullet budget, squads, elites, boss health) by party size and modifiers, with a sample roll of any wave | `Almanac/almanac.src.html` + data read from the game | https://claude.ai/artifact/Y26659PNCbnRfeiT7V4c1c |
| **Build Planner**: pick a loadout, stack upgrade cards, see the stat sheet (health, damage, fire rate, cooldowns, charges); the page's address keeps the build | `Planner/planner.src.html` + data and icons read from the game | https://claude.ai/artifact/4444egDjCCmMeAar9Jr1Lz |

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

## Updating the Wave Almanac and the Build Planner

```
Docs/Data/build.sh
```

Unlike the Armory, these two pages copy almost nothing by hand. `Data/extract.py` reads the numbers straight from the
C# code into `Data/game-data.json`: roles and class multipliers, weapons, enemies (`Defs.cs`), every `AbilityDef`,
the upgrade cards and what each does to the stat sheet (`Upgrades.cs`), the boss cycle, wave enemy pools and boss
escorts (`EnemyAI.cs`), squads and elite traits (`Variety.cs`), bosses' health multipliers (`Balance.cs`). The build
injects it (and the Armory's `icons.json`) into each page at the `/*DATA*/null` and `/*ICONS*/null` placeholders and
writes `Almanac/out/wave-almanac.html` and `Planner/out/build-planner.html`. Publish those over the URLs above.

A few formulas are code rather than data (wave size, health scaling, the bullet budget, elite chance, fire rate,
spawn gap, mastery cooldowns), so the pages' JavaScript repeats them. `extract.py` keeps a list of the exact lines
they mirror (`FORMULAS`) and **fails the build if one changes**: update the page's formula and the list together.
The planner models the stat sheet the way the game's `StatSheet.cs` does; it doesn't simulate weapons, so its
"Output" compares builds of the same weapon, not different weapons.

## Checking the pages

`checks/` holds Playwright scripts that open a page, click through it and take screenshots, e.g.

```
node Docs/checks/armory-lance.js "$PWD/Docs/Armory/out/arena-armory.html" /tmp/shots
node Docs/checks/bestiary-screens.js "$PWD/Docs/Bestiary/arena-bestiary.html" /tmp/shots
node Docs/checks/almanac-screens.js "$PWD/Docs/Almanac/out/wave-almanac.html" /tmp/shots
node Docs/checks/planner-screens.js "$PWD/Docs/Planner/out/build-planner.html" /tmp/shots
```

They print any page errors as JSON; an empty `errs` list means the page ran cleanly. (In the cloud container
Playwright and Chromium are pre-installed; run from a directory where `require('playwright')` resolves.)
