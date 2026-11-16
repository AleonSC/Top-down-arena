# Long hunt: a 20-step Boss Hunt with a save at 10 (design)

Status: **steps 1-2 built** (step 1: length plumbing, `Game.HuntLength`, `Session.HuntLong`, protocol 363; step 2: the lobby's Long hunt switch, 20-row scrolling map, `Hunts.StrengthWave`, the Miniboss node, the hunt map is the only hunt); steps 3-4 left (Nov 2026). Builds on [`HuntMap.md`](HuntMap.md) and [`BossHuntsAndPerks.md`](BossHuntsAndPerks.md).

## Decisions (owner)

| Question | Decision |
|---|---|
| Quick Play | **Unchanged**: it stays the quick 10 waves + random boss ([`QuickPlay.md`](QuickPlay.md)) |
| Hunt length | The hunt gets a **length switch: 10 or 20 steps** (the lobby's Boss Hunt tab, host only; 10 stays the default) |
| Save | **A checkpoint at step 10** of a 20-step hunt: clearing it writes a resumable save (hunts aren't saved today) |
| Hunt map | **Always on** (owner, Nov 2026): the checkbox is gone; the plain ten waves are only reachable by tests (`Session.HuntMapMode = false`) |
| Strength | A long hunt's step counts half a wave for enemy health and damage (`Game.StrengthWave`), so 20 steps are as tough per fight as 10; `huntbal long` (4 maps, Good bots: 3/4 won, a Miniboss costs ~40% of max health) |
| Step 10 | The **faction's miniboss** (owner, Nov 2026): a fixed boss-like fight at step 10, so the halves read as two acts; the checkpoint is written when it falls. Which enemy it is per faction is open |
| Clues | Up to **5**, each a random health or damage clue (built, protocol 363) |

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

- Built: the Warden's miniboss is an existing enemy, the **Riot Shield** (owner: "the riot guard guy") as an elite with 4x health and 1.5x size (`HuntMinibossHp`, `HuntMinibossSize`); its escort is the node's wave (an Elite-style wave with the set piece). Other factions pick theirs when they get a hunt.
- Do the later bosses' hunts (no factions yet) come as 10-only until they have one?
