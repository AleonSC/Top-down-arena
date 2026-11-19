# Fog of war: rooms stay dark until someone walks in

Status: **planned, nothing built.** Decided with the owner on Nov 19, 2026. Expanding arena only.

## The idea

A newly built room is **unseen**: you know its shape, because you built it, but not what's inside. It stays dark on the
minimap and the build map and dim in the world until a player walks in. Then the whole room is **seen** for the whole
team, for the rest of the run. This prepares for hand-made room layouts with their own spawns (owner, later): what's
inside a room is the surprise.

## Decisions (Nov 19)

| Question | Decision |
|---|---|
| What's hidden | **Only room insides.** Nodes, chests on nodes, mystery doors and the lattice outside stay as they are. |
| Who sees a reveal | **The whole team.** One `Seen` flag per room, set by the host. |
| Look while unseen | **Map and dim world:** dark with a `?` on the minimap and build map; dimmed in the world, its enemies and pickups hidden until you stand in its doorway. |
| Reward for entering first | **None:** the reward is seeing what's there. |

## Rules (first guess)

- **Starts seen:** the 4 x 3 core, and any room kept through a map reset.
- **Starts unseen:** every piece you build, a mystery door's room, a surprise room, a breach Stash and the mini-boss room.
  A service room (Shop, Black Market, Arms Dealer, Medic, Builder Shop) shows its sign on the map but stays dim inside until entered.
- **Reveal:** a standing player's centre inside any box of the piece reveals every box of it (a piece's boxes share a `Parent`).
  A short gold sweep runs across the room from the door you came in by.
- **Doorway peek:** a player within about 2 tiles of an unseen room's door sees enemies, bullets and pickups in about 6
  tiles of that door, so a fight at the threshold is never blind.
- **Bullets** that leave an unseen room are always drawn. Fog only hides what's inside, and never hides a hit you can take.
- **A map reset** sets the fog back for every room that's rebuilt.

## Steps

1. **Core:** `ExpansionRoom.Seen`; the host's reveal check each tick (`Game.TickFog`); synced in `Msg.ExpandState` and in
   saves (protocol bump); suite `fog` (unseen at build, revealed by entry for the whole team, kept on rejoin and load).
2. **Map look:** unseen rooms drawn dark with a `?` on the minimap and the build map; previews `FOG=1` for `hud` and the build map.
3. **World look:** the dim veil over unseen rooms, the doorway peek, the reveal sweep; preview before/after.
4. **Hook for hand-made layouts:** a room's own contents (spawns, props) are placed at build time and show only on reveal.

## Open questions

- Should enemies in an unseen room still spawn during waves (a real ambush), or wait until it's seen?
- Should bots treat unseen rooms differently (scout them first), or ignore the fog?
