# Campaign and Boss Hunt: run length, cycles and modes (design)

Status: **step 1 built** (Nov 2026: Expanding runs save and continue; Rooms runs already did); steps 2-6 not built. The owner's decisions are in bold. The numbers are first guesses.
Related: [`Checkpoints.md`](Checkpoints.md) (lives), [`Campaign.md`](Campaign.md) (themed build-up, steps 1-2 built),
[`BossHuntsAndPerks.md`](BossHuntsAndPerks.md), [`LateGamePressure.md`](LateGamePressure.md).

## The two modes (owner)

| Mode | Shape | Gives you |
|---|---|---|
| **Campaign** (the main mode, today's Gauntlet) | **100 waves**, the same 10 bosses, one every 10 waves; saves + lives | a boss beaten unlocks its **Hunt**; rank XP and records **even when the run is lost** |
| **Boss Hunt** | **10 waves, one boss, a single arena**, a fast rush (as now) | a won Hunt unlocks that boss's **perks** |
| Practice | the sandbox (unchanged) | nothing |

**The loop:** Campaign → beat a boss → its Hunt opens → win the Hunt → its perk → take the perk into the Campaign →
reach the next boss.

**Fewer choices, not more:** no 50/100 toggle (Hunts are the short mode). The arena styles (Classic, Rooms, Expanding)
stop being lobby choices: they become what the Campaign's cycles look like (below). The lobby's mode carousel shows
Campaign, Boss Hunt and Practice.

## The owner's problem with today's run

The build-up to a boss is boring: waves 1-4 are weaker versions of the boss's enemies, and they don't teach what the
boss will ask of you. Making waves harder only by bigger numbers doesn't prepare the player.

## A cycle teaches its boss

Each 10-wave cycle is a lesson for its boss: every wave before it teaches one thing the boss will test. Difficulty
ramps through what you have to know, not only through health and damage.

| Wave of the cycle | Job | Warden cycle (worked example) |
|---|---|---|
| 1-2 | **Introduce**: one mechanic at a time, on its own | Riot Shields only: learn to flank |
| 3-4 | **Second mechanic**, then the two mixed | Gas Grenadiers (zones you must leave); then shields that push you into the gas |
| 5 | **Mid-boss**: an elite with one of the boss's real moves | a Guard captain with the Warden's cell slam (open: see below) |
| 6-7 | **Third mechanic** plus pressure | Taser Guards (stun lines) mixed with what came before |
| 8 | **Remix**: everything, in tighter squads | Breakout and Cell-block squads |
| 9 | **Exam**: the hardest wave before the boss | the Riot line + gas + tasers |
| 10 | **Boss**: each move should feel familiar | The Warden |

- **The first cycle also teaches the basics**: dash on wave 1, reading shots on wave 2, parry on wave 3, and so on.
- After the boss, its enemies phase out slowly while the next faction phases in (`Campaign.md`, built).
- **What each boss needs**: about three enemy types whose mechanics preview its attacks, plus a mid-boss. The Warden has
  most of this; the other nine bosses need factions anyway for their Hunts, so it's the same work.
- A Boss Hunt is the **hard version of the same cycle** (more types, tougher squads), as `Campaign.md` plans.

## Arena style per cycle

Each cycle has a fixed arena style that fits its boss, so a Campaign shows every style without the player picking one:
e.g. the Warden's cycle in **Rooms** (cell blocks), a later one in **Expanding**, others in **Classic** with its layouts.
Which style goes with which boss is open. The switches stay in the Difficulty tab's Custom, for players who want one
style the whole run. **Boss Hunts stay a single arena (Classic).**

Saving works in every style (step 1, built).

## Balance for 100 waves

Today's numbers grow per wave and were tuned for 50. Stretched to 100 unchanged, enemy health would reach about 16x and
damage about 4x.

- **Scale by cycle, not by wave.** Every per-wave formula reads one *effective wave* = wave ÷ 2 (so boss N is as hard
  as today's boss N): `SpawnHp`, `EnemyDamageScale`, `WaveFireRate`, `ShooterLimit`, `ShotBudget`, the elite chance,
  `MeleeShare`, the late-boss health bonus, enemy unlocks. One function, many call sites.
- **Breathing within a cycle**: pressure ramps from wave 1 to 9 of a cycle and drops back after the boss. That helps
  the late-game face-tanking too (`LateGamePressure.md`).
- **Same power at each boss**: XP per wave halved (the same level, so the same upgrade picks, at each boss); gold and
  chests about the same per cycle, spread out. The strong-build boss boost already corrects what's left.
- **Bot check**: a test plays the Campaign with bots and compares, at each boss, party power, level, gold, boss
  time-to-kill and damage taken against today's 50-wave numbers (within about 10%).

## What you keep when you lose

- Already true: a boss beaten counts at once (its Hunt opens, rank XP, records), win or lose.
- Lives (`Checkpoints.md`): a wipe after a boss can retry from the wave after it, by difficulty 3/2/1/0.
- Open: a reward for the furthest boss reached.

## Steps (proposed)

1. Saving in Rooms and Expanding runs. **Built**: Rooms runs already saved; Expanding saves its map, kits, seed, vectors
   and shop shelves (`saves` suite).
2. Effective-wave scaling + 100 waves, a boss every 10; the bot balance check.
3. The Warden's teaching cycle (waves 1-9 as above) and its mid-boss.
4. Arena style per cycle; lobby shows Campaign / Boss Hunt / Practice; switches moved to Custom.
5. Checkpoints and lives (`Checkpoints.md`).
6. A teaching cycle for each later boss, together with its Hunt.

## Open

- The wave-5 mid-boss: an elite with one boss move, a small copy of the boss, or a scripted squad event?
- Which arena style goes with which boss?
- Rename Gauntlet to Campaign in the lobby and the README?
- A reward for the furthest boss reached on a lost run?
