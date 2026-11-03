# To do after the usage reset

A running list of things to pick up next session. Tick off / delete as they're done.

## Bugs / polish
- [x] **Pause menu: models show up before the rest of the menu.** Done (Nov 2026): models fade with their panel.

## Balance / design
- [x] **Defend the Crystal should use mostly melee enemies.** Done (Nov 2026): melee, a gun 15% of the time.
- [ ] **Healer Summoner left-click attacks.** Picks (Nov 2026), mockup [`Mockups/healer-summoner-attacks.html`](Mockups/healer-summoner-attacks.html)
      (https://claude.ai/artifact/V7omfhCLTvzB8i3riqqfA8):
      - [x] **Sprite Staff: Firefly Hop.** Built (protocol 130): a firefly seeds what it hits, then hops (up to 2, 300 px) to the
            nearest enemy that isn't seeded. 7 damage every 0.5 s: ~14 a second on one enemy, ~42 across three.
      - [x] **Treant Rod: Root Lash**, and the tether to the Treant is now a root too. Built: 250 px line, 15 damage every 0.55 s
            (~27 a second), heals allies it touches 10. Homing Rounds widen it. Code names (`Vine*`, `TickVines`) are unchanged.
      - [x] **Totem Staff replaced by Spirit Chalk**, and **Prism Beam replaced by Nimbus** (Healer Elementalist). Built (Nov 2026, protocol 131)
            from [`Mockups/totem-staff-rework.html`](Mockups/totem-staff-rework.html) (https://claude.ai/artifact/3cp8ynYASdq6fySbRoaeJZ).
            Spirit Chalk also traps: **draw a loop round non-boss enemies and they can't leave it for 3.5 s.** The old weapons'
            code that isn't tied to their ids is still in the tree and unused: `Bullet.Refract` with `SplitRefracted` and
            `RefractThroughAlly` (Prism Beam), `ZoneKind.Totem`, `TotemCharge` and friends, `Player.TotemCd` (Totem Staff). Ask before removing.

## Visuals
- [x] **Arc Lightning looks.** Built (Nov 2026): style A, Thunderhead (`Game/ArcLightningLooks.cs`), in the arena and the
      lobby preview, from [`Mockups/arc-lightning.html`](Mockups/arc-lightning.html).
- [x] **Chain Light looks.** Built (Nov 2026): style A, Radiance (`Game/ChainLightLooks.cs`), in the arena and the
      lobby preview, from [`Mockups/chain-light.html`](Mockups/chain-light.html).
- [x] **Character models for every role and class (18), with weapon models.** Built (Nov 2026) from
      [`Mockups/character-models.html`](Mockups/character-models.html), colour-by-class (`Game/CharacterModels.cs`),
      plus a bigger role-coloured glow on the floor round every player (`Juice.cs`, `GlowPlayer`). The weapon models are
      a first pass: redraw any of them in `DrawHeld`.
- [x] **Melee attack animations.** Built (Nov 2026): style E, the weapon models with the energy arc
      (`Game/MeleeLooks.cs`), from [`Mockups/melee-attacks.html`](Mockups/melee-attacks.html).
- [x] **Repulsion Cannon visuals.** Built: look D, the cannon blast (`RepulseLook.cs`), Nov 2026. Mockup: [`Mockups/repulsion-cannon.html`](Mockups/repulsion-cannon.html)
      (https://claude.ai/artifact/QutT2bQZmuigHQpx9cX5h3): today's look and four options, A Shockwave, B Light
      burst, C Force ripples (bends the floor grid), D Cannon blast (smoke, sparks, small kick), at the real 70° / 240 px
      cone, with optional hit feedback (flash, slide streak, orb). Drawing only (`DrawRepulseFx`), no protocol change.
      Not built from D: the dust puffs behind hit enemies (needs hit positions in the effect).
- [x] **Frost Lance visuals.** Built: look A, the crystal spear (`FrostLook.cs`), Nov 2026. Mockup: [`Mockups/frost-lance.html`](Mockups/frost-lance.html)
      (https://claude.ai/artifact/3iseS4yjtmVDriXUxqJoxF): today's look and four options, A Crystal spear, B Frost
      growth, C Glacier, D Aurora lance, at the real 230 px reach and 0.15 s splinter delay, with optional frost on
      hit enemies. Drawing only (`DrawIcicleFx` / `DrawSplinterFx`), no protocol change. Not built from A: the facet
      sparks on hit enemies (needs a per-enemy hit timer).
- [x] **Redesign the main menu buttons.** Built: design D with the pen plot intro (Nov 2026).
      Mockups done, waiting for a pick: four layouts in [`Mockups/menu-redesign.html`](Mockups/menu-redesign.html)
      (https://claude.ai/artifact/6T2r1GZSBsajVK8pxaAo8s), and three plotted intros for the menu in
      [`Mockups/menu-intro.html`](Mockups/menu-intro.html) (https://claude.ai/artifact/TdwEQNxJ8STpVk2MRPqLuk).
      Owner likes **D (Big play)**: its intros and the Host game → lobby move are in
      [`Mockups/menu-d.html`](Mockups/menu-d.html) (https://claude.ai/artifact/VskdxmJhgTzmpeUnjjM2R5). With D the
      port moves to Settings (Network, 7777 by default) and the name is edited on the bottom-left chip.
- [x] **Knockback visual effect.** Done (Nov 2026): pushes slide (0.18 s) with a streak, dust and an impact puff.
- [x] **Load-in animations for the How to Play and Field guide windows.** Done (Nov 2026): How to play slides in;
      the Field guide rises in.

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

- [x] **Sort modifiers by tier.** Done (Nov 2026): Easier / Risky / Harder headings in one grid.

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
- [x] **Zombie enemies for Zombie Infection** (built, with the dark green arena). Mockup: `Docs/Mockups/zombie-enemies.html` (published:
      https://claude.ai/artifact/9Dvd5zEz49owzvonTP3hnb). Four green zombies with green particle effects: Shambler
      (wide triangle, slow melee), Runner (narrow long triangle, fast melee, packs of 3), Spitter (pentagon, lobs globs
      that leave acid puddles), Crawler (trapezoid, grabs and roots the player), Bloater (swelling circle, bursts into a slowing cloud), Shrieker
      (diamond, screams to frenzy the horde), Mender (cross, heals and raises the dead faster). Waiting for the owner's go-ahead or
      changes; then build them as new EnemyTypes replacing the stand-ins.
- [x] **Wave editor** (option B, timeline) in F1 → Tools. Generation script done (`Tools/WaveGen/build.sh`, `Data/Waves`). Next: let
      hunts/approaches/paths use designed waves (the Warden Hunt's nine first).
- [x] Zombie Infection modifier, basic version (hordes, cap, the dead rise). Next: zombie looks/types, infected
      co-op players: [`ZombieInfection.md`](ZombieInfection.md)
- [x] Boss Hunt for the Warden (10 waves, its own enemies). Next: factions + starting builds for later bosses, then Perks:
      [`BossHuntsAndPerks.md`](BossHuntsAndPerks.md)
