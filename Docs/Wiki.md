# In-game wiki

Status: step 1 built (Nov 24 2026): the registry, references and the main menu's WIKI screen. Steps 2-4 are proposals.

## What it is

Type anything you see in game and find out what it is, with every reference to it. Built from the game's own tables, so new
content joins with no extra work.

## How it works (step 1)

- **Entries** (`Core/Wiki.cs`): one per class, role, weapon, ability, Ultimate, upgrade card, perk, enemy and boss (about 500),
  gathered from `Classes.All`, `Defs.Roles`, `Defs.Weapons`, `Upgrades.Abilities`, `Upgrades.All`, `Perks.All` and `Defs.Enemies`.
  The text is the game's own (`Desc`, `Blurb`, `Line`, a Capstone as "Mastery 5"). Cards that grant an ability fold into the ability;
  unoffered cards and a boss's decoys are left out.
- **References**, shown from both sides:
  - data links: a weapon's or ability's class and role; a card's class, role, weapons, ability and boss; a perk's boss; a faction
    enemy's boss (`Approaches.Of`);
  - mentions: an entry's text naming another entry as a whole word (4+ letters, the longest name wins).
- **Search**: every word must match; whole name, then name start, word start, anywhere in the name, then the text.
- **Screen** (`Game/BayWiki.cs`): the WIKI key on the main menu's case rolls the shutter up onto WIKI_ (search box, results, a page
  with text and reference links, ‹ BACK, Esc). Preview: `WIKI=<query> [WIKIPAGE=<name>] Tools/Preview/shot.sh x menu`.
- **Enforcement**: suite `wiki` fails when an entry has no text (so a new enemy needs a `Blurb`), when a weapon, class, ability, perk
  or boss has no reference, or when an entry appears twice.

## Adding content

- Weapons, abilities, cards, perks, enemies, bosses: nothing extra; give it its description (the suite says if it's missing).
- To link things the text doesn't name, use the data fields (`UpgradeDef.Weapons`, `Boss`, `RequiresAbility`, a faction's members).
- A new kind of thing (statuses, props, game modes...): add a `WikiKind` at the end and a loop in `Wiki.Build`.

## Next steps (proposed)

2. **More kinds**: status effects, mechanics (Glossary terms: wound, mastery, expertise...), props, challenge rooms, game modes.
3. **A wiki key in runs and the lobby**: the same screen as an overlay (pauses solo, not in co-op; local only, no protocol change),
   and a WIKI row in the pause and lobby menus.
4. **Eyedropper**: hold the wiki key and click: in the world, match the click against enemies, players, minions and shots in the
   game state; in the UI, have `DevKit` widgets record (rect, entry name) each frame, then card names, the Tab screen and the shop.
