# Nodes: map points you build a room around

Status: **Nov 20 (owner): the health shrine is the only node placed; chests come from rooms (see the end), protocol 426.** **steps 1-3 built (protocol 282)**, node tiers built (no protocol change), step 4 open; fog, scout towers and gold veins built (protocol 386), **scout towers and gold veins retired Nov 20 (owner): not placed, not drawn, no code; an old save's tower or vein loads as a chest**. **Nov 18 (owner): fog off, no scout towers, no floating shop / Black Market / Gatekeeper rooms (room cards and mystery doors only), bigger node marks** (see the end). **Changed Nov 11 (owner): shop and Black Market are tiny 4 x 4 tile sealed rooms you open and walk into, not nodes; only chests stay nodes** (see the end). Decided with the owner on Nov 11, 2026. Replaces the floating shop and Black Market rooms of
[`FloatingRooms.md`](FloatingRooms.md); the Gatekeeper (mini-boss) stays a sealed room.

## The idea

A **node** is a point in empty space (no walls, no size): chest, shop, Black Market, ... It is locked until a built room **fully encloses** it.
Then it becomes a fixture inside that room, at the room's size. Mini-bosses stay sealed rooms as today.

## Decisions

| Question | Decision |
|---|---|
| Placement | Far from the start room and from each other. Start: 10-16 tiles from the nearest connected room, at least 8 tiles between nodes (first guesses, tune with `vecbal`). Never next to the start. |
| Enemies | **Shop and Black Market nodes: no spawns** in the room (`SpawnWeight` 0, as now). **Chests and other nodes: the room spawns enemies as normal.** |
| Respawn | **None**, for nodes and for the existing sealed rooms. Each is placed once and stays until claimed. |
| New nodes | May appear **around the current build** as it grows (each boss cycle, placed relative to the built map, never inside it). They are new nodes, not respawns. |
| Mini-bosses | Unchanged: sealed rooms, touch to open, duel, prize pick. |

**Nov 19 (owner): room chests are gone.** Building a room, a surprise room or a breach event's room no longer drops a chest node; chests come only from the map's nodes. (What this note said before: every built room could drop a chest node 3-5 tiles outside its wall.)

## Build rules

- A node counts as enclosed when its point is inside a built room's box (not on a wall). The draw tool allows boxes that cover a node.
- Cost is the usual room price; there is no node fee.
- **Rooms only** (owner, Nov 11): a hallway never claims a node; it takes a room (wider than 3 tiles, or a drawn outline) round it.
- Claiming: shop/Black Market open their screen while you stand in the room; a chest opens when walked to, then is spent (the room stays).

## Plan (one step at a time)

1. `MapNode` data + placement (`Expansion`), seeded like `FloatingFor`, no respawn; remove shop/Black Market floating rooms. **Protocol bump** (`Msg.ExpandState`).
2. Enclose check, fixture inside the room, spawn rules per kind.
3. Look: ghost marker in the arena, minimap and draw screen (`Tools/Preview` shots first).
4. Bots build around nodes; `vecbal` balance pass; README.

## Step 1 (built, protocol 282)

