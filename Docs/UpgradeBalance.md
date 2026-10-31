# Upgrade balance pass (step 3)

Status: **A-H done** (Oct 2026); **I** (Multishot / Barrage per weapon) in progress. Steps 1 and 2 (bugs and dead cards) are done:
every weapon upgrade now does something on every weapon.

## How it's measured

`Tools/SimTest/run.sh 1 1 balance` (about 50 s). For each of the 54 weapons, no other upgrades, it measures the
health taken off enemies in 10 s with nothing, with **one pick** of a card and with a **full stack** (the card's
cap, or 3 for uncapped cards), in three fights:

- **Boss**: one big target with as much health as the weapon deals in 10 s, so a run is one whole kill (this is
  what lets Executioner count).
- **Pack**: 4 enemies (400 health) bunched within 75 px; they come back when killed.
- **Spread**: 8 enemies (400 health) in a wide arc 100-130 px apart; you switch target every half second.

The numbers are the **median gain over all 54 weapons**, with the 10th-90th percentile when the spread matters.

Limits: enemies are pinned in place (so slows are worth nothing here and Homing only shows its cost), charge
weapons release on a fixed timer (the Golem Staff, Warhammer and Mender's Bow read low), and there is no aim
error. The Fuma Shuriken reads -100% against the boss (its loop misses a target that close); that's the
test's geometry. Treat it as a guide, not the truth; the session recordings are the check.

## What one pick is worth now (median over weapons)

| Card | Rarity | Boss | Pack | Spread | Notes |
|---|---|---|---|---|---|
| Sharpened Rounds | Common | +7% | +8% | +8% | |
| Quick Trigger | Common | +6% | +7% | +6% | 0 on some charge weapons |
| Hollow Points | Uncommon | +19% | +19% | +20% | |
| Hair Trigger | Uncommon | +14% | +14% | +14% | |
| Multishot | Rare | +36% | +45% | +29% | 0 on ~10% of weapons, +100% on others |
| Barrage | Rare | +27% | +56% | +13% | same unevenness |
| Piercing Rounds | Rare | 0 | +20% | 0 | only pays in lines of enemies |
| Swarm Rounds | Legendary | +1% | +40% | +28% | |
| Brood Mother | Legendary | 0 | 0 | 0 | turrets are too rare / short to matter |
| Executioner | Legendary | +15% | +12% | 0 | |
| Twin Fang | Legendary | +21% | +23% | +23% | the only legendary good everywhere |
| Collapse Rounds | Legendary | 0 | +80% | 0 (+130% at 3) | |
| Graviton Rounds | Legendary | 0 | 0 | 0 | utility only (slow) |
| Dark Matter | Legendary | +20% | **+105%** | +20% | |
| Homing Rounds | Mod | -10% | -7% | -9% | utility; costs 10% damage a stack |
| Ricochet | Mod | 0 | **+72%** | **+71%** | 3 stacks: +164% |
| Incendiary Rounds | Mod | +14% | +13% | +20% | (after the step 1 fix) |
| Cryo Rounds | Mod | 0 | 0 | 0 | utility only (slow / freeze) |
| Shock Rounds | Mod | 0 | +19% | +19% | |
| Explosive Rounds | Mod | 0 | +56% | 0 | |
| Velocity Rounds | Mod | +14% | +14% | +15% | |

## What's wrong

1. **Commons are near-worthless per pick** (+6-8%) next to everything else; a single Rare or mod is worth five of them.
2. **Ricochet is the best card in the game in any crowd** (+72% for one pick, more than most legendaries).
   It got there in step 2, when it started working on every weapon at the full 75% a bounce.
3. **Legendaries are all over the place.** Dark Matter (+105% in a pack) and Collapse (+80%) dwarf
   Brood Mother (0), Graviton (0 damage) and Executioner (+12-15%). A boss's three legendaries should be a real choice.
4. **Multishot and Barrage swing from 0 to +100% by weapon.** Some weapons turn an extra projectile into
   something weak or nothing (Bone Guard, Treant Rod, Totem Staff, Radiant Orb, Imp Caller, Frost Lance).
5. **Boss fights ignore half the cards.** Collapse, Explosive, Ricochet, Shock, Swarm and Piercing are worth 0
   against a lone boss. That's partly fine (they're crowd cards) but a boss pick shouldn't be a trap.
