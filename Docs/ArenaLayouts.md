# Arena layouts

Mockups: [`Mockups/arena-layouts.html`](Mockups/arena-layouts.html) (https://claude.ai/artifact/UyfbbAwAhNxfg3WEUrRniy).
Player reference: README "Arena layouts". Code: `Layouts.cs`, `LayoutRules.cs`, `Game/LayoutLook.cs` (protocol 171).

## Decisions (from the owner)

1. **Enemy gates.** Holographic red parts of the edge wall; enemies come in through them. Player gates (teal) are the only
   gaps you can wrap through.
2. Gates pairing with a different spot (portal style): not yet.
3. Walls and pillars block your shots too. Crates stay one-way cover.
4. A layout lasts one boss cycle and is picked at random (never the same twice in a row). Boss waves are open.
5. Random crates, pots and barrels still spawn in the gaps.
6. Paths and layouts mix freely.
7. All six are built.

## How it works

- **Data** (`Layouts.cs`): a layout is walls (boxes), pillars (discs), starting crates, an edge set and the sides
  enemies may use. The edge wall is generated from the edge set (the whole edge minus the player gates; the enemy
  gates sit inside it and are solid). Waypoints (box corners, 8 spots round each pillar, pushed out 30 px) are
  precomputed for steering.
- **Which one** (`PickLayout`, host, at the start of each wave): boss waves, the Practice Range and the Showcase are
  open. Otherwise the cycle is `(Wave - 1) / 5`; a new cycle picks at random excluding the last. `Game.Layout` is one
  byte at the end of every snapshot, so there is no extra message. Challenge rooms count as open while you're in one.
- **Walking**: `PushOutOfProps` (players and non-boss enemies) ends with `PushOutOfSolids`. The edge wall only ever
  pushes inward, so a player who lands in it (a blink, a knockback) isn't thrown out and wrapped across the arena.
  A player's wrap is unchanged: a gap in the wall lets them cross the edge, anywhere else the wall holds them.
- **Shots**: in `Collide`, any bullet touching a wall or pillar is removed (grenades, boulders, medkits and flak shells
  fly over, like they do crates). Beams and melee arcs ignore walls for now.
- **Spawns**: `SpawnEnemy`, squads (`SpawnSquadAt`) and the Pincer's Hexers use the enemy gates when the edges are
  gated, and the layout's allowed sides otherwise. Bosses and designed waves are unchanged.
- **Steering** (`LayoutDir`, from `MoveEnemy`): if the straight line to the target crosses a wall, head for the visible
  waypoint with the shortest way round, and keep it for 0.5 s. Bosses don't steer (they only appear on open waves).
- **Narrow Pass, Tiny Arena, Solid Walls** (`Walled`): the edge wall and the gates drop away; inner walls stay.

## Known limits and ideas

- Bots don't path round walls; they can get stuck behind them.
- Enemy beams (Beam Sentry) and the Reaver's cone pass through walls.
- Enemies standing behind a wall still fire (their shots stop at it).
- No lobby switch to turn layouts off. A Difficulty-tab toggle and a "pick the layout" host option are easy to add.
- Gate portals (a gate that leads to a different spot) are for later.
