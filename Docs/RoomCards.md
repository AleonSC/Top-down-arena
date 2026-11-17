# Room cards: build the Expanding arena from a hand of preset rooms

Status: **steps 1-3 built** (protocol 389): cards, hand, play, sources, the vendor's card shelf, bots, the lobby switch, the build screen's hand and ghost.
Step 4 (balance pass, themed cards) open. Decided with the owner on Nov 17, 2026. Builds on [`SurpriseRooms.md`](SurpriseRooms.md) (the shapes and
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
| Where they come from | **A starting hand**, **a free basic card each build round**, **boss and wave drops**, **chest nodes**, and **the vendor** (bought with vectors). |
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

## Where cards come from (first guesses)

- **Starting hand** (dealt at the first build round, and to anyone joining later): Box, Hall and one seeded shape.
- **Each build round**: +1 basic card for everyone: a Hall after odd waves, a Box after even ones.
- **A cleared regular wave**: each player has a 1 in 4 seeded chance of a shape card.
- **A beaten boss**: a shape card for everyone, and a Mini-boss card for one player (it goes round the seats).
- **A chest node opened**: a shape card for everyone.
- **The vendor** (the Black Market room): three cards on offer each boss cycle (seeded, the same for everyone), paid in vectors.
- A full hand (6) drops what comes in, with a log line.

Vectors still pay for openings (refund), teleporters (stash), gates, mystery doors and the vendor's cards.

## How it plays

- Cards mode replaces the Room tool on the build screen with **Cards**: your hand on the right (each card a little drawing of its room), the map on the left.
- Pick a card: the room follows the mouse as a ghost, snapped to the legal spot nearest the pointer (green), with the loops it closes. **R** tries the other turns and mirrors that fit there. **Click** builds it.
- Builds go up at once (no team vote in cards mode), during a build round, like free building.
- Card rooms are ordinary rooms (`Price` 0, `Owner` the player): they roll room chests, take room kits, and can be opened into.

## Wire and saves

- `Player.Hand` (6 bytes, `RoomCard`) in the player snapshot (`SyncVectors`).
- `Msg.PlayCard` (client -> host): hand slot, x, y, turn, mirror. The host rebuilds the room from the shape and checks it (`Expansion.CardRooms`).
- `ShopAction.BuyCard` (arg: the vendor offer 0-2).
- `DiffKnob.RoomCards` in the difficulty.
- Saves: a `hand` line per seat.

## Steps

1. (built) Cards, hand, sources, `Msg.PlayCard`, the lobby switch, bots (play a Hall toward the nearest shop, vendor or node, else the best spot), saves; tests (suite `roomcards`).
2. (built) Build screen: the Cards tool, the hand panel, the ghost and R; previews `CARDS=box,hall,ring,miniboss [CARDSEL=n] [MOUSE=x,y]` with `EXPANDVOTE=<n> EXPANDFREE=1 EXPANDDRAW=0,0,0,0`.
3. (built) The vendor's card shelf (three offers a boss cycle, vector prices).
4. Balance pass with `vecbal` (card rates, prices, hand size); themed cards (a card with a path floor) if wanted.

## Open questions (defaults in bold)

- Should the vendor's offers be bought **any number of times** or once each per player?
- Themed cards (e.g. "Cross · Frozen Lake"): **not yet**; room kits still theme a built room.
