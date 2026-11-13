# Wave & spawn refactor

**Status: steps 0-1 done** (suite `wavedump`; `WaveNumbers.cs`); steps 2-5 planned. Owner picked "refactor first".

## Why

One wave is built by six overlapping systems and spawned by four drivers, so each new ask touches many places.

### Building (`EnemyAI.cs` `BuildWave`, ~100 lines)
- Six builders share one function with early returns: boss switch, zombies (`BuildZombieWave`), path (`BuildPathWave`), late recipe (`WaveRecipe.cs`, wave 13+), legacy loop (waves 1-12), then the Approach overlay (`MixApproach`). Each early return must remember `MixApproach()` itself.
- They talk through hidden fields: `kinds`, `kindCap`, `Headliners`, `approachSingles/Squads/Trickle`, `singlesSinceSquad`.
- The enemy count is inline magic numbers (`4 + n*2`, caps 32/40, `+6` on wave 1, x1.25, layout/expansion/difficulty multipliers). `TimedWaves` (alive cap, window), `SpawnPortals` (burst size) and the legacy interval (`0.7/1.1 - wave*0.05`) have their own.
- `WavePool(n)` lists types from wave 14 on that `BuildWave` ignores (the recipe takes over at 13), but the new-enemy chips (`NewEnemies.cs`, `WaveClearedUi.cs`) read them, so they stay. Entries are out of order (Sapper before Phantom).
- Boss composition is a hardcoded switch in `BuildWave`, `Practice.cs` has its own copy, and `SpawnEnemy` hardcodes boss and Twin positions by type, after already drawing a random spawn spot (wasted rng draw).

### Queues and state
- Pending enemies live in `spawnQueue`, `squadQueue`, `designQueue`, `risers`, `portalCargo`. `Remaining` and `QueuedUnits()` count them in different places. 18 files touch the queues.
- Boss state is `BossSlot`, `Leg`, `BossStartsNext`, `Bosses.Slotted` vs `IsBossWave(n)`: two ways to ask "is this a boss wave".

### Spawning
- `TickWaves` Fighting case chooses among: Dev Arena, freeze, room-by-room, timed (portals or direct), legacy interval, designed wave, zombies. The "squad after 3 singles, else next single" dispatch is copied in `Waves.cs`, `TimedWaves.cs` and `SpawnPortals.cs`.
- The Intermission to Fighting block (~60 lines) is order-dependent: `PlaceFloatingRooms` > `PickRooms` > `PickLayout` > `BuildWave` > `StartRoomsWave` > `FloodBreachWave` (sets `spawnTimer`, read later by `BeginTimedWave`) > `CapSentries` > `WaveProps` > `BeginTimedWave`. `timedWave = false` for Practice is set again right after `BeginTimedWave` already checked it.
- `SpawnEnemyAt` (47 call sites, including boss minions) mixes HP scaling, modifiers, paths, elites, boss power, Quick Play and Core Carriers. Quick Play overwrites the HP `SpawnHp` just computed.

## Target shape

1. **`WavePlan`**: the wave as one ordered list of `SpawnEntry { Type or Squad, Source tag }`, plus the numbers (count, cap, window). Built by named stages run in a fixed order (`Boss`, `Zombie`, `Path`, `Recipe`/`Legacy`, `Approach`, `Breach flood`, `Sentry cap`). One place for the count math.
2. **`Spawner`**: one `Emit(entry)` and one **pacing** strategy per wave (Timed, Portals, Interval, Design, Room-by-room, Dev). Squad-vs-single dispatch exists once.
3. **One `Remaining`**: the plan's pending count + living foes + risers + portal cargo, from one method.
4. **`SpawnEnemyAt`** split into `NewEnemy(type)` (stats) + placement; boss positions move into the boss's own def.
5. Boss waves read their composition from `BossSchedule`, not a switch.

## Mode rules (owner)

- **Quick Play and Campaign build waves the same way.** Only the boss schedule and length differ; no separate wave logic.
- **Boss Hunt has more enemies per wave** than the others. How many is the owner's call (not decided yet).
- So the plan carries a per-mode **profile** (`WaveProfile`: count multiplier, alive cap, boss schedule) instead of mode checks inside the builders. Step 1's `WaveNumbers` holds one profile per mode; Quick and Campaign share one.

## Steps (one at a time, "start on step N")

0. **Safety net (done)**: a `wavedump` sim suite that prints every wave's queue (types, squads, counts) for waves 1-60 on fixed seeds across modes (normal, path, zombie, Quick Play, Boss Hunt, Campaign legs with boss slots, Expanding arena with breaches, Rooms arena, practice). Every step also keeps the `bossschedule`, `bossslot`, `longrun` and `portals` suites passing. Saved as a golden file. Every later step must keep it identical, unless the owner asks for a change.
1. **(done)** Pull the count math and magic numbers into one `WaveNumbers` (still `Balance.cs`-indexed); remove the double Practice reset (`WavePool` stays: the chips use it). No behavior change.
2. Introduce `WavePlan`; make each builder a stage that fills it; `BuildWave` becomes the stage list.
3. `Spawner` with pacing strategies; delete the three copies of the dispatch; one `Remaining`.
4. Split `SpawnEnemyAt`; boss positions and composition into data.
5. Then the owner's new wave behavior goes on top as one stage or one pacing.

No network or snapshot change is planned, so no protocol bump unless a step needs one.
