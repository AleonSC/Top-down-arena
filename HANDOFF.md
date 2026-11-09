# Handoff: Top-Down Arena

Everything a new session needs to pick up this project: what the game is, how the code is put together,
how to test and preview changes without Unity, how the owner likes to work, and the findings of the
October 2026 code audit (with a prioritised list of improvements).

`README.md` is the player-facing and feature-level reference (every weapon, ability, enemy, boss, control
and tool is described there). This document is the developer-facing overview. `CLAUDE.md` is the short
version that loads on its own; read the sections here (and of README) that your task needs, not the whole file.

**The owner's to-do list is [`Docs/AfterReset.md`](Docs/AfterReset.md)**: bugs, polish, plans and feature ideas
they've asked to have done next, with the design docs they link to. Check it at the start of a session; tick items
off (or update them) as they're done.

---

## 1. The game in one page

- **What:** a 1-3 player **co-op top-down bullet hell** made in **Unity 6** (`6000.6.7f1`, Unity 6.6; built-in render
  pipeline). The owner plays it with a friend over **Tailscale** (one hosts, the other joins by IP).
- **A run:** 50 waves, a boss every 5 (Warden, Hive Queen, Twin Reapers, Singularity, Colossus, Duelist,
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
- **Divergent paths:** after each boss the host picks one of two **paths** (or no path) for the four regular
  waves before the next boss: enemy mixes, hazard floors, twists and arena rules (9 paths, danger 1-3 with a
  bonus and a prize). Bosses are always standard. See `Docs/Paths.md`.
- **Dev tools (F1):** a tabbed menu (Match / Players / Tools / DPS): stat editor, god mode, wave skip, session
  recording toggle, **DPS meter (F2) and target dummies**, **Boss Lab** (bots that play like people fight any boss; live stats; also a CLI batch mode).
- **Field guide (a button in the pause menu and in Settings):** in-game **Bestiary** and **Armory** databases
  (`FieldGuide.cs`): searchable lists, live models, stats, links between entries, and a link to the published web
  page.
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
Docs/                  sources of the Armory and Bestiary reference pages            [added in the audit]
```

### Core (logic) — `Game` is one big `partial class` split by feature

| File | Lines | What it holds |
|---|---|---|
| `Game.cs` | 1.2k | match state and engine constants (arena size, snapshot rate), lifecycle (`HostStart`, `Stop`), upgrades and picks (`Recalc`, `HostPick`, `NextOffer`), the frame loop (`FixedStep`, `Update`), `LocalControl` (local movement, dash, bow, boss shove), `HostTick`, `TickPlayers` |
| `Entities.cs` | 0.39k | the data types the match holds: `MatchState`, `FxKind`, `Sfx`, `SoundCue`, `AbilitySlot`, `Player`, `Enemy`, `Bullet`, `Orb`, `Zone`, `Minion`, `Effect`, `PlayerInput` |
| `GameNet.cs` | 0.55k | **all network serialisation**: inputs (`OnInput`, `OnAbility`), the snapshot (`SyncSnapshot` and its `SyncPlayer`/`SyncEnemy`/`SyncMinion`/`SyncZone`/`SyncOrb`; `SyncMastery`, `SyncShop`, `SyncWarden`, `SyncHazards`, `SyncCages` live with their systems), `OnHostMessage`, `SendSteer`/`ReadSteer`, `BroadcastFx` |
| `NetSync.cs` | 0.03k | one pass that writes (host) or reads (client) each field by `ref`, so a snapshot field is listed once for both sides |
| `Combat.cs` | 0.8k | bullets (`SpawnBullet`, `MoveBullets`, homing `Steer`, gravity wells), `Collide`, damage to players (`Damage`, `RevivePlayer`) and enemies (`HurtEnemy`), on-hit/on-kill effects, burn, orbs |
| `Weapons.cs` | 0.37k | `FireRate`, `FirePlayer`, `FireVolley`, `ExtraShots`, Revolver and Mender's Bow, `WeaponRange`, `SetFrags` |
| `Abilities.cs` | 0.2k | `HostAbility`, `CastAbility` (Soldier abilities; kits, Knight and ultimates have their own files) |
| `Waves.cs` | 0.12k | `TickWaves`, `WaveCleared`, `CheckGameOver` |
| `WaveReady.cs` | 0.25k | the between-waves ready system and the wave's numbers: `Player.Ready` / `AutoReady` (replicated by `Msg.WaveReady`, client -> host `SetMyReady` / `SetMyAutoReady`; host -> all `BroadcastReady`), `TickReadyTimer` (called from the break's countdown in `Waves.cs`: everyone on auto-ready = the usual 4 s, everyone ready = `ReadyCountdown` 3 s, else it waits; not at the opening countdown, in Practice or after a dev jump), `MarkWaveStart` / `SummariseWave` (per-wave kills, damage, damage taken from `Run` baselines, `WaveAward` flags, sent once as `Msg.WaveSummary`, read into `Game.WaveReport`). `Game/WaveClearedUi.cs` draws the box (one CRT tube: look A of `Docs/Mockups/crt-wave-cleared.html`; the older card was look 1 of `Docs/Mockups/wave-cleared-ready.html`) and keeps the saved Auto-ready choice (`PlayerPrefs auto_ready`, off by default) sent to the host; key `KeyAction.ReadyUp` (Y). `Game.WavePool(n)` is the enemy-type list `BuildWave` draws from, shared with the box's "next wave" panel. Test: `run.sh 1 30 waveready`. Preview: `WAVECLEARED=1 shot.sh x hud 4 12.2 3` (`=auto`, `=boss`; `WCT=<s>` for the drop-in) |
| `CloneWall.cs` | 0.1k | Shadow Clone as a wall: `SpawnCloneWall` (9 `MinionKind.CloneWall` 80 px ahead, 32 px apart, `Goal` = its slot in the row), `TickCloneWalls` (host: players shove clones, clones push each other, a spring keeps the row's shape). Every shot wears a wall clone (`BlockedBySummons`, unlike other blockers, which only boss shots wear). Thousand Clones still uses the old decoy `MinionKind.Clone` that bursts |
| `Ninja.cs` | 0.45k | the Ninja: `FireNinjaWeapon`, `NinjaAbility`, Substitution's `MovePlayer` (`Msg.Teleport`), Shadow Step's landing, clones and Vanish (`NinjaTarget`), Smoke Bomb's slowed bullets, `TickNinjas` |
| `BoStaff.cs` | 0.1k | the Bo Staff's shove: `HostBoShove` (from `DashShove` over `Msg.WeaponDash`, aimed at the cursor it was pressed at; checks `Player.ShoveCd`, starts the chain over, broadcasts `FxKind.StaffShove`) and `TickBoShoves` (lands it `BoShoveWind` later: damage and a straight push to everything in the bar). The slash, jab, spin chain itself is in `FireNinjaWeapon` (`Ninja.cs`; `Player.BoStep` is replicated, there is no timer); look in `Game/BoStaffLook.cs` |
| `Katana.cs` | 0.2k | the Katana's chain timers and queue (`TickKatana`, `TickKatanaFire`), the strike meter (`KatanaGain`), the Alt strike: your own aim and release (`LocalKatana`, `DashKatana` over `Msg.WeaponDash`) and the host's spend, untouchable window and landing (`HostKatanaStrike`, `TickKatanaStrikes`); `Game/KatanaLook.cs` draws the meter arc, the aim overlay and `FxKind.KatanaStrike`; design `Docs/KatanaCombo.md` |
| `BloodMage.cs` | 0.55k | the Hemomancer (`Kit.Hemomancer`): `FireBloodWeapon`, Implosion (`ImplodeTarget` picks the enemy under `Player.AimAt`, `ImplodeReady` holds fire without one, `pendingImplosions`/`ResolveImplosions` from `Collide`, `FxKind.Implode`), `BloodShotHit` (leech, pools, orb drops), health costs (`PayHealth`, `PayAbilityCost` from `HostAbility`, `castPaid`), Blood Price (`BloodDamageMult` in `HurtEnemy`), `BloodLifesteal`, `BloodAbility`, Crimson Pact hooks in `Damage` (`BloodCut`, `BloodTookHit`), `TickBloodMages` (Pact burst; Blood Debt, Arterial Spray and Vein Rush have their own files) |
| `BloodShell.cs` | 0.1k | the Hemomancer tank's Blood Shell, a toggle: `RaiseShell`, `ShellRelease` (pressing again drops it, handled in `HostAbility` and the local press loop like Last Stand), `TickBloodShell` (bleeds `ShellDrain` a second; ends at 1/3 health or when downed), `EndShell` (burst shoves enemies; mastery erases bullets), `ShellHit` (called from `Combat.cs` for each bullet stopped: `PayHealth` 7, `Heal` 4 to each ally inside), `KeepOutOfShell`. `Player.ShellOn` is replicated (protocol 201 removed the hold flag from `Msg.Input`). `TickSlots` pauses its cooldown while it's up |
| `TextOverrides.cs` | 0.15k | the dev text editor's edits: `Apply` (what to show; digit runs are "#" in keys, so numbers stay the game's), `Problem` (an edit must keep its numbers), `TextOverrides.txt` load/save |
| `Showcase.cs` | 0.2k | the lobby's loadout preview: `Showcase` runs a private offline `Game` (`Session.HostOffline`, `NetPeer.Offline`) with 3 target dummies and, for abilities that act on teammates (`Showcase.TargetsAlly`, read from the ability's bot rule), two stand-in teammates that exist only in that private game: they're hurt (and one downed for revives) shortly before each demo cast and the cursor goes on them; `Game.ShowcaseMode` hooks skip waves, freeze enemies, and block XP/mastery |
| `Difficulty.cs` | 0.25k | the run's difficulty: presets (Easy / Normal / Hard / Nightmare) and Custom, every knob's range and unit, `Game.Diff` / `D(DiffKnob)`, `EnemyDamageScale`; the lobby tab is `DifficultyUi.cs` |
| `Balance.cs` | 0.07k | **the general balance numbers** (waves, rerolls, big health orbs, revive range, contact damage, the Healer's aura, Soldier and card-only abilities, Mage spell range), and an index of where every other system's numbers live |
| `Swordsman.cs` | 0.2k | Swordsman and Shield Swordsman (non-shooters, waves 7 and 10): `SwordsmanMove`, `SwordsmanTurn`, `SwordsmanSpecial` (phases 0 close in, 1 wind-up, 2 cut, 3 recover; the cut hits everyone in `SwordCovers` once), `SwordGuardAt`/`SwordShieldBlocks` (the shield, from `BlocksShot`), `SwordShieldStruck` (block flash in `T2`); PhaseT and T2 reach clients through `SyncNonShooter` |
| `NonShooters.cs` | 0.4k | Fuse Runner, Hookman, Sapper and its Tripwire Pods (`SapperMine`), wave 15-18: `NonShooterMove`, `NonShooterSpecial`, `NonShooterDeath` (blasts that hurt players and enemies), `HookIsOpen` (+50%), `SyncNonShooter` (PhaseT and T2 in the snapshot); drawn in `Game/NonShooterLooks.cs`; test `nonshooters`; preview `NONSHOOT=1 Tools/Preview/shot.sh x hud` |
| `WaveRecipe.cs` | 0.2k | the late wave recipe (wave 13+): `EnemyJob`, `JobShares`, `RecipeTypes` (each type's wave and job), headliners, `RecipeWeight` (fresh types up, old ones retiring), `BuildRecipeSingles` after `BuildSquads` (which takes half the wave on a recipe wave); `Docs/EnemyRoles.md`, report `wavemix` |
| `MeleeGrunts.cs` | 0.1k | Buzzsaw and Spike Block (wave 5+), the Spiker's zig-zag (`SpikerMove`; the plain white Swarmer has no AI of its own, it shares the Brute's straight run), the Spike Block's wind-up / lunge / rest (`SpikeBlockSpecial`); drawn in `Game/WorldEnemies.cs` (`DrawFaceSpikes`, `DrawSpikeBlockTell`) |
| `MeleeSounds.cs` | 0.2k | Cues for Swarmer, Spiker, Brute, Lunger, Buzzsaw, Spike Block, made each frame in `UpdateMeleeSounds` (from `Update`) from replicated type, position and Phase, so nothing is sent: bites, steps, flips, tells, leaps, rests; a death when an enemy disappears. `QuietAt(pos)` sets `SoundCue.Quiet` by distance from you (`EarNear`/`EarFar`/`EarFloor`); enemy shots use it too. Same-kind deaths within `DeathWindow` make one sound. The Buzzsaw has no tick: `SawWhirrLevel` drives a loop. Recipes and the loop voice: `Game/MeleeSoundBank.cs`. Mockup: `Docs/Mockups/melee-enemy-sounds.html`. Test: `run.sh 1 1 meleesounds` |
| `EnemyAI.cs` | 1.0k | enemy roster per wave, movement, firing patterns, contact damage, boss specials, bullet budget |
| `LateGame.cs` | 0.45k | bosses 25-50 (Colossus → Eclipse; most of each now lives in its own file) |
| `Eclipse.cs` | 0.6k | wave-50 final boss in five stages (Corona / Penumbra / Umbra / Echoes / Last Light + Totality): `EclipseState` (synced by `SyncEclipse`), the moon and its shadow, flares, closing rings, sunspots and the dark, crescents, the echoes, the burning edge, the nova. Look: `EclipseLook.cs` |
| `Warden.cs` | 0.7k | wave-5 boss, a prison riot in three stages (Count / Lockdown / Riot + last stand): turn tables, searchlight + stun + shackle ring, gates of push bullets (`WardenGatePush` runs on each player's own game), tear gas (a `PathPatch`), rioters, riot shield, baton charge; `WriteWarden`/`ReadWarden` after the boss's snapshot record |
| `Reapers.cs` | 0.7k | wave-15 Twin Reapers in three stages (Twilight / Dual Reaper / Sundered) on one `ReaperFight` (game-level, `SyncReapers` in the snapshot): tether sweep that follows its target, the fusion (twins removed, `EnemyType.DualReaper` holds their health; power-up blasts) and the split, twin lasers, `EnemyType.ReaperPillar` (each killed leaves a hole), the suck-in (the Singularity's `PullStrength`), the chase and trap (shield, then open; `DualReaperHit` armor), reaps; drawn in `ReaperLook.cs` |
| `Queen.cs` | 0.6k | wave-10 boss, a hive in three stages (Swarm / Honeycomb / Frenzy + last stand): drones (shield; leftovers fly home and heal), bombers, eggs (`EnemyType.HiveEgg`), hex cells (`HiveCells`, same on every game), nectar spray (`Defs.HoneyShot`, `PatchKind.Honey` slows on your own game), swarm cloud (bee positions computed from the cloud and the clock on every game), royal dive (immune in the air), bomber barrage; `WriteQueen`/`ReadQueen` after her snapshot record; drawn in `QueenLook.cs` |
| `Singularity.cs` | 0.25k | wave-20 boss: corner wells, lasers, cages |
| `Colossus.cs` | 0.6k | wave-25 boss in three stages (Awakening / Crumbling / Molten Core + last stand): `ColossusState` (synced by `SyncColossus`), stomp rings of push blocks (`StompRing`, pushed on your own game in `ColossusPush`, hurt on the host), drops (boulders, meteors, pillars), fissures, plates (`ColSlab`, stop bullets both ways in `Collide` via `ColossusCoverStops`), charge + daze (`ColDazeMult` in `HurtEnemy`), lava patches (`PatchKind.Lava`). Look: `ColossusLook.cs` |
| `LumenWard.cs` | 0.2k | The Healer Elementalist's Lumen Ward (`AbilityId.Sanctify`, renamed Nov 2026): `CastLumenWard` makes a `ZoneKind.LumenWard` zone (Pos = apex, Radius = length, `Aim` replicated); `LumenShape`/`InLumen` give the fanning cone; `TickLumen` heals inside, burns enemy bullets in it, and (mastered) freezes enemies outside; `LumenSpeed` (called by `MoveBullets` on every peer) builds `Bullet.Stasis` outside the light once the stopping wave reaches a bullet. |
| `Minions.cs` | 0.93k | Elementalist and Summoner abilities (`KitAbility`), minions (`SpawnMinion`, `TickMinions`) and ground zones (`AddZone`, `TickZones`) |
| `KitWeapons.cs` | 0.93k | Elementalist and Summoner weapons (`FireKitWeapon`) and what they leave behind: bleeds, chain lightning, seeds and vines, sent minions, orbiters, refraction, ion trails, the wolf pack, arc fields, light cones, bone cages, boulders, shocks |
| `LastStand.cs` | 0.1k | The Tank Knight's Last Stand: `PlantStand`, `TickStand` (the barrier turns toward your aim at `StandTurn`; ends when you go down or the fight stops), `EndStand` (starts the cooldown; `TickSlots` holds it while planted), `StandRelease` (pressing it again cancels, handled in `HostAbility` and the local press loop in `Game.cs`), `StandBlocks`/`StandHit` (the arc test and the 10% hit, in `Combat.cs`'s bullet loop next to the Blood Shell). `Player.StandOn`/`StandAim` are replicated (protocol 177); local movement is rooted next to the Warden stun in `Game.cs`; `MovesYou` lists the abilities it blocks. Look: `Game/LastStandLook.cs` (plates, stakes, `FxKind.StandHit` sparks) | |
| `HalberdMotion.cs` / `Game/HalberdLook.cs` | 0.2k / 0.4k | the Halberd's swing as one motion from the click (`HalberdMotion.At`, `CrossTime`, `StepTime`: Knight.cs times its six steps from it) and its Cleaver look (held in two hands, `DrawHalberd`, `DrawHalberdFx` from `DrawSlashLayer`, `DrawHalberdIdle` from the player loop) |
| `TwinBlades.cs` / `TwinBladesMotion.cs` / `Game/TwinBladesLook.cs` | 0.2k / 0.2k / 0.3k | the Twin Blades' Crescent Waltz: `FireTwinBlades` (the chain step in `Player.Combo`'s low 3 bits, a step count above them; `ComboT` until it starts over), `TickTwinBlades`, `HostTwinFang` (Msg.WeaponDash `DashStrike`) and its `FangRun`s, `LocalTwinStep` (your game steps you forward when the count changes); `TwinBladesMotion.At` (key poses per hand) and `FangPos`; the look draws both arms and sabres (the model leaves its own out), `FxKind.TwinStep` / `TwinFang` from their age, and the five pips; `Game/TwinSoundBank.cs` its ten sounds; `TwinHitCue` sends `FxKind.TwinHit` for `Juice.cs` `TwinHitFeel` (hit-pause and shake on your own hits) |
| `Lightbringer.cs` / `LightbringerMotion.cs` / `Game/LightbringerLook.cs` | 0.3k / 0.1k / 0.4k | the Lightbringer's Dawn, Mercy, Noon: `FireLightbringer` (step in `Player.Combo` like the Twin Blades; `TickTwinBlades` forgets it), `LightLanded` (called from `LandSwing` via `Swing.LightKind`: gauge, crescents with `Bullet.HealAlly`/`WardAlly`, Noon's ring heal, Radiant ward), `Ward` (`ShieldHp`/`ShieldT` plus the replicated `Player.MercyT`), `HostSunlance` (Msg.WeaponDash `DashLance`), `LanceRun`s landed by `TickLightbringer`, `LocalLightStep`/`LocalSunlance`; motion keys reuse `TwinBladesMotion.Step`/`At` (`HasHome` settles to the Lightbringer's guard); the look draws arms and longsword (idle, steps, lance), Noon's ring, the lance beam, three pips, the cooldown ring and Mercy's hexagon; its sounds are `Game/LightSoundBank.cs`; its light (Sundial, Sunrise crescents, Spear, Hex plates, `FxKind.LightMark`) is `Game/LightbringerFxLook.cs`, from `Docs/Mockups/lightbringer-looks.html`, drawn in the glow pass (`DrawLightGlow`); mockup `Docs/Mockups/lightbringer-rework.html`; preview `KNIGHT=1 SWINGW=Lightbringer SWING=0.2 LIGHTSTEP=1..4\|lance [LIGHT=n] [WARD=1] Tools/Preview/shot.sh x hud 6 12.2 2`; tests `run.sh 1 30 lightbringer` |
| `Lance.cs` / `LanceMotion.cs` / `Game/LanceLook.cs` / `Game/LanceSoundBank.cs` | 0.3k / 0.1k / 0.4k / 0.1k | the Tank Knight's Lance, the Breach: `FireLance` (step in `Player.Combo` like the Twin Blades; `TickTwinBlades` forgets it; `Player.LancePrimed` from a hook makes the next step 4), jabs and the Breaching Thrust are `Swing` boxes, the Wide Sweep `LanceSweep` (eight `Swing.Sweep` cones timed where the motion has the lance; `Swing.LanceAcross` bats along the swing via `LanceKnockDir` in `LandSwing`); `Swing.LanceKind` arms Impact (`Enemy.ImpactBy`; `Knock.cs` `TickKnock` calls `LanceBump`/`LanceWallImpact` while 34 px or more of the push is left), sends a `FxKind.LanceSpark` per enemy and a `LanceHitCue`; `HostLanceHook` (Msg.WeaponDash `DashHook`), `HookRun`s landed by `TickLance`, `LocalLanceStep` (a delayed step forward per `LanceMotion.MoveFrom/MoveTo`) and `LocalLanceHook`; motion keys reuse `TwinBladesMotion.Step`/`At` (`HookSlide`: the lance out through the glove); the look draws arms, heater shield and the Winged War Lance (idle, steps, hook), the trail, the jab wedge and the breach box, sparks, Impact bursts and wall cracks, four pips and the cooldown ring; the Impact number and tag are `Juice.cs` `ImpactNumber`; mockup `Docs/Mockups/lance-rework.html`; preview `KNIGHT=1 SWINGW=Lance SWING=0.3 LANCESTEP=0..4\|hook [PRIMED=1] Tools/Preview/shot.sh x hud`; sounds `Tools/SoundCheck/render.sh Lance`; tests `run.sh 1 30 lance` |
| `Rapier.cs` | 0.1k | the Rapier's Tempo and Flourish (DPS Knight): `RapierGainTempo` (from `LandSwing`, one stack per enemy hit, none for skewered bullets), `RapierCooldown` (from `FireKnightWeapon`: `RapierSlow` to `RapierFast` by Tempo, over `FireRate`, floor `RapierMinInterval`), `TickRapier` (grace then drain; the Flourish's swings), `HostRapierFlourish` (Msg.WeaponDash `DashFlourish`), `RapierFlourishWave` (an alternating `FxKind.Slash` sweep and a piercing `KnightWave`); `Player.RapierTempo` and `RapierGraceLeft` are replicated; mockup `Docs/Mockups/rapier-tempo.html`; preview `KNIGHT=1 SWINGW=Rapier SWING=0.05 TEMPO=0..20 [GRACE=0..1] [FLOURISH=1] Tools/Preview/shot.sh x hud 6 12.2 2`; tests `run.sh 1 30 lastfour` |
| `Censer.cs` / `CenserMotion.cs` / `Game/CenserLook.cs` | 0.2k / 0.2k / 0.4k | the Censer's Thurible Rite (Healer Knight): `FireCenser` (Sweep, Return Sweep, Benediction; the step in `Player.Combo`'s low bits as the Twin Blades keep theirs, `ComboT` until it starts over), `CenserLanded` (from the swing resolution in `Knight.cs`: Embers, the Benediction's +1 heal per enemy hit, the step's incense), `TickCenser`, `HostCenserCast` (Msg.WeaponDash `DashCast`) and its `CastRun`s (`CastLanded`: burst per Ember, a healing incense cloud via `Zone.HealMult`, `Zone.ExtraRevive` and a `CastReviveKick` for downed allies in it); incense only comes from the swings and the cast (the old walking trail is gone); its sounds are `Game/CenserSoundBank.cs` (hear them: `Tools/SoundCheck/render.sh Censer`); `Player.Embers` is replicated; `CenserMotion.At` (head angle and distance, body turn, the blessing hand), `Head`, `Grip`, `CastHead`; the look draws both hands on the haft as the Halberd is held (the model leaves its arms out), a simulated chain (`CenserRig`), the head and its smoke, the trail, heal motes, the cast, three pips and the Ember flames |
| `Greatsword.cs` / `Game/GreatswordLook.cs` | 0.2k / 0.2k | the Greatsword's three-hit chain timers, Alt guard (`HostGreatGuard`, `GreatGuardBlock`, `TickGreat`) and Rend, and its look (`DrawGreatPips`, `DrawGreatWindup`, `DrawGreatGuardPose`, `DrawGreatGuard`, `DrawGreatRendFx`; sounds in `Game/GreatswordSoundBank.cs`) |
| `Greatsword.cs` / `Game/GreatswordLook.cs` | 0.2k / 0.2k | the Greatsword's three-hit chain timers, Alt guard (`HostGreatGuard`, `GreatGuardBlock`, `TickGreat`) and Rend, and its look (`DrawGreatPips`, `DrawGreatWindup`, `DrawGreatGuard`, `DrawGreatRendFx`; sounds in `Game/GreatswordSoundBank.cs`) |
| `Kusarigama.cs` / `Game/KusarigamaLook.cs` | 0.3k / 0.1k | the Kusarigama (Docs/Mockups/kusarigama-combo.html): the primary is a sickle cut (a swing) then a weight throw (`FireKusarigama`, `StartKusariThrow`; `HookId`/`HookT`/`KusariAim` replicate the throw and `TickKusariThrow` lands it at `KusariImpactAt` and drags the target in); the weapon-action key whirls (`HostKusariWhirl` on `Msg.WeaponDash` `DashWhirlOn`/`DashWhirlOff`, `TickKusariWhirl`: pull within `KusariPull`, the chain's sweep hits, `EndKusariWhirl` snaps). The old hook and reel are gone. Protocol 206 |
| `StormHammer.cs` / `Game/StormHammerLook.cs` | 0.2k / 0.3k | the Storm Hammer's delayed throw (`QueueHammerThrow`, `TickHammerThrows`) and Alt slam (`HostHammerSlam`, `TickHammerSlams`, `DashHammerSlam`); `FxKind.HammerThrow` (the held hammer's pose clock) and `FxKind.HammerSlam` (the wave); looks: `DrawStormHammerHeld`, `DrawStormHammerThunder`, `DrawHammerSlamFx`, `DrawHammerGlow`; mockups `Docs/Mockups/storm-hammer-throw.html`, `storm-hammer-slam.html`; preview `HAMMERFX=throw:0.1\|slam:0.45\|wave:0.25`; tests `run.sh 1 30 storm`, `stormslam`. Protocol 207 |
| `Quarterstaff.cs` / `Game/QuarterstaffLook.cs` | 0.1k / 0.2k | the Quarterstaff's Jab stacks and Alt jab (`HostStaffJab`, `TickStaff`, `StaffCap`) and its Beacon look (`DrawQuarterstaffModel` from `DrawMeleeModel`, `DrawStaffPips`, `StaffJabCone`); mockup `Docs/Mockups/quarterstaff-rework.html`; preview `KNIGHT=1 SWING=0.1 SWINGW=HolyMace [STAFFJAB=1] [STACKS=n]`; tests `run.sh 1 30 quarterstaff` |
| `Knight.cs` | 0.6k | Knight melee: swings (`Swing`, `LandSwing`, Lance box), sword waves, Knight abilities |
| `GuardiansOath.cs` | 0.1k | the Healer Knight's Guardian's Oath: `LocalOathCharge` (your own game dashes to the player nearest the cursor), `OathCast`/`TickOath` (host: grab, `KnockPlayer` pull, a downed ally carried with `MovePlayer`, then the `ZoneKind.OathWard` with `DamageCut` 0.5 and `ExtraRevive`); the Bulwark upgrade adds `OathCut` |
| `Ultimates.cs` | 0.15k | the 12 ultimates |
| `Mastery.cs` | 0.2k | ability XP/levels and capstone helpers (capstone texts are in each `AbilityDef`) |
| `Knock.cs` | 0.05k | pushes slide: `KnockEnemy(e, delta)` (every shove goes through it), `TickKnock` (from `TickEnemies`); a `FxKind.Knock` draws the streak and dust (`WorldEffects.DrawKnockFx`) |
| `Nimbus.cs` | 0.07k | the Nimbus (Healer Elementalist; replaced the Prism Beam): the cloud is a companion `MinionKind.Nimbus` steered to your cursor within `NimbusReach` of you (`NimbusGoal`, in the minion move switch); `TickNimbus` heals allies in its 70 px rain; a click is `NimbusStrike` (a 40 px blast under the cloud, `FxKind.Strike`; extra projectiles = more strikes). Looks: `NimbusLook.cs` |
| `Sanctum.cs` | 0.04k | the Sanctum ultimate's healing pulses (`SanctumPulse`, from `TickZones`): 7 rings from the dome's centre, one every 0.9 s, 20 HP each and 30 for the last, healing each ally as the ring reaches them. Look: `SanctumLook.cs` |
| `HallowedBlade.cs` | 0.05k | the Hallowed Blade ultimate after it lands: a planted `ZoneKind.HallowedBlade` (ring 15 HP/s heal, 20/s burn, 8 s) and 4 arena-wide pulses (every 2 s, 10 heal / 13 damage each, ring reaches each target in turn); `HallowedPulse` from `TickZones`; look in `Game/HallowedBladeLook.cs` |
| `LumenWardLook.cs` | 0.2k | Lumen Ward's cone of light with Visual effects on (look A, Cathedral, of `Docs/Mockups/sanctify-cone.html`): `DrawLumenWard` (glow bands, core, god rays, dust, edges, sunburst) from `Game.LumenShape`, and `DrawFrozenShots` (a gold halo on bullets with `Stasis`). Off: a plain wedge; Reduced: no rays or dust. |
| `DeathLotusLook.cs` | 0.2k | The Ninja's Death Lotus (look D, Crimson Reaping, of `Docs/Mockups/death-lotus-looks.html`): `DrawDeathLotusFx` (from `DrawEffect`'s `FxKind.DeathLotus` case). The cast sends one effect with `Pos2.X = -1` (darkens the arena, crimson glow on the Ninja for 1 s; `Juice.cs` kicks the camera) and one per enemy struck (`Pos2.X` = seconds until its cuts land, 0.11 s apart, 0.07 s at the capstone; three cuts, then a lotus stain for about 1.7 s). Damage still lands on the cast tick. Effects off: plain crossing cuts only. Protocol 161. |
| `LifelineLook.cs` | 0.1k | The Healer Soldier's Lifeline with Visual effects on (look E, Heartbeat Halo, of `Docs/Mockups/lifeline.html`): `DrawLifelineFx` (from `DrawEffect`'s `FxKind.Lifeline` case; one effect per player reached, `Pos2` = (their id, the caster's id), `Radius` = their distance, lasting `Game.LifelineFxTime`) draws from the effect's age: the caster's own effect draws the lub-dub pulse (330 px/s), each player gets a halo dropping in as it lands and a column of light for the 3 s (blinking the last 0.6 s), then everything fades over 0.7 s. `IsLifelinePlain` hides the old ring and beam (marked by their durations) while Visual effects are on. Protocol 153. |
| `Tsunami.cs` | 0.13k | Tsunami (Elementalist DPS; took Arcane Surge's place and number): `CastTsunami` launches a `SeaWave` (host logic, moved by `TickTsunami`: 340 px wide, 520 px/s, 1000 px, 12 damage once per enemy) and broadcasts one `FxKind.Tsunami` that clients animate. Crossed enemies get `Enemy.WetT` (8 s, 14 mastered; replicated as `Wet`): `TickWet` slows them 30% (15% heavy). Every lightning source calls `WetArcs(at, struck, dmg, owner)`: a bolt to every other wet enemy for 40% of the hit, at most once per 0.4 s each (`WetArcCd`). Hooked in: `ArcField`, `ShockPulse` (counts as half a hammer hit), `LightCone`, `NimbusStrike`. A new lightning weapon needs one `WetArcs` call. Mastered: a second wave rolls back (wets, no damage). Looks: `ElementalistLooks.cs` (also Meteor's falling rock and `FxKind.Meteor` impact, and Blizzard's whiteout) |
| `Chalk.cs` | 0.28k | Spirit Chalk (Healer Summoner; replaced the Totem Staff; ink lasts 10 s and only blocks bullets with the Warding Ink card): hold = `DrawChalk` lays `InkDot`s along the tip (700 px/s, 600 px of ink; at the cap `EraseOldest` fades your oldest dot to make room, and breaks its loop if it was in one); `InkTouches` burns/slows enemies, heals players and destroys enemy bullets crossing ink (wearing it); `TryClose` finds a loop in a stroke and seals it as an `InkPen` (enemies inside get `PenId`; bosses and mini-bosses excluded); `EnforcePens` keeps them inside; `SyncInk` sends the dots and each drawer's live tip in each snapshot (`InkTip`; `UpdateInkView` eases `InkTipDisp` and ages a client's ink between snapshots; `Enemy.Penned` is replicated). Looks: `ChalkLook.cs` |
| `DpsTools.cs` | 0.15k | the DPS meter's data (`DpsTrack`: a 10 s ring of 0.1 s slices; `TickDps` reads each player's `Run.DamageDealt` gain per tick, `DotDamage` reports the over-time share) and target dummies (`Enemy.Dummy`: a pinned Juggernaut refilled in `TickDummy`; `Foes()` is the enemy count that ignores them, used by the wave-clear checks). Host only, no network |
| `Parry.cs`, `Pause.cs`, `DevTools.cs`, `StatSheet.cs`, `RunStats.cs`, `Kits.cs` | small | as named |
| `BossSounds.cs` | 0.1k | soft audio cues when bosses start attacks |
| `Paths.cs` | 0.46k | divergent paths: `PathDef` registry (`Paths.All`), offer/choice (`OfferPaths`, `ChoosePath`, `Msg.PathOffer`/`PathChosen`), enemy mixes (`BuildPathWave`, `ApplyPathToEnemy`), bonus/prize (`PathBonus`, `FinishPath`), hazard patches (`PathPatch`, `TickPaths`, `Msg.PathPatch`), arena rules (`ArenaRule`, `NarrowF`/`FogF`, the playable rect `ArenaMin`/`ArenaMax`) |
| `Layouts.cs` | 0.2k | the six arena layouts (`LayoutId`, `LayoutDef`, `Solid` walls and pillars, `EdgeSpan` player and enemy gates, `Layouts.Get`/`Playable`, edge presets Centre/Twin/Sides, waypoints). Plan: `Docs/ArenaLayouts.md` |
| `LayoutRules.cs` | 0.3k | how layouts play: `PickLayout` (host, from `Waves.cs`: random per boss cycle, open on boss waves), `PlaceLayoutCrates` (from `WaveProps`), `SyncLayout` (one byte in the snapshot), `PushOutOfSolids` (called from `PushOutOfProps`; the edge wall only pushes inward), `SolidStops` (from `Collide`), `LayoutSpawn`/`LayoutSquadBase`/`LayoutSideY` (enemies through the enemy gates), `LayoutDir` (steer round walls: shortest route through the wall corners, any number of them, from a corner graph built per layout and a per-target cost table cached for 0.2 s; `RouteTo`/`RouteVel` route an enemy to a spot of its own, used by the Warden's faction and squad `GoalVel`). Look: `Game/LayoutLook.cs`. Tests: `layouts`; `Game.LayoutsByDefault` is off in the runner. Preview: `LAYOUT=<1-6> Tools/Preview/shot.sh <name> hud` |
| `Props.cs` | 0.5k | the arena's props: crates (cover), pots, barrels, the treasure chest and each player's private item; spawning by wave, bullet stops, blasts, push-out, the wave waiting for treasure, `Msg.Loot`, sync (look: `Game/PropsLook.cs`: with Visual effects on, crates and pots are look A "Hologram" of `Docs/Mockups/digital-props.html` (`DrawCrateHolo`, `DrawPotHolo`, `DrawBarrelHolo`, the health barrel and `DrawChestHolo`, smash in `DrawHoloBreakFx` from the Debris effect; a pot's effect lasts `PotFxTime` 1.3 s); off keeps wood and clay; Reduced drops scanlines, flicker and the pot's motion) |
| `Shop.cs` | 0.3k | the Bazaar: gold per kill (`GrantGold`, `GoldPerXp`), `OpenShop` / per-player shelf (`RollShelf`), `ShopAction`s (`Shop` → `Msg.Shop` → `HostShop`), giving gold (`Msg.ShopGift`, notices), `CloseShop` (leftover gold → XP), revive token, `BotShop`, snapshot fields |
| `Modifiers.cs` | 0.07k | `RunMod` flags and `ModDef`s; `Game.Mods` / `Has()`; sent in the lobby message |
| `Practice.cs` | 0.2k | Practice Range plan (`PracticePlan`/`PracticeWave`), text format, the picker list (`Choices`, built from each `EnemyDef.Picker`), building a practice wave |
| `PartyBots.cs` | 0.1k | bots in the lobby's open slots: each is a `Session` joined to the host over localhost with `Game.Bot` on; readies itself; marks the host game `Unrecorded` |
| `BotBrain.cs` | 0.4k | the "smart" bot (Boss Lab): dodging, positioning, aiming, ability use (`BotSense`; each ability's rule is its `AbilityDef.Bot`) |
| `BossLab.cs` | 0.25k | Boss Lab sessions and stats |
| `Classes.cs` | 0.08k | the class registry: `KitDef` (name, blurb, icon, `HpMult`/`SpeedMult`, hooks `Fire`, `Ability`, `Tick`, `TickEnemy`, `OnShotHit`, `OnKill`), `Classes.All` (Kit order, checked at start-up) and the gathered hook lists the game loop runs |
| `Approaches.cs` | 0.15k | boss approaches: `FactionDef` per boss, `ApproachShare`, `BuildApproach`/`MixApproach` (see `Docs/Approaches.md`) |
| `WardenFaction.cs` | 0.3k | the Warden's faction: Riot Shield (shield, bash, `Msg.Knock` knockback, the searchlight pair, `HerdSpot`) and Gas Grenadier |
| `Defs.cs` | 0.3k | role, weapon, enemy and bullet definitions (each class's `WeaponDef`s live in its own file; `Defs.Weapons` gathers them) (stats, colours; each enemy's `Blurb`, `Picker` group, `Late` and `Heavy` flags) |
| `BestiaryData.cs` | 0.1k | the in-game Bestiary's words (`BestiaryEntry`, `BestiaryAttack`); most come from `BestiaryData.Generated.cs`, built from the Bestiary page by `Docs/Bestiary/build_data.js` |
| `Upgrades.cs` | 0.4k | upgrade cards, `AbilityDef` and the ability registry (an ultimate's `UltAlt` 1 makes it its class's second, key 1 of the lobby's Ultimate row: `Upgrades.Ultimate(role, kit, pick)`, `UltCount`, `ClampUlt`) (`Upgrades.Abilities`, gathered from each class's file; the lobby tables `RoleAbility`/`Ultimate` are built from the entries), per-weapon/Knight descriptions |
| `Pings.cs` | 0.15k | pings: `PingKind` (Here, Focus, Danger, Help, Loot, Go), `Session.Pings`, `SendPing` / `HostPing` (host-checked pause 0.4 s, 3 live each, finite position, Focus snaps to the nearest enemy and follows it via `PingNow`), `Msg.PingMark` (protocol 145); `Game/PingLooks.cs` is the tap / hold-wheel input (`TickPing`, Ping key = middle mouse) and the drawing (`DrawMatchPings`, `DrawPingWheel`) |
| `LobbyAvatars.cs` | 0.2k | the lobby Hangout room: `Avatar`, room size (2400 x 1600), `MoveMyAvatar`/`StepAvatars`, `Msg.LobbyMove`/`LobbyPos` (a partial of `Session`; `Docs/Hangout.md`) |
| `LobbyCombat.cs` | 0.2k | Hangout PvP: `FireMyShot`, `CastMyAbility` (Dash / Barrier / Burst heal by role), `SetMyPvp`, `StepLobbyCombat` (called from `Poll`), `ShotStats`, `Msg.LobbyFire`/`LobbyShot`/`LobbyAbility`; the host decides hits (a partial of `Session`) |
| `Session.cs` | 0.7k | hosting/joining, lobby, ready state, **message enum** (`Msg`), protocol version, practice sync; each `LobbyEntry`'s ultimate pick (`Ult`: 0 the default, 1 the second, clamped by `Upgrades.ClampUlt`; `Msg.UltRequest`, also in `Hello`, the lobby and start messages and saved seats) |
| `Net.cs` | 0.26k | `NetPeer`: length-prefixed messages over TCP (`NoDelay`) |
| `SessionRecorder.cs` | 0.5k | the session JSON recorder |
| `RunTimeline.cs` | 0.3k | Recent runs data. Every game samples the run each 10 s of play (`Game.TickRunTimeline`, from `FixedStep`, only while Fighting or Intermission and for a counted run: not Practice, a Hunt, dev tools or the bot): each player's running `RunStats` totals plus HP%. `RunTimeline` turns samples into rates, health lines and per-wave accuracy and reads / writes a text file (`runlog 1`); `RunTimelineStore` keeps `Profile.Folder/runs/<id>.run` (newest `RecentKept` = 20). The End record (`RunSaves.cs`) closes the curve with a last sample, saves it and sets `RankLog.File` (plus class, mode, time, damage). No protocol change: totals come from `RunStatsLive` (parties) or locally, HP from snapshots. See Docs/RecentRuns.md |
| `Replay.cs` | 0.4k | run replays. `ReplayRecorder` (made in `HostStart` when `Game.ReplayFolder` is set) keeps every kept host message (`Session.Broadcast` -> `Replay.Capture`) with the frame it went out in, plus each frame's length and timeline marks, and saves a gzipped `.replay` on `Game.Stop`. `ReplayPlayer` owns a private offline `Session` (`Net == null`, so it's a client; `MyId = 0` so nobody is "you", `Game.Replaying` skips `LocalControl`, `Game.ViewId` is whose screen `Me` is) and feeds the messages to `Session.ReplayApply`. `Seek` always resets and replays: it applies lasting messages up to 360 frames before the target, only the newest Snapshot/RunStatsLive, skips Fx/Sound/Spawn/Remove, then steps the last 360 frames for real. No protocol change |
| `MathTypes.cs` | 0.08k | `V2`, `Rgba`, `MathX` |
| `Challenges.cs` | 0.85k | challenge rooms: the portal every five waves, Defend the Crystal, the Gatekeeper, Kill Rush, Zones, Mirror (shadows); the altar's prize |
| `Variety.cs` | 0.94k | the second half's enemies (Beam Sentry, Scorcher, Reaver, Warder), shooting-pattern enemies, elite traits, squads |
| `Readability.cs` | 0.11k | readable regular waves: shooters take turns (`AssignShooterTurns`), bullet looks sent with each spawn |
| `BuildPower.cs` | 0.1k | boss health that keeps up with strong builds (a build's power vs a typical build at that wave) |
| `Duelist.cs`, `Lich.cs`, `Herald.cs`, `Devourer.cs` | 0.64-0.82k each | the three-stage Duelist (wave 30), Lich (35), Storm Herald (40) and Devourer (45): stage, turns, synced state |
| `ForcePact.cs` | 0.2k | Force Pact (Tank Summoner, replaces Guardian Spirit): `ForcePactCast` takes the enemy nearest the cursor, `TickPact`/`TickThrall` run the thrall (a `MinionKind.Thrall` carrying its `EnemyType` in `Minion.Pact`); health and respawn reuse the Warden's Guard paths in `Stun.cs`; look in `Game/ForcePactLook.cs`; test suite `pact` |
| `Stun.cs`, `QueenLoot.cs`, `ReaperLoot.cs` | 0.26-0.28k each | boss legendaries: the Warden's (Stun Gun, Warden's Guard), the Queen's (Hive Drones, Royal Dive, Bomber Escort), the Reapers' (Twin Scythes, Final Reap, Scythe Throw) |
| `HolyCross.cs` | ~0.1k | the Healer Knight's Consecrate: a growing Holy Cross (heals + `SwiftT` speed for allies; damage, slow, knockback for enemies); `ZoneKind.Consecrate` is no longer cast (kept so numbers don't shift); look in `Game/HolyCrossLook.cs` |
| `Airstrike.cs` / `Game/AirstrikeLook.cs` | 0.15k / 0.2k | the Soldier DPS's Airstrike (`AbilityId.Airstrike`, the Cluster Grenade's old number; saves that say `Grenade` load as it, `RunSaves.cs` `OldAbilityName`): host-only `AirRun`s (`TickAirstrikes`): the lane across the aim through the flare, cut to the arena (`ClipToArena`), bombs timed by `AirBombTime` (each a grenade-coloured `FxKind.Boom`, so it gets the Frag Burst look and sound), the burning lane, the mastered second wall 160 px out; one `FxKind.Airstrike` per wall (Pos/Pos2 = the lane's ends, Radius = thrower id × 10000 + the flare's distance along it) drawn from its age; `Bigger Boom` widens the bombs (`Stats.GrenadeRadiusMult`); tests `run.sh 1 30 airstrike`. Protocol 217 |
| `TwinGrenades.cs` | 0.1k | the Twin Rifle's grenade tubes: `TwinRifleHit` (Combat.cs, after the crossfire check) loads `Player.TwinNades` (two at most) by `TwinCharge` (40, crossfire counting twice); Alt (`LocalWeaponDash` → `Msg.WeaponDash` `DashNade` → `HostTwinGrenade`) fires the tube `Player.TwinNext` names and flips it, so the order holds through a refill (`TwinTubeLoaded`: with one loaded it's in the next tube); the grenade flies from that tube's muzzle (`TwinTubeMuzzle`) to the crossing; the look is `Game/DpsGunLooks.cs` (`DrawTwinTube` under each rifle, the model sets `mHeldTube` per hand; `DrawTwinNadePips`); preview `TWINNADES=<loaded>,<next>,<charge>`; tests `run.sh 1 30 twinnades`. Protocol 217 |
| `CavalryCall.cs` | ~0.1k | the Tank Knight's Cavalry Call (`AbilityId.CavalryCall`, was Unbreakable): host-only lancer charges (`TickCavalry`), one `FxKind.CavalryCall` per cast drawn by `Game/CavalryCallLook.cs`; the 20% guard reuses `Player.UnbreakT` |
| `CaughtSteel.cs`, `Gauntlet.cs`, `Misericorde.cs`, `Drawbridge.cs` | ~0.5k | the DPS Knight's Space abilities and ultimate (keeping Battle Fury's, Lunge's, Whirlwind's and Blade Tempest's `AbilityId`s): Caught Steel catches enemy bullets that skim past (`SteelGraze`, called from the enemy-bullet loop in `Combat.cs`), `Player.Steel`/`SteelT` replicated, the second press (`SteelRelease`) or the time running out throws them as `Defs.SteelShard` bullets that cut enemy bullets (`TickSteel`), a hit drops a third (`SteelHurt` from `Damage`); the Gauntlet (`CastGauntlet`: the enemy nearest the cursor in the aim lane, `TickGauntlets`: hit, brace, charge carried by `Msg.Carry` with bowling, impact; `Enemy.ChallengeT`/`ChallengeBy` replicated; `GauntletKill` from `KnightKit.OnKill` resets the cooldown with `CutCooldown`); Misericorde (`MercyFirst`, also your own game's pre-check so a press with nobody wounded costs nothing, `TickMercy`: leaps carried by `Msg.Carry`, executions, a boss stab ends it); the Drawbridge (`ZoneKind.Drawbridge` whose age is its phase: `TickDrawbridge` slams at 1.05 s, `KeepOffBridges` after the enemies move, `BridgeStops` in the enemy-bullet loop). Fx kinds Steel*, Gauntlet*, Mercy*, BridgeSpark. Protocol 221 |
| `Exsanguinate.cs` | ~0.1k | the tank Hemomancer's Exsanguinate, the Barbed Lash (`Docs/Mockups/exsanguinate-rework.html` take B): hooks every enemy in reach (`Enemy.ReelBy`, `ReelT`, `ReelFrom`; stunned through `GraspT`), the yank (`ExsPull`, an ease-in from `ExsYank` to `ExsLand`) to touching you, the drain on arrival, heavy enemies drained in place; `TickReels` from `TickBloodMages`. One `FxKind.Exsanguinate` for the cast (Pos2.X = -1), one per hooked enemy (its id; Dur - `ExsShow` = its stagger), one impact per caster per tick (-2). Drawn by `Game/ExsanguinateLook.cs`; Juice.cs gives the impact its hit-pause and shake |
| `VeinTithe.cs` | ~0.15k | the tank Hemomancer's Vein Tithe (`AbilityId.BloodRite` kept, replaces the 8 s taunt): `CastTithe` lists every enemy on screen (not spawning, not dummies) into `Player.Tithed` nearest first, `TickTithe` tops up `Enemy.GraspT` (held: no moving or shooting), drops the dead, pays `MaxHp * TitheRate(p) * dt` (not a hit: no grace, `DownIfDead` can down you) and ends it at 30 s (40 s mastered), on going down or when the slot changes; `TitheOutput` cuts your damage 40% in `HurtEnemy`; look in `Game/VeinTitheLook.cs` (arteries, heart, cuffs, `FxKind.TitheSnap`) |
| `Bloodbath.cs` | ~0.15k | the tank Hemomancer's second ultimate (`AbilityId.Bloodbath`, `AbilityDef.UltAlt = 1`, key 1 of the lobby's Ultimate row; Vein Tithe stays key 0): `HealthCost` 15% paid by `PayAbilityCost` like a Space ability's; `CastBloodbath` adds a `ZoneKind.Bloodbath` zone (radius `BathRadius` 170, mastered 220; 10 s; `Zone.HealBack` 25%, mastered 35%) that replicates as an ordinary zone; `BathReach` grows it over `BathGrow` 0.9 s; `TickBloodbath` (from `TickZones`) slows what's in it (normal enemies by `SlowMult`, Heavy and bosses by `Enemy.WadeT`/`WadeMult`, since `MoveEnemy` skips the usual slow for them) and every `BathTick` 0.5 s hurts each enemy in it through `HurtEnemy` (damage multiplier, Blood Price) with `bathTick` set, so `BloodLifesteal` adds no 3%, then heals the owner `HealBack` of what it dealt and sends `FxKind.BathHeal`. Its own tick, not `ApplyBleed`: that bleed stacks and lingers after the last hit. `Damage` cuts 20% for anyone in one (`InBath`); `WaveCleared` clears it; going down doesn't |
| `BloodFreeze.cs` | ~0.1k | the healer Hemomancer's Blood Freeze (`AbilityId.SiphonField` kept): `CastBloodFreeze` queues a wave, `TickBloodFreeze` freezes each enemy it reaches (`FrozenT` 5 s, boss 3 s, `Stats.FreezeBonusTime` from Hungry Earth) and runs `IceT`/`IceMult` (mastered +25% damage, applied in `HurtEnemy`). One `FxKind.BloodFreeze` at the cast; look `Game/BloodFreezeLook.cs` (stateless particles from the effect's age; per-enemy crystal state like the Glacial Tomb's). Test: `bloodfreeze`. Preview: `BLOOD=1 CAST="Blood Freeze" CASTT=0.9 Tools/Preview/shot.sh x hud` |
| `SanguineRain.cs` | ~0.1k | the healer Hemomancer's Sanguine Rain (reworked Nov 2026): `CastRain` sets `Player.RainClock` and `UltT` (siphon + rain), `TickRain` pays the 12% siphon, the 3%/s drain and heals every living ally 3% per 0.5 s, `EndRain` (second press via `RainPress` in `Abilities.cs`/`Game.cs`, timer, or the 15% floor) sends one `FxKind.RainRelease` per ally. Look: `Game/SanguineRainLook.cs` (drawn from the clock alone; rings trail allies). Test: `rain`. Preview: `RAIN=<s> CASTCLEAN=1 Tools/Preview/shot.sh x hud` |
| `CrimsonHarvest.cs` | ~0.2k | the DPS Hemomancer's Crimson Harvest (`AbilityId.CrimsonApotheosis` kept, replaces the old 1 HP / Blood Price window): `CastHarvest` snapshots every enemy as a `HarvestTarget`, `TickHarvest` lands four 5-damage ticks per enemy (`HarvestStart`/`HarvestTickAt`, shared with the look) and adds `HarvestWeight` blood, then the hold; a second press (`HarvestPress`, wired in `Abilities.cs` and `Game.cs` like Blade Fan) or the timer calls `ThrowHarvest` (one `FxKind.HarvestThrow`), and `LandHarvest` does the blast (`HarvestRadius`, `HarvestDamage`, knock, slow). Replicated: `Player.HarvestClock/HarvestBlood/HarvestHold`. Look: `Game/CrimsonHarvestLook.cs`. Test: `harvest` |
| `RadiantOrb.cs`, `Repulsion.cs`, `SoulGhost.cs` | <0.1k each | the Elementalist Healer's Radiant Orb, the Soldier Healer's Repulsion Cannon, Soul Link's ghost (retired: nothing casts it any more) |
| `WorldTree.cs` | <0.1k | the Healer Summoner's World Tree: waves of `MinionKind.Butterfly` (one per ally, ally id in `Minion.LinkTo`, `Launch` = stagger delay, `Vel` = velocity) flutter to allies and heal on arrival; the zone (`ZoneKind.Grove`, 100 px, 15 s) is cast in `Ultimates.cs`; `Game/WorldTreeLook.cs` draws tree and butterflies |
| `Titan.cs` | <0.2k | the Tank Summoner's Titan ultimate: a 140 px `MinionKind.Titan` that rises 1.4 s, chases, winds up 0.4 s and slams, blocks bullets with its body, crumbles; slam, stand-up thud and rubble are `FxKind.TitanSlam`; `Minion.Wind` replicated; `Game/TitanLook.cs` draws it (Runestone look) |
| `Burrower.cs` | <0.1k | the Tank Summoner's Earthquake (rework, Nov 2026; Docs/Mockups/burrower-visuals.html look A): `CastEarthquake` slams 25 dmg / 140 px / 50% slow for 2 s round you, then spawns a static-looking `MinionKind.Burrower` at the cursor (max 650 px away). `TickBurrower`: telegraph 0.5 s, breaks out (`m.Stood` for the sound), surfaces at 1.1 s, then runs `BurrowerRun` = 3 s at 190 px/s steering (3.2 rad/s) at the nearest enemy it hasn't bitten (`Minion.Burrowed`), biting each once for 30 within 38 px (`FxKind.BurrowSnap`); a capstone (`Minion.Capstone`) freezes what it bites. It then stays `BurrowerSink` = 2.2 s unseen so the client can fade the scar. Replaces the old "you and every minion slam". Look: `Game/BurrowerLook.cs` (draws from age, `Aim`, `Disp` and a per-minion trail kept on the client). Test: `burrower` |
| `Hydra.cs` | <0.1k | the Summoner DPS's Hydra (replaced Sacrifice): a static `MinionKind.Hydra` that rises at the cursor, its heads spit venom at the 1st, 2nd, 3rd nearest enemies; head count rides in `Minion.LinkTo`; `Game/HydraLook.cs` draws it |
| `SoulLantern.cs`, `Medkit.cs` | <0.1k each | the Summoner Healer's Soul Lantern (minion linked to a player, heals and flames), the Soldier Healer's Deployable Medkit (a lobbed `MedkitShot` that bursts into `Orb.Med` orbs) |
| `Contraband.cs` | 0.03k | Contraband: `IsCycleStart`, `ContrabandChest` (host; used by `WaveProps` in `Props.cs`, which adds the guaranteed chest) ; `RollChestLoot` lifts the holder's uncommon items to rare. Unlock: `Profile.HuntsWonHard` (a hunt won on Hard or Nightmare; fifth field of the profile's `hunt` line). Plan: `Docs/Contraband.md` |
| `RiotGearLook.cs` | 0.2k | (Game/) Riot Gear's Plate look from `Docs/Mockups/riot-gear-effects.html`: `DrawRiotGear` (from `DrawPlayerAuras` in `WorldRenderer.cs`: the plate swings to the nearest enemy shot, rests behind you otherwise, ghost + fill while recharging, pops when back), `DrawRiotBlockFx` (`FxKind.RiotBlock`, `WorldEffects.cs`: white flash, ring, sparks, three flying pieces; effects reduced or off: the flash only) and `DrawRiotChip` (above the XP strip, `Hud.cs`). Protocol 154 added `FxKind.RiotBlock`. Preview: `RIOT=ready\|wait,17\|block,0.1 Tools/Preview/shot.sh riot hud` |
| `RiotGear.cs` | 0.05k | Riot Gear: `PerksFor` (host, at match start: real runs only, a continued run's saved seat perks) and `GivePerks` (every screen, from the Start message), `RiotBlocks` (first thing in `Damage`; damage-over-time callers pass `overTime: true`), `TickRiot` (30 s recharge); `Player.Perks`/`RiotT` |
| `Perks.cs` | 0.15k | perks: `PerkId`, `Perks.All` (Riot Gear, Contraband), unlock rules from the profile's hunt wins, slots (1 / run win / 5 hunts), `Equip` (one per boss), `Clean`; equipped perks live in `Profile.PerkNames` (profile line `perks`, format 4). Plan: `Docs/RiotGear.md` |
| `Hunts.cs` | 0.07k | Boss Hunt mode (ten waves: nine of the boss's faction, then the boss); hunt-aware `IsBossWave`/`BossAt`/`NextBossWave` |
| `Zombies.cs` | 0.3k | the Zombie Infection modifier: hordes, the dead rise, and the seven zombie kinds' AI (`ZombieMove`, `ZombieSpecial`, `ZombieFire`; Crawler grab via `StunT`, Spitter acid and Bloater spores as patches) |
| `WaveDesign.cs` | 0.4k | the wave editor's designs (`WaveSet`/`DesignWave`/`DesignGroup`/`DesignProp`, spawn points A-E), the `.waves` text format, playing them in the Practice Range (`Game.Design`, `BuildDesignWave`, `TickDesign`) |
| `ProfileStore.cs` / `ProfilesUi.cs` | 0.2k / 0.25k | several profiles (folder each under `profiles/`, `active.txt`), migration of the old single profile, the "Who's playing?" screen (`Docs/Profiles.md`, preview `shot.sh who who`) |
| `ProfileTransition.cs` / `DriveSlot.cs` | 0.2k / 0.15k | the 5.4 s move between the picker and the menu (the disk flies into the drive under the title; `Docs/Mockups/floppy-slot.html`, B), run from `TickMenuIntro`; the drive (`DrawDriveSlot`, replaces the x-axis on the picker and menu); sounds `Sfx.Prof*` and `Sfx.Disk*`; preview `PROFTR=<0..5.4>,<slot>[,back] shot.sh x whotr` |
| `RunGraphs.cs` | 0.4k | Records > Recent runs page (`DrawRecentRuns`): strip, tiles, Overview / Trends, GL charts. Preview: `SECTION=4 [RTAB=1] [RSEL=n] [MOUSE=x,y] Tools/Preview/shot.sh x records` |
| `RunSaves.cs`, `SaveData.cs`, `Profile.cs` | 0.25-0.32k each | saved runs (written as each wave starts), the profile, records and `Unlocks.All` |
| `Rank.cs` | 0.2k | the account rank (`Docs/Rank.md`): `Rank.Of(xp)`, tiers, `Rank.Award` (XP for a run), the cosmetics table; `RunSaves.cs` pays it out in `ReadRecord` (`Game.LastGain`), `Profile` keeps `Xp`/equipped/`Recent`, `Session` shares rank, frame and trail (`Msg.Cosmetics`) |
| `WeaponDps.Generated.cs` | 0.07k | generated by `run.sh 1 1 weapondps write`; don't edit by hand |

### Game (front end) — `ArenaApp` is a `MonoBehaviour` partial class

| File | Lines | What it holds |
|---|---|---|
| `ArenaApp.cs` | 0.45k | boot, command line, input and the frame loop (`Update`, `FixedUpdate`), layout/scaling, `OnGUI` dispatch, small shared helpers |
| `WorldRenderer.cs` | 0.74k | **world rendering in GL**: `OnPostRender`, `DrawWorld` (floor) → `DrawArenaContents` (one helper per layer: auras, shots, zones, orbs, enemies, players, effects), `DrawEnemy` (layers: underlay / status / body / details, with per-type helpers), `DrawPlayer`, `DrawKnight`, aim line / bow draw / cylinder overlays, GL primitives and clipping (`Tri`, `UiClip`, `BeginUiGl`/`UiModelAt`) (the frame, auras, parry shield, orbs, aim line and the GL primitives stay here; the rest is split by what is drawn into the four files below) |
| `WorldShots.cs` | 0.37k | player shots (`DrawSmallShot`, `DrawNinjaShot`, fireballs, boulders, hammers), `DrawShotMods`, charge overlays (bow draw, hammer/boulder charge, revolver cylinder) |
| `WorldEffects.cs` | 0.56k | `DrawEffect` and every `FxKind` look (repulse, shatter, slashes, claws, chains, arc fields, light cones, icicles, jabs, bolts) |
| `SwordsmanLook.cs` | 0.2k | the Swordsman and Shield Swordsman: the floor cone (reach band, red wind-up fill, cream swept wedge), cloak, pauldrons, helmet, tapered blade, shield plate and its block flash; timed from `Enemy.SeenT` |
| `NonShooterLooks.cs` | 0.2k | the Fuse Runner (Powder Keg: keg, burning wick, closing fuse ring), Hookman (Harpoon: diamond, blinking aim line, cable and harpoon, white shimmer when open) and Tripwire Pods (hex pods, lasers between armed pods, beep and blast ring), the looks picked from `Docs/Mockups/non-shooter-enemies.html` |
| `WorldEnemies.cs` | 0.83k | Singularity hazards, `DrawZone`, `DrawMinion`, clones, `DrawEnemy` with its underlay, tells, details and status icons |
| `WorldPlayers.cs` | 0.2k | `DrawPlayer` (footprint ring, the model, the hit-core ring), `DrawKnight` and `DrawNinja` (their ability overlays) |
| `HudLayout.cs` | 0.1k | the HUD's layout in one place (UI audit step 2): the console (`SideW`/`SideL`/`SideR`/`SideH`/`SideY`, `HudWavePlate`, `HudMyCard`, `ConsoleR`), the keys (`HudTile(i)`, `HudBarX`/`HudBarW`, `HudTileY`), `HudXpStrip`, `HudBarPivot` (what the bar's hide/show slide scales round), the stacks above it (`HudChipsY`/`HudChipLine` over the wave plate; `HudStackY`, the Riot Gear chip and teammate row sizes and `HudStackTop` over your card, which ChatBox.cs sits on), `HudMasteryToastAt`, `HudEmoteBarAt` (EmoteBar.cs), `HudChestHint` (PropsLook.cs), the top row (`HudTopRow` for HudBossBars.cs and its slide, `HudObjectiveY`/`HudObjectiveUnderBossY` for ApproachUi.cs, `HudHintPill`, `HudRecTag`), `BannerRect`, `HudUnlockToastY`, `HudHiddenHintY`. Moving the console moves everything stacked on it. Check with `Tools/Preview/uishots.sh -r HEAD hud` (the list has the HUD solo, mid-slide, with Riot Gear, the emote bar, chips and a boss) |
| `Hud.cs` | 0.49k | HUD (layout "C", Oct 2026: wave panel left of the ability bar, your card right of it with teammates as slim rows above, `DrawWaveCorner`/`DrawPlayerCorner`/`DrawTeammateRow`; boss bars wide at the top middle): wave plate, player cards, boss bars, ability bar, mastery toasts, Tab menu, upgrade picker and cards, `StatsSidebar`, banners, run summary (Tab menu, boss bars, picker and summary are in the four files below) |
| `HudTab.cs` | 0.24k | the Tab screen's own parts as hologram panels: YOUR STATS down the left (`DrawHoloStats`, `DrawTabStats`) and Your build (`DrawTabMenu`: header, `DrawTabAbilities` with the MOVE TO keys, `DrawTabUpgrades` grouped by rarity with scrolling, `DrawAbilityHover`) |
| `TabHolo.cs` | 0.15k | the Tab screen's hologram look (look B of `Docs/Mockups/crt-tab-menu.html`): layout (`HoloTop`/`HoloBottom`, the stats column, the page's area, `TabDark`), `DrawHoloBeam` (the beam strip and cone), `HoloPanel` (switches on from a line after `holoOpenAt`/`holoPageAt`, then the glass and the content over it), `DrawHoloGlass` (`CardArt.Holo` in the CRT colour, see-through; scanlines, drifting band, fringe, flicker), `HoloTitle`, `HoloRows`, `HoloTile`, `HoloBar`. Preview: `STATSTAB=0|1|2 [HOLOAT=<s>] shot.sh x tab 23 12.2 3` |
| `HudBossBars.cs` | 0.43k | `DrawBossBars` and each boss's stage names and notes |
| `HudPicker.cs` | 0.45k | `DrawUpgradePicker`, trophy and upgrade cards, the stat panel and sidebar |
| `HudSummary.cs` | 0.25k | the end-of-run summary as CRT tubes (`DrawRunSummary`, `RackTube`, `DrawPlayerSummary`, `SummaryTimeline`, `SummaryRows`): the run's DPS and health charts from `RunGraphs.cs` on the right, the rank strip under the players, `KeyUi` keys along the bottom |
| `ProfileUi.cs` | 0.3k | your profile as a CRT cabinet (`Docs/Mockups/crt-profile.html`): header, profile screen (badge, name + pencil, XP), four tab keys, option keys, Back; each option is a cab window of kind 3 (`OpenCabWindow(3)`, `DrawCabWindow` calls `DrawProfileSection`); `OpenProfile` from the player card, or the pause/lobby menu's Profile row without Look; the "YOUR PROFILE" nudge (`DrawProfileHint`) |
| `ProfileWindows.cs` | 0.2k | the profile's windows: Rank (Progress, Tiers, XP sources), the Unlocks tab, `InfoRow`, `BlockBar` |
| `CosmeticsUi.cs` | 0.3k | the Look windows (`DrawLookWindow`: live preview with try-on on hover, how others see you, Equip list) and the Reward ladder |
| `AimLineLooks.cs` | 0.3k | the nine rank-unlocked aim lines (`DrawAimLook`, used by `DrawAimLine` and the cosmetics previews); the Classic Corner pointer and its Lock-on morph into the reticle (`DrawClassicMorph`); local only, no protocol change |
| `CursorLooks.cs` | 0.25k | the ten rank-unlocked crosshairs (`DrawCursorLook`, used by `DrawCrosshair` and the cosmetics previews; `StepCursor` keeps the fire pulse and the Light Cycle history); local only, no protocol change |
| `RankLook.cs`, `RankUi.cs`, `RankTrails.cs` | 0.1-0.2k each | the rank badge (pixel shield texture per tier), the results strip / hover card / Profile > Rank, and the rank trails (Ember, Frost, Storm, Void, Gold, Prism); `LightCycleTrail.cs` draws the rank-45 Light Cycle trail (a corner-snapped wall, `CycleRider`); `NameEffects.cs` draws names in their effect (`DrawName`: gradients are clipped bands, sheens a clipped strip; used by the party card, the Hangout plate, the world tag and the look card); `EmoteLooks.cs` draws the thirteen emote bubbles and `EmoteBar.cs` is the hold-T bar (`TickEmoteBar` in `Update`, `DrawEmoteBar` in the HUD and the Hangout; `Session.SendEmote` / `Msg.Emote`, host-checked rank and pause; `EmoteBody` moves the model, number keys 1-6 play the bar's slots); `CardBackgrounds.cs` draws the ten card backgrounds (`DrawCardBackground`, called from `DrawFrameCard` / `DrawPartyCard`; `CosmeticKind.CardBackground`, `LobbyEntry.CardBg`); `FrameLooks.cs` draws the frames (Circuit, Ember Edge, Void Pulse, Legend Foil, Prism) |
| `DiskEject.cs` | 0.2k | the eject key right of the menu's disk drive and its 2.9 s spring-pop move back to the picker (`Docs/Mockups/disk-eject.html`, key A + animation B): `DrawDriveEject`, `StartProfEject`, `TickProfEj`, `DrawProfEj`, `DriveEjectAt`; uses `profTr` + `profEj`; preview `PROFEJ=<0..2.9>,<slot> shot.sh x whotr` |
| `Menus.cs` | 0.5k | main menu ("Big play", `Docs/Mockups/menu-d.html`: title over the disk drive (`DriveSlot.cs`) with the shot's tapered-stroke arc (`DrawMenuArc`) and your player card under the box (`DrawMenuPlayerCard`, `MenuPlayerRect`, `Docs/Mockups/menu-card-bottom.html`; the tip box, `MenuTip` / `DrawMenuTipBox` / `DrawMenuTipGl` / `HowToTips`, is parked and not drawn); the box `MenuCard` with Play, Join, Records and Settings (`DrawMenuCardInside`, Join's address row), or after Play the saved-runs picker (`DrawPlayPicker`, `SavesUi.cs`); the header CRT screen with the Quit key (`MenuHeaderRect`, `MenuQuitKey`) and the tooltip (`DrawMenuSecondary`; the profile switch is the drive's eject key, `DiskEject.cs`); name, port and last address saved (`Load/SaveMenuFields`)), How to play, pause menu, settings and audio |
| `MenuServo.cs` | 0.2k | the main menu's Play move, bay doors version 2 (Servo) of `Docs/Mockups/menu-bay-doors.html`: `menuPlayK` (0 to 1 over `MenuPlayLen` 1 s, either way) drives `DrawMenuServo` (runs screen behind the doors via `ServoClip`, Play halves, `DrawServoKey` drops, New run rise, a thunk shake), `DrawServoHeader` (split-flap letters, `KeyUi.Key` with a second legend for Quit to Back) and `ServoCues` / `PlayServoCues` (`Sfx.Bay*`, never sent). `DrawPlayPicker` skips its header and New run while `servoPicker`. Preview: `PLAY=1 PLAYK=0.5 shot.sh x menu` (`PLAYBACK=1`) |
| `MenuIntro.cs` | 0.1k | the main menu drawing itself in the first time it shows (pen plot, option B): `menuIntroT`, `TickMenuIntro` (any key or click skips), `MenuIntroK(a, b)` that every piece asks, the ink-then-fill title and the pen tip, and the title's typing sounds (`TickTitleTyping`: `Sfx.TitleKey` per letter, `TitleSpace`, `TitleEnter` on the last; look B of `Docs/Mockups/menu-typing-sounds.html`; hear them: `Tools/SoundCheck/render.sh Title`). Preview: `MENUINTRO=<s> Tools/Preview/shot.sh <name> menu` (`JOINOPEN=1` for Join's row) |
| `PerksUi.cs` | 0.2k | the lobby's Perks tab (tab 5), look A of `Docs/Mockups/riot-gear-perk-menu.html`: equipped slots (empty / filled / locked with the rule), a card per `Perks.All` perk (click to equip or take off, hover tip) and locked placeholders for the bosses to come; equipping saves the profile and calls `Session.ShareCosmetics`. On a continued run it is read-only and shows the seat's saved perks. `DrawSeatPerks` draws each seat's perk icons on its party card (called from `DrawPartyCard`). Icons `perk_riot`, `perk_bag` in `PixelArt.cs`. Host side: `LobbyEntry.ChosenPerks` (picked) vs `PerkIds` (what the seat will bring; `Session.ShownPerks`, refreshed in `BroadcastLobby`) |
| `LobbyRoom.cs` | 0.4k | the Hangout tab: the scrolling window and camera, WASD and mouse aim, minimap, PvP switch, shots, health bars, each character with its trail, rank badge and frame name plate, and the preview-window grow transition (`hangoutAnim`, `DrawHangoutTransition`) |
| `LobbyUi.cs` | 0.5k | the lobby as one digital CRT screen (`Docs/Mockups/crt-lobby-digital.html`): header (Back key, `LOBBY_`, "!" popup with port and invite addresses), mode keys, party cards with live characters, open slots, Ready/Start keys (`DrawLobbyActions`), tab keys, and the big loadout screen (`DrawLoadout`: the live mini-arena `DrawShowcase`, glowing stat bars, `DrawRoleClassTitle`, role / class / weapon (with `AscensionKey`) / ability / ultimate keys, `DrawDetailBox`, `DrawAscensionWindow`); layout constants (`PartyTop`, `LobbySplit`, `LobbyBodyRect`...) are what the entrance pieces and `LobbyEnter` use |
| `RoundWindows.cs` | 0.05k | the between-round windows' shared frame (UI audit step 4): `RoundBackdrop(a)` (the dark layer, `RoundDark`; `PickerDark`, `ShopDark`, `ResultsDark`; the path choice's `PathDim`), `RoundPart(a, offset, draw)` (a part faded and moved; fades `Ui.Alpha` as well as `GUI.color`, so `Ui.Fill`/`Ui.Dot` fade with the text; was RunEnding's `EndPart`), `RoundTube(r, k, slide, draw)` (a blue CRT tube that comes in; was HudSummary's `RackTube`). The results screen is built of tubes; the picker, shop and path choice use the layer; the Wave cleared box fades with `RoundPart`. The CRT Wave cleared looks: `Docs/Mockups/crt-wave-cleared.html` (https://claude.ai/artifact/B2qrLd1kKF6usdi9E93XgR). Preview: `WAVECLEARED=1 [WCT=<s>]` (the box that long after it appeared; settled by default) |
| `CrtStyle.cs` | 0.1k | the CRT look's settings (UI audit step 3): the palette (`Ink`, `Dim`, `Bg`, `Bezel`, `Danger`/`DangerDark`, `Body`, `Desc`, `Head`, `Gold`, `Corner`, `BoxFill`/`BoxTop`/`BoxRim`, `ScreenGlow`, the glass frame and `ScreenFrame`) and the key settings that `CrtKit.SoftBox`/`SoftText` (so `CrtKit.Key`, `LobbyKit.Key`, `WinKey`, `DrawerRow` and the lobby's own boxes), `LobbyKit.Key`'s waiting state, `WinKey`'s static label, the HUD console's `CrtKeyBox` and the inverse-video `CrtUi.Button`/`Small` read; `KeyColor` picks a key's colour (locked, danger, accent). The old names (`CrtUi.Ink`/`Dim`/`Bg`, `LobbyKit.Soft`/`HeadCol`/`Gold`, `CabSoft`, `CabDanger`) are aliases. Not in it: the main menu's mechanical keycaps (`KeyUi.cs`), a separate look |
| `LobbyLayout.cs` | 0.1k | the lobby's layout in one place (UI audit step 2): `PartyX`/`PartyW`, the header keys (`LobbyBackKey`, `LobbyTitleRect`, `LobbyTitleAnchor` where the menu's title lands, `LobbyInfoKey`), `LobbyModeRect`, `LobbyPartyLabel`/`LobbyHangoutKey`, the party card sizes, Ready/Start, `LobbyBodyRect`, `LobbyTabsRect`; and the bands the animations cut the lobby into, derived from those boxes: `LobbySplit` (party column's right edge + 14), `LobbyLeftSide`/`LobbyRightSide` (Recall.cs, LaunchIntro.cs) and the ten `Lobby*Band`s that `DrawLobbyPieces` (LobbyEnter.cs, LobbyCrt.cs) slides in. Moving a box here carries the entrance, Leave, New run boot, return and launch along. Check with `Tools/Preview/uishots.sh -r HEAD lobby` (the list has those animations mid-way) |
| `LobbyKit.cs` | 0.2k | lobby-only digital CRT pieces: `Key` (fits its text: shrinks, wraps, icon beside or stacked, locked / disabled), `Locked`, `Heading`, `Panel`, `Tile`, `StatBar` |
| `PracticeUi.cs` | 0.45k | Practice Range editor (live enemy tiles, shared edits, save/load) |
| `ZombieLook.cs` | 0.2k | the zombies' shapes, haze, drips, frenzy streaks, the Crawler's vines, acid and spore patches; the dark green arena colours |
| `WaveEditorUi.cs` | 0.7k | the F1 wave editor (option B of `Docs/Mockups/wave-editor.html`): arena map (spawn points, props, the wave at the playhead), palette, group settings, timeline (drag clips), save/load `.waves`, Test |
| `MenuBackgrounds.cs` | 0.34k | 10 animated menu backgrounds + the lobby's picker card (bottom left: live thumbnail, arrows, dots); your pick is also drawn dimmed behind the arena (`DrawWorld`, `ArenaBgDim`) |
| `DevTextEdit.cs` | 0.15k | F4 text-edit mode: banner, pick a text by clicking (`TextEditBeginGui` in `OnGUI`), the edit box; `Ui.Shown` (called by every label and button) applies edits and finds the text under the mouse. `Tools/TextEdits/fold.py` writes edits into the code |
| `BossLabUi.cs`, `DevMenu.cs` | 0.3-0.4k each | F1 tools (`DevMenu`: sidebar tabs Match / Players / Tools / DPS / Replays, card layout) |
| `ReplayUi.cs` | 0.4k | the Replays tab (`DrawDevReplays`) and the timeline (`DrawReplayBar`, `ReplayKeys`). While one plays, `ArenaApp.session`/`game` point at the replay's own (swapped back in `CloseReplay`); `Update` and `FixedUpdate` hand off to `ReplayUpdate`/`ReplayFixedStep` |
| `DpsMeterUi.cs` | 0.1k | the F2 DPS meter overlay (`DrawDpsMeter`) and the F1 DPS tab (`DrawDevDps`) |
| `PathsUi.cs` | 0.48k | Choose-your-path cards (live floor + enemies, skulls, rules, bonus, prize, "No path"), each path's floor pattern (`DrawPathFloor`), fire/pool patches, Narrow Pass walls (`DrawNarrowWalls`) and Fogbound mist (`DrawFog`, per-vertex alpha grid), HUD path chip |
| `ShopUi.cs` | 0.3k | the Bazaar's shop screen (`DrawShop`): shelf cards, services, remove-upgrade list, Give popup, Done button and status, host close button |
| `ModifiersUi.cs` | 0.1k | the lobby's Modifiers tab: tiles in two columns under foldable tier headings (`DrawModTile`, look C1 of `Docs/Mockups/modifiers-crt-icons.html`); icons via `ModIconKey` |
| `ModIcons.cs` | 0.2k | the 12×12 `mod_*` icons (white, tinted by the tab); a partial of `PixelArt`, which `Get` falls back to |
| `RunStatsUi.cs` | 0.4k | the Run stats screen (Tab, or the pause menu's Run stats) as hologram panels (TabHolo.cs): the tab keys (`LobbyKit.Key`), opening and page changes (`statsLastFrame`, `statsShown` set `holoOpenAt` / `holoPageAt`), Run so far (header with tiles, totals tiles, `BuildRoute` / `DrawRouteCard`, `DrawFightCard` with the party's share), Your build (`DrawTabMenu`), Team (`DrawMateCard`, a panel per teammate) |
| `ReferencePages.cs` | 0.03k | the published Bestiary / Armory URLs (`OpenReferencePage`, the guide's Web page button) and `FieldGuideColor` |
| `FieldGuide.cs` | 0.8k | the in-game Bestiary and Armory: pages (`GuideBestiary`, `GuideClasses`...), lists built once from `BestiaryData` / `Defs` / `Upgrades` (`GuideItem`), search, the list with live models, one `Draw...Entry` per page, the scrolling body helpers (`GText`, `GHeading`, `GLink` jumps to another entry) |
| `Ui.cs` | 0.5k | IMGUI style kit (panels, buttons, choices, bars, fancy text). Buttons are the owner's chosen style "F": icon cards (`Choice`, `ActionButton` (the lobby's Back / Ready / Start: a Choice card) with an icon tile, `Button` with an optional icon chip), `FancyButton` for menu showpieces, `PulseRing`, tinted `Icon` |
| `Ambience.cs` | 0.25k | more local effects: orb trails and pickup pulses, dust puffs, each path's weather (`WeatherFor`) |
| `CardArt.cs` | 0.1k | the level-up cards' shaded gradient bodies (one texture per rarity colour); with `Ui.Glow` (a soft glow, 9-sliced) and `Ui.Dot` (true circles for stack counters: the 9-sliced capsule collapses when small). Milestone upgrades live in `Upgrades.cs` (`WithMilestones`, `MilestoneOffer`), `Game.cs` (`GrantXpTo`, `NextOffer`) and `Tools/SimTest/Tests/Milestones.cs`. Preview: `MILESTONE=1 Tools/Preview/shot.sh x picker`; the preview now draws real textures and 9-sliced boxes, so glows and gradients show |
| `ShotLooks.cs` | 0.2k | new looks for player weapons' shots with Visual effects on (`ShotWeapon` picks out a weapon's own shot; `DrawWeaponShot` from `DrawBigShot`/`DrawSmallShot`; `EmitFromShot` from `UpdateJuice`; `DrawJuicyBolt`; the Prism split in `BulletEnding`). Preview: `SHOTLOOKS=1 Tools/Preview/shot.sh <name> hud 3 4` |
| `GunBowLooks.cs` | 0.4k | Revolver and Mender's Bow with Visual effects on (look A of `Docs/Mockups/revolver-bow-animations.html`): `OnRevolverShot` (called from `UpdateJuice` when a Pistol round appears: flash, smoke, brass, kick via `GunLook`), `UpdateBowLooks` (notices a release: string snap and ring), `DrawBowFlex` (the held bow), `DrawBowFullPop`, `DrawArrowStreak`; particle kinds `PMuzzle`/`PCasing`/`PSnap` drawn by `DrawGunPart`. Preview: `BOW=0.9`, `REVOLVER=1` |
| `SoldierLooks.cs` | 0.2k | Soldier DPS abilities with Visual effects on (Dash B, Grenade A (now the Twin Rifle's tubes and Airstrike's bombs), Mark A + skull of `Docs/Mockups/soldier-dps-abilities.html`): `DrawDashStreak` (from `DrawPlayer`, own dash only), `DrawGrenadeFlight`, `IsGrenadeBoom`/`DrawFragBlast`/`FragBurst` (grenade Boom effect; particle kind `PFrag`), `DrawMarkLook` (from `DrawEnemyStatus`; clients only get a marked flag, so each draws its own 6 s clock). Preview: `CAST="Mark for Death"`, `CAST="Cluster Grenade"` |
| `MeleeLooks.cs` | 0.4k | melee attacks with Visual effects on (style E of `Docs/Mockups/melee-attacks.html`): `DrawSlashE` / `DrawJabE` (called first by `DrawSlashFx` / `DrawJabFx`; false = the old look) find the player who swung (`MeleeOwnerOf`); the weapon (`DrawMeleeModel`) and sparks in the normal pass, the energy (`GlowArcBand`, beams, the Lance box, the hammer's trail and impact) in the glow pass (`DrawMeleeGlow`, from `DrawGlowPass`; the lobby preview runs it too, clipped); the Halberd sends one Slash per sweep (`Swing.FxHalf`); the Warhammer's overhead swing and slam (`DrawHammerSlamE`, raised while charging: `DrawHammerRaised`). Preview: `SWING=<s>` with `SWINGW=<WeaponId>` |
| `BearLooks.cs` | 0.45k | the Bear Totem in style A of `Docs/Mockups/bear-totem.html`: a claw on each hand (`DrawBearModel`, from `DrawPlayerModel`; the left rakes across, the right rakes back; which hand and how far come from the swipe's own `FxKind.Claw` effect, found with `BearSeenOf`, so nothing is sent), the claw trails (`DrawBearClawFx`, with Visual effects on), the totem as a medallion on the back, the Bear companion (`DrawBear`: strides from how far it moved, its jaw snaps when it's next to an enemy and when that enemy is hurt) and claw scars on bleeding enemies (`DrawBleedScars`, from the replicated `Enemy.BleedHits`). Preview: `SWING=<s> SWINGW=BearTotem` (`BLEEDHITS=<n>`) |
| `EnemyPresenceLook.cs` | 0.05k | Regular enemies with Visual effects on (look D of `Docs/Mockups/enemy-prominence.html`): `DrawEnemyShadow` (from `DrawEnemy`) puts a dark contact shadow under the body, `DrawEnemyPresenceGlow` (from `DrawGlowPass`) a soft glow in its own colour (`GlowAlpha` 0.12, `GlowRadius` 2.6, far quieter than a player's halo). Not bosses, spawning enemies or zombies. The wind-up (`VarietyLook.cs`) is unchanged. Front end only, no protocol change | |
| `RadiantOrbLooks.cs` | 0.1k | The Radiant Orb's trail with Visual effects on (style B, Stardust, of `Docs/Mockups/radiant-orb-trails.html`): `UpdateRadiantStardust` (from `UpdateJuice`) follows each orb and Light Orb and sheds motes, `DrawRadiantStardust` (from `DrawGlowPass`) draws a faint tail, glows and four-point sparkles. Front end only, no protocol change. Glow isn't drawn by the preview tool | Sounds: `Sfx.RadiantCast`/`RadiantTouch`/`RadiantFade` (recipes in `SoundBank.cs`, sent to everyone, quieter for allies; hear them: `Tools/SoundCheck/render.sh Radiant`) |
| `LeapSlamLook.cs` | 0.2k | The Knight's Leap Slam with Visual effects on (look C, Bulwark, of `Docs/Mockups/leap-slam-rework.html`): `DrawLeapSlamFx` from `FxKind.LeapSlam` (host sends one at the cast from `Knight.cs`: Pos = where they leapt from, Pos2 = where they land, Radius = id*1000 + the slam's radius, Color.A 1 = mastered, Dur = `LeapTime` + `LeapFxTail` + the stun; everything runs off its clock), `LeapHeight` (the knight swells and rises in `DrawPlayer`, shadow left on the floor, no invulnerability flicker), `LeapCaged` (a stunned enemy gets a hex cage, not the ice block), `IsLeapBoom` (the landing's plain Boom glow and sparks are skipped; its scorch, ripple and shake stay). Preview: `LEAPSLAM=<s> [LEAPMASTERED=1] Tools/Preview/shot.sh x hud`. Protocol 163 | |
| `KnightDpsLook.cs`, `DrawbridgeLook.cs` | ~1k | the DPS Knight's looks, ported shape for shape from the chosen looks of `Docs/Mockups/knight-dps-abilities.html` (Forge, Heraldic, Last Rites, Oak & Iron): shared pieces (`KSparks`, `KPuffs`, `KShatter`, `KCrescent`, `KStar`, `KGhost`), Caught Steel's shards (a per-player list built from `FxKind.SteelCatch`, kept in step with `Player.Steel`; `DrawSteelShardShot` for the thrown bullets), the Gauntlet's throw, crest, ribbon, charge, impact and return, Misericorde's daggers, crosshair, leap (`LeapHeight` in `LeapSlamLook.cs` lifts the knight), chain of light, kill and candle, the Drawbridge's lane, towers, fall, chains, slam and deck; `DrawKnightDpsUnder`/`Over`/`Top` in `WorldRenderer.cs`, `DrawKnightDpsPops` on the HUD, `KnightDpsFeel` in `Juice.cs`. Preview against the mockup: `KNIGHTDPS=steel|gauntlet|mercy|bridge KDT=<s> Tools/Preview/shot.sh x hud`, crop (352,200)-(928,520) |
| `GlacialTombLook.cs` | 0.3k | The Glacial Tomb with Visual effects on (look C, Crystal Reliquary, of `Docs/Mockups/glacial-tomb.html`). The cast's `Ring` effect (radius 900, `Dur` = `Game.TombFxTime(mastered)`: freeze + `TombFxTail` 1 s; `IsGlacialTomb` tells it) runs everything off its clock: `DrawTombFloor` (from `DrawArenaContents`) the hex lattice growing at 1100 px/s, draining back at the thaw, and the mandala; `DrawGlacialTomb` (`DrawEffect`) the wave, flash, a ripple and crystal or star where each erased shot was (`UpdateTombLook`, from `UpdateJuice`, keeps last frame's enemy shot positions since the host erases them at once) and eight spears round each boss; `DrawTombSpires` replaces Frost Nova's ice block on enemies frozen by a tomb (`TombCage` finds the cast from the effect list; spires rise as the wave arrives) and `DrawTombShatter` breaks them when `Enemy.Frozen` drops. Effects off: the old ring. Drawing only, no protocol change. Preview: `CAST="Glacial Tomb" CASTT=<s> shot.sh x hud` (one frame, so shots, bosses and the break aren't shown) |
| `GolemWallLook.cs` | 0.2k | The Summoner Tank's Golem Wall, look B (Basalt) of `Docs/Mockups/golem-wall-rework.html`: `DrawGolemWall` per `MinionKind.Wall` (rise from when first seen, ember veins and cracks by `GuardFrac`, hit flash found by watching it drop, ground seam to the next golem), `SweepGolemWalls` (a golem that vanishes bursts into rubble). The golems have no timer (`T = -1`), wear from every enemy shot (`BlockedBySummons`), a recast replaces them. No protocol change | |
| `CloneWallLook.cs` | 0.2k | The Ninja's Shadow Clone wall, look C (Afterimage Phalanx) of `Docs/Mockups/shadow-clone-wall.html`: `DrawCloneWall` per `MinionKind.CloneWall` (dash-out afterimages by age = Duration - T, landing ring and flash, energy fence to the next clone along the wall, glitch hit found by watching `GuardFrac` drop, scan lines), `SweepCloneWalls` (a clone that vanishes with time left shatters). Protocol 167 | |
| `ShadowStepLook.cs` | 0.2k | The Ninja's Shadow Step with Visual effects on (look 6, Smoke Reform, of `Docs/Mockups/shadow-step.html`): `DrawShadowStepFx` from `FxKind.ShadowStep` (host sends it from `Ninja.cs` with where they stood, where they landed, and id*1000 + the struck enemy's radius), `StepHides` (the body isn't drawn for the first 0.36 s; the step itself is instant), `IsShadowStepPlain` (hides the plain ring and slash that Visual effects off still draws). Where they stood travels in `Msg.Ability` (`Game.StepFrom`). Protocol 162 | |
| `AirTrailLooks.cs` | 0.15k | The air trail with Visual effects on (look A, Ribbons, of `Docs/Mockups/air-trail.html`): `UpdateAirTrails` (from `UpdateJuice`) smooths each player's, enemy's and minion's drawn speed and keeps 0.55 s of path, `DrawAirTrails` (normal pass, from `WorldRenderer`, under every body) draws two wobbling ribbons per body. Strength is `AirStrength(speed)`: ((speed-110)/220) squared, capped at 1 (`AirStart`/`AirFull`). Front end only, no protocol change | |
| `ChainLightLooks.cs` | 0.15k | Chain Light with Visual effects on (style A of `Docs/Mockups/chain-light.html`): `ChainLightDrawnAsRadiance` (called first by `DrawLightConeFx` / `DrawBoltFx`) hands the cone and its warm-coloured bolts to the glow pass, where `DrawChainLightGlow` draws the fan, god-rays, motes, beams and halos. Preview: `SHOTW=ChainLight SHOTT=<s>` |
| `EnergyWardLook.cs` | 0.2k | The Healer Elementalist's Energy Ward (was Arcane Ward) as a bubble with Visual effects on (Heal core with Jelly hits of `Docs/Mockups/energy-ward-shrink.html`): `DrawEnergyWard` (from `DrawPlayerAuras`) keeps a per-player spring for the radius (shrinks to 55% with the shield or the last 2.5 s), squash and rim wobble knocked by each `ShieldHp` drop (hit direction = toward the nearest enemy), the blue-to-green shell and the beating green core; `DrawEnergyPop` plays the ring and motes when it ends. Reads the replicated `ShieldHp`, `ShieldT` and `EnergyMax` (0 = not a ward; protocol 151). Off with Visual effects, the old ring is drawn. Preview: `CAST="Energy Ward" CASTT=0.6 shot.sh x hud` |
| `MendPulseLook.cs` | 0.1k | The Healer Soldier's Mend Pulse with Visual effects on (style B of `Docs/Mockups/mend-pulse.html`): `IsMendPulse` spots the `FxKind.Ring` (pulse green, 0.6 s, radius 240+; Soul Link's 90 px ring shares the colour), `DrawMendPulseFx` (from `DrawEffect`) draws the fill, two rings and a heart on each ally as the ring reaches them. Drawing only, no protocol change. Preview: `CAST="Mend Pulse" CASTT=0.2 Tools/Preview/shot.sh mendpulse hud` |
| `TankLooks.cs` | 0.3k | The Tank Soldier's Roar and Bulldoze with Visual effects on (`Docs/Mockups/tank-abilities.html`, Proposed): `DrawTankCastFx` (from `DrawEffect`'s Callout case) draws each cast from the Callout's age, so nothing is remembered or synced: `DrawRoarWave` (pull-in, three rings, blast, streaks, each enemy's flash and "!"), `DrawBulldoze` (lane, afterimages, streaks, dust, skids, bow wave, contact sparks, stomp); `DrawRoarSustain` (from `DrawPlayerLinks`: plates, rim from `TauntT`, tethers, pulse); `IsTankRing` skips the old plain rings; `UpdateTankLooks` kicks the camera once per cast. Preview: `CAST=Roar CASTT=0.25`, `CAST=Bulldoze CASTT=0.2` (`hud 6 9 2`), lobby `LOADOUT=0,0,0,1 ABILITY=1 SHOWT=1.55` |
| `BarrierLook.cs` | 0.3k | The Tank Soldier's Barrier with Visual effects on (style C of `Docs/Mockups/barrier-animations.html`): `DrawBulwark` (called from `DrawPlayerAuras`; off, `WorldRenderer` draws the old disc and ring) keeps a cosmetic `BulwarkState` per player from the replicated `BarrierT`/`BarrierFrac` (a fresh cast, plates lost as the soak runs down, shattered vs ran out) and the blocks `Juice.BulletEnding` reports through `NoteBarrierBlock`. Eight plates slam down, the nearest recoils and flashes on a block, one is knocked off per eighth of soak, the rest slide off (ran out) or blow out (shattered). Nothing synced. The mockup's reflect tint isn't drawn: `BarrierReflect` is host-only. Preview: `CAST=Barrier CASTT=<s> Tools/Preview/shot.sh <name> hud` |
| `ArcLightningLooks.cs` | 0.2k | Arc Lightning with Visual effects on (style A of `Docs/Mockups/arc-lightning.html`): `ArcLightningDrawnAsThunder` (called first by `DrawArcFieldFx`; `DrawArcCharge` draws nothing with effects on) hands the strip to the glow pass, where `DrawArcLightningGlow` draws the charge (halo, sparks off the staff, your own dashed strip with its pulse and marker: `DrawThunderCharge`) and the release (the spine bolt, forks, flash and the stun crackle on each enemy: `DrawThunderStrip`). The lobby preview runs the glow pass and now draws the charge too. Preview: `SHOTW=ArcLightning SHOTT=<s>` (release) or `SHOTC=<0..1>` (charge) |
| `HealthBars.cs` | 0.1k | the damage ghost on every health bar (`GhostBar`, `Ghost`), and the slim boss bars when the HUD is hidden (H) |
| `CameraZoom.cs` | 0.1k | scroll-wheel zoom: `zoomWant` (1 to 2, x1.12 a notch) eased into `camZoom`, `camFocus` eased onto you and clamped inside the arena; `Px` applies `ZoomPoint` while `camZoomOn` (set in `BeginLeanFrame`, off for the mood tint / ending vignette / game-over dim), `ToUi` and `MouseArena` follow, `UiWorldScale` sizes HUD rings and label offsets; zoom 1 draws exactly as before; saved as `cam_zoom`; preview `ZOOM=1.8`; local only, no protocol change |
| `OffscreenMarkers.cs` | 0.1k | zoomed-in edge arrows: `DrawOffscreenMarkers` (from `DrawHud`) finds what `ToUi` puts off the canvas (`OffBy`), places a chevron where the line from the view's middle meets `MarkArea` (`EdgePoint`, below the boss bars, above the console); enemies nearest first, capped at 40 (bosses always), smaller and fainter farther out; teammates in their colour with a name label; fades in with the zoom; preview `ZOOM=2` draws them |
| `CameraLean.cs` | 0.1k | camera lean: `lean` (arena units) eased toward 65% aim + 35% movement, max 80 x 45 x 50%; `Px` shifts the world, `MouseArena` adds it back, `ToUi(worldPos)` is the HUD-canvas position for labels (name tags, damage numbers, portal/altar labels); saved as `cam_lean`; local only, no protocol change |
| `Juice.cs` | 0.8k | visual effects, all local: glow pass, shake, hit-pause (frame replay), shatter, particles, trails, scorch, ripples, mood, damage numbers, level-up bursts, boss entrance card; their settings |
| `HudTiles.cs` | 0.15k | the ability bar's tiles react ("Snap"): `TickTiles` (Update) finds ready / charge back / use from each slot's `Charges` against last frame's, `TileKeyPressed` (the key handlers) a press while recharging; `TileLookFor` gives `DrawAbilityTile` (Hud.cs) its pop, shake, rim flashes, READY slide and pip pop; `TileTransformed` scales a tile round its centre on top of the current `GUI.matrix` |
| `ApproachUi.cs` | 0.04k | the approach chip above the wave panel and the banner's line |
| `CharacterModels.cs` | 0.6k | the player models (`Docs/Mockups/character-models.html`, colour-by-class): `DrawPlayerModel` draws back piece, shoulders, role trim (`ModelRoleTrim`), arms, the held weapon (`DrawHeld`, one case per `WeaponId`; `HoldOf` gives the pose) and head (`ModelHead`) in a local frame (`MW`, `MLine`, `MPoly`...; x along the aim, y to the right, Tank units, BodyR = 18). Class palettes in `ModelPals`, role colour only in the trim. Preview: `Tools/Preview/shot.sh x kitgrid` with `KIT=0..5` |
| `ClassLooks.cs` | 0.05k | one `KitLook` per class (ability and buff overlays drawn over the model, and auras under them), in Kit order |
| `AbilityLooks.cs` | 0.3k | telling what abilities do, for every class: callouts (each cast's name over its caster, `FxKind.Callout` from `HostAbility`; Settings > Ability names, `fx_callouts`), buff timers (`DrawBuffTimers`: a shrinking arc under the player per timed buff, the `Buffs` table; a new timed buff adds a line), taunt eyes over enemies (`DrawTauntMarks`, any taunt), beams to the allies an ability reached (`FxKind.Link`: Guardian's Oath, Lay on Hands, Lifeline, Energy Ward, Sanctify), Blade Tempest and Alpha Call's frenzied minions. Preview any ability: `CAST="Roar" CASTT=1.5 Tools/Preview/shot.sh roar hud 6 9 2` (plays the ability's own class) |
| `BloodMageLook.cs` | 0.2k | the Blood Mage's look (hood, circling drops that glow brighter as health falls, weapon in hand, buffs), its shots, Blood Pool zones, Crimson Pact's ring; ability feedback: the health-cost spurt (`FxKind.Bleed`), Blood Shell's plates and forming, Crimson Pact's reach/countdown/stored blood (`PactStore`, `PactRadius` are replicated) and burst, taunted enemies' eyes (`DrawBloodTaunts`), Exsanguinate / Blood Gift / Martyr's blood streams (`FxKind.Drain`, also Vein Rush's refunds). Vein Tithe, Sanguine Rain, Crimson Harvest, Blood Debt, Arterial Spray and Vein Rush have their own look files (preview the DPS three with `HEMODPS=spray|debt|rush Tools/Preview/shot.sh x hud`). Preview any ability: `BLOOD=0 CAST="Crimson Pact" CASTT=2 Tools/Preview/shot.sh pact hud 6 9` |
| `BloodbathLook.cs` | ~0.15k | Bloodbath (Docs/Mockups/bloodbath.html, https://claude.ai/artifact/SmVXr2cLz1BF87z6Cjmooi): `DrawBloodbath` (from `DrawZone`) draws the pool from the replicated zone: a fan from a glossy dark middle to a wavy, uneven edge (`BathEdgeAt`, three sine terms that scale with the reach and pull back unevenly as it dries), a wet rim, the flood's splash, ripples rolling out, a glint, cracks while it dries over the last `Game.BathDry` s, and a guard arc under each ally in it; `DrawBloodbathOver` (after the enemies) tints each enemy wading in it with a ring at its feet and drops falling off; `DrawBathPops` (HUD) the "+n" from each `FxKind.BathHeal`. Visual effects off: a plain disc, a rim and a time ring |
| `MartyrLook.cs` | 0.15k | look A "Offering" of `Docs/Mockups/martyr.html`: the chest cross, rosary of beads, rise burst (`FxKind.MartyrCast`: Radius = ally id * 10 + 1 revive, 0 ward on, 2 ward spent), the invulnerable shimmer and the 12-bead ward halo drawn from `Player.WardT` |
| `BloodShellLook.cs` | 0.2k | look B "Heartbeat" of `Docs/Mockups/blood-shell.html`: the bubble (cell wall, veins, rim that dents at hits and beats faster as health falls, a ring of health left to spend), `ShellHit` (spray and beads of blood to you and allies) and `ShellPop` (forming, letting go, burst) effects. Preview: `CAST="Blood Shell" CASTT=1.2 CASTHP=0.8 Tools/Preview/shot.sh name hud` |
| `Tooltips.cs` | 0.15k | hover tooltips on the Tab screens: Your build's weapon and upgrades, and every teammate's weapon, abilities and upgrades on the Team page (`TipClear` / `TipUpgrade` / `TipWeapon` / `TipAbility` / `TipMore`, then `DrawTip` last; upgrades read for that player's weapon via `Upgrades.DescFor`). Preview: `MOUSE=x,y [STATSTAB=2] Tools/Preview/shot.sh <name> tab` |
| `LobbyEnter.cs` | 0.15k | the main menu turning into the lobby: the menu's box grows into the loadout, the title flies off as LOBBY, the bottom bar (with your name card) drops away and fades, and the lobby's pieces slide and fade in. Preview: `LOBBYIN=<s> Tools/Preview/shot.sh <name> lobby` |
| `LobbyCrt.cs` | 0.3k | the lobby as a blue CRT screen (screen, scanlines and rolling bar behind the panels; dark corners, glass highlight and rounded bezel over them) and New run's 7.1 s CRT boot into it (the saved-runs screen grows to the window as a full CRT, `DrawCrtGrowScreen`, a link log types, a scan line lights the lobby's pieces). Preview: `CRT=<s> PLAY=1 Tools/Preview/shot.sh <name> lobby` |
| (pause sounds) | — | the pause menu's signal-glitch sounds are in `PauseGlitch.cs` (`PauseSounds`, from `StepPauseAnim`): `Sfx.PauseTear`/`PauseType`/`PauseSettle`/`PauseClose`, local only. Hear them: `Tools/SoundCheck/render.sh Pause` |
| (launch sounds) | — | the run launch's sounds are in `LaunchIntro.cs` (`LaunchSounds`, from `TickLaunch`): `Sfx.LaunchSweep`/`LaunchRipple`/`LaunchBeam`/`LaunchLand`/`LaunchHud`, local only. Hear them: `Tools/SoundCheck/render.sh Launch` |
| `PickSounds.cs` | 0.05k | the upgrade picker's sounds, local only (`TickPickSounds` from Update plays each as the opening/close passes its moment; recipes `Sfx.PickRise`..`PickAbsorb` in SoundBank.cs, appended to `Sfx` and never sent). Hear them: `Tools/SoundCheck/render.sh Pick` |
| `PickIntro.cs`, `PickOutro.cs` | 0.3k | the upgrade picker opening out of your character and closing back into it (see "Upgrade picker opening and close" below) |
| `PixelArt.cs` | 1.15k | 16×16 pixel-art icons as text grids |
| `MusicLibrary.cs`, `CustomMusic.cs`, `MusicPlayerUi.cs` | 0.7k | the music player's track list (extra mood recipes), `Jukebox` selection logic, and its Settings > Audio tab |
| `SoundBank.cs`, `GameMusic.cs`, `MenuMusic.cs` | 1.3k | every sound and all music synthesised in code |
| `WardenLook.cs`, `StageShiftLook.cs`, `SingularityLook.cs`, `LichLook.cs`, `DuelistLook.cs`, `HeraldLook.cs`, `DevourerLook.cs` | 0.1-0.6k each | each boss fight's look (`StageShiftLook`: the Warden's and Queen's 2 s stage changes) |
| `QueenLootLook.cs`, `ReaperLootLook.cs` | <0.1k each | how the Queen's and Reapers' legendaries look |
| `VarietyLook.cs` | 0.26k | the second half's enemies and elite trait markers |
| `ChallengeLook.cs` | 0.5k | challenge rooms on screen: portal, room floor, crystal, zones, altar, HUD cards |
| `Keybinds.cs`, `KeybindsUi.cs` | 0.3k, 0.1k | the rebindable controls (Fire and Parry too; they default to the mouse buttons and `ArenaApp` blocks each while a menu is up): `KeyAction` enum, defaults (weapon secondary = Left Alt), PlayerPrefs save (`key_<Action>`), `Keybinds.Tick` captures the next key while rebinding (`Busy` swallows Esc and gameplay keys that frame), `Down`/`Held` for the game, `SlotLabels` for the ability bar; Settings → Keybinds draws the page. `PlayerInput.WeaponAction` is local (not networked, no protocol bump); `Ninja.cs` `LocalWeaponDash` uses it for the Kunai blink, Twin Fang, Shuriken recall, Bo Staff shove and Kusarigama reel |
| `MenuDrawer.cs`, `WindowAnim.cs` | 1.1k, 0.2k | the menu windows as a side drawer (pause menu, Settings, Profile, How to play; `DrawRecordsSection` draws the Records, Unlocks and Rank pages) and their slide-and-stagger animation |
| `RunEnding.cs`, `Recall.cs` | 0.25k, 0.2k | the run ending ("last breath") and going back to the lobby ("beam out") |
| `SavesUi.cs`, `HuntUi.cs` | 0.4k, 0.1k | Play picker in the menu box (`DrawPlayPicker`), the boss list the Records tab reads, unlock toasts; the lobby's Boss Hunt tab |

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
  arena (paths never apply to boss waves).
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
   (`KIT=<n> Tools/Preview/shot.sh <name> kitgrid`, `LOADOUT=...` in `lobby`), README, Armory (`Docs/Armory`).

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
- **Compile check:** `Tools/CompileCheck/check.sh` (mono `mcs` + Unity stubs). If the game starts using a
  Unity member the stubs lack, add it to `Tools/CompileCheck/UnityStubs.cs` **and** `Tools/Preview/Stubs.cs`.
  Missing so far and avoided: `Mathf.Lerp`, `Mathf.Tan` (use `System.Math`).
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
  - Single systems: `parry`, `aim`, `extra`, `knight`, `ninja`, `blood`, `paths`, `bazaar`, `practice`, `bosses`,
    `warden`, `singularity`, `mastery`, `classcards`, `showcase`, `ults`, `mods`, `hitfx` (hit flash and blast thinning), `nonshooters` (Fuse Runner, Hookman, Sapper), ... Reports (numbers, no pass/fail):
    `ttk`, `lab`, `levels`, `bulletcount`, `wavemix` (each wave's share of shooters, melee and support), `dps`, `ultdps`, `balance` (each damage upgrade's worth per pick and full
    stack in a boss, pack and spread fight; see `Docs/UpgradeBalance.md`), `upgradecov` (which weapon upgrades each
    weapon ignores, and each upgrade's damage gain), `laserdebug`. A new test: add it to its system's file, then to
    the `Suites` table (and to `FeatureTests` if it belongs in the feature suite).
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
  `DEVTAB=n` (F1 tab), `FROST=<0..1>` (a Frost Lance icicle that far through, with its splinters), `HAMMER=1` (Storm
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
- **Reference pages:** `Docs/` holds the Bestiary and Armory sources. After editing a Bestiary entry run `node
  Docs/Bestiary/build_data.js` so the in-game Field guide says the same. `Docs/Armory/build.sh` rebuilds the Armory
  (its icons come straight from `PixelArt.cs`) and `Docs/checks/*.js` are Playwright scripts that screenshot the pages
  and report page errors. See `Docs/README.md` for where they're published and how to update them; their numbers and
  descriptions are hand-copied from the code, so they go stale with balance changes. **Don't update them (or the
  Bestiary/Armory data and published pages) unless the owner asks for it** (see `CLAUDE.md`).

## 4. How the owner works (preferences learned over many sessions)

- Pushes go **straight to `main`** of `AleonSC/top-down-arena` (the remote reports it moved to
  `AleonSC/Top-down-arena`; pushing works). Commit messages end with the session's attribution lines; don't
  put model ids in commits.
- Requests are short and outcome-focused ("make the lance a rectangle", "do 1, 2"). Implement fully: code,
  tests, README, the Armory/Bestiary when relevant, bump the protocol if messages changed, then commit and push.
- They play-test in Unity and send **screenshots** and **session JSON uploads** ("this is a run I suicided in
  for early-game data"). Session analyses should give concrete numbers and a short list of proposed fixes,
  then ask which to do.
- They want things to look **polished** ("make it really nice", "fancy"), with live previews over names.
- Before deleting code, ask with a question box.
- For bigger features they like to **plan first** (design doc with their decisions, e.g. `Docs/Paths.md`), then
  say "start on step N" one step at a time. Each step: code, tests, previews, README/doc status, push, then a
  short report naming the protocol version and what's next.
- For visual choices they like **mockups to choose from** (e.g. the button styles A-F; they picked F).

## 5. Recent history

Moved to [`Docs/History.md`](Docs/History.md) (newest first). Read it only when you need the background to a
feature; `git log --oneline` is usually enough.

---

## 6. Audit findings (October 2026)

### 6.1 Dead code — already removed

The codebase is kept tidy as features change, so very little was dead. Removed (with the owner's OK): `Game.HavenHeal`
(unused constant), `NetPeer.ConnectionCount` (never called), the Boss Lab seat picker's "no corner" option and a stale
"F1 > Practice Range" line in saved practice files (leftovers of the old F1 Practice Range). Scans covered unused
methods, fields that are written but never read, and unused enum values; nothing else qualified. The **simple test
bot** in `LocalControl` (`Bot` without a `BotLevel`) is **not** dead: most sim tests rely on its predictability (and
the `--bot` launch option uses it). F1 "Bot plays for you" uses the smart bot since Oct 2026.

### 6.2 Where coding is slow: one feature, many places (highest value)

Adding or changing one weapon, ability or enemy means editing many scattered `switch` statements and hand
copies of the same numbers:

| Adding a... | Touches |
|---|---|
| **Weapon** | its `WeaponDef` in its class's file (since Oct 2026: lobby place, numbers, sound, area and card texts; before, `Defs` held the definition and numbers: `Speed`, `Life`, `Damage`, `Reach`, `Grow`, `BotRange`; `WeaponRange`, `MeleeRange` and the bots read them since Oct 2026) · `FireVolley` / `FireKitWeapon` / `FireKnightWeapon` / Ninja / Blood Mage (behaviour) · a `WeaponRange` case only if it's unusual (bow, returning star, beam, sword waves) · draw code · `ExtraDesc` text · README · Armory · re-run `Tools/SimTest/run.sh 1 1 weapondps write` (the lobby's Damage stat, `WeaponDps.Generated.cs`) |
| **Ability** | `AbilityId` · its `AbilityDef` entry in its class's file (lobby place, name, cooldown, texts, capstone, instant, charges, bot rule; since Oct 2026) · its cast `case` next to the entry (`CastAbility` / `KitAbility` / `KnightAbility` / `NinjaAbility` / `BloodAbility` / `Ultimate`), including what its capstone does · `PixelArt` icon by name · README · Armory |
| **Enemy** | `EnemyType` + its `EnemyDef` in `Defs` (stats, blurb, picker group, late/heavy flags; since Oct 2026) · `EnemyAI` or `LateGame` (roster, move, fire, special) · `WorldEnemies.DrawEnemy` · README · Bestiary (then `node Docs/Bestiary/build_data.js` for the in-game one) |

**Recommendations**
1. ✅ **Done (Oct 2026).** **Put weapon numbers in `WeaponDef`** (shot speed, lifetime, damage, reach, per-extra
   growth). Then `WeaponRange`, bot ranges, upgrade texts, the Armory and balance tweaks read one place. `WeaponRange`
   currently duplicates every speed/life literal (guarded by the `aim` test, but still double work).
2. ✅ **Done (Oct 2026):** one `AbilityDef` per ability, kept beside its class's cast code (`SoldierAbilities` in
   `Abilities.cs`, `MageAbilities`/`SummonerAbilities` in `Minions.cs`, `UltimateAbilities` in `Ultimates.cs`,
   `KnightAbilities`, `NinjaAbilities`, `BloodMageAbilities`); `Upgrades.Abilities` gathers them and the lobby tables
   are built from each entry's `Kit`/`Role`/`Choice` (the game refuses to start if a place is empty or doubled; sim
   test `every ability has a complete entry`). Casts stay in each class's switch (owner's choice). **An ability
   registry**: one entry per ability with its cast function, bot heuristic, capstone and text, instead of 5+ parallel
   switches. Adding a Knight-sized class took most of a session because of this.
3. ✅ **Done (Oct 2026), data only (owner's choice):** each `EnemyDef` holds its Practice `Blurb`, its picker group
   (`PickerGroup`: the picker lists by group, then `EnemyType` order) and `Late`/`Heavy` flags; `BlurbOf`, `Choices`,
   `IsLateGame` and `Heavy` read them. AI, drawing, boss sounds and music stay per-type switches. **An enemy
   registry** in the same spirit (definition + blurb + draw style + AI hooks), so the Practice Range, Bestiary and
   README tables could be generated rather than hand-kept.
4. ✅ **Done (Oct 2026), the class side:** a class is a `KitDef` (`Classes.cs`) with hooks, its weapons and abilities
   are lists in its own file, the look is a `KitLook` line, and the `classes` sim test checks a class is complete; see
   "Adding a class" in section 2. The scattered per-weapon lists (`ShotSound`, `AreaWeapon`, the card texts) became
   `WeaponDef` fields.
5. **Generate the reference docs** (README tables, Armory/Bestiary data) from the definitions with a small
   tool, so balance changes don't need three manual edits.

### 6.3 File organisation

5. ✅ **Done (Oct 2026).** **Split `Game.cs` (3.3k lines)** into `GameNet.cs` (snapshot send/read, host/client
   messages, inputs), `Combat.cs` (`Collide`, damage, on-hit effects), `Abilities.cs` (`CastAbility`), `Waves.cs`
   (`TickWaves`, `BuildWave`) and `Weapons.cs` (`FireVolley`, `WeaponRange`). Pure moves: low risk, big readability
   gain. *Done as pure moves (every line kept, verified by line multiset). `BuildWave` was already in `EnemyAI.cs`
   with the roster and stays there; the scattered constants stayed at the top of `Game.cs` until item 14 moved them to
   `Balance.cs`.*
6. ✅ **Done (Oct 2026).** **Split `ArenaApp.cs` (2.8k lines)** into `WorldRenderer.cs` (`DrawWorld`, `DrawEnemy`,
   `DrawPlayer`, fx; the 351-line `DrawWorld` and 268-line `DrawEnemy` would also benefit from per-type helpers),
   `Hud.cs` (wave plate, ability bar, Tab menu, picker, run summary) and `Menus.cs` (main menu, settings, how-to).
7. ✅ **Done (Oct 2026).** **Split `Tools/SimTest/Program.cs` (3.7k lines)** into one file per system, replace the
   `args[2] ==` chain with a table of named suites, and add an `all` suite. **Rename the `music` suite** (it runs the
   whole feature suite, which is confusing) and make the default run everything. (Done as `Tests/*.cs`, the `Suites`
   table, `features` with `music` kept as an alias, and `all`; the default stays `core` so the usual two-step check
   doesn't run the core suite twice.)

### 6.4 Networking

8. ✅ **Done (Oct 2026):** the snapshot is one `Sync*` pass per entity over a `NetSync` (writes on the host, reads on a
   client); same bytes as before, so no protocol bump. **Snapshot read/write were hand-paired** (136 `Write` calls in
   `Game.cs`): adding a replicated field means editing `SendSnapshot` and `ReadSnapshot` in lockstep plus a protocol
   bump, and a mismatch silently corrupts the stream. A symmetric `Serialize(IStream)` per entity (one method that
   both reads and writes) would remove that whole class of bug.
9. ✅ **Done (Oct 2026):** scratch fields (`snapSeen`, `snapGone`, ...) in `GameNet.cs`. `ReadSnapshot` allocated
   several `HashSet`/`List`s **per snapshot** (20 Hz on clients); reuse scratch collections like `Collide` already
   does. Small GC win on the client.
10. Bandwidth is fine for 3 players over Tailscale (full state at 20 Hz, no bullets). Delta compression isn't
    needed unless player counts grow.

### 6.5 Gameplay performance

11. **Collision is all-pairs** (player bullets × enemies, enemy bullets × players). With the bullet budget it's
    cheap today (a few thousand checks per tick); a uniform grid would only matter if counts grow a lot
    (endless mode, more players).
12. Rendering is GL immediate mode with many small circles (8-48 segments). Fine on desktop; if frame time
    ever matters, lower segment counts for small bullets and batch the menu backgrounds.
13. ✅ **Done (Oct 2026):** the per-tick and per-swing lists are reused scratch fields: `Collide`'s dead enemies,
    `TickOrbs`, `TickMinions`, `TickZones`, `TickEnemies` (its copy and the separation pass), the Knight's `LandSwing`
    / `DeflectBullets` / `CutBullets` and `NetPeer.Broadcast`. Only methods that never call back into themselves were
    changed; the full seeded suite's output was identical before and after. Still allocating, on purpose (rarer, or
    could re-enter): `ShockPulse`, Chain Light's per-cone lists, `SendImps`, `RemoveTemporaryMinions`.

### 6.6 Balance tooling

14. ✅ **Done (Oct 2026), owner's choice "general rules + index":** `Balance.cs` holds the general numbers (moved
    verbatim from `Game.cs` and `EnemyAI.cs`) and its header lists where each class, boss and system keeps its own
    block (beside its code). Numbers written inline in code stay put; a dev-menu-reloadable file was not built.
    **Tunables are scattered constants** across `Game.cs`, `EnemyAI.cs`, `Knight.cs`, `Defs.cs`. A single `Balance.cs`
    (or a JSON file the dev menu can reload) would make tuning sessions much faster.
15. ✅ **Done (Oct 2026).** **Flaky tests** use the game's RNG: give each test a fixed seed so results are repeatable.
    (`Game.SeedForTests`; see section 3.)
16. ✅ **Done (Oct 2026):** the F1 toggle sets `BotLevel = BotSkill.Good` (and `Off` when turned off). Both bot kinds
    share the level-up pick/reroll and Bazaar shopping code (`FixedStep` in `Game.cs`), so the smart bot no longer
    stalls in a shop; sim test `f1bot`. The F1 **"Bot plays for you"** used to use the simple test bot; the Boss Lab's
    smart bot (`BotSkill.Good`) would make it far more useful for play-testing. Keep the simple bot for the sim tests.

### 6.7 Suggested order

Low-risk, high-value first: (5) and (6) file splits → (7) test suite layout → (1) weapon numbers in
`WeaponDef` → (8) symmetric serialisation → (2)/(3) registries → (14) balance file. Do each as its own commit,
running both test suites and the compile check after each.

### 6.8 Work plan (the owner's order, October 2026)

One step per commit, pushed to the session's branch; compile check and both suites must pass first (rerun known flakes).
Bump `Session.ProtocolVersion` and tell the owner whenever a message format changes. Preview any UI touched,
before and after. Ask before deleting code, changing behaviour, or growing a step beyond what's described.

1. ✅ Split `Game.cs` into `GameNet.cs`, `Combat.cs`, `Abilities.cs`, `Waves.cs`, `Weapons.cs` (item 5).
2. ✅ Split `ArenaApp.cs` into `WorldRenderer.cs`, `Hud.cs`, `Menus.cs`; `DrawArenaContents` and `DrawEnemy` broken
   into helpers (item 6). Checked byte-for-byte with `Tools/RenderCheck`. (`DrawArenaContents` was already pulled out
   of `DrawWorld` for the loadout preview.)
3. ✅ Reorganise `Tools/SimTest/Program.cs`: one file per system, a table of named suites plus `all`, rename the
   `music` suite, fixed seeds for the flaky tests (items 7, 15). Now `Tests/*.cs`, `run.sh list`, `features`
   (alias `music`), seeds per test (`SEED=random` to vary).
4. ✅ Weapon numbers into `WeaponDef` (`Speed`, `Life`, `Damage`, `Reach`, `Grow`, `BotRange`); the firing code,
   `WeaponRange`, `MeleeRange` and the bots read them (item 1). Proven unchanged: every weapon's bullets and swings
   at 0-3 extra projectiles, field for field, and the `aim` and `extra` outputs, identical before and after.
   Texts (`Desc`, `ExtraDesc`) still state their numbers by hand; the bots' lead-speed guesses (`ShotSpeedFor`) are
   left as they were (they differ from the real speeds, so reading `Speed` would change how bots aim).
5. ✅ Symmetric serialise per entity replacing `SendSnapshot`/`ReadSnapshot`; scratch collections reused
   (items 8, 9). The byte layout is unchanged, so **no protocol bump**: a seeded host/client harness compared every
   snapshot's bytes and the client's whole state after reading it, old code vs new, over 7 runs (all six classes,
   waves 5-40, forced hazards, shop, shelves, offers and Warden gates): identical. To add a replicated field, add
   one `s.Float(ref ...)` (etc.) line in the right `Sync*` method and bump the protocol.
6. Ability registry and enemy registry (items 2, 3). Design agreed with the owner: data entries, casts and AI stay
   in their switches; entries live in each class's file; enemies get data only (blurb, picker order, flags).
   ✅ Abilities: a fingerprint of all 77 abilities' data, the lobby tables, every upgrade card, max charges and
   ~15,000 bot decisions per ability was identical before and after. To add an ability: an `AbilityId`, an entry
   in its class's list, its cast `case`, an icon.
   *Formerly red test, fixed Oct 2026:* "enemy bullets stay near the bullet budget" sat on the edge (wave 21 peaks
   at 183-214 over random seeds, the test allowed 210). Only volleys check the budget; a Phantom's blink ring (12),
   a Charger's dash ring (10) and a Mortar bomb's burst (10-14) always fire. The owner chose to keep that and allow
   1.5x the budget in the test rather than change the game.
   ✅ Enemies: blurb, picker group, late and heavy flags in each `EnemyDef`; a fingerprint of every enemy's data,
   name, blurb, late/heavy checks and the picker's list was identical before and after.
7. **Divergent paths**: all 4 steps ✅ (framework, no-path option, all 9 paths). Arena rules: `ArenaRule()`, eased
   `NarrowF`/`FogF` in `Paths.cs`, which every game derives from the path, so nothing extra is sent; the playable rect
   is `ArenaMin`/`ArenaMax` (`ClampToArena`, `KeepInArena`, `RandomInArena`). Next: tune numbers in play. Designed
   with the owner, see [`Docs/Paths.md`](Docs/Paths.md) (decisions, the first pool of 9 paths, screens, engineering
   notes and a 4-step build order). Doesn't need the registries. Protocol bump.
8. ✅ Gather the scattered balance constants into `Balance.cs` (item 14). Pure move: a dump of all 323 constants on
   `Game` (name, value, visibility) was identical before and after.
9. ✅ F1 "Bot plays for you" uses the smart bot (`BotSkill.Good`); the simple bot stays for tests and `--bot` (item
   16).

Side requests done along the way (Oct 2026): lobby "!" connection popup, arena shows your lobby background,
live mini-arena loadout preview, aim line capped at range, background picker card, Vector Colosseum
main menu and How to play, Practice Range editor redesign, dev menu tabs, Field guide, button style F, Frost
Lance spear, stat pixel arrow, minimizable background picker, run modifiers. `Tools/CompileCheck/check.sh` fails
loudly when mono is missing.

**Boss approaches** ([`Docs/Approaches.md`](Docs/Approaches.md)): step 1 (framework + the Warden) is built. Next: the
owner plays waves 1-4 at Blend and Fully themed and picks a default; then step 2 (the Hive Queen) when they say so.

**Where to pick up:** the October 2026 audit plan is done (steps 1-9). Still open from the audit: item 4 (generate
README/Armory/Bestiary tables from the definitions), 10-12 (networking and rendering notes). Paths are
feature-complete but untuned: ask the owner for play-test feedback (Frozen Lake's grip, Fogbound's sight 350 px,
Narrow Pass's 1120 × 720, fire damage) and session JSONs, which record each wave's path. Open questions in
`Docs/Paths.md`: whether waves 1-4 should also get a path choice. Planned in the design but not built yet: a route
strip in the Tab menu, the route in the run summary (the route only shows on the Choose-your-path screen today), and
`Tools/SessionReport` grouping damage and downs by path.

**Back to the lobby from a run: a confirm, then the beam-out (Nov 2026), protocol 124:** owner's ask. `MenuDrawer.cs`:
the pause menu's Leave to lobby sets `pauseConfirmLobby` and `DrawLobbyConfirm` swaps the drawer for "TO LOBBY?" (Back
to the lobby / Keep playing, `confirmSel` starts on Keep playing; ↑ ↓ Enter; Esc says no in ArenaApp) with a note on
whether the run is saved; the run card stays. Yes calls `RecallToLobby` (`Recall.cs`), as do the results screen's Back
to lobby and its L key. `Session.StartRecall` (host) broadcasts the new `Msg.Recall` (appended to the enum) and bumps
`Session.Recalls`; clients bump it on the message. `TickRecall` notices the count change in the game and runs
`recallT` (1.7 s): menus close (`PauseMenuWanted` is false while `RecallActive`), the HUD, wave banner
(`LaunchHoldsBanner`), results card and "Press H" hint hide, `HoldControls` is on, each player gets a pillar
(`DrawPlayerBeam`, now shared with LaunchIntro) and vanishes up it (`RecallHides`, folded into `LaunchHides`), then a
ring closes on the centre as `DrawRecallFade` darkens the screen. At the end the host calls `ReturnToLobby`; a client
stays dark until ToLobby lands. In the lobby `returnT` (1.3 s) drives `DrawLobbyReturning`: the dark lifts, the party
column slides in from the left and the rest from the right (`LobbyPiece`, Repaint only), then `DrawPartyCardRing`
(split out of LobbyEnter). Sounds reuse the launch's (beam, land, ripple, sweep, HUD swish). Previews: `PAUSECONFIRM=1
shot.sh <n> pause 3`, `RECALL=<s> shot.sh <n> hud 3 0 2`, `DOWNED=down:0.8|revive:0.6|up:0.3 shot.sh <n> hud 6 12.2 2` (you going down, being revived, standing back up; DownedLooks.cs), `RETURN=<s> shot.sh <n> lobby 1 2 2`.
