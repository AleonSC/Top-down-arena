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
| `Ninja.cs` | 0.45k | the Ninja: `FireNinjaWeapon`, `NinjaAbility`, Substitution's `MovePlayer` (`Msg.Teleport`), Shadow Step's landing, clones and Vanish (`NinjaTarget`), Smoke Bomb's slowed bullets, `TickNinjas` |
| `BloodMage.cs` | 0.55k | the Hemomancer (`Kit.Hemomancer`): `FireBloodWeapon`, Implosion (`ImplodeTarget` picks the enemy under `Player.AimAt`, `ImplodeReady` holds fire without one, `pendingImplosions`/`ResolveImplosions` from `Collide`, `FxKind.Implode`), `BloodShotHit` (leech, pools, orb drops), health costs (`PayHealth`, `PayAbilityCost` from `HostAbility`, `castPaid`), Blood Price (`BloodDamageMult` in `HurtEnemy`), `BloodLifesteal`, `BloodAbility`, Crimson Pact / Blood Rite hooks in `Damage` (`BloodCut`, `BloodTookHit`), `TickBloodMages` (Pact burst, Frenzy drain, Apotheosis payout, Blood Boils) |
| `TextOverrides.cs` | 0.15k | the dev text editor's edits: `Apply` (what to show; digit runs are "#" in keys, so numbers stay the game's), `Problem` (an edit must keep its numbers), `TextOverrides.txt` load/save |
| `Showcase.cs` | 0.2k | the lobby's loadout preview: `Showcase` runs a private offline `Game` (`Session.HostOffline`, `NetPeer.Offline`) with 3 target dummies; `Game.ShowcaseMode` hooks skip waves, freeze enemies, and block XP/mastery |
| `Difficulty.cs` | 0.25k | the run's difficulty: presets (Easy / Normal / Hard / Nightmare) and Custom, every knob's range and unit, `Game.Diff` / `D(DiffKnob)`, `EnemyDamageScale`; the lobby tab is `DifficultyUi.cs` |
| `Balance.cs` | 0.07k | **the general balance numbers** (waves, rerolls, big health orbs, revive range, contact damage, the Healer's aura, Soldier and card-only abilities, Mage spell range), and an index of where every other system's numbers live |
| `EnemyAI.cs` | 1.0k | enemy roster per wave, movement, firing patterns, contact damage, boss specials, bullet budget |
| `LateGame.cs` | 0.45k | bosses 25-50 (Colossus → Eclipse; most of each now lives in its own file) |
| `Eclipse.cs` | 0.6k | wave-50 final boss in five stages (Corona / Penumbra / Umbra / Echoes / Last Light + Totality): `EclipseState` (synced by `SyncEclipse`), the moon and its shadow, flares, closing rings, sunspots and the dark, crescents, the echoes, the burning edge, the nova. Look: `EclipseLook.cs` |
| `Warden.cs` | 0.7k | wave-5 boss, a prison riot in three stages (Count / Lockdown / Riot + last stand): turn tables, searchlight + stun + shackle ring, gates of push bullets (`WardenGatePush` runs on each player's own game), tear gas (a `PathPatch`), rioters, riot shield, baton charge; `WriteWarden`/`ReadWarden` after the boss's snapshot record |
| `Reapers.cs` | 0.7k | wave-15 Twin Reapers in three stages (Twilight / Dual Reaper / Sundered) on one `ReaperFight` (game-level, `SyncReapers` in the snapshot): tether sweep that follows its target, the fusion (twins removed, `EnemyType.DualReaper` holds their health; power-up blasts) and the split, twin lasers, `EnemyType.ReaperPillar` (each killed leaves a hole), the suck-in (the Singularity's `PullStrength`), the chase and trap (shield, then open; `DualReaperHit` armor), reaps; drawn in `ReaperLook.cs` |
| `Queen.cs` | 0.6k | wave-10 boss, a hive in three stages (Swarm / Honeycomb / Frenzy + last stand): drones (shield; leftovers fly home and heal), bombers, eggs (`EnemyType.HiveEgg`), hex cells (`HiveCells`, same on every game), nectar spray (`Defs.HoneyShot`, `PatchKind.Honey` slows on your own game), swarm cloud (bee positions computed from the cloud and the clock on every game), royal dive (immune in the air), bomber barrage; `WriteQueen`/`ReadQueen` after her snapshot record; drawn in `QueenLook.cs` |
| `Singularity.cs` | 0.25k | wave-20 boss: corner wells, lasers, cages |
| `Colossus.cs` | 0.6k | wave-25 boss in three stages (Awakening / Crumbling / Molten Core + last stand): `ColossusState` (synced by `SyncColossus`), stomp rings of push blocks (`StompRing`, pushed on your own game in `ColossusPush`, hurt on the host), drops (boulders, meteors, pillars), fissures, plates (`ColSlab`, stop bullets both ways in `Collide` via `ColossusCoverStops`), charge + daze (`ColDazeMult` in `HurtEnemy`), lava patches (`PatchKind.Lava`). Look: `ColossusLook.cs` |
| `Minions.cs` | 0.93k | Elementalist and Summoner abilities (`KitAbility`), minions (`SpawnMinion`, `TickMinions`) and ground zones (`AddZone`, `TickZones`) |
| `KitWeapons.cs` | 0.93k | Elementalist and Summoner weapons (`FireKitWeapon`) and what they leave behind: bleeds, chain lightning, seeds and vines, sent minions, orbiters, refraction, ion trails, the wolf pack, arc fields, light cones, bone cages, boulders, shocks |
| `Knight.cs` | 0.6k | Knight melee: swings (`Swing`, `LandSwing`, Lance box), sword waves, Knight abilities |
| `Ultimates.cs` | 0.15k | the 12 ultimates |
| `Mastery.cs` | 0.2k | ability XP/levels and capstone helpers (capstone texts are in each `AbilityDef`) |
| `Knock.cs` | 0.05k | pushes slide: `KnockEnemy(e, delta)` (every shove goes through it), `TickKnock` (from `TickEnemies`); a `FxKind.Knock` draws the streak and dust (`WorldEffects.DrawKnockFx`) |
| `Nimbus.cs` | 0.07k | the Nimbus (Healer Elementalist; replaced the Prism Beam): the cloud is a companion `MinionKind.Nimbus` steered to your cursor within `NimbusReach` of you (`NimbusGoal`, in the minion move switch); `TickNimbus` heals allies in its 70 px rain; a click is `NimbusStrike` (a 40 px blast under the cloud, `FxKind.Strike`; extra projectiles = more strikes). Looks: `NimbusLook.cs` |
| `Chalk.cs` | 0.28k | Spirit Chalk (Healer Summoner; replaced the Totem Staff): hold = `DrawChalk` lays `InkDot`s along the tip (700 px/s, 600 px of ink; at the cap `EraseOldest` fades your oldest dot to make room, and breaks its loop if it was in one); `InkTouches` burns/slows enemies, heals players and destroys enemy bullets crossing ink (wearing it); `TryClose` finds a loop in a stroke and seals it as an `InkPen` (enemies inside get `PenId`; bosses and mini-bosses excluded); `EnforcePens` keeps them inside; `SyncInk` sends the dots and each drawer's live tip in each snapshot (`InkTip`; `UpdateInkView` eases `InkTipDisp` and ages a client's ink between snapshots; `Enemy.Penned` is replicated). Looks: `ChalkLook.cs` |
| `DpsTools.cs` | 0.15k | the DPS meter's data (`DpsTrack`: a 10 s ring of 0.1 s slices; `TickDps` reads each player's `Run.DamageDealt` gain per tick, `DotDamage` reports the over-time share) and target dummies (`Enemy.Dummy`: a pinned Juggernaut refilled in `TickDummy`; `Foes()` is the enemy count that ignores them, used by the wave-clear checks). Host only, no network |
| `Parry.cs`, `Pause.cs`, `DevTools.cs`, `StatSheet.cs`, `RunStats.cs`, `Kits.cs` | small | as named |
| `BossSounds.cs` | 0.1k | soft audio cues when bosses start attacks |
| `Paths.cs` | 0.46k | divergent paths: `PathDef` registry (`Paths.All`), offer/choice (`OfferPaths`, `ChoosePath`, `Msg.PathOffer`/`PathChosen`), enemy mixes (`BuildPathWave`, `ApplyPathToEnemy`), bonus/prize (`PathBonus`, `FinishPath`), hazard patches (`PathPatch`, `TickPaths`, `Msg.PathPatch`), arena rules (`ArenaRule`, `NarrowF`/`FogF`, the playable rect `ArenaMin`/`ArenaMax`) |
| `Props.cs` | 0.5k | the arena's props: crates (cover), pots, barrels, the treasure chest and each player's private item; spawning by wave, bullet stops, blasts, push-out, the wave waiting for treasure, `Msg.Loot`, sync (look: `Game/PropsLook.cs`) |
| `Shop.cs` | 0.3k | the Bazaar: gold per kill (`GrantGold`, `GoldPerXp`), `OpenShop` / per-player shelf (`RollShelf`), `ShopAction`s (`Shop` → `Msg.Shop` → `HostShop`), giving gold (`Msg.ShopGift`, notices), `CloseShop` (leftover gold → XP), revive token, `BotShop`, snapshot fields |
| `Modifiers.cs` | 0.07k | `RunMod` flags and `ModDef`s; `Game.Mods` / `Has()`; sent in the lobby message |
| `Practice.cs` | 0.2k | Practice Range plan (`PracticePlan`/`PracticeWave`), text format, the picker list (`Choices`, built from each `EnemyDef.Picker`), building a practice wave |
| `BotBrain.cs` | 0.4k | the "smart" bot (Boss Lab): dodging, positioning, aiming, ability use (`BotSense`; each ability's rule is its `AbilityDef.Bot`) |
| `BossLab.cs` | 0.25k | Boss Lab sessions and stats |
| `Classes.cs` | 0.08k | the class registry: `KitDef` (name, blurb, icon, `HpMult`/`SpeedMult`, hooks `Fire`, `Ability`, `Tick`, `TickEnemy`, `OnShotHit`, `OnKill`), `Classes.All` (Kit order, checked at start-up) and the gathered hook lists the game loop runs |
| `Approaches.cs` | 0.15k | boss approaches: `FactionDef` per boss, `ApproachShare`, `BuildApproach`/`MixApproach` (see `Docs/Approaches.md`) |
| `WardenFaction.cs` | 0.3k | the Warden's faction: Riot Shield (shield, bash, `Msg.Knock` knockback, the searchlight pair, `HerdSpot`) and Gas Grenadier |
| `Defs.cs` | 0.3k | role, weapon, enemy and bullet definitions (each class's `WeaponDef`s live in its own file; `Defs.Weapons` gathers them) (stats, colours; each enemy's `Blurb`, `Picker` group, `Late` and `Heavy` flags) |
| `BestiaryData.cs` | 0.1k | the in-game Bestiary's words (`BestiaryEntry`, `BestiaryAttack`); most come from `BestiaryData.Generated.cs`, built from the Bestiary page by `Docs/Bestiary/build_data.js` |
| `Upgrades.cs` | 0.4k | upgrade cards, `AbilityDef` and the ability registry (`Upgrades.Abilities`, gathered from each class's file; the lobby tables `RoleAbility`/`Ultimate` are built from the entries), per-weapon/Knight descriptions |
| `Session.cs` | 0.7k | hosting/joining, lobby, ready state, **message enum** (`Msg`), protocol version, practice sync |
| `Net.cs` | 0.26k | `NetPeer`: length-prefixed messages over TCP (`NoDelay`) |
| `SessionRecorder.cs` | 0.5k | the session JSON recorder |
| `MathTypes.cs` | 0.08k | `V2`, `Rgba`, `MathX` |
| `Challenges.cs` | 0.85k | challenge rooms: the portal every five waves, Defend the Crystal, the Gatekeeper, Kill Rush, Zones, Mirror (shadows); the altar's prize |
| `Variety.cs` | 0.94k | the second half's enemies (Beam Sentry, Scorcher, Reaver, Warder), shooting-pattern enemies, elite traits, squads |
| `Readability.cs` | 0.11k | readable regular waves: shooters take turns (`AssignShooterTurns`), bullet looks sent with each spawn |
| `BuildPower.cs` | 0.1k | boss health that keeps up with strong builds (a build's power vs a typical build at that wave) |
| `Duelist.cs`, `Lich.cs`, `Herald.cs`, `Devourer.cs` | 0.64-0.82k each | the three-stage Duelist (wave 30), Lich (35), Storm Herald (40) and Devourer (45): stage, turns, synced state |
| `Stun.cs`, `QueenLoot.cs`, `ReaperLoot.cs` | 0.26-0.28k each | boss legendaries: the Warden's (Stun Gun, Warden's Guard), the Queen's (Hive Drones, Royal Dive, Bomber Escort), the Reapers' (Twin Scythes, Final Reap, Scythe Throw) |
| `RadiantOrb.cs`, `Repulsion.cs`, `SoulGhost.cs` | <0.1k each | the Elementalist Healer's Radiant Orb, the Soldier Healer's Repulsion Cannon, Soul Link's ghost |
| `Hunts.cs` | 0.07k | Boss Hunt mode (ten waves: nine of the boss's faction, then the boss); hunt-aware `IsBossWave`/`BossAt`/`NextBossWave` |
| `Zombies.cs` | 0.3k | the Zombie Infection modifier: hordes, the dead rise, and the seven zombie kinds' AI (`ZombieMove`, `ZombieSpecial`, `ZombieFire`; Crawler grab via `StunT`, Spitter acid and Bloater spores as patches) |
| `WaveDesign.cs` | 0.4k | the wave editor's designs (`WaveSet`/`DesignWave`/`DesignGroup`/`DesignProp`, spawn points A-E), the `.waves` text format, playing them in the Practice Range (`Game.Design`, `BuildDesignWave`, `TickDesign`) |
| `RunSaves.cs`, `SaveData.cs`, `Profile.cs` | 0.25-0.32k each | saved runs (written as each wave starts), the profile, records and `Unlocks.All` |
| `WeaponDps.Generated.cs` | 0.07k | generated by `run.sh 1 1 weapondps write`; don't edit by hand |

### Game (front end) — `ArenaApp` is a `MonoBehaviour` partial class

| File | Lines | What it holds |
|---|---|---|
| `ArenaApp.cs` | 0.45k | boot, command line, input and the frame loop (`Update`, `FixedUpdate`), layout/scaling, `OnGUI` dispatch, small shared helpers |
| `WorldRenderer.cs` | 0.74k | **world rendering in GL**: `OnPostRender`, `DrawWorld` (floor) → `DrawArenaContents` (one helper per layer: auras, shots, zones, orbs, enemies, players, effects), `DrawEnemy` (layers: underlay / status / body / details, with per-type helpers), `DrawPlayer`, `DrawKnight`, aim line / bow draw / cylinder overlays, GL primitives and clipping (`Tri`, `UiClip`, `BeginUiGl`/`UiModelAt`) (the frame, auras, parry shield, orbs, aim line and the GL primitives stay here; the rest is split by what is drawn into the four files below) |
| `WorldShots.cs` | 0.37k | player shots (`DrawSmallShot`, `DrawNinjaShot`, fireballs, boulders, hammers), `DrawShotMods`, charge overlays (bow draw, hammer/boulder charge, revolver cylinder) |
| `WorldEffects.cs` | 0.56k | `DrawEffect` and every `FxKind` look (repulse, shatter, slashes, claws, chains, arc fields, light cones, icicles, jabs, bolts) |
| `WorldEnemies.cs` | 0.83k | Singularity hazards, `DrawZone`, `DrawMinion`, clones, `DrawEnemy` with its underlay, tells, details and status icons |
| `WorldPlayers.cs` | 0.2k | `DrawPlayer` (footprint ring, the model, the hit-core ring), `DrawKnight` and `DrawNinja` (their ability overlays) |
| `Hud.cs` | 0.49k | HUD (layout "C", Oct 2026: wave panel left of the ability bar, your card right of it with teammates as slim rows above, `DrawWaveCorner`/`DrawPlayerCorner`/`DrawTeammateRow`; boss bars wide at the top middle): wave plate, player cards, boss bars, ability bar, mastery toasts, Tab menu, upgrade picker and cards, `StatsSidebar`, banners, run summary (Tab menu, boss bars, picker and summary are in the four files below) |
| `HudTab.cs` | 0.24k | the Tab menu (`DrawTabMenu`, stats, abilities, upgrades pages) |
| `HudBossBars.cs` | 0.43k | `DrawBossBars` and each boss's stage names and notes |
| `HudPicker.cs` | 0.45k | `DrawUpgradePicker`, trophy and upgrade cards, the stat panel and sidebar |
| `HudSummary.cs` | 0.25k | the end-of-run summary (`DrawRunSummary`, `DrawPlayerSummary`, `SummaryRows`) |
| `Menus.cs` | 0.5k | main menu ("Big play", `Docs/Mockups/menu-d.html`: title on its axis with the shot's arc (`DrawMenuArc`) and a changing chip under it (`MenuTip` picks the line, `DrawMenuTipChip` draws it: tag, topic, rest, in a `Ui.Capsule`); the box `MenuCard` with Host, Join and Continue (`DrawMenuCardInside`, Join's address row); the bottom bar with the name field, Records, Settings, Quit (`DrawMenuSecondary`); name, port and last address saved (`Load/SaveMenuFields`)), How to play, pause menu, settings and audio |
| `MenuIntro.cs` | 0.1k | the main menu drawing itself in the first time it shows (pen plot, option B): `menuIntroT`, `TickMenuIntro` (any key or click skips), `MenuIntroK(a, b)` that every piece asks, the ink-then-fill title and the pen tip. Preview: `MENUINTRO=<s> Tools/Preview/shot.sh <name> menu` (`JOINOPEN=1` for Join's row) |
| `LobbyUi.cs` | 0.5k | the lobby: "!" connection popup by the title (port, invite addresses), the hero card's live mini-arena (`DrawShowcase`, stepped from `FixedUpdate`), mode switch (left column, under the title), party cards with live characters, loadout with hero preview, bottom bar |
| `PracticeUi.cs` | 0.45k | Practice Range editor (live enemy tiles, shared edits, save/load) |
| `ZombieLook.cs` | 0.2k | the zombies' shapes, haze, drips, frenzy streaks, the Crawler's vines, acid and spore patches; the dark green arena colours |
| `WaveEditorUi.cs` | 0.7k | the F1 wave editor (option B of `Docs/Mockups/wave-editor.html`): arena map (spawn points, props, the wave at the playhead), palette, group settings, timeline (drag clips), save/load `.waves`, Test |
| `MenuBackgrounds.cs` | 0.34k | 10 animated menu backgrounds + the lobby's picker card (bottom left: live thumbnail, arrows, dots); your pick is also drawn dimmed behind the arena (`DrawWorld`, `ArenaBgDim`) |
| `DevTextEdit.cs` | 0.15k | F4 text-edit mode: banner, pick a text by clicking (`TextEditBeginGui` in `OnGUI`), the edit box; `Ui.Shown` (called by every label and button) applies edits and finds the text under the mouse. `Tools/TextEdits/fold.py` writes edits into the code |
| `BossLabUi.cs`, `DevMenu.cs` | 0.3-0.4k each | F1 tools (`DevMenu`: sidebar tabs Match / Players / Tools / DPS, card layout) |
| `DpsMeterUi.cs` | 0.1k | the F2 DPS meter overlay (`DrawDpsMeter`) and the F1 DPS tab (`DrawDevDps`) |
| `PathsUi.cs` | 0.48k | Choose-your-path cards (live floor + enemies, skulls, rules, bonus, prize, "No path"), each path's floor pattern (`DrawPathFloor`), fire/pool patches, Narrow Pass walls (`DrawNarrowWalls`) and Fogbound mist (`DrawFog`, per-vertex alpha grid), HUD path chip |
| `ShopUi.cs` | 0.3k | the Bazaar's shop screen (`DrawShop`): shelf cards, services, remove-upgrade list, Give popup, Done button and status, host close button |
| `ModifiersUi.cs` | 0.08k | the lobby's Modifiers tab |
| `RunStatsUi.cs` | 0.4k | the Run stats screen (Tab, or the pause menu's Run stats): tabs Run so far (header chips, totals strip, `BuildRoute`, fight and party-share cards), Your build (`DrawTabMenu`), Team (`DrawMateCard`) |
| `ReferencePages.cs` | 0.03k | the published Bestiary / Armory URLs (`OpenReferencePage`, the guide's Web page button) and `FieldGuideColor` |
| `FieldGuide.cs` | 0.8k | the in-game Bestiary and Armory: pages (`GuideBestiary`, `GuideClasses`...), lists built once from `BestiaryData` / `Defs` / `Upgrades` (`GuideItem`), search, the list with live models, one `Draw...Entry` per page, the scrolling body helpers (`GText`, `GHeading`, `GLink` jumps to another entry) |
| `Ui.cs` | 0.5k | IMGUI style kit (panels, buttons, choices, bars, fancy text). Buttons are the owner's chosen style "F": icon cards (`Choice` with an icon tile, `Button` with an optional icon chip), `FancyButton` for menu showpieces, `PulseRing`, tinted `Icon` |
| `Ambience.cs` | 0.25k | more local effects: orb trails and pickup pulses, dust puffs, each path's weather (`WeatherFor`) |
| `ShotLooks.cs` | 0.2k | new looks for player weapons' shots with Visual effects on (`ShotWeapon` picks out a weapon's own shot; `DrawWeaponShot` from `DrawBigShot`/`DrawSmallShot`; `EmitFromShot` from `UpdateJuice`; `DrawJuicyBolt`; the Prism split in `BulletEnding`). Preview: `SHOTLOOKS=1 Tools/Preview/shot.sh <name> hud 3 4` |
| `GunBowLooks.cs` | 0.4k | Revolver and Mender's Bow with Visual effects on (look A of `Docs/Mockups/revolver-bow-animations.html`): `OnRevolverShot` (called from `UpdateJuice` when a Pistol round appears: flash, smoke, brass, kick via `GunLook`), `UpdateBowLooks` (notices a release: string snap and ring), `DrawBowFlex` (the held bow), `DrawBowFullPop`, `DrawArrowStreak`; particle kinds `PMuzzle`/`PCasing`/`PSnap` drawn by `DrawGunPart`. Preview: `BOW=0.9`, `REVOLVER=1` |
| `MeleeLooks.cs` | 0.4k | melee attacks with Visual effects on (style E of `Docs/Mockups/melee-attacks.html`): `DrawSlashE` / `DrawJabE` (called first by `DrawSlashFx` / `DrawJabFx`; false = the old look) find the player who swung (`MeleeOwnerOf`); the weapon (`DrawMeleeModel`) and sparks in the normal pass, the energy (`GlowArcBand`, beams, the Lance box, the hammer's trail and impact) in the glow pass (`DrawMeleeGlow`, from `DrawGlowPass`; the lobby preview runs it too, clipped); the Halberd sends one Slash per sweep (`Swing.FxHalf`); the Warhammer's overhead swing and slam (`DrawHammerSlamE`, raised while charging: `DrawHammerRaised`). Preview: `SWING=<s>` with `SWINGW=<WeaponId>` |
| `BearLooks.cs` | 0.45k | the Bear Totem in style A of `Docs/Mockups/bear-totem.html`: a claw on each hand (`DrawBearModel`, from `DrawPlayerModel`; the left rakes across, the right rakes back; which hand and how far come from the swipe's own `FxKind.Claw` effect, found with `BearSeenOf`, so nothing is sent), the claw trails (`DrawBearClawFx`, with Visual effects on), the totem as a medallion on the back, the Bear companion (`DrawBear`: strides from how far it moved, its jaw snaps when it's next to an enemy and when that enemy is hurt) and claw scars on bleeding enemies (`DrawBleedScars`, from the replicated `Enemy.BleedHits`). Preview: `SWING=<s> SWINGW=BearTotem` (`BLEEDHITS=<n>`) |
| `RadiantOrbLooks.cs` | 0.1k | The Radiant Orb's trail with Visual effects on (style B, Stardust, of `Docs/Mockups/radiant-orb-trails.html`): `UpdateRadiantStardust` (from `UpdateJuice`) follows each orb and Light Orb and sheds motes, `DrawRadiantStardust` (from `DrawGlowPass`) draws a faint tail, glows and four-point sparkles. Front end only, no protocol change. Glow isn't drawn by the preview tool |
| `ChainLightLooks.cs` | 0.15k | Chain Light with Visual effects on (style A of `Docs/Mockups/chain-light.html`): `ChainLightDrawnAsRadiance` (called first by `DrawLightConeFx` / `DrawBoltFx`) hands the cone and its warm-coloured bolts to the glow pass, where `DrawChainLightGlow` draws the fan, god-rays, motes, beams and halos. Preview: `SHOTW=ChainLight SHOTT=<s>` |
| `ArcLightningLooks.cs` | 0.2k | Arc Lightning with Visual effects on (style A of `Docs/Mockups/arc-lightning.html`): `ArcLightningDrawnAsThunder` (called first by `DrawArcFieldFx`; `DrawArcCharge` draws nothing with effects on) hands the strip to the glow pass, where `DrawArcLightningGlow` draws the charge (halo, sparks off the staff, your own dashed strip with its pulse and marker: `DrawThunderCharge`) and the release (the spine bolt, forks, flash and the stun crackle on each enemy: `DrawThunderStrip`). The lobby preview runs the glow pass and now draws the charge too. Preview: `SHOTW=ArcLightning SHOTT=<s>` (release) or `SHOTC=<0..1>` (charge) |
| `HealthBars.cs` | 0.1k | the damage ghost on every health bar (`GhostBar`, `Ghost`), and the slim boss bars when the HUD is hidden (H) |
| `Juice.cs` | 0.8k | visual effects, all local: glow pass, shake, hit-pause (frame replay), shatter, particles, trails, scorch, ripples, mood, damage numbers, level-up bursts, boss entrance card; their settings |
| `HudTiles.cs` | 0.15k | the ability bar's tiles react ("Snap"): `TickTiles` (Update) finds ready / charge back / use from each slot's `Charges` against last frame's, `TileKeyPressed` (the key handlers) a press while recharging; `TileLookFor` gives `DrawAbilityTile` (Hud.cs) its pop, shake, rim flashes, READY slide and pip pop; `TileTransformed` scales a tile round its centre on top of the current `GUI.matrix` |
| `ApproachUi.cs` | 0.04k | the approach chip above the wave panel and the banner's line |
| `CharacterModels.cs` | 0.6k | the player models (`Docs/Mockups/character-models.html`, colour-by-class): `DrawPlayerModel` draws back piece, shoulders, role trim (`ModelRoleTrim`), arms, the held weapon (`DrawHeld`, one case per `WeaponId`; `HoldOf` gives the pose) and head (`ModelHead`) in a local frame (`MW`, `MLine`, `MPoly`...; x along the aim, y to the right, Tank units, BodyR = 18). Class palettes in `ModelPals`, role colour only in the trim. Preview: `Tools/Preview/shot.sh x kitgrid` with `KIT=0..5` |
| `ClassLooks.cs` | 0.05k | one `KitLook` per class (ability and buff overlays drawn over the model, and auras under them), in Kit order |
| `AbilityLooks.cs` | 0.3k | telling what abilities do, for every class: callouts (each cast's name over its caster, `FxKind.Callout` from `HostAbility`; Settings > Ability names, `fx_callouts`), buff timers (`DrawBuffTimers`: a shrinking arc under the player per timed buff, the `Buffs` table; a new timed buff adds a line), taunt eyes over enemies (`DrawTauntMarks`, any taunt), beams to the allies an ability reached (`FxKind.Link`: Guardian's Oath, Lay on Hands, Lifeline, Arcane Ward, Sanctify), Arcane Surge, Blade Tempest and Alpha Call's frenzied minions. Preview any ability: `CAST="Roar" CASTT=1.5 Tools/Preview/shot.sh roar hud 6 9 2` (plays the ability's own class) |
| `BloodMageLook.cs` | 0.2k | the Blood Mage's look (hood, circling drops that glow brighter as health falls, weapon in hand, buffs), its shots, Siphon Field and Blood Pool zones, Blood Rite / Sanguine Rain auras; ability feedback: the health-cost spurt (`FxKind.Bleed`), Blood Shell's plates and forming, Crimson Pact's reach/countdown/stored blood (`PactStore`, `PactRadius` are replicated) and burst, taunted enemies' eyes (`DrawBloodTaunts`), Exsanguinate / Blood Gift / Martyr's blood streams (`FxKind.Drain`), Blood Boil's bubbling (`FxKind.Boil`), Blood Rite's lifelines. Preview any ability: `BLOOD=0 CAST="Crimson Pact" CASTT=2 Tools/Preview/shot.sh pact hud 6 9` |
| `Tooltips.cs` | 0.15k | hover tooltips on the Tab screens: Your build's weapon and upgrades, and every teammate's weapon, abilities and upgrades on the Team page (`TipClear` / `TipUpgrade` / `TipWeapon` / `TipAbility` / `TipMore`, then `DrawTip` last; upgrades read for that player's weapon via `Upgrades.DescFor`). Preview: `MOUSE=x,y [STATSTAB=2] Tools/Preview/shot.sh <name> tab` |
| `LobbyEnter.cs` | 0.15k | the main menu turning into the lobby: the menu's box grows into the loadout, the title flies off as LOBBY, the bottom bar (with your name card) drops away and fades, and the lobby's pieces slide and fade in. Preview: `LOBBYIN=<s> Tools/Preview/shot.sh <name> lobby` |
| (pause sounds) | — | the pause menu's signal-glitch sounds are in `PauseGlitch.cs` (`PauseSounds`, from `StepPauseAnim`): `Sfx.PauseTear`/`PauseType`/`PauseSettle`/`PauseClose`, local only. Hear them: `Tools/SoundCheck/render.sh Pause` |
| (launch sounds) | — | the run launch's sounds are in `LaunchIntro.cs` (`LaunchSounds`, from `TickLaunch`): `Sfx.LaunchSweep`/`LaunchRipple`/`LaunchBeam`/`LaunchLand`/`LaunchHud`, local only. Hear them: `Tools/SoundCheck/render.sh Launch` |
| `PickSounds.cs` | 0.05k | the upgrade picker's sounds, local only (`TickPickSounds` from Update plays each as the opening/close passes its moment; recipes `Sfx.PickRise`..`PickAbsorb` in SoundBank.cs, appended to `Sfx` and never sent). Hear them: `Tools/SoundCheck/render.sh Pick` |
| `PickIntro.cs`, `PickOutro.cs` | 0.3k | the upgrade picker opening out of your character and closing back into it (see "Upgrade picker opening and close" below) |
| `PixelArt.cs` | 1.15k | 16×16 pixel-art icons as text grids |
| `SoundBank.cs`, `GameMusic.cs`, `MenuMusic.cs` | 1.3k | every sound and all music synthesised in code |
| `WardenLook.cs`, `StageShiftLook.cs`, `SingularityLook.cs`, `LichLook.cs`, `DuelistLook.cs`, `HeraldLook.cs`, `DevourerLook.cs` | 0.1-0.6k each | each boss fight's look (`StageShiftLook`: the Warden's and Queen's 2 s stage changes) |
| `QueenLootLook.cs`, `ReaperLootLook.cs` | <0.1k each | how the Queen's and Reapers' legendaries look |
| `VarietyLook.cs` | 0.26k | the second half's enemies and elite trait markers |
| `ChallengeLook.cs` | 0.5k | challenge rooms on screen: portal, room floor, crystal, zones, altar, HUD cards |
| `Keybinds.cs`, `KeybindsUi.cs` | 0.3k, 0.1k | the rebindable controls (Fire and Parry too; they default to the mouse buttons and `ArenaApp` blocks each while a menu is up): `KeyAction` enum, defaults (weapon secondary = Left Alt), PlayerPrefs save (`key_<Action>`), `Keybinds.Tick` captures the next key while rebinding (`Busy` swallows Esc and gameplay keys that frame), `Down`/`Held` for the game, `SlotLabels` for the ability bar; Settings → Keybinds draws the page. `PlayerInput.WeaponAction` is local (not networked, no protocol bump); `Ninja.cs` `LocalWeaponDash` uses it for the Kunai blink, Twin Blades dash-strike, Shuriken recall, Bo Staff vault and Kusarigama reel |
| `MenuDrawer.cs`, `WindowAnim.cs` | 1.1k, 0.2k | the menu windows as a side drawer (pause menu, Settings, Records, How to play) and their slide-and-stagger animation |
| `RunEnding.cs`, `Recall.cs` | 0.25k, 0.2k | the run ending ("last breath") and going back to the lobby ("beam out") |
| `SavesUi.cs`, `HuntUi.cs` | 0.23k, 0.1k | Continue list, Records, unlock toasts; the lobby's Boss Hunt tab |

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
    `warden`, `singularity`, `mastery`, `classcards`, `showcase`, `ults`, `mods`, ... Reports (numbers, no pass/fail):
    `ttk`, `lab`, `levels`, `bulletcount`, `dps`, `ultdps`, `balance` (each damage upgrade's worth per pick and full
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
- **Screenshots without Unity:** `Tools/Preview/shot.sh <name> <mode>` → `Tools/Preview/out/<name>.png`. The arena's
  triangles are drawn like the GPU (`Tools/Preview/raster.py`: colour blended across each triangle, alpha blending, the
  glow pass adding light, 2x supersampled; about 7 s); `FAST=1` gives the old quick look (each triangle flat in its
  first vertex's colour, no glow), which can't show gradients or glows, so don't judge effects with it. `SHOTW=<WeaponId>` fires that weapon three times (shots at 0.06 / 0.18 / 0.3 s) for comparing shot looks with
  `Docs/Mockups/projectiles.html`. `LINEAR=1` shows
  how a Linear-colour-space project would draw it (washed out); the game is meant for **Gamma** (see below). Modes: `hud`,
  `tab`, `picker`, `dev`, `labsetup`, `labpanel`, `lobby`, `lobbypr`, `practicehud`, `menu`, `howto`, `bg` (with
  `BG=0..9`), `pause`, `kitgrid`, `legendary`, ... Path flags: `PATHOFFER=1|2|3|4` (the choice screen with each pair
  of paths), `PATHON=<PathId number>` (in-arena look of that path; 8 = Fogbound, 9 = Narrow Pass), `PATCHES=1` (sample
  fire/pools). Others: `TEXTEDIT=1` (lobby in text-edit mode, outline under `MOUSE`) or `TEXTEDIT=<text>` (its edit
  box; `TEXTBUF` = typed), `LOBBYTAB=n` (lobby tab, e.g. Modifiers), `BGMIN=1` (minimized background picker),
  `DEVTAB=n` (F1 tab), `FROST=<0..1>` (a Frost Lance icicle that far through, with its splinters), `HAMMER=1` (Storm
  Hammers in flight and an electrified enemy arcing), `BOULDER=<draw secs>` (the Golem Staff charging, with its
  landing marker, and boulders in flight), `CAGE=1` (a Bone Guard cage, with guard shots), `WEAPONS5=1` (Fuma
  Shuriken, Chain Light, Sanguine Orb coating, Fireballs and Arc Lightning in one scene), `HEMO=1` (enemies with 1-4
  Blood Bolts stuck in them), `HOOK=1` (a Kusarigama hooked in an enemy, and a throw). Env flags: `KNIGHT=<weapon>`,
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

One step per commit, pushed to `main`; compile check and both suites must pass first (rerun known flakes).
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
live mini-arena loadout preview, aim line capped at range, background picker card, Cartesian Colosseum
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
shot.sh <n> pause 3`, `RECALL=<s> shot.sh <n> hud 3 0 2`, `RETURN=<s> shot.sh <n> lobby 1 2 2`.
