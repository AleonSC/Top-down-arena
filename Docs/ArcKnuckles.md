# Arc Knuckles: the Knight DPS's fourth weapon (design)

Status: **step 1 built** (Nov 2026, protocol 265): the slot, the combo, the parry reset and Counter Hammer, the Arc Coil look. Steps 2-4 to come. Mockup: [`Docs/Mockups/gauntlets.html`](Mockups/gauntlets.html)
(https://claude.ai/artifact/Rqn2WEDWUQL1RTJaxvPf8Y), version 5. The **Arc Coil** look, the **Full combo**, **Parry cancel**
and **Perfect parry** scenarios, and the **Rocket Dash**, **Dash → Flurry** and **Full dash chain** Alt scenarios are the
design; the page's other Alt fires (Ground Pound, Grab & Throw on its own, Rocket Fist, Shockwave Clap, Seismic Line,
Magnet Yank) were not picked.

> Coiled blue alloy fists. Fire is a five-punch combo that ends in a ground slam. The weapon-action key is a Rocket Dash,
> and each Fire press as a move lands extends it into a chain: Flurry, then Grab & Throw, then Rocket Punch.

## Decisions (owner)
- **Name:** Arc Knuckles (the Knight DPS's Space ability is already "the Gauntlet").
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
2. **Rocket Dash** on the weapon-action key: the thruster lunge, the hit, its cooldown, the bot use, the look and sound.
3. **The chain**: Flurry, Grab & Throw (the carried enemy, the no-boss rule), Rocket Punch (the chained fist), the 0.35 s windows.
4. **Upgrades and balance**: what Homing Rounds, Barrage and extra projectiles do for it (`HomingDesc`, `BarrageDesc`,
   `ExtraDesc`), its ascension, and a balance-test pass against Twin Blades, Greatsword and Rapier.
