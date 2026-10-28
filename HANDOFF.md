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
- **Players** pick a **role** (Tank, Healer, DPS) × **class/kit** (Soldier, Mage, Summoner, Knight), then one
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
  shared live with the lobby, savable as text files). The host picks the mode.
- **Dev tools (F1):** stat editor, god mode, wave skip, session recording toggle, **Boss Lab** (bots that
  play like people fight any boss; live stats; also a CLI batch mode).
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
| `ArenaApp.cs` | 2.8k | boot, input, layout/scaling, **world rendering in GL** (`DrawWorld`, `DrawEnemy`, `DrawPlayer`, `DrawKnight`, effects), main menu, settings, how-to-play, HUD (wave plate, player cards, ability bar, Tab menu, upgrade picker, run summary), sound playback |
| `LobbyUi.cs` | 0.5k | the lobby: "!" connection popup by the title (port, invite addresses), the hero card's live mini-arena (`DrawShowcase`, stepped from `FixedUpdate`), mode switch, party cards with live characters, loadout with hero preview, bottom bar |
| `PracticeUi.cs` | 0.45k | Practice Range editor (live enemy tiles, shared edits, save/load) |
| `MenuBackgrounds.cs` | 0.34k | 10 animated menu backgrounds + the lobby's picker card (bottom left: live thumbnail, arrows, dots); your pick is also drawn dimmed behind the arena (`DrawWorld`, `ArenaBgDim`) |
| `BossLabUi.cs`, `DevMenu.cs` | 0.3k each | F1 tools |
| `Ui.cs` | 0.34k | IMGUI style kit (panels, buttons, choices, bars, fancy text) |
| `PixelArt.cs` | 1.15k | 16×16 pixel-art icons as text grids |
| `SoundBank.cs`, `GameMusic.cs`, `MenuMusic.cs` | 1.3k | every sound and all music synthesised in code |

### Key mechanisms worth knowing

- **Networking:** host-authoritative TCP. Clients send inputs; **clients are authoritative for their own
  movement** (`LocalControl`), the host for everything else. The host sends a full **snapshot every 3 ticks**
  (20 Hz) of players/enemies/minions/orbs; bullets are sent as spawn/remove events and simulated on both
  sides with periodic `BulletSync`. Effects and sounds are broadcast (`Msg.Fx`, `Msg.Sound`).
  **`Session.ProtocolVersion` (now 50) must be bumped whenever any message format changes**; mismatched
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
- **Class tables** are indexed by role × kit with `Defs.KitCount = 4`.

## 3. Testing and previewing (no Unity in the cloud container)

- **Setup:** the cloud container may not have mono. Install it with
  `apt-get install -y mono-mcs mono-runtime libmono-system-core4.0-cil` (Pillow for previews is already there).
  Without mono, `check.sh` wrongly prints "OK: compiles" (it only greps for "error"), so make sure `which mcs` works.
- **Compile check:** `Tools/CompileCheck/check.sh` (mono `mcs` + Unity stubs). If the game starts using a
  Unity member the stubs lack, add it to `Tools/CompileCheck/UnityStubs.cs` **and** `Tools/Preview/Stubs.cs`.
  Missing so far and avoided: `Mathf.Lerp`, `Mathf.Tan` (use `System.Math`).
- **Sim tests:** `Tools/SimTest/run.sh <wave> <seconds> [mode]` builds and runs `Program.cs`.
  - `run.sh 1 30` = the **core** match tests (~164 checks).
  - `run.sh 1 30 music` = the **feature** suite (~117 checks: parry, aim, mastery, budget, Boss Lab,
    Singularity, Knight, boss sounds, weapon extras, revolver, bow, practice, weapon range...). The name is
    historical (it began as the music test). **Run both before committing.**
  - Single systems: `knight`, `practice`, `extra`, `aim`, `parry`, `lab`, `singularity`, `bosses`, `dps`,
    `budget`, `mastery`, `bow`, `revolver`, `three`, `late`, `ults`, `mods`, `showcase`, ... (see the `args[2] ==` chain
    near the end of `Program.cs`).
  - Known flaky checks (pass on rerun): "a perfect parry sends the shots back", "every weapon gets its
    own upgrade from extra projectiles" and "enemy bullets stay near the bullet budget" (limit 210; the peak
    varies about 175-235 between runs, on the original code too).
  - Boss Lab batch from the CLI: `run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat]`, seats like
    `T/K/1` (Tank Knight, second weapon).
- **Screenshots without Unity:** `Tools/Preview/shot.sh <name> <mode>` → `Tools/Preview/out/<name>.png`.
  Modes: `hud`, `tab`, `picker`, `dev`, `labsetup`, `labpanel`, `lobby`, `lobbypr`, `practicehud`, `menu`, `howto`, `bg`
  (with `BG=0..9`), ... Env flags: `KNIGHT=<weapon>`, `POSE=1`, `THRUST=<0..1>`, `BOW=<draw>`, `MASTERY=1`,
  `MOUSE=x,y`, `AIMLINE=<px>` (mouse that far from you, with the aim line), `SOLO=1`, `INFO=1` (open the lobby's "!" connection popup), `BG=n` (also in `hud`: the arena background), `LOADOUT=role,kit,weapon,ability` / `ABILITY=n` (hover) / `SHOWT=secs` (the lobby's live preview). The renderer is approximate (no text fields, faint seams in big circles, thin translucent lines look a few times brighter than in Unity); use it to
  check layout, then ask the owner to look in Unity.
- **Reference pages:** `Docs/` (Armory, Bestiary) — see `Docs/README.md` for how to rebuild and where they're
  published. Their numbers are hand-copied from the code; update them with balance changes.

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

## 5. Recent history (newest first)

Main menu title is now "Cartesian Colosseum" (title only: the product name, window title and docs keep "Top-Down Arena" so saved settings stay) on an animated x-axis; How to play redone with key caps and illustrated tips · Aim line runs to the crosshair, capped at weapon range · lobby background picker moved bottom left as a card with a live thumbnail · Lobby loadout preview is a live mini-arena (real game offline, target dummies, ability demo on hover/pick) · Lobby bottom bar: ready notes only with other players (no "Click Ready" prompt) · Your lobby background also shows dimmed behind the arena; lobby's "Pick Practice Range" hint removed · Lobby: hosting/port line and invite addresses moved into a popup behind a "!" button by the title · Lobby backgrounds (10, per player, still option) · lobby redesign + Practice Range moved into the lobby,
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
6. **Split `ArenaApp.cs` (2.8k lines)** into `WorldRenderer.cs` (`DrawWorld`, `DrawEnemy`, `DrawPlayer`, fx;
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
