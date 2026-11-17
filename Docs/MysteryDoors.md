# Mystery doors: sealed doors on the outer walls you pay vectors to open

Status: **design only, nothing built.** Decided with the owner on Nov 17, 2026. Expanding arena only. Builds on
[`SurpriseRooms.md`](SurpriseRooms.md) (shapes, placement), [`BreachRooms.md`](BreachRooms.md) (Stash, a door lined up with a
wall spot), [`Nodes.md`](Nodes.md) (tiny shop / vendor rooms) and [`FactionMinis.md`](FactionMinis.md). **Drawing rooms stays
exactly as it is**: doors are a second, cheaper way to grow the map.

## Decisions (owner)

| Question | Decision |
|---|---|
| Replace drawing? | **No, alongside it.** |
| How many | **1-2 new doors each build round**, unopened ones stay, **cap 3** on the map. |
| Behind a door | **Combat room, loot (Stash / chest), shop / vendor, risk (mini-boss / ambush).** |
| Price | **~60% of drawing** a room of the same size (you don't pick shape or spot). |
| When | **Any time**, mid-wave too; the room joins the fight at once. |

## How it plays

- A **sealed door** is a lit shutter in an outer wall with its price over it. The room behind is already rolled (seeded, same on
  every machine) but hidden: the map shows only a "?" footprint-less marker on the wall.
- Any player stands at it and holds **Interact** to pay from their own vectors (no vote). Not enough vectors: the price shows red.
- On paying, the shutter lifts with the reveal (the gold build-in of surprise rooms) and the room is added like any built room.
- The room's **kind** shows as a small icon on the door once a player is near (combat / loot / shop / risk), so it's a gamble on
  size and shape, not a blind one. *(Open: show the kind or keep it fully hidden?)*

## What's behind (first-guess weights)

| Kind | Weight | Room |
|---|---|---|
| Combat | 50 | a surprise shape (Ring, Cross, L, T, Chamfer), placed by `SurpriseOptions` behind the door's wall |
| Loot | 20 | a Stash (vector cache) or a small room round a chest |
| Shop / vendor | 15 | the tiny 4 x 4 shop or vendor room, already open |
| Risk | 15 | the faction mini-boss room (`MiniBossNow`), or an **ambush**: a squad spawns in the room as it opens |

Risk is never rolled before wave 5 or on the wave after a boss. A door whose room no longer fits (a drawn room took its space) is
re-rolled to a spot that does, or removed.

## Price

`Door price = round(0.6 * Expansion.Cost(room))`, minimum 1. Loot and shop doors are priced as their small room (cheap); risk
doors at the combat price of a mid shape so they don't stand out.

## Steps

1. **Core**: `MysteryDoor` (wall spot, rolled kind + shape + placement, price), the roll (seeded from map + wave, 1-2 per build
   round, cap 3), `Game.MysteryDoors`, `Msg.DoorOpen` (player -> host) and the doors in `Msg.ExpandState`; opening adds the room to
   `Expanded` and rebuilds the world; mid-wave opening through the `DoorsOpen` path. Suite `mysterydoor`. Protocol bump.
2. **Contents**: loot / shop / risk rooms (Stash cache, chest, tiny shop / vendor, mini-boss, ambush squad).
3. **Look**: the sealed shutter, price tag, kind icon, "?" on the minimap and build map, the reveal; previews.
4. **Bots and balance**: bots open doors when they can afford them; measure vectors spent per wave and survival vs drawing only.

## Open questions

- Show the room's kind on the door, or keep it hidden?
- Do doors expire (e.g. vanish after 3 rounds unopened) or stay until the cap pushes the oldest out?
- Should an unopened door ever act as a breach (enemies pour out of it)?
