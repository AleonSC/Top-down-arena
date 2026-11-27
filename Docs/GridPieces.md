# Grid pieces: room cards as Tetris pieces on the grid

Status: **plan agreed with the owner; steps 1-4 built, step 5 in part (Grid is the default)** (protocol 403, Nov 18, 2026). Builds on [`GridRooms.md`](GridRooms.md) (the lattice, the core, door spots) and replaces most of [`RoomCards.md`](RoomCards.md). Owner wants Grid to be the default and the cards reworked "like Tetris: pieces made to fit together, gaps filled with different pieces".

## Decisions (owner, Nov 18)

| Question | Decision |
|---|---|
| Default | **Grid is the default Building mode.** |
| Piece source | **A hand of pieces**, with the card sources that exist today (starting hand, +1 a round, wave and boss drops, chests, the Builder Shop). |
| Shapes | **Polyominoes of 1 to 4 cells** (a cell is a 6 x 6 tile room, 600 px): single, domino, the two trominoes (I, L), the five tetrominoes (I, O, T, S, L). Rotate (R) and mirror. |
| Placement | **Only at door spots**: one cell of the piece must sit on a door spot of a built room, the piece overlaps nothing and every cell is on the lattice. |
| Cards become pieces | The room cards **are** the grid pieces. A card is a **shape** plus any **modifiers** ([`PieceMods.md`](PieceMods.md)): a floor, combat, or both. An L piece can carry any of them. |
| Shops and the Black Market | **Always one cell** (the smallest piece), so they are easy to place. |
| Mini-bosses | They **spawn as preset rooms that fit the cell sizes** (cell-aligned, whole cells), so building up to one keeps the grid uniform. They **wait on the lattice** like a node: a locked cell-sized ghost ahead of the map; build pieces up to it and it joins, walk in to wake the mini-boss. |
| Price | **Playing a piece costs nothing.** Pieces can cost vectors to **buy** (the Builder Shop and the like) and still come as **drops and rewards**. Vectors keep paying for openings, portals, gates and mystery doors. |
| Inside a piece | **One open room**: the cells of a piece are open to each other, one rim round the whole piece (an L piece is an L-shaped room). |
| Old Room cards code | **Rework in place** (hand, sources, Builder Shop, bots, saves), then list what is left of the old cards (placement, obstacle layouts, lobby option) for the owner to approve deleting. Nothing is deleted before then. |

## What stays from Grid rooms

- The lattice from the core's corner, the free 4 x 3 cell core, 14 door spots, host-side legality (`Expansion.GridLegal` becomes a piece check), bots, nodes at a cell's middle, a claimed chest or relic at the middle of its room.
- The palette of four sizes (Room, Gauntlet, Hall, Arena) is replaced by the hand.

## Still open (small, settle while building)

- Modifiers: **built (Nov 20)**, see [`PieceMods.md`](PieceMods.md): a card carries floor and/or combat, rolled whenever it is offered; more kinds slot in through `PieceMods.All`.
- Mystery doors open any of the nine pieces (medium: domino, corner, square, T, S, L; Nov 18).
- Mini-boss rooms are two cells and one waits at a time (first guess; tune after play).

## Steps

