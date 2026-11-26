# Handoff: Top-Down Arena

Everything a new session needs to pick up this project: what the game is, how the code is put together,
how to test and preview changes without Unity, and the ideas still open from the code audits. How the owner likes
to work is in `CLAUDE.md`; the per-file map is `CODEMAP.md`.

`README.md` is the player-facing and feature-level reference (every weapon, ability, enemy, boss, control
and tool is described there). This document is the developer-facing overview. `CLAUDE.md` is the short
version that loads on its own; read the sections here (and of README) that your task needs, not the whole file.

There is no to-do file; the owner sends requests per session. Design docs in `Docs/` start with their status.

---

## 1. The game in one page

(Rewritten Nov 2026. If something here disagrees with the code, the code wins: fix this page.)

- **What:** a 1-3 player **co-op top-down bullet hell** made in **Unity 6** (`6000.6.7f1`, Unity 6.6; built-in render
  pipeline). The owner plays it with a friend over **Tailscale** (one hosts, the other joins by IP).
- **Players** pick a **role** (Tank, Healer, DPS) × **class/kit** (Soldier, Elementalist, Summoner, Knight, Ninja, Engineer,
  Hemomancer; old saves may still say `Mage`/`BloodMage`, loaded by `KitFromSave`), then one of 3 weapons and one of 3 1st abilities. Each role × kit has an ultimate (F). Very rare
  cards add 2nd, 3rd or 4th abilities. Abilities level up with use (**mastery** 1-5, capstone at 5). **Perks** (unlocked by
  winning a Boss Hunt, e.g. Riot Gear) are equipped in the lobby.
- **The ten bosses, in Campaign order:** Warden, Hive Queen, Twin Reapers, Singularity, Colossus, Duelist, Lich,
  Storm Herald, Devourer, Eclipse (`Game.BossCycle`, EnemyAI.cs). **Every boss has its own faction** of themed enemies,
  squads and a finale set piece (`Approaches.cs`: Warden's prison, Hive, Dusk and Dawn cult, Infall, Quarry, Masked
  Court, the dead, Squall, Larder, Penumbral Court).
- **Lobby modes** (host picks on the Game Mode screen's carousel, `ModeScreenUi.cs`; a new profile starts on Quick Play):
  - **Quick Play** (`QuickPlay.cs`): 10 waves of one boss's faction, then that boss (the Warden on a profile's first
    play, then a shuffled bag). Boss health tuned to ~2 min. Then optional endless: 10 more waves + a new boss, repeat.
    Has the tutorial. No paths, Data Gates, lives or saves.
  - **Campaign** (the main mode): **100 waves, a boss after every 10th** (Warden after 10, Hive Queen after 20, ...
    Eclipse after 100), then optional endless. Saved, with **lives** (Difficulty: Easy 3, Normal 2, Hard 1).
  - **Boss Hunt** (`Hunts.cs`): 10 waves of one boss's faction, the boss on wave 10. Opens once that boss is beaten
    anywhere; winning unlocks its perk. **Only the Warden has a hunt so far.** Optional **Hunt map** (node choices).
  - **Survival** (`Survival.cs`): no waves, one clock, factions join every 4 min, one big walled room, ends when all
    are down.
- **Track waves vs shown waves (important):** the Campaign is the old 50-wave track with **each track wave fought
  twice** ("legs", `Game.Leg`). All scaling code reads `Wave` (track, 1-50, a boss after every 5th:
  `CampaignBosses.Every = 5`); the player sees `ShownWave` (1-100). Wave numbers in README and older docs are usually
  **track** waves: double them for what the player sees. XP, gold, chests and vectors per wave are halved to match.
  Quick Play and Boss Hunts don't use legs.
- **How Campaign waves are filled — Campaign buildup** (default on, `Approaches.Campaign`): every wave is the coming
  boss's faction, growing through its stretch; the last boss's faction fades out over the next waves; older bosses
  stay as a small pool and return as **comeback squads**. Each boss's 10 Campaign waves are a **teaching cycle**
  (`FactionDef.Lessons`): one new enemy per wave, arriving alone and last, a recap at 5, all squads at 9, the finale
  set piece (e.g. the Warden's Riot line) at 10. The old **boss approach** (faction only in the 4 track waves before a
  boss) is what you get with Campaign buildup turned off.
- **Arena:** Campaign and Quick Play are played on the **Expanding arena**: it starts as a box and grows as the party
  builds rooms between waves with **vectors** (draw a room, vote it in; shops/Black Markets, a free surprise room after each
  boss). The **Simple Arena** modifier keeps the open arena. Boss Hunts use the open arena; Survival has its
  own 3200 x 1800 room.
- **Between waves (Campaign):** level-up cards (3 picks, rerolls, or gold instead), room building and gold shops,
  floating mini-boss rooms (a faction's mini-boss, `FactionMinis.cs`), **Data Gates** (an optional challenge once per
  5 track waves), and after each boss a legendary + a weapon mod. **Paths** (`Docs/Paths.md`, a pick of two after each
  boss) only run with Simple Arena: the Expanding arena has none.
- **Defence:** a **parry** shield (right mouse) that soaks damage and can reflect; Knights' swings cut small bullets.
  Boss bullets: violet (wear down blockers) and **red aimed shots** (unblockable except by the Knight's Shield Wall).
  Down at 0 HP; an ally revives you in 4 s; everyone down = a life is spent (respawn at the start of that wave) or
  game over.
- **Pacing:** a **bullet budget** caps regular enemies' bullets (40 + 3/track wave, max 85, +20% per extra player; 60%
  while a boss is up on Normal). Enemy damage grows 3% per track wave. Touching a boss hurts once a second and shoves
  you ~100 px.
