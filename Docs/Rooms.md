# Rooms: a hub with side rooms on every regular wave

**Status: phases 0-3 built (protocol 234): the Arena setting, the Wings world and its camera, nests, spills, locked
doors, room rewards, the minimap, the vault with the Gatekeeper, and a first tuning pass (Rooms-only 1.3x enemy damage).
Next: play it in Unity and adjust; then phase 4 (the power-stacking nerf pass).**
Mockup: [`Mockups/rooms.html`](Mockups/rooms.html) (https://claude.ai/artifact/4GPVBfq2ytFkB3oKkbSevj): three maps (Wings,
Cross, Annexes), hallways on or off (with the vault), a following camera at 1x / 1.5x / 2x, minimap or room strip, split
up or stay together, a live sim.

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

Second round (after the mockup, Nov 2026):

5. **Maps: simple first.** Build **Wings** first, then **Annexes**, then **Cross** later.
6. **Hallways always.**
7. **The vault: one wave in ten, holding a mini-boss.** For now that's **The Gatekeeper**, who leaves the Data Gates:
   the mini-boss duel is no longer a Data Gate challenge, and his duel room's look becomes the vault's. **Timing**: the
   duel Data Gate stays until the vault is built, then the Gatekeeper moves in the same change (he never drops out of the
   game in between). The other four Data Gate challenges stay as they are.
8. **HUD: the minimap.** No room strip.
9. **Doors lock behind you** while the room's nest lives (the vault's while the Gatekeeper lives).
10. **Spill squads are fast melee** for now (runners), so they reach the hub as a real threat.
11. **Layouts stay** in the hub (the boss-cycle layout as today), and rooms and hallways get some small obstacles.

Third round (Nov 2026):

