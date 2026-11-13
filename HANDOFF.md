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

- **What:** a 1-3 player **co-op top-down bullet hell** made in **Unity 6** (`6000.6.7f1`, Unity 6.6; built-in render
  pipeline). The owner plays it with a friend over **Tailscale** (one hosts, the other joins by IP).
- **A run:** 50 regular waves, a boss fight after every 5th (Warden, Hive Queen, Twin Reapers, Singularity, Colossus, Duelist,
  Lich, Storm Herald, Devourer, Eclipse), then optional endless mode. Level-ups give a pick of 3 upgrade
  cards (rerolls available); each boss gives a legendary upgrade and a weapon mod.
- **Players** pick a **role** (Tank, Healer, DPS) × **class/kit** (Soldier, Elementalist, Summoner, Knight, Ninja,
  Hemomancer; `Kit.Elementalist` and `Kit.Hemomancer` were `Kit.Mage` and `Kit.BloodMage` until Oct 2026, and many
  methods and files still say Mage or BloodMage, e.g. `BloodMage.cs`), then one of 3 weapons and one of 3 Space
  abilities for that combination. Each role × kit has an ultimate (F). Bonus abilities on Q/E/Shift come from very
  rare upgrade cards. Abilities level up with use (**mastery**, levels 1-5, with a level-5 "capstone" bonus each).
- **Defence:** everyone has a **parry** shield (right mouse) that soaks damage and can reflect; Knights' swings
  cut small bullets. Boss bullets come in two kinds: violet (wear down blockers) and **red aimed shots**
  (unblockable except by the Knight's Shield Wall).
- **Pacing rules:** a **bullet budget** caps regular enemies' bullets on screen (40 + 3/wave, max 85,
  +20% per extra player; 60% of it while a boss is up on Normal). Touching a boss hurts once per second and shoves
  you ~100 px out.
- **Modes in the lobby:** **Run** or **Practice Range** (hand-built waves of any enemies/bosses at any level,
  shared live with the lobby, savable as text files). The host picks the mode, and the run's **modifiers**
  (Modifiers tab: Glass Cannon, Tough Crowd, Frenzy, Swift Foes, Famine, Lucky).
- **Divergent paths:** after each boss the host picks one of two **paths** (or no path) for the five regular
  waves before the next boss: enemy mixes, hazard floors, twists and arena rules (9 paths, danger 1-3 with a
  bonus and a prize). Bosses are always standard. See `Docs/Paths.md`.
- **Dev tools (F1):** a dock over the live game (`DevDock.cs`: pinned God / Full heal / Skip wave / Kill all, WAVE / PARTY / VIEW / LABS drawers, a stat-editor side drawer, sheets for DPS, Replays and Balance): stat editor, god mode, wave skip, session
  recording toggle, **DPS meter (F2) and target dummies**, **Boss Lab** (bots that play like people fight any boss; live stats; also a CLI batch mode).
- **Session recording:** the host's matches are saved as JSON (per-wave damage by source, picks, downs,
  samples). The owner uploads these into sessions for balance analysis; `Tools/SessionReport/report.py`
  summarises them.

## 2. How the code is organised

```
Assets/Scripts/Core/   plain C# game logic (namespace Arena); no UnityEngine dependency at all
Assets/Scripts/Game/   the Unity front end: rendering (GL), IMGUI menus/HUD, sound and music synthesis
Assets/Editor/         ArenaEditor.cs: scene setup and the Windows build menu item
Tools/SimTest/         headless test suite (mono) that drives real Sessions over localhost TCP
Tools/SessionReport/   report.py: summarises recorded session JSON
Tools/Preview/         renders screens to PNG without Unity (stubs + Pillow)         [added in the audit]
Tools/CompileCheck/    fast compile check with Unity stubs                           [added in the audit]
Tools/RenderCheck/     proves a refactor left the arena's drawing byte-identical      [added Oct 2026]
Tools/SoundCheck/      renders the synthesized sounds to WAV to listen without Unity  [added Nov 2026]
Docs/                  design docs, mockups, history
```

