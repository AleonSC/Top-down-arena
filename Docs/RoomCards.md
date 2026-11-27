# Room cards: build the Expanding arena from a hand of preset rooms

**REMOVED (Nov 18, protocol 407):** room cards are gone from the game; the Expanding arena builds only from grid pieces ([`GridPieces.md`](GridPieces.md)). The hand, the service cards and the Builder Shop live on as grid cards. Kept as history.

Status: **steps 1-3 built** (protocol 389), plus designed obstacles inside each card's room (protocol 391) and the Builder Shop utility card (protocol 393): cards, hand, play, sources, the Black Market's card shelf, bots, the lobby switch, the build screen's hand and ghost.
Service cards (Shop, Black Market, Arms Dealer: tiny rooms) added by [`ArmsDealer.md`](ArmsDealer.md) step 1 (protocol 394). Step 4 (balance pass, themed cards) open. Decided with the owner on Nov 17, 2026. Builds on [`SurpriseRooms.md`](SurpriseRooms.md) (the shapes and
their placement), [`VectorBuilding.md`](VectorBuilding.md) (vectors) and [`Nodes.md`](Nodes.md).

## Why

- Drawn rooms are a size choice, not a tactical one, and free drawing makes long hallways and dead ends (bots die at wave 14-21 in Expanding, 26+ in Classic).
- Preset shapes are tested to fight well (the surprise rooms: Ring, Cross, L, T, Chamfer). Cards make every room one of them.
- Picking a card and a spot is faster than drawing corners mid-break, and reads well in co-op.

## Decisions (owner)

| Question | Decision |
|---|---|
| What you build with | **Room cards**: each one a preset room you place against a room's wall (any legal turn and mirror). Playing one is free. |
| Who holds them | **Per player**: your own hand (at most 6), like your vectors. |
| Where they come from | **A starting hand**, **a free basic card each build round**, **boss and wave drops**, **chest nodes**, and **the Black Market** (bought with vectors). |
| Mini-bosses | **A Mini-boss card** places a duel room holding the coming boss's faction mini-boss (the mystery door's duel room). |
| Nodes | Stay for small things a room can hold: chests, relics, rule areas, towers, veins. |
| Free drawing | **Kept as a lobby switch** (Building: Room cards / Free drawing / Team vote), so the old system is one click away. Cards are the default. |

## The cards

| Card | Room (tiles) | Vendor price (vectors) |
|---|---|---|
| Box | 7 x 6 plain room | 16 |
| Hall | 8 x 3 straight hallway | 10 |
| Chamfer | 9 x 7, corners notched | 22 |
| L hall | 12 + 9, 3 wide | 24 |
| T hall | 15 bar + 9 stem, 3 wide | 26 |
| Cross | 15 across, arms 3 wide | 30 |
| Ring | 11 x 11 lane round a 5 x 5 block | 34 |
| Mini-boss | 8 x 6 duel room (`RoomKind.Gatekeeper`) | 40 |
| Builder Shop | 5 x 4 card shop (`RoomKind.Builder`) | 36 |

## Where cards come from (first guesses)

- **Starting hand** (dealt at the first build round, and to anyone joining later): Box, Hall and one seeded shape.
- **Each build round**: +1 basic card for everyone: a Hall after odd waves, a Box after even ones.
- **A cleared regular wave**: each player has a 1 in 4 seeded chance of a shape card.
- **A beaten boss**: a shape card for everyone, and a Mini-boss card for one player (it goes round the seats).
- **A chest node opened**: a shape card for everyone.
- **The Black Market** (the Black Market room): three cards on offer each boss cycle (seeded, the same for everyone), paid in vectors.
- A full hand (6) drops what comes in, with a log line.

Vectors still pay for openings (refund), teleporters (stash), gates, mystery doors and the Black Market's cards.

## How it plays

- Cards mode replaces the Room tool on the build screen with **Cards**: your hand on the right (each card a little drawing of its room), the map on the left.
- Pick a card: the room follows the mouse as a ghost, snapped to the legal spot nearest the pointer (green), with the loops it closes. **R** tries the other turns and mirrors that fit there. **Click** builds it.
- Builds go up at once (no team vote in cards mode), during a build round, like free building.
- Card rooms are ordinary rooms (`Price` 0, `Owner` the player): they take room kits, and can be opened into.

