# Fog of war: rooms stay dark until someone walks in

Status: **steps 1-3 built, reworked as a line-of-sight cone (protocol 420, Nov 20)**: `Core/RoomFog.cs`, `Game/RoomFogLook.cs`, suite `roomfog`, preview `FOG=1` (with `EXPANDMAP`). Step 4 waits for the hand-made layouts. Decided with the owner on Nov 19, 2026; the cone and slow exploring on Nov 20 (scope: unseen rooms only, shared by the team). Expanding arena only.

## The idea

A newly built room is **unseen**: you know its shape, because you built it, but not what's inside. It stays dark on the
minimap and the build map and dim in the world until the team has **explored** it with a line-of-sight cone (below). Then the
whole room is **seen** for the whole team, for the rest of the run. This prepares for hand-made room layouts with their own spawns (owner, later): what's
inside a room is the surprise.

## Decisions (Nov 19)

| Question | Decision |
|---|---|
| What's hidden | **Only room insides.** Nodes, chests on nodes, mystery doors and the lattice outside stay as they are. |
| Who sees a reveal | **The whole team.** One `Unseen` flag per room, set by the host. |
| Look while unseen | **Map and dim world:** dark with a `?` on the minimap and build map; dimmed in the world, its enemies and pickups hidden until you stand in its doorway. |
| Reward for entering first | **None:** the reward is seeing what's there. |
| Spawns | **Enemies still spawn in unseen rooms** (a real ambush). |
| Bots | **Scout first:** with nothing to fight, a bot walks to the nearest unseen room. |

## Rules (as built; the cone, Nov 20)

- **Starts seen:** the 4 x 3 core, and any room kept through a map reset.
- **Starts unseen:** every piece you build, a mystery door's room, a surprise room, a breach Stash and the mini-boss room.
- **The cone** (`RoomFog.Sees`): a standing player sees 90 degrees the way they aim (`ConeHalf`), `ConeReach` 900 px long, plus a
  `CloseReach` 170 px circle all round. Walls and fins stop it (`RoomFog.Blocked`, a segment against the world's wall boxes), so a
  doorway is a window into the next room. Everyone's cones count for everyone.
- **Explored cells:** each unseen box has a grid of `Cell` 25 px cells; the host (`ScanFog`, every 0.1 s) marks every cell a cone or
  circle sees. Synced in `Msg.FogCells` (a few times a second) and at the end of `Msg.ExpandState`; saved as `fog` lines.
- **Seen:** once `DoneShare` (90%) of a piece's cells are explored, every box of it is seen (`Unseen` false, the gold scan line).
- **Hiding:** an enemy, coin or orb in an unseen room is drawn only while a cone or circle is on it (`FogHides`), explored or not.
  Explored cells stay dimly lit (a layout memory), unexplored ones dark and hatched; the maps lighten with the share explored.
- **Bullets** are always drawn. Fog only hides what's inside, and never hides a hit you can take.
- **Bots** walk to the nearest unexplored cell when they have nothing to fight (`NearestUnseen`), turning to look at it.
- **A map reset** sets the fog back for every room that's rebuilt.

## Steps

1. **Core:** `ExpansionRoom.Unseen`; the host's reveal check each tick (`Game.TickRoomFog`); synced in `Msg.ExpandState` and in
   saves (protocol bump); suite `roomfog` (unseen at build, revealed by entry for the whole team, kept on rejoin and load).
2. **Map look:** unseen rooms drawn dark with a `?` on the minimap and the build map; previews `FOG=1` for `hud` and the build map.
3. **World look:** the dim veil over unseen rooms, the doorway peek, the reveal sweep; preview before/after.
4. **Hook for hand-made layouts:** a room's own contents (spawns, props) are placed at build time and show only on reveal.

## Dormant groups (owner, Nov 19; built, protocol 419)

When a piece card is built, each of its cells rolls `DormantGroups.CellChance` (40%) for a sleeping group of the faction enemies of
the wave it was built on (`Game.DormantPool`: the coming boss's faction as far as the wave reaches, no bosses, minis, planted
things or pairs). One type per group, `GroupSize` enemies (3, +1 every 15 waves, at most 6) in a ring round the cell's middle.
A dormant enemy (`Enemy.Dormant`, in the snapshot) doesn't move, shoot, recycle or count for the wave; bots ignore it. The room
seen, one of them hit, or a player seeing one from within `RoomFog.WakeReach` (600 px) wakes every group in the room. Woken between waves, they fight in the break; a level gained there is offered at once (`LevelUpPlayer`), so the countdown never waits on it. A map reset removes them. Saves don't keep them (enemies aren't saved).
Code: `Core/DormantGroups.cs`; look `DrawDormantMarks` (RoomFogLook.cs); preview `DORMANT=1`; tests in suite `roomfog`.
