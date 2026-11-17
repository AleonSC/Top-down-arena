# Engineer: a vector builder class

Status: **mockup** (Nov 17, 2026). Nothing built yet. Mockup: [`Mockups/engineer-class.html`](Mockups/engineer-class.html)
(real renders, a playable bench, the kit, a Breach room roller). Numbers are first guesses.
The look is real draw code: `Game/EngineerLook.cs`, rendered with `ENGINEER=strider|pylon|breach ENGT=<s> Tools/Preview/shot.sh x hud`
(`Tools/Preview/ShotEngineer.cs`); the renders are in `Mockups/engineer/`.

## The class

A builder whose constructions draw themselves out of cyan vector line (the Expanding arena's wall ink). Health x1.0,
speed x0.95. Passive **Draftsman**: +25% vectors (Expanding arena). DPS first; Tank and Healer kits later.

## Kit (one of each for now)

| Slot | Name | What it does | Numbers |
|---|---|---|---|
| Weapon | **Vector Forge** | Fire: a light rivet gun. The weapon owns one **Strider**, a walker that hunts the enemy nearest you, punches, slams every 4 s and draws aggro. Alt on an enemy locks it on (+50% damage, +40% speed until it dies); Alt on the floor posts it there to guard (200 px). Broken: rebuilds at your side in 8 s. | Strider 450 HP (+8%/wave), punch 34 / 0.55 s, slam 60 in 80 px + 0.4 s stun; rivet 10 / 0.3 s; ≈ 95 sim DPS |
| Space | **Drag Pylon** | Plant at the cursor (≤ 320 px). A vector grid on the floor slows enemies in it. Stays until the next one is placed, which folds the old one up. | 6 s cooldown, 130 px, 45% slow (bosses 20%), 0.6 s linger, can't be hit |
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
