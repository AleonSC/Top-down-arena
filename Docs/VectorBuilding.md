# Vector building: the Expanding arena without a purse

Status: **step 1 built** (the vector economy); steps 2-5 not started. Decided with the owner on Nov 10, 2026. It replaces the
purse and the random room offers of [`ExpandingArena.md`](ExpandingArena.md) (steps 1-5, all built and merged) with per-player
vectors; **the team still votes on rooms**. The map builder, the draw tool, loops, walls, sync of the room list and the
camera/minimap/spawn work all stay.

## The idea

Every player holds a stock of **vectors**. **Every round, between waves, any player can draw a room or hallway and put it to
the team as a proposal; the team votes and the winner is built, paid for by the player who drew it.** There is no purse and no
random offers. Boss kills refill the stock, upgrades raise how much you can hold, and a little comes in as you play.

## Decisions (owner)

| Question | Decision |
|---|---|
| What does a room cost? | **Its new wall length**: the perimeter minus the walls it shares with existing rooms. A shared wall is free, so compact builds and loops are cheaper than long dead ends. |
| When can players draw? | **Between waves only**, every round (the intermission). Everyone can draw at once. |
| How are rooms chosen? | **The team votes**, every round. Each player may propose one drawn room; everyone votes among the proposals (and "no room"). **The most votes wins; a tie (a 50/50) is a random choice among the tied.** Only the winning proposal's drawer pays. |
| Shop and Floor rooms? | **Dropped for now.** Plain rooms and hallways only. The code stays dormant (no deletion without asking). |
| How are vectors earned and held? | **All three**: a boss kill refills your stock, upgrades raise the most you can hold, a small amount comes with every cleared wave and from kills. Unspent vectors carry over, up to your cap. |
| Whose vectors? | Each player's own. You can only propose what you can afford; if your proposal wins you pay, if it loses it costs nothing. |

## Rules (first guesses to tune)

- **Unit**: 1 vector = 1 tile (100 px) of wall. Cost = (perimeter - every wall it shares with existing rooms) in tiles. A 6 x 4
  closet along the hub's top wall: 20 - 6 = **14**; a 3 x 12 hallway standing off it: 30 - 3 = **27**; a box that touches two
  rooms is cheaper by the second shared wall (loops pay).
- **Placement**: the existing `Expansion.Custom` rules (100 px grid, sides 300-1200, at most 96 tiles, shares at least 300 px of
  wall with a room, no overlap, 12 rooms, the size cap). Rooms sharing a door's width of wall get a door.
- **Stock**: start 25, cap 40 (each cap upgrade +15). A cleared boss wave refills everyone to the cap and adds 10 on top (kept
  until spent; ordinary gains never push you over the cap). Each other cleared wave +6; every 25 kills +1. Numbers come from
  play-testing: a player should be able to afford about one decent room a round.
- **Proposing**: in the intermission each player has a chip "Draw rooms" and the draw screen (as today) with a vector meter and the
  live cost. A drawing becomes your proposal (one per player; a newer one replaces it). It is checked when you draw it and again
  when the vote resolves.
- **Voting**: the vote window shows the proposals as cards (as today) plus "no room". It resolves when everyone has voted, or when
  the next wave starts (the optional kind: the countdown never waits). Most votes wins, **ties are a random pick**, no proposals or
  "no room" winning means nothing is built. A proposal that no longer fits (it can't: only one room is built a round) or whose
  drawer can no longer afford it is dropped before the count.
- **Waves**: as now (the map is up on regular waves, boss waves are fought in the hub). A bigger map still means more enemies
  (`ExpansionScale`).
- **Bots**: bots spend their vectors on a simple rule (a room that touches the hub or a loop) so test runs grow a map.

## What goes, what stays

- **Goes**: the purse (`Purse`, `AddPurse`, the chip and HUD purse line) and the random offers (`Expansion.Offers`; the cards now
  show players' own proposals). **The vote stays** (`ExpandVotes`, `Msg.ExpandVote`, the window, the optional chip and its
  close rule) with two changes: ties are random, and a proposal costs its drawer's vectors instead of the purse.
- **Stays**: `World`, `Expansion.Placement/Custom/Build`, loops, closed walls, the Expanding setting, the minimap and camera,
  spawns by area, `ExpansionScale`, the draw screen and `Msg.ExpandDraw` (reworked to spend vectors).
- **Dormant**: `RoomKind` Shop/Floor, shop-room gold and shop opening, floor rules (`FloorAt`...). Nothing creates such a room,
  so they never run; they come back as a later step (a kind picked when drawing, floors unlocked by bosses).
- **Personal shop gold** stays where it was (Bazaar, Gold Rush).

## Steps

1. **Vectors (built)**: `Player.Vectors` and `VectorCap` (synced), `Expansion.Cost` (new wall length), the income rules (start, cleared
   wave, boss refill, kills) and a vector chip; tests. The purse and the random offers still run for now (the vote is built on
   them) and go in step 2.
2. **Proposals**: each player's drawing becomes their proposal (reworked `Msg.ExpandDraw`); the vote counts proposals and "no room",
   ties random; the winner's drawer pays vectors; remove the purse and the random offers; bots propose. Protocol bump.
3. **UI**: the "Draw rooms" chip, the draw screen with the vector meter and live cost, proposal cards with who drew them, the
   vote window. Previews.
4. **Upgrades**: the cap upgrade(s) in the upgrade pool (Expanding arena only), the boss refill.
5. **Balance and polish**: income and cost numbers against bot runs, README, docs, and a decision on bringing kinds back.

## Open questions (defaults in bold)

- Should a room show who drew it (a colour)? **Yes, a thin tint line on its wall in the drawer's colour.**
- Can a player build with 0 vectors left (free hallway)? **No.**
- Do boss waves give vectors to downed players too? **Yes, everyone.**
- Can a player see others' vector stocks? **Yes, a small number on each card.**
