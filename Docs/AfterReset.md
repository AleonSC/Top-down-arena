# To do after the usage reset

A running list of things to pick up next session. Tick off / delete as they're done.

## Bugs / polish
- [ ] **Pause menu: models show up before the rest of the menu.** When you press pause, the player and enemy
      models (e.g. the next boss's model where the pause menu shows the next boss) appear first, ahead of the
      rest of the menu. They should come in with the panel (same fade/slide timing), not pop in early.
      Check how the live models are drawn in the pause menu vs. the panel's open animation.

## Balance / design
- [ ] **Defend the Crystal should use mostly melee enemies.** Ranged enemies make the challenge about shooting the
      crystal from afar; it should be about bodies rushing it that you have to intercept. Change the room's enemy
      mix (`Assets/Scripts/Core/Challenges.cs`) to mostly melee (Swarmer, Brute, Lunger) with a few ranged at most,
      and update the `challenges` SimTest check if it depends on the mix.

## Visuals
- [ ] **Mockups for melee attack animations.** HTML mockups (like the others in `Docs/Mockups/`) of how melee
      weapons' swings, thrusts and slams could look, to pick from before changing the game.
- [ ] **Redesign the main menu buttons.** Mock up a few styles first, then pick one to build.

## Unlocks & ascensions plan
Two layers that don't compete: **unlocks give new items, ascensions deepen the ones you have.** Both use the unused
unlock system in `Profile.cs` (`Unlocks.All`) and the stats Records already keep.

**Unlock rules (weapons and abilities)**
- Earned by playing that class, ideally hinting at what the item is good at. Takes 2-5 runs, not a grind.
- Can't need the item itself. Per player in co-op (like Records). Today's weapons/abilities stay unlocked.
- Built from a few **standard condition pieces**, themed per item: play class/role · reach wave N · beat boss X ·
  win hunt X (later) · with item Y equipped · simple totals. Build the pieces once; each new unlock is just data.
- Truly unique conditions only for a few showcase/secret unlocks.
- Locked items show their condition and progress in the lobby and Records.
- First one: **Missile Pod**, a 4th DPS Soldier weapon. Hold fire and sweep to lock up to 4 targets (~0.3 s each),
  release to fire homing missiles with small blasts; a tap with no locks fires one unguided rocket. Upgrade: +1 lock
  or bomblets. Weak up close. Unlock: **beat the Hive Queen as a DPS Soldier**. Mockup first.

**Ascensions (per weapon, 3 tiers; the lobby strip is already there as a placeholder, `LobbyUi.DrawAscensionStrip`)**
- Earned only by using that weapon:
  I = reach wave 10 with it · II = beat 3 different bosses with it · III = win a full run or 3 different hunts with it.
- Rewards are **twists, not raw power**; each tier is a variant you can switch on, the base stays available.
  Example (Twin Rifle): I = new tracer/muzzle look · II = streams cross closer, crossfire hits harder ·
  III = crossfire hits mark enemies for Mark for Death.
- Links between systems, kept few: Boss Hunts count toward Ascension III; at most **one unlock per class** comes from
  an ascension (e.g. Twin Rifle II unlocks the Missile Pod); perks stay separate (they come from bosses).
- Open: each weapon's actual ascension variants need designing (a design doc or mockups before building).

## Difficulty with twists
Today the presets only change numbers (README "Difficulty"), so Nightmare plays like Normal, just harder. Keep the
numbers, but each step up also **adds named rules**, shown on the difficulty card.

- **Hard** adds:
  - **Smarter enemies:** snipers lead their shots, chargers fake out, melee enemies flank instead of beelining.
  - **Elite affixes:** each elite gets one trait (shielded, splits on death, leaves a hazard trail...).
- **Nightmare** adds Hard's twists, plus:
  - **Boss upgrades:** every boss gets one extra move per stage (e.g. the Warden's searchlight splits in the last
    stand, the Hive Queen's eggs hatch faster). Most work (10 bosses), most memorable.
  - Longer last stands; health orbs fade after a few seconds.
- **Ladder past Nightmare** (name it **Threat** or **Heat**, not "ascension", which is for weapons): beat a Nightmare
  run to unlock 10 rungs, each adding one stacking rule (enemies drop a hazard on death · slower revives and the
  reviver can't shoot · mimic chests · a forced Harder modifier · bosses get an extra stage · ...). Each rung won is
  a record.
- **Ties in:**
  - "Win on Hard" / "win at Threat N" become standard unlock pieces.
  - A hunt's "Hard" (perk #2) uses these twists.
  - The twists become switches in the Custom panel.
- **Build first:** the Hard twists (smarter AI + elite affixes). They're in every wave, so they change the feel
  fastest, and need no per-boss work.

## Bigger features (design docs ready)
- [ ] Zombie Infection modifier, basic version first: [`ZombieInfection.md`](ZombieInfection.md)
- [ ] Boss Hunts, then Perks: [`BossHuntsAndPerks.md`](BossHuntsAndPerks.md)
