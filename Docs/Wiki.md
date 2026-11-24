# In-game wiki

Status: steps 1, 3 and 4 built (Nov 24 2026): the registry and references, the WIKI screen on the main menu and everywhere else
(Wiki key, pause and lobby rows), and the eyedropper. Step 2 is a proposal.

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

## Steps

2. **More kinds**: status effects, mechanics (Glossary terms: wound, mastery, expertise...), props, challenge rooms, game modes.
3. **Built: the Wiki key everywhere** (`Game/WikiPick.cs`): `KeyAction.Wiki` (I). A tap opens the wiki on the main menu's bay, or
   anywhere else as a modal screen (`screenWiki`: dimmed room, a CRT panel with the same screen; Esc or a click outside closes it).
   Solo runs pause while it's open (`wikiPaused`, resumed on close); co-op doesn't pause and nothing is sent (no protocol change).
   The pause and lobby menus have a WIKI row.
4. **Built: the eyedropper**: hold the Wiki key. Text uses the dev text editor's picking (`Ui.PickText`: every label goes through
   `Ui.Shown`, the smallest one under the pointer is outlined in gold); `Wiki.FindIn` turns it into an entry (the whole text as a name,
   else the longest entry name in it). In a run, the enemy (`Wiki.Of(EnemyType)`, a decoy shows its boss) or player (their class)
   under the pointer counts too. A tag by the pointer names what a click opens; the click is used up, so it never presses a button
   or fires. Later: minions, shots, chests and props (they need entries first).
