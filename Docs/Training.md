# Training: drills that teach one mechanic each (design)

Status: **steps 0-3 built: the mode, its page, Move and Aim, Dodge and Wall Kick** (`Core/Training.cs`, `Core/DrillGame.cs`,
`Game/TrainingUi.cs`, `Game/DrillUi.cs`, suite `training`; protocol 485). Next: step 4, the Shoot drill.

## The owner's decisions

- Training is a lobby mode on the Game Mode carousel (after Survival), with its own page, not the shared mode page.
- The page: compact drill cards in a row under each group's heading, then a big scope (a schematic of the drill) and the
  brief (goal, medal times, your best, keys, START DRILL).
- A drill starts from **START DRILL** on the page (the host), not the lobby's START, which stays locked in Training.
- The basics only for now: **Movement** (Move and Aim, Dodge, Wall Kick) and **Combat** (Shoot, Parry, Read the Tells).
- Drills are built one at a time.
- In a party, everyone runs the drill at once in the same arena, each with their own beacons, clock and medal.
- A drill runs full screen on the classic arena, like a run: a drill card (top left), a result card, RETRY and BACK.
- Move and Aim teaches aiming too: the last two beacons are shot open instead of walked onto.

## How a drill runs

The host's START DRILL (`Session.StartDrill`) sends `Msg.Start` with the drill's index; every machine runs the real match on
the classic arena with no waves (`Game.TickDrill`, held in a fight). Each player has a `DrillRunner` (step, clock, done, their
target's id); the host decides when a step is reached and the snapshot carries the runners (`SyncDrill`). A shoot step's
target is a target dummy only its player can hurt (`DrillShieldsHit` in `HurtEnemy`) and only its player sees
(`DrillTargetOf`). Nothing hurts anyone, nothing levels up, nothing is recorded. Each machine keeps its own player's best time
when they finish (a client's clocks and walls run on between snapshots: `UpdateDrillView`). On the result card the host has RETRY (R: the drill again for everyone) and BACK (B or Enter: the lobby,
still on the Training page).

## Medals

Each drill has a gold and a silver time (`TrainingDrill.Gold`/`Silver`); finishing it at all is bronze. The profile keeps
each drill's best time (`Profile.DrillBest`, `drill <key> <seconds>` lines), and `Training.Medal` turns it into the medal.
Times are first guesses until each drill is played.

## Steps

0. The mode and its page (done).
1. Move and Aim: walk onto three beacons, shoot the targets on two more (done).
2. Dodge: dodge through six walls of shots (done). Each wall is a column of shots sweeping the whole arena from one side,
   alternate sides, a little faster each time (250 px/s, +30 a wall), 0.8 s apart. It's only yours (drawn for you alone, never
   a real shot). The host tells when it crosses you: a dodge within its invulnerability plus 0.25 s of slack (for the dodge's
   trip to the host) takes you through; anything else is a hit (a red flash, HIT xN) and that wall starts again.
3. Wall Kick: kick off three walls, reach the flag (done). The drill's floor plan is three panels in the open arena
   (`Training.KickLayout`, through `ActiveLayout`, so walking, shots and the wall kick treat them as walls). The lit panel
   glows with KICK; your own game sees a kick (Dodge.cs `WallKick`) and tells the host (`Msg.DrillKick`), which counts it if
   you're within 90 px of the lit panel. Then the flag lights: stand on it (the copy nudges you to sprint there).
4. Shoot: break twelve targets.
5. Parry: block ten shots.
6. Read the Tells: slip eight wind-ups.

## Open questions

- Medal times are first guesses (Move and Aim: gold 0:15, silver 0:25; Dodge: gold 0:20, silver 0:30; Wall Kick: gold 0:15, silver 0:25).
