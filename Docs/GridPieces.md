# Grid pieces: room cards as Tetris pieces on the grid

Status: **plan agreed with the owner; step 1 built** (protocol 403, Nov 18, 2026). Builds on [`GridRooms.md`](GridRooms.md) (the lattice, the core, door spots) and replaces most of [`RoomCards.md`](RoomCards.md). Owner wants Grid to be the default and the cards reworked "like Tetris: pieces made to fit together, gaps filled with different pieces".

## Decisions (owner, Nov 18)

| Question | Decision |
|---|---|
| Default | **Grid is the default Building mode.** |
| Piece source | **A hand of pieces**, with the card sources that exist today (starting hand, +1 a round, wave and boss drops, chests, the vendor). |
| Shapes | **Polyominoes of 1 to 4 cells** (a cell is a 6 x 6 tile room, 600 px): single, domino, the two trominoes (I, L), the five tetrominoes (I, O, T, S, L). Rotate (R) and mirror. |
| Placement | **Only at door spots**: one cell of the piece must sit on a door spot of a built room, the piece overlaps nothing and every cell is on the lattice. |
| Cards become pieces | The room cards **are** the grid pieces. A card is a **shape** plus a **room type**: a basic room, a floor-modifier (themed) room, a combat room, and so on. An L piece can be any of them. |
| Shops and vendors | **Always one cell** (the smallest piece), so they are easy to place. |
| Mini-bosses | They **spawn as preset rooms that fit the cell sizes** (cell-aligned, whole cells), so building up to one keeps the grid uniform. They **wait on the lattice** like a node: a locked cell-sized ghost ahead of the map; build pieces up to it and it joins, walk in to wake the mini-boss. |
| Price | **Playing a piece costs nothing.** Pieces can cost vectors to **buy** (the Builder vendor and the like) and still come as **drops and rewards**. Vectors keep paying for openings, portals, gates and mystery doors. |
| Inside a piece | **One open room**: the cells of a piece are open to each other, one rim round the whole piece (an L piece is an L-shaped room). |
| Old Room cards code | **Rework in place** (hand, sources, vendor, bots, saves), then list what is left of the old cards (placement, obstacle layouts, lobby option) for the owner to approve deleting. Nothing is deleted before then. |

## What stays from Grid rooms

- The lattice from the core's corner, the free 4 x 3 cell core, 14 door spots, host-side legality (`Expansion.GridLegal` becomes a piece check), bots, nodes and room chests at a cell's middle, a claimed chest or relic at the middle of its room.
- The palette of four sizes (Room, Gauntlet, Hall, Arena) is replaced by the hand.