- `MapNode` / `NodeKind` (Shop, Vendor, Chest; chests aren't placed yet) in `Expansion.cs`; `Game.Nodes` synced after the kits in `Msg.ExpandState`, saved as `node` lines.
- `Expansion.NodeFor(seed, cycle, built, nodes, kind)`: 10-16 tiles straight out from a connected room (so a straight 3-tile hallway reaches it: `NodeHall`), 2 tiles clear of every room (chest and relic nodes 6 tiles, `PrizeClear`; `FloatingFor` keeps floating rooms as far from them),
  8+ tiles from other nodes; one shop and one Black Market node per boss cycle (`PlaceFloatingRooms`), at most 4 unclaimed. The Gatekeeper stays a floating room.
- Claiming: `Expansion.Build(..., nodes)` makes a Combat room that encloses a shop or Black Market node a Shop or Vendor room (all boxes of a multi-box room), so spawns, gold and the shop screens work unchanged.
- Bots build the cheapest straight hallway to the nearest unclaimed node (`BotBuildsToFloater`).
- Look: breathing ring and mark on the arena floor, a square on the minimap and the draw screen with its name (`RoomsLook.cs`, `ExpansionUi.cs`). Preview: `EXPANDFLOAT=1` with `EXPANDVOTE=0 EXPANDDRAW=0,-1200,300,1200`.
- Nov 12 (owner): nodes and shop/Black Market rooms placed off the start room sit `Expansion.StartExtra` (6 tiles) farther out (`ConnectorFor` reaches that much farther).
- Nov 13 (owner): `StartExtra` 10 tiles, the Gatekeeper too; nothing (node or floating room) is placed within `Expansion.StartClear` (10 tiles) of the start room, whichever room it was placed off; the map cap went from 6400 x 4000 to 8000 x 5000 so they still fit.
- Not yet: a fixture drawn inside the claimed room (step 3).
- Old saves with floating shop or Black Market rooms still load and work as before.

## Step 2 (built, protocol 282)

- **Rooms only**: `Expansion.NodeEncloses`; `Build(..., nodes)` and `NodeClaimed` use it. `NodeStep` is the next box toward a node: a 5 x 5 tile room round it if it reaches back to a connected
  room, else the straight hallway out to where that room would start; `NodeReachable` checks (at most 4 boxes) that a node can be claimed. Bots follow `NodeStep`.
- **Chests**: one chest node per boss cycle (owner: one, enemies spawn). A claimed one holds a real treasure chest prop (`TickNodes`, host: `AddProp(Chest)` at the node the moment its room is built and every wave tick, put back each wave
  until opened; `Props.cs` gives each player their own item). Opening it spends the node (`MapNode.Spent`, synced and saved); spent chests draw dim. The wave never waits on it. Max waiting nodes 6.
- No cover within 90 px of a node (`Expansion.NodeClear`). Spawn rules need no change: a Shop or Vendor room spawns nothing, a chest's room is a Combat room.
- Tests (suite `expand`): hallway first then room, claims only with the node in the map, chest opens once.

## Step 3 (built, no protocol change)

- `Game/NodeLook.cs`: an unclaimed node is a breathing ring and mark; a claimed one is a fixture of its room: a shop's stall (awning, counter, coin), a Black Market's pedestal (plus, turning dashes),
  a chest (glow and a walk-up ring; opened: lid up, dim). Chest ink is violet on the map, minimap and draw screen too.
- Previews: `EXPANDMAP=0 EXPANDROOMS="1600,200,700,500" EXPANDNODE="shop:1950,450;chest:1750,450:spent" EXPANDAT=1850,500` (mode hud).

## Open

- Whether chest gold and vectors should scale harder (balance pass with `vecbal`).

## Shop and Black Market as tiny rooms (built, no protocol change)

- Owner, Nov 11: "the shop nodes should be tiny square rooms that you walk into instead of nodes you build around". Shop **and** Black Market (chests stay nodes), **4 x 4 tiles** (`Expansion.ShopRoomSide`).
- They are floating rooms again (`FloatingFor`, as the Gatekeeper's, sealed until a built box shares 2 tiles of wall): `PlaceFloatingRooms` places Gatekeeper, shop and Black Market rooms (at most `MaxSealed` waiting) and only chest nodes.
- An opened shop or Black Market room draws the node's stall or pedestal in its middle (`DrawFloatingMark` -> `DrawNodeFixture`); the draw screen's legend lists Shop room / Vendor room.
- Bots: `BotBuildsToFloater` ranks shop, Black Market, chest, Gatekeeper across nodes and sealed rooms (`ConnectorFor` for rooms).
- Old saves with shop or Black Market nodes still load and claim as before. Preview: `EXPANDMAP=0 EXPANDFLOAT=3` (hud, you stand in the opened shop) or with `EXPANDVOTE=0 EXPANDDRAW=0,-1200,300,1200` (draw screen).

## Relic nodes (prototype, protocol 304)

- Owner (Nov 12): node-earned **relics**, separate from boss weapon mods: **one relic slot per player**, a new one replaces the old. First relic: **Wall Bounce**.
- `NodeKind.Relic`: one per **even** boss cycle (0, 2, 4, ...), placed like chests (`PlaceFloatingRooms`). Claimed the same way (a room round it); the first player to walk
  up to it (`Relics.TakeReach`) gives **that player** its relic (nov 18, owner: relics are per player, not shared). Their old relic is left in the node (`MapNode.Drop`, protocol 401; `Relics.Held`) to be taken by anyone, and the swapper must walk out of reach before swapping back; with an empty slot the node is spent (`Relics.cs` `TickRelicNodes`).
- Wall Bounce: a player's shot that reaches a wall or pillar bounces once (mirrored off the face), keeping 70% damage (`Relics.WallBounces`, `WallBounceDmg`).
- Nov 17 (owner): two more relics, and each node rolls its relic (`Relics.ForNode`: from the node's spot; protocol 387), named in the build map's tip:
  **Backup** (an auto revive: the first time you'd go down you're back up at 50% health with 2 s grace, then the slot empties; `UseBackup` from `DownIfDead`) and
  **Hallway Runner** (+25% move speed in a box 3 tiles wide or less; `RelicSpeedAt`, `Relics.InHall`).
- Open: more relics (Home Turf, Gatecrasher, Portal Ward, Spiked Walls were offered), a pick-up banner, bots walking to relic nodes.

## Fog, scout towers and gold veins (built, protocol 386; towers and veins retired Nov 20, node fog off since Nov 18)

- Owner (Nov 17): picked fog (7), scout tower (4) and gold vein (1) from the node ideas. Decisions: fog hides the **kind only** (a grey "?" where the node is); a node is
  revealed for everyone by a player within **8 tiles** or a claimed scout tower; the tower is a **permanent radar** (30 tiles, nodes placed later too); the vein is **plain passive** gold.
- `MapNode.Hidden` (synced after `Cleared`, saved as a 7th `node` field; old saves and hand-made nodes are unfogged). `NodeFor` places every node fogged.
- `NodeKind.Tower` (odd boss cycles, in place of the relic node) and `NodeKind.Vein` (every cycle); both keep `PrizeClear` from rooms like chests. `MaxNodesWaiting` 6 -> 8.
- `NodeScouting.cs`: `TickNodeFog` (host, from `TickExpansion`, every 0.25 s; also reveals a claimed node), `PayVeins` (from `WaveCleared`: `VeinGold` 10 + 1 per wave to each player, per vein).
- Look (`NodeLook.cs`): fogged ghost ("?" with drifting mist dashes; a fogged rule node keeps its area, grey, with a "?" mark), tower (mast, sweeping beam, ping), vein (rocks and
  nuggets, sparkles); grey on the minimap and build map, "UNKNOWN NODE" / "SCOUT TOWER" / "GOLD VEIN" in the legend, tips name and explain them.
- Bots still build toward fogged nodes. Not done: the tower's range drawn on the build map; numbers are first guesses (tune with `vecbal`).
- Preview: `EXPANDMAP=0 EXPANDROOMS="1600,200,900,500" EXPANDAT=2400,450 EXPANDNODE="tower:2850,250;vein:2850,650;chest:2950,450:hidden"` (hud).

## Nov 18 (owner): no room nodes, no fog, bolder nodes (no protocol change)

- "Room nodes ... delegated to only room card placement or mystery rooms": `PlaceFloatingRooms` no longer places shop, Black Market or Gatekeeper rooms; old saves keep theirs.
- Fog removed: `NodeFor` places nodes unfogged; `TickNodeFog` clears an old save's fogged node at once. Scout towers are no longer placed (odd cycles: chest, rule, vein). `MapNode.Hidden` stays on the wire for old saves.
- Look (`NodeLook.cs`): an unclaimed node is a lit disc, a bold double ring, turning dashes and two beacon rings rippling out, its mark at full size; the minimap square is 10 px with a dark edge and halo (`RoomsLook.cs`).

**Nov 18 (owner): no rule nodes by default; a ring of prize nodes round the hub.** `PlaceFloatingRooms` no longer places rule nodes (the modified-floor rooms). The first boss cycle instead rings the hub with six chest, vector chest and relic nodes, one per side in turn, 7-10 tiles off the hub wall (`RingMin`/`RingMax`) (`Expansion.NodeFor(..., ring)`: `ring % 4` is the side off the hub); later cycles add a chest, a relic every other cycle and a gold vein. Old saves and drawn rule nodes still work.

**Nov 18 (owner): chests and relics stand in the middle of their room.** The node point only says where to build; once a room encloses a chest, vector chest or relic node, the fixture stands at the middle of that room (a multi-box room: the middle of its outline if that is floor, else of the box holding the node), moved to the nearest half-tile point clear of cover so a card room's pillars and blocks stay (`Expansion.NodeSpotIn`, `World.NodeSpots`, `Game.NodeSpot`). The chest kind and the relic still roll from the node's own point, so what a ghost marker shows is what you get. Reach checks, the chest prop, the burst and the look all use the spot. No wire change.
Hallways (3 tiles wide or less) are the exception: their chest stays where it was put, so a mystery door's hallway keeps its chest at the far end.

## Node tiers (owner Nov 18, no protocol change)

- A prize node's tier is its distance from the hub's wall (`Expansion.NodeTier`, `TierMid` 15 / `TierOuter` 30 tiles): a pure function of its point, so nothing new on the wire or in saves.
- What it holds follows the tier: **inner** Gold chest or vector chest, **middle** Grid piece or Buildables chest, **outer** Upgrade chest or relic. `Expansion.Tiered` sets the kind of every chest, vector chest and relic node the host places (the first-cycle ring, each cycle's nodes, room chests); `NodeChestKind` rolls the chest kind by tier. Veins are untouched.
- Look (`DrawNodeTier`, `NodeLook.cs`): HUD corner brackets in the tier's metal (bronze, silver, gold) and a row of three square pixel cells under the node, filled up to its tier, with a blinking cursor cell; the tip starts with the tier; on the minimap an unclaimed prize node's square has a tier-metal edge and halo (`RoomsLook.cs`); on the build map a tier-metal frame, three small cells under it and the tiers in the legend (`ExpansionUi.cs`).
- Preview: `EXPANDMAP=0 EXPANDNODE="chest:2600,450;chest:3700,450;relic:4800,450" EXPANDAT=3950,450 ZOOM=1.3 shot.sh x hud 3`. Tests in suite `chests`.
- Zones (Nov 19, owner): the tier is the farther of the two axes off the hub's box (square zones, not rounded). (The ground drawing of the zones, `TierBandsLook.cs`, was never wired in and was removed.)
- Placement (Nov 19): nodes are placed at a tier, the chain of rooms heading out until the cell reaches it (`GridNodeFor(..., tier)`): the first cycle's ring is two inner, two middle and two outer nodes (`RingTier`); later cycles put the relic outer and the chest middle, outer, inner by turns (`CycleTier`). Before this every node landed 2-3 rooms off the map, nearly always inner.
- Old saves: a chest's kind now follows its tier, so an old save's chest may hold a different kind.
- Bots (owner Nov 18): they build toward the prize node they need most (`Expansion.BotNodeWant`/`BotNodeScore`, `Game.BotNodeWant`; used by `BotFloaterPick` and the Grid bots' `BotCardGoal`): gold chests and veins the less gold they hold (`BotGoldEnough` 300), vector chests the emptier their bar, grid piece chests the fewer cards in hand, upgrade chests at a steady 0.7; relics and buildables chests are ignored. Distance from the hub trims the score a little. Tests in suite `chests`.

**Nov 20 (owner): one universal chest** ([`Chests.md`](Chests.md)). A chest node holds a real treasure chest made by `SpawnChest`; the vector chest node is gone (a chest node at the inner tier holds Gold or Vector, `NodeKind.VectorChest` loads as a chest node).

**Nov 20 (owner): no scout towers, no gold veins.** `NodeKind.Tower`/`Vein` stay in the enum so the numbers don't shift, but `Expansion.Current` turns them into a Chest on load (network and saves). A cycle now places a chest, and a relic every other cycle. `PayVeins`, `NodeScouting`'s tower and vein code and their looks are gone. No protocol change.

## Health shrines and room chests (owner Nov 20, protocol 426)

- **Owner:** "chests have a chance of spawning in basic room pieces, even with modifiers. The type of chest determines its rarity. They can appear in mystery rooms even. They just can't appear in vendor rooms." Nodes work the same (a room built over the node's cell turns that cell into the feature), but **the health shrine is the only node for now**: one use per player, health past max.
- **Shrines** (`Shrines.cs`, `NodeKind.Shrine`): `PlaceFloatingRooms` rings the hub with `Shrines.Ring` (3) in the first boss cycle (one per side, inner/middle/outer tier), then one per cycle; no more chest, relic or rule nodes (old saves keep theirs). A claimed shrine stands in the middle of its cell (`CentresInRoom`); stall cards can't cover it. Host `TickShrines` (from `TickExpansion`, waves and build rounds): a living player within `Reach` who hasn't used it gets full health (to the wound cap) and `OverFrac` (50%) of max as `Player.OverHp`. Damage soaks `OverHp` after shields; going down clears it; it never decays. Uses are a bit per shrine (its place among the map's shrines) in `Player.ShrinesUsed`. Both ride the player snapshot and the seat save (`shrine` line).
- **Room chests** (`RoomChests.cs`): host `RollRoomChest` on every placed shape piece (`HostPlayPieceCard`, modifiers or not) and every mystery room that opens to enemies or empty: `RoomChestChance` 30%, kind by `RoomChestWeights` (Upgrade 8, Gold 30, Vector 25, Relic 3, Piece 16, Buildables 10, Gadget 8). It adds a chest node at a random cell's middle with the kind in `MapNode.Rule` (kind + 1; `Expansion.NodeChestKind(seed, node, cards)`), so `TickNodes` spawns it at once. Service cards (shop, Black Market, Arms, Medic), the Builder Shop and vendor mystery rooms never roll.
- Look (`Game/ShrineLook.cs`): ghost marker with a green cross; claimed: octagon stone plinth, four lit corner posts, a floating cross over a glow with rising motes and a walk-up ring; grey and still once you've used it. HUD: gold "+n" after max, a gold strip over the health cells; a gold ring with turning ticks round a player carrying it.
- Wire: `NodeKind.Shrine` (end of the enum), `Player.OverHp` and `ShrinesUsed` in the player snapshot. Tests: suite `shrines`.
- Open: the numbers (30%, the weights, 50%) are first guesses; bots don't seek shrines out (they build toward them like any node).

**Nov 20 (owner): no rule nodes.** `NodeKind.Rule`, `NodeRule`, `MapNode.Cleared`, `ClearRuleRooms`, the rule floor in `Expansion.Build`, `RuleHalf`/`NodeHalf` (every node is a point again) and the rule node's look are gone; nodes are now Chest, Relic and BreachLoot. Protocol 428; an old save's rule node is skipped on load.
