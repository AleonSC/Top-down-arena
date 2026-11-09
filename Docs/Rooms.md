# Rooms: a hub with side rooms on every regular wave

**Status: design only, nothing built.** Next step: a mockup of the map and the camera (`Docs/Mockups/rooms.html`).

## Why

Owner's feedback (Nov 2026, after a 2-player win on Normal, 50 waves, Knight + Hemomancer, protocol 232): the late game
is too much to read without leaning on abilities, healing and kill speed. But nothing is a threat either: we took almost
no damage. Raising damage or lowering health today would make the late-game screen unsurvivable without the same
over-stacked builds. With the scroll-wheel zoom on, the arena also feels too small: zoom in and the enemies are right
outside the view.

**The idea:** keep the total number of enemies, but spread them over side rooms that you hunt (or that come to you).
Then fewer threats are near you at once, each one is readable, and **enemy damage can go up** without the game turning
into face-tanking. The order of the work matters:

1. Rooms bring down how many threats you face at once (this doc).
2. Then raise enemy damage and lower player health until a hit matters.
3. Then nerf the power stacking (kill speed, full-heal sustain) so a build can't skip 1 and 2. A separate doc.

### What the run log said (`session-20261109-122113.json`)

| What | Number |
|---|---|
| Downs in 50 waves | 1 (wave 5, a Warden Guard) |
| Lowest health on regular waves after wave 10 | about 50%, most waves 85-100% |
| Waves 46-49 | 17-21 s each, 5 damage taken between both players |
| Damage taken on boss waves | about half of all damage taken |
| Healing received vs damage taken | almost equal, nearly every wave (the Hemomancer refilled everything) |
| Damage parried | 7,835 (the Knight: 5.6k parried vs 9.3k taken, about 38%) |
| Enemies alive at once (regular waves) | 11-18 usually, peaks about 30 |
| Enemy bullets alive (regular waves) | 30-80 usually, peaks about 120 (waves 27-38 the busiest) |
| Average enemy lifetime | 2-8 s (Grunt 7.4, Swarmer 4.5, Spiker 4.8, Shard 1.9) |
| Boss kill time | Warden (w5) 252 s, Devourer (w45) 59 s; Eclipse took 1.8M damage in 149 s |

Enemies spawn near you and die before they set up. What does hit you is parried or healed back. Rooms fix the first half
(enemies get time and space to be a threat, and you have to travel to them). Step 3 fixes the second half.

The log has no record of **where** enemies were relative to you, so the zoom problem isn't in the data. Phase 0 adds that.

## Decisions (from the owner)

1. **Rooms on every regular wave.** Boss waves stay in the hub, open (bosses are tuned for 1600×900: the Eclipse ring,
   the Herald spokes, arena-wide pulses).
2. **Pressure: nests that spill.** Each room has a nest. An uncleared room sends a squad through its door into the hub
   every so often. Kill fast and it stays calm; kill slow and it piles up.
3. **Players can split up freely.** Clearing two rooms at once is faster, but you leave your healer, your revives and
   your auras behind. A real risk/reward choice.
4. **A small reward for each cleared room** (a chest, orbs or XP), so going in is worth it and not a chore.

## The map (first draft; the mockup decides)

- **The hub is today's arena**, 1600×900, at the same world coordinates (0..1600, 0..900). Everything tuned for the hub
  (bosses, paths, layouts' inner walls, challenge rooms) keeps working unchanged.
- **Up to four room slots** on the hub's sides: left and right (about 900×900), top and bottom (about 1600×600). A
  wave uses **2-4 of them** (more later in the run and for bigger parties). Unused slots stay walled off.
- **Doors**: each room joins the hub through one 160 px doorway, placed where today's red enemy gates are. Doors are
  open both ways. The hub's edge wall is solid everywhere else, so **wrapping is off** on room waves (it stays on for
  boss waves).
- **Rooms have their own cover**: a few walls, pillars and crates from a small set of room plans, reusing `Solid`,
  `LayoutCrate` and the waypoint steering in `LayoutRules.cs`.

## How a wave plays

