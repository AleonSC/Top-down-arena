# Rule rooms: big nodes that force a bigger room

Status: **design agreed, steps 1-3 built (protocol 307).** Owner, Nov 13, 2026: restriction rooms like the old paths (a dark room, ...) as **nodes, but bigger ones that force a
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

## Step 1 (built, protocol 306)

- `NodeKind.Rule` and `NodeRule` (Dark, Frozen, BloodMoon, Burning, Swarm, Elite) in `Expansion.cs`; `MapNode.Rule` is synced after `Spent` in `Msg.ExpandState` and saved as the fifth field of the `node` line.
- The node covers **8 x 8 tiles** (`RuleHalf` 400 round its point); `NodeInside` needs the whole area inside a built room's walls, so a room at least 9 x 9 tiles claims it (`NodeEncloses` is unchanged otherwise).
- Placed like a chest, one per boss cycle (`PlaceFloatingRooms`, after chest and relic), out to the left or right of the connected map (the map is wider than tall), and skipped when no spot fits.
  `RuleFor(seed, cycle)` picks its rule, the same on every machine. `NodeReach` (550 for a rule node, 250 otherwise) is the half side of the room the bots build round it (11 x 11) and the clear space kept round it.
- Look (first pass): an orange square outline of the whole area on the arena floor, minimap and draw screen ink; the real look is step 3. The rule does nothing yet (step 2).
- Tests (suite `expand`): placed, a 9 x 9 room claims it and smaller ones don't, `NodeStep` reaches it.

## Step 2 (built, protocol 307)

- A room that claims a rule node becomes a **Floor room** with the rule's ground (`Expansion.RuleFloor`: Dark = Fogbound, Frozen = Frozen Lake, Blood Moon, Burning = Burning Grounds, Swarm = Swarm Fields, Elite = Elite Gauntlet), a multi-box room all of it.
  The rules are the old paths' own room-scoped ones, so they hold in that room only: ice sliding and chill, no regeneration or orbs and kill heals, fire patches, a doubled crowd (melee at 60% health) or doubled all-elite crowd, and kills there pay the path's XP and score bonus.
- **Dark** is new as a room rule: the Fogbound mist (350 px sight round each player) comes on for the local player while they stand in a Fogbound room (`InDarkRoom`, `TickArenaRules`) and rolls off when they leave.
- **Prize**: `MapNode.Cleared` (synced and saved) is set when a wave is cleared with the room claimed (`ClearRuleRooms`, from `WaveCleared`; not on boss waves). Dark, Burning, Swarm and Elite put a treasure chest at the node (the chest-node logic, `TickNodes`);
  Blood Moon a relic to walk up to (`TickRelicNodes`); Frozen pays gold (40 + 4 a wave) and 8 vectors to everyone at once and spends the node. First guesses, tune with `vecbal`.
- Look: nothing drawn in a claimed rule room until it is cleared, then the chest or relic fixture. The floors keep their path looks; a rule room's own look is step 3.
- Tests (suite `expand`): each rule gives its ground, the way there is no rule room, Frozen pays once, a Dark room's chest only after clearing.

## Step 3 (built, no protocol change)

- A rule node on the arena floor (`NodeLook.cs` `DrawRuleNode`): the whole 8 x 8 tile area in its rule's path colour (`NodeInkOf`): a faint tint, 45 degree hatching, breathing corner brackets, and in the middle a ring with turning dashes and the rule's mark
  (an eye for Dark, a snowflake, a blood-drop moon, a flame, a crowd of dots, a diamond for Elite).
- The build screen's map (`ExpansionUi.cs`): the node's area as a tinted, framed square on the holo map and the minimap; hovering anywhere on it names the rule, what it does and what it pays (`RuleBlurb`); the legend lists "DARK RULE" etc.
- A claimed rule room keeps the path's own floor and weather; its prize shows once it is cleared (step 2). Preview: `EXPANDMAP=0 EXPANDNODE="rule.dark:2400,450" EXPANDAT=1900,450` (mode hud; `rule.frozen|bloodmoon|burning|swarm|elite`, a third part `cleared` or `spent`).
