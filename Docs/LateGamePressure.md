# Late-game pressure: why wave 20+ turns into face-tanking

Owner's feedback (Nov 2026): "after a certain point it just becomes bullets on the screen and face tanking more than
anything. After about round 20ish it became too much to really react to." And: incendiary rounds and other effects
stacking on enemies, bosses especially, made a blinding white blob.

The white blob is fixed (protocol 192; see the bottom of this page). This page is about the first half: what the waves
do after about wave 15, and some options to choose from. **Nothing below is built yet.**

## What the numbers say

Every late-game knob turns up together, and most of them reach their cap at about the same point. So from wave 15-20
there's nothing new to read. There are just more bullets, faster:

| What | Formula (Normal) | Wave 10 | Wave 15 | Wave 20 | Wave 25 | Where |
|---|---|---|---|---|---|---|
| Enemy fire rate | 1 + 5% a wave, **cap 2x at wave 21** | 1.45x | 1.70x | 1.95x | 2.00x | `EnemyAI.cs` `WaveFireRate` |
| Bosses' fire | the same multiplier (bosses ignore the bullet budget) | | 1.70x | 1.95x | 2.00x | `TickEnemies` |
| Bullet budget (duo) | 40 + 3 a wave, cap 85, +20% a player | 84 | 102 (cap) | 102 | 102 | `ShotBudget` |
| Enemy bullets on screen (duo, measured) | `run.sh 1 1 bulletcount` | ~25 avg | ~68 avg (w18) | ~56-81 | ~85 avg, p90 104 | |
| Shooters at once (duo) | 4 + 1 every 6 waves, cap 8, +25% a player | 7 | 8 | 9 | 10 | `Readability.cs` `ShooterLimit` |
| Melee share of a wave | (13 − wave) / 12 | 25% | **0%** | 0% | 0% | `MeleeShare` |
| Elite chance | 4% a wave from 12, cap 35% | 0 | 16% | 35% (cap) | 35% | `Variety.cs` |
| Enemy health | 1 + 15% a wave | 2.35x | 3.1x | 3.85x | 4.6x | `SpawnHp` |
| Enemy damage | 1 + 3% a wave | 1.27x | 1.42x | 1.57x | 1.72x | `EnemyDamageScale` |

The key points:

1. **The screen is full from about wave 15.** The budget caps at wave 15 and fire rate keeps rising to wave 21, so
   shooters fill the budget again almost as soon as a bullet leaves it. The bullet count doesn't grow after 15, but
   the gaps between volleys close up. There's never a quiet moment to reposition.
2. **No melee after wave 13.** Melee rushers make you move in a different way (kite, dash, body-block). From wave 13
   every regular enemy is a shooter, so the only verb left is "weave through bullets". When that gets too dense, the
   best play is to stand and out-heal it.