1. **Start**: the hub is quiet (maybe a small opening squad so it isn't empty). Each open room has its nest and a first
   group of enemies. Rooms are dim on the minimap until someone looks inside.
2. **In a room**: the nest keeps its room topped up to a **room cap** (about 5-8 alive, scaled by wave and party) from
   the wave's enemy mix. Room enemies stay in their room until they spill, unless they've seen a player.
3. **Spill**: every `SpillTime` (about 20 s, shorter later in the run) an uncleared room sends a squad of 3-5 through
   its door into the hub. Spilled enemies hunt the nearest player. A warning flashes over the door 2 s before.
4. **Clear**: kill the nest and the room's enemies and the room is cleared: the door light turns teal and the reward
   drops (a chest, or orbs and XP).
5. **Wave end**: every room cleared and no enemies left in the hub. The wave's total enemy count still comes from
   `BuildWave`; rooms just divide it.

Target feel: **at most 8-10 threats in view at once** on a regular wave, wherever you stand. Then enemy damage goes up.

## The camera

- The camera **follows you** and is clamped to the world. At zoom 1 in the hub it shows the whole hub, exactly like
  today. Step through a door and it slides to the room (rooms fit the screen at zoom 1).
- The scroll-wheel zoom (`CameraZoom.cs`) now has a real use: zoom in for detail, and the rooms keep enemies out of
  the part of the world you can't see.
- **Off-screen markers**: arrows at the screen edge for spilling squads, a downed teammate and the nearest enemy
  shooting at you from outside the view.
- **Minimap** (small, corner): the hub, the rooms (dim / lit / cleared), teammates, the doors' spill timers.

Each client already draws its own screen, so every player's camera follows themself. No split screen.

## Co-op

- Splitting is allowed. A downed player in another room is marked on the minimap and with an edge arrow; reviving means
  going there.
- Healers: auras and heals already have a range. Splitting off from the healer is the price of speed.
- Bots (`BotBrain.cs`) don't path round walls today (`ArenaLayouts.md`, "Known limits"). They'll need to follow doors at
  least: a bot sticks with its human, or goes to the nearest uncleared room through its door.

## What has to change (where)

| Area | Change | Where |
|---|---|---|
| World size | The world is bigger than the hub on room waves. Everything that clamps, wraps or picks a random spot uses the hub or the world bounds as appropriate (about 20 `KeepInArena` / `RandomInArena` calls, about 135 uses of `Width` / `Height`) | `Game.cs`, `LayoutRules.cs`, many |
| Rooms | Room plans (walls, door, nest spot, crates), which slots a wave opens, room state (nest alive, cleared, spill timer) | new `Rooms.cs` |
| Spawning | Room enemies spawn in their room; spills come through the door; the hub gets the spills | `SpawnEnemy`, `BuildWave` |
| The nest | A room nest: a new `EnemyType` at the end of the enum, or the existing Nest with a room flag. It spawns the wave mix instead of Shards | `Defs.cs`, `EnemyAI.cs` |
| Wrapping | Off on room waves; the hub's edge wall has doors instead of player gates | `Layouts.cs`, `LayoutRules.cs` |
| Bullets | Cull at the world's edge, not the hub's; walls already stop shots | `Collide` |
| Camera | Follow, clamp, ease between hub and rooms; zoom 1 shows the hub as today | `CameraZoom.cs`, `CameraLean.cs`, `WorldRenderer.cs` |
| HUD | Minimap, edge arrows, door spill warning | new `Game/RoomsHud.cs` |
| Look | Room floors, doors (red = uncleared, teal = cleared), the nest | `LayoutLook.cs` style, mockup first |
| Paths | Path hazards are hub-shaped (Frozen Lake, Burning Grounds, Fogbound, Narrow Pass): decide room by room whether a room gets the hazard | `Paths.cs` |
| Layouts | Inner walls can stay in the hub; the edge-gate part is replaced by doors. Maybe rooms replace layouts on regular waves | `Layouts.cs` |
| Challenge rooms | The portal opens in the break, in the hub; unchanged | `Challenges.cs` |
| Bots | Use doors | `BotBrain.cs` |
| Network | Room state in the snapshot; **bump `Session.ProtocolVersion`** | `NetSync`, `Session.cs` |
| Run log | New sample fields: nearest enemy distance, enemies in view, enemies in the player's room | run stats |
| Tools | Preview and RenderCheck for a world bigger than the hub; a `rooms` SimTest suite | `Tools/` |

## Phases

0. **Measure** (small, worth doing first): add *nearest enemy distance* and *enemies within the view at 2x zoom* to the
   run log samples, and a SimTest baseline for threats near you and damage taken per wave. This is the before picture.
1. **World and camera**: a world bigger than the hub on regular waves, a following camera, wrapping off, empty walled
   rooms with doors, edge arrows. Check the zoom feels right in Unity.
2. **Nests, room spawning, spill, clear reward.** The hub goes quiet except for spills. Minimap.
3. **Tuning**: room caps, spill timing, how many rooms a wave opens. Then raise enemy damage and lower player health
   until a regular wave costs real health again. A `rooms` SimTest suite to check the numbers.
4. **The power-stacking nerf pass** (its own doc): kill speed vs enemy health past wave 20, full-heal sustain, parry.
5. **Polish**: paths in rooms, room plans with character, bots.

## Open questions

- How many room plans, and do rooms have their own types (an elite room, a hazard room, a loot room)?
- Does the hub keep the cycle's layout walls, or do rooms replace layouts on regular waves?
- Should a room's door close behind you while its nest is alive (a lock-in), or stay open?
- Spill squads: the room's mix, or always fast melee so they reach the hub as a real threat?
- What does a cleared room give exactly: a chest every time, or a chance?
- Endless mode and Boss Hunts: rooms too, or the hub only?