Per-file map: **`CODEMAP.md`** (one line per source file; add new files there). The older, more detailed per-file
notes (function names, message flow) are in `Docs/CodeNotes.md`, kept as a reference but no longer maintained.

### Key mechanisms worth knowing

- **Networking:** host-authoritative TCP. Clients send inputs; **clients are authoritative for their own
  movement** (`LocalControl`), the host for everything else. The host sends a full **snapshot every 3 ticks**
  (20 Hz) of players/enemies/minions/orbs; bullets are sent as spawn/remove events and simulated on both
  sides with periodic `BulletSync`. Effects and sounds are broadcast (`Msg.Fx`, `Msg.Sound`).
  **`Session.ProtocolVersion` (see `Core/Session.cs`) must be bumped whenever any message format changes**; mismatched
  players are turned away with a clear message. New `Msg` values go at the end of the enum.
- **Rendering:** no sprites. The world is drawn with **GL immediate-mode triangles** in `OnPostRender`
  (arena 1600×900 scaled to the screen). The UI is **IMGUI** on a 1280×720 virtual canvas (`W`, `H`), scaled.
  Menus can draw game models on top of panels: `BeginUiGl()` / `UiModelAt(pos, zoom)` / `EndUiGl()` issue GL
  during IMGUI's Repaint (used by the lobby and Practice Range tiles).
- **Loadout preview:** `DrawShowcase` swaps the `Showcase` game into `ArenaApp.game` and calls `DrawArenaContents`
  (the part of `DrawWorld` between the floor and the overlays), clipped with `UiClip` (software clipping in `Tri`,
  so it works the same in Unity and the preview tool).
- **Colour space: Gamma.** Everything is drawn with raw vertex colours (`GL.Color`), which Unity doesn't convert in a
  Linear project, so there every colour and see-through layer comes out lighter and greyer and glows blend differently
  from the mockups. `ArenaEditor` sets `PlayerSettings.colorSpace = Gamma` when the project opens (ProjectSettings
  aren't in git), and the game logs a line at start if it's still Linear. It also turns on 4x MSAA.
- **IMGUI gotcha:** with a modal open (F1 menu, Boss Lab setup) the screen underneath is drawn **only on
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
  Practice, the Boss Lab, the lobby preview and older saves. Sim tests run the 50-wave track (`Game.LongRunDefault`
  false) except suite `longrun`. Profile records (best wave, unlocks, rank XP) are still on the track's scale.
- **The boss between waves:** `Game.BossSlot` (`Hunts.cs`) is true while the boss after regular wave `Wave` is fought (and in the box after it); `Wave` itself doesn't move for it. Ask `BossNow` ("a boss fight, or just beaten") and `BossNext` ("the boss starts next") instead of `Wave % 5`; `IsBossWave(n)` only says "a boss follows wave n" (and, in a hunt, "n is the last wave"). `Waves.cs` starts it (`BossStartsNext`), the snapshot and saves carry it (a save from before it counts a multiple of 5 as beaten), and a negative `Session.StartWave` (-10) starts at the boss after that wave (Boss Lab, tests; `DevJumpToWave(n, true)` in the dev dock).
- **Path state is derived, not sent:** each game eases `NarrowF` / `FogF` from `CurrentPath` + wave, and ages
  patches itself; only the offer, the choice and each patch's spawn go over the network.
- **Classes** are registries, like abilities: each class keeps its `KitDef` (name, blurb, icon, health and speed
  shares, hooks), its `WeaponDef` list and its `AbilityDef` list in its own file; `Classes.All`, `Defs.Weapons` and
  `Upgrades.Abilities` gather them, and each entry's `Kit`/`Role`/`Choice` places it in the lobby tables (indexed by
  role × kit, `Defs.KitCount = 6`). FireVolley sends a weapon without its own case to its class's `KitDef.Fire`;
  CastAbility's default goes to `KitAbility`, then each class's `KitDef.Ability`, then `Ultimate`. The look is a
  `KitLook` line in `Game/ClassLooks.cs`. See "Adding a class" below. Blood Mage abilities cost health:
  `AbilityDef.HealthCost` (share of max health, or of current with `CostsCurrent`), paid in `HostAbility` before the
  cast, never below 1 HP.

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
   value (`Defs.cs`) and `Defs.KitCount` + 1; 9 `WeaponId`s (`Defs.cs`); 9 Space and 3 ultimate `AbilityId`s
   (`Upgrades.cs`). New player bullet kinds, if any: a `Defs` byte constant with its radius and colour
   (`BulletRadius`, `BulletColor`) and its drawing in `WorldShots.DrawSmallShot`. **Bump `Session.ProtocolVersion`.**