1. **Built (protocol 403).** `Core/PieceShapes.cs`: `PieceShape` (Mono, Domino, TriI, TriL, TetI, TetO, TetT, TetS, TetL), `Expansion.PieceShapeOrients` (the 28 distinct turns and mirrors; an orientation is a small index), `PieceShapeRooms` (one open room of boxes from `FromTiles`, free, on the lattice), `GridSpots` / `PlacementsFor` (a cell of the piece on a door spot), `Msg.PieceShape`.
2. **Built (protocol 403).** `Core/GridPieces.cs`: a hand code from 32 up is a piece (`Expansion.MakePiece(piece, floor)` = 32 + floor x 16 + piece), so any piece is a plain combat room or a floor room; below it the old cards and the service cards (Shop, Market, Arms, Builder Shop), which build one cell in a Grid run; the Mini-boss card is not played in Grid (step 4). `Game.HandOn` (room cards or Grid) now gates the sources: the starting hand (one seeded plain piece, any 1 to 4 cell shape, since Nov 19; was a single, a domino and a tromino) and the round card (a domino after odd waves, a single after even; since protocol 408 offered as a wave pick of two beside a dropped card), wave drops and chests (`GridDropCard`: 1 in 5 a service card, a third tetrominoes, a quarter on a floor), the Black Market shelf (`GridCardOffers`, 6 vectors a cell, 8 more for a floor), the Builder Shop (`GridBuilderOffers`) and the boss pick (pieces, service cards and floor kits). Playing: `Msg.PieceShape` is (slot, orientation, x, y) and `HostPlayPiece` takes the card out of the hand and builds it free (`Game.PlayPiece`); bots play pieces (`BotPlayPiece`). Hands sync and save as raw codes. The inventory shows the hand as "Pieces". Tests: `Tests/PieceShapes.cs`. The build screen uses them from step 3.
3. **Built (no protocol change).** `Game/GridRoomsUi.cs`: the build screen's first tool is **Pieces**: your hand on the right (each card its cells, a floor piece's ground under its name, old cards dimmed), the picked piece as a ghost cell by cell on the door spot under the mouse, R steps through every turn and spot that fits there, a click plays it (`PlayPiece`); a pulsing mark on every door spot it fits; clicking a piece in the inventory opens it here. The size palette is gone: the host refuses drawn rooms in a Grid run and bots only play pieces (`GridOptions`, `GridLegal` and `BotGridBuild` stay for nodes and tests, listed for deletion in step 5). Preview `EXPANDVOTE=0 EXPANDFREE=1 EXPANDDRAW=0,0,0,0 GRID=1 [GRIDCARDS=mono,tett:burninggrounds,shop] [GRIDSEL=n] [MOUSE=x,y] shot.sh x hud`.
4. **Built (no protocol change).** Room types: a piece is a plain combat room or a floor room (step 2); Shop, Black Market, Arms Dealer and Builder Shop cards build one cell (step 2). **Mini-bosses** (`GridMiniFor`, `PlaceGridMini` from `PlaceFloatingRooms`): each boss cycle, if none waits, a sealed two-cell mini-boss room (1200 x 600 either way, `RoomKind.Gatekeeper`, `Floating`, its foe the coming boss's faction's) is placed on the lattice one cell out from the map; a piece into the cell between opens it and the duel works as before (`GatekeeperRoom.cs`). A sealed room has no door spots until it joins (`GridSpots`). Preview `GRIDMINI=1` with `GRID=1`.
5. **In part (Nov 18).** Grid is the lobby's default (`DiffKnob.GridRooms` on, `Pieces` off by default); a save from before keeps its old mode (`Difficulty.Parse`: no `GridRooms=` means Grid off and room cards on unless the save said otherwise; `ToText` now always writes both); the test harness keeps drawn rooms (`HostAs`). **Owner will decide on deletion later**: the old Room cards option, its shapes, placement and layouts (`Pieces.cs` shape cards, `RoomCardLayouts.cs`, `PiecesUi.cs` card mode), the Grid size palette (`GridOptions`/`GridLegal`/`BotGridBuild`, used now only by nodes and tests) and the free-draw paths stay until then. Balance pass after play.

## Polish (Nov 18, owner; mockups: artifact "Build Map Polish", picks C and C)

- **Core as one room** on the build map and the minimap: the openings between the start room and the core rooms draw no door bars (`CoreSeam`), the core rooms take the start room's fill and one outline is drawn round the core (`RoomsLook.cs`).
- **Grid on the map** (pick C): the lattice's cell lines, faint, fading out 15 tiles from the map's edge (`DrawGridLattice`, `GridRoomsUi.cs`), in place of the old 5-tile grid in a Grid run.
- **Placing a piece** (pick C): the new rooms slide in from a cell out and slam into the wall: a white flash, the wall they join pulsing gold, sparks at the door (`DrawPlaceFx`; on the build map, every player sees each new piece).
- **Tool rail**: Teleporter and Trap show only while you own one; **Open wall is gone in a Grid run** (`Game.OpenWallsOn`; the host refuses cuts, and the between-waves doors-only screen never opens).
- **Mystery doors** in a Grid run: the room behind is a piece on the lattice (`Mystery.PiecesOf`: small one cell, medium a domino or corner, the hallway a line of 3 or 4; `Expansion.GridMysteryOptions`), so a shop or Black Market behind one is exactly one cell; a door goes if a room is built on its piece (no re-plan).
- **Surprise and breach rooms** in a Grid run (owner Nov 18): `Expansion.SurpriseFor` / `SurpriseOptionsFor` (grid: `GridSurpriseOptions`, `PieceShapeOfSurprise`): a boss's surprise room is a 4-cell piece (L hall an L, T hall and Cross a T, Ring and Chamfer a square), a breach's Stash is the one cell behind it, the gold breach event's room and the Engineer's Breach room are pieces on the lattice too. Test `GridSurpriseTest`.
- **Old code deleted (Nov 18, owner):** the old room cards (shape cards, `CardRooms`/`CardOptions`, `RoomCardLayouts.cs`, card mode on the build screen, `PlayRoomCard`/`HostPlayCard`/`BotPlayCard`, the old Black Market, Builder and boss offers; `Msg.PlayCard` is kept in the enum but ignored, `DiffKnob.RoomCards` kept but retired) and the Grid size palette (`GridShape`, `GridOptions`, `GridLegal`, `BotGridBuild`; nodes and room chests now use `PlacementsFor` of a single). Free drawing stays for old saves.