12. **Classic stays.** Today's arena is kept exactly as it is, as **Arena: Classic**, next to **Arena: Rooms**.
    - **A setting for now**: a lobby choice on the Difficulty tab, a flavour knob like `DiffKnob.ArenaLayouts` (the same
      on every preset, doesn't make a Custom difficulty; a new `DiffKnob` value at the end). It can become its own game
      mode in the Play menu later. The host's choice is synced with the lobby and saved with the run.
    - **Classic is the default** until Rooms is tuned.
    - **Classic is untouched**: one 1600×900 screen, edge wrap, enemies from the edges, today's camera, and the
      Gatekeeper's duel Data Gate. Every rooms code path checks the setting.
    - **Balance is per arena**: the phase 3 changes (more enemy damage, less player health, room caps, spills) apply to
      Rooms only. The stacking nerf pass (phase 4) is judged per arena.
    - **The Gatekeeper moves to the vault in Rooms only**; Classic keeps his Data Gate.
    - **Records say which arena**: run history, saved runs and the session recorder note it.
    - A git tag (`classic-arena`) on `main` before rooms code lands, as an exact snapshot.

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

### Hallways and the vault (owner: yes, Nov 2026)

- **Why**: with one door per room, every room is a dead end and the safe play is to stand in the hub and shoot through
  the doorway while enemies funnel into the choke. Hallways join neighbouring rooms into a **loop round the hub**:
  enemies can come at you two ways (no free door camping), and you can run the loop to kite. The loop also replaces
  the feel of today's edge wrap, which stays off on room waves (a literal wrap would be confusing with a following camera).
- **Galleries, not corridors**: **260 px wide** (the mockup), short, maybe a pillar at a corner. A narrow corridor with
  a volley coming down it can't be dodged, which is face-tanking again.
- **Per map** (mockup):
  - **Wings**: a gallery above and one below the hub, each joining West and East (a door from each room into each).
  - **Cross**: four corner galleries (West to North, North to East, East to South, South to West): a ring.
  - **Annexes**: a side door between the two rooms on each side, and a gallery across the top joining the top two.
- **Spills**: a room with several doors to the hub or a hallway picks one at random for each spill (never into the next
  room). The minimap's spill ring and the amber warning sit on that door. Hallway spills take longer to arrive but come
  from where you aren't looking.
- **The vault** (decision 7): a small room off a hallway on **one regular wave in each 10-wave stretch** (picked at random,
  never a boss wave), holding a **mini-boss**: The Gatekeeper for now, with his duel's attacks, health share
  (`Challenges.MiniBossShare` of `BossEquivalentHp`), boss bar and the duel room's look (ground tint and colour). Beat him
  and an **altar gives everyone a prize pick** (rare or better, sometimes a legendary), as the duel does now. **Optional**:
  the wave ends without it, and the rooms keep spilling while you're inside; its door locks behind you until he falls.
  More mini-bosses can join the vault later. Rooms behind rooms otherwise stay out: each
  layer adds backtracking, and a far room's spills take so long to reach the hub that the pressure fades.
- **Routing**: steering becomes a path over doors (the mockup does a shortest path over door crossings), not just
  "head for the one door". Enemies and bots both use it.

## How a wave plays

1. **Start**: the hub is quiet (maybe a small opening squad so it isn't empty). Each open room has its nest and a first
   group of enemies. Rooms are dim on the minimap until someone looks inside.
2. **In a room**: the nest keeps its room topped up to a **room cap** (about 5-8 alive, scaled by wave and party) from
   the wave's enemy mix. Room enemies stay in their room until they spill, unless they've seen a player.
   **Doors lock behind you** (decision 9): once a player is inside and the nest lives, that player can't leave until the
   nest dies. Doors lock one way: teammates can still come in (to help or revive), and spills still go out.
3. **Spill**: every `SpillTime` (about 20 s, shorter later in the run) an uncleared room sends a squad of 3-5 **fast
   melee runners** (decision 10) through one of its doors into the hub or a hallway. They hunt the nearest player. A
   warning flashes over that door 2 s before.
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

0. **Measure** (**built**, run-log format 3): every 5 s sample now has, per player not downed, `near`: `nearest` (px to the
   nearest enemy, -1 for none), `within300`, `inView2x` (enemies inside the 800×450 view the 2x zoom shows, held inside
   the arena like the camera) and `shotsInView2x`. `Tools/SessionReport/report.py` prints them as "Threats near you" per
   wave. Record a few runs on the current game for the before picture.
1. **The Arena setting (Classic / Rooms, Classic by default) first**, then **world and camera, Wings only**: a world bigger than the hub on regular waves, a following camera, wrapping off,
   the two Wings rooms and both hallways (walled, with doors and small obstacles), the hub's layout kept, edge arrows,
   path steering over doors. Check the zoom feels right in Unity.
   **Built (protocol 233)**, as follows:
   - **Setting**: `DiffKnob.ArenaRooms`, a flavour knob (Classic on every preset), "Classic arena / Rooms arena" on the
     lobby's Difficulty tab under the approach and layout picks. Saved runs and the lobby carry it like any knob; the
     session recorder writes `"arena": "Classic"` or `"Rooms"`. The `classic-arena` git tag marks `main` before this.
   - **World** (`Rooms.cs`): the hub 0..1600 x 0..900; West and East rooms 900 x 900; Top and Bottom halls 260 px tall
     across the whole width (-900..2500). Doors 160 px: hub-West, hub-East, and each room into each hall. Walls 24 px,
     the world's outer edge walled too. Cover: two pillars and a short wall in each room, two blocks in the top hall,
     two pillars in the bottom hall.
   - **How it's done**: the world is one box symmetric round the hub, so `ArenaMin`/`ArenaMax` simply become the world
     while `RoomsOn` and `Walled` turns on (clamp instead of wrap, bullets culled at the world edge: no new code for
     those). The walls are layout solids: `Rooms.With(LayoutId)` is the cycle's floor plan minus its edge wall plus the
     rooms' walls and cover, with waypoints either side of every door, so the existing `LayoutDir` corner graph routes
     enemies through doors. Spawning falls back to the world's edges, which are all in rooms and hallways.
   - **When**: `PickRooms` at each wave start (before `PickLayout`): regular waves only; not in Boss Hunts, the Practice
     Range, the Showcase, with Tiny Arena or on the Narrow Pass path. A boss wave runs `PullIntoHub` (players moved in,
     props outside removed). Data Gates open in the hub (`HubMin`/`HubMax`); inside a Data Gate's challenge the world is
     the hub. Synced as the top bit of the layout byte. The 30% layout enemy bonus only counts a real floor plan.
   - **Camera** (`CameraZoom.cs`): follows you at any zoom while `RoomsOn` (`Rooms.ViewCentre`: inside your area, an
     area smaller than the view centred, then inside the world), sliding at `RoomEase` through doors; at 1x in the hub it
     is exactly the classic view. Off-screen arrows show at 1x too. Backdrop and grid cover the whole world
     (`RoomsLook.cs`), doors get cyan brackets.
   - **Bots**: the smart bot steers through doors (`SteerDir`, the enemies' steering generalised). The simple test bot
     doesn't, but enemies come to it.
   - **Run log**: the phase 0 "in a 2x view" numbers use the rooms' camera framing while the rooms are up.
   - **Tests**: `run.sh 1 30 wings`. **Preview**: `ROOMS=x,y [ZOOM=1.5] Tools/Preview/shot.sh <name> hud`.
   - **Not yet**: nests, spills, locks, rewards, the minimap, the vault (phase 2); paths' hazards (Burning Grounds,
     Frozen Lake, Fogbound) still only cover the hub; teleports and blinks can still cross a wall, as with layouts.
2. **Nests, room spawning, fast-melee spills, doors that lock behind you, clear reward, the minimap.** The hub goes quiet
   except for spills. Then **the vault**: one wave in ten, the Gatekeeper moved out of the Data Gates into it.
   **Built (protocol 234)**, as follows (all in `Rooms.cs` unless named; numbers in `Rooms`):
   - **Nests**: `StartRoomsWave` (after `BuildWave`) puts a Nest (`NestHpMult` 3x health, `Enemy.Home` = its room, no
     shards: `LateGame.cs`) at each room's `NestSpot`. `Enemy.Home`/`Spilled` are host-only; children inherit them.
   - **Spawning**: the wave's queue (singles and squads, `Variety.cs`) only spawns while a room with a living nest is
     under `RoomCap` (5 + wave/8 + 2 per extra player, max 12), at `RoomSpawnSpot` by that nest. With every nest down
     `RoomsSpawnReady` clears the queue. The total still comes from `BuildWave`.
   - **Guarding**: `RoomsLeash` (from `TickEnemies`) keeps a room's enemies orbiting their nest, not firing, while it
     lives and nobody is in the room; a player inside is their target. Once the nest dies they hunt.
   - **Spills**: `TickRoomsWave`: each room's `SpillT` (first after `FirstSpill` 18 s + 4 s per room, then
     `SpillTime` 22 s down to 11 s) sends `SpillSize` Runners (3 + wave/15 + 1 per extra player), `Spilled`, out of
     `NextDoor` (random each time: hub or hallway). Warning: the door flashes amber `SpillWarn` 2 s before.
   - **Locks**: `AreaLocked` (a room while its nest lives, the vault while its mini-boss does); `RoomsHold` in your own
     game's movement (`Game.cs`) keeps you in the area you were in. One way.
   - **Clear**: nest dead and no unspilled `Home` enemies left → `ClearRoom`: two big orbs, `ClearXp` (30 + 3 a wave)
     for everyone, teal doors.
   - **Vault**: `VaultWaveFor` picks one regular wave per stretch of ten (from the stretch's 2nd wave). The world's
     top grows by `VaultH` (`MinFor`, `WallsFor(true)`: the top wall gets a door, the vault its own walls). The
     Gatekeeper spawns with `Home` = vault (his Data Gate attacks and health share), stays in (leash), and the wave ends
     without him (`FoesOutsideVault`; `EndRoomsWave` removes him and lets anyone out). `WinVault`: a prize pick each
     (`PrizePicks`, the duel's legendary chance). The Data Gate pool drops `MiniBoss` with the Rooms arena; Classic
     keeps it. His intro banner is skipped and his boss bar only shows at the vault (`InVault`, `AtVault`).
   - **Sync**: `SyncRooms` after the layout byte: per room flags, spill tenths, next door; the vault's flags.
   - **Look** (`RoomsLook.cs`): room tints (red, teal once cleared, gold vault), door colours (red, flashing amber,
     solid bar when locked, teal, gold), the minimap (top right, `DrawRoomsMinimap` from `Hud.cs`).
   - **Tests**: `wings` covers all of it, including smart bots clearing a whole wave. **Preview**: `ROOMS=x,y [VAULT=1]
     [ROOMCLEAR=1]`.
3. **Tuning**: room caps, spill timing, how many rooms a wave opens. Then raise enemy damage and lower player health
   until a regular wave costs real health again. A `rooms` SimTest suite to check the numbers.
   **Built (first pass)**. The measuring tool is a report, `Tools/SimTest/run.sh 1 1 arenabal [last wave] [seed] [damage x]`:
   two Good bots play the same seed on Classic and on Rooms, and each regular wave prints its length, damage taken (%
   of max health), threats and enemy shots in a 1x view, and downs (`ARENADBG=1` prints a dump when a wave runs past
   150 s, and every down with what was near). Final numbers, 8 seeds, waves 1-24 (medians, Rooms vs Classic):

   | | Classic | Rooms |
   |---|---|---|
   | Wave length | 30 s | 44 s |
   | Damage taken per player per wave (% of max HP) | 20% | 34% |
   | Threats in a 1x view (average) | 9.6 | 3.8 |
   | Enemy shots in a 1x view | 28 | 9.5 |
   | Downs per run / lost runs (of 8) | 0 / 0 | 1 / 1 |
   | Worst moment, threats in view (median wave / 90th percentile) | 29 / 36 | 12 / 16 |

   Changes from it:
   - **Room cap** 4 + wave/7 + 1 per extra player, max 10 (was 5 + wave/8 + 2 a player, max 12): the worst moments.
   - **Rooms-only enemy damage 1.3x** (`Rooms.EnemyDamageMult`, applied in `Combat.cs` while `RoomsOn`; boss waves
     and Classic unchanged). Player health is left alone (it would change the boss fights too). The bots take ~1.7x
     Classic's damage with it; they're a weak stand-in for skilled players (who took almost nothing in the Nov 9 run),
     so **this is the number to adjust by playing**.
   - **Spills come out on the far side of the door** (they spawned on the room side, so a hallway spill walked back
     through the room). A spill also switches door just before its warning if someone stands where it would come out
     (`SpillClear` 180 px).
   - **The vault never comes before the first boss** (`FirstVault` 6): a Data Gate-strength mini-boss that locks you
     in was too much at wave 2.
   - **Steering**: a corner with no way on from it now picks another corner instead of walking into the wall (Rooms
     only), and the rooms' own walls keep steering spots 24 px clear (stubs where two room walls meet stranded
     Runners). The routing test now includes Runners and hallway-to-room trips.
   - **Bots** (F1 "Bot plays for you", the reports): walk a door route directly when nothing threatens them; their
     move scoring knows walls, locked doors and room corners (and no longer treats the rooms as "off the edge"); they
     turn past crates, barrels and chests (both arenas); locked in a room they clear it before going to revive; they
     leave the optional vault alone; the treasure bot routes through doors.
   - The bots still lose a run now and then on Rooms (locked in with a melee rush, a person would kite) and can
     stall (a slow wave in a few hundred); the report shows these.
4. **The power-stacking nerf pass** (its own doc): kill speed vs enemy health past wave 20, full-heal sustain, parry.
5. **More maps** (Annexes, then Cross), more vault mini-bosses. **Polish**: paths in rooms, room plans with character, bots.

## Open questions

- Doors lock one way (teammates can come in): confirm.
- Once Annexes and Cross are built: how they're picked (per boss cycle, by wave?) and how many rooms a wave opens.
- Which mini-bosses join the vault after the Gatekeeper.
- How many room plans, and do rooms have their own types (an elite room, a hazard room, a loot room)?
- What does a cleared room give exactly: a chest every time, or a chance?
- Endless mode and Boss Hunts: rooms too, or the hub only?
