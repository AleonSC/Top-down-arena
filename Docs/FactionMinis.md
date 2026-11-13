# Faction mini-bosses and themed nodes

Status: **step 1 built (protocol 328)**: the system and the Warden's Riot Captain. Steps 2-3 open. Decided with the owner on
Nov 13, 2026. Builds on [`FloatingRooms.md`](FloatingRooms.md) (the sealed mini-boss room), [`Nodes.md`](Nodes.md) and
[`Approaches.md`](Approaches.md) (factions).

## Decisions (owner)

| Question | Decision |
|---|---|
| What's themed | **A faction mini-boss and a faction node** each boss cycle |
| Which faction | **The next boss's faction** (the one whose enemies are building up) |
| The Warden's mini-boss | **A new Riot Captain**: tower shield, shield bash charge, calls Taser Guards, gas volley |
| The Gatekeeper | **Back to the Data Gates only** (every arena); mini-boss rooms and the Rooms arena's vault hold a faction's mini-boss |
| Rollout | This doc, the system and the Warden's mini-boss first; one faction at a time after |

## How it works (step 1, built)

- `FactionMinis.For(boss)` names each faction's mini-boss; `Game.MiniBossNow()` is the next boss's faction's.
- The Expanding arena's sealed mini-boss room (`RoomKind.Gatekeeper`, kept for saves) stores its foe when it is placed
  (`ExpansionRoom.Mini`, synced in `Msg.ExpandState`, saved as the room line's 14th field), so a room placed in the Warden's cycle
  stays a Riot Captain room. Old saves' rooms (no foe) take the current cycle's. The map labels the room with its mini-boss's name.
- The Rooms arena's vault spawns `MiniBossNow()`; the Gatekeeper's Data Gate is back in the Rooms arena's gate pool.
- Factions without their own mini-boss yet bring the Riot Captain.

## The Riot Captain (the Warden's)

`Core/RiotCaptain.cs`, look `Game/RiotCaptainLook.cs`, test suite `captain`, preview `CAPTAIN=0|1 ZOOM=1.6 shot.sh x hud`.

- 65% of the Warden's health at your wave (like the Gatekeeper). A tower shield blocks the front (~65° each side), never breaks,
  turns 1.5 rad/s.
- Attacks in turn (charge, buckshot, gas, charge, buckshot, whistle), every ~2 s:
  - **Shield charge**: 0.9 s lane, 560 px/s for 0.7 s; 18 damage and a 220 px knockback. Into a wall: **dazed 2.2 s** (no shield,
    +50% damage). Otherwise shield low 1.2 s.
  - **Buckshot**: 3 fans of 7.
  - **Tear gas**: 3 canisters round you, rings fill 0.8 s.
  - **Whistle**: 2 Taser Guards at the room's far corners (max 2 up); they stay in the locked room and go when it falls.
- Below half health: a quarter faster, charges twice, 4 fans, 4 canisters.

## Themed nodes (step 2, proposals: pick one per faction)

A faction node is placed with the cycle's other nodes and claimed like a chest node (a room round it).

| Faction | Proposal |
|---|---|
| Warden | **Evidence Locker**: a steel locker; opens for everyone's choice of one Riot Gear item (Docs/RiotGear.md) for the cycle |
| Hive | **Honey Cache**: a comb; standing in its room heals 2%/s between waves and drops royal jelly (a free reroll) |
| Reapers | **Grave Shrine**: a lantern; claim it for one free revive per player this cycle |
| Singularity | **Lens Array**: a prism; its room's floor bends enemy shots away from you |
| Colossus | **Ore Vein**: a crystal seam; every wave mined there pays vectors |

## Plan

1. **Built**: faction mini-boss system, the Riot Captain, the Gatekeeper back to Data Gates only. Protocol 328.
2. Faction nodes: the Warden's first (once the owner picks), then the rest.
3. One mini-boss per remaining faction (Hive, Reapers, Singularity, Colossus), one per session.
