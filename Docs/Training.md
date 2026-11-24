# Training: drills that teach one mechanic each (design)

Status: **step 0 built: the mode and its page** (`Core/Training.cs`, `Game/TrainingUi.cs`, suite `training`; protocol 483).
No drill is playable yet (`TrainingDrill.Built` is false for all six). Next: step 1, the Move and Aim drill.

## The owner's decisions

- Training is a lobby mode on the Game Mode carousel (after Survival), with its own page, not the shared mode page.
- The page: compact drill cards in a row under each group's heading, then a big scope (a schematic of the drill) and the
  brief (goal, medal times, your best, keys, START DRILL).
- A drill starts from **START DRILL** on the page (the host), not the lobby's START, which stays locked in Training.
- The basics only for now: **Movement** (Move and Aim, Dodge, Wall Kick) and **Combat** (Shoot, Parry, Read the Tells).
- Drills are built one at a time.

## Medals

Each drill has a gold and a silver time (`TrainingDrill.Gold`/`Silver`); finishing it at all is bronze. The profile keeps
each drill's best time (`Profile.DrillBest`, `drill <key> <seconds>` lines), and `Training.Medal` turns it into the medal.
Times are first guesses until each drill is played.

## Steps

0. The mode and its page (done).
1. Move and Aim: touch five beacons.
2. Dodge: dash through six bullet walls.
3. Wall Kick: kick off three walls, reach the flag.
4. Shoot: break twelve targets.
5. Parry: block ten shots.
6. Read the Tells: slip eight wind-ups.

## Open questions

- In a party, does everyone run the drill in their own lane (the page says so), or does the host run it alone?
- Where a drill is played: a small room of its own, or the Practice room's arena.
