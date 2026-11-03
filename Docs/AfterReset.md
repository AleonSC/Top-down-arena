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
- [x] **Redesign the main menu buttons.** Built: design D with the pen plot intro (Nov 2026).
      Mockups done, waiting for a pick: four layouts in [`Mockups/menu-redesign.html`](Mockups/menu-redesign.html)
      (https://claude.ai/artifact/6T2r1GZSBsajVK8pxaAo8s), and three plotted intros for the menu in
      [`Mockups/menu-intro.html`](Mockups/menu-intro.html) (https://claude.ai/artifact/TdwEQNxJ8STpVk2MRPqLuk).
      Owner likes **D (Big play)**: its intros and the Host game → lobby move are in
      [`Mockups/menu-d.html`](Mockups/menu-d.html) (https://claude.ai/artifact/VskdxmJhgTzmpeUnjjM2R5). With D the
      port moves to Settings (Network, 7777 by default) and the name is edited on the bottom-left chip.
- [ ] **Knockback visual effect.** Pushes are instant jumps today, which is disorienting: the Repulsion Cannon
      (`Repulsion.cs`, `e.Pos = ... + away * push`) and the shared `Knockback()` in `Mastery.cs` (Roar and the
      shockwave use it) move the enemy the whole distance in one frame. Make it a quick slide instead (a short knock
      velocity over ~0.15-0.25 s, like the players' `ApplyKnock`), with a visual: a motion streak or dust trail
      behind the enemy and a small impact puff. Check other instant pushes too (Scattergun point-blank shove).
- [ ] **Load-in animations for the How to Play and Field guide windows.** They should open with an animation (like
      the other panels' open transitions, e.g. `Docs/Mockups/tab-transitions.html`) instead of just appearing.

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
- Earned only by using that weapon: I = reach wave 10 with it · II = beat 3 different bosses with it · III = win a
  full run or 3 different hunts with it.
- Rewards are **twists, not raw power**; each tier is a variant you can switch on, the base stays available.
  Example (Twin Rifle): I = new tracer/muzzle look · II = streams cross closer, crossfire hits harder ·
  III = crossfire hits mark enemies for Mark for Death.
- Links between systems, kept few: Boss Hunts count toward Ascension III; at most **one unlock per class** comes from
  an ascension (e.g. Twin Rifle II unlocks the Missile Pod); perks stay separate (they come from bosses).
- Open: each weapon's actual ascension variants need designing (a design doc or mockups before building).

**Modifier unlocks** (same standard condition pieces, no new system)
- **Open from the start:** the Easier ones (Lucky, Head Start, Practiced, Second Wind) plus a couple of simple
  Harder ones (Tough Crowd, Swift Foes). Easier ones help struggling players, who have unlocked the least.
- **Unlocked by playing:** the rest of the Harder/Risky ones, each tied to something that fits its theme. Examples:
  - Nightfall: reach wave 20.
  - Death Throes: beat the Hive Queen.
  - Hunted: win a co-op run.
  - Zombie Infection: beat the Lich.
- In co-op the **host's** unlocks decide what's available.
- Locked modifiers show their condition in the Modifiers tab.
- **Badges:** a Records badge for each modifier you've won a run with, so the open ones also get tried.

**Sort modifiers by tier.** In the lobby's Modifiers tab, group the switches by their tag (Easier / Risky /
Harder), with a small heading per group, instead of the current order.

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

## Online play without Tailscale: relay with room codes (not urgent)
So friends can play without Tailscale or port forwarding.
- A small **relay** program on a cheap server (~$4-6/month, e.g. Hetzner or DigitalOcean) that just passes messages
  between the host and the joiners. The host's game still runs the match.
- In the menu: **Host online** gives a short room code (e.g. `KJ4P`); **Join online** takes the code. Nobody
  installs anything extra.
- Fits the current setup: networking is plain TCP (`Assets/Scripts/Core/Net.cs`, `Session.cs`) and the core game
  doesn't depend on Unity, so the relay can be plain C#. Test it like the rest of multiplayer (SimTest drives real
  sessions over localhost); add a relay check.
- Deliverables: the relay program, the two menu buttons and code entry, a version check through the relay, and
  step-by-step server setup instructions (rent, install, run as a service) in the README.
- Until then, a no-code stopgap: the host runs a free tunnel app like playit.gg and friends join its address + port.

## Bigger features (design docs ready)
- [ ] **Wave editor.** Mockups: `Docs/Mockups/wave-editor.html` (published: https://claude.ai/artifact/C7SkLmfmXQmqvEFbzJd7NV),
      options A map first, B timeline, C live test bench. Waiting for the owner's pick. Plan: in-game editor saves
      `.waves` text files; for final builds a script turns them into generated code (like the Bestiary data).
- [x] Zombie Infection modifier, basic version (hordes, cap, the dead rise). Next: zombie looks/types, infected
      co-op players: [`ZombieInfection.md`](ZombieInfection.md)
- [x] Boss Hunt for the Warden (10 waves, its own enemies). Next: factions + starting builds for later bosses, then Perks:
      [`BossHuntsAndPerks.md`](BossHuntsAndPerks.md)
