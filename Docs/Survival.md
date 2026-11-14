# Survival: one arena, one clock, last as long as you can (design)

Status: **steps 1-5 built** (`Core/Survival.cs`, `Core/SurvivalGame.cs`, suite `survival`; protocol 342). Step 3: the HUD (clock plate, held-for-pick plate, new-faction notice) and the end screen (`Game/SurvivalUi.cs`). Step 4: the lobby carousel entry and tab. Step 5: records (`Profile.SurvivalEnded`: plays, best time overall and per class, best phase; Recent runs and rank XP; the lobby tab shows them). The mode plays on the current arena for now; the big room and tuning (step 6) are left. Decisions below are the owner's; numbers are first guesses to tune with the
`Tools/SimTest` suites once built. Open questions at the bottom.

## The idea

A fourth lobby mode beside Quick Play, Campaign, Boss Hunt and Practice. The party fights in **one fixed arena** with
**no waves, no boss and no route**. A single clock runs, enemy pressure climbs with it, and the run ends when everyone
is down. **Score is time survived.**

How it differs from the rest:

- Quick Play's endless stretch is waves after a boss win; Survival has no waves at all.
- A Boss Hunt (with or without the hunt map) ends at a boss; Survival has no end to win.
- The hunt map is untouched: Survival has no map or votes.

## Decisions (owner)

| Question | Decision |
|---|---|
| Shape | One continuous clock. No wave plates, no bosses. |
| Arena | **One big room that extends off the camera**: larger than the screen, the camera follows the party, and it is still a single room (no doors, no expanding rooms). One leaderboard. |
| Enemies | Mixed factions by time: basic faction first, other factions phase in as the clock climbs. |
| Progression | Still **level based**: kills give XP, levels give the usual upgrade picks (no timed picks, no Bazaar pop-up). A pending pick **freezes the world** (clock, enemies and shots) until everyone has chosen. |

## The room (owner's answers)

- **Size:** about **4x the area of today's arena** (first reading of "4x the size"; if 4x each side was meant, say so).
- **Camera:** follows the player. In co-op each client's camera follows its own player, so players may spread out;
  no leash or zoom-out. Enemies spawn off each player's view, so the spawner treats every player's view as no-spawn.
- **Contents:** open floor with **a ton of crates and other destructible props** scattered across it, plus a few
  **simple walls in the very middle and the corners** as cover. Props use the existing `Arena props` system; crates
  can drop the usual pickups. Layout is fixed (same every run) so records compare.

## Spawner

A time-driven spawner replaces the wave builder (`Spawns.md` and `WaveRefactor.md` hold the wave side).

- **Pressure curve** `P(t)` rises with the clock (first guess: roughly linear for 10 min, then steeper). Each tick the
  spawner keeps a *budget* of on-screen threat near `P(t)` and spawns enemies to fill it, so killing fast brings more.
- **Tier ramp:** enemy health and damage scale with `t` on the same scale the waves use (wave 1 at 0:00, "wave 30" by
  about 15 min), so the existing balance data applies.
- **Faction phases:** 0:00 basic faction; a second faction joins around 4:00, a third around 8:00, and so on. Each
  phase adds that faction's roster and squads; earlier ones thin out rather than vanish.
- **Elite share** grows with `t` (the Elite variants from hunt-map Elite nodes).
- **Squads and set pieces** (Riot line, Last stand, ...) fire on a timer from the faction's own definitions instead of
  on wave numbers.
- **Big-room spawns:** enemies spawn just outside the camera view (never inside it) and at the room's far reaches, then
  path toward the party; a cap on live enemies, spawns away from players and a short grace at 0:00.

## Rules reused as they are

Levels and upgrade picks, classes, weapons, abilities, perks (equipped as in a run), revive and downed rules, the
Modifiers and Difficulty tabs, HUD (timer replaces the wave plate), the Tab screen, session JSON reports.

## Run end and records

- The run ends when every player is down. The end screen shows **time survived**, kills, level, damage, and the
  faction phase reached.
- Records keep per player: plays, **best time** (per class too, like other records), longest run in co-op. Survival
  runs are tracked as their own mode and are **not** counted as Quick Play or Campaign runs.
- Not saved to continue.

## Lobby

A **Survival** entry on the mode carousel with its own tab: the arena, the rules in a few lines, your best time, and
START. Uses `DevKit.cs` widgets (`DkPanel`, `DkTitle`, `DkKey`, ...) and a `Tools/Preview` render before building.

## Protocol

New mode value on the start message plus the survival clock in the snapshot, so **bump `Session.ProtocolVersion`** and
add a line to `ProtocolHistory.md`. Any new enum values go at the end, before `None`.

## Build steps

1. **Core spawner:** `Core/Survival.cs` (pressure curve, budget, faction phases) and test suite `survival`
   (determinism, cap, curve, phase timing). No Unity.
2. **Mode wiring:** mode id, start message, clock in snapshots, game-over rule; protocol bump.
3. **HUD and end screen:** timer plate, phase notice, end summary; previews.
4. **Lobby tab and carousel entry:** preview render sent before and after.
5. **Records:** best time per player and class; session report line.
6. **Tuning:** SimTest curves against the balance data; README section, `CODEMAP.md` lines, doc status.

## Open questions (owner)

- **Co-op scaling:** do 2 and 3 players raise the budget per head (first guess: yes, 1.6x and 2.2x), and are best
  times kept per party size?
- **Soft ceiling:** after some time (say 25 min) does the pressure keep climbing forever, or plateau so a very strong
  build can sit at the cap?
- **Milestones:** a plain "5:00 / 10:00" banner only, or a reward (rare card, heal) at each?
