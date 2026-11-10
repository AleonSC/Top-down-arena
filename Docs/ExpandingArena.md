# Expanding arena: design plan

Status: **plan only, nothing built.** Written Nov 2026 from the owner's idea and a read of `Rooms.cs`, `Paths.cs`
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

## Open questions (defaults in bold)

- Who buys: **host decides** (like paths), or a team vote?
- Currency: **a shared team purse for rooms, personal gold for the shop**, or one pool for everything?
- Is "Wings" the Rooms arena as it is today? **Yes.**
- Do expansions ever shrink or close (a destroyed room)? **No.**
- Do bosses appear in the grown map? **No: fixed boss arena.**