- **Lobby settings:** Difficulty tab (Easy/Normal/Hard/Nightmare/Custom, lives, Campaign buildup), Modifiers tab
  (Glass Cannon, Tough Crowd, Frenzy, Swift Foes, Famine, Lucky, Simple Arena, ...).
- **Profile:** rank XP and Cores, records per mode, **Recent runs** with graphs, saved loadouts.
- **Dev tools (F1):** a dock over the live game (`DevDock.cs`: God / Full heal / Skip wave / Kill all, WAVE / PARTY /
  VIEW / LABS drawers, stat editor, run snapshots). Bots fight any boss from the command line (`run.sh 1 1 lab`, `Core/BossLab.cs`).

## 2. How the code is organised

```
Assets/Scripts/Core/   plain C# game logic (namespace Arena); no UnityEngine dependency at all
Assets/Scripts/Game/   the Unity front end: rendering (GL), IMGUI menus/HUD, sound and music synthesis
Assets/Editor/         ArenaEditor.cs: scene setup and the Windows build menu item
Tools/SimTest/         headless test suite (mono) that drives real Sessions over localhost TCP
Tools/Preview/         renders screens to PNG without Unity (stubs + Pillow)         [added in the audit]
Tools/CompileCheck/    fast compile check with Unity stubs                           [added in the audit]
Tools/CodeMap/         check.py: CODEMAP.md names every source file once, and no deleted one (CI) [added Nov 2026]
Tools/RenderCheck/     proves a refactor left the arena's drawing byte-identical      [added Oct 2026]
Tools/SoundCheck/      renders the synthesized sounds to WAV to listen without Unity  [added Nov 2026]
Tools/SoundForge/      browser synth that exports WAVs named after game sounds (replace any Sfx)
Docs/                  design docs, mockups, history
```

Per-file map: **`CODEMAP.md`** (one line per source file; add new files there). The older, more detailed per-file
notes (function names, message flow) are in `Docs/CodeNotes.md`, kept as a reference but no longer maintained.

### Key mechanisms worth knowing

- **Networking:** host-authoritative TCP. Clients send inputs; **clients are authoritative for their own
  movement** (`LocalControl`), the host for everything else. The host sends a full **snapshot every 3 ticks**
  (20 Hz) of players/enemies/minions/orbs; bullets are sent as spawn/remove events and simulated on both
  sides with periodic `BulletSync`. Coins go as changes (`SyncCoins`, Coins.cs: dropped/moved/taken since the last snapshot, a full list every 60): they
  pile up all wave, and sending every one 20 times a second swamped the link. Effects and sounds are broadcast (`Msg.Fx`, `Msg.Sound`).
  **`Session.ProtocolVersion` (see `Core/Session.cs`) must be bumped whenever any message format changes**; mismatched
  players are turned away with a clear message. New `Msg` values go at the end of the enum.
- **Rendering:** no sprites. The world is drawn with **GL immediate-mode triangles** in `OnPostRender`
  (arena 1600×900 scaled to the screen). The UI is **IMGUI** on a 1280×720 virtual canvas (`W`, `H`), scaled.
  Menus can draw game models on top of panels: `BeginUiGl()` / `UiModelAt(pos, zoom)` / `EndUiGl()` issue GL
  during IMGUI's Repaint (used by the lobby, the dev tools' enemy list and the pause menu).
- **Loadout preview:** `DrawShowcase` swaps the `Showcase` game into `ArenaApp.game` and calls `DrawArenaContents`
  (the part of `DrawWorld` between the floor and the overlays), clipped with `UiClip` (software clipping in `Tri`,
  so it works the same in Unity and the preview tool).