2. **`Core/<Class>.cs`** from the template: its `KitDef` (name, blurb, icon, `HpMult`/`SpeedMult`, hooks), its 9
   `WeaponDef`s (each with `Kit`, `Role`, `Choice` 0-2; `Sound`; `AreaDesc` if its damage lands in an area), its 12
   `AbilityDef`s (Space `Choice` 0-2, ultimates `AbilityDef.Ult`; a `Bot` rule each), the fire method (one `case` per
   weapon), the cast method (one `case` per ability, ultimates included, returning true) and a tick if it needs one.
   Extra-projectile, Barrage and Homing texts: `ExtraDesc`, `BarrageDesc`, `HomingDesc` on the weapon.
   Unusual weapon ranges (charge, beam, boomerang) get a case in `WeaponRange` (`Weapons.cs`).
3. **Register it** in three lists: `Classes.All` (`Classes.cs`), `Defs.Weapons` (`Defs.cs`), `Upgrades.Abilities`
   (`Upgrades.cs`). The game stops at start-up with a message naming anything missing or doubled.
4. **`Game/<Class>Look.cs`** from the template, plus its `KitLook` line in `Game/ClassLooks.cs`. Per-class player
   fields (timers, buffs) go on `Player` in `Game.cs`, and in `SyncPlayer` (`GameNet.cs`) if clients draw them.
