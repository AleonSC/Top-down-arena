# Spawns: bursts, portals, flanks and surges

**Status: built** (protocol 303). Code: `Core/SpawnPortals.cs`, `Core/TimedWaves.cs`, `Game/SpawnPortalLook.cs`; test suite `portals`; preview `PORTALS=1 Tools/Preview/shot.sh portals hud 8`.

Before this, each single enemy picked its own random edge spot, so a wave felt like a steady drip from everywhere.

## The owner's picks

1. **Directional bursts**: singles come in groups from one spot instead of one by one.
2. **Flank spawns**: now and then a burst comes from behind the party.
3. **Shootable portals**: each burst comes through a portal; shoot it closed to cancel the rest.
4. **Surge rhythm**: the wave's arrivals come in three surges with lulls between them.

Decisions: a sealed portal cancels its cargo **and pays out** (coins in the Expanding arena, score everywhere);
surges are **felt only** (no banner or sound); flanks get **a red arrow round your player and a longer open** (1.6 s vs 1 s).

## How it works

- `TickTimedSpawn` works out how many enemies are due (`TimedWaves.Share`) and how many fit under the alive cap, then
  `TickPortalSpawn` opens a portal once a whole burst is due (or after 4 s with nothing open). Squads, Nests and
  bosses skip portals. Not on breach waves (the breaches are the way in) or any wave that isn't timed.
- A portal (`EnemyType.Portal`) is an enemy with no AI: it appears (0.3 s), opens (1 s / 1.6 s flank), lets one enemy
  out every 0.35 s, then closes (0.45 s) and is removed with no reward. Frozen, caged or held, it pauses.
- Spots come from `SpawnSpot` (the same rules regular spawns use), at least 160 px from players. A flank takes the
  most-behind of 8 candidate spots, opposite the party's average aim.
- The surge curve: the old ramp/plateau/taper times a pulse that is 1 at each surge (20%, 48%, 76% of the window,
  ±8%) and 0.4 between them. More portals may be open during a surge (3 vs 2).
- Look (`SpawnPortalLook.cs`): a digital "loading" gate; in a surge its `Winding` is on (set in `TickPortals`) and the ring chases faster; shot closed it sends `FxKind.PortalSeal` (a white flash, the ring blown apart; protocol 334).
- Wire: no new fields. `Phase` (0 opening, 1 emitting, 2/3 flank, 4 closing) and `Facing` ride the enemy snapshot;
  clients time the opening and closing from when they saw the phase change.

## Ideas not built

Drop-ins on marked circles near players; a reinforcement caller enemy; an escort convoy with a loot carrier.
