# Floating rooms and themed rooms: paths, shops, vendors and the Gatekeeper in the Expanding arena

Status: **step 1 built** (floating rooms exist, are sealed, connect and show; the vendor and the Gatekeeper do nothing yet); steps 2-5 not started. Decided with the owner on Nov 10, 2026 (one idea from a friend: shops and vendors in "floating rooms"
you have to build to). It builds on [`ExpandingArena.md`](ExpandingArena.md) and [`VectorBuilding.md`](VectorBuilding.md) (all built).

## Decisions (owner)

| Question | Decision |
|---|---|
| Paths in the Expanding arena | **Paths become themed rooms.** A path (Burning Grounds, Sanctuary, Frozen Lake, Blood Moon, Swarm Fields, Sniper Ridge, Elite Gauntlet) is a theme you pick when drawing a room, for extra vectors, unlocked by beating bosses. Its rule and its kill bonus hold inside that room only. The map-wide path vote stays off in this arena. |
| Shops and vendors | **Pre-placed, out of reach.** Each boss cycle a shop or vendor room is placed a few tiles off the map and drawn as a ghost. It stays sealed until a built room or hallway touches it (3 tiles of shared wall); then its door opens. |
| The Gatekeeper | **A floating sealed room**, like the shops: you build to it, walking in starts the duel inside it, beating it gives the prize pick and the room becomes an ordinary room. |

## How it fits what exists

- **Floating rooms are rooms the map already knows about but nobody has connected.** They are in the room list from the start of their cycle, with a
  kind (Shop, Vendor, Gatekeeper) and `Sealed = true`. `Expansion.Build` gives them walls but no door.
- **Connecting is the loop rule we already have**: any two rooms sharing at least `MinDoorRun` (300 px) of wall get a door. So a drawn room
  that touches a floating room opens it; the draw tool just has to accept a box whose only neighbour is the map *and* a floating room
  (the floating room is not a valid parent: a drawn room must still touch a connected room).
- **Placement** (`Expansion.FloatingFor(seed, cycle, built)`): deterministic from the run seed and the boss cycle, so every machine agrees and nothing
  needs syncing beyond the room list. 3 to 6 tiles from the nearest built room, never overlapping or touching anything, inside the size cap, the
  shop and vendor on different sides when possible. Seen as a pulsing ghost with an icon on the minimap, the draw screen and in the arena.
- **Shop**: the existing shop (`Shop.cs`: shelf, services, Done) opens while you stand in the room, as the dormant shop-room code does (`InShopRoom`).
  **Vendor**: a second service room (what it sells is open: see below). Both are safe rooms (no enemy spawns, `SpawnWeight` 0).
- **Gatekeeper**: reuse the Wings vault's mini-boss (`StartRoomsWave` / `WinVault`, `ChallengeKind.MiniBoss`). The room is a sealed vault: its door
  is shut until connected; the first player in starts the duel (door locks behind them, as the vault does); beating it gives the prize pick and unseals
  the room for good. One per boss cycle.
- **Paths as themed rooms** reuse the dormant floor-room rules (`FloorAt`, `FloorBonusAt`, patches, ice, Blood Moon): the room kind Floor plus
  `Floor = PathId`. New work: pick a theme in the draw screen (a row of themes under the box, locked ones greyed with the boss that unlocks them),
  an extra vector cost per theme (danger 1: +3, 2: +6, 3: +10 vectors, first guesses), and per-room versions of the two arena-rule paths
  (Fogbound: a fog inside the room; Narrow Pass: dropped, its job is the corridor shape). Bazaar is the shop room, so it goes.

## Steps

1. **Floating rooms**: `ExpansionRoom.Sealed` and a room list that may hold rooms with no parent; `FloatingFor` placement; `Build` skips their parent door;
   `Placement` refuses a box that touches only floating rooms; ghost drawing on the minimap, draw screen and in the arena; sync (protocol bump);
   tests (deterministic, never overlapping, opens when touched, a lone touch to a floating room is refused).
2. **Shop and vendor rooms**: the shop opens in the room (dormant code revived, no longer gated on Bazaar/gold rules: personal gold from kills while a
   shop is connected); a vendor with a small fixed stock (decide: see open questions). Safe rooms.
3. **Gatekeeper room**: the vault duel in a floating room, its prize, unsealing.
4. **Themed rooms**: the theme row in the draw screen, costs, unlocks by boss, per-room Fogbound.
5. **Balance and polish**: how far off the map they float against vector income (the hallway to reach one should cost about a room's worth), bot
   behaviour (bots build to shops when they have gold), README and previews.

## Step 1 (built, protocol 252)

- `ExpansionRoom.Floating` (in `Msg.ExpandState`'s room list); `RoomKind.Vendor` and `Gatekeeper` at the end of the enum; `RoomArea.Floating` / `Sealed`
  (sealed = floating with no door). `Expansion.FloatingFor(seed, cycle, built, kind)` places one 3-6 tiles off a connected room, 2 tiles clear
  of everything and inside the size cap (shop/vendor 6 x 4 tiles, Gatekeeper 8 x 6); `PlaceFloatingRooms` runs as a regular wave starts, once per
  boss cycle (`(Wave - 1) / 5`): a shop on even cycles or a vendor on odd ones, and the Gatekeeper, at most `MaxSealed` 4 waiting.
- `Expansion.ConnectedAreas` / `SealedCount` / `BoughtCount`: `Placement` only takes a connected room as the parent (a box touching only a floating room is
  refused: "Must share at least 3 tiles of wall with the connected map"); touching a floating room as well gives it a door (the loop rule). The room cap
  counts bought rooms only.
- Sealed rooms add no crowd (`ExpansionScale`), no spawns (`SpawnWeight` 0, also for shop, vendor and Gatekeeper rooms), no cover; a floating shop is a shop room
  (`HasShopRoom`) once opened. The arena floor draws a ring and a mark (coin, plus, diamond) that breathes while sealed; the minimap and the draw
  screen show them as ghosts with their names. Previews: `EXPANDFLOAT=1` with `EXPANDMAP` or `EXPANDVOTE`.
- Tests (suite `expand`): determinism, clearance and caps over 80 seeds, sealed rooms have no doors, a lone touch is refused, a connecting hallway opens a room,
  a boss cycle places a shop and the Gatekeeper on both screens with no crowd and no spawns.

## Open questions (defaults in bold)

- What does the vendor sell? **Consumable help (a revive token, a full heal, a reroll) at fixed vector-free prices in gold**, while the shop keeps the upgrade shelf.
- Do floating rooms stay if nobody connects them? **Yes, until the next cycle's rooms are placed (the old ones remain on the map as ghosts).**
- Can enemies path into a connected shop or Gatekeeper room? **No spawns inside; enemies may wander in through its door.**
- Do themed rooms count as rooms for the 24-room cap? **Yes.**
