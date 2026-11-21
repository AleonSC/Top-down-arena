# Cell variants and pits

Status: **planned, nothing built** (decided with the owner Nov 21, 2026). Mockup: `Docs/Mockups/cell-pits.html`
(approximate HTML, not the real draw code). Builds on `CellLayouts.cs` (the 18 cell layouts) and Docs/GridPieces.md.

## Decisions

- **Cell variants are a system, not a one-off.** A cell layout can have variants: the plain one plus others (pits first;
  later ones such as a different floor or extra cover use the same slot). Each Combat lattice cell rolls its layout,
  then its variant, seeded from its place on the lattice like `CellLayouts.Plans`, so no wire data.
- **Pits**: voids in the floor. Look **B, Hazard rim** (marching gold/black stripes, a grid falling away inside).
- **All 8 pit variants** go in: Sinkhole (Open Plaza), Four Wells (Four Pillars), Island Vault (Vault), The Chasm
  (Side Alcoves), Corner Drops (Corner Nooks), Crossfire Gap (Crossfire), Pit Halls (Hall & Alcoves), Stepping Stones
  (Pillar Field).
- **Enemy falls in**: dies, counts as the shover's kill, its coins pop onto the rim. Bosses, mini-bosses and flyers hover.
- **Player falls in**: **15% max HP, never lethal** (stops at 1 HP), respawns on the rim next to where they fell, 1 s shield.
- Falling needs your centre over the void. A dash that lands on solid ground crosses; bullets fly over; steering and
  bots treat pits as walls.

## Fix before building

- The chest always stands in the cell's middle (`RoomChests.CellMiddles`): Sinkhole becomes a ring round a chest island,
  and The Chasm, Corner Drops and Crossfire Gap keep the chest in the middle (the mockup moved some).
- Pit variants keep each layout's real sleeper, trap and loot slots (`CellLayouts.cs`), moving any that sit in a pit.

## Steps

1. **Variants in data**: `CellVariant` on `CellLayout` (pits as rects/circles, cell-local), the seeded roll, `Turned`
   for pits; suite `celllayouts`.
2. **Falling**: pit checks for players and enemies, the fall, kill credit, rim coins, player damage + respawn + shield;
   a fall event on the wire (protocol bump); steering and bots avoid pits; tests.
3. **Look**: the Hazard rim in `Game/`, fall and respawn effects, minimap marks; `Tools/Preview` shots.
4. **Tune**: variant chance per cell, then the README.