5. **Icons** in `PixelArt.cs`: the class card (`class_<name>`, named by `KitDef.Icon`) and one per Space ability
   (`ByUpgrade` maps the ability's name to it). Ultimates have none.
6. **Check:** `Tools/SimTest/run.sh 1 1 classes` lists every missing piece (weapon cases, ability casts, icons, blurb,
   look). Then a sim test file for the class (`Tests/<Class>.cs`, see `Tests/Ninja.cs`), `run.sh 1 1 extra` and `aim`
   (every weapon's extra projectiles and range), a Boss Lab run (`run.sh 1 1 lab 5 2 3 D/<Name>/0`), previews
   (`KIT=<n> Tools/Preview/shot.sh <name> kitgrid`, `LOADOUT=...` in `lobby`), README.

Things that already work for any class without edits: the lobby's class cards (they size themselves to
`Defs.KitCount`), the Boss Lab's class picker, the Field guide, saves and records (by name), the session recorder.
The lobby fits about 8 class cards in a row before they get too small.

## 3. Testing and previewing (no Unity in the cloud container)

- **Setup:** the cloud container may not have mono. Install it with
  `apt-get install -y mono-mcs mono-runtime libmono-system-core4.0-cil` (Pillow for previews is already there).
  Without mono, `check.sh` now fails with a clear message (exit 2); it used to print "OK" anyway.
- **Designed waves:** `Tools/WaveGen/build.sh` turns `Data/Waves/*.waves` (the F1 wave editor's files) into
  `Assets/Scripts/Core/DesignedWaves.Generated.cs` with the game's own parser (`Gen.cs`; `Stub.cs` stands in for the
  generated file while it builds, so a stale one can't block it). It stops, writing nothing, on any problem. The
  `wavegen` suite fails if the generated file is out of date.
- **Compile check:** `Tools/CompileCheck/check.sh` (mono `mcs` + the Unity stubs in `Tools/Preview/Stubs.cs`, shared
  with the previews, RenderCheck and SoundCheck). If the game starts using a Unity member the stubs lack, add it there.
  Missing so far and avoided: `Mathf.Lerp`, `Mathf.Tan`, `Mathf.Approximately` (use `System.Math`).
- **Sim tests:** `Tools/SimTest/run.sh <wave> <seconds> [suite]` builds `Program.cs` (shared helpers, the **`Suites`
  table** and `Main`) and `Tests/*.cs` (one file per system: `Match`, `Upgrades`, `Weapons`, `Abilities`, `Parry`,
  `Knight`, `Ninja`, `BloodMage`, `Classes`, `Enemies`, `Bosses`, `Paths`, `Practice`, `BossLab`, `Reports`, `Music`,
  `TextEdit`). `run.sh list` prints every suite.
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
  - Single systems: `parry`, `aim`, `extra`, `knight`, `ninja`, `blood`, `paths`, `bazaar`, `practice`, `bosses`,
    `warden`, `singularity`, `mastery`, `longrun` (the 100-wave Campaign), `bossschedule` (the boss schedules match the old formulas), `quickfights` (Quick Play boss fights with bots: each boss's fight length after waves 10/20/30), `quick` (Quick Play: its waves, boss, records, endless, room building with two bots, the tutorial; prints each boss's health after waves 10/20/30), `bossgate` (the boss gate before a boss: holds the wave until everyone is in, bots walk in; the only suite with `Game.GatesOn`), `wardencycle` (its Warden wave lessons), `checkpoints` (lives and retries), `classcards`, `showcase`, `ults`, `mods`, `cardids` (card ids over the network as 2 bytes), `offers` (every card offer reads the weapon), `hitfx` (hit flash and blast thinning), `nonshooters` (Fuse Runner, Hookman, Sapper), ... Reports (numbers, no pass/fail):
    `ttk`, `lab`, `levels`, `bulletcount`, `wavemix` (each wave's share of shooters, melee and support), `dps`, `ultdps`, `balance` (each damage upgrade's worth per pick and full
    stack in a boss, pack and spread fight; see `Docs/UpgradeBalance.md`), `upgradecov` (which weapon upgrades each
    weapon ignores, and each upgrade's damage gain), `laserdebug`. A new test: add it to its system's file, then to
    the `Suites` table (and to `FeatureTests` if it belongs in the feature suite, otherwise to `CiExtras`).
  - **Repeatable random rolls:** every test runs with its own seed (from the suite's name and the test's place in
    it) through `Game.SeedForTests`, so the game's random choices (enemy mixes, spawns, drops, bot decisions)
    come out the same each run. `SEED=random` gives fresh rolls; `SEED=<n>` tries another fixed set. Timing
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
- **GIF clips:** `Tools/Preview/clip.sh <name> <mode> [wave secs players]` keeps the match running after the shot
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
  first vertex's colour, no glow), which can't show gradients or glows, so don't judge effects with it. `SHOTW=<WeaponId>` fires that weapon three times (shots at 0.06 / 0.18 / 0.3 s) for comparing shot looks with
  `Docs/Mockups/projectiles.html`. `LINEAR=1` shows
  how a Linear-colour-space project would draw it (washed out); the game is meant for **Gamma** (see below). Modes: `hud`,
  `tab`, `picker`, `dev`, `labsetup`, `labpanel`, `lobby`, `lobbypr`, `practicehud`, `menu`, `bg` (with
  `BG=0..9`), `pause`, `kitgrid`, `legendary`, ... Path flags: `PATHOFFER=1|2|3|4` (the choice screen with each pair
  of paths), `PATHON=<PathId number>` (in-arena look of that path; 8 = Fogbound, 9 = Narrow Pass), `PATCHES=1` (sample
  fire/pools). Others: `TEXTEDIT=1` (lobby in text-edit mode, outline under `MOUSE`) or `TEXTEDIT=<text>` (its edit
  box; `TEXTBUF` = typed), `LOBBYTAB=n` (lobby tab, e.g. Modifiers), `BOTS=1|edit` (a bot in Aleon's seat; `edit` also opens its loadout), 
  `DEVDRAWER=1..4` / `DEVSIDE=1` / `DEVSHEET=1..3` (F1 dock: drawer, stat editor, sheet), `FROST=<0..1>` (a Frost Lance icicle that far through, with its splinters), `HAMMER=1` (Storm
  Hammers in flight and an electrified enemy arcing), `BOULDER=<draw secs>` (the Golem Staff charging, with its
  landing marker, and boulders in flight), `CAGE=1` (a Bone Guard cage, with guard shots), `WEAPONS5=1` (Fuma
  Shuriken, Chain Light, Sanguine Orb coating, Fireballs and Arc Lightning in one scene), `HEMO=1` (enemies with 1-4
  Blood Bolts stuck in them), `HOOK=whirl` or `HOOK=<0..0.65 s into the throw>` (a Kusarigama's whirl, or its thrown weight). Env flags: `KNIGHT=<weapon>`,
  `POSE=1`, `THRUST=<0..1>`, `BOW=<draw>`, `MASTERY=1`, `KIT=n` with mode `kitgrid` (every role and weapon of a class,
  drawn big), `NINJA=<weapon>` / `BLOOD=<weapon>` (like `KNIGHT=`), `MOUSE=x,y`, `AIMLINE=<px>` (mouse that far from
  you, with the aim line), `SOLO=1`, `INFO=1` (open the lobby's "!" connection popup), `BG=n` (also in `hud`: the
  arena background), `PICKCARDS=a,b,c` (picker: these three cards), `LOADOUT=role,kit,weapon,ability` / `ABILITY=n`
  (hover) / `SHOWT=secs` (the lobby's live preview). The renderer is approximate (no text fields, faint seams in big
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

- **Generate the reference docs** (README tables, Armory/Bestiary data) from the definitions (`WeaponDef`,
  `AbilityDef`, `EnemyDef`) with a small tool, so a balance change doesn't need three hand edits.
- **Collision is all-pairs** (player bullets x enemies, enemy bullets x players). Cheap with the bullet budget; a
  uniform grid only matters if counts grow a lot (endless mode, more players).
- **Rendering** is GL immediate mode with many small circles (8-48 segments). Fine on desktop; if frame time ever
  matters, lower segment counts for small bullets and batch the menu backgrounds. Bandwidth is fine for 3 players
  (full state at 20 Hz, no bullets); delta compression only if player counts grow.
- `Kits.Zones` and `Kits.Minions` are read by kind number, so removing a kind means removing its entry too (the `core`
  suite checks the counts) and bumping the protocol. The **simple test bot** in
  `LocalControl` (`Bot` without a `BotLevel`) is *not* dead: most sim tests and `--bot` rely on it.
- **Paths** are built but untuned (Frozen Lake's grip, Fogbound's 350 px sight, Narrow Pass's 1120 x 720, fire
  damage); planned but not built: a route strip in the Tab menu, the route in the run summary, `Tools/SessionReport`
  grouping by path. Open: should waves 1-4 get a path choice? See [`Docs/Paths.md`](Docs/Paths.md).
- **Boss approaches:** see the status line of [`Docs/Approaches.md`](Docs/Approaches.md) for which factions are built.
- **Campaign as a themed-enemy buildup (idea, not designed):** the campaign accumulates each boss's themed enemies;
  after a boss falls they phase out slowly while the next boss's faction phases in, and some squads or enemies can
  return later on. The **Boss Hunt** should then be an enhanced version of those themed mobs (more types, more of
  them, tougher squads). Design: [`Docs/Campaign.md`](Docs/Campaign.md).
- **Checkpoints and lives (planned):** every boss kill is a checkpoint; a wipe can retry from it while lives last
  (by difficulty: 3/2/1/0), at a score cut. Design: [`Docs/Checkpoints.md`](Docs/Checkpoints.md).
- **100-wave Campaign (planned):** two main modes (Campaign, Boss Hunt); a boss every 10 waves, each cycle teaching its
  boss, arena style per cycle, scaling by effective wave. Design: [`Docs/RunLength.md`](Docs/RunLength.md).
