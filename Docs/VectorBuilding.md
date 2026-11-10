# Vector building: the Expanding arena without a purse

Status: **plan only, nothing built.** Decided with the owner on Nov 10, 2026. It replaces the purse, the room vote and the
optional offers of [`ExpandingArena.md`](ExpandingArena.md) (steps 1-5, all built and merged). The map builder, the draw
tool, loops, walls, sync of the room list and the camera/minimap/spawn work all stay.

## The idea

Every player holds a stock of **vectors** and spends it to draw rooms and hallways on the shared map. There is no purse, no
vote and no random offers: **every round, between waves, each player can decide to build.** Boss kills refill the stock,
upgrades raise how much you can hold, and a little comes in as you play.

## Decisions (owner)

| Question | Decision |
|---|---|
| What does a room cost? | **Its new wall length**: the perimeter minus the walls it shares with existing rooms. A shared wall is free, so compact builds and loops are cheaper than long dead ends. |
| When can players draw? | **Between waves only**, every round (the intermission). Everyone can draw at once. |
| Shop and Floor rooms? | **Dropped for now.** Plain rooms and hallways only. The code stays dormant (no deletion without asking). |
| How are vectors earned and held? | **All three**: a boss kill refills your stock, upgrades raise the most you can hold, a small amount comes with every cleared wave and from kills. Unspent vectors carry over, up to your cap. |
| Whose vectors? | Each player's own. A room anyone draws joins the one shared map and spends the drawer's vectors. |

## Rules (first guesses to tune)

- **Unit**: 1 vector = 1 tile (100 px) of wall. Cost = (perimeter - shared wall) in tiles. A 6 x 4 closet off the hub: 20 - 3 = **17**;
  a 12 x 3 hallway: 30 - 3 = **27**; a box that touches two rooms is cheaper by the second shared wall (loops pay).
- **Placement**: the existing `Expansion.Custom` rules (100 px grid, sides 300-1200, at most 96 tiles, shares at least 300 px of
  wall with a room, no overlap, 12 rooms, the size cap). Rooms sharing a door's width of wall get a door.
- **Stock**: start 25, cap 40 (each cap upgrade +15). Boss kill: refill to the cap, plus 10 over it for that round. Each cleared
  wave +6; every 25 kills +1. Numbers come from play-testing: a player should be able to afford about one decent room a round.
- **Building**: in the intermission each player has a chip "Draw rooms" and the draw screen (as today) with a vector meter.
  Placing takes effect at once for everyone. The host applies placements in arrival order and re-checks each (a box that now
  overlaps someone's new room is refused and costs nothing). You can **undo your last room** for a full refund until the next
  wave starts.
- **Waves**: as now (the map is up on regular waves, boss waves are fought in the hub). A bigger map still means more enemies
  (`ExpansionScale`).
- **Bots**: bots spend their vectors on a simple rule (a room that touches the hub or a loop) so test runs grow a map.

## What goes, what stays

- **Goes**: the purse (`Purse`, `AddPurse`, the chip and HUD purse line), the offers and the vote (`OfferExpansion`,
  `ExpandOffer`, `ExpandVotes`, `Msg.ExpandVote`, `Expansion.Offers`), the optional chip, the vote window. The draw screen becomes
  the only builder.
- **Stays**: `World`, `Expansion.Placement/Custom/Build`, loops, closed walls, the Expanding setting, the minimap and camera,
  spawns by area, `ExpansionScale`, the draw screen and `Msg.ExpandDraw` (reworked to spend vectors).
- **Dormant**: `RoomKind` Shop/Floor, shop-room gold and shop opening, floor rules (`FloorAt`...). Nothing creates such a room,
  so they never run; they come back as a later step (a kind picked when drawing, floors unlocked by bosses).
- **Personal shop gold** stays where it was (Bazaar, Gold Rush).

## Steps

1. **Vectors**: `Game.Vectors` per player (synced in the player snapshot), `Expansion.Cost`, the cap and the income rules, and
   tests for the cost maths and income. Remove the purse.
2. **Placing**: rework `Msg.ExpandDraw` to place at once and spend the drawer's vectors; host re-validation; undo with refund;
   bots. Remove the offers, the vote and the optional chip. Protocol bump.
3. **UI**: the "Draw rooms" chip in the intermission, the draw screen with a vector meter and the live cost, undo, who built what.
   Previews.
4. **Upgrades**: the cap upgrade(s) in the upgrade pool (Expanding arena only), the boss refill.
5. **Balance and polish**: income and cost numbers against bot runs, README, docs, and a decision on bringing kinds back.

## Open questions (defaults in bold)

- Should a room show who drew it (a colour)? **Yes, a thin tint line on its wall in the drawer's colour.**
- Can a player build with 0 vectors left (free hallway)? **No.**
- Do boss waves give vectors to downed players too? **Yes, everyone.**
