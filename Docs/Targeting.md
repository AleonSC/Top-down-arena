# Targeting: one way to ask "what does this hit?"

> **Status: steps 1-8 done**, plus the lobby's PVP switch (`Session.HangoutPvp`, protocol 478). `Core/FoeQuery.cs` answers every player attack loop but three; PvP is on in the Hangout (`Core/Pvp.cs`):
> 89 of 91 weapons hit players in the `pvp` test (the other two only miss its close range), and every ability's query does. Status
> effects reach players too (step 5).

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
   nothing yet. Enemies' death blasts credited to a player stay out.
4. **Summons, lock-ons and beams** (done): `NearestEnemy` and `NearestInLeash` take the owner (`by`), so summons, turrets and
   homing go for other players too; weapons that keep a target's id look it up with `TryFoe` / `FoeExists` (a stand-in's id is
   `StandBase - player id`, clear of the 0 and -1 that mean "none"); target-choosing loops use `AllFoes(by:)`; copied lists use
   `SnapFoes(by)`; `DotDamage` (burns, ink) routes stand-ins too, as damage over time; other players' Spirit Chalk ink burns you.
   The `pvp` test lists any weapon that doesn't land: Gore Mortar (its lob lands 160 px out at least) and Fuma Shuriken (it needs
   its orbit stance) only miss the test's close range.
5. **Status effects on players** (done, `TickPvp`): stand-ins are kept between ticks, so whatever an attack writes on one (however
   it's written) is read back each Hangout tick and becomes the player's own: a freeze, stun or hold is `StunT` (halved, at most
   `PvpStunCap` 1.2 s), a slow is `PvpSlowT`/`PvpSlowMult` (replicated, protocol 476; halved, at most 3 s, never below 45% speed),
   a push is a knockback (`KnockPlayer`, half the distance), and burns, bleeds and poison tick as damage over time from whoever set
   them (never your own on yourself). A melee daze does nothing to a player (it would be a stun-lock).
6. **Walls** (done): another player's Blood Wall and Ossuary Wall keep you off them (a knockback, `PvpWallPush`, at most every
   0.12 s) and hurt you as they hurt enemies (the Blood Wall's bleed and its mastered slow, the Ossuary Wall's darts). Your own
   walls never push you; the Blood Wall still drains its caster as before.
7. **Marks and other states** (done): each tick a stand-in runs the same status ticks as an enemy (burns, incendiary pools,
   bleeds, poison, venom, wet, bone cages, every class's `TickEnemy` hook), so their damage, timers and stacks work as on an
   enemy. Holds (Leech Tendrils' grasp, a bone cage, the Heart Lift) are a stun; wet is a slow, and a wet player draws
   Tsunami's arcs; marks (`MarkMult`), a Searchlight and the attacker's damage cards (Blood Price, Vein Tithe, Execute,
   Graviton Rounds, crits, cast power) raise PvP hits (`PvpHitMult`). After a PvP stun a player can't be stunned again for
   `PvpStunImmune` (1.5 s past its end).

8. **Audit clean-up** (done): four damage loops that still walked the raw list moved onto `FoesInCircle` (Supply Drop's
   impact, Bladed Dive's slashes, Iron Armadillo's ball, Bamboo Grove's ring, which keeps its own half-body test inside the
   circle), and the hand-written "nearest enemy" picks of Poison Dart, Force Pact and the Nunchaku's swatted shots use
   `NearestEnemy` (`Targets.cs`). Its overload `NearestEnemy(at, ok, range, by)` takes a filter (Force Pact's `CanPact`; no
   `by` there, as a player can't be bound). Same picks in PvE (the first in the list wins a tie, as before). Still hand-written:
   Shadow Step's landing (`ShadowStepLanding`, Ninja.cs) runs on your own game, where `Targetable` (host-only `SpawnT`) can't be
   used, and loops with their own rule (venom and Lightning Rod jumps, caged enemies, bot sensing, boss and enemy AI).

## Behaviour notes

- Sites that only checked "spawned in" now also skip an enemy already at 0 health this tick (no double hit on a corpse).
- The cover check runs only where it ran before, after the shape test, as before (it notes the hit's side for the Devourer).
