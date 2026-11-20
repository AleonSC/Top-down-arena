# Universal chests

Status: **built (steps 1-4, no protocol change).** Decided with the owner on Nov 20, 2026. `SpawnChest` is in `Props.cs`; the vector chest node is a chest node holding a Vector chest.

## The idea

Every chest is one thing: a `PropKind.Chest` prop (`Props.cs`), and `ChestKind` is the only place that says what it holds.
Whatever needs a chest (a wave drop, Contraband, a node, a rule room, a mystery-door hallway) calls one function and gets one.

## Today (why change it)

- Treasure chest prop: `ChestKind {Upgrade, Gold, Vector, Relic, Piece, Buildable}`, opened by `OpenChest`.
- A `NodeKind.Chest` node holds a prop chest, linked by the host-only `nodeChests` table (`IsNodeChest`, `NodeIndexOfChest`, `SpendNodeOfChest`).
- A `NodeKind.VectorChest` node is **not** a prop: `TickVectorChests` has its own reach, open and pickup burst, and `RemoteChests.cs` repeats the vector payout.
- What a chest holds is picked in three places: `Expansion.NodeChestKind` (tier), `RollChestLoot` (items), the mystery door.
- `ChestKind.Vector` and `NodeKind.VectorChest` are the same idea twice; `ChestKind.Relic` is unused.

## Decisions

1. **Fold `VectorChest` into the one chest.** A vector chest is a `PropKind.Chest` with `ChestKind.Vector`; `OpenChest` bursts the vector pickups (`VectorChestPickups`). `TickVectorChests`, `VectorChestReach` and the vector branch of `HostCollectChest` go.
2. **Every source uses the one spawn call**: node, rule room prize, mystery-door hallway, wave drop, Contraband.
3. **A callback, not a prop field (option A).** `SpawnChest(V2 pos, ChestKind kind, Action<Prop> onOpened = null)` (host). The hook is kept host-side by prop id and called once from `OpenChest`. The node passes "spend me"; a door or wave drop passes nothing. This replaces `nodeChests`, `IsNodeChest`, `NodeIndexOfChest` and `SpendNodeOfChest`.
   - A hook is not in snapshots or saves. After a load the node re-scan (`TickNodes`, which already puts a missing chest back) spawns the chest again with its hook. A spawned chest with no hook (a wave drop) needs none.
   - "Never holds the wave" (`IsNodeChest`) becomes a flag on the spawn call (`holdsWave: false`).
   - A piece chest's seed (`ChestCardFor(pl, seedIdx)`) moves to a spawn argument, no longer read back from the node index.
4. **One draw path.** `NodeLook` and `ChestLooks` draw every chest from `ChestKind`.
5. **One place picks the contents.** `NodeChestKind` stays the tier rule for nodes but returns a `ChestKind` including `Vector`; nothing else decides.

## Migration

- Keep `NodeKind.VectorChest` in the enum (its number stays, so the others don't shift) but mark it legacy: loading a save with one turns it into `NodeKind.Chest`, and `Expansion.ApplyTier` (inner tier) picks `ChestKind.Vector` for it. New maps never place it.
- Wire: the prop snapshot already carries `Chest` (a byte), so **no format change is expected, no protocol bump**; confirm when building (bump and add a `Docs/ProtocolHistory.md` line if the node snapshot changes).

## Steps

1. `SpawnChest` + hook, move `Chest` node and rule room onto it, delete the `nodeChests` family. Tests: `ChestNodes`, `ChestKinds`.
2. Vector chest as a prop: `OpenChest` Vector case, drop `TickVectorChests`, remote collect, look, save migration. Tests: `ChestNodes`, `RemoteChests`.
3. Mystery-door hallway, wave drops and Contraband through `SpawnChest`. Tests: `MysteryDoors`, `Contraband`, `Props`.
4. One draw path (`NodeLook` / `ChestLooks`), previews (`Tools/Preview`), README, `CODEMAP.md`, `Docs/Nodes.md` note.

Run per step: `Tools/CompileCheck/check.sh` and the suites named above (`run.sh list`).

## As built

- `Game.SpawnChest(pos, kind, onOpened, holdsWave, seed)`, `TrySpawnChest(kind)` (a free spot: wave drops, Contraband, an Ambush's reward) and `ChestAt(pos)` are in `Props.cs`; hooks live in the host-only `chestSources` (cleared with the props).
- `TickNodes` spawns a node's chest once (`ChestAt(NodeSpot)` says if it is already there), with a hook that spends the node (`SpendNode`). A mystery-door hallway and a rule room's prize are chest nodes, so they go the same way.
- `Expansion.NodeChestKind` at the inner tier is Gold or Vector (the coin the vector chest node used to be placed by). `NodeKind.VectorChest`, `Shop` and `BlackMarket` are gone from the enum (protocol 421). An older save's `VectorChest` node loads as a chest node; its shop and Black Market nodes are skipped.
- `Msg.CollectChest` is just the chest's prop id (the `node` bool went in 421).
- `NodeLook` no longer draws a vector chest node; a claimed chest is the prop, drawn by `PropsLook` / `ChestLooks` for every `ChestKind`.
