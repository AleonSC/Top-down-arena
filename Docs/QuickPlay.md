# Quick Play (and the first-run tutorial)

Status: **planned, nothing built** (owner's decisions, Nov 12, 2026). Build one step at a time on "start on step N".

## What it is

A short run that's the game's front door: **ten waves, then a random boss** (its own fight after wave 10, as the Campaign's
boss slot: it doesn't use up a wave number), then the party may go **endless** (a random boss after every tenth wave). It has the Expanding arena's **room building**, and a profile's first Quick Play is
the **guided tutorial**.

## Decisions

| Question | Decision |
|---|---|
| Where | A new **first mode** on the lobby carousel (Quick Play, Campaign, Boss Hunt, Practice); new profiles land on it, others keep their last mode |
| Length | Waves 1-10, then the boss after wave 10 (the Campaign's `BossSlot` layout, so the HUD and wave box read the same). Win screen: **End run** or **Continue endless** (host picks) |
| Endless | A boss after every **10th** wave (after 20, 30, ...), each a random boss, scaling with the wave as a run does |
| Boss pick | A profile's first Quick Play: always the **Warden**. After that random from all 10, no repeat until every boss has come up (a shuffle bag, seeded by the run so every machine agrees) |
| Waves between bosses | The **coming boss's faction** (as Boss Hunt: `Hunts.Step`, set piece on the 5th and 10th wave of each ten). Bosses without a faction yet (all but the Warden) fall back to the normal run's mixed waves |
| Building | The Expanding arena's **vector room drawing**, as in the Campaign (build round after each cleared wave) |
| Rewards | Full XP, rank and boss-beaten unlocks; its own records (plays, best wave, bosses beaten); **not saved** to continue |
| Tutorial | Guided steps on the profile's first Quick Play, skippable, never again after (a profile flag) |

## Tutorial lessons

Each step is a CRT prompt (DevKit widgets) that ticks off when done; the wave timer waits for the current wave's steps.

1. **Wave 1**: move, aim, shoot, dash.
2. **Wave 2**: use your ability; parry a shot.
3. **Between waves 1-3**: pick a level-up card; collect gold; visit the vendor.
4. **After wave 2**: open Build, draw a room, vote it in.

Per seat and local only (each player's own profile decides): no network message. Skip with a key on the prompt.

## Steps

1. **Mode and waves**: `Session.QuickMode`, `Game.Quick` (Core), the carousel's first mode and default; a boss slot after every
   10th wave (as the Campaign's shown waves); the boss bag (Warden first); faction waves with the mixed fallback; win screen End / Continue endless; records;
   sim suite `quick`. Protocol bump (start message, `Msg` for the endless choice).
2. **Building**: Quick Play on the Expanding arena; check vector income fits a 10-wave run (first build round after wave 1).
3. **Tutorial**: `Core/Tutorial.cs` (step machine, what counts as done), `Game/TutorialUi.cs` (prompts), profile flag,
   skip; `Tools/Preview` shots of each prompt.
4. **Polish**: README section, lobby dossier for Quick Play (the boss bag shown as "???" until met), preview shots.

## Open

- A late boss (home wave 45) met at wave 10 strength: met after wave 10, its health/damage scale by wave, but its mechanics may be too much
  for a first-time party. Watch the first reports; a cap on which bosses can roll at wave 10 is the fallback.
- Factions for the other nine bosses (`Docs/Approaches.md`) would make every ten feel like its boss.
