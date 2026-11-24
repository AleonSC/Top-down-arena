# Arc Knuckles: the Knight DPS's fourth weapon (design)

Rework status: **built** (Nov 2026, protocol 492): two stances, Brawl steered by the click wheel; see [Stance rework](#stance-rework-built) at the end. The five-punch combo and the Alt Rocket Dash below are the old design, replaced.

Status: **built** (Nov 2026, protocol 272): all four steps, then the lock-on and hit feel. The weapon's damage went from 12 to 22 in step 4 (about 88 a second, with the other DPS Knight weapons); every number below is the step-1 design at 12, scaled by 22/12 in the game.
(https://claude.ai/artifact/Rqn2WEDWUQL1RTJaxvPf8Y), version 5. The **Arc Coil** look, the **Full combo**, **Parry cancel**
and **Perfect parry** scenarios, and the **Rocket Dash**, **Dash → Flurry** and **Full dash chain** Alt scenarios are the
design; the page's other Alt fires (Ground Pound, Grab & Throw on its own, Rocket Fist, Shockwave Clap, Seismic Line,
Magnet Yank) were not picked.

> Coiled blue alloy fists. Fire is a five-punch combo that ends in a ground slam. The weapon-action key is a Rocket Dash,
> and each Fire press as a move lands extends it into a chain: Flurry, then Grab & Throw, then Rocket Punch.

## Decisions (owner)
- **Name:** Arc Knuckles (the Knight DPS's 1st ability is already "the Gauntlet").
- **Look:** Arc Coil: blue alloy plates, glowing knuckle bands, coils on the cuffs that pulse, small lightning arcs on every hit.
- **Slot:** Knight DPS, **Choice 3**: the 4th weapon key that is locked today. The other classes keep 3 weapons and a locked 4th key.
- **Parry:** raising the parry cancels the punch and **resets the combo to the jab** (`ResetAttackChains`, like every other chain).
  A **perfect** parry also arms the **Counter Hammer**: the next Fire press is the gold finisher, from any step.
- **Alt:** Rocket Dash, with the three chained follow-ups below.

## Fire: the combo (damage is the weapon's, before upgrades)
| Step | Punch | Damage | Shape | Next punch can start |
|---|---|---|---|---|
| 1 | Jab (left) | 12 | 70 px thrust, 14 px wide | 0.24 s |
| 2 | Cross (right, body turns in) | 14 | 80 px thrust, 15 px wide | 0.28 s |
| 3 | Hook (left) | 16 | 120° sweep, 72 px | 0.32 s |
| 4 | Uppercut (right) | 18 | 70 px thrust; launches: airborne 0.55 s, can't shoot | 0.38 s |
| 5 | Twin Hammer (both fists) | 30 | 110 px shockwave, 38 px shove, 0.6 s stun | 0.62 s, then back to 1 |

- About 1.8 s for the full chain, roughly 60 damage per second. 1.2 s without a punch resets to the jab.
- The hammer's shockwave cuts small bullets in it, like the other Knight melee.
- **Counter Hammer** (perfect parry armed): 38 damage, 135 px gold shockwave, 48 px shove, 0.8 s stun. The combo then starts from the jab.
- Five pips under the player show the step; gold when the Counter Hammer is armed.

## Alt (weapon-action key, Left Alt): Rocket Dash and its chain
| Step | Input | Move | Damage |
|---|---|---|---|
| 1 | Alt tap (3 s cooldown) | **Rocket Dash**: thruster lunge 130 px in 0.2 s, fist first, untouchable. The first enemy hit takes the damage and a **short** 24 px knockback (it stays in reach) | 24 |
| 2 | Fire within 0.35 s of the dash hitting | **Flurry** where you landed: 8 alternating jabs in 0.7 s, then a straight (12 px knockback). You can't move while it runs | 8 × 6 + 20 |
| 3 | Fire within 0.35 s of the straight | **Grab & Throw**: grab the Flurry target, swing it round once, throw it along your aim at 520 px/s. Not bosses: on a boss the chain ends here | 20 to it, 26 to what it hits |
| 4 | Fire within 0.35 s of the throw landing | **Rocket Punch**: the fist fires off on its chain down a 210 px line, hits everything on it, then reels back | 28 each |

- A dash that hits nothing ends the chain (no follow-up). Missing a window, or raising the parry, ends it too.
- The whole chain does about 194 damage to the main target, plus the throw and punch splash.
- The dash moves the player through `Msg.Carry` (like the Gauntlet's charge); the grabbed enemy is carried by the host.

## Build steps
1. **Slot and combo.**
   - Per-class weapon count: `Defs.WeaponCount(role, kit)` (Knight DPS 4, the rest 3) replaces the fixed `WeaponsPerRole` where it means "this class's
     weapons": `Defs.Weapon`, the lobby's 4th key (`LobbyUi.cs`), saves (`SaveData.cs`), session checks (`Session.cs`),
     bots (`PartyBots.cs`), Run stats (`RunStatsUi.cs`). **Built.** Not yet: the Wildcard's class wheel still lists each class's
     first three DPS weapons (its picks are numbered kit × 3 + choice, shared with the abilities), so the Wildcard can't pick it yet.
   - `WeaponId.ArcKnuckles` at the end of the enum, with its `WeaponDef` in the Knight's list.
   - `Core/ArcKnuckles.cs`: the five steps, the perfect-parry Counter Hammer, the parry reset.
   - `Game/ArcKnucklesLook.cs`: the Arc Coil fists, trails, arcs, shockwave, pips.
   - Sounds, bot use, a `knuckles` sim suite, a preview shot, README, CODEMAP, protocol bump.
2. **Rocket Dash** on the weapon-action key: the thruster lunge, the hit, its cooldown, the look and sound. **Built** (your own game
   moves you when `Player.KnuckleDashes` changes, like the Katana's dashes, not `Msg.Carry`). Bots don't use the weapon-action key
   for any weapon, so they don't dash either.
3. **The chain**: Flurry, Grab & Throw (the carried enemy, the no-boss rule), Rocket Punch (the chained fist), the 0.35 s windows. **Built**
   (`Core/ArcKnucklesChain.cs`): `Player.KnuckleChain` holds the stage; the host carries the grabbed enemy along `KnuckleGrabFist`,
   which the look shares; you're rooted during the Flurry and the grab; a pulsing ring and chevrons show while a window is open.
   A grab needs the target within 110 px; on a boss, or with the target gone, the press throws a normal punch and the chain ends.
4. **Upgrades and balance**: what Homing Rounds, Barrage and extra projectiles do for it (`HomingDesc`, `BarrageDesc`,
   `ExtraDesc`), its ascension, and a balance-test pass against Twin Blades, Greatsword and Rapier.

   **Built:** the generic melee rules cover it (damage, fire rate, crits, elemental rounds through the hit pipeline; Piercing Rounds'
   reach; Barrage's sword waves); +1 extra projectile is an aftershock slam, now drawn. Damage 22 (measured 88 a second against
   Twin Blades 83, Rapier 85, Greatsword 89); the dash and chain hits are multiples of it, so upgrades lift them too. Ascension is the
   lobby's placeholder for every weapon. The Wildcard can pick it: its weapon picks run on past kit × 3 + choice to
   the DPS fourth weapons (`Defs.WildWeaponPick`, `WildWeaponKit`; the Arc Knuckles are pick 18), and the class wheel shows the
   Knight's four. Not done: bots on the weapon-action key (no weapon has that).

## After play-testing (Nov 2026, protocol 272)
- The arms crossed over the body: the look put the fists on the far side from their shoulders (and from where the host carries a
  grabbed enemy). Fixed: +y in the poses is clockwise of the aim everywhere, as in the mockup.
- **Lock-on:** the Rocket Dash locks onto the enemy nearest the cursor within 300 px (`KnuckleLockTarget`) and flies you up to it (up
  to 280 px); the Flurry and the Rocket Punch turn to it, the grab takes it, the Rocket Punch reaches it and 80 px past. A faint
  reticle shows the pick while the dash is ready, a bright one while the dash and chain run.
- **Hit feel**, as the Twin Blades have: each punch that connects sends `FxKind.KnuckleHit` with its contact sound; every screen throws
  sparks and a ring, and the puncher's own holds the picture and shakes, sized to the punch (`Juice.KnuckleHitFeel`). The press
  sounds are whooshes now; the knock is the contact's.

## Stance rework (built)
Mockup: `Docs/Mockups/arc-knuckles-stances.html` (https://claude.ai/artifact/Tc7nHgT8fu9QPumT3k8ntp), playable, with demos.
Damage is a multiple of the weapon's 22.

**Decisions (owner):**
- Two stances, swapped on the **weapon-action key** (the Rocket Dash moves into the Rocket stance). Swapping resets both combos.
- **Brawl** (stance 1, the skill one): the steer direction is the movement keys at the press; with none held, a punch carries on along
  you → target and a dash goes toward the cursor. A punch needs an enemy in reach under the cursor; a **whiff keeps your step**.
  1.6 s idle returns to move 1.
- **Rocket** (stance 2): today's chain on Fire with **no timing windows**; each press is the next move. **4 s idle** returns to the
  Rocket Dash. Parry cancels and resets.
- **Wall slam** kept: an enemy knocked into a wall fast (over 300 px/s) takes ×0.5 and a 0.6 s stun.
- Parry cancels the move and resets the stance to move 1 in both stances; a perfect parry arms the Counter Hammer as today.

| # | Brawl move | What it does | Numbers |
|---|---|---|---|
| 1 | Drive Punch | Step in, punch the clicked enemy the steer way; everything near it goes too | ×1.0, 60 px blast, 190 px knockback, reach 150 px |
| 2 | Slip Dash | Dash the steer way, untouchable | 130 px in 0.15 s |
| 3 | Drive Punch | Second steered punch | ×1.15, 60 px, 190 px |
| 4 | Slip Dash | Second steered dash | 130 px |
| 5 | Pin Punch | Stuns the group in place | ×1.6, 85 px, 1.8 s stun, knockback cleared |
| 6 | Barrage | 10 jabs; the stun can't run out while it lasts | 10 × ×0.35, 75 px, 0.8 s, rooted |
| 7 | Quake Punch | Hold to charge, release on the ground: radial harm and push | ×1.4 → ×3.0, 90 → 180 px, 0–1.2 s charge |

| # | Rocket move | Numbers |
|---|---|---|
| 1 | Rocket Dash (lock-on, up to 300 px) | ×1.1 |
| 2 | Flurry (rooted) | 8 × ×0.27 + ×0.9 straight |
| 3 | Grab & Throw (not bosses) | ×0.9 to it, ×1.2 to what it hits |
| 4 | Rocket Punch (210 px line) | ×1.3 each |

**Wheel steering (owner's pick, built):** movement keys only move; the mouse steers. Brawl starts with a **Pull Punch** (click an
enemy up to 230 px away and within 70 px of the cursor: pulls you in, ×0.8, a 0.5 s stun on everything within 50 px). A landed one
opens a four-way wheel **fixed on the screen** where you clicked; each later click steers by its side of the wheel (the centre is no
direction), so right, right is punch right, then dash after them. Clicks during a move wait 0.3 s for it; held Fire repeats. Brawl is
8 moves (Pull, Drive, Slip, Drive, Slip, Pin, Barrage, Quake); the punches take the enemy nearest you and step you in to it.

**As built:** the old five-punch combo is gone (the Counter Hammer stays, for a perfect parry, in both stances). The Barrage is the
Flurry's timing: eight jabs ×0.35 and a straight ×0.7, rooted 0.95 s. A full Quake Punch can be held 0.6 s, then it lets go on its
own (bots hold Fire). The Rocket Dash has no cooldown; one that hits nothing blocks the next click for 0.6 s. Extra projectiles
are aftershocks of the Quake Punch (40 px wider) and the Counter Hammer. Live Wire: the Pull Punch and Rocket Dash reach 25%
farther, the Pin Punch and Counter Hammer stun 0.3 s longer. Bots swap stance between combos (`BotWeaponAlt.cs`).
The wire: `Msg.Input` carries the wheel's steer as an angle byte; the snapshot carries the stance, the Brawl move, the root and the
charge.

**Build steps (all built):** 1. stance state on the player (`Player.KnuckleStance`), the swap on the weapon-action key, HUD pips per stance;
2. Rocket stance: move the chain to Fire, drop the windows, 4 s idle; 3. Brawl moves 1–4 (steer input from the move keys, the
group knockback, wall slam); 4. moves 5–7 (Pin stun, Barrage hold, Quake charge); 5. look, sounds, `knuckles` suite, README,
protocol bump, balance pass.
