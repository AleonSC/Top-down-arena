# Checkpoints and lives (design)

Status: **planned, nothing built** (Nov 2026). The owner's decisions are in bold. The numbers are first guesses. It
builds on saved runs (`Core/RunSaves.cs`, `Core/SaveData.cs`) and the Difficulty tab (`Core/Difficulty.cs`).

## The idea (owner)

Long runs (possibly 100 waves, see the last section) shouldn't be lost all at once. A wipe late in a run sends you
back to a checkpoint instead of ending the run, a limited number of times.

**Picked: automatic checkpoints + lives** (over a Backup Disk item dropped by bosses, or a Checkpoint perk).

## The rules

| Rule | Decision |
|---|---|
| Checkpoint | **every boss kill** sets one: the start of the wave after the boss (all picks made, path chosen, shop closed) |
| Lives per run | **by difficulty**: Easy 3, Normal 2, Hard 1, Nightmare 0; Custom gets a *Lives* knob (0-5) |
| Using a life | everyone wipes → the results screen offers **RETRY FROM WAVE n (x lives left)** next to the usual buttons |
| What comes back | the run exactly as it was at the checkpoint: every seat's upgrades, levels, mastery, gold, perks, path, modifiers |
| Cost | **score −25% per life used** (multiplied, so two lives = 0.75 × 0.75), and the run loses **Deathless** |
| Out of lives | a normal game over: the save is deleted as today |
| Deathless | a run won without using a life gets a Deathless mark in Records > Recent runs and on the results screen |
| Before the first boss | no checkpoint yet: a wipe on waves 1-4 is a normal game over (nothing much lost) |

## How it works

Today the host saves the run (`RunSave`) as each wave starts and deletes it in `RunOver` on a win or loss.

1. **Checkpoint copy.** When a boss falls, the save written at the next wave's start is also copied to a second
   file, `<RunId>.checkpoint`, next to the run's save. It's the same `RunSave` text. Only the newest checkpoint is
   kept.
2. **Lives in the save.** `RunSave` gets `LivesLeft` and `LivesUsed` (the score cut and Deathless both come from
   `LivesUsed`). New runs start with the difficulty's number.
3. **On a wipe.** If `LivesLeft > 0` and a checkpoint exists, `RunOver(false)` doesn't delete the save or the
   checkpoint and doesn't record the End yet. It marks the run *awaiting retry*. The results screen shows the retry key
   (host only; clients see "Host can retry from wave n").
4. **Retry.** The host loads the checkpoint as if continuing a saved run (`ApplyResume`) with `LivesLeft − 1` and
   `LivesUsed + 1`, and plays the **Play again** drop-in (no lobby). Seats stay the same players.
5. **Giving up.** Leaving the results screen (To lobby or Quit) without retrying is the real game over: End record,
   save and checkpoint deleted.
6. **Continue later.** If the game is closed on the results screen, the save stays and the picker shows the run as
   **Wiped: retry from wave n**. Continue goes straight to the retry.

## Where it shows

- **HUD:** a small lives chip by the wave panel (hearts or disk icons, the number left). It's hidden at 0 lives on
  Nightmare.
- **Wave cleared box after a boss:** a "CHECKPOINT SAVED" line, gold.
- **Results screen:** the retry key (a `DkKey`) with the lives left, and the score shown with the cut.
- **Saved-runs picker:** the lives left on each row.
- **Difficulty tab:** a *Lives* row in the table and a Custom slider.

## Network and tests

- `LivesLeft` goes in the snapshot (the HUD chip), and the retry is a host action → **protocol bump** when built.
- Test suite `checkpoints`: wipe after a boss → retry restores upgrades and wave; lives count down; out of lives deletes
  both files; score cut applied; Deathless only when `LivesUsed == 0`; no checkpoint before the first boss; Hunts,
  Practice, Boss Lab and the Expanding arena are never checkpointed (they aren't saved today).

## Open

- Does a retry re-roll the waves (a new seed) or replay the same ones? Proposed: **a new seed**, so it isn't memorised.
- A co-op player who left before the wipe: does their seat come back empty (a bot) as with Continue today?

## The 100-wave run (proposed, not decided)

The owner asked about a 100-wave run so each boss gets a proper buildup. Proposed shape, still open:

- **The same 10 bosses, one every 10 waves**: 9 Campaign buildup waves and the boss on wave 10, the same shape as a Boss
  Hunt, so a hunt is a sample of a real run.
- **Scaling by boss cycle, not by wave**: today's per-wave growth (health +15%, damage +3%, fire rate, elites) halved,
  so wave 100 is about as hard as wave 50 is now. Otherwise health would reach about 16x and damage about 4x.
- **A mid-cycle event on wave 5** (an elite squad or mini-boss) so the buildup doesn't drag.
- **A slower XP curve**, so builds peak at the final boss and not at wave 60.
- Checkpoints matter more in a run this long; with a boss every 10 waves, a checkpoint costs at most 10 waves.
