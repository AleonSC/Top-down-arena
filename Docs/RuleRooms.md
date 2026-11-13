# Rule rooms: big nodes that force a bigger room

Status: **design agreed, nothing built.** Owner, Nov 13, 2026: restriction rooms like the old paths (a dark room, ...) as **nodes, but bigger ones that force a
bigger room**. First pool: all four below. Sits beside [`Nodes.md`](Nodes.md); the rules come from [`Paths.md`](Paths.md).

## The idea

A **rule node** is a large node (an area, not a point). It is locked until a built room **fully encloses its area**, so the room has to be big (the price
is the usual room price, so the size is the cost). While anyone stands in the claimed room the **rule** applies and the room's enemies spawn under it.
Clearing it pays a reward.

## First pool

| Rule node | Source | Inside the room | Reward (first guess) |
|---|---|---|---|
| **Dark** | Fogbound | Sight about 350 px round each player; enemies and bullets beyond fade out | Chest |
| **Frozen** | Frozen Lake | You slide with momentum; enemies chilled 10% slower | Gold and vectors |
| **Blood Moon** | Blood Moon | No health orbs or regeneration; each kill heals its killer 2 HP | Relic |
| **Burning / Swarm / Elite** | Burning Grounds, Swarm Fields, Elite Gauntlet | One of: fire patches spreading; twice the melee at 60% health; half the enemies, all elite | Chest |

## Decisions so far

| Question | Decision |
|---|---|
| Shape | A bigger node: needs a room that encloses its whole area (first guess 8 x 8 tiles), so it forces a bigger room than a chest node. |
| Claiming | Rooms only, like chests (`NodeEncloses`); a hallway never claims one. |
| Respawn | None; new ones may appear each boss cycle like other nodes. |

## Decided (Nov 13)

- **Only that room**: the rule applies while you are inside the claimed room, nowhere else.
- **Fixed reward per rule** for now (table above); no choice screen.
- **One rule node per boss cycle** (which rule: random from the pool, to settle in step 1).

## Plan (one step at a time)

1. `NodeKind.Rule` with a size and a rule id, placement like chests (`PlaceFloatingRooms`). **Protocol bump** (`Msg.ExpandState`).
2. Enclose check for the area; claimed-room state; rule hooks in the wave tick (reuse the `Paths.cs` rule functions, scoped to the room).
3. Look: big ghost marker, floor tint per rule (`Tools/Preview` shots first).
4. Bots, `vecbal` balance, README.
