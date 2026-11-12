# Breach rooms: a small free room behind every breach

Status: **steps 1 and 2 built** (protocol 300 for both): 20 s into a breach wave (or at its end) a Stash opens behind each breach left (`Game.RevealBreachRooms`, `BreachEvent.cs`). Decided with the owner on Nov 12, 2026.
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
- **Contents**: vectors. Each Stash gives every player `Game.StashVectors` (6) on opening (`AddVectors`, so the cap holds); no pickup, nothing on the wire. A walk-in claim would need a synced flag.
- **Look**: teal instead of gold (`SurpriseInk` in `SurpriseLook.cs`, rim, flash, minimap, a `STASH  +6 VECTORS` chip). Preview: `BREACHROOMS=1 BREACHAGE=25 STASHAGE=0.5 STASHAT=1 EXPANDMAP=6` (mode hud, wave 7).
- Friends pin the breaches and play the build-in when a Stash arrives (`ExpansionGame.cs`, the room list read); a joiner's first list is quiet.

## Open

- A walk-in vector claim (first player in, or a synced "claimed" flag) instead of the instant team payout.
- Cover or a fight inside the Stash; whether a Stash should keep its breach open (enemies still come out of it).
- The next breach wave's breaches are re-rolled for the bigger map (the Stashes are permanent).
