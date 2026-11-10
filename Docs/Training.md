# Training drills (Practice Range, second section)

Status: **groundwork built** (Nov 2026): the Training tab, the drill library, the text format and Load this drill. No real
drills yet beyond one placeholder (`dodge-basics`, marked EXAMPLE). Look: look A of
[`Mockups/crt-practice-redesign.html`](Mockups/crt-practice-redesign.html) (https://claude.ai/artifact/23YzJJzNS4J5eVNjkadkku).

## What it is

The Practice mode in the lobby has two tabs: **Practice** (build your own waves) and **Training** (small drills that each teach
one mechanic: dodging, a weapon, an ability, an enemy, reviving). The same rules apply to both: no XP, Respawn on
(`Docs`: see README "Practice Range").

## How a drill works today

A drill is a `PracticePlan` (waves and enemies) with a name, a group, a goal and a few steps. **Load this drill** (the host's key)
copies its plan into the lobby's Practice setup, exactly as if the host had built it, so there is no new network message;
Ready and Start play it. Nothing in the run checks the steps yet: they are text for the player.

## Adding a drill

Either drop a `.training` file in the `training` folder (next to the `practice` folder; the Training tab's **Folder** key opens
it and **Reload** re-reads it), or add one to `TrainingLibrary` (`Core/Training.cs`). The file:

```
# comments
name: Dodge Basics
group: Move & Dodge        (Move & Dodge, Weapons, Abilities, Enemies or Co-op; anything else lands in Co-op)
goal: Slip through three bullet patterns.
step: Clear three patterns without a hit      (as many step lines as you like)
example: yes               (optional: marks the card EXAMPLE)
loop: yes
wave 1: Burster x2, Spinner x1
```

After the header lines it is the Practice setup format (README "Practice tab"), so a saved setup becomes a drill by adding
`name:` and `group:`.

## Ideas for later (not built)

- Drills that set a fixed loadout, an arena layout or a scripted spawn, and check their steps (medals, "done" marks).
- Drills that start from the Training tab directly instead of through the Practice setup.
- More than four slots a group (paging); today extra drills in a group are not shown.