- **Colour space: Gamma.** Everything is drawn with raw vertex colours (`GL.Color`), which Unity doesn't convert in a
  Linear project, so there every colour and see-through layer comes out lighter and greyer and glows blend differently
  from the mockups. `ArenaEditor` sets `PlayerSettings.colorSpace = Gamma` when the project opens (ProjectSettings
  aren't in git), and the game logs a line at start if it's still Linear. It also turns on 4x MSAA.
- **IMGUI gotcha:** with a modal open (F1 menu, Cell Editor) the screen underneath is drawn **only on
  Repaint**, otherwise its buttons steal clicks (see `modal` in `OnGUI`).
- **Knight melee:** swings are queued (`pendingSwings`) and resolved in `Collide` via a pseudo-bullet
  (`Defs.KnightSlash`) through `OnBulletHit`, so weapon mods apply. The Lance hits a box
  (`Swing.HalfWidth`, `LanceReach`, `LanceStun`), drawn by `FxKind.Jab`.
- **Arena bounds:** players wrap round the edges (`WrapArena`) except while Narrow Pass's walls are up. Use
  `ClampToArena` / `KeepInArena` / `RandomInArena` / `ArenaMin`-`ArenaMax` (instance members, in `Paths.cs`
  and `Game.cs`) rather than `0..Width`, so new code respects the walls. Boss-only code still uses the full
  arena (paths never apply to boss fights).
- **The 100-wave Campaign (legs):** `Game.LongRun` (start message, saved) makes each track wave `Wave` two fights, `Leg` 0
  and 1 (snapshot, saved); the player sees `ShownWave` (1-100), `ShownStart/ShownEnd(n)` convert a track wave, and
  `NextTrackWave` is the next regular fight's. Everything that scales or unlocks keeps reading `Wave`; per-wave rewards
  multiply by `Pace` (0.5). Show numbers to players with `ShownWave`, never `Wave`. Off (`LongLegs` false) in Hunts,
  the Boss Lab, the lobby preview and older saves. Sim tests run the 50-wave track (`Game.LongRunDefault`
  false) except suite `longrun`. Profile records (best wave, unlocks, rank XP) are still on the track's scale.
- **The boss between waves:** `Game.BossSlot` (`Hunts.cs`) is true while the boss after regular wave `Wave` is fought (and in the box after it); `Wave` itself doesn't move for it. Ask `BossNow` ("a boss fight, or just beaten") and `BossNext` ("the boss starts next") instead of `Wave % 5`; `IsBossWave(n)` only says "a boss follows wave n" (and, in a hunt, "n is the last wave"). `Waves.cs` starts it (`BossStartsNext`), the snapshot and saves carry it (a save from before it counts a multiple of 5 as beaten), and a negative `Session.StartWave` (-10) starts at the boss after that wave (Boss Lab, tests; `DevJumpToWave(n, true)` in the dev dock).
- **Path state is derived, not sent:** each game eases `NarrowF` / `FogF` from `CurrentPath` + wave, and ages
  patches itself; only the offer, the choice and each patch's spawn go over the network.
- **Classes** are registries, like abilities: each class keeps its `KitDef` (name, blurb, icon, health and speed
  shares, hooks), its `WeaponDef` list and its `AbilityDef` list in its own file; one line in `Classes.Lists` gathers them (`Classes.All`,
  `Defs.Weapons` and `Upgrades.Abilities` are built from it), and each entry's `Kit`/`Role`/`Choice` places it in the lobby tables (indexed by
  role × kit, `Defs.KitCount = 6`). FireVolley calls the weapon's `WeaponDef.Fire`, or else its class's `KitDef.Fire`;
  CastAbility's default goes to `KitAbility`, then each class's `KitDef.Ability`, then `Ultimate`. The look is a
  `KitLook` line in `Game/ClassLooks.cs`. See "Adding a class" below. Hemomancer abilities cost health:
  `AbilityDef.HealthCost` (share of max health, or of current with `CostsCurrent`), paid in `HostAbility` before the
  cast, never below 1 HP.

### Grid vocabulary

See "Building on the grid" in `Docs/Glossary.md`: cell, shape (`PieceShape`), piece card (`PieceCard`), modifier (`PieceMod`), placement, piece chest, Black Market.

### Adding an enemy

Every non-boss enemy runs on a **brain** (Docs/EnemyBehaviours.md): parts for its move (and turning, touch damage),
special, fire and shield, plus role flags. Add a line to its family's `...Brains(t)` (e.g. `ShootBrains` in
`Core/ShootBrains.cs`), reusing parts or wiring a new method with `CodeMove`/`CodeSpecial`/`CodeFire`; looks stay in
`Game/*Look.cs`. `Tools/SimTest/run.sh 1 1 aitrace` fails until it has one; then `AITRACE=update` adds its line to
`Golden/aitrace.txt`.
The owner designs new enemies in **Enemy Forge** (`Tools/EnemyForge`, Docs/EnemyForge.md) and pastes its brief: a
Markdown summary, an `EnemyDef` draft and the JSON spec, with a "plays like" enemy to start the brain from.

### Adding a screen or a menu row

UI code is built from four small pieces (all CRT look; `Ui.cs` is drawing primitives only):

- **Widgets** (`Game/DevKit.cs`): `DkKey` (button), `DkChoice` (lit while chosen), `DkSwitch`, `DkField`, `DkSub` / `DkPanel` /
  `DkHud` / `DkBox` (boxes), `DkTitle`, `DkTag`, `DkBar`. Pick a colour with `Ui.Neutral` (phosphor), `Ui.Danger`, `Ui.GoldButton`
  or any colour for an accent.
- **Layout** (`Game/UiLayout.cs`): `DkStack.Column/Row(area, gap)` with `.Next(size)`, `.Space`, `.Rest()`, and `DkStack.Cells` /
  `Grid` for keys and tiles, so rows reflow when you change a size. `DkWindow(panel, title, sub, onClose)` is the shell of a tool
  window (dim backdrop, box, title, X key) and returns the body rect.
- **Tool screens** (`Game/UiScreens.cs`): a `UiScreen` (id, draw method, `Modal`, `OnEsc`, `EscNeedsFreeFocus`, `OnClose`) in a stack.
  To add one: make a `UiScreen` in `InitScreens()` (draw method = your `DrawFoo`), optionally a `bool fooOpen` property over it like
  `devMenuOpen`, and call `OpenScreen` / `CloseScreen`. ArenaApp blocks the game's input while a modal screen is open, draws the top
  one and gives it Esc: no edits to the Esc chain or the draw switch.
- **Menu rows** (`Game/MenuDrawer.cs`): the pause and lobby menus are `List<MenuRow>` (title, line, icon, colour, action, `Key`,
  `Danger`, `Visible`) drawn by `DrawMenuRows` and moved by `MenuRowKeys`. Add or remove a row in `PauseMenuRows()` /
  `LobbyMenuRows()`; nothing else changes.

Screens that are part of a flow (the pause menu, lobby, Settings, profile, run stats) still use their own flags and the Esc chain in
`ArenaApp.Update`; move one onto the stack by giving it a `UiScreen` the same way.

