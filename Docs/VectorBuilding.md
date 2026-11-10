# Vector building: the Expanding arena without a purse

Status: **all five steps built** (vectors, proposals and the vote, drawer colours, cap cards, a balance pass). Decided with the owner on Nov 10, 2026. It replaces the
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
  wall with a room, no overlap, 16 rooms, the size cap). Rooms sharing a door's width of wall get a door.
- **Stock** (retuned after play-testing, Nov 10): start 25, cap 35. A cleared boss wave refills everyone to the cap and adds 10 on top (kept
  until spent; ordinary gains never push you over the cap). Each other cleared wave +4; every 15 kills +1. Numbers come from
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

## Step 2 (built, protocol 249)

- **Round**: every cleared wave (`Waves.cs`) calls `OpenBuildRound` after `GrantVectorsForWave`; `ResolveBuildRound` runs as the next
  wave starts (and early, once every player has voted). Never blocks the countdown. Not in the Practice Range, Boss Hunts, after
  the last wave or on a full map.
- **Proposals** (`ExpandProposals`, one per player, `ExpansionRoom.Owner`): `Msg.ExpandDraw` (x, y, w, h) -> `HostExpandDraw`:
  `Expansion.Custom` must accept it and the drawer must have the vectors (`Price` is now `Expansion.Cost`); a newer one replaces the
  last and drops its votes; the drawer's own vote is cast for them.
- **Votes** (`ExpandVotes`: voter -> proposer id, 0 = no room, `Msg.ExpandVote` carries a short): most votes wins, ties are a random
  pick among the tied (`rng`), "no room" counts as one of them; proposals whose drawer can no longer pay are dropped before the
  count; only the winner's drawer pays. Bots propose a room they can afford from `Expansion.Offers` after 1.2 s (7 times in 10) and
  vote after 3 s (2 in 10 for no room).
- **Gone**: the purse (`Purse`, `AddPurse`, the snapshot field), the boss vote and the optional chip, `ExpandOpen` replacing `ExpandOffer`.
  **Kept, dormant**: `Expansion.Offers` (random blueprints, used by the bots to find a box), `PriceFor`, `RoomKind` Shop/Floor and
  their rules (nothing builds those rooms now). Ask before deleting them.
- **UI**: chip "Build rooms" (`ExpandChipUp`), the window with proposal cards (who drew it, cost, votes), "Draw a room / Redraw
  your room", "No room", "Decide later"; the draw screen shows the live vector cost. Previews: `EXPANDVOTE=<rooms>` (window),
  `EXPANDVOTE=<rooms> EXPANDCHIP=1` (chip), `EXPANDDRAW=x,y,w,h`.

## Step 3 (built, no protocol change)

- `RoomArea.Owner` (from `ExpansionRoom.Owner`, which already rides in `Msg.ExpandState`): built rooms show their drawer's class colour
  as a thin inset line and tint in the arena (`DrawRoomsFloor`), and as the fill and frame on the minimap.
- While a round is open the minimap grows to include the proposals and draws them as pulsing ghosts in their drawers' colours; the
  draw screen shows everyone's proposals the same way, with the drawer's name and cost.
- Proposal cards already showed who drew them, the cost and "<name> has N" (step 2). Previews: `EXPANDMAP=<rooms> EXPANDPROP=1
  ZOOM=0.5` (5th arg 2 players), `EXPANDDRAW`.

## Step 4 (built, protocol 250)

- Two cards at the end of `Upgrades.All` (`WithBuilders`, so every older upgrade keeps its number): **Deep Inkwell** (uncommon, +10 cap, 4
  stacks) and **Architect** (milestone, +20 cap and `VectorsNow` 10, 3 stacks). `Stats.VectorCap` carries the bonus; `Recalc` sets
  `Player.VectorCap = VectorCapBase + Stats.VectorCap`; `GrantUpgrade` gives `VectorsNow` (up to the new cap) in the Expanding arena.
- `UpgradeDef.NeedsExpanding`: `Available`, `RollOffer`, `PrizeOffer`, `MilestoneOffer`, `RandomOfRarity` take an `expanding` flag
  (the host passes `ExpandingOn` for level-up offers, challenge prizes, milestone picks, chests and the shop), so the cards never turn
  up in the other arenas.
- The boss refill (step 1) already fills the raised cap. Tests: `ExpandingVectorsTest`.

## Step 5 (built, no protocol change): balance

Report: `run.sh 1 1 vecbal [last wave] [seed] [1 = skip the Classic run]` (a duo of Good bots; `VECSCALE=<rate>` tries another crowd rate).
First numbers (start 25, cap 40, +6 a wave, boss +10, 12 rooms, crowd up to 2x): the duo built **11 rooms in 12 rounds** (the map
was full by wave 14 of 50), 10 of 24 player-rounds opened at the cap (a lot of income wasted), the crowd reached 2.0x, and the
bots died at **wave 13** (Classic: wave 26+).

Changes: start **15**, cap **25**, wave gain **2**, boss bonus **5**, kills +1 per 25 (as before), `MaxRooms` **16**, crowd
`1 + 0.15 x (floor area / hub area - 1)` up to **1.4x** (was 0.35 and 2.0). Result (seeds 1 and 2): about **one room every two
rounds** (11 built by wave 19; 7 by wave 13), 15 of 40 and 7 of 26 player-rounds at the cap, the crowd 1.4x at most, the bots
reaching wave **21** (seed 1) and dying at wave **14** (seed 2). Even with no crowd scaling at all the bots died at waves 15-16, so most of
the extra danger is the map's shape (long hallways, dead-end rooms, enemies spread out), not the head count.

**Retune (owner: "vectors need to be more abundant")**: start **25**, cap **35**, wave gain **4**, boss bonus **10**, a vector every **15** kills, `MaxRooms` **24**. Bots (who always propose) then build about **0.85 rooms a round** (8 in 10 rounds on seed 1, 13 in 15 on seed 2), so the map would fill within ~25 waves of 50; people propose less. Seed 1's bots died at wave 10 with the bigger map earlier, so watch the difficulty.

Not done: Expanding is still harder for bots than Classic (they die around wave 14-21 against Classic's 26+); the owner's play-tests
should say if people feel it too. Shop and Floor rooms stay dormant until the owner decides about bringing kinds back.

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
