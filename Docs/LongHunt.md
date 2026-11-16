# Long hunt: a 20-step Boss Hunt with a save at 10 (design)

Status: **plan, nothing built** (Nov 2026). Builds on [`HuntMap.md`](HuntMap.md) and [`BossHuntsAndPerks.md`](BossHuntsAndPerks.md).

## Decisions (owner)

| Question | Decision |
|---|---|
| Quick Play | **Unchanged**: it stays the quick 10 waves + random boss ([`QuickPlay.md`](QuickPlay.md)) |
| Hunt length | The hunt gets a **length switch: 10 or 20 steps** (the lobby's Boss Hunt tab, host only; 10 stays the default) |
| Save | **A checkpoint at step 10** of a 20-step hunt: clearing it writes a resumable save (hunts aren't saved today) |
| Step 10 | A **mid-boss finale**: the Elite set-piece wave, always an Elite node, so the halves read as two acts |

## What changes

- `Hunts.Length` (const 10) becomes the short length; the run's length is `Game.HuntLength` (10 or 20), sent in the start
  message. `HuntMap.Steps` stops being a const (rows are sized per map). About 25 places read them: `Hunts.cs`
  (`LastWave`), `BossSchedule.cs` (`HuntBosses`), `Waves.cs`, `RunSaves.cs`, `HuntMap*.cs`, `HuntMapUi.cs`, `HuntUi.cs`, `Hud*.cs`,
  `ApproachUi.cs`, `RunStatsUi.cs`.
- **Strength:** `Hunts.Step` (1-4 over waves 1-7) stretches so a 20-step hunt reaches step 4 around wave 14; the set piece
  waves move to 10 and 19. The 20-step boss fight is the same boss at the same strength (Boss Lab data holds).
- **Power:** more waves means more XP and picks, so `BossPowerScale` pushes the boss harder; re-run `huntbal` (plain, safe,
  elite, random routes) at 20 and trim clue value (`HuntMap.ClueHpCut`, cap) if elites are too common: 3 clues is easy at 20 steps.
- **Map:** 20 rows, three lanes, same link rules. The screen scrolls vertically to the current row. Rest/Shop spacing
  (never two in a row, one before the boss) and "an Elite at the middle" (row 10) stay.
- **Save:** a hunt save at the step-10 checkpoint: map seed + path, clues, gold, build (as `RunSaves.cs` keeps a run), the
  hunt boss and length. **Continue** on the lobby resumes at the map's step 11. Co-op: host's save, as runs.
- **Records:** 20-step hunts keep their own best time / wins (a `long` flag beside the boss) so 10-step records stay clean.
- **Protocol:** bump (start message length, snapshot map size, save format); line in `ProtocolHistory.md`.
- **Tests:** `huntmap` and `hunts` suites run at both lengths; a save/resume test at step 10; `huntbal` at 20.

## Build order

1. Length as a setting (`Game.HuntLength`), 10 default, everything reading the old consts; no behavior change at 10.
2. 20-step map generation + scrolling screen + strength curve + step-10 Elite.
3. The step-10 save and Continue.
4. Records flag, README, tuning with `huntbal`.

## Open

- Does the 20-step hunt still end at the same Warden fight, or may a mid-boss node (a weaker boss) appear at step 10? (Plan: Elite set piece only.)
- Do the later bosses' hunts (no factions yet) come as 10-only until they have one?
