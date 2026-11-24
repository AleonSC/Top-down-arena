# In-game wiki

Status: built (Nov 24 2026): steps 1-4: the registry and references, more kinds, the WIKI screen on the main menu and everywhere
else (Wiki key, pause and lobby rows), and the eyedropper.

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

2. **Built: more kinds** (about 650 entries): minions (text from their numbers in `Kits.Minions`, linked to the weapon that keeps
   them), gadgets (`Game.GadgetDesc`, linked to the gadget chest), relics (a faction relic links to its boss), Black Market
   buildables, run modifiers, paths, game modes, difficulty presets, challenge rooms, arena layouts, and mechanics
   (`Core/WikiTopics.cs`, the Glossary's words; mentioned in any case, so "a wound" in any text links to Wound). The eyedropper
   also picks minions.
   Then: **status effects** (`WikiTopics.Statuses`: Slowed, Burning, Bleeding, Chilled, Frozen, Wet, Stunned, Dazed, Caged, Marked,
   Poisoned, Venom, Suppressed; mentioned in any case, so they link to what causes them) and **props and chests**
   (`WikiTopics.Things`, by PropKind and ChestKind; `Wiki.Of(PropKind, ChestKind)`).
   Then: **player status effects** (`WikiTopics.PlayerStatuses`: Swallowed, Grabbed, Taunting, linked to their causes; Slowed,
   Bleeding and Stunned say what they do on you) and **boss phases** (`Core/WikiPhases.cs`: every boss's phases with their health
   range, linked to the boss, plus one shared Last stand). Then **boss attacks** (`Core/WikiAttacks.cs`: about 70, each linked to its
   phase and boss; an attack that is an enemy already, like Ball Lightning or Hive Egg, stays an enemy). About 785 entries.
3. **Built: the Wiki key everywhere** (`Game/WikiPick.cs`): `KeyAction.Wiki` (I). A tap opens the wiki on the main menu's bay, or
   anywhere else as a modal screen (`screenWiki`: dimmed room, a CRT panel with the same screen; Esc or a click outside closes it).
   Solo runs pause while it's open (`wikiPaused`, resumed on close); co-op doesn't pause and nothing is sent (no protocol change).
   The pause and lobby menus have a WIKI row.
4. **Built: the eyedropper**: hold the Wiki key. Text uses the dev text editor's picking (`Ui.PickText`: every label goes through
   `Ui.Shown`, the smallest one under the pointer is outlined in gold); `Wiki.FindIn` turns it into an entry (the whole text as a name,
   else the longest entry name in it). In a run, the enemy (`Wiki.Of(EnemyType)`, a decoy shows its boss) or player (their class)
   under the pointer counts too. A tag by the pointer names what a click opens; the click is used up, so it never presses a button
   or fires. In a run it also picks minions, shots (a player's opens their weapon; an enemy's its shooter, on the host) and props
   and chests (the pointer inside the box), and the tag lists the status effects on the enemy under it (its replicated flags:
   `WikiStatusesOn`), or on the player under it (stunned, swallowed, taunting, downed, wounded); a right click opens the first.
   A phase or attack name on a boss bar opens that boss's phase or attack (`Wiki.PhaseIn`: "LAST STAND" under the Warden is the boss phase, not the
   Knight's Ultimate).
