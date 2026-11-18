# Grid pieces: room cards as Tetris pieces on the grid

Status: **plan agreed with the owner; steps 1-4 built, step 5 in part (Grid is the default)** (protocol 403, Nov 18, 2026). Builds on [`GridRooms.md`](GridRooms.md) (the lattice, the core, door spots) and replaces most of [`RoomCards.md`](RoomCards.md). Owner wants Grid to be the default and the cards reworked "like Tetris: pieces made to fit together, gaps filled with different pieces".

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

## Still open (small, settle while building)

- Which room types a piece can roll beyond plain and floor (a combat variant?), and how a themed piece shows on the card before you play it.
- Mini-boss rooms are two cells and one waits at a time (first guess; tune after play).

## Steps

1. **Built (protocol 403).** `Core/GridPieces.cs`: `GridPiece` (Mono, Domino, TriI, TriL, TetI, TetO, TetT, TetS, TetL), `Expansion.GridPieceOrients` (the 28 distinct turns and mirrors; an orientation is a small index), `GridPieceRooms` (one open room of boxes from `FromTiles`, free, on the lattice), `GridSpots` / `GridPieceOptions` (a cell of the piece on a door spot), `Msg.GridPiece`.
2. **Built (protocol 403).** `Core/GridCards.cs`: a hand code from 32 up is a piece (`Expansion.PieceCard(piece, floor)` = 32 + floor x 16 + piece), so any piece is a plain combat room or a floor room; below it the old cards and the service cards (Shop, Market, Arms, Builder Shop), which build one cell in a Grid run; the Mini-boss card is not played in Grid (step 4). `Game.HandOn` (room cards or Grid) now gates the sources: the starting hand (a single, a domino, a seeded tromino) and the round card (a domino after odd waves, a single after even), wave drops and chests (`GridDropCard`: 1 in 5 a service card, a third tetrominoes, a quarter on a floor), the vendor shelf (`GridCardOffers`, 6 vectors a cell, 8 more for a floor), the Builder Shop (`GridBuilderOffers`) and the boss pick (pieces, service cards and floor kits). Playing: `Msg.GridPiece` is (slot, orientation, x, y) and `HostGridCard` takes the card out of the hand and builds it free (`Game.PlayGridCard`); bots play pieces (`BotPlayGridCard`). Hands sync and save as raw codes. The inventory shows the hand as "Pieces". Tests: `Tests/GridPieces.cs`. The build screen uses them from step 3.
3. **Built (no protocol change).** `Game/GridRoomsUi.cs`: the build screen's first tool is **Pieces**: your hand on the right (each card its cells, a floor piece's ground under its name, old cards dimmed), the picked piece as a ghost cell by cell on the door spot under the mouse, R steps through every turn and spot that fits there, a click plays it (`PlayGridCard`); a pulsing mark on every door spot it fits; clicking a piece in the inventory opens it here. The size palette is gone: the host refuses drawn rooms in a Grid run and bots only play pieces (`GridOptions`, `GridLegal` and `BotGridBuild` stay for nodes and tests, listed for deletion in step 5). Preview `EXPANDVOTE=0 EXPANDFREE=1 EXPANDDRAW=0,0,0,0 GRID=1 [GRIDCARDS=mono,tett:burninggrounds,shop] [GRIDSEL=n] [MOUSE=x,y] shot.sh x hud`.
4. **Built (no protocol change).** Room types: a piece is a plain combat room or a floor room (step 2); Shop, Black Market, Arms Dealer and Builder Shop cards build one cell (step 2). **Mini-bosses** (`GridMiniFor`, `PlaceGridMini` from `PlaceFloatingRooms`): each boss cycle, if none waits, a sealed two-cell mini-boss room (1200 x 600 either way, `RoomKind.Gatekeeper`, `Floating`, its foe the coming boss's faction's) is placed on the lattice one cell out from the map; a piece into the cell between opens it and the duel works as before (`GatekeeperRoom.cs`). A sealed room has no door spots until it joins (`GridSpots`). Preview `GRIDMINI=1` with `GRID=1`.
5. **In part (Nov 18).** Grid is the lobby's default (`DiffKnob.GridRooms` on, `RoomCards` off by default); a save from before keeps its old mode (`Difficulty.Parse`: no `GridRooms=` means Grid off and room cards on unless the save said otherwise; `ToText` now always writes both); the test harness keeps drawn rooms (`HostAs`). **Owner will decide on deletion later**: the old Room cards option, its shapes, placement and layouts (`RoomCards.cs` shape cards, `RoomCardLayouts.cs`, `RoomCardsUi.cs` card mode), the Grid size palette (`GridOptions`/`GridLegal`/`BotGridBuild`, used now only by nodes and tests) and the free-draw paths stay until then. Balance pass after play.

