# Engineer: a vector builder class

Status: **steps 1-6 built** (Nov 19, 2026; protocol 417; step 6 removed the three forges): each role's one weapon is its robot weapon (Vector Blade, Iron Fist, Stasis Gun), and the DPS has a second, the Bumble Remote (Nov 25, protocol 519: `Core/BumbleRemote.cs`, README); Drag Pylon and Breach; the Draftsman passive and the Wide Field card. Steps 1-4 below are history (the forges, Strider, Sentinel and med-drone are gone). Next: the Vector Wall build-mode rework (mockup: https://claude.ai/artifact/5mqoqTQJi4WS6rsJggfQME). Mockup: [`Mockups/engineer-class.html`](Mockups/engineer-class.html)
(real renders, a playable bench, the kit, a Breach room roller). Numbers are first guesses.
The look is real draw code: `Game/EngineerLook.cs`, rendered with `ENGINEER=pylon|breach|real|bots ENGT=<s> Tools/Preview/shot.sh x hud`
(`Tools/Preview/ShotEngineer.cs`); the renders are in `Mockups/engineer/`.

## The class

A builder whose constructions draw themselves out of cyan vector line (the Expanding arena's wall ink). Health x1.0,
speed x0.95. Passive **Draftsman**: +25% vectors (Expanding arena). DPS first; Tank and Healer kits later.

## Kit (one of each for now)

| Slot | Name | What it does | Numbers |
|---|---|---|---|
| Weapon | **Vector Forge** | Fire: a light rivet gun. The weapon owns one **Strider**, a walker that hunts the enemy nearest you, punches, slams every 4 s and draws aggro. Alt on an enemy locks it on (+50% damage, +40% speed until it dies); Alt on the floor posts it there to guard (200 px). Broken: rebuilds at your side in 8 s. | Strider 450 HP (+8%/wave), punch 34 / 0.55 s, slam 60 in 80 px + 0.4 s stun; rivet 10 / 0.3 s; ≈ 95 sim DPS |
| 1st ability | **Drag Pylon** | Plant at the cursor (≤ 320 px). A vector grid on the floor slows enemies in it. Stays until the next one is placed, which folds the old one up. | 6 s cooldown, 130 px, 45% slow (bosses 20%), 0.6 s linger, can't be hit |
| Ultimate | **Breach** | Aim at a wall ≤ 220 px. A vector hammer smashes a 3-tile hole (80 damage + knockback in 140 px) and a random room draws in behind it, free and with no vote (like a Stash). | 70 s; 6 shapes (Stash, Long hall, Workshop L, Cross, Pillar room, Twin cells); contents: Vector cache 30%, Supply crate 25%, Repair bay 15%, Workbench 15%, Ambush 15% |

Breach reuses the breach-room work ([`BreachRooms.md`](BreachRooms.md), `SurpriseOptions`, `PinBreaches`): the room is
placed so its floor covers the hole, shifting sideways around neighbours; no legal spot = blast only.

## Looks (picked Nov 17: Engineer A Hi-vis, Strider B plated walker)

`Game/EngineerModel.cs`, sheets in `Mockups/engineer/look-*.png`: Engineer A Hi-vis / B Drafter / C Welder; Strider A wireframe /
B plated / C spider rig; the Vector Forge is a rivet gun with a cyan emitter.

## Open questions

- Breach in arenas with no rooms to grow: shockwave only, or loot drops at the hole? (proposed: the latter)
- Role: DPS first? (proposed)
- Vectors in the kit: Draftsman passive and repairing the Strider with vectors (1 vector = 30 HP)? (proposed: both)
- Ambush rooms: keep the risk roll? (proposed: keep, double gold)

## Build steps

1. **Built** (protocol 397): `Kit.Engineer`, DPS only (stand-ins lock its other lobby places: `FillsGaps`); Vector Forge
   (rivets + the Strider: hunt, punch, slam, aggro, Alt lock/post/recall, rebuilt in 8 s), Drag Pylon, Breach (blast; a
   room behind the Expanding arena's wall with one loot roll, the loot dropped where it lands); looks A Hi-vis and the
   plated walker. Defaults taken for the open questions: DPS first, no wall = blast + loot at the cursor, Ambush kept.
2. **Built**: Draftsman (+25% from every vector gain, `AddVectors` -> `Draftsman`, part-vectors carried in `Player.DraftCarry`),
   repairs (within 90 px of your hurt Strider in the Expanding arena: 1 vector per 0.25 s for 30 health) and the class cards
   Reinforced Frame, Hydraulic Fists, Quick Assembly and Wide Field (`Game.EngineerCards`).
3. **Built**: Breach rooms keep their loot as a map node (`NodeKind.BreachLoot`, the kind in `Rule`) taken by the first player to
   walk up to it (`TickBreachLoot`; a Stash keeps its own vector cache); the Strider builds in for 1.2 s (`Minion.Wind` 2-3: the
   wireframe draws in, then the plated walker).
4. **Built** (protocol 397): **Tank and Healer kits** (decided Nov 17; one weapon, 1st ability and ultimate each, like the DPS):
   - Tank: **Bulwark Forge** (a nail shotgun; the follower is a heavy **Sentinel** walker that soaks shots and taunts wider,
     Alt orders it like the Strider), **Vector Wall** (1st ability: a 5-tile wall from you toward the cursor that stops enemy shots
     for 8 s), **Bastion** (ultimate: a ring of vector walls round you for 10 s, with gaps to shoot out of).
   - Healer: **Medi-Drone Forge** (a light rivet gun; the follower is a flying **med-drone** that heals the most hurt ally),
     **Med Pylon** (1st ability: a pylon whose field heals allies; one at a time), **Safe Room** (ultimate: Breach a wall into a
     room that heals everyone inside for 12 s; elsewhere a healing dome at the cursor).
   Built as: the Sentinel runs the Strider's tick (`IsWalker`), Vector Wall and Bastion tiles are `MinionKind.VectorBlock`
   (static blockers that wear by the damage they stop), the med-drone is a weapon companion (`MinionMove.FollowHurt`,
   `MinionAttack.Heal`), Med Pylon shares the Drag Pylon's cast (`CastPylon`), Safe Room shares Breach's hammer (`SmashWall`)
   and is a `ZoneKind.SafeRoom` with `Zone.DamageCut`. The model wears its role's build and trim. Stand-ins now prefer their
   own role's (`FillsGaps`).
5. **Built** (protocol 411): **robot weapons**, each role's second weapon (`EngineerBots.cs`): the first click summons a robot
   Engineer of the role at the cursor (a click on an enemy sends it there); it fights and draws enemies near it; with it out,
   Fire is a basic attack. Tank **Iron Fist** (Tank Bot: nail shotgun, 850 HP, taunt 240 px; fist: an arc shove, 10 damage,
   180 px push), Healer **Stasis Gun** (Medic Bot: rivets + 9/s heal in 110 px; hold Fire: a 60 px field at the cursor that holds
   enemies in place and hurts 14/s), DPS **Vector Blade** (Assault Bot: rapid rivets; a 24-damage swipe). Rebuild 8 s.
6. Next: a third weapon and more abilities per role (each role has one locked weapon key and two locked ability keys).
6. **Built** (protocol 417, Nov 19): the **Vector, Bulwark and Medi-Drone Forges removed** with the Strider, Sentinel and
   med-drone (their cards Reinforced Frame, Hydraulic Fists and Quick Assembly, Draftsman's repairs, and Breach's repair and
   workbench loot too). Each role's robot weapon is now its first weapon and fills the locked places (`FillsGaps`).

Unlock (Nov 18): a locked class, `Unlocks` "class-engineer" (win a run; `Profile.EngineerUnlocked`), with a padlock card in the lobby and
the Wildcard wheel until then. Open to everyone for now: `EngineerUnlockedForTesting` in `LobbyUi.cs`.