## Inside the rooms (built, protocol 391)

Each card's room has a designed set of obstacles (`Core/RoomCardLayouts.cs`) instead of the random cover and side walls a drawn room gets:

| Card | Obstacles |
|---|---|
| Box | four pillars round a low wall in the middle |
| Hall | a block off alternate walls near each end (a zig-zag; the middle stays clear for a door) |
| Ring | a buttress mid-way along each outer wall, a pillar in each corner |
| Cross | a pillar in the crossing, a block in each arm turning like a pinwheel |
| L hall | a pillar in the bend, blocks off alternate walls |
| T hall | a pillar facing the stem, a block in each arm and in the stem |
| Chamfer | a pillar in the middle, a low wall above and below it |
| Mini-boss | four pillars to fight round |
| Builder Shop | a pillar in each corner (the workbench in the middle is drawn, not solid) |

- Lane pieces hug a wall and leave at least 220 px to pass. The layout turns and mirrors with the room; a symmetric shape tries every matching turn and keeps
  the one with the most pieces clear of its doors; anything within a door's mouth (its width + 60 px along, 110 px in) is left out.
- `ExpansionRoom.Card` (the Piece, on each box) carries it over the wire and in saves (the `room` line's 15th number).
- Preview: `EXPANDMAP=0 CARDROOM=<card> [CARDSPOT=n] ZOOM=0.5 Tools/Preview/shot.sh x hud`. Test: suite `roomcards`, since removed.

## Utility cards (built, protocol 393)

Cards whose room has a job rather than a fight shape: the **Mini-boss** (above) and the **Builder Shop** (`Core/BuilderShop.cs`, `Game/BuilderShopUi.cs`):

- Its room is `RoomKind.Builder` (amber, a workbench drawn in the middle, no enemy spawns). The Black Market sells the card the cycle before a Mini-boss one (`CardOffers`: cycle % 3 == 1).
- Standing in it during a build round shows its shelf at the bottom of the screen (the arena stays live; clicks on it don't fire): `BuilderOfferCount` (5) cards from
  `BuilderOffers(seed, wave)`: Box, Hall, three different shapes (a Mini-boss in place of the third one wave in three), the same on every machine, new each wave.
- Price: `BuilderPrice` = 3/4 of `CardPrice` (rounded up), in vectors; bought cards go to the hand. Refused outside the room, outside a build round, on a full hand
  or short of vectors (`BuilderRefusal`).
- Wire: `Msg.BuilderBuy` (client -> host: offer index). Preview: `EXPANDMAP=0 CARDROOM=buildershop BUILDEROPEN=1 [BUILDERVEC=n] Tools/Preview/shot.sh x hud`.

## Wire and saves

- `Player.Hand` (6 bytes, `Piece`) in the player snapshot (`SyncVectors`).
- `Msg.PlayCard` (client -> host): hand slot, x, y, turn, mirror. The host rebuilds the room from the shape and checks it (`Expansion.CardRooms`).
- `ShopAction.BuyCard` (arg: the Black Market offer 0-2).
- `DiffKnob.RoomCards` in the difficulty.
- Saves: a `hand` line per seat.

## Steps

1. (built) Cards, hand, sources, `Msg.PlayCard`, the lobby switch, bots (play a Hall toward the nearest shop, Black Market or node, else the best spot), saves; tests (suite `roomcards`, since removed).
2. (built) Build screen: the Cards tool, the hand panel, the ghost and R; previews `CARDS=box,hall,ring,miniboss [CARDSEL=n] [MOUSE=x,y]` with `EXPANDVOTE=<n> EXPANDFREE=1 EXPANDDRAW=0,0,0,0`.
3. (built, retired Nov 18) The Black Market's card shelf (three offers a boss cycle, vector prices): the Black Market sells no cards now.
4. Balance pass with `vecbal` (card rates, prices, hand size); themed cards (a card with a path floor) if wanted.

## Open questions (defaults in bold)

- Should the Black Market's offers be bought **any number of times** or once each per player?
- Themed cards (e.g. "Cross · Frozen Lake"): **not yet**; room kits still theme a built room.
