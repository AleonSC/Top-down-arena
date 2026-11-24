# Targeting: one way to ask "what does this hit?"

> **Status: steps 1-2 done** (`Core/FoeQuery.cs`; every player attack loop but three uses it). Step 3 (PvP damage) is next, when the
> owner wants it.

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
3. **PvP damage** (not started): the queries also yield other players when PvP is on, and one `Hurt(target, dmg, attacker)` sends
   damage to an enemy or a player. Status effects (slows, stuns, knockback, marks) write enemy fields at each site, so they stay
   enemy-only until each is mapped to its player version.

## Behaviour notes

- Sites that only checked "spawned in" now also skip an enemy already at 0 health this tick (no double hit on a corpse).
- The cover check runs only where it ran before, after the shape test, as before (it notes the hit's side for the Devourer).