6. **Utility cards show 0 here** (Cryo, Graviton, Homing). That's expected: they need moving enemies to show.

## Proposed targets (one pick, median over weapons, in the card's best fight)

| Rarity | Target | Today |
|---|---|---|
| Common | +10% | +6-8% |
| Uncommon | +20% | +14-20% |
| Rare | +30-40%, and above 0 on every weapon | +20-56%, 0 on some |
| Mod (one of eight after a boss) | +25-35% in its fight, or clear utility | -10% to +72% |
| Legendary (one of three, from a boss) | +40-60% in its fight, and at least +15% against a boss | 0 to +105% |

## Proposed changes

| # | Change | Expected (estimates; re-measured after) |
|---|---|---|
| A | Sharpened Rounds +8% → +10% damage; Quick Trigger +8% → +10% fire rate; Hair Trigger +18% → +20% | commons ~+10% |
| B | Ricochet: a bounce carries 40% (not 75%) | ~+38% a pick, ~+60% at 3 |
| C | Dark Matter: +1 pierce (not +2), keeps +20% damage | pack ~+50% |
| D | Brood Mother: 40% of kills (not 20%) spawn a turret for 6 s (not 4); turrets also spawn from boss hits every 3 s | ~+20-30% |
| E | Graviton Rounds: also +15% damage to slowed enemies | +15% everywhere, keeps the slow |
| F | Executioner: below 50% health (not 35%) | boss ~+25% |
| G | Collapse Rounds: the implosion also hits the enemy shot (half damage), so it isn't 0 on bosses | boss ~+20% |
| H | Homing Rounds: costs 5% damage a stack (not 10%) | -5% a stack, still a trade |
| I | Multishot / Barrage: give the weak weapons a real extra (one at a time, with numbers) | above 0 everywhere |

A-H are number changes plus small code (D, E, G). I is per weapon and would come last, after A-H are measured.

## Decisions

The owner chose: the targets above; do A-H, then I.

## After A-H (one pick, median over weapons)

Measured with the same report after the changes (pack and spread enemies now have about 2.5 s of the weapon's
damage in health, and the stand-in boss counts as a boss, so Brood Mother's boss turrets show).

| Card | Rarity | Boss | Pack | Spread | Before (boss / pack / spread) |
|---|---|---|---|---|---|
| Sharpened Rounds | Common | +9% | +7% | +10% | +7 / +8 / +8 |
| Quick Trigger | Common | +8% | +7% | +7% | +6 / +7 / +6 |
| Hair Trigger | Uncommon | +19% | +18% | +19% | +14 / +14 / +14 |
| Brood Mother | Legendary | +58% (10 s; ~+40% over a whole fight) | +26% | 0 | 0 / 0 / 0 |
| Executioner | Legendary | +24% | +23% | +9% | +15 / +12 / 0 |
| Collapse Rounds | Legendary | +18% | +99% | +20% | 0 / +80 / 0 |
| Graviton Rounds | Legendary | +13% | +11% | +10% | 0 / 0 / 0 |
| Dark Matter | Legendary | +20% | +33% | +20% | +20 / +105 / +20 |
| Homing Rounds | Mod | -5% | -1% | -3% | -10 / -7 / -9 |
| Ricochet | Mod | 0 | +36% | +39% | 0 / +72 / +71 |

Brood Mother's boss turret: one every 8 s whatever the stacks (one every 3 s and one a stack read +115% for one
pick and +276% at three: turrets don't scale with your weapon, so they swamp a weak one).

Still off target, for a later pass: Piercing Rounds (0-6%; a Rare should be +30-40%), Swarm Rounds against a boss
(+2%), Twin Fang and Dark Matter at +20-33% (under the legendary target, but fine everywhere), and the mods
Incendiary / Shock / Velocity at +6-19% (under +25%).