### Adding a class

A class is mostly two new files plus a line in each registry. Templates: `Docs/ClassTemplate/MyClass.cs` (logic) and
`Docs/ClassTemplate/MyClassLook.cs` (drawing); both compile once copied in and renamed (checked as a 7th class in Oct
2026).

1. **Ids** (always at the end of each enum, before `None`, so the network and saves keep the old numbers): a `Kit`
   value (`Defs.cs`) and `Defs.KitCount` + 1; 9 `WeaponId`s (`Defs.cs`); 9 1st-ability and 3 ultimate `AbilityId`s
   (`Upgrades.cs`). New player bullet kinds, if any: a `Defs` byte constant with a row in `Defs.BuildBulletTable` (radius,
   colour, and damage for enemy shots) and its drawing in `WorldShots.DrawSmallShot`. **Bump `Session.ProtocolVersion`.**
2. **`Core/<Class>.cs`** from the template: its `KitDef` (name, blurb, icon, `HpMult`/`SpeedMult`, hooks), its 9
   `WeaponDef`s (each with `Kit`, `Role`, `Choice` 0-2; `Sound`; `AreaDesc` if its damage lands in an area), its 12
   `AbilityDef`s (1st ability `Choice` 0-2, ultimates `AbilityDef.Ult`; a `Bot` rule each), the fire method (one `case` per
   weapon), the cast method (one `case` per ability, ultimates included, returning true) and a tick if it needs one.
   Extra-projectile, Barrage and Homing texts: `ExtraDesc`, `BarrageDesc`, `HomingDesc` on the weapon. A weapon with an Alt
   gets a smart-bot rule: `BotAlt = <Weapon>BotAlt` on its `WeaponDef` (a static method beside the weapon's code, e.g.
   `LanceBotAlt` in `Lance.cs`; `BotAltHold = true` if the Alt is held, not tapped).
   Unusual weapon ranges (charge, beam, boomerang) get a `Range` on the `WeaponDef` (read by `WeaponRange`, `Weapons.cs`);
   a hold-to-charge weapon sets `Charge = true`.
3. **Register it** with one line in `Classes.Lists` (`Classes.cs`: its `KitDef`, weapon list and ability list); `Classes.All`,
   `Defs.Weapons` and `Upgrades.Abilities` are built from it. The game stops at start-up with a message naming anything missing or doubled.
4. **`Game/<Class>Look.cs`** from the template, plus its `KitLook` line in `Game/ClassLooks.cs`, and an `L(WeaponId.X, ...)`
   line per weapon in `Game/WeaponLooks.cs` (held model, grip, arms/pauldrons, melee colour and rest pose). Per-class player
   fields (timers, buffs) go on `Player` in `Game.cs`, and in `SyncPlayer` (`GameNet.cs`) if clients draw them.
5. **Icons** in `PixelArt.cs`: the class card (`class_<name>`, named by `KitDef.Icon`) and one per 1st ability
   (named by its `AbilityDef.Icon`). Ultimates have none.
   **Upgrade cards** for the class (its 1st abilities' Rare cards, weapon or class cards) go in `Game.<Class>Cards`
   beside its abilities, added to `Upgrades.All`'s `Gather` (Docs/UpgradePools.md).
6. **Check:** `Tools/SimTest/run.sh 1 1 classes` lists every missing piece (weapon cases, ability casts, icons, blurb,
   look). Then a sim test file for the class (`Tests/<Class>.cs`, see `Tests/Ninja.cs`), `run.sh 1 1 extra` and `aim`
   (every weapon's extra projectiles and range), a Boss Lab run (`run.sh 1 1 lab 5 2 3 D/<Name>/0`), previews
   (`KIT=<n> Tools/Preview/shot.sh <name> kitgrid`, `LOADOUT=...` in `lobby`), README.

Things that already work for any class without edits: the lobby's class cards (they size themselves to
`Defs.KitCount`), saves and records (by name).
The lobby fits about 8 class cards in a row before they get too small.

## 3. Testing and previewing (no Unity in the cloud container)

- **Setup:** the cloud container may not have mono. Install it with
  `apt-get install -y mono-mcs mono-runtime libmono-system-core4.0-cil` (Pillow for previews is already there).
  Without mono, `check.sh` now fails with a clear message (exit 2); it used to print "OK" anyway.
- **Cell layouts:** edit them in the game (F1 > LABS > CELL EDITOR); SAVE writes `CellLayoutEdits.txt` (project root in the Editor), which the game loads at start. `python3 Tools/CellEdits/fold.py` writes it into `Core/CellLayouts.cs` (do it before committing, then the `celllayouts` suite). The editor's tabs: CELLS (the 18 cell layouts), ROOM VIEW (a piece's cells together; preview `CEDTAB=1 CEDSHAPE=n`) HUB (the six hub floor plans, folded into `HubLayouts.Generated.cs`) and CORE (the ten core floor plans, folded into `CoreLayouts.cs`; the `corelayouts` suite checks the rules). TEST LAYOUT / TEST builds the cell, piece or hub plan in your current run (start a run first). Dev only: every machine in a game needs the same files.
- **Per-tick garbage and distance tests:** to walk a copy of the enemies while they change, use `SnapEnemies()` / `SnapKeys(dict)` and `Free(list)` (`Core/Scratch.cs`), not `new List<Enemy>(Enemies.Values)`; for a one-off list, `enemyLists.Rent()` and `Return`. To compare a distance, use `a.CloserThan(b, r)` / `WithinReach` / `FartherThan` / `FartherOrAt` (`MathTypes.cs`) rather than `a.DistanceTo(b) < r` (no square root). Suite `scratch`.
- **Reference tables:** `Docs/Reference.md` (weapons, abilities, enemies, roles, classes) is generated from the definitions: after a balance or text change run `Tools/SimTest/run.sh 1 1 refdocwrite` and commit it; the `refdoc` suite fails when it is stale.
- **Compile check:** `Tools/CompileCheck/check.sh` (mono `mcs` + the Unity stubs in `Tools/Preview/Stubs.cs`, shared
  with the previews, RenderCheck and SoundCheck). If the game starts using a Unity member the stubs lack, add it there.
  Missing so far and avoided: `Mathf.Lerp`, `Mathf.Tan`, `Mathf.Approximately` (use `System.Math`).
  `check.sh -W` (run by CI) also fails on any compiler warning: the game compiles with none. A field only
  `Tools/Preview` sets (by reflection) gets `#pragma warning disable 0649` around it; `check.sh -w` lists them.
