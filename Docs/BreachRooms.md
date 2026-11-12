# Breach rooms: a small free room behind every breach

Status: **steps 1-3 built** (protocols 300, 301): 20 s into a breach wave (or at its end) a Stash opens behind each breach left (`Game.RevealBreachRooms`, `BreachEvent.cs`). Decided with the owner on Nov 12, 2026.
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

## Open

- Cover or a fight inside the Stash; whether a Stash should keep its breach open (enemies still come out of it).
- The next breach wave's breaches are re-rolled for the bigger map (the Stashes are permanent).
