# Training: drills that teach one mechanic each (design)

Status: **steps 0-6 built: the mode, its page and all six basic drills, played as a plain recruit** (`Core/Training.cs`, `Core/DrillGame.cs`,
`Game/TrainingUi.cs`, `Game/DrillUi.cs`, suite `training`; protocol 485). Next: tune the medal times from play; then, if wanted, more groups (Your Kit, The Run).

## The owner's decisions

- Training is a lobby mode on the Game Mode carousel (after Survival), with its own page, not the shared mode page.
- The page: compact drill cards in a row under each group's heading, then a big scope (a schematic of the drill) and the
  brief (goal, medal times, your best, keys, START DRILL).
- A drill starts from **START DRILL** (the host), not the lobby's START, which stays locked in Training. START DRILL takes the
  Game Mode screen's top right in place of TO THE LOBBY, since the lobby has no use in Training.
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
4. Shoot: break twelve targets (done). One at a time, each your own target dummy (140 health; only you hit or see it), at
   twelve fixed spots. The first three stand still; the rest slide back and forth 220 px either side of their spot, from
   90 px/s up by 30 each (`Training.ShootAt`, moved by the host; it reaches clients as an enemy, so nothing new on the wire).
5. Parry: block ten shots (done). One at a time, each from a new side of a ring of eight round you (430 px out), homing
   on you at 280 px/s, +15 a shot, so you can't step out of it. It's only yours (drawn for you alone, never a real shot):
   on reaching you the host checks your shield is up and facing it (`Facing`); else it's a hit (red flash, HIT xN) and
   that shot comes again. A ring of red dots round you shows the side it comes from, brighter as it nears.
6. Read the Tells: slip eight wind-ups (done). One at a time, each aimed where you stand as it shows: a Sniper's thin line
   (from 420 px away, through you and on across the arena), a Lunger's wide lane (to 300 px past you) or a slam's circle
   (105 px round you). Wind-ups shorten from 1.15 s by 0.06 a tell. When it ends it fires (a bright flash of its shape):
   still in it is a hit and it comes again (`Training.TellCatches`), out of it and you've slipped it.

## The recruit

Everyone in a drill plays a plain **recruit**, whatever their loadout (the owner's call): `Game.MakeRecruit`, done on every
machine as the drill's Start is read, so nothing goes over the wire. The DPS build with no class or role look
(`RecruitLook.cs`: grey fatigues, a helmet with a Training-cyan visor, a neutral footprint ring instead of a role colour),
no abilities, ultimate or perks, and the **Training Pistol** (`WeaponId.TrainingPistol`, `Training.Pistol`: one plain
round, 30 damage, 0.24 s between shots), a weapon in no lobby list (`Defs.WeaponOf` finds it in Training.cs).

## Open questions

- Medal times are first guesses (Move and Aim: gold 0:15, silver 0:25; Dodge: gold 0:20, silver 0:30; Wall Kick: gold 0:15, silver 0:25; Shoot: gold 0:25, silver 0:40; Parry: gold 0:25, silver 0:35).
