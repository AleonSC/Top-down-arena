# Quick Play (and the first-run tutorial)

Status: **steps 0-1 built** (the boss schedule; the mode, waves, bosses, records and endless, protocol 296); steps 2-4 planned (owner's decisions, Nov 12, 2026). Build one step at a time on "start on step N".

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
| Boss mechanics | **The same in every mode**: no per-mode or per-wave attack tuning; only health and damage follow the wave (as now). Any boss can roll after wave 10 |
| Boss HP | **About a 2-minute fight at any wave**: one formula, health = what a typical build at that wave deals in ~2 min (the typical build the `bosspower` sim already scores). The same rule after wave 10, 20, 30..., so endless keeps climbing as builds grow; the strong-build boost still applies on top. Nothing fixed to wave 10, nothing tuned per boss |
| Tutorial | Guided steps on the profile's first Quick Play, skippable, never again after (a profile flag) |

## Tutorial lessons

Each step is a CRT prompt (DevKit widgets) that ticks off when done; the wave timer waits for the current wave's steps.

1. **Wave 1**: move, aim, shoot, dash.
2. **Wave 2**: use your ability; parry a shot.
3. **Between waves 1-3**: pick a level-up card; collect gold; visit the vendor.
4. **After wave 2**: open Build, draw a room, vote it in.

Per seat and local only (each player's own profile decides): no network message. Skip with a key on the prompt.

## Steps

0. **Boss schedule** (built, Nov 12): `Core/BossSchedule.cs`. One object per mode answers which waves have a boss and
   which boss (`Game.Bosses`): the Campaign's rotation and a hunt's last-wave boss today, Quick Play's random bag in step 1.
   Every caller asks it instead of the every-fifth-wave formula; no gameplay, save or protocol change (sim suites identical).
1. **Mode and waves** (built, Nov 12; protocol 296): `Session.QuickMode`, `Game.Quick` (Core), the carousel's first mode and default; a boss slot after every
   10th wave (as the Campaign's shown waves); the boss bag (Warden first); faction waves with the mixed fallback; boss HP for a ~2 min fight at any wave (sim prints each boss's fight length after waves 10, 20, 30); win screen End / Continue endless; records;
   sim suite `quick`. Protocol bump (start message, `Msg` for the endless choice).
2. **Building**: Quick Play on the Expanding arena; check vector income fits a 10-wave run (first build round after wave 1).
3. **Tutorial**: `Core/Tutorial.cs` (step machine, what counts as done), `Game/TutorialUi.cs` (prompts), profile flag,
   skip; `Tools/Preview` shots of each prompt.
4. **Polish**: README section, lobby dossier for Quick Play (the boss bag shown as "???" until met), preview shots.

## Built in step 1

- `Core/QuickPlay.cs`: the boss order (bags of ten, the Warden first while the host's profile has no Quick Play), `QuickBosses`,
  `QuickBossHp` = the boss's Campaign health at its own wave x (a typical build's power now / then) x 120/90 s. Its health after
  waves 10/20/30 against its home health: `run.sh 1 1 quick` prints the table (the Warden x2.1 after wave 10, the Eclipse x0.08).
- The lobby carousel's first mode; `Profile.LastMode` reopens the host's last pick. No paths, Data Gates, Contraband's
  after-the-boss chest moves to wave 11, 21, ...; no lives or saves. The win is recorded at the first boss; falling in endless
  records the stretch since (`RunSaves.QuickRecord`).
- Fight lengths are the formula's (it assumes a boss's Campaign fight lasts ~90 s); the bots haven't measured them yet.

## Open

- Late bosses after wave 10 stay hard by design: same attacks, health set for a ~2 min fight at whatever wave they come. The tutorial run is always the Warden.
- Factions for the other nine bosses (`Docs/Approaches.md`) would make every ten feel like its boss.
