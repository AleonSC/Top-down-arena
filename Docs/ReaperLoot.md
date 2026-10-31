# The Twin Reapers' legendary cards

Status: **built** (Oct 2026, protocol 104): Twin Scythes, Final Reap, Scythe Throw. Their old three (Executioner,
Twin Fang, Soul Harvest) stay in the game but are never offered (`UpgradeDef.Unoffered`), like the Queen's.

## Why

Same idea as the Warden's and the Queen's (`Docs/QueenLoot.md`): each card hands you one of **their** fight's
tricks. The old three were plain stats (double damage below 50%, 40% double-damage chance, heal on kill). The owner
picked these from two rounds of proposals; Void Pull (a spot that drags enemies in, then blows) was kept for the
Singularity's rework.

| Old card | What it did | One pick (boss / pack / spread) |
|---|---|---|
| Executioner | Double damage to enemies below 50% health | +23% / +23% / +10% |
| Twin Fang | 40% chance for each hit to deal double damage | +41% / +38% / +37% |
| Soul Harvest | Every kill heals you 5 | healing only |

## The new cards

**Twin Scythes** (companion; their scythes and the Sundered reaps). Two scythes circle you in opposite directions,
Dusk (pink) 60 px out and Dawn (blue) 100 px out, cutting what they pass (16, once per enemy per 0.5 s) and slicing
small enemy bullets. Every 1.6 s each reaps out at the nearest enemy within 240 px and flies back. 2nd stack: every
1.1 s and spinning 40% faster; 3rd: a third scythe (gold, 80 px).

**Final Reap** (passive; the reaps). When your hit leaves a regular enemy below 25% health, a phantom scythe reaps
it and heals you 3. Your hits on a boss below 40% health deal double. Stacks: reap below 30%, then 35%; bosses x2.5,
then x3. (Executioner's job and Soul Harvest's healing in one card.)

**Scythe Throw** (new Q / E / Shift ability, 9 s). A giant scythe flies to your cursor (up to 380 px) and back,
cutting everything it passes each way (45) and slicing small bullets; catch it to take 40% off the cooldown
(mastered: 60%, and it cuts 50% harder). The catch reaches the player's own game by `Msg.CooldownCut` (each game
runs its own cooldowns).

## Numbers (one pick, median over the 54 weapons, `run.sh 1 1 balance`)

| Card | Boss | Pack | Spread | 3 stacks (boss / pack) |
|---|---|---|---|---|
| Twin Scythes | +45% | +33% | +39% | +88% / +56% |
| Final Reap | +17% | +26% | +11% | +24% / +42% |

Changes while measuring: the scythes only circling read 0 (enemies stand off at range), so they now reap out at
enemies (their reaps); cut 18 -> 16 after that put the boss at +53%. Final Reap was +9-14% at 15% / +50% below 30%,
so it reaps below 25% and doubles damage on a boss below 40%. Final Reap's healing isn't counted. Scythe Throw is an
ability: 90 to everything on its line each throw, every ~5.4 s if you catch it.