- **Sim tests:** `Tools/SimTest/run.sh <wave> <seconds> [suite]` builds `Program.cs` (shared helpers, the **`Suites`
  table** and `Main`) and `Tests/*.cs` (one file per system: `Match`, `Upgrades`, `Weapons`, `Abilities`, `Parry`,
  `Knight`, `Ninja`, `Hemomancer`, `Classes`, `Enemies`, `Bosses`, `Paths`, `BossLab`, `Reports`, `Music`). `run.sh list` prints every suite.
  - **Setup helpers** (`Program.cs`): `HostAs(new Session { ... }, port, bot, name, role, ...)`, `JoinAs(port, bot, name,
    role, ...)`, `WaitForGame(host, client, dt)`, `WaitForLobby(host, client)` and `PollBoth(host, client)`. Use them in
    new tests instead of writing the loops out. Big systems span several files (`Weapons`, `WeaponsByKit`,
    `WeaponsSpecial`; `Bosses`, `BossesLate`, `BossMechanics`).
  - `run.sh 1 30` (or `core`) = the **core** match tests (~165 checks).
  - `run.sh 1 30 features` = the **feature** suite (~247 checks). `music` still works: it was its old name.
    **Run both before committing.** `run.sh 1 30 all` runs the two back to back.
  - `Tools/SimTest/extras.sh [k/n]` = every pass/fail suite the two above skip (`CiExtras` in `Program.cs`, ~6 min here),
    each in its own process, built once; `k/n` runs one shard. CI runs it as two shards beside `core` and `features`.
    **A new suite whose tests aren't in `FeatureTests` goes in `CiExtras`.** `TIMES=1 run.sh ...` prints each test's time.
  - Single systems: `snapshot` (run snapshots, F6 / F7), `parry`, `aim`, `extra`, `knight`, `ninja`, `blood`, `bloodlash`, `paths`, `bazaar`, `bosses`,
    `warden`, `singularity`, `mastery`, `longrun` (the 100-wave Campaign), `bossschedule` (the boss schedules match the old formulas), `quickfights` (Quick Play boss fights with bots: each boss's fight length after waves 10/20/30), `quick` (Quick Play: its waves, boss, records, endless, room building with two bots, the tutorial; prints each boss's health after waves 10/20/30), `bossgate` (the boss gate before a boss: holds the wave until everyone is in, bots walk in; the only suite with `Game.GatesOn`), `wardencycle` (its Warden wave lessons), `reapercult` (the Twin Reapers' faction: Bound Wraiths, Scythe Wraith, Lamplighter, Mourner), `checkpoints` (lives and retries), `classcards`, `showcase`, `ultimates`, `mods`, `cardids` (card ids over the network as 2 bytes), `offers` (every card offer reads the weapon), `cardfilters` (class and weapon-list cards), `classcardlists` (each class's card list), `hitfx` (hit flash and blast thinning), `nonshooters` (Fuse Runner, Hookman, Sapper), `turnkey` (the Warden faction's Turnkey: wave-3 debut, running, traps, Cell Doors), `aitrace` (every non-boss enemy alone for 12 s vs `Golden/aitrace.txt`, the enemy behaviour refactor's safety net, Docs/EnemyBehaviours.md; `AITRACE=update` rewrites it), ... Reports (numbers, no pass/fail):
    `ttk`, `lab`, `levels`, `bulletcount`, `perf` (sim ms a frame, host and friend, through a Quick Play breach wave: `run.sh 1 1 perf [secs] [boss] [wave]`), `survperf` (the same for Survival at the 60-enemy cap: `run.sh 1 1 survperf [secs] [start min]`), `wavemix` (each wave's share of shooters, melee and support), `dps`, `ultdps`, `balance` (each damage upgrade's worth per pick and full
    stack in a boss, pack and spread fight; see `Docs/UpgradeBalance.md`), `upgradecov` (which weapon upgrades each
    weapon ignores, and each upgrade's damage gain), `laserdebug`. A new test: add it to its system's file, then to
    the `Suites` table (and to `FeatureTests` if it belongs in the feature suite, otherwise to `CiExtras`).
  - **Repeatable random rolls:** every test runs with its own seed (from the suite's name and the test's place in
    it) through `Game.SeedForTests`, so the game's random choices (enemy mixes, spawns, drops, bot decisions)
    come out the same each run. Rolls a game keeps off its own rng (the Expanding map's seed, the Warden's orbs,
    Quick Play's order, party bots) come from `Game.NewSideRandom()`, seeded the same way. `SEED=random` gives fresh
    rolls; `SEED=<n>` tries another fixed set (sweep a few to find seed-fragile checks); `ONLY=<i> run.sh 1 1 <suite>`
    runs just the suite's i-th test (from 0) with its usual seed; `PORTBASE=<n>` lets runs go in parallel. Timing
    between the host and client threads still varies a little, so a check that only just passes can still
    flicker. The real game never seeds, so it stays random.
  - Formerly flaky checks, now steady under the seeds (each passed 4+ runs in a row in Oct 2026): perfect
    parry, extra projectiles, bullet budget, Barrier vs regular waves, Mirror Guard, Swarm Fields / Sniper Ridge
    mixes, fire warning, the session's "regeneration" entry. "Once broken, the parry stops blocking" used to wait
    for the Warden to fire; it now fires its own test shot. If one starts failing for every run, it's a real
    break (or that seed found an edge): check it with `SEED=random` a few times before blaming the seed.
  - Boss Lab batch from the CLI: `run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat]`, seats like `T/K/1`
    (Tank Knight, second weapon; `M` = Elementalist, `U` = Summoner, `N` = Ninja, `B` = Hemomancer, or the start of
    any class's name, e.g. `D/Hemo/0`).
- **Visual effects check:** `Tools/RenderCheck/juice.sh` drives the effects (`Game/Juice.cs`) through real frames and
  checks each one fires; the render check below runs with effects off.
- **Sound check:** `Tools/SoundCheck/render.sh [prefix]` renders the synthesized sound effects whose names start with
  prefix (default `Boss`) from `SoundBank.cs` to `Tools/SoundCheck/out/*.wav`, so they can be heard (and their levels
  checked) without Unity.
- **Sound Forge:** open `Tools/SoundForge/index.html` in a browser: a three-layer synth (tone or noise, pitch glide,
  FM, envelope, swept filter, tremolo/flicker; drive, bit crush, a 5-band EQ over the sound's spectrum, echo,
  output volume; a seamless loop mode for hums) with presets and your own saved presets (browser storage, Export/Import JSON), that exports 44.1 kHz 16-bit WAVs named after a
  game sound (`ShotRifle.wav`, takes `ShotRifle_1.wav`...) for the sounds folder. `names.py` refreshes its name list
  from the `Sfx` enum; its settings JSON can be pasted to Claude to make a code recipe.
- **Render check:** `Tools/RenderCheck/check.sh [git-ref]` renders 16 fixed, seeded scenes (every enemy and minion
  type, each class's abilities and ultimate; four Ninja and three Hemomancer scenes, so every class's own shots are
  drawn; refs before the Ninja can't build it) with `DrawWorld`, in the working tree and at `git-ref` (default
  `HEAD`), and compares every GL triangle. `IDENTICAL` means nothing on screen changed: use it for any rendering
  refactor.
- **Every UI screen at once:** `Tools/Preview/uishots.sh` builds the preview once and renders each screen listed in
  `Tools/Preview/ui-screens.txt` (main menu, Play picker, Who's playing, profile, Look, Records, settings sections,
  lobby tabs, lobby Esc menu, HUD, Tab, pause, wave cleared, picker, paths, shop, game over, victory; about 30) in
  parallel to `Tools/Preview/out/ui/<name>.png`, plus `sheet.png` (all of them, labelled). Names after it render only
  screens starting with those (`uishots.sh lobby- menu`). `-r <git-ref>` also renders the same screens at that commit
  and prints which are unchanged (pixel-identical) and which changed, with `compare.png` showing the changed ones side
  by side (ref left, now right). Shots are repeatable: the preview seeds the game's and Juice's random rolls
  (`Game.SeedForTests(1)`; `RANDOM=1` turns it off) and dates its made-up saves from `Shot.Now`, a fixed time. Screens with
  several players differ a little from run to run (real network timing); they're marked `NOISY=1` in the list and
  reported apart, to check by eye. Use it for UI redesigns: `-r HEAD` before committing a refactor that should look the
  same. About 80 s for all, about 3 min with `-r`.
- **Variant sheet:** `Tools/Preview/variants.sh <name> <mode> [wave secs players] VAR=a,b,c [VAR2=x,y]` renders every
  combination of the listed env-flag values (or `WAVE`/`SECS`/`PLAYERS`) in parallel and lays them out as a labelled
  grid, `Tools/Preview/out/<name>.png`. Values split on `|` when they hold commas (`MOUSE=100,200|300,400`). `CROP=x,y,w,h`
  zooms on a part of the screen, `SCALE`, `COLS`. Use it to offer the owner looks to choose from.
- **Draw cost (lag):** `PERFRUN=70[,boss] PERFIDLE=50 Tools/Preview/shot.sh <name> hud 7 1 2` (`ShotPerf.cs`) plays a Quick
  Play breach wave (default the Twin Reapers' cult, two rooms built) with the bots standing still `PERFIDLE` s so a crowd
  builds, drawing every frame; every 2 s it prints enemies, particles, GL vertices a frame and draw ms, plus the top
  vertex sources of one traced frame (`GL.Trace` in `Stubs.cs`). Unity's GL immediate mode pays per vertex, so the
  vertex count is what to cut (`OffScreen`, `SegsFor`, `CrowdTol` in `WorldRenderer.cs`; `PartCrowd` in `Juice.cs`; round-ended
  limbs use half discs, `Cap`/`HalfDisc` in `WardenLook.cs`). One frame: `CROWD=<n>` (a mob round you) with `VTXTRACE=1`. `PERFMAP=<n>`: on a grown
  map (the grid core and n pieces). Its stdout goes to `/dev/null` in `shot.sh`: run `mono shot.exe` in `Tools/Preview/out`.
- **Sim cost on a big map (lag):** `run.sh 1 1 breachcrowd [secs] [wave]` (`Tests/BreachCrowd.cs`) plays a Warden breach wave
  with timed spawning on a six-breach map, bots fighting: alive enemies by type and host ms a frame. Enemy steering
  (`SteerDir`, `GoalCosts` in `LayoutRules.cs`) was the cost there; `SolidGrid.cs` keeps its line tests to nearby walls.
- **GIF clips:** `Tools/Preview/clip.sh <name> <mode> [wave secs players]` keeps the run running after the shot
  (`CLIP=<frames>` in `ShotClip.cs`; `FRAMES=30`, `CLIPSTEP=3` fixed steps a frame = 20 fps, `CLIPHURT=1`), or with
  `VAR=from:to:step` draws one frame per value of a scene's time flag (`CASTT`, `THRUST`, `KDT`, ...). Writes
  `out/<name>.gif` and `out/<name>-strip.png` (six frames). About 3 s a frame on 4 cores; `CROP` keeps GIFs small.
- **Balance grid:** `Tools/BalanceGrid/grid.sh [waves=10,20,30] [runs=2] [roles=DTH] [skill=2] [classes]` runs the
  `grid` report (`Tests/BalanceGrid.cs`: each role, class and weapon fights each listed boss in the Boss Lab, through
  `LabFight`) on every core (`GRIDSHARD`, `PORTBASE`), writes `out/grid.csv` and draws `out/grid.png`: win rate, fight time
  and the seat's boss DPS per cell, coloured by `METRIC=wins|dps|time` (dps and time against the role's median), plus
  the biggest DPS outliers. `GRIDTEAM=auto` (default) gives every row the same two partners (Soldier Tank / Soldier
  DPS / Elementalist Healer for the other roles); `GRIDTEAM=` fights alone. 330 fights (three bosses, two runs) take ~3 min.
- **Screenshots without Unity:** `Tools/Preview/shot.sh <name> <mode>` → `Tools/Preview/out/<name>.png`. The arena's
  triangles are drawn like the GPU (`Tools/Preview/raster.py`: colour blended across each triangle, alpha blending, the
  glow pass adding light, 2x supersampled; about 7 s); `FAST=1` gives the old quick look (each triangle flat in its
  first vertex's colour, no glow), which can't show gradients or glows, so don't judge effects with it. Text is drawn
  in the game's own fonts (VT323 for the CRT text, Rajdhani for menus: `Resources.Load<Font>` in the stubs returns a named
  font, `TextW` measures with its width table, `shot.py` draws with `Assets/Resources/Fonts`); unstyled labels stay
  Arial (Liberation Sans). `SHOTW=<WeaponId>` fires that weapon three times (shots at 0.06 / 0.18 / 0.3 s) for comparing shot looks with
  `Docs/Mockups/projectiles.html`. `LINEAR=1` shows
  how a Linear-colour-space project would draw it (washed out); the game is meant for **Gamma** (see below). Modes: `hud`,
  `tab`, `picker`, `dev`, `lobby`, `menu`, `bg` (with
  `BG=0..9`), `pause`, `kitgrid`, `legendary`, ... Path flags: `PATHOFFER=1|2|3|4` (the choice screen with each pair
  of paths), `PATHON=<PathId number>` (in-arena look of that path; 8 = Fogbound, 9 = Narrow Pass), `PATCHES=1` (sample
  fire/pools). Others: `LOBBYTAB=n` (lobby tab, e.g. Modifiers), `BOTS=1|edit` (a bot in Aleon's seat; `edit` also opens its loadout), 
  `DEVDRAWER=1..4` / `DEVSIDE=1` / `DEVSHEET=4..7` (F1 dock: drawer, stat editor, sheet: upgrades, enemies, snapshots, gadgets, chests and relics; `SPAWNERMODE=1` the enemy sheet's Spawner row), `DEVSPAWNER=1` (two dev spawner pads in the world), `SHOCK=<s>` (the Shockwave ability that long after the cast, played through the game; `SHOCKCAP=1` mastered), `HAMMER=1` (Storm
  Hammers in flight and an electrified enemy arcing), `BOULDER=<draw secs>` (the Golem Staff charging, with its
  landing marker, and boulders in flight), `CAGE=1` (a Bone Guard cage, with guard shots), `WYVERN=<s>` (the Wyvern Staff hovering, then breathing; `WYALT=1` the slam), `WEAPONS5=1` (Fuma
  Shuriken, Chain Light, Sanguine Orb coating, Fireballs and Arc Lightning in one scene), `HEMO=1` (enemies with 1-4
  bleed stacks), `NEEDLESWARM=<s held>` (`NSALT=<s>`: the Needle Swarm's tendrils, wraps and Alt fan), `HOOK=whirl` or `HOOK=<0..0.65 s into the throw>` (a Kusarigama's whirl, or its thrown weight). Env flags: `KNIGHT=<weapon>`,
  `POSE=1`, `THRUST=<0..1>`, `BOW=<draw>`, `MASTERY=1`, `KIT=n` with mode `kitgrid` (every role and weapon of a class,
  drawn big), `NINJA=<weapon>` / `BLOOD=<weapon>` (like `KNIGHT=`), `MOUSE=x,y`, `AIMLINE=<px>` (mouse that far from
  you, with the aim line), `SOLO=1`, `INFO=1` (open the lobby's "!" connection popup), `BG=n` (also in `hud`: the
  arena background), `PICKCARDS=a,b,c` (picker: these three cards), `LOADOUT=role,kit,weapon,ability` / `ABILITY=n`
  (click that ability key: picked, detail box, preview casting it) / `SHOWT=secs` (the lobby's live preview). The renderer is approximate (no text fields, faint seams in big
  circles, thin translucent lines look a few times brighter than in Unity; translucent UI and rimmed cards were fixed
  to match Unity in Oct 2026, so older preview PNGs look fainter); use it to check layout, then ask the owner to look
  in Unity.
## 4. How the owner works

Moved to `CLAUDE.md` ("How the owner works"), which every session reads.

## 5. Recent history

Moved to [`Docs/History.md`](Docs/History.md) (newest first). Read it only when you need the background to a
feature; `git log --oneline` is usually enough.

---

## 6. Open ideas from the audits

The October 2026 audit and its work plan are done; the full notes (and the November second pass) are in
[`Docs/History.md`](Docs/History.md) under "Audit findings". Still open:

- **Late-Nov 2026 audit, still open:** `TtInCone` and `AmmoInCone` stay their own (reach tested as `d - r > reach`, a
  full-circle shortcut, a linear angle pad and mixed `<`/`<=`: four options `MathX.InCone` would need); the irregular enemy rings (gaps, nested, half-step) not on `EnemyRing`; HUD strings
  rebuilt every OnGUI pass (`Hud.cs`), a new `byte[]` per received message (`Net.cs`), pooled `Effect`s; 9 abilities
  with no icon (suite `classes` lists them). Done: 11 network/sim bugs, solo snapshot cost, `EnemyIndex`, render
  buckets (`FxOf`/`EnemiesOf`), `BossFight`, `WeaponAlt`, `AbilityDef.Icon`, ~900 lines of dead code, `WeaponDef.BotAlt`
  and `WeaponLooks.cs`, the weapon switches (`WeaponDef.Fire`/`Range`/`Charge`; `WeaponLook.Model`, `DpsShot`/`DpsTick`/`DpsPose`,
  `TankShot`/`TankPose`), per-weapon `Sync*` methods (SyncPlayer 550 -> 219 lines), `MathX.InCone`/`AngleWithin`,
  `EnemyRing`, one gold (`Rgba.Gold` = `CrtStyle.Gold`; the 1/.82/.3 and 1/.82/.4 near-golds folded in).
- **Reference tables are generated** into [`Docs/Reference.md`](Docs/Reference.md) (`run.sh 1 1 refdocwrite`; suite `refdoc` fails when stale). Still open: the README's hand-written weapon/enemy prose could link to or drop numbers that table now holds.
- **Collision is all-pairs** (player bullets x enemies, enemy bullets x players). Cheap with the bullet budget; a
  uniform grid only matters if counts grow a lot (endless mode, more players).
- **Rendering** is GL immediate mode with many small circles (8-48 segments). Fine on desktop; if frame time ever
  matters, lower segment counts for small bullets and batch the menu backgrounds. The faction looks are the heaviest
  enemies (a Hive bee was 4-5x a Warden enemy's triangles): `HiveLook.cs` sizes its ellipses by screen size and drops
  the glow strokes past `HiveCrowd` enemies; do the same for another faction look if a flood of it lags. Bandwidth is fine for 3 players
  (full state at 20 Hz, no bullets); delta compression only if player counts grow.
- `Kits.Zones` and `Kits.Minions` are read by kind number, so removing a kind means removing its entry too (the `core`
  suite checks the counts) and bumping the protocol. The **simple test bot** in
  `LocalControl` (`Bot` without a `BotLevel`) is *not* dead: most sim tests and `--bot` rely on it.
- **Paths** are built but untuned (Frozen Lake's grip, Fogbound's 350 px sight, Narrow Pass's 1120 x 720, fire
  damage); planned but not built: a route strip in the Tab menu, the route in the run summary. Open: should waves 1-4 get a path choice? See [`Docs/Paths.md`](Docs/Paths.md).
- **Boss approaches:** see the status line of [`Docs/Approaches.md`](Docs/Approaches.md) for which factions are built.
- **Campaign as a themed-enemy buildup (idea, not designed):** the campaign accumulates each boss's themed enemies;
  after a boss falls they phase out slowly while the next boss's faction phases in, and some squads or enemies can
  return later on. The **Boss Hunt** should then be an enhanced version of those themed mobs (more types, more of
  them, tougher squads). Design: [`Docs/Campaign.md`](Docs/Campaign.md).
- **Checkpoints and lives (planned):** every boss kill is a checkpoint; a wipe can retry from it while lives last
  (by difficulty: 3/2/1/0), at a score cut. Design: [`Docs/Checkpoints.md`](Docs/Checkpoints.md).
- **100-wave Campaign (planned):** two main modes (Campaign, Boss Hunt); a boss every 10 waves, each cycle teaching its
  boss, arena style per cycle, scaling by effective wave. Design: [`Docs/RunLength.md`](Docs/RunLength.md).
