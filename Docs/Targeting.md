# Targeting: one way to ask "what does this hit?"

> **Status: steps 1-4 done.** `Core/FoeQuery.cs` answers every player attack loop but three; PvP is on in the Hangout (`Core/Pvp.cs`):
> 89 of 91 weapons hit players in the `pvp` test (the other two only miss its close range), and every ability's query does. Status
> effects on players (slows, stuns, knockback, bleeds) are the open step.

## Why

Every weapon, ability, summon and gadget used to walk the enemy list with its own loop and its own checks (still spawning? alive?
behind cover? does its body count?). The same rule was written ~75 different ways, some slightly different, and PvP would have meant
editing all of them.

## The helpers (`Core/FoeQuery.cs`)

- `FoesInCircle(c, r)`: blasts, splashes, slams, auras.
- `FoesInBox(a, b, halfWidth)`: lanes, thrusts, beams.
- `FoesInCone(apex, aim, half, reach)`: swings, cones.
- `AllFoes()`: attacks with their own shape (sprays, walls, crosses, phalanxes); the loop tests the shape itself.

A foe is a `Targetable` enemy (spawned in, alive) whose body touches the shape (its radius counts). An optional `cover` point skips
one that blocks hits from there (`BlocksShot`: bosses changing phase, a Gutworm underground, a Duelist's guard). The queries are
struct enumerables: no allocation, they nest, and `HurtEnemy` inside one is safe.

## Steps

1. **Helpers** and the shared hits (`Combat.cs` splashes and grenades, `Minions.cs` summons and zones). Done.
2. **Every other attack loop** (weapons, abilities, ultimates, gadgets, loot; ~70 loops in ~55 files). Done. Left on the raw enemy
   list on purpose: Exsanguinate's reel (a hooked enemy, not a pick), the zone tick in `Minions.cs` (squares and lanes) and the
   path hazards in `Paths.cs` (the floor, not a player).
3. **PvP damage** (done, `Core/Pvp.cs`): every query takes the attacker (`by`); with `PvpOn` (the Hangout) it also yields each
   other player as a stand-in `Enemy` (never in `Game.Enemies`, `Enemy.Stand` set). `HurtEnemy` sends a stand-in's hit to its
   player through `Damage`, at `PvpDamage` (35%), never to the attacker, never through a dodge's i-frames or a respawn beam. Melee
   swings (`LandSwing`) and player bullets (`PlayerShotHits`) use the same queries. Status effects land on the stand-in and do
   nothing yet. Left out: Blood Wall and Ossuary Wall (several owners per loop) and enemies' death blasts credited to a player.
4. **Summons, lock-ons and beams** (done): `NearestEnemy` and `NearestInLeash` take the owner (`by`), so summons, turrets and
   homing go for other players too; weapons that keep a target's id look it up with `TryFoe` / `FoeExists` (a stand-in's id is
   `StandBase - player id`, clear of the 0 and -1 that mean "none"); target-choosing loops use `AllFoes(by:)`; copied lists use
   `SnapFoes(by)`; `DotDamage` (burns, ink) routes stand-ins too, as damage over time; other players' Spirit Chalk ink burns you.
   The `pvp` test lists any weapon that doesn't land: Gore Mortar (its lob lands 160 px out at least) and Fuma Shuriken (it needs
   its orbit stance) only miss the test's close range.
5. **Status effects on players** (open): slows, stuns, freezes, knockback, marks and bleeds land on the stand-in and do nothing.
   Each needs mapping to its player version (Player.SlowT, StunT, a knock), one effect at a time.

## Behaviour notes

- Sites that only checked "spawned in" now also skip an enemy already at 0 health this tick (no double hit on a corpse).
- The cover check runs only where it ran before, after the shape test, as before (it notes the hit's side for the Devourer).
