# Quick Play (and the first-run tutorial)

Status: **all steps built** (Nov 12, 2026): the boss schedule; the mode, waves, bosses, records and endless (protocol 297); room building; the tutorial; the lobby tab (owner's decisions, Nov 12, 2026). Build one step at a time on "start on step N".

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

## Tutorial lessons (built, step 3)

A CRT card at the top left (`Game/TutorialUi.cs`, DevKit widgets) shows the first lesson with a step left; a lamp per step
lights as you do it, with your own key beside it, and **SKIP**. Steps tick in any order (`Core/Tutorial.cs`):

1. **The basics** (wave 1): move (0.8 s), aim (the cursor travels 400 px), shoot (0.5 s held), dodge.
2. **Level up**: pick an upgrade card.
3. **Build a room** (the first build round, right after the first card): open Build rooms, draw a room (a proposal of
   yours), vote it in (a vote, or a room built after).
4. **Your kit** (the next wave): use an ability; raise the parry shield (0.25 s).

The prompt shows the first unfinished lesson that can be done **now** (`Tutorial.CurrentFor`): building only while a build round
is open, the kit only in a fight; otherwise the first unfinished one. (Fix, Nov 12: the build round opened after the first card
while the prompt still asked for the kit.)
5. **Gold and the shop**: collect gold; walk into a shop or vendor room. Moved last (the plan had gold and the vendor between
   waves 1-3): in the Expanding arena gold only drops and the shop only opens once a built room connects one.

Changed from the plan: it **never holds the wave timer** (between waves the ready-up already waits for you, and a per-seat hold
would need the network); local only, no protocol change. It ends for good when done, skipped, or once the first Quick Play is
recorded (`Profile.TutorialDone`). Not for bots, replays, the Boss Lab or the lobby preview.

## Steps

0. **Boss schedule** (built, Nov 12): `Core/BossSchedule.cs`. One object per mode answers which waves have a boss and
   which boss (`Game.Bosses`): the Campaign's rotation and a hunt's last-wave boss today, Quick Play's random bag in step 1.
   Every caller asks it instead of the every-fifth-wave formula; no gameplay, save or protocol change (sim suites identical).
1. **Mode and waves** (built, Nov 12; protocol 297): `Session.QuickMode`, `Game.Quick` (Core), the carousel's first mode and default; a boss slot after every
   10th wave (as the Campaign's shown waves); the boss bag (Warden first); faction waves with the mixed fallback; boss HP for a ~2 min fight at any wave (sim prints each boss's fight length after waves 10, 20, 30); win screen End / Continue endless; records;
   sim suite `quick`. Protocol bump (start message, `Msg` for the endless choice).
2. **Building** (built, Nov 12; no protocol change): Quick Play on the Expanding arena (`Game.ArenaKind`), the surprise room
   after each boss. Vectors at the full rate (+8 a wave, 1 per 8 kills, cap 70; the Campaign halves it for its 100 waves).
   Measured (`run.sh 1 1 quick`, two bots): a build round after every wave from wave 1, 15 rooms by the boss, each bot's
   vectors between 12 and 61 (never stuck at the cap), so no tuning.
3. **Tutorial** (built, Nov 12; no protocol change): `Core/Tutorial.cs` (the steps, what counts as done), `Game/TutorialUi.cs`
   (the prompt), `Profile.TutorialDone`, SKIP; preview `TUTORIAL=<lesson>,<steps done> shot.sh <name> hud`.
4. **Polish** (built, Nov 12; no protocol change): the lobby's **Quick Play** tab (`Game/QuickPlayUi.cs`, tab 8, opens when the
   host picks the mode): the run's ladder, its rules, your records, and the boss pool (a boss beaten anywhere shows its model and
   name, the others "???"; the Warden marked FIRST on a first play). Previews: `shot.sh <name> lobby`, `QUICKMET=4` for a played profile.

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
