# Breach rooms: a small free room behind every breach

Status: **step 1 built** (protocol 300): a cleared breach wave adds a Stash behind each breach left (`Game.RevealBreachRooms`, `BreachEvent.cs`). Decided with the owner on Nov 12, 2026.
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

## Open (still to settle)

- When the room opens: at the wave's start with the breach, or after the breach has been held (like the gold event's 20 s)?
- What is in it: empty, cover, or a pickup.
- Cap: `MaxRooms` allows only so many; extra breach rooms must not crowd out the team's own rooms (skip when within 2 of the cap, as the event does).
- The new rooms are permanent map; the next breach wave's breaches are re-rolled for the bigger map.
- Wire: needs `Surprise` to carry the Stash and the breach set (protocol bump when built).
