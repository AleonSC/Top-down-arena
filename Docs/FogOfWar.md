# Fog of war: rooms stay dark until someone walks in

Status: **steps 1-3 built** (protocol 416): `Core/RoomFog.cs`, `Game/RoomFogLook.cs`, suite `roomfog`, preview `FOG=1` (with `EXPANDMAP`). Step 4 waits for the hand-made layouts. Decided with the owner on Nov 19, 2026. Expanding arena only.

## The idea

A newly built room is **unseen**: you know its shape, because you built it, but not what's inside. It stays dark on the
minimap and the build map and dim in the world until a player walks in. Then the whole room is **seen** for the whole
team, for the rest of the run. This prepares for hand-made room layouts with their own spawns (owner, later): what's
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

## Rules (as built)

- **Starts seen:** the 4 x 3 core, and any room kept through a map reset.
- **Starts unseen:** every piece you build, a mystery door's room, a surprise room, a breach Stash and the mini-boss room.
  A service room (Shop, Black Market, Arms Dealer, Medic, Builder Shop) shows its sign on the map but stays dim inside until entered.
- **Reveal:** a standing player's centre inside any box of the piece reveals every box of it (a piece's boxes share a `Parent`).
  A gold scan line lifts the veil left to right (0.6 s).
- **Peek:** an enemy, coin or orb in an unseen room shows to any standing player within 4.5 tiles of it (`RoomFog.PeekReach`),
  so a fight at the doorway is never blind.
- **Bullets** are always drawn, even inside an unseen room. Fog only hides what's inside, and never hides a hit you can take.
- **A map reset** sets the fog back for every room that's rebuilt.

## Steps

1. **Core:** `ExpansionRoom.Unseen`; the host's reveal check each tick (`Game.TickRoomFog`); synced in `Msg.ExpandState` and in
   saves (protocol bump); suite `roomfog` (unseen at build, revealed by entry for the whole team, kept on rejoin and load).
2. **Map look:** unseen rooms drawn dark with a `?` on the minimap and the build map; previews `FOG=1` for `hud` and the build map.
3. **World look:** the dim veil over unseen rooms, the doorway peek, the reveal sweep; preview before/after.
4. **Hook for hand-made layouts:** a room's own contents (spawns, props) are placed at build time and show only on reveal.
