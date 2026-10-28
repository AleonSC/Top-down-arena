# Handoff: Top-Down Arena

Everything a new session needs to pick up this project: what the game is, how the code is put together,
how to test and preview changes without Unity, how the owner likes to work, and the findings of the
October 2026 code audit (with a prioritised list of improvements).

`README.md` is the player-facing and feature-level reference (every weapon, ability, enemy, boss, control
and tool is described there). This document is the developer-facing overview; read both.

---

## 1. The game in one page

- **What:** a 1-3 player **co-op top-down bullet hell** made in **Unity 6** (`6000.0.23f1`, built-in render
  pipeline). The owner plays it with a friend over **Tailscale** (one hosts, the other joins by IP).
- **A run:** 50 waves, a boss every 5 (Warden, Hive Queen, Twin Reapers, Singularity, Colossus, Duelist,
  Lich, Storm Herald, Devourer, Eclipse), then optional endless mode. Level-ups give a pick of 3 upgrade
  cards (rerolls available); each boss gives a legendary upgrade and a weapon mod.
- **Players** pick a **role** (Tank, Healer, DPS) × **class/kit** (Soldier, Mage, Summoner, Knight, Ninja), then one
  of 3 weapons and one of 3 Space abilities for that combination. Each role × kit has an ultimate (F).
  Bonus abilities on Q/E/Shift come from very rare upgrade cards. Abilities level up with use (**mastery**,
  levels 1-5, with a level-5 "capstone" bonus each).
- **Defence:** everyone has a **parry** shield (right mouse) that soaks damage and can reflect; Knights' swings
  cut small bullets. Boss bullets come in two kinds: violet (wear down blockers) and **red aimed shots**
  (unblockable except by the Knight's Shield Wall).
- **Pacing rules:** a **bullet budget** caps regular enemies' bullets on screen (50 + 4/wave, max 120,
  +30% per extra player; 40% of it while a boss is up). Touching a boss hurts once per second and shoves
  you ~100 px out.
- **Modes in the lobby:** **Run** or **Practice Range** (hand-built waves of any enemies/bosses at any level,
  shared live with the lobby, savable as text files). The host picks the mode, and the run's **modifiers**
  (Modifiers tab: Glass Cannon, Tough Crowd, Frenzy, Swift Foes, Famine, Lucky).
- **Divergent paths:** after each boss the host picks one of two **paths** (or no path) for the four regular
  waves before the next boss: enemy mixes, hazard floors, twists and arena rules (9 paths, danger 1-3 with a
  bonus and a prize). Bosses are always standard. See `Docs/Paths.md`.
- **Dev tools (F1):** a tabbed menu (Match / Players / Tools): stat editor, god mode, wave skip, session
  recording toggle, **Boss Lab** (bots that play like people fight any boss; live stats; also a CLI batch mode).
