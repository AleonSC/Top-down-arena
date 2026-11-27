# Grid rooms: build the Expanding arena on a grid of door spots

**Nov 18: the size palette (Room, Gauntlet, Hall, Arena) and its code are gone; rooms are Grid pieces from your hand ([`GridPieces.md`](GridPieces.md)).**

Status: **built** (protocol 402; one lattice and a 4 x 3 cell core since the mockup pass): the lobby's **Building: Grid** switch, door spots, four sizes, host rule, bots, nodes and room chests on cells, preview `GRID=`, suite `gridrooms`. Decided with the owner on Nov 18, 2026 after a mockup
(free place / uniform grid / grid + big rooms): "the grid, with unique rooms bigger", as a new lobby switch beside Room cards, Free draw and Team vote.

## Why

- Free building gets chaotic (slivers, odd gaps, dead ends); the grid keeps every room joined to a wall on a known spot.
- A room is a spot and a size: nothing for the host to guess, and the map is easy to read and to plan on.

## Rules

| Question | Decision |
|---|---|
| The core | A Grid run starts with the start room grown to **4 x 3 cells (2400 x 1800)**: four free rooms round it (`Expansion.GridCore`, seeded by `TickGridCore` on a bare map, again after a map reset) with the walls between them and the start room opened (`GridCoreCuts`). They cost nothing and don't count towards the 24-room cap. Owner Nov 18: "make the core room bigger to compensate", to match the mockup. |
| Where a room goes | **One lattice** of 6-tile (600 px) cells from the core's corner (`GridX0`, `GridY0`). Every cell along the outside of a wall on a lattice line is a **door spot**; a room hangs off it flush with the wall, in the first alignment that fits (a big room can sit a cell to either side). The core has 14 spots (4 top, 4 bottom, 3 each side). |
| Sizes | **Room** 6 x 6, **Gauntlet** 12 x 6, **Hall** 6 x 12, **Arena** 12 x 12 (tiles). Same rules as any room (no overlap, a door's worth of shared wall, caps). |
| Cost and build | The new wall in vectors (`Expansion.Cost`), built at once like free building; no team vote. |
| Switch | `DiffKnob.GridRooms` (flavour, **the default since Nov 18**; Docs/GridPieces.md). Room cards win over it; the lobby's row keeps the four options exclusive. |
| Off in this mode | Drawn outlines, themes, wall push, room cards. Open wall, teleporter, gate and trap still work. |
| Nodes | `Expansion.GridNodeFor`: the middle of a cell two or three rooms out (the first ring one side each); `GridChestFor`: a chest at the middle of a free cell against the new room. Claiming a node puts its chest or relic at the middle of the room ([`Nodes.md`](Nodes.md), `Expansion.NodeSpotIn`). |

## Where it lives

- `Core/GridRooms.cs`: `GridShape`, `GridOption`, `Expansion.GridCore` / `GridCoreCuts` / `GridOptions` / `GridLegal` / `GridNodeFor` / `GridChestFor`, `Game.GridRoomsOn`, `TickGridCore`, `BotGridBuild`.
- `HostExpandDraw` refuses any box that is not a `GridOptions` spot (the client UI sends the same `Msg.ExpandDraw`); `HostExpandOutline` and wall push are off.
- `Game/GridRoomsUi.cs`: the palette, door-spot marks, the ghost under the mouse (`DrawExpansionDraw`'s grid mode, like cards mode).
- Preview: `EXPANDVOTE=0 EXPANDFREE=1 EXPANDDRAW=0,0,0,0 GRID=room|gauntlet|hall|arena [MOUSE=x,y] [EXPANDBUILT=x,y,w,h;...] Tools/Preview/shot.sh x hud`. Suite: `gridrooms`.

## Ideas for later

- Service and unique rooms (shop, Black Market, mini-boss) as grid footprints; themed rooms on the grid; an L or T footprint.
