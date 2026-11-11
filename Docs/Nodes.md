# Nodes: map points you build a room around

Status: **steps 1-3 built (protocol 282)**, step 4 open. **Changed Nov 11 (owner): shop and vendor are tiny 4 x 4 tile sealed rooms you open and walk into, not nodes; only chests stay nodes** (see the end). Decided with the owner on Nov 11, 2026. Replaces the floating shop and vendor rooms of
[`FloatingRooms.md`](FloatingRooms.md); the Gatekeeper (mini-boss) stays a sealed room.

## The idea

A **node** is a point in empty space (no walls, no size): chest, shop, vendor, ... It is locked until a built room **fully encloses** it.
Then it becomes a fixture inside that room, at the room's size. Mini-bosses stay sealed rooms as today.

## Decisions

| Question | Decision |
|---|---|
| Placement | Far from the start room and from each other. Start: 10-16 tiles from the nearest connected room, at least 8 tiles between nodes (first guesses, tune with `vecbal`). Never next to the start. |
| Enemies | **Shop and vendor nodes: no spawns** in the room (`SpawnWeight` 0, as now). **Chests and other nodes: the room spawns enemies as normal.** |
| Respawn | **None**, for nodes and for the existing sealed rooms. Each is placed once and stays until claimed. |
| New nodes | May appear **around the current build** as it grows (each boss cycle, placed relative to the built map, never inside it). They are new nodes, not respawns. |
| Mini-bosses | Unchanged: sealed rooms, touch to open, duel, prize pick. |

## Build rules

- A node counts as enclosed when its point is inside a built room's box (not on a wall). The draw tool allows boxes that cover a node.
- Cost is the usual room price; there is no node fee.
- **Rooms only** (owner, Nov 11): a hallway never claims a node; it takes a room (wider than 3 tiles, or a drawn outline) round it.
- Claiming: shop/vendor open their screen while you stand in the room; a chest opens when walked to, then is spent (the room stays).

## Plan (one step at a time)

1. `MapNode` data + placement (`Expansion`), seeded like `FloatingFor`, no respawn; remove shop/vendor floating rooms. **Protocol bump** (`Msg.ExpandState`).
2. Enclose check, fixture inside the room, spawn rules per kind.
3. Look: ghost marker in the arena, minimap and draw screen (`Tools/Preview` shots first).
4. Bots build around nodes; `vecbal` balance pass; README.

## Step 1 (built, protocol 282)

- `MapNode` / `NodeKind` (Shop, Vendor, Chest; chests aren't placed yet) in `Expansion.cs`; `Game.Nodes` synced after the kits in `Msg.ExpandState`, saved as `node` lines.
- `Expansion.NodeFor(seed, cycle, built, nodes, kind)`: 10-16 tiles straight out from a connected room (so a straight 3-tile hallway reaches it: `NodeHall`), 2 tiles clear of every room,
  8+ tiles from other nodes; one shop and one vendor node per boss cycle (`PlaceFloatingRooms`), at most 4 unclaimed. The Gatekeeper stays a floating room.
- Claiming: `Expansion.Build(..., nodes)` makes a Combat room that encloses a shop or vendor node a Shop or Vendor room (all boxes of a multi-box room), so spawns, gold and the shop screens work unchanged.
- Bots build the cheapest straight hallway to the nearest unclaimed node (`BotBuildsToFloater`).
- Look: breathing ring and mark on the arena floor, a square on the minimap and the draw screen with its name (`RoomsLook.cs`, `ExpansionUi.cs`). Preview: `EXPANDFLOAT=1` with `EXPANDVOTE=0 EXPANDDRAW=0,-1200,300,1200`.
- Not yet: a fixture drawn inside the claimed room (step 3).
- Old saves with floating shop or vendor rooms still load and work as before.

## Step 2 (built, protocol 282)

- **Rooms only**: `Expansion.NodeEncloses`; `Build(..., nodes)` and `NodeClaimed` use it. `NodeStep` is the next box toward a node: a 5 x 5 tile room round it if it reaches back to a connected
  room, else the straight hallway out to where that room would start; `NodeReachable` checks (at most 4 boxes) that a node can be claimed. Bots follow `NodeStep`.
- **Chests**: one chest node per boss cycle (owner: one, enemies spawn). A claimed one holds a real treasure chest prop (`TickNodes`, host: `AddProp(Chest)` at the node, put back each wave
  until opened; `Props.cs` gives each player their own item). Opening it spends the node (`MapNode.Spent`, synced and saved); spent chests draw dim. The wave never waits on it. Max waiting nodes 6.
- No cover within 90 px of a node (`Expansion.NodeClear`). Spawn rules need no change: a Shop or Vendor room spawns nothing, a chest's room is a Combat room.
- Tests (suite `expand`): hallway first then room, claims only with the node in the map, chest opens once.

## Step 3 (built, no protocol change)

- `Game/NodeLook.cs`: an unclaimed node is a breathing ring and mark; a claimed one is a fixture of its room: a shop's stall (awning, counter, coin), a vendor's pedestal (plus, turning dashes),
  a chest (glow and a walk-up ring; opened: lid up, dim). Chest ink is violet on the map, minimap and draw screen too.
- Previews: `EXPANDMAP=0 EXPANDROOMS="1600,200,700,500" EXPANDNODE="shop:1950,450;chest:1750,450:spent" EXPANDAT=1850,500` (mode hud).

## Open

- Whether chest gold and vectors should scale harder (balance pass with `vecbal`).

## Shop and vendor as tiny rooms (built, no protocol change)

- Owner, Nov 11: "the shop nodes should be tiny square rooms that you walk into instead of nodes you build around". Shop **and** vendor (chests stay nodes), **4 x 4 tiles** (`Expansion.ShopRoomSide`).
- They are floating rooms again (`FloatingFor`, as the Gatekeeper's, sealed until a built box shares 2 tiles of wall): `PlaceFloatingRooms` places Gatekeeper, shop and vendor rooms (at most `MaxSealed` waiting) and only chest nodes.
- An opened shop or vendor room draws the node's stall or pedestal in its middle (`DrawFloatingMark` -> `DrawNodeFixture`); the draw screen's legend lists Shop room / Vendor room.
- Bots: `BotBuildsToFloater` ranks shop, vendor, chest, Gatekeeper across nodes and sealed rooms (`ConnectorFor` for rooms).
- Old saves with shop or vendor nodes still load and claim as before. Preview: `EXPANDMAP=0 EXPANDFLOAT=3` (hud, you stand in the opened shop) or with `EXPANDVOTE=0 EXPANDDRAW=0,-1200,300,1200` (draw screen).
