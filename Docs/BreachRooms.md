# Breach rooms: a small free room behind every breach

Status: **steps 1-5 built** (protocols 300, 301): as a breach wave's breaches come up a Stash opens behind each breach left (`Game.RevealBreachRooms`, `BreachEvent.cs`). Decided with the owner on Nov 12, 2026.
Builds on [`SurpriseRooms.md`](SurpriseRooms.md) (placement, free rooms) and [`RoomBuilding2.md`](RoomBuilding2.md) (breaches). Expanding arena only.

## Decisions (owner)

| Question | Decision |
|---|---|
| Scope | **A small room behind every breach**, on every breach wave. The gold breach event (big surprise room) stays as it is. |
| Size | **Stash**: 5 x 3 tiles (500 x 300 px), a plain rectangle, free, no vote. |

## Mock

- `SurpriseShape.Stash` (end of the enum) and `Expansion.SurpriseBase`: 5 x 3 floor.
- Preview: `BREACHROOMS=1 EXPANDVOTE=6 EXPANDMIDWAVE=1 EXPANDDRAW=0,0,0,0` (mode hud; minimap) puts a Stash behind each breach with `SurpriseOptions(..., through: breach wall + Behind)` and `PinBreaches`.
  In the 6-room map, 3 of the 4 breaches had a legal spot (one was blocked by a neighbour).

## Built (step 1), defaults taken for the open questions

- Opens **as the wave is cleared** (called in `Waves.cs` before the gold event's reveal), so no breach is torn down mid-fight; empty (no cover or pickup yet); stops within 2 rooms of `MaxRooms`.
- The Stash is not a gift: `NextSurpriseShape`/`SurpriseCount` ignore it, so the boss and event rotation is unchanged. Test: `ExpandingBreachEventTest`.

## Step 2 (built, decided Nov 12)

- **Timing**: 20 s into the wave (`BreachEvent.Delay`, from `TickBreachEvent`), once a wave; the wave-end call stays as a fallback. A breach with a Stash behind it is torn down (`PinBreaches`), so that breach stops spawning; with none left, enemies come in at random spots.
- **Contents**: vectors (step 2 paid the whole team 6 on opening; replaced by the walk-in claim in step 3).
- **Look**: teal instead of gold (`SurpriseInk` in `SurpriseLook.cs`, rim, flash, minimap, a `STASH  +6 VECTORS` chip). Preview: `BREACHROOMS=1 BREACHAGE=25 STASHAGE=0.5 STASHAT=1 EXPANDMAP=6` (mode hud, wave 7).
- Friends pin the breaches and play the build-in when a Stash arrives (`ExpansionGame.cs`, the room list read); a joiner's first list is quiet.

## Step 3 (built, protocol 301): walk-in vector claim

- Each Stash holds a cache. The first player to stand in the box takes `Game.StashVectors` (10, `AddVectors` so the cap holds); once. `TickStashClaims` (host, `BreachEvent.cs`, from `TickRoomsWave`).
- Wire: `ExpansionRoom.Claimed` (a bool after `Surprise` in the `Msg.ExpandState` room list; saves get a trailing token). A claim-only change makes a friend's game swap the list quietly (no rebuild, no build-in).
- Look: a breathing teal diamond in the middle until claimed (`DrawSurpriseRoom`); the chip reads `WALK IN FOR +10 VECTORS`. An unclaimed cache stays for later waves.
- Test: `ExpandingBreachEventTest` (no pay on opening, the walker takes it, the friend sees it, once only).

## Step 4 (built, no wire change)

- **The door lines up with the breach**: of the legal Stash spots, the one whose doorway (middle of the shared wall) is nearest the breach is taken (`Game.CentreOnBreach`, both in `StashAccept` and `RevealBreachRooms`).
- **The breach never closes**: `PinBreaches` ignores Stash rooms when dropping breaches (the gold event's room still drops its breach), so a Stash's breach stays open and drawn.
- **A share each**: `Breaches.Quota(total, breaches)`: the wave's `breachTotal` (set in `FloodBreachWave`: enemies + squads, times the flood) is split evenly; `BreachSpot` counts what each breach has let in (`breachUsed`, host only) and skips a spent one. When all are spent it returns null and the enemy comes in the usual way.
- **Cover**: `AddStashCover` (`Expansion.cs`): a block and a pillar on a diagonal either side of the cache; fins are skipped for a Stash and the outline-sliver rule does not apply to it. Test: every Stash spot has cover and none on the cache.
- **Marker**: the diamond is bigger (26-31 px) with a `+10` label over it (`DrawStashLabels`, `SurpriseLook.cs`, from `Hud.cs`).

## Step 5 (built, no wire change)

- An off-screen **cache marker**: a teal edge arrow with a ring for each unclaimed Stash, while zoomed out (`OffscreenMarkers.cs`, like the chest arrows).
- **Not built**: enemies coming out of the Stash. The breach spawns in front of the wall and the Stash joins the room by an ordinary door elsewhere, so spawning inside would have enemies walking round to the door; it needs pathing or a wall opening first. Owner had no preference, so it stays open.

## Timing change (Nov 12, no wire change)

Stashes now open **right away** (`BreachAge >= Breaches.Telegraph`, 1.5 s) instead of 20 s in; the owner found the wait too long. The gold event's room stays at 20 s.
- The gold breach is skipped, and a Stash may not overlap the room the event has planned (`TouchesPlannedEventRoom`).
- A friend works out the wave's breaches on the old map before new rooms arrive (`ExpansionGame.cs`), so an early Stash cannot re-roll them.

## Breaches only where a Stash fits (Nov 12, no wire change)

A breach used to open on any outer wall, and a few had no room behind them, so no Stash came. `Breaches.For` now takes an `accept` test (`Game.StashAccept`, `Breaches.cs`): each rolled spot is tried against the map plus the Stashes already given to the breaches picked before it, and dropped if none fits (another is rolled; the random numbers stay the same on every machine). So every breach has its Stash. Near the size cap (no Stashes) any wall goes. The gold breach is exempt from the Stash but its room is planned behind it as before.

## Open

- Enemies coming out of the Stash itself (see above).
- The next breach wave's breaches are re-rolled for the bigger map (the Stashes are permanent).
