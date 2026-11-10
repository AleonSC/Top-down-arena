# Room building, part 2: breaches, portals and fittings

Status: **plan, nothing built.** Owner picked these three on Nov 10, 2026. Builds on [`VectorBuilding.md`](VectorBuilding.md) and
[`FloatingRooms.md`](FloatingRooms.md) (all built).

## Why

- Bots die around wave 14-21 in Expanding vs 26+ in Classic, and most of it is the **map's shape** (long hallways, dead ends, enemies spread out), not the head count.
- Rooms are plain boxes plus random cover: drawing one is a size choice, not a tactical one.
- At ~0.85 rooms a round the map fills by wave ~25 of 50; after that vectors have nothing to buy.

## 1. Breach spawns

- Enemies spawn at **breaches**: glowing cracks on outer walls (not random spots). A few open each wave, weighted to long outer walls and dead ends.
- Compact builds and loops have less outer wall, so fewer breaches: the shape you draw decides where fights happen.
- Breaches telegraph 1.5 s before spawning (readability, Docs/EnemyReadability.md).
- Ties to the existing cost rule (new wall length): outer wall is both what you pay for and where the danger comes from.

## 2. Buyable portals

- A new draw-screen mode, **Link**: click two rooms to propose a portal pair. Voted like a room; cost ~ 8 vectors + 1 per 2 tiles of walking distance saved.
- Reuses the Wings portals (`Rooms.Portals`, `PortalHop` corner-graph links, cooldown, look); enemies use them too.
- Fixes "too linear" on big maps and gives a late-game vector sink. Cap: 3 pairs.

## 3. Room fittings

- Spend vectors to fit one feature into a built room (one per room, proposal + vote):
  - **Turret nest**: a weak auto-turret on a pedestal (enemies can break it; rebuilt next round).
  - **Healing well**: slow regen while standing in it, shuts off while you shoot.
  - **Trap floor**: a strip that slows and burns enemies.
  - **Shutter gate**: a door the team can close for 8 s (cooldown 30 s) to split a crowd.
- Gives late-game use for vectors and turns rooms into planned defensive spots.

## Build steps

1. Breach spawns (spawn rule, telegraph look, steering from breaches, `expand` tests, bot balance report `vecbal`).
2. Buyable portals (`RoomKind.Link` proposal, sync, portal build into `World`, look on map and minimap). Protocol bump.
3. Room fittings (kinds, proposal, host logic, looks, mockups to pick from first). Protocol bump.

## Open questions (defaults in bold)

- Breaches on boss waves? **No: fixed boss arena.**
- Can enemies break fittings? **Turret yes; the rest no.**
- Do portals count toward the room cap? **No: their own cap of 3.**