- **Field guide (pause menu):** opens the published **Bestiary** and **Armory** pages, with live previews.
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
Docs/                  sources of the Armory and Bestiary reference pages            [added in the audit]
```

### Core (logic) — `Game` is one big `partial class` split by feature

| File | Lines | What it holds |
|---|---|---|
| `Game.cs` | 1.3k | Player/Enemy/Bullet/Minion/Zone classes, match state and constants, lifecycle (`HostStart`, `Stop`), upgrades and picks (`Recalc`, `HostPick`, `NextOffer`), the frame loop (`FixedStep`, `Update`), `LocalControl` (local movement, dash, bow, boss shove), `HostTick`, `TickPlayers` |
| `GameNet.cs` | 0.55k | **all network serialisation**: inputs (`OnInput`, `OnAbility`), `SendSnapshot`/`ReadSnapshot`, `OnHostMessage`, `SendSteer`/`ReadSteer`, `BroadcastFx` |
| `Combat.cs` | 0.8k | bullets (`SpawnBullet`, `MoveBullets`, homing `Steer`, gravity wells), `Collide`, damage to players (`Damage`, `RevivePlayer`) and enemies (`HurtEnemy`), on-hit/on-kill effects, burn, orbs |
| `Weapons.cs` | 0.37k | `FireRate`, `FirePlayer`, `FireVolley`, `ExtraShots`, Revolver and Mender's Bow, `WeaponRange`, `SetFrags` |
| `Abilities.cs` | 0.2k | `HostAbility`, `CastAbility` (Soldier abilities; kits, Knight and ultimates have their own files) |
| `Waves.cs` | 0.12k | `TickWaves`, `WaveCleared`, `CheckGameOver` |
| `Ninja.cs` | 0.45k | the Ninja: `FireNinjaWeapon`, `NinjaAbility`, Substitution's `MovePlayer` (`Msg.Teleport`), Shadow Step's landing, clones and Vanish (`NinjaTarget`), Smoke Bomb's slowed bullets, `TickNinjas` |
| `BloodMage.cs` | 0.45k | the Blood Mage: `FireBloodWeapon`, `BloodShotHit` (leech, pools, orb drops), health costs (`PayHealth`, `PayAbilityCost` from `HostAbility`, `castPaid`), Blood Price (`BloodDamageMult` in `HurtEnemy`), `BloodLifesteal`, `BloodAbility`, Crimson Pact / Blood Rite hooks in `Damage` (`BloodCut`, `BloodTookHit`), `TickBloodMages` (Pact burst, Frenzy drain, Apotheosis payout, Blood Boils) |
| `Showcase.cs` | 0.2k | the lobby's loadout preview: `Showcase` runs a private offline `Game` (`Session.HostOffline`, `NetPeer.Offline`) with 3 target dummies; `Game.ShowcaseMode` hooks skip waves, freeze enemies, and block XP/mastery |
| `EnemyAI.cs` | 1.0k | enemy roster per wave, movement, firing patterns, contact damage, boss specials, bullet budget |
| `LateGame.cs` | 0.45k | bosses 25-50 (Colossus → Eclipse) |
| `Singularity.cs` | 0.25k | wave-20 boss: corner wells, lasers, cages |
| `Minions.cs` | 0.7k | Mage and Summoner weapons and abilities, companions/minions |
| `Knight.cs` | 0.6k | Knight melee: swings (`Swing`, `LandSwing`, Lance box), sword waves, Knight abilities |
| `Ultimates.cs` | 0.15k | the 12 ultimates |
| `Mastery.cs` | 0.3k | ability XP/levels and capstones |
| `Parry.cs`, `Pause.cs`, `DevTools.cs`, `StatSheet.cs`, `RunStats.cs`, `Kits.cs` | small | as named |
| `BossSounds.cs` | 0.1k | soft audio cues when bosses start attacks |
| `Paths.cs` | 0.46k | divergent paths: `PathDef` registry (`Paths.All`), offer/choice (`OfferPaths`, `ChoosePath`, `Msg.PathOffer`/`PathChosen`), enemy mixes (`BuildPathWave`, `ApplyPathToEnemy`), bonus/prize (`PathBonus`, `FinishPath`), hazard patches (`PathPatch`, `TickPaths`, `Msg.PathPatch`), arena rules (`ArenaRule`, `NarrowF`/`FogF`, the playable rect `ArenaMin`/`ArenaMax`) |
| `Shop.cs` | 0.3k | the Bazaar: gold per kill (`GrantGold`, `GoldPerXp`), `OpenShop` / per-player shelf (`RollShelf`), `ShopAction`s (`Shop` → `Msg.Shop` → `HostShop`), giving gold (`Msg.ShopGift`, notices), `CloseShop` (leftover gold → XP), revive token, `BotShop`, snapshot fields |
| `Modifiers.cs` | 0.07k | `RunMod` flags and `ModDef`s; `Game.Mods` / `Has()`; sent in the lobby message |
| `Practice.cs` | 0.2k | Practice Range plan (`PracticePlan`/`PracticeWave`), text format, enemy blurbs, building a practice wave |
| `BotBrain.cs` | 0.4k | the "smart" bot (Boss Lab): dodging, positioning, aiming, ability use |
| `BossLab.cs` | 0.25k | Boss Lab sessions and stats |
| `Defs.cs` | 0.3k | role, weapon, enemy and bullet definitions (stats, colours) |
| `Upgrades.cs` | 0.45k | upgrade cards, abilities, per-weapon/Knight descriptions |
| `Session.cs` | 0.7k | hosting/joining, lobby, ready state, **message enum** (`Msg`), protocol version, practice sync |
| `Net.cs` | 0.26k | `NetPeer`: length-prefixed messages over TCP (`NoDelay`) |
| `SessionRecorder.cs` | 0.5k | the session JSON recorder |
| `MathTypes.cs` | 0.08k | `V2`, `Rgba`, `MathX` |

### Game (front end) — `ArenaApp` is a `MonoBehaviour` partial class

| File | Lines | What it holds |
|---|---|---|
| `ArenaApp.cs` | 0.45k | boot, command line, input and the frame loop (`Update`, `FixedUpdate`), layout/scaling, `OnGUI` dispatch, small shared helpers |
| `WorldRenderer.cs` | 1.3k | **world rendering in GL**: `OnPostRender`, `DrawWorld` (floor) → `DrawArenaContents` (one helper per layer: auras, shots, zones, orbs, enemies, players, effects), `DrawEnemy` (layers: underlay / status / body / details, with per-type helpers), `DrawPlayer`, `DrawKnight`, aim line / bow draw / cylinder overlays, GL primitives and clipping (`Tri`, `UiClip`, `BeginUiGl`/`UiModelAt`) |
| `Hud.cs` | 1.0k | HUD (layout "C", Oct 2026: wave panel left of the ability bar, your card right of it with teammates as slim rows above, `DrawWaveCorner`/`DrawPlayerCorner`/`DrawTeammateRow`; boss bars wide at the top middle): wave plate, player cards, boss bars, ability bar, mastery toasts, Tab menu, upgrade picker and cards, `StatsSidebar`, banners, run summary |
| `Menus.cs` | 0.4k | main menu (title axis, plot corners), How to play, pause menu, settings and audio |
| `LobbyUi.cs` | 0.5k | the lobby: "!" connection popup by the title (port, invite addresses), the hero card's live mini-arena (`DrawShowcase`, stepped from `FixedUpdate`), mode switch, party cards with live characters, loadout with hero preview, bottom bar |
| `PracticeUi.cs` | 0.45k | Practice Range editor (live enemy tiles, shared edits, save/load) |
| `MenuBackgrounds.cs` | 0.34k | 10 animated menu backgrounds + the lobby's picker card (bottom left: live thumbnail, arrows, dots); your pick is also drawn dimmed behind the arena (`DrawWorld`, `ArenaBgDim`) |
| `BossLabUi.cs`, `DevMenu.cs` | 0.3-0.4k each | F1 tools (`DevMenu`: sidebar tabs Match / Players / Tools, card layout) |
| `PathsUi.cs` | 0.48k | Choose-your-path cards (live floor + enemies, skulls, rules, bonus, prize, "No path"), each path's floor pattern (`DrawPathFloor`), fire/pool patches, Narrow Pass walls (`DrawNarrowWalls`) and Fogbound mist (`DrawFog`, per-vertex alpha grid), HUD path chip |
| `ShopUi.cs` | 0.3k | the Bazaar's shop screen (`DrawShop`): shelf cards, services, remove-upgrade list, Give popup, Done button and status, host close button |
| `ModifiersUi.cs` | 0.08k | the lobby's Modifiers tab |
| `ReferencePages.cs` | 0.07k | the pause menu's Field guide (opens the Bestiary / Armory pages) |
| `Ui.cs` | 0.45k | IMGUI style kit (panels, buttons, choices, bars, fancy text). Buttons are the owner's chosen style "F": icon cards (`Choice` with an icon tile, `Button` with an optional icon chip), `PulseRing`, tinted `Icon` |
| `BloodMageLook.cs` | 0.2k | the Blood Mage's look (hood, circling drops that glow brighter as health falls, weapon in hand, buffs), its shots, Siphon Field and Blood Pool zones, Blood Rite / Sanguine Rain auras |
| `PixelArt.cs` | 1.15k | 16×16 pixel-art icons as text grids |
| `SoundBank.cs`, `GameMusic.cs`, `MenuMusic.cs` | 1.3k | every sound and all music synthesised in code |

### Key mechanisms worth knowing

- **Networking:** host-authoritative TCP. Clients send inputs; **clients are authoritative for their own
  movement** (`LocalControl`), the host for everything else. The host sends a full **snapshot every 3 ticks**
  (20 Hz) of players/enemies/minions/orbs; bullets are sent as spawn/remove events and simulated on both
  sides with periodic `BulletSync`. Effects and sounds are broadcast (`Msg.Fx`, `Msg.Sound`).
  **`Session.ProtocolVersion` (now 62) must be bumped whenever any message format changes**; mismatched
  players are turned away with a clear message. New `Msg` values go at the end of the enum.
- **Rendering:** no sprites. The world is drawn with **GL immediate-mode triangles** in `OnPostRender`
  (arena 1600×900 scaled to the screen). The UI is **IMGUI** on a 1280×720 virtual canvas (`W`, `H`), scaled.
  Menus can draw game models on top of panels: `BeginUiGl()` / `UiModelAt(pos, zoom)` / `EndUiGl()` issue GL
  during IMGUI's Repaint (used by the lobby and Practice Range tiles).
- **Loadout preview:** `DrawShowcase` swaps the `Showcase` game into `ArenaApp.game` and calls `DrawArenaContents`
  (the part of `DrawWorld` between the floor and the overlays), clipped with `UiClip` (software clipping in `Tri`,
  so it works the same in Unity and the preview tool).
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
- **Class tables** (weapons, Space abilities, ultimates) are indexed by role × kit with `Defs.KitCount = 6`; a new class's
  entries go after each role's last class. Ninja and Blood Mage weapons are routed by weapon id (`>= WeaponId.Kusarigama`,
  `>= WeaponId.BloodLash`). Blood Mage abilities cost health: `AbilityDef.HealthCost` (share of max health, or of
  current with `CostsCurrent`), paid in `HostAbility` before the cast, never below 1 HP.

## 3. Testing and previewing (no Unity in the cloud container)

- **Setup:** the cloud container may not have mono. Install it with
  `apt-get install -y mono-mcs mono-runtime libmono-system-core4.0-cil` (Pillow for previews is already there).
  Without mono, `check.sh` now fails with a clear message (exit 2); it used to print "OK" anyway.
- **Compile check:** `Tools/CompileCheck/check.sh` (mono `mcs` + Unity stubs). If the game starts using a
  Unity member the stubs lack, add it to `Tools/CompileCheck/UnityStubs.cs` **and** `Tools/Preview/Stubs.cs`.
  Missing so far and avoided: `Mathf.Lerp`, `Mathf.Tan` (use `System.Math`).
- **Sim tests:** `Tools/SimTest/run.sh <wave> <seconds> [mode]` builds and runs `Program.cs`.
  - `run.sh 1 30` = the **core** match tests (~164 checks).
  - `run.sh 1 30 music` = the **feature** suite (~175 checks: parry, aim, mastery, budget, Boss Lab,
    Singularity, Knight, boss sounds, weapon extras, revolver, bow, practice, weapon range...). The name is
    historical (it began as the music test). **Run both before committing.**
  - Single systems: `knight`, `practice`, `extra`, `aim`, `parry`, `lab`, `singularity`, `bosses`, `dps`,
    `budget`, `mastery`, `bow`, `revolver`, `three`, `late`, `ults`, `mods`, `showcase`, `classcards`, `ninja`, `blood`, `paths` (all path tests, ~30 checks), ... (see the `args[2] ==` chain
    near the end of `Program.cs`).
  - Known flaky checks (pass on rerun): "a perfect parry sends the shots back", "every weapon gets its
    own upgrade from extra projectiles" "enemy bullets stay near the bullet budget" (limit 210; the peak
    varies about 175-235 between runs, on the original code too) and, rarely, "regular waves can't wear a Barrier
    down" (core suite; fails when no enemy happens to shoot near the tank in its 5 s window), and "Mirror Guard"
    (rare), and "Swarm Fields: mostly melee rushers, about twice as many" (random; about 29-32 enemies against a
    29.4 threshold). Also "fire warns first, then burns players and enemies" (paths; fails when an enemy shot lands during the warning). The bullet-budget one fails about 1 run in 4-5 (re-measured Oct 2026: peaks 195-245).
  - Boss Lab batch from the CLI: `run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat]`, seats like
    `T/K/1` (Tank Knight, second weapon; `N` = Ninja, `B` = Blood Mage).
- **Render check:** `Tools/RenderCheck/check.sh [git-ref]` renders 11 fixed, seeded scenes (every enemy and minion
  type, each class's abilities and ultimate, two Ninja scenes; refs before the Ninja can't build it) with `DrawWorld`, in the working tree and at `git-ref` (default `HEAD`),
  and compares every GL triangle. `IDENTICAL` means nothing on screen changed: use it for any rendering refactor.
- **Screenshots without Unity:** `Tools/Preview/shot.sh <name> <mode>` → `Tools/Preview/out/<name>.png`.
  Modes: `hud`, `tab`, `picker`, `dev`, `labsetup`, `labpanel`, `lobby`, `lobbypr`, `practicehud`, `menu`, `howto`, `bg`
  (with `BG=0..9`), `pause`, `kitgrid`, `legendary`, ... Path flags: `PATHOFFER=1|2|3|4` (the choice screen with
  each pair of paths), `PATHON=<PathId number>` (in-arena look of that path; 8 = Fogbound, 9 = Narrow Pass),
  `PATCHES=1` (sample fire/pools). Others: `LOBBYTAB=n` (lobby tab, e.g. Modifiers), `BGMIN=1` (minimized
  background picker), `DEVTAB=n` (F1 tab), `FROST=1` (Frost Lance shots). Env flags: `KNIGHT=<weapon>`, `POSE=1`, `THRUST=<0..1>`, `BOW=<draw>`, `MASTERY=1`,
  `KIT=n` with mode `kitgrid` (every role and weapon of a class, drawn big), `NINJA=<weapon>` / `BLOOD=<weapon>` (like `KNIGHT=`), `MOUSE=x,y`, `AIMLINE=<px>` (mouse that far from you, with the aim line), `SOLO=1`, `INFO=1` (open the lobby's "!" connection popup), `BG=n` (also in `hud`: the arena background), `PICKCARDS=a,b,c` (picker: these three cards), `LOADOUT=role,kit,weapon,ability` / `ABILITY=n` (hover) / `SHOWT=secs` (the lobby's live preview). The renderer is approximate (no text fields, faint seams in big circles, thin translucent lines look a few times brighter than in Unity; translucent UI and rimmed cards were fixed to match Unity in Oct 2026, so older preview PNGs look fainter); use it to
  check layout, then ask the owner to look in Unity.
- **Reference pages:** `Docs/` holds the Bestiary and Armory sources. `Docs/Armory/build.sh` rebuilds the Armory
  (its icons come straight from `PixelArt.cs`) and `Docs/checks/*.js` are Playwright scripts that screenshot the
  pages and report page errors. See `Docs/README.md` for where they're published and how to update them; their
  numbers and descriptions are hand-copied from the code, so update them with balance changes.

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

## 5. Recent history (newest first)

**Blood Mage class (Oct 2026, protocol 61):** 6th kit for all three roles: health as a resource (Space abilities cost health, never below 1 HP; Blood Price up to +40% damage at low health; 3% lifesteal), 9 weapons, 9 Space abilities, 3 ultimates, 9 ability upgrades + class cards, icons, look; `blood` test suite; preview `BLOOD=n`.

**This session (Oct 2026), newest first:** Vein Tether is a real beam (`BloodMage.cs`: `pendingBeams`/`ResolveBeams` from `Collide`, `BeamEnd` raycast shared with drawing; `Player.FiringSeen` synced so every screen draws everyone's beam, `DrawVeinBeams`; Armory `s.beam`, `Docs/checks/armory-vein.js`); protocol 63 · Bear Totem (summoner Tank) is now melee: `FireBearClaws` queues two quick claw swipes (110°, 85 px, 11 damage, 0.12 s apart, raking opposite ways; `Swing.ClawSide`, drawn by `FxKind.Claw`) that don't cut bullets, and each hit bleeds (`Swing.Bleed`, `ApplyBleed`/`TickBleed`: 30% of the hit per second for 3 s, stacking to five hits; replicated status bit 16, drops and a blood-drop icon); `DotDamage` shared with burning; `bear` sim mode; protocol 62 · H hides/shows the HUD (`hudShown`/`hudT`, `HudGroup` staggers four pieces with slide + scale + ease-out-back + fade; `Ui.Alpha`/`Ui.Faded` make GUI.color-setting draws fade too; preview flag `HUDT=0..1`) · The Bazaar (a 10th path, 1 skull): kills on its waves give every player the same gold (`GoldPerXp` 0.3), and its prize is a shop before the boss (`Shop.cs`, `ShopUi.cs`): per-player shelf of 4 upgrades + a legendary + a weapon mod priced by rarity, services (restock, remove an upgrade, full heal, revive token), giving gold (reopens a done player's shop), no timer (closes when everyone is Done; the host can close it after 60 s), leftover gold becomes each player's own XP; bots shop by themselves; gold shows as a chip above the wave panel (`DrawGoldChip`; preview `GOLD=n`); preview mode `shop` (`SHOPSEL`, `GIVE=1`, `REMOVE=1`) and `PATHOFFER=bazaar`; protocol 60 · HUD moved to the bottom (owner picked mockup C of three): wave panel and your card flank the ability bar, teammates as slim rows, boss bars wide at the top · Session recorder check (format 2): mode (run / practice), endless runs record their real end (they used to stay "victory" at wave 50), path offers/choices/prizes, healing received by source, floor damage to enemies, dev tool use, each player's final abilities and mastery; `report.py` shows paths, healing sources, dev-tool flags and leaves Practice Range sessions out (`--practice` to include) · Divergent paths, all 4 steps (protocols 57-59): framework and the
Choose-your-path screen, Swarm Fields / Sniper Ridge / Elite Gauntlet, Burning Grounds and Sanctuary (hazard
patches, bots avoid fire and seek pools), the "no path" option, Frozen Lake (momentum movement in
`LocalControl`: `iceVel`, `Paths.IceGrip`) and Blood Moon (no orbs/regen, kills heal), Fogbound (client-side
mist) and Narrow Pass (walls, playable rect; bullets spark and stop at the walls); `paths` sim mode; the
Sanctuary count test now counts spawn rolls (swarmer pairs made it flaky) · `Docs/Paths.md` design with the
owner's decisions · Run modifiers: lobby Modifiers tab, six switches · Lobby: no RUN / PRACTICE label by Ready;
background picker has a minimize button (fades after a few seconds, hover to bring back) · Stat previews use a
pixel arrow icon (`ui_arrow`) instead of "->" · Frost Lance flies as an ice spear with a wider (20 px)
spearhead hitbox (`DrawFrostLance`) · UI buttons restyled as icon cards (style "F") · F1 dev menu tabbed and
card-based; Field guide (Bestiary, Armory) in the pause menu · Docs: Armory build script (icons from
`PixelArt.cs`) and Playwright page checks · Audit: dead code removed, Game.cs and ArenaApp.cs split,
`Tools/RenderCheck`, `Tools/Preview`, `Tools/CompileCheck`, this HANDOFF.

Earlier: Parry gauge on the shield: a thin blue bar around the rim drains toward your aim (`DrawParryGauge`; faint while lowered and refilling, your own only), the HUD parry tile is gone, and a break plays `FxKind.Shatter` (cracks, then jagged pieces and sparks, seeded by position so everyone sees the same burst); protocol 56 · Ninja class (5th kit) for all three roles: 9 weapons, 9 Space abilities, 3 ultimates, 9 ability upgrades + class cards, icons, look; protocol 52 · YOUR STATS is a see-through sidebar down the whole left edge in the Tab menu and the upgrade picker (`StatsSidebar`) · Class ability cards: very rare cards for your class's two other Space abilities, on Q/E/Shift (`UpgradeDef.ClassAbility`, `Weight` 2; generated in `Upgrades.WithClassAbilities`; ability upgrades now need the ability on any key, `Upgrades.HasAbility`); protocol 51 · Practice Range editor redesigned (numbered wave list, level stepper, tiles sized to fit, hover info bar with the enemy drawn, tidy footer; `Ui.SmallButton` for tight rows) · Main menu title is now "Cartesian Colosseum" (title only: the product name, window title and docs keep "Top-Down Arena" so saved settings stay) on an animated x-axis; How to play redone with key caps and illustrated tips · Aim line runs to the crosshair, capped at weapon range · lobby background picker moved bottom left as a card with a live thumbnail · Lobby loadout preview is a live mini-arena (real game offline, target dummies, ability demo on hover/pick) · Lobby bottom bar: ready notes only with other players (no "Click Ready" prompt) · Your lobby background also shows dimmed behind the arena; lobby's "Pick Practice Range" hint removed · Lobby: hosting/port line and invite addresses moved into a popup behind a "!" button by the title · Lobby backgrounds (10, per player, still option) · lobby redesign + Practice Range moved into the lobby,
shared and co-editable · Tab menu during upgrade picks; mastery XP line on ability tiles · aim line ends at
weapon range (`Game.WeaponRange`, tested against real shots) · Lance: box hitbox 180×88 that slides out,
1 s stun · Practice Range (first as an F1 tool) · Shield Wall blocks red shots · boss contact shove ·
Lance replaced the Knight Tank's Sword & Shield · Knight class · Mender's Bow arrows · Singularity rework ·
Boss Lab with human-like bots · bullet budget and homing changes · ability mastery.

Open balance notes from the last session analysis (Knight Tank Lance run, before the Lance box change):
waves 1-4 deal almost no damage (46 total vs 276 HP) and the wave-5 Warden is a sudden wall (406 damage);
Shielders are the Knight's worst enemy (43 s average lifetime, their shields block swings); Mortars, Shielders
and Snipers do the most damage per spawn; regular enemies' bodies can still hit you every frame you stand
on them (only bosses got the once-a-second rule). The owner hasn't yet picked which of these to act on.

---

## 6. Audit findings (October 2026)

### 6.1 Dead code — already removed

The codebase is kept tidy as features change, so very little was dead. Removed (with the owner's OK):
`Game.HavenHeal` (unused constant), `NetPeer.ConnectionCount` (never called), the Boss Lab seat picker's
"no corner" option and a stale "F1 > Practice Range" line in saved practice files (leftovers of the old F1
Practice Range). Scans covered unused methods, fields that are written but never read, and unused enum
values; nothing else qualified. The **simple test bot** in `LocalControl` (`Bot` without a `BotLevel`) is
**not** dead: most sim tests rely on its predictability, and F1 "Bot plays for you" uses it.

### 6.2 Where coding is slow: one feature, many places (highest value)

Adding or changing one weapon, ability or enemy means editing many scattered `switch` statements and hand
copies of the same numbers:

| Adding a... | Touches |
|---|---|
| **Weapon** | `Defs` (definition) · `FireVolley` / `FireKitWeapon` / `FireKnightWeapon` (behaviour) · `Game.WeaponRange` (speed × life copied again) · `Knight.MeleeRange` · `BotBrain` range table · `ArenaApp.DrawKnight`/draw code · `Upgrades` extra-projectile text · README · Armory |
| **Ability** | `AbilityId` · `Upgrades` (def, description, role/kit tables) · `Game.CastAbility` / `Minions.KitAbility` / `Knight.KnightAbility` / `Ultimates` · `Mastery` capstone text and effect · `BotBrain.WantAbility` · `PixelArt` icon by name · README · Armory |
| **Enemy** | `EnemyType` + `Defs` · `EnemyAI` (roster, move, fire, special) · `ArenaApp.DrawEnemy` · `Practice.BlurbOf` + `Choices` · README · Bestiary |

**Recommendations**
1. **Put weapon numbers in `WeaponDef`** (shot speed, lifetime, damage, reach, per-extra growth). Then
   `WeaponRange`, bot ranges, upgrade texts, the Armory and balance tweaks read one place. `WeaponRange`
   currently duplicates every speed/life literal (guarded by the `aim` test, but still double work).
2. **An ability registry**: one entry per ability with its cast function, bot heuristic, capstone and text,
   instead of 5+ parallel switches. Adding a Knight-sized class took most of a session because of this.
3. **An enemy registry** in the same spirit (definition + blurb + draw style + AI hooks), so the Practice
   Range, Bestiary and README tables could be generated rather than hand-kept.
4. **Generate the reference docs** (README tables, Armory/Bestiary data) from the definitions with a small
   tool, so balance changes don't need three manual edits.

### 6.3 File organisation

5. ✅ **Done (Oct 2026).** **Split `Game.cs` (3.3k lines)** into `GameNet.cs` (snapshot send/read, host/client messages, inputs),
   `Combat.cs` (`Collide`, damage, on-hit effects), `Abilities.cs` (`CastAbility`), `Waves.cs` (`TickWaves`,
   `BuildWave`) and `Weapons.cs` (`FireVolley`, `WeaponRange`). Pure moves: low risk, big readability gain.
   *Done as pure moves (every line kept, verified by line multiset). `BuildWave` was already in `EnemyAI.cs`
   with the roster and stays there; the scattered constants stay at the top of `Game.cs` until item 14.*
6. ✅ **Done (Oct 2026).** **Split `ArenaApp.cs` (2.8k lines)** into `WorldRenderer.cs` (`DrawWorld`, `DrawEnemy`, `DrawPlayer`, fx;
   the 351-line `DrawWorld` and 268-line `DrawEnemy` would also benefit from per-type helpers), `Hud.cs`
   (wave plate, ability bar, Tab menu, picker, run summary) and `Menus.cs` (main menu, settings, how-to).
7. **Split `Tools/SimTest/Program.cs` (3.7k lines)** into one file per system, replace the `args[2] ==` chain
   with a table of named suites, and add an `all` suite. **Rename the `music` suite** (it runs the whole
   feature suite, which is confusing) and make the default run everything.

### 6.4 Networking

8. **Snapshot read/write are hand-paired** (136 `Write` calls in `Game.cs`): adding a replicated field means
   editing `SendSnapshot` and `ReadSnapshot` in lockstep plus a protocol bump, and a mismatch silently
   corrupts the stream. A symmetric `Serialize(IStream)` per entity (one method that both reads and writes)
   would remove that whole class of bug.
9. `ReadSnapshot` allocates several `HashSet`/`List`s **per snapshot** (20 Hz on clients); reuse scratch
   collections like `Collide` already does. Small GC win on the client.
10. Bandwidth is fine for 3 players over Tailscale (full state at 20 Hz, no bullets). Delta compression isn't
    needed unless player counts grow.

### 6.5 Gameplay performance

11. **Collision is all-pairs** (player bullets × enemies, enemy bullets × players). With the bullet budget it's
    cheap today (a few thousand checks per tick); a uniform grid would only matter if counts grow a lot
    (endless mode, more players).
12. Rendering is GL immediate mode with many small circles (8-48 segments). Fine on desktop; if frame time
    ever matters, lower segment counts for small bullets and batch the menu backgrounds.
13. Hot paths are mostly allocation-free already (shared scratch lists in `Collide`); the remaining
    per-event allocations are in Knight swings (`LandSwing` makes a hit list per swing) and `ReadSnapshot`.

### 6.6 Balance tooling

14. **Tunables are scattered constants** across `Game.cs`, `EnemyAI.cs`, `Knight.cs`, `Defs.cs`. A single
    `Balance.cs` (or a JSON file the dev menu can reload) would make tuning sessions much faster.
15. **Flaky tests** use the game's RNG: give each test a fixed seed so results are repeatable.
16. The F1 **"Bot plays for you"** still uses the simple test bot; the Boss Lab's smart bot (`BotSkill.Good`)
    would make it far more useful for play-testing. Keep the simple bot for the sim tests.

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
   into helpers (item 6). Checked byte-for-byte with `Tools/RenderCheck`. (`DrawArenaContents` was already pulled out of `DrawWorld` for the loadout preview.)
3. Reorganise `Tools/SimTest/Program.cs`: one file per system, a table of named suites plus `all`, rename the
   `music` suite, fixed seeds for the flaky tests (items 7, 15).
4. Weapon numbers into `WeaponDef`; `WeaponRange`, bot range tables and upgrade texts read them; `aim` test
   must pass (item 1).
5. Symmetric serialise per entity replacing `SendSnapshot`/`ReadSnapshot`; reuse scratch collections in
   `ReadSnapshot`. Protocol bump (items 8, 9).
6. Ability registry and enemy registry (items 2, 3): show the owner the design first, then migrate in small
   commits.
7. **Divergent paths**: all 4 steps ✅ (framework, no-path option, all 9 paths). Arena rules: `ArenaRule()`, eased `NarrowF`/`FogF` in `Paths.cs`, which every game derives from the path, so nothing extra is sent; the playable rect is `ArenaMin`/`ArenaMax` (`ClampToArena`, `KeepInArena`, `RandomInArena`). Next: tune numbers in play. Designed with the owner, see [`Docs/Paths.md`](Docs/Paths.md) (decisions, the first pool
   of 9 paths, screens, engineering notes and a 4-step build order). Doesn't need the registries. Protocol bump.
8. Gather the scattered balance constants into `Balance.cs` (item 14).
9. F1 "Bot plays for you" uses the smart bot (`BotSkill.Good`); the simple bot stays for tests (item 16).

Side requests done along the way (Oct 2026): lobby "!" connection popup, arena shows your lobby background,
live mini-arena loadout preview, aim line capped at range, background picker card, Cartesian Colosseum
main menu and How to play, Practice Range editor redesign, dev menu tabs, Field guide, button style F, Frost
Lance spear, stat pixel arrow, minimizable background picker, run modifiers. `Tools/CompileCheck/check.sh` fails
loudly when mono is missing.

**Where to pick up:** the next items are 3 (SimTest reorganisation) or 8 (Balance.cs). Paths are feature-complete
but untuned: ask the owner for play-test feedback (Frozen Lake's grip, Fogbound's sight 350 px, Narrow Pass's
1120 × 720, fire damage) and session JSONs, which record each wave's path. Open questions in `Docs/Paths.md`:
whether waves 1-4 should also get a path choice. Planned in the design but not built yet: a route strip in the
Tab menu, the route in the run summary (the route only shows on the Choose-your-path screen today), and
`Tools/SessionReport` grouping damage and downs by path.