3. **Bosses get 2x fire too.** Their basic fire is scaled by the wave fire rate and isn't held by the bullet budget.
   Their attacks were tuned at around 1x, so at wave 20-25 a boss's spray is twice as dense as it was designed to be.
   (Their timed attacks in `Special` aren't scaled. Only `FireT`-driven fire is.)
4. **Healing scales, dodging doesn't.** Lifesteal, regen, overheal and healers grow with the build. How dodgeable a
   screen is doesn't. Once the screen can't be dodged, face-tanking with sustain is the right answer, and the game
   ends up rewarding that.
5. **Player effects hid enemy bullets.** Enemy shots were drawn *under* effects, sparks and the additive glow.
   (Fixed with the white blob: they're now drawn on top.)

## Options (pick any; each one is small and has its own number to tune)

**A. Fire rate: lower cap, bosses left out.** *(smallest change, biggest effect)*
`WaveFireRate`: cap at 1.5x (reached at wave 11) instead of 2x at 21, and bosses don't use it (they already scale through
health, damage, stages and their late-boss health bonus). Then late waves are harder through *what* shoots, not how
often. Touches: `EnemyAI.cs` (2 lines), the `Shooters` and `FireRate` knob descriptions, the bosses test numbers.

**B. Pressure cycles (breathing room).**
The regular bullet budget pulses: about 10 s at full, then about 3 s at 40%. During the lull most shooters reposition and
hold fire (the turn system already knows how). You get a window to push, pick up orbs or revive, and the next push is
readable because it starts from a thin screen. A faint cue in the HUD or music on the swell would help. Touches:
`EnemyAI.cs` `RegularShotBudget`, a test in `budget`.

**C. Bring melee back late.** *(Superseded by [`EnemyRoles.md`](EnemyRoles.md): the owner sees it as enemy design, and that page plans the whole late roster by job.)*
Instead of melee fading to 0% at wave 13, keep a floor of about 20-25%, drawn from the *late* melee (Buzzsaw, Spike Block,
Lunger, Reaver, Juggernaut) and as squads (a shooter line with a rush squad in front). Moving to deal with a charge is
a different decision from weaving. Touches: `MeleeShare`, `BuildWave`.

**D. Fewer bullets that hit harder.**
Late budget cap 85 → about 65, with enemy damage growth 3% → about 3.5% a wave to make up for it. Each bullet matters
more, there's room to dodge between them, and tanking gets expensive. This is the classic bullet-hell trade: density
you can read, and a punishment for not reading it.

**E. A reason to dodge: graze.**
An enemy bullet passing within a few px of you without hitting gives a small reward (ultimate charge, or a short damage
buff). Weaving then has an upside, not just "not losing HP". A bigger feature: needs its own design doc and mockup.

**F. Visual priority (more of what was just done).**
Enemy bullets now draw on top. Next steps: a thin dark outline on enemy bullets so they read on any effect, and a
Settings slider "My effects: 100% / 60% / 30%" that dims your own and your allies' effects (not enemy shots or
telegraphs). Needs a mockup.

My recommendation: **A + B + C** first. They're all small, together they change how a late wave feels, and they're easy
to test with the bots (`bulletcount`, `latewaves`, `levels`). Then play a run to wave 25 and decide on D/E.

## The white blob (fixed, protocol 192)

What caused it, and what changed:

- **The hit flash ran on any drop in health.** Incendiary Rounds, the Flamethrower, bleeding and poison take health every
  tick, so a burning enemy flashed pure white and bulged *every frame* for as long as it burned. That happened on the
  host's Visual-effects flash (`Juice.cs`) and on clients (the snapshot flashed on any health drop). Now only a real hit
  flashes: `Juice.cs` watches the enemy's own `Flash` (set by `HurtEnemy`), and the snapshot sends a "just hit" flag
  (`GameNet.cs`, protocol 192). Damage-over-time still adds to the damage numbers.
- **Steady fire softens the flash.** `Enemy.FlashHeat` (every peer, drawing only) rises while hits keep coming. The
  flash mixes toward white by 100% for a fresh hit, down to 30% after about 0.4 s of steady fire, and never more than
  70% on a boss. The hit bulge restarts only once the last one has mostly settled, and it's a third as big on bosses.
- **Explosive / Collapse Rounds blew up on every hit.** Each hit sent a full blast (10-20 sparks plus a big additive
  glow) and a network message. On one spot, a blast is now shown at most once every 8 ticks (`SplashFxGap`), but the
  splash damage always lands. In the test, a Burst SMG's 45 hits show 15 blasts.
- **Impact sparks are thinned where hits pile up.** Each hit used to throw 5 additive sparks. Where many land in the same
  64 px cell, they drop to 2, 1, then 0 (`Juice.cs` `ImpactCrowd`).
- **Enemy bullets draw on top** of effects, sparks and glow (`WorldRenderer.cs` `DrawEnemyShots`). They still draw under
  Fogbound's mist and the mood tint.

Test: `Tools/SimTest/run.sh 1 1 hitfx`.
