# Boss nodes: the Campaign builds toward its bosses (design)

Status: **design only** (Nov 12, 2026). Nothing built. The owner's decisions are in bold; everything else is a first
guess to confirm before step 1. Builds on [`Nodes.md`](Nodes.md) (map nodes), [`RunLength.md`](RunLength.md) (the
100-wave Campaign) and [`Campaign.md`](Campaign.md) (the themed build-up).

## The idea (owner)

- The Campaign is about **building rooms toward boss nodes**, not reaching wave 10, 20, 30...
- **Waves keep going** until every boss is beaten. A run is as long as it takes.

## Decisions

| Question | Decision |
|---|---|
| Boss nodes on the map | **All bosses are visible from the start**, beaten in any order |
| Stalling | **Nothing forces the fight**: enemies get harder by wave number (as now), so slow teams face harder waves |
| Records | **Most bosses beaten first, then fewest waves** |

## Proposal (first guesses)

- **Placement**: 10 boss nodes in a ring far from the start (e.g. 30-60 tiles, nearer ones for the early bosses in
  `Game.BossCycle`), seeded like `NodeFor`, never moved. A boss node can be reached only by a room that encloses it
  (`Nodes.md`'s rules), and that room is a big boss arena (e.g. 12 x 12 tiles).
- **Starting the fight**: claiming a boss node arms it; the next wave is that boss's wave (the arena pulls the team in,
  as boss waves already do with `PullIntoHub`). Beaten: the node is spent and its Hunt unlocks, as now.
- **Schedule**: a new `NodeBosses : BossSchedule` (`BossSchedule.cs`): `IsBossWave` = a boss node is armed,
  `BossAt` = that node's boss. The rest of the game reads the schedule, so most of it needs no change.
- **Themed enemies**: the faction mix follows the **nearest unbeaten boss node** to the built map (by walk distance),
  so a team building toward the Queen sees Queen enemies grow. Beaten factions fade and come back as now
  (`Approaches.Comeback`).
- **Difficulty**: unchanged, by wave number. The boss's health scales with the wave it is fought on (like
  `QuickBossHp`), so a late fight is fair.
- **End**: the run is won when the last boss falls. No 100-wave cap; checkpoints and lives stay.
- **Bots**: build toward the nearest boss node when no chest or shop is closer (`BotBuildsToFloater`).
- **Look**: a boss node is a large skull ring with the boss's colour and name, on the floor, minimap and draw screen.

## Open questions

- Should the Warden still come first on a first play (the teaching cycle)? Proposal: his node is the nearest one.
- Purse and vectors: reaching a node 50 tiles out costs a lot of vectors. Tune with `vecbal`.
- Late arenas: a map that reaches all 10 nodes is very big. `ExpansionScale` may need a cap.

## Plan (one step at a time)

1. Boss nodes: data, placement, save, sync (`Msg.ExpandState`, **protocol bump**), look on the map. Tests in `expand`.
2. `NodeBosses` schedule: claiming arms the boss, the boss wave, spent node, run won on the last boss.
3. Faction mix from the nearest node; boss health by wave; records by bosses then waves.
4. Bots, `vecbal` balance pass, README.
