# Expanding arena: design plan

Status: **steps 1-2 built** (the `World` refactor; room types, offers and the map builder, not in the game yet); steps 3-5 not started. Written Nov 2026 from the owner's idea and a read of `Rooms.cs`, `Paths.cs`
and `Shop.cs`. Numbers are first guesses.

## The idea

The run starts in one box. Every 5 rounds the players are offered a few randomly generated **expansions** (new rooms
that attach to what already exists, picked by what's there) and buy one with currency. The map grows as the run goes.

Three **layouts** stay in the game and share one base, so a future mode can reuse the groundwork:

| Layout | What it is | Exists? |
|---|---|---|
| **Single** | One box (today's Classic arena) | yes (`ArenaRooms` off) |
| **Wings** | The fixed hub + side rooms + hallways + vault + portals (today's Rooms arena) | yes (`Rooms.cs`) |
| **Expanding** | Starts as Single, grows by bought rooms | new |

Single = Expanding with zero purchases. Build Expanding first, then the other two are special cases.

## What exists today (the audit)

- **Arena setting**: `DiffKnob.ArenaRooms` is on/off (Classic or Rooms). `RoomsUp` is decided per wave: regular waves
  get the rooms, boss waves get the hub alone (bosses are tuned for 1600x900, `PullIntoHub`).
- **Geometry is hard-coded**: `Rooms` is a static class with fixed areas (`Hub=0 ... Vault=5`), doors, portals and
  `WorldMin/WorldMax`. `RoomsOn` is checked in about 15 files (camera, renderer, AI, spawns, props, challenges).
- **The seam that already works**: `Game.ArenaMin / ArenaMax / Walled` (`Paths.cs`) already answer "how big is the
  playable area" for Classic, Narrow Pass, Tiny Arena and Rooms. Code that uses them needs no change for new shapes.
- **Steering** uses a floor plan (`LayoutDef`, `Rooms.With`, corner graph with portal links), so enemies and bots
  already route through doors. A new map just has to produce a `LayoutDef`.
- **Paths** (`Paths.cs`): a separate system on top of waves. After each boss the host picks 1 of 2 paths for the
  next 4 regular waves (enemy mix, arena rule, hazard floor...). Narrow Pass shrinks the arena through `ArenaMin`.
- **Currency**: gold already exists (`Shop.cs`): per player, dropped by kills (`GoldPerXp`), spent in the shop,
  leftovers turn into XP when it closes. The Bazaar path is a shop path. So "currency everywhere" is mostly
  already true; what's missing is spending it on rooms.
- **Co-op sync**: the layout rides in the snapshot (`SyncRooms`, the layout byte's top bit).

## Proposed design

1. **One world object.** Replace the static `Rooms` constants with a `World` (list of rooms: rect + type + modifier,
   doors, portals). Wings becomes one hard-coded `World`; Single is a one-room `World`; Expanding builds its `World`
   from purchases. `ArenaMin/ArenaMax`, the `LayoutDef` floor plan, camera bounds and minimap all read from it.
2. **Layout policy.** An `ArenaLayout { Single, Wings, Expanding }` setting replaces the on/off knob. The policy answers
   two questions: "what's the world this wave?" and "what can the players buy at a milestone?".
3. **Room types** (data, not code): Combat, Shop, Floor modifier (reuses the path floors and hazards: fire, ice,
   fog...), Boss arena, Challenge/Vault. A room's type says what it does; where it sits is separate.
4. **Offers.** Every 5 waves, 2-3 offers drawn with a seed from `(run seed, round)`. Each is a room shape plus a type
   plus a price. Generation attaches the shape to an open door slot on an existing room (grid-aligned, so no overlap
   or geometry cleanup). Weights prefer types and shapes not yet on the map and avoid dead ends.
5. **Currency.** Reuse gold. A team purse is simpler for the vote; per-player gold stays for the shop. See open
   questions.
6. **Bosses** stay in a fixed 1600x900 arena (existing `PullIntoHub` idea), so boss balance and the Boss Lab hold.
7. **Sync.** The map is deterministic from the seed plus the list of purchases, so only the purchase list is sent
   (a new snapshot field, also in the join message for late joiners). **Protocol version bump** on the step that adds it.
8. **Caps.** Max room count and world size so collision, steering graphs, the minimap and the camera stay cheap.
9. **Enemy scaling.** Spawn count and the ranged share grow with floor area, so a bigger map isn't an easier one
   (the Rooms 1.3x damage is the model; tune with the bot balance test).

## Where paths fit

Paths and expansions overlap (both change the ground and the rules). Recommendation: **keep paths as they are** for
the Single and Wings layouts, and in Expanding mode let a bought room carry a floor modifier (the path floors and
hazards reused per room), so the two don't fight for the same decision. If paths turn out redundant there, hide them
in that layout with a switch; don't delete code.

## Build steps

1. **World refactor, no behavior change.** Introduce `World`; express Single and Wings on it; `Tools/SimTest` core,
   features and `wings` suites and `RenderCheck` stay identical. Layout setting becomes three-way (protocol bump).
2. **Room types and offers.** Room type data, seeded generator, a sim test (no overlaps, always connected, same
   result for the same seed).
3. **Expanding layout.** Milestone offer window (CRT widgets, like the path choice), purchase, host-only or vote,
   sync, camera and minimap growth, enemy scaling.
4. **Room features.** Shop room, floor-modifier rooms, then whatever else you want.
5. **Polish and balance.** Previews, bot balance run, README section.

## Decisions (owner, Nov 10)

- Who buys: **a team vote** (not the host alone).
- Currency: **a shared team purse for rooms**; personal gold stays for the shop.
- "Wings" is the Rooms arena as it is today.

## Step 1 (built)

- `World.cs`: `World` holds an arena's areas, doors, portals, wall spans and cover, and builds the walls and the
  steering floor plan (`With`). `World.Single` is the plain box; `Rooms.Wings` is today's Rooms map, built as data.
- `Rooms.Areas/Doors/Portals/Cover/WallsFor/With/AreaAt/ViewCentre/...` now forward to `Rooms.Wings`, so callers and
  tests didn't change. `Game.CurrentWorld` is the world in play; `ArenaMin/ArenaMax` read it.
- `Game.ArenaKind` (`ArenaLayout`: Single, Wings, Expanding) comes from the existing on/off Arena setting. The
  three-way setting and the protocol bump are left for step 3 (nothing is synced differently yet, so protocol stays 243).
- Checked: core + features suites, `wings` suite (new World checks), RenderCheck identical.

## Step 2 (built)

- `Expansion.cs`: `ExpansionRoom` = a box attached to a parent room (area index, 0 = hub), a `RoomKind` (Combat, Shop,
  Floor) and a path `Floor`. The map is the hub plus this list, so only the list ever needs syncing.
- **Shapes** (6): Closet 600x400, Room 800x600, Chamber 1000x700, Plaza 900x900, Hall 1200x300, Corridor 300x900.
- **Offers**: `Expansion.Offers(seed, round, built)` gives 3 different rooms from a deterministic generator
  (`DetRandom`, not `System.Random`, so every machine agrees). A room attaches to a wall of an existing room, sliding
  along it in 100 px steps with at least 300 px of shared wall for the door. Rooms with fewer doors are likelier
  parents, shapes not bought yet are 3x as likely, a shop is offered while the map is short of them (never first), a
  floor prefers a path floor not used yet.
- **Rules**: rooms never overlap and touch only through their own door (so the map is a tree: no duplicate walls),
  max 12 bought rooms, max 6400 x 4000 overall. Price = 40 + area/12000, x1.3 shop, x1.15 floor, +20% per room
  already bought (first guesses).
- `Expansion.Build(rooms)` returns the `World` (walls per room, one door per child). With nothing bought it is the
  hub alone, walled in; step 3 decides when the Expanding layout counts as unwalled Single.
- **For step 3**: the world's bounding box has void in it, so random spawns, props and the minimap must go by area
  (`AreaAt`), not by the box.
- Test suite `expand`: determinism, no overlaps, connected, open doors with steering spots, the cap, variety.

## Open questions (defaults in bold)

- Do expansions ever shrink or close (a destroyed room)? **No.**
- Do bosses appear in the grown map? **No: fixed boss arena.**
