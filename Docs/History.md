# Change history (newest first)

Moved out of `HANDOFF.md` so sessions don't read it by default. `git log` has the same story;
look here only when you need why something was built the way it was.

**Melee style E, weapon + energy (Nov 2026), protocol 127 (no change):** the owner's pick from
`Docs/Mockups/melee-attacks.html`: the weapon models with the energy arc. `Game/MeleeLooks.cs`, drawing only and only with
Visual effects on (with them off `DrawSlashFx` / `DrawJabFx` draw as before; render check IDENTICAL). A player's
`FxKind.Slash` / `FxKind.Jab` is matched to the player standing where it started (`MeleeOwnerOf`, noted per effect; the
effect's `Dur` is stretched on this screen so the energy and embers last 0.55 s). Sweeps: the weapon (a model per melee
weapon in `DrawMeleeModel`) sweeps through a glowing band with a white core, sparks off its tip and embers after; Twin
Blades and the Katana alternate sides. Thrusts stab out through a beam; the Halberd's six steps draw one halberd at a time
(`NewerStepThan`); the Lance slides out through a glowing box. The Warhammer is raised over your shoulder while it charges
(`DrawHammerRaised`), swings over in a burning arc in 0.08 s and slams: a glowing circle with turning ticks, a pillar of
light, embers (aftershocks are just the impact). Hits and timing are unchanged: the hit lands as the effect starts.
Bosses' and enemies' slashes keep the old look. Preview: `SWING=<seconds>` with `SWINGW=<WeaponId>` (and `KNIGHT=0`).
Follow-up (owner: "not smooth, the line is too thick, the gradient is blocky"): the arc is one band coloured per vertex
(`EnergyBand`: about 20 slices a radian, clear at both edges, brightest near the rim) instead of two stepped bands, the
core is a 1.5 px line that brightens toward the blade (`CoreArc`), thrust beams are a soft line (`SoftLine`), the
hammer's trail is one ribbon, and the blades and handles are slimmer. Then (owner: "the line is too thick, the halberd didn't get
updated"): the arc is a thin glowing line at the tip's reach (12 px wide, 1 px core) instead of a band over 40% of the
reach, and the Halberd's steps keep the new look on every frame (stretching a step's `Dur` had made later frames take
it for a dash streak); `juice.sh` checks both. Then (owner: "the halberd has no fade behind the slash, the model looks bad, the other
weapons still have the thick line, nothing looks as good as the mockup"): the preview had been drawing each triangle in
one flat colour with no glow pass, so gradients and glows were invisible to every check; `Tools/Preview/raster.py` now
draws like the GPU (the stub marks the glow material's vertices), and a side-by-side with the mockup at the game's scale
showed what was wrong: the energy had been drawn with plain alpha in the slash colour (near white, so a grey haze) where
the mockup adds light in each weapon's own colour. Now the energy is drawn in the additive glow pass (`DrawMeleeGlow`, from
`DrawGlowPass`) in `MeleeColor(weapon)`, as the mockup's band (72-100% of the reach, 0.6 at the blade, a 6 px soft edge)
with a 2 px white core; the models are the mockup's `blade()` shapes; the Halberd's six slices tile from one step's angle
to the next (no overlaps doubling up in the additive light), so they make one band fading behind the blade.

**Designed waves built into the game (Nov 2026), no protocol change:** owner's ask, the wave editor's code generation.
Designed sets live in `Data/Waves/*.waves` (outside `Assets`, so Unity doesn't import loose files);
`Tools/WaveGen/build.sh` compiles `Gen.cs` with the core (leaving the generated file out, `Stub.cs` in its place),
parses each file with `WaveSet.Parse` and writes `DesignedWaves.Generated.cs` (`Names`, `Build`, one `SetN()` building
each set with object initialisers), stopping on any problem. `DesignedWaves.Get(name)` (`Core/DesignedWaves.cs`) hands
out a fresh copy. The wave editor's Load panel lists them under "Built into the game"; in the Unity editor its "Save to
project" writes to `Data/Waves` (`Application.dataPath/../Data/Waves`). First set: `warden-drills`. Test: `wavegen`
(files and built-in sets match exactly, a stale build fails it). Not yet used by hunts, approaches or paths.

**Twin Rifle toned down (Nov 2026), no protocol change:** owner, after a zombie run (solo DPS Soldier, waves 1-10,
about 190 damage taken in 11 minutes): Twin Rifle too strong. It had the top Soldier DPS (155, tied for the game's best)
with no range downside, and crossfire came almost free. Now 4 per barrel (was 4.7), shots last 0.7 s (about 575 px,
was 820), and the barrels sit 60 px apart (`TwinBarrelGap`, was 28), so up close the streams can pass either side of a
small enemy unless you aim at it. Measured DPS 138 (`WeaponDps.Generated.cs` rewritten by `run.sh 1 1 weapondps
write`). Test: `twinsmg` (a Swarmer 70 px away is missed when aiming past it, nothing at 650 px). The Armory page is
now out of date for the Twin Rifle.

**Smaller zombies (Nov 2026), no protocol change:** owner: "zombies are way too big". Radii cut about 35-40% to sit
with the regular enemies (Shambler 22→14, Runner 15→10, Spitter 19→12, Crawler 17→11, Bloater 27→18, Shrieker
17→11, Mender 18→12; Defs.cs and the Bestiary), the spore haze 2.1r→1.7r and drips a little smaller. Also: zombies
skip the shooter turns and shot budget (a Spitter's glob is a patch), and the zombie test pins the Spitter's target.

**Zombie enemies and the green arena (Nov 2026), protocol 128:** owner's asks, from `Docs/Mockups/zombie-enemies.html`.
Seven `EnemyType`s at the end of the enum (Shambler, Runner, Spitter, Crawler, Bloater, Shrieker, Mender; `Zombies.Is`)
with `EnemyDef`s (green, in the Practice picker) and Bestiary entries in a new "Zombies" group. `Zombies.Pick` weights
by wave (Runner picks are packs of three), `BuildZombieWave` caps the support kinds per wave. AI hooks: `ZombieMove`
(MoveEnemy: Shamblers sway, Crawlers skitter, Spitter/Shrieker/Mender keep their distance; `FrenzyT` x1.3),
`ZombieSpecial` (Special: the Crawler's grab sets the player's `StunT` and holds on, released by 25 damage since the
grab, death or the stun ending; the Bloater bursts on contact; the Shrieker's scream sets `FrenzyT` within 320 px;
the Mender heals within 220 px with `FxKind.Link` threads), `ZombieFire` (EnemyFire: the Spitter's glob is a
`PatchKind.Acid` patch with a landing ring). New `PatchKind`s `Acid` and `Spores` (the Bloater's death, in
`MaybeRise`); `HoneySlowAt` now takes the strongest of honey, acid and spores; `TickAcid` burns. What each game draws
rides in the enemy's `Phase` bits (`Zombies.PhFrenzy`, `PhGrab`, `PhAct`), so the snapshot didn't change; the bump is
for the new enum values. `Game/ZombieLook.cs`: shapes (`DrawZombieBody`, in place of `Polygon` in `DrawEnemy`), haze,
eyes, drips (`Sparks`), frenzy streaks, the Crawler's vines and its ring (`DrawZombieUnder`; `DrawStunned` skips a
player a Crawler holds), the acid and spore patches; `WorldRenderer.DrawWorld` paints the dark green floor, grid and
border while the modifier is on (`ZombieArena`). Tests: `zombies` (the mix, each kind's trick, the friend's game
seeing grabs and frenzies). Preview: `MODS=65536 Tools/Preview/shot.sh <name> hud 7 4 1`.

**Modifier tiers line up and fold (Nov 2026), no protocol change:** owner's asks: the tier headings weren't aligned (a
tier carried on along the previous one's row, so RISKY's heading sat mid-row), and they should collapse.
`ModifiersUi.DrawModifiers`: each tier starts its own row under a full-width heading (fold marker, tag, blurb, line,
and on the right "n on", or "n hidden · n on" when folded); clicking it folds the tier (`ModTierFolded`/`FoldModTier`,
a bit per `ModLean` in PlayerPrefs `mods_folded`). Cards stay 84-104 tall; the list scrolls (`modsScroll`, mouse
wheel, a thin bar) inside a `GUI.BeginGroup` when the open tiers don't fit. Preview: `LOBBYTAB=2 MODSFOLD=<bits>
Tools/Preview/shot.sh <name> lobby 1 2 1`.

**To-do items 1-5 (Nov 2026), protocol 127:** owner's list in `Docs/AfterReset.md`. (1) **Pause menu models came in
before the menu:** GL ignored `Ui.Alpha`, so models in a fading panel (the run card's boss, the party) drew at full
strength while the panel faded in. `BeginUiGl` now takes `Ui.Alpha` into `glAlpha`, which `SetColor` multiplies in
(`EndUiGl` puts it back to 1; the arena's own drawing never goes through it, so the render check is unchanged). (2)
**Defend the Crystal** spawns melee (Swarmer, Lunger, Brute) with a Grunt or Sniper only `Challenges.DefendRanged`
(15%) of the time; the `challenges` test checks the mix is mostly melee. (3) **Modifiers tab** grouped by tier in one
grid (Easier, Risky, Harder; a group starts under its own heading, on the same row if there's room), the per-card tier
badge replaced by a colour bar on the card's left edge. (4) **Window opening animations:** a drawer list opened from
another (the pause or lobby menu's How to play, Settings, Records) showed its panel at once, because `BeginPanelAnim`
counted from the window's opening whenever the list changed; it now slides in once the window is open. The **Field
guide** rises in (0.4 s, a small bounce, fading up; `guideOpenAt`; preview flag `GUIDEOPEN`). (5) **Knockback slides**
(`Core/Knock.cs`): `KnockEnemy` adds a push to `Enemy.KnockLeft`, `TickKnock` travels it over `KnockTime` 0.18 s;
pushes 24 px or more broadcast `FxKind.Knock` (from, to; at most every 0.25 s per enemy) for the streak, dust and
impact puff (`DrawKnockFx`; preview flag `KNOCK=<seconds>`). Used by the Repulsion Cannon, `Knockback` (Roar,
Shockwave), the Scattergun's shove, Knight swings, Bulldoze, Fortress and the Fuma fan; pulls (Kusarigama, Drain, the
hook) still jump. With the old jump the render check stays IDENTICAL, so the slide is the only change on screen.

**Wave editor (Nov 2026), protocol 126 (no change):** owner's pick, option B (timeline) of
`Docs/Mockups/wave-editor.html`, in the F1 dev tools. `Core/WaveDesign.cs`: `WaveSet` (spawn points A-E, waves, loop)
of `DesignWave` (level, `DesignGroup`s, `DesignProp`s); a group is members or a named squad (`WaveSet.AllSquads`:
the factions' squads and finales, then `Game.Squads`), `From` letters, `At`, `Every`, `ArriveMode` (one by one,
together, line, ring). Text format `.waves` (`ToText`/`Parse`, skips and names bad lines). Playback in the Practice
Range: `Session.DesignTest` → `Game.Design` at `StartMatch` (cleared by editing practice waves, leaving practice
mode, or leaving); `TickWaves` builds `BuildDesignWave` instead of `BuildPracticeWave` (props laid out with
`AddProp`, arrivals sorted into `designQueue`), `TickDesign` spawns on the clock (`SpawnSquadAt` takes a
position, so squads keep their formation at their point), and the wave waits for `DesignPending`. `Game/WaveEditorUi.cs`:
the editor (modal like the Boss Lab setup: `waveEdOpen` in `menuUp`, `modal`, the draw order and Esc), opened from
DevMenu's Tools tab. Stubs gained `Event.shift`, `Vector2.Distance`, `EventType.MouseDrag`, `KeyCode.Delete`,
`Mathf.Ceil`. Test: `wavedesign` (in features). Preview: `Tools/Preview/shot.sh <name> waveeditor` (`EDT`, `EDGROUP`,
`EDPROPS=1`, `EDLOAD=1`, `MOUSE`).

**Main menu: the tip chip, the box in the middle (Nov 2026), no protocol change:** option A of
`Docs/Mockups/menu-tip.html`. `DrawMenuTipChip` (Menus.cs) draws the line under the title as a capsule (new
`Ui.Capsule`) with a TIP (blue, `ui_bulb`) or RECORD (gold, `ui_trophy`) tag, the topic in bold and the rest; each line
slides up and fades in, and on up as it goes (8 s each). `MenuCard` is centred on the screen (y 253) and the buttons
and Join's row are placed from it; the title, axis, arc and chip moved up to fit (title y 126, axis 188, chip 220).

**Main menu "Big play" with the pen plot intro (Nov 2026), no protocol change:** owner's pick: design D of
`Docs/Mockups/menu-redesign.html` with intro B and the box of `Docs/Mockups/menu-d.html`. `Menus.cs`: the title at
y 174 (size 74) on its axis (y 236), the shot's dashed arc over it (`DrawMenuArc`, `ArcAt`) and a line under it
(`MenuTip`: best run, then How to play's tips, every 8 s); `MenuCard` is now the box behind Host game (with
`Ui.PulseRing`), Join (opens `MenuJoinRow`: address and Connect, or the connecting status and Cancel) and Continue (the
saved-run count); the bottom bar has the name field, Records, Settings, Quit. The port moved to Settings, Gameplay. Name,
port and last address are saved (`player_name`, `net_port`, `join_address`; the command line still wins).
`MenuIntro.cs`: the intro (2.75 s; a 0.45 s fade with Reduced effects or effects off), once per launch, skipped by
any key or click; buttons take no clicks until it's over. `LobbyEnter.cs`: the title and axis start from the new
spot, the arc and tip fade, the bar drops without the name, which flies (`MenuNameRect` to the party card) and hands
over to the party card piece. Stubs gained `Input.anyKeyDown` and `PlayerPrefs.GetString/SetString`; the preview's
`LOBBYIN` works again (`DrawLobbyEntering` is overloaded, so it's looked up by its empty signature).

**Boss Hunt: ten waves of the boss's own enemies (Nov 2026), protocol 126:** owner's asks: the Tab screen still said
"wave n / 50" in a hunt, and hunts should be 10 waves with the boss's own enemies building up. `Hunts.Length` 10, all
hunts start at wave 1 and the boss comes on wave 10 (`HuntDef.BossWave`/`FirstWave` gone; `HomeWave` is only for
text). Hunt-aware helpers in `Hunts.cs` replace the "every fifth wave" sums where a hunt can reach them: `IsBossWave`,
`BossAt`, `NextBossWave`, `LastWave`, `ApproachBossAt`, `ApproachStepAt` (BuildWave, Waves.cs's boss name, Gold
Rush's shop, paths (none in a hunt), props, Zombies, the HUD, Tab screen (header, chips, route: "waves 1-9" then the
boss), pause menu, boss music, approach chip and banner line, session recordings). Build-up waves: `ApproachShare` is
1 on waves 1-9, `BuildApproach` filters the faction by `Hunts.Step` (1-4 over the nine waves) and adds the Finale on
waves 5 and 9 (`Hunts.FinaleWave`). The Warden is fought at wave-10 numbers (health, damage growth; the build boost
compares with a typical wave-10 build). Protocol bumped though no message changed: an older game would disagree on
where a hunt's boss is. Tests: `hunts` (only faction enemies on waves 1-9, every kind seen, boss on 10, client's
`LastWave`). Preview: `HUNT=1 STATSTAB=0 Tools/Preview/shot.sh <name> tab 6 12 2`.

**Ghost bullets after a clear; core suite green (Nov 2026), no protocol change:** `core` had 3 failures. Two were the
test command, not the game: `run.sh 1 1 core` runs the core match for 1 s (args are start wave, seconds), too short to
level up or reroll; run `Tools/SimTest/run.sh` (defaults) instead. The third was real: the wave-30 Duelist sweep found
2 small shots stuck on the client. `ClearEnemyBullets` (wave cleared, a boss's stage change) broadcasts its clear
mid-tick, but shots fired earlier that tick (here the Duelist's lunge blades) went out in the end-of-tick `Msg.Spawn`,
after the clear, and lived on the client only. `ClearEnemyBullets` now also drops cleared bullets from
`pendingSpawns`. The late-boss sweep logs ghost bullets (count and kinds). Test: `clearghosts` (in features; fails
without the fix).

**Boss Hunt (the Warden) and Zombie Infection (Nov 2026), protocol 125:** owner's asks, from
`Docs/BossHuntsAndPerks.md` and `Docs/ZombieInfection.md`. **Boss Hunt** (`Core/Hunts.cs`, `Game/HuntUi.cs`): the
lobby's mode switch is now three parts (Run / Boss Hunt / Practice; `Session.SetMode(practice, hunt)`, `HuntMode` +
`HuntBoss` ride in `Msg.Lobby`, hence the bump). A hunt (`Game.Hunt`, a `HuntDef`; clients set it at `Msg.Start`) is
the boss's own five waves: `StartMatch` sets `StartWave = FirstWave` for it, `ApproachShare` returns the ramp's steps
unscaled (fully themed by wave 4), `Win()` when the boss wave clears; no portal (`MaybeOpenPortal`), no endless
(`ContinueEndless`, the button and C), not `Saveable`, not a run in Records (`BeginRunRecord` skips RunStarted;
`RecordKind.End` calls `Profile.HuntEnded`: `hunt <boss> <played> <won> <best s>` lines). Unlock: `Unlocks.All` gets
its first entry, `hunt-warden` (Warden beaten); `Hunts.Available` also accepts the boss beaten before hunts existed;
the host's profile decides, and `StartMatch` refuses a locked hunt (the Start button greys out). Only the Warden has a
`HuntDef`; other bosses show "Coming soon" (their stretches start mid-run and need a starting build). HUD: "HUNT ·
WAVE n / 5", HUNT COMPLETE / HUNT FAILED. Preview: `HUNT=1 Tools/Preview/shot.sh <name> lobby 1 2 2`. **Zombie
Infection** (`Core/Zombies.cs`, `RunMod.ZombieInfection` = 65536, Harder): `BuildWave` hands regular waves to
`BuildZombieWave` (Swarmer pairs, Brute from 3, Lunger from 5; count x1.5 to x3 by wave 20); `ZombieSpawnReady` caps
alive at 60 and divides the spawn interval by the count multiplier; XP x `ZombieXpScale` (1/multiplier, 0 for
`Enemy.Risen`); `MaybeRise` after each kill (25%, not burning, not a melee weapon's kill, not boss waves or rooms)
queues a `Riser` with a green `FxKind.Target`; `TickRisers` in Fighting brings it back at half health; the wave waits
while `ZombiesRising` (counted in `Remaining`); cleared in `WaveCleared`/`Stop`. Tests: `hunts` and `zombies` suites
(in features).

**Main menu: four stacked buttons (Nov 2026), no protocol change:** owner's ask. `DrawMenuSecondary` drops Field guide
and How to play (both stay in the lobby's and pause menu's drawers) and stacks Continue a run, Records, Settings and
Quit, each the card's width (500) and 34 px tall, 8 px apart. LobbyEnter's drop of these buttons is unchanged (it
calls the same function).

**Pause menu: Leave to lobby (Nov 2026), no protocol change:** owner's ask. `PauseChoices` gains "Leave to lobby"
(`PauseToLobby`), shown to the host only (not in the Boss Lab) through `PauseRows()`, which the drawer and ↑ ↓ / Enter
now walk (`pauseSel` is a row, not a choice). It calls `session.ReturnToLobby()` (Msg.ToLobby for everyone);
`Game.Stop` saves the run first, so it can be continued from the main menu.

**Pause menu: the players move into the run card (Nov 2026), no protocol change:** owner's ask. The drawer no longer
has the party or the "↑ ↓ Enter · Esc to resume" line; `DrawRunCard` takes its full rect (`W - 500, 54, 450, H - 94`)
and `DrawPartyStack` puts the players at its foot as `PartyCompact` cards (46 px: class icon, name, class and weapon
(the weapon only when it fits), level, a thin health bar), the first at the bottom and each next above it, under a
PLAYERS heading; they rise in with `PartyAnim`.

**Main menu: Field guide and How to play; Settings is three groups again (Nov 2026), no protocol change:** owner's
asks. `Menus.DrawMenuSecondary`: a third row; the left column is Continue a run, Field guide, How to play, the right
Records, Settings, Quit. On the menu screen `fieldGuideOpen` now stays open by itself (ArenaApp only drops it in the
lobby without its menu) and is drawn first. `SettingsGroups` back to Audio / Visual effects / Gameplay
(`SettingsHowTo` and its drawer swap are gone; How to play is `DrawHowToWindow` from the main menu). Settings in the
pause and lobby menus now uses the drawer key "pause" (it was "pause-settings", which reset the list so its rows
didn't slide in from the right). The lobby's "Esc · menu" hint is gone.

**The lobby's menu on Esc; the Field guide leaves Settings (Nov 2026), no protocol change:** owner's asks.
`MenuDrawer.cs`: `lobbyMenuOpen` / `OpenLobbyMenu` / `CloseLobbyMenu` / `DrawLobbyMenu` (title MENU; rows Back to the
lobby, Settings, Field guide, Leave to main menu, Quit game; ↑ ↓ Enter via `LobbyMenuKeys`, `LobbyChoose`). It reuses
the pause menu's drawer key ("pause") and `pauseSettings`, so its Settings swaps into the same drawer and Back returns
to its list; it opens with its own clock (`winOpenAt`) and closes through `CloseWindow`. ArenaApp: Esc in the lobby
(no text field focused, not entering or leaving) opens it; Esc backs out of the Field guide, then its Settings, then
closes it; the lobby is drawn on Repaint only under it; the Field guide may stay open in the lobby while
`lobbyMenuOpen`; `lobbyMenuOpen` drops off any other screen. Leave to main menu runs `StartLobbyLeave` (the reverse
animation). `LobbyUi.DrawLobbyBottom`: no Leave button or cog; an "Esc · menu" hint bottom-left; Ready (and Start for
the host) centred (`readyX = W/2 - 260` host, `W/2 - 120` client). Settings' How to play no longer has the Field guide
row (the Field guide is now only in the pause menu and the lobby's menu; the main menu has none). Preview:
`LOBBYMENU=1|settings Tools/Preview/shot.sh <name> lobby 1 2 2`. Later: both the lobby menu and the pause menu got a
**How to play** row under Field guide (`pauseHowTo`; it draws `DrawHowToContent` in the drawer with key "pause-howto";
Back and Esc return to the list; reset with `pauseSettings`). `HowToColor` is shared with the main menu button.
Preview: `LOBBYMENU=howto`.

**Menu windows: slide and stagger (Nov 2026), no protocol change:** option A of `Docs/Mockups/menu-animations.html`
(published: https://claude.ai/artifact/2hhgvuxPgpyUqJRE5vBHfj), in `Game/WindowAnim.cs` (was PauseGlitch.cs: the
glitch opening, its frame grab and slices are gone; `Sfx.PauseTear`/`PauseType` stay in the enum unused). One clock
for every drawer window: `winOpenAt` (set by `DrawerIn(key, opens)` when the key changes; the pause menu sets it from
`pauseAnim` each frame, `WinOpenLen` 0.9 s open / `WinCloseLen` 0.3 s close) and `winCloseK`. `BeginDrawer` slides by
`DrawerSlide`, notes its list by title (`NoteList`: the window's first list staggers in from the left from 0.18 s, a
later one, e.g. pause → Settings → How to play, from the right now), raises a new title (`TitleIn`) and draws the
underline across (`UnderlineIn`); `DrawerRow` takes the next stagger (`NextRowAnim`, 0.06 s apart; a row still
invisible isn't drawn or clickable); the pause party uses `PartyAnim`; `BeginPanelAnim(key, drop)` / `EndPanelAnim`
wrap the open section's panel (and the run card): the first drops in with `BackOut`, a new one slides in 80 px from
the right, both fade as the window closes; `PushShift` moves IMGUI (GUI.matrix) and the canvas's GL together
(`uiShift`, added in `CanvasPx`). Closing: `CloseWindow(done)` (Back buttons and Esc for Settings, Records, How to
play) runs `TickWindowClose` and calls `done` once out; meanwhile the window draws on Repaint only
(`SkipWhileClosing`). The pause menu slides out by `pauseAnim` falling. `drawerKey` resets when nothing is open, and
when the pause menu starts opening (so a friend pausing gets the same opening). Lobby: its dim fades with `WindowVis`.
Preview: `PAUSEANIM=0..1` (opening; `PAUSECLOSE=1` closing), `WINT=<s>` / `WINCLOSE=0..1` for settings/records/howto.

**Menu windows as a side drawer (Nov 2026), protocol 123:** option C of `Docs/Mockups/menu-windows.html` (published:
https://claude.ai/artifact/Vojq2a4CDtaQsJbGUfHaPE), in `Game/MenuDrawer.cs`. Shared pieces: `BeginDrawer`/`EndDrawer`
(a `GUI.BeginGroup` sliding in by `DrawerIn(key)` 0.28 s, per window), `DrawerRow` (icon, name lit with a fading strip
when open, a line, a key cap or tag), `SwitchRow` (a pill switch), `PartyMini` (small trophy card), `Chip`,
`IconTile`, `OpenSectionPanel` (the right panel, `DrawerPanel`), `DrawCorners`. Windows: `DrawPauseWindow` (replaces
`DrawSettingsPanel(true)` inside `DrawPauseMenu`, so the glitch opening still applies; `pauseSel` with ↑ ↓/W S + Enter
via `PauseKeys`, hover moves it only when the mouse moves; `pauseSettings` shows Settings inside the pause menu, Esc
backs out of it; `DrawRunCard` with the path, `Modifiers.All` chips, `Diff.Name`, the next boss via
`DrawEnemyPreview`, and this stretch's room from `lastRoomKind/Won/Wave`, which ChallengeLook's `TickChallengeLook`
records as a room ends), `DrawSettingsWindow(paused)` (`settingsGroup`; `DrawFxPreview` is a GL mini fight that
follows fxOn/fxShake/fxNumbers/fxCallouts), `DrawRecordsWindow` (`recordsSection`), `DrawHowToWindow` (`HowToTopics`,
tips reuse `DrawTipGlyph` by index or a pixel icon `"icon:name"`; `DrawPortalDiagram`). `DrawSettings`, `DrawRecords`,
`DrawHowToPlay` now call these; the old `DrawSettingsPanel`, `DrawFxOptions`, `DrawAudioOptions`, `TipRect`,
`RecordLine` are gone (`VolumeSlider`, `KeyCap`, `HowToKeys/Actions/Tips` stay). Main menu Esc now closes Settings,
How to play and Records too. Records' challenge rooms: `Profile.RoomsPlayed/RoomsWon` (lines `room <kind> <played>
<won>`), filled by `RecordKind.Room` (RunSaves.cs, sent by the host from `WinRoom`/`LoseRoom` via `RoomRecord`; a new
record kind, hence the protocol bump). Preview: `SECTION=n Tools/Preview/shot.sh <name> settings|records|howto 12 4
2`, `PAUSESET=n ... pause` (Settings inside the pause menu).

**Leaving the lobby: the way in, backwards (Nov 2026), no protocol change:** the lobby's Leave button calls
`StartLobbyLeave` (LobbyEnter.cs) instead of `session.Leave`: `leaveT` runs for `LeaveLen` 1 s while
`DrawLobbyLeavingToMenu` draws `DrawLobbyEntering(e, leaving: true)` with `e` going from `LeaveFrom` 1.15 s back to 0
(skipping the entrance's last half second, which is only the party card's ring, also left out), so your card leaves
first, the pieces slide out, the panel shrinks into the menu card and LOBBY flies back as the title. `TickLobbyEnter`
calls `session.Leave("")` as it ends (the menu's first frame matches its last), or drops it if the lobby closed
another way. Preview: `LOBBYOUT=<s> Tools/Preview/shot.sh <name> lobby 1 2 1`.

**Challenge rooms (Nov 2026), protocol 122:** `Core/Challenges.cs` (logic) and `Game/ChallengeLook.cs` (look, HUD).
Owner's choices: a portal guaranteed once every five waves; challenges Defend the Crystal, The Gatekeeper (mini-boss),
Kill Rush, Hold the Zones, Mirror Match; safe to fail; prize = rare-or-better pick + 25% legendary. **Flow:**
`WaveCleared` calls `MaybeOpenPortal` (host; wave%5 in 1-3, chances 1/3, 1/2, 1 so exactly one per stretch,
`portalBlock` remembers it; the first stretch skips wave 1; off in practice, `Game.ChallengesByDefault`/`ChallengesOn`
off in tests unless asked). `RoomPhase` Portal (Intermission; `TickPortal` fills `PortalFill` from who's standing in
it, capped at 0.95 until everyone alive is in; `RoomT` 15 s counts only while nobody's picking; the break's
`StateTimer` holds) → `EnterRoom` (State = Fighting, props/patches/zones/bullets cleared, players moved with
`MovePlayer`/`Msg.Teleport`) → Intro (3 s) → Active (`TickChallenge`; `TickWaves` runs `TickRoom` instead of the wave
while `InRoom`) → Won (altar; touch or 12 s → `ClaimPrize`: `PrizePicks`++ and maybe a `LegendaryQueue` entry for the
next boss) → Claimed → `ExitRoom` (back to Intermission, StateTimer reset, downed revived at 30%, `NextOffer`) / Lost
(`CheckGameOver` → `RoomCatchesWipe` when all are down in a room; crystal broken; time out). **Challenges:**
`RoomSpawner` streams enemies from the edges; `RoomTarget` (after `NinjaTarget` in `TickEnemies`) sends 2 in 3 at the
crystal (Defend) or half onto the zone (Zones), and a shadow at its own player; `TickCrystal` (enemy shots and bodies
hurt it), `TickZone`; `RoomKilled` (from `OnEnemyKilled`) counts the rush. **New enemies** `EnemyType.Gatekeeper`,
`Shadow` (end of the enum; `EnemyDef.Mini` = boss bar, Juice entrance card and big death, none of a boss's rules): AI
in `RoomMove`/`RoomSpecial`/`RoomFire` (hooked in MoveEnemy, Special, EnemyFire; exempt from shooter turns and the
bullet budget); health `SpawnHp` → `BossEquivalentHp(wave)` (the Warden's at that wave with the build boost) ×
`MiniBossShare` 0.5 / `ShadowShare` 0.3 (bots: Gatekeeper ~16 s, shadows ~23 s); `ShadowOf` (enemy id → player id)
rides in the snapshot. **Prize:** `Player.PrizePicks`/`OfferPrize` (synced), `Upgrades.PrizeOffer` (rare 55 / very
rare 45 via `RollWeighted`), no rerolls; the picker says CHALLENGE PRIZE. **Sync:** `SyncRoom` in `SyncSnapshot`
(phase, kind, clocks, portal, crystal, kills, zones, shadows). **Look:** `DrawRoomFloor` (tint, rune circle, wall
glyphs), `DrawChallengeWorld` (portal swirl + fill ring + threads, zones, crystal with health ring, altar with light
column), `DrawGatekeeper` (telegraph line in phase 1), `DrawShadow` (the player's own `DrawPlayer` on a puppet
`Player`, tinted by `glShadow` in `SetColor`), `DrawChallengeHud` (warp flash, portal card, title card, objective
plate, result banners), `GatekeeperNote`. Bots: `BotRoom` (portal, zone, crystal, altar). Dev: F1 Match → Challenge
rooms buttons (`DevOpenPortal(kind, true)`). Bestiary: new group "Challenge rooms" (page entries with `share`, preview
AI for both; `BestiaryData.Groups`). Tests: `challenges` suite (in features); report `run.sh 1 1 rooms [last wave]
[seed]` (Good duo through a run: each room's result, boss times for comparison). Preview:
`ROOM=portal|defend|boss|rush|zones|mirror|won|lost [KIND=..] [ROOMINTRO=1] Tools/Preview/shot.sh <name> hud 7 4 2`,
`PRIZE=1 ... picker`. The render check's every-enemy scene now includes the two new types.

**Ability tiles react, "Snap" (Nov 2026), no protocol change:** option A of `Docs/Mockups/ability-tiles.html`
(published: https://claude.ai/artifact/A3eNrzfFMWhy1EGENN1SPv), in `Game/HudTiles.cs`. `DrawAbilityBar` now draws each
tile through `DrawAbilityTile` inside `TileTransformed` (scale around the tile's centre and a sideways offset,
composed onto the current `GUI.matrix` from its `m00`/`m03`/`m13`, so the HUD's own slide-in still applies). Moments:
ready (pop to 108%, 112% and 0.4 s for the ultimate; white rim via the new `Ui.HudRim`; READY slides 14 px in), charge
back (`tilePip` pops with a halo), use (press to 93%, spring back), too early (5 px shake, red rim, red timer; skipped
for the Stun Gun while its shots are out, when downed, and outside a match). Reduced effects halves the pop and drops
the shake and white rim; Visual effects off leaves the bar as before. Checked in `juice.sh`; the render check stays
IDENTICAL. Preview: `TILEFX=ready|charge|use|deny,<seconds> Tools/Preview/shot.sh <name> hud` (the stub ignores
`GUI.matrix`, so the rims, colours and READY slide show, not the pop or shake). Stubs gained `Matrix4x4.m00/m03/m13`.

**Run ending "last breath" (Nov 2026), no protocol change:** option A of `Docs/Mockups/run-ending.html` (published:
https://claude.ai/artifact/NRz8VVni7CYEMw5ke5BesN), in `RunEnding.cs`. `TickEnding` (Update, after the game's) tracks
the last player to go down, the last boss's position and (at wave 50) the enemy shots, then runs `endT` from the
moment the state turns GameOver/Victory (not in the Boss Lab). `EndSpeed` scales the dt given to `game.Update` and
`game.FixedStep` (1 → 0.07 over 0.35 s, held to 1.6 s, then 0.02 behind the results, back to 1 when the state
changes), so the host's slow motion reaches the clients; Juice's own clock follows it, but shake, hurt tint and boss
cards still fade in real time. World: `BeginEndingWorld` zooms the world around the focus with `glShift`/`glZoom` and
sets `glDesat` (SetColor drains colour on a game over); `DrawEndingWorld` draws the victory's rays (glow), ring and
gold sparks; `EndEndingWorld` resets them and adds the vignette and flash; the old 0.45 dim becomes `0.45 * EndBg`.
Results: `DrawRunSummary` takes `EndTitleIn`/`EndTitleScale`/`EndLift`/`EndTileIn(i)`/`EndCardIn(i)`/`EndButtonsIn`
through `EndPart` (alpha plus a `GUI.BeginGroup` offset); with no ending playing they are all 1. The stamp plays
`Sfx.BossSlam` and a `Shake`. Checked in `juice.sh`; the render check stays IDENTICAL. Preview: `ENDING=<seconds>
Tools/Preview/shot.sh <name> gameover|victory <wave> <secs> 2`. Stubs gained `Mathf.Lerp` and `Vector2.zero`/`*`/`==`.

**Pause menu: the signal glitch opening (Nov 2026), no protocol change:** option E of `Docs/Mockups/pause-menu.html`
(https://claude.ai/artifact/5sA1irr1SG3N9Qws6fU3bS), in `Game/PauseGlitch.cs`. `pauseAnim` (0..1) runs up over
`GlitchOpenTime` 0.65 s while `PauseMenuWanted` and down over `GlitchCloseTime` 0.3 s after (the menu keeps drawing,
unclickable, while it closes; `StepPauseAnim` from `Update`). As it opens, `OnPostRender` grabs the frame
(`GrabPauseFrame`: `Texture2D.ReadPixels` after the world is drawn), and `DrawPauseMenu` draws torn bands of it
(`DrawGlitchSlices`: 9 bands shifted sideways with red/cyan lines, mapped from the UI canvas through
`offX`/`offY`/`scale`), the dim, 3 px scanlines, then the panel flickering on with a small shake (`GUI.matrix` nudged;
`GUI.color` and `Ui.Alpha` for its fade) and `pausedTitle` typed out with a block cursor. Reduced effects or Visual
effects off: a 0.15 s fade. Stubs gained `ReadPixels`, `GUI.DrawTextureWithTexCoords`, `TextureFormat.RGB24`. Preview:
`PAUSEANIM=0..1 shot.sh <name> pause` (no torn bands there: they need the grabbed frame; the preview's `GUI.Box`
ignores `GUI.color`, so the panel's box doesn't flicker in it).

**Run launch "drop in" (Nov 2026), no protocol change:** option A of `Docs/Mockups/run-launch.html` (published:
https://claude.ai/artifact/VqMRbVPvwGJqUqQLMBsjdD), in `LaunchIntro.cs`: `TickLaunch` (from Update) notices the screen
going to Game, or a match restarting after the results, and runs `launchT` for `LaunchLen` 4.6 s. `DrawLobbyLeaving`
draws the lobby twice in clipped `GUI.BeginGroup`s (left part sliding left, the rest right, fading; `uiGlBlocked`
pauses its GL models and `DrawShowcase`), `DrawLaunchWorld` (in `DrawArenaContents`) the ripple's ring and each
player's pillar (its foot fades out just above them, with a halo once they land, like the picker's pillar) and landing
ring (Juice's grid ripple is added at 0.35 s), `LaunchHides(p)` keeps a player, their aura, links, aim line and name
off until their pillar lands, `LaunchHoldsHud` holds `hudT` at 0 until 1.7 s, `LaunchHoldsBanner` holds GET READY
until 2.0 s, and `DrawLaunchWave` pops WAVE N in gold as the fight starts. Preview: `LAUNCH=<seconds>
Tools/Preview/shot.sh <name> hud 1 4 2`.

**The Warden's riot (Nov 2026), protocol 121:** camping a corner while the rioters killed the Warden made the Riot
easy (36 rioters x 0.6% could clear the whole 20% stage). Rioters now take at most `RioterCap` 5% a riot turn
(`WardenState.RioterDealt`; past it they're batted away in `RiotersReachWarden`). Its riot shield wears:
`RiotShield`/`RiotShieldMax` (synced; 7% of max health, fresh each riot turn in `NextWardenTurn`), worn through
`WearShield` -> `WearWardenShield` (cracks at 75/50/25%, `WardenShieldCrack`), broken = no blocking and
`RiotBrokenMult` in `HurtEnemy` (`WardenShieldBroken`); drawn cracking in `WardenLook.cs`, boss-bar note "break his
shield (n%)". Boss Lab: the Riot stage still takes about 22 s (13-28 s). The guards still follow you into the last
stand (the owner's price for letting a riot run long).

**The Warden's last stand (Nov 2026), protocol 120:** below 10% the Warden now has a 2 s stage change
(`StartWardenTransition` to `WardenStage.LastStand`) in which it puts on armor: `WardenState.Armor`/`ArmorMax` (synced
in `SyncWarden`), worth `WardenArmorFrac` (0.47) of its max health, taken first in `HurtEnemy` via `WardenArmorTakes`
(breaks with a boom and `Sfx.BossCrack`). Plates lock on at `ArmorPlateAt` with a clang each; drawn by
`DrawWardenArmor` (WardenLook.cs: five plates, falling off as it wears), a calm "ARMOR UP" banner (`StageShift.Armor`:
no pulse or siren, no flashing in the last stand), a blue bar above its health on the boss bar and the slim bars. The
last stand fires long "+" bursts turning 45° each time (`LastStandFacing`, `LastStandCycle`), one orb ring per pause,
red warning lanes, and a stun round every `LastStandStunEvery` 3 s. Tuned with `LABLOG=1 Tools/SimTest/run.sh 1 1 lab
5 2 6 T/K/1 D/Elem/0` (prints the boss's log with times): about 30 s from the armor going on to the kill.

**Main menu into the lobby (Nov 2026), no protocol change:** option A of `Docs/Mockups/host-to-lobby.html` (published:
https://claude.ai/artifact/HBrykRLnx26J9qiw7TFcLk), in `LobbyEnter.cs`, 1.6 s after Host game, a join connecting, or
Continue a run. `TickLobbyEnter` (Update) runs `enterT`; `LobbyEntering` also covers the frame the Host click lands
on, so the full lobby never flashes first. `DrawLobbyEntering` replaces `DrawLobby` meanwhile: the menu card
(`MenuCard`) grows into the loadout panel (`LobbyBodyRect`) with its plot corners, the title flies to the top left and
turns into LOBBY (its axis along, `DrawAxis` now takes an alpha), the buttons under the card drop away (`DrawMenu` is
split into `DrawMenuTitle` / `DrawMenuCardInside` / `DrawMenuSecondary`, which draw only when not `live`), and the
lobby comes in as clipped pieces of `DrawLobby` (`LobbyPiece`), each sliding from its side; your party card last, with
a ring. The pieces are drawn on Repaint only, so nothing takes a click until it settles (not `GUI.enabled = false`,
which Unity draws at half opacity: the lobby looked see-through), and its live models wait (`uiGlBlocked`). Preview:
`LOBBYIN=<s> Tools/Preview/shot.sh <name> lobby 1 2 1`.

**Upgrade picker opening and close (Nov 2026), no protocol change:** option B and close 1 of
`Docs/Mockups/upgrade-picker-dropin.html` (published: https://claude.ai/artifact/B8h7VybcJDoTUQw4ax5m9d); this
replaced the old shatter outro. `PickIntro.cs`: when new cards appear (`SyncSelection` → `StartPickIntro`), a gold
pillar and ring rise out of your character, the title pops in the middle on a dark band and slides up
(`PickTitleMotion`), then each card flies out of you along an arc (`PickArc`), small and turned edge-on, opening out
as it lands with a ring in its rarity colour; the bottom row rises from below (`PickButtonsRise`). Cards are drawn in
flight with `GUI.matrix` (`DrawCardInFlight`), so `DrawTrophyCard`/`DrawUpgradeCard` are unchanged. `PickIntroPlaying`
blocks keys, clicks, Reroll and Take until the last card lands (about 1.1 s; 0.65 s for a reroll or the next of
several picks, which skip the pillar and title: `introFull`). `PickOutro.cs` plays it backwards in 0.95 s: the
passed-on cards dim and fly home first, the taken one flashes and follows, landing with a ring, a short gold pillar
(`PickPillar`: its foot fades out just above your middle with a gold halo round you, option 2 of
`Docs/Mockups/pillar-foot.html`, https://claude.ai/artifact/XRb6rrRvHNuV9CEwYVZepV) and its icon over your head.
Preview: `INTRO=<s> [INTROREROLL=1]` or `OUTRO=<s>` with `Tools/Preview/shot.sh <name> picker`; the preview stub now
applies `GUI.matrix` (translation and scale) to what it draws.

**Weapon ascension strip (Nov 2026), no protocol change:** `LobbyUi.DrawAscensionStrip`: under the weapon and ability
grids, one row with the picked weapon's name over "ASCENSIONS 0/3" and three locked cards (Ascension I › II › III,
`AscensionsPerWeapon`), each with the "Coming soon" tooltip. Placeholders only: ascensions don't exist yet. To make
room the role cards are 46 px, class cards 32 px and spec tiles 38 px; descriptions still fit (checked for Knight
Tank, Hemomancer DPS, Summoner Healer). Locked cards share `DashedCard` and now give the hover tick.

**Dev kill-all key (Nov 2026), no protocol change:** F1 Match tab toggle (`ArenaApp.devKillAllKey`, PlayerPrefs
`dev_killall`); X (host, not while standing on a chest item) calls `Game.DevKillAll(id)` (DevTools.cs), which sets
every enemy's Hp to 0 with `LastHitBy` = that player (shields and bubbles cleared, damage counted), so the next
`Collide` dead-sweep gives XP, score, drops, gold and kill credit as usual. Checked in the `dev` suite.

**Results screen restyled (Nov 2026), no protocol change:** `DrawRunSummary` / `DrawPlayerSummary` (Hud.cs) in the
picker's trophy style: outlined `Ui.Fancy` title (red GAME OVER with the boss on a boss wave, gold VICTORY!), totals
as `Ui.Card` tiles, a card per player (class icon `class_<kit>` on a glowing `Hatch()` backdrop in their colour, role
chip, level chip, a trophy chip per award; name in fancy lettering, rows with bars against the party's best),
`Ui.FancyButton`s (Leave bottom-left; Play again, Back to lobby, Keep going on the right). Preview: `shot.sh <name>
gameover|victory <wave> <secs> <players>`.

**Upgrade picker restyled, "trophy cards" (Nov 2026), no protocol change:** option C of
`Docs/Mockups/upgrade-picker.html` (published: https://claude.ai/artifact/8mQbdsSJXcRpSqRbVPyW8c). `DrawUpgradePicker`
(Hud.cs): an outlined `Ui.Fancy` title (LEVEL UP! gold, BOSS REWARD! legendary, WEAPON MOD), `DrawTrophyCard` (glow +
pixel hatch `Hatch()` backdrop in the rarity colour, the icon at 108 px, rarity chip and keycap, a shine on Rare and
better, a rarity line fading at both ends; the picked card lifts and glows, the others step back), `PickChangeLine`
instead of the stats sidebar (Hold Tab still shows everything), and `Ui.FancyButton`s (Reroll, hidden for boss
rewards; gold "Take <name>"). Weapon mods keep the compact grid (`DrawUpgradeCard`, compact). `DrawStatPanel` stays
for the Tab menu and the Bazaar. Previews: `shot.sh <name> picker` (`PICKCARDS=a,b,c`), `legendary`, `modpicker`.

**Hive Queen's Swarm bombers flank (Nov 2026), no protocol change:** in Swarm's Bombers turn (only; Barrage and the
last stand keep the straight ones, Phase 0) `LaunchFlanker` (Queen.cs) sends each bomber out to one side (`Phase 1`,
alternating `QueenState.FlankSide`) for a flank point `FlankOut` 300 px square beside its target (75/105/60/120
degrees near an edge), steering at `FlankTurn` (`FlankerMove`, so it swoops), with `RetargetT` held so it keeps to
that player. At the point (or after `FlankMaxOut` 3 s) `FlankerTurnIn` locks onto where they stand (a Target marker)
and it dives (`Phase 2`, `FlankDive` x speed). Drawn as a wasp by `DrawFlankBomber` (QueenLook.cs): trail, beating
wings, striped body, burning sting; gold out, red-hot with a dashed line diving. Drones flying home now count as home
within 6 px of her edge (enemy separation could hold one at exactly touching distance forever). Tests: `queen`
(bombers come in 50+ degrees off her side), `bosses` (the loop). Preview: `FLANKERS=1 Tools/Preview/shot.sh <name>
hud`. Bestiary demo and text.

**Locked loadout slots (Nov 2026), no protocol change:** `LobbyUi.cs`. Every loadout row shows slots for content still
to come: 4 role cards (1 locked), classes in rows of 4 with the icon beside the name (8 slots, 2 locked), weapon and
ability as side-by-side 2 x 2 grids (1 locked each). Counts are `RoleSlots`, `ClassSlots`, `WeaponSlots`,
`AbilitySlots`; real content past a count simply grows the row. `LockedSlot` draws the dashed padlock card; hovering
one shows a gold "Coming soon" tooltip, drawn last (`DrawLockedTip`). New `ui_lock` icon in `PixelArt.cs`. A Wildcard
4th role (pick any class's weapon/ability) was discussed for that slot but not started.

**Warden Lockdown: fairer gates, gold locks, a gas barrage (Nov 2026), protocol 117:** `Warden.cs`. Gates: a cross
turn's three all come from `WardenState.GateSide` (flipped as each cross turn starts; the first is the top), so two
never close in; pinned at the edge a gate rolls over you without a second hit; `LobGas` retries until it's clear of
the newest gate's lanes (`InDoorLane`/`InGateLanes`), and `AddWardenGate` rolls each gate's doors and lock clear of
gas already down. Locks: `WardenGate.LockX`/`LockHp`/`LockOpen` (synced); `GateLockHit` in `Collide` takes your shots
(`LockHits` 6), then `InGateGap` lets you through a `LockDoor` (110 px) door there; drawn by `DrawGateLock`.
Lockdown's turns are now Cross 12 s, Rest 2, **Gas** 8, Rest 2 (`WardenTurn.Gas`, at the end of the enum):
`GasBarrage` drops two volleys (0.3 s and `GasVolley2` 4.3 s) onto one of `GasPatterns` (not the same twice; mirrored
at random; the ring is round the Warden), and `WardenFire` sends 3-shot aimed bursts every `GasBurstEvery` 1.2 s.
Checks in `warden`.

**Balance from a duo run to wave 25 (Nov 2026), protocol 116:** (1) Boss health: the build-strength boost and the
late-boss boost no longer multiply: `SpawnHp` applies `LateBossBoost(wave)`, and `SpawnEnemyAt` sets `PowerScale =
max(late, BossPowerScale())` and multiplies by `PowerScale / late`; `BossPowerCurve` 0.75 -> 0.6 (the run's Colossus
would have had ~119k health instead of 201k). (2) Damage to players that grew with the wave on top of the difficulty's
3% a wave lost its own per-wave term (boss contact, Beam Sentry, Colossus stomp, Singularity cage, Reaper tether,
Lich, Herald, Devourer bite, Eclipse, LateGame hazards, Burning Grounds); the Bestiary shows these as "X, +3% a wave"
via `dmgGrow`. (3) Beam Sentry: 16 a sweep, and at most `MaxSentries` 3 a wave (`CapSentries` after `BuildWave`:
squads that would bring more break up, spare Sentries become Snipers or Grunts). (4) Healer 140 HP (was 120). (5)
Warden and Hive Queen `BossHpMult` 2.5 (were 4 and 3.5). Also Chain Light: Barrage's extra cones deal
`BarrageCopyDamage` 60%, one cone is at most `LightConeMaxHalf` (100 degrees across) and all cones fit in
`LightConeMaxSpread` 200 degrees (`LightConeHalfFor`), so Homing + Magnum + Barrage can't make a ring round you any
more (the run had three 150-degree cones).

**Colossus stomp rings reworked (Nov 2026), protocol 114:** gaps are placed by `GapBeside` (a short step, `StompStep`
130 px, to one side of a player, alternating via `ColossusState.StompSide`; two-gap rings put the second by the other
player via `SecondGap`), never narrower than `RingGapMinArc` 100 px (`RingGapHalfAt`, also used to draw the gap). Lava
Stomps used to put the gaps 90 degrees round from you, which needs about your top speed to reach. Caught, a ring
passes over you: `ColossusPush` always puts you just behind it (no carrying you out to the wall, no roll-over
teleport); the host hurts you once when it sees you cross (`StompRing.Outside`, replacing `Rolled`), for `StompDamage`
20 (no per-wave term; the difficulty's growth scales it). Boss Lab, wave 25 Good bots T/B/2 + H/M/1, 3 runs: before 1
win and 2 time-outs, stomps 1,078 damage, 0.7 downs a fight; after 3 wins, 174 s average, stomps 433, no downs.

**Readable regular waves (Nov 2026), protocol 113:** `Readability.cs`. Shooters take turns: `AssignShooterTurns` (each
tick, before the enemies act) gives `Enemy.ShootT` turns of `ShooterTurn` 5 s to whoever has waited longest
(`ShootWait`), up to `ShooterLimit` (4 + wave/6, max 8, +25% a player, x `DiffKnob.Shooters`, a new Difficulty knob);
after a turn `ShootRest` 1.5 s. `TakesTurns`: not bosses, not melee, not things that never fire (FireT >= 50). A
volley without a turn is held like the bullet budget. Wind-up: `Enemy.Winding` (synced in `SyncEnemy`) for the last
`WindUp` 0.3 s before a volley; drawn in `DrawVarietyOnEnemy`. Shapes: `Bullet.Look` (`ShotLook` Round / Dart /
Crescent), sent in the spawn message's flag bits 2-3 (`Bullet.Sent` is its pending spawn entry so it can be set after
`SpawnBullet`); `EnemyShot` sets it (`LookFor`: curving = crescent, within 0.5 rad of the target = dart) and
`NoteVolley` makes any volley of `RingVolley` 6+ shots round; drawn by `DrawShapedShot`. Rings thinned: Burster 8 +
wave/2 (max 12) at 115, Charger 8 at 120, Phantom 8 at 115, Mortar bombs 7 / 10 at 125, Volatile 12 at 125, Death
Throes 6 at 120. Tests: `readable` (feature suite).

**The Warden fights back less one-sidedly (Nov 2026), protocol 112:** four changes in `Warden.cs` / `WardenLook.cs`.
**Exposed:** `CheckExposed` (every tick, after the turn logic): the searchlight's pool on the Warden for `ExposeCatch`
(0.3 s) blinds it for `ExposeTime` (3 s; then `ExposeCooldown`): `WardenFire` holds fire, `HurtEnemy` multiplies by
`ExposedMult` (1.5; `WardenExposed`). **Stunned:** a perfect parry of a `StunShot` marks the reflect `Bullet.StunBack`
(host only); the reflect flies straight at the Warden (`Collide`'s reflects loop) and its hit calls `StunWarden`
(`WardenStunTime` 1.5 s: `WardenSpecial` returns early, `WardenMove` stands still, no fire). **Fan telegraph:**
`WardenFire`'s Fan case warns first (`FanAim`, `FanWarn` = `FanWarnTime` 0.35 s; `fanPending`), then fires along
`FanAim`; a volley every `FanEvery` 0.8 s (was 0.45 s, unwarned). **Alarm:** entering `LastStand` turns the
searchlight on (`LightMode.Follow`; it follows the target, never catches, but still exposes); `DrawWardenAlarm` (from
`DrawArenaRules`) darkens the arena red with holes round players, the Warden and the light's pool; the tower's red
beacon turns. `ExposedT`, `StunT`, `FanAim`, `FanWarn` are in `SyncWarden`; clients count them down in
`UpdateWardenView`. Looks: glare and a countdown ring (`DrawWardenOver`, after the enemies), stun stars, red fan lanes
(`DrawWardenUnderlay`), "EXPOSED · +50% DAMAGE" / "STUNNED" tags (`DrawWardenTags`, HUD). Bestiary text updated (its
web demo's Warden sim isn't). Tests in `warden`; preview `WARDEN=0.8 WARDENSET=exposed|stun|fan` and `WARDEN=0.05 ...
hud 5 14` for the alarm.

**Health barrels in boss fights (Nov 2026), protocol 111:** `PropKind.HealthBarrel` (at the end of the enum).
`TickBossBarrels` (Props.cs, from `HostTick`) drops one every `BossBarrelMin`-`BossBarrelMax` (18-26 s; the first
after `BossBarrelFirst`, 15 s; `BossBarrelCap` 2 at once) while a boss is up, clear of the players and 120 px past the
boss; like a pot it isn't solid and breaks under your shots or when walked into (`Smashable`), into `BossBarrelOrbs`
(2) small health orbs. Regular waves' `WaveProps` clears any left over. Look: `DrawHealthBarrel` (PropsLook.cs);
preview `PROPS=1` has one. Checked in `props`.

**Shuriken on bosses (Nov 2026), no protocol change:** from a solo Ninja session (32 boss damage a second on the
Warden). The fan was 0.22 rad between stars, so past about 240 px only the middle star hit a boss; now `Game.StarFan`
0.08 (all three hit out to about 650 px). Stuck stars no longer just drop after `StarStick` (5 s): `TickStuckStars`
sends them back with `ReturnStar` (the recall's second hit and the cut on the way back), so the double-tap only brings
them sooner. Measured damage a second 52 → 157 (`weapondps write`); Boss Lab, Good bot, wave 5 solo: average fight 150
s → 98 s, boss left at 39% → 24% on average (the bot still goes down in 2 of 3: the Ninja's 85% health). Armory text
and demo updated (`Docs/Armory/out` rebuilt, not republished). Checks in `fourmore` (run last in it: their 5 s wait
would otherwise move the wave's end, and its +30% heal, into the Lightbringer checks).

**Arena props (Nov 2026), protocol 110:** `Props.cs` (logic) and `Game/PropsLook.cs` (look, the item card, the HUD
line). `PropKind` Crate / HeavyCrate / Pot / Barrel / Chest on `Game.Props`; chest items are `Loot` on `Game.Loots`
(every game has all of them, only the owner's draws them). `WaveProps` (after `BuildWave`) tops props up by
`CrateTarget`/`PotTarget`/`BarrelChance`/`ChestChance`, or clears them for a boss; `PropStops` in `Collide` (crates
stop enemy shots, barrels any shot, pots break under yours), `PropBlast` from grenades/flak/splashes, `TickProps`
(fuses, touches, breaks, `ExplodeBarrel`, `OpenChest`), `PushOutOfProps` for your own movement (`LocalControl`) and
enemies (`TickEnemies`). The wave waits while `PropsHoldWave()` (`HoldForTreasure` revives the downed). Items:
`RollChestLoot` (`Upgrades.RandomOfRarity`, `LegendaryOffer`), `ResolveLoot` → `Msg.Loot` → `HostLoot` (G / X in
`ArenaApp`), recorded as `"chest"` picks; bots collect in `BotTreasure`. New `FxKind.Debris`;
`PixelArt.RowsFor`/`PixelColor` draw icons in GL. The test runner turns props off by default (`Game.PropsByDefault =
false` in `Seeded`) so a crate or pot can't throw a mechanic test; the core match and `props` tests turn them on. Test
`props` (in the feature suite); preview `PROPS=1` (`PROPS=2`: a legendary item) `Tools/Preview/shot.sh <name> hud 6
4`.

**More visual effects (Nov 2026), no protocol change:** H (HUD hidden) keeps a slim bar per boss at the top
(`DrawMiniBossBars`); every health bar has a damage ghost (`HealthBars.cs`: `GhostBar` for IMGUI and GL bars, keyed
per enemy / player, `GhostHold` 0.45 s then `GhostDrain`); `Ambience.cs`: orb trails, a link to the player and a
pickup pulse (orb movement unchanged), dust puffs on setting off, hard turns and dashes, per-path weather
(`WeatherFor`, fading in); bullet endings (`Juice.cs`: `WhyGone` sorts a gone shot into ran out / hit an enemy /
blocked / hit a player from its last position, speed, life and what was near: a shield's front, a parry, Barrier,
Shield Wall or a blocking minion; then `BulletEnding` plays a fade and wisp, a splash, a tumbling ricochet or a
burst). All checked in `juice.sh`; the render check (effects off) stays IDENTICAL. Not done: exit sparks for piercing
shots (a shot that carries on isn't "gone").

**Visual effects (Nov 2026), no protocol change:** mocked up in `Docs/Mockups/visual-flair.html` (the owner asked for
all of it, with the hurt and low-health tints made a faint red in the corners only and bullet glow 5% softer).
`Game/Juice.cs`, front end only: `UpdateJuice` (start of `DrawWorld`) compares the screen with last frame
(`seenEnemies`: health drops = hit flash, bulge, damage numbers merged per 0.15 s; gone = `Shatter`; `seenShots`:
trails, muzzle flashes, impact sparks; new `Effect`s, marked by `Effect.Juiced`; your own health, level and
legendaries; a new boss = the entrance card). It never touches the simulation or the network. Death detection: any
enemy that's gone after spawning shatters (a client already played a death for every removal); a killing blow's number
comes from the death `Effect` the game plays where it died; the big boss death plays only when no boss is left (not at
the Reapers' fusion). Drawing: `Tri` and the direct `GL.Vertex` calls now go through `Vtx`, and `SetColor` remembers
`glCur`, so a frame can be recorded and replayed: **hit-pause** replays last frame's triangles (`ReplayHeldFrame`) for
`holdT`, at most every 0.4 s. **Shake** is `shakeX/Y` added in `Px` (world only; the lobby's preview uses the clipped
path). **Glow** is a second material (`BlendMode.SrcAlpha` / `One`) between GL batches (`GlowPass`, `SwitchToGlow`);
`GlowDisc`/`FanGradient`/`VQuad` use a colour per vertex, ordered so the preview tool (which colours a triangle by its
first vertex) leaves them out. Settings: `fxOn`, `fxShake` (0-2), `fxNumbers`, `fxReduced` in PlayerPrefs; Settings is
now two columns (Audio, Visual effects). Checks: `Tools/RenderCheck/check.sh` turns effects off and stays IDENTICAL;
`Tools/RenderCheck/juice.sh` (new) drives real frames through hits, kills, a one-shot kill's number, a hold, a blast,
being hurt, a level up and a boss's entrance and death. The stubs gained `BlendMode.One`.

**Boss approaches, step 1: the framework and the Warden (Nov 2026), protocol 109:** designed in `Docs/Approaches.md`.
`Approaches.cs`: `FactionDef` per boss (members with weight and `From`, squads), `Game.ApproachShare(wave)` from two
new flavour knobs (`DiffKnob.ApproachShare`, 60% on every preset; `ApproachRamp`, on: the four waves build up a
quarter at a time), `BuildApproach` (in `BuildWave` before `BuildPathWave`/`BuildSquads`; takes the share off `count`,
no random rolls at 0%) and `MixApproach` (after them: singles at random places in `spawnQueue`, squads in
`squadQueue`). `KnobDef.Flavour`: never red/green, doesn't make a preset Custom, presets leave it alone, `ToText`
writes it after a preset name when changed ("Hard ApproachShare=1"). The Warden's faction: Rioters and Guards
(`GuardMove` goes straight for you when there's no Warden), and two new types in `WardenFaction.cs`
(`EnemyType.RiotShield`, `GasGrenadier`, at the end of the enum; the variety dispatch in `MoveEnemy` is now an
explicit range so new types don't fall into it). **Riot Shield:** its `Phase` packs the shield's cracks (low 3 bits,
`RiotCrack`) and its state (`RiotState`: walk, winding, lunging, low, staggered, flail); shield `RiotShieldShare` (4x)
of its health, worn through `WearShield`/`BlocksShot` like the Shielder's; broken it takes `RiotBrokenMult` in
`HurtEnemy`. The bash (`RiotBash`) is checked on the host, which deals the damage and sends `Msg.Knock` (new) so the
player's own game moves them (`StartKnock`/`ApplyKnock` in `LocalControl`, 900 px/s for 160 px); `Player.BashGraceT`
(host) stops a second bash for 0.8 s. The Warden's searchlight turn calls `SendRiotShields` (two, parent the Warden,
1.25x speed); `HerdSpot` walks them round to the player's side away from the light; cleared on his stage change and
death (`ClearWardenShields`). **Gas Grenadier:** keeps 330 px away, lobs the Warden's gas patch (70 px, 4 s) every 3.6
s; `TickWardenFaction` makes gas hurt when there's no Warden. Bots avoid gas (all of it, the Warden's included: a
change in the Boss Lab) and step out of a winding shield's front. UI: `ApproachUi.cs` (chip above the wave panel, the
banner's line), the Difficulty tab's two rows and Blend / Fully themed buttons, `WardenLook.cs` (`DrawRiotShield`,
`DrawGrenadier`). Session JSON: each wave's `approach` and `approachShare`; `report.py` groups waves by them.
Bestiary: in-game entries (`BestiaryData.Extra`) for both; Rioter and Guard now from wave 1; the page's Warden
searchlight line mentions the pair. Tests: `approaches` (in the feature suite). Previews: `APPROACH=1
Tools/Preview/shot.sh <name> hud 4 6` (every Riot Shield state, Grenadiers, gas), `LOBBYTAB=3 ... lobby` (the
settings).

**Classes are registries (Oct 2026), no protocol change:** adding a class went from edits in ~15 files to two new
files and three registry lines (see "Adding a class" in section 2). `KitDef` (`Classes.cs`: name, blurb, icon, health
and speed shares, hooks `Fire`, `Ability`, `Tick`, `TickEnemy`, `OnShotHit`, `OnKill`) replaced
`KitName`/`KitHpMult`/`KitSpeedMult`'s ternaries, `Menus.KitBlurb`'s switch, `LobbyUi.KitIcons`, `ClampKit`'s
last-class check, FireVolley's weapon-id ranges and the Knight/Ninja/Blood ability chain; the Ninja's and Hemomancer's
ticks and `BloodShotHit` run through the hooks (same order as before; the Knight's tick stays a plain call because it
also times everyone's boss contact). Each class's `WeaponDef`s moved into its file with `Kit`, `Role`, `Choice`
(`Defs.Weapons` is gathered and checked like `Upgrades.Abilities`), and `Sound`, `AreaDesc`, `HomingDesc`,
`BarrageDesc` replaced `ShotSound`'s switch, `AreaWeapon`'s list and `DescFor`'s per-weapon cases. Front end:
`Game/ClassLooks.cs` (`KitLook` per class). `Kit.Mage` and `Kit.BloodMage` are now `Kit.Elementalist` and
`Kit.Hemomancer` (same numbers; old saves load through `KitFromSave`; session JSON now says the new names). New:
`run.sh 1 1 classes` (also in the feature suite), the templates in `Docs/ClassTemplate/`, Boss Lab seats by class name
(`D/Hemo/0`). Proven unchanged: a fingerprint of every weapon's fields, the lobby tables, every weapon's shot sound
and area flag and every upgrade card's text for every weapon (8,000 lines) was identical; the render check (now 16
scenes: two Ninja and three Hemomancer scenes added, after breaking each shot hook on purpose went unnoticed with the
old 11) was IDENTICAL; both suites pass as before. The test found that no ultimate has a pixel icon (by design, it
seems).

**Warden gates stop for a moment (Oct 2026), no protocol change:** each Lockdown gate slides `GateHoldIn` (30 px) in,
stops for `GateHold` (0.3 s) with its doors flashing (`WardenLook.DrawGate`, `Game.GateHolding`), then carries on at
180 px/s. `WardenGate.Warn` now keeps counting down past 0 and `AdvanceGates` sets `Front` from `-Warn` (`GateMoved`),
so host and clients place it the same way from the existing snapshot fields. The web Bestiary's demo does the same.
Test: `warden` checks the stop on host and client. Preview: `WARDEN=0.5 GATEHOLD=1 Tools/Preview/shot.sh <name> hud 5
13`. Asked for after a solo Tank Elementalist run died to the first gate with 14 HP (that session's analysis: a solo
Tank does about a third of a DPS's damage to the Warden, 199-208 s fights in the Boss Lab).

**Four rule-changing modifiers (Oct 2026), protocol 107:** `RunMod.Hunted`, `DeathThroes`, `SolidWalls`, `SecondWind`
(numbers in `Modifiers`: `HuntedDamage`, `ThroesShots`/`ThroesFuse`/`ThroesSpeed`, `SecondWindTime`). `ModsWaveStart`
(Modifiers.cs, called as each wave starts in `TickWaves`) picks `Game.HuntedId` (in the snapshot) and gives
`Player.SecondWind` back on waves 1, 6, 11... (synced in `SyncShop` beside the revive token). Hunted: `PickTarget`
returns the quarry after the Roar check; `HurtEnemy` doubles their damage (`IsHunted`). Death Throes reuses the
Volatile burst queue (`volatileN` holds each burst's shot count). Solid Walls makes `Walled` true (so `KeepInArena`
clamps, bullets stop at the edge, no wrap ghosts); drawn by `DrawSolidWalls`. Second Wind rides `TickReviveToken`
(used before a token) and counts in `CheckGameOver`. The Modifiers tab goes to four columns past 12 cards; HUD chips
say who's hunted and whether your Second Wind is ready. Tests: `rulemods` (in the feature suite). Preview: `MODS=61440
Tools/Preview/shot.sh <name> hud`.

**Boss sounds (Nov 2026, protocol 115):** 15 new `Sfx` (`BossLaserWarn` ... `BossChomp`, at the end of the enum)
synthesized in `SoundBank.cs` (new helpers `Mix`, `Concat`, `Reverse`, `Tremolo`, `Bell`, `Crackle`); every boss's
mechanics now sound at their warning and their strike, through `BroadcastFx`'s sound or `BroadcastSound`
(BossSounds.cs), guarded to once per event. Host-only `Sounds.Add` calls in the boss files (heard only by the host)
are gone. The generic per-turn `BossCue` stays as the soft "new attack" cue. Hear them with
`Tools/SoundCheck/render.sh`.

**Health orbs at boss stage changes (Oct 2026):** `StageOrbs` (Warden.cs, next to `StageStartBurst`, which every boss
calls on a stage change) drops `StageOrbBase` (2, Balance.cs) + one per player small orbs 70-140 px outside the boss;
the Twin Reapers call it at the fusion and the split. Checked in `warden` and `reapers`.

**Class icons (Oct 2026), no protocol change:** `PixelArt.cs` gains `class_soldier` (helmet), `class_elementalist`
(flame), `class_summoner` (spirit), `class_knight` (great helm), `class_ninja` (masked face) and `class_hemomancer`
(blood drop); the lobby's class cards (`LobbyUi.KitIcons`, Kit order) are 48 px tall with the icon above the name
(beside it didn't fit 79 px cards). `Ui.Choice` icon cards now honour `textSize`, and `iconLeft` (role cards) can also
lay out a card without a second line.

**Shooting-pattern enemies (Oct 2026), protocol 106:** three enemies in `Variety.cs` (`EnemyType.Pauser`, `Leader`,
`Stitcher`, at the end of the enum; drawn in `VarietyLook.cs`). **Pauser** (14): fans 8 `Defs.PauseShot` (a new enemy
bullet kind, 53) whose speed is worked out from their age (`PauseSpeedNow`: full speed, slowing to 0 by `PauseFly`,
then hanging), so every machine moves them the same; each has the host-only `Bullet.Rush`, so when its life runs out
`MoveBullets` puts it in `landed` and `PauseRush` fires a SmallShot from there at the nearest player. **Leader** (19):
`Player.Motion` (host only, smoothed from positions in `TrackMotion`) feeds `LeadAngle`/`LeadTime` (intercept, capped
at 1.6 s); Phase 1 is the aim line (Facing tracks the lead, synced as usual), then a pair of FastShots. **Stitcher**
(22): `Defs.StitchShot` (54) orbs; `StitchLinks` ties each orb to its nearest other within `StitchLink` (280 px), each
pair once, from bullet positions alone, so host (damage, in `TickVariety`), screens (`DrawVarietyFloor`) and bots
agree with nothing extra on the wire. `StitchEvery x StitchSpeed > StitchLink` keeps one Stitcher's volleys from tying
along their path. In a squad (Seamstresses) the pair stands side by side `StitchPairGap` apart (`SquadMove`) and the
lower slot fires both orbs on one parallel course: a moving fence. Squads: Hold the line (14), Hunting party (19),
Seamstresses (22). Bots: `PauseTravel` predicts the stop; threads count as danger. `V2.Dot` added. Tests: `patterns`
(in the feature suite). Preview: `PATTERNS=1 Tools/Preview/shot.sh <name> hud`. Bestiary entries and demos (`node
Docs/Bestiary/build_data.js` regenerated the Field guide); `Docs/checks/bestiary-patterns.js` screenshots them.

**Loadout spec sheet (Oct 2026), no protocol change:** `LobbyUi.cs`. The role and class blurbs moved into their
heading lines (`HeadingWithBlurb`); weapon and ability are two columns of stacked `Ui.Choice` buttons; below them
`DrawSpecSheet` shows the picked weapon and ability (name, description, number tiles from `WeaponDef` / `AbilityDef`:
companions, damage per hit/swing, shots or swings per second; cooldown with a looping ring, charges, health cost) in a
dark panel with plot corners in the role colour.

**UI hover and click sounds (Oct 2026), no protocol change:** a soft click when a button, toggle or `Ui.HitArea` is
clicked (`Ui.TakeClick`, `SoundBank.PlayUiClick`), and a soft tick (`SoundBank.PlayUiHover`, Effects volume, not an
`Sfx` so it never goes over the network) when the mouse moves onto a different button. `Ui.Button`, `Ui.Toggle` and
the new `Ui.HitArea(r)` (use it instead of `GUI.Button(r, GUIContent.none, GUIStyle.none)` for hand-drawn cards, tabs
and swatches) report themselves via `Ui.NoteHover`; `OnGUI` brackets the frame with `Ui.BeginHoverFrame` /
`EndHoverFrame`. The stubs gained `GUIUtility.GUIToScreenPoint`.

**Difficulty (Oct 2026), protocol 105:** `Difficulty.cs` holds `DifficultyLevel` (Easy / Normal / Hard / Nightmare /
Custom), one `KnobDef` per `DiffKnob` (name, description, unit, range, step, the four presets) and `Difficulty`
(values; `Set` makes it Custom; `Write`/`Read` for the lobby message, `ToText`/`Parse` for run saves). `Session.Diff`
(host: `SetDifficulty`, `SetDifficultyKnob`) is copied to `Game.Diff` at the start; the game reads it with
`D(DiffKnob.X)`. New Normal balance: enemy damage grows 3% a wave (`EnemyDamageScale`, applied once at the top of
`Damage`, so every enemy, boss and hazard hit scales), regen 0.25 HP/s (was 0.5), small orbs 12% (was 18%), revives 4
s (was 3), hit grace 0.25 s (was 0.35), adds get 60% of the bullet budget in boss fights (was 40%), solo enemies 85%
health (was 70%). Removed from `Balance.cs`: `PassiveRegen` (now `game.PassiveRegen`), `OrbChance`, `ReviveTime`,
`HitIframes`. UI: the lobby's 4th tab (`DifficultyUi.cs`: cards, sliders with a Normal tick, text boxes), a HUD chip,
a saved-run badge. Tests: `difficulty` (in the feature suite); tests that measure an ability's damage cut pass `100f /
g.EnemyDamageScale(g.Wave)` so the hit lands as 100. Preview: `DIFF=Custom LOBBYTAB=3 Tools/Preview/shot.sh <name>
lobby`. Session JSON has a `difficulty` field and `report.py` shows it.

**10 new menu backgrounds (Oct 2026), no protocol change:** `MenuBackgrounds.cs` now has 20: added Ember Storm,
Constellations, Radar, Blueprint, Bullet Curtain, Frost Peaks, Circuit, Event Horizon, Firefly Wood and Plotter
(`BgEmbers` ... `BgPlotter`, cases 10-19), each with an accent in `BackgroundAccents`. They were mocked up in a web
page first and ported shape for shape; the mockup's text labels (Blueprint's "432 px", Plotter's formulas) were left
out because backgrounds are GL only. Saved picks 0-9 are unchanged. The picker's swatches tighten their gap past 12
backgrounds.

**Fancy background picker (Oct 2026), no protocol change:** `MenuBackgrounds.cs`. Each background has an accent colour
(`BackgroundAccents`) used for the card's pulsing rim, the thumbnail rim and the name's glow. The full card has drawn
chevron buttons, a swatch bar (one swatch per background in its colour; hover names it), a LIVE / STILL switch in
place of the Animated checkbox and a fold-down button; the name slides in from the side it came from (`bgChangedAt`,
`bgChangeDir`). The minimized tab is a slim card with a tiny live thumbnail (it shrinks away as the tab fades), the
name, quick prev/next arrows and an expand button.

**Lobby header (Oct 2026), no protocol change:** the RUN / PRACTICE RANGE switch (and the saved-run banner) moved from
the top centre into the left column under the title, above the party; the tabs and loadout box moved up level with the
title (`LobbyBodyTop` 68, new `PartyTop` 184). "The host picks the mode" line removed.

**Bosses scale with build strength (Oct 2026):** `BuildPower.cs`. `BuildPower(Stats)` scores a build (damage x fire
rate x (1+0.4 Multishot)(1+0.3 Barrage) x crits x boss legendaries x companions); `TypicalPower(role, ability, wave)`
is the 75th percentile of 200 builds picked at random from the game's own offers (`TypicalLevel` = 1.35 x wave
level-ups, plus a legendary and a mod per earlier boss; cached); `BossPowerScale()` = (party's mean power /
typical)^0.75, x1 to x3, applied in `SpawnEnemyAt` to bosses (`Enemy.PowerScale`, recorded as `powerScale` in
sessions). Off with `Game.ScaleBosses = false` (Boss Lab, the quick-kill Queen test) and in the Practice Range. The
uploaded run's build: x1.07 at the Queen, x1.47 at the Reapers, x2.50 at the Singularity, x3 at the Colossus
(`bosspower` report; `bosspowertest`).

**From the OP Hemomancer session (Oct 2026):** Barrage's fanned copies deal `BarrageCopyDamage` (60%) via
`volleyShare` in `FirePlayer`/`FireVolley` (one pick: boss +28% -> +19%, pack +60% -> +43%). Session recordings:
bosses that leave without dying (the twins fusing, the Dual Reaper splitting) are closed by
`SessionRecorder.BossMerged` and written with `"merged": true`; `report.py` skips them (before, they stayed open and
collected every later hit and down). Checked in `reapers`.

**Run stats screen (Oct 2026), protocol 108:** Tab now shows `RunStatsUi.cs` (mockup A at
https://claude.ai/artifact/R283Z23R49V3k9KUVETFoA): tabs Run so far / Your build (the old `DrawTabMenu`) / Team. The
pause menu has a **Run stats** button (`runStatsOpen`) for a paused party. Solo, holding Tab pauses (`tabPaused` in
`ArenaApp.Update`; the pause menu stays hidden for it); in multiplayer it doesn't. New `Msg.RunStatsLive`: the host
sends every player's `RunStats` about once a second (`TickLiveStats` in `HostTick`) and on pausing (`SetPaused`);
`ReadRunStats(r, final: false)` leaves `RunStatsReady` alone. The route is built from `Game.Route` + `BossForWave`
(`BuildRoute`). Not tracked yet (so not shown): damage by source, path prizes, boss fight times. Test: `runstats` (in
the feature suite). Preview: `STATSTAB=0|1|2 [PAUSE=1] Tools/Preview/shot.sh <name> tab 14 12.2 3`. *Zoom-through (Nov
2026, mockup at https://claude.ai/artifact/DbvMWLr3LpEyi92MoArDrM):* `DrawRunStatsScreen` remembers
`statsShown`/`statsFrom`/`statsSwitchAt`; for `StatsZoomOut` (0.14 s) the old page scales up 6% and fades, and after
`StatsZoomDelay` the new one eases in from 94% over `StatsZoomIn` (0.2 s). `DrawStatsZoomed` scales `GUI.matrix` about
the page centre and fades with `GUI.color` + `Ui.Alpha`; `DrawStatsPage(tab, …, dim)` draws one page (the backdrop dim
is drawn once and blended between pages). GL ignores `GUI.matrix`, so `statsZooming` skips the Team page's live models
mid-switch. `fxOn` off: instant; `fxReduced`: fade only. Preview a mid-switch frame with `STATSFROM=n STATSAT=0.12`
(the preview stub ignores `GUI.matrix`, so it shows the fade only).

**Button style trial (Oct 2026), no protocol change:** six looks (A Sheen, B Slab, C Capsule, D Chamfer, E Glow, F
Split; mockups at https://claude.ai/artifact/F99igZttNtrVDWJnMGCqTi) were tried from a Settings dropdown; the owner
kept **A Sheen** (`Ui.FancyButton`), and the dropdown and the other styles were removed. The lobby has a cog by Leave
that opens Settings.

**Fancy menu buttons + Field guide button (Oct 2026), no protocol change:** `Ui.FancyButton(r, text, tint, icon, t)`
(icon chip, `Ui.Fancy` lettering, a lighter face and a shine on hover; no outline: the owner doesn't want outlines
around buttons) on the main menu's buttons (Host / Join / Cancel too), every Back, the saved runs' Continue / Delete,
Resume / Leave game, the Field guide, and the lobby's Leave / Ready / Start (Ready no longer has a pulse ring). New
icons `ui_back`, `ui_trophy`, `ui_gear`, `ui_book`, `ui_help`, `ui_power`, `ui_trash`. The pause menu's two Field
guide cards became one button (also in Settings; the guide works from the main menu too, solo at wave 1). Preview:
`shot.sh <name> settings`.

**In-game Field guide (Oct 2026), no protocol change:** the pause menu's Bestiary and Armory are now in-game databases
(`FieldGuide.cs`) instead of links to the web pages (still a button away). Bestiary text is generated from the page
(`node Docs/Bestiary/build_data.js` → `BestiaryData.Generated.cs`); stats come from `Defs`, health from the new
`Game.SpawnHp(type, wave)` (what `SpawnEnemyAt` uses) and fire timing from `Game.WaveFireRate`. The Armory reads
`Defs.Weapons`, `Upgrades.Abilities` and `Upgrades.All`. Preview: `Tools/Preview/shot.sh <name> guide` with
`GUIDE=0..4` (Bestiary, Classes, Weapons, Abilities, Upgrades), `GUIDESEL=<key>`, `GUIDESEARCH=`, `GUIDESCROLL=`.
Test: `run.sh 1 30 fieldguide` (also in the feature suite).

**The Twin Reapers' new legendaries (Oct 2026, protocol 104):** `Docs/ReaperLoot.md`; code `ReaperLoot.cs`, look
`Game/ReaperLootLook.cs`. **Twin Scythes** (`MinionKind.Scythe`, companions; `Minion.Ring` signed radius;
`TickTwinScythes`, `MoveScythe`: circle, cut via `scytheCuts` per (scythe, enemy) on `scytheClock`, reap out every
`ScytheSweepEvery` like a sent wisp; `CutSmallBullets` also feeds `pendingRemoved`). **Final Reap**
(`Stats.ReapBelow`/`ReapBossBonus`): `FinalReap` at the end of `HurtEnemy`, boss bonus before crits. **Scythe Throw**
(`AbilityId.ScytheThrow`, `ReaperAbilities`, `MinionKind.ThrownScythe`, `throwHits` per leg); the catch calls
`CutCooldown`, which sends the new `Msg.CooldownCut` so the thrower's own game shortens its cooldown. Executioner,
Twin Fang and Soul Harvest are `Unoffered`. Preview `REAPERLOOT=1`; test `reaperloot`.

**The Hive Queen's new legendaries (Oct 2026, protocol 103):** plan and numbers in `Docs/QueenLoot.md`; code in
`QueenLoot.cs` (look: `Game/QueenLootLook.cs`). **Hive Drones** (`MinionKind.HiveDrone`, companions so they stay
between waves and are dazed, not broken; `Stats.HiveDrones` 2 then +1 a stack): `TickHiveDrones` keeps them up,
`MoveDrone` circles / flies out / stings 3 x `DroneSting` / flies home and heals `HiveDroneHeal`; sent by
`DroneOnKill` (KillEffects) and `DroneOnBossHit` (HurtEnemy, every `DroneBossEvery`), `DronesPerSend` = stacks.
**Royal Dive** (`AbilityId.RoyalDive`, a bonus ability like the Stun Gun; `QueenAbilities`): moved like Leap Slam on
your own game (LocalControl), `StartRoyalDive`/`TickDives` land it (`RoyalDive*` consts; her own dive uses `Dive*`),
leaving `ZoneKind.HoneyPool` (heal 6/s, slow 40%). **Bomber Escort** (`MinionKind.HiveBomber`, `Stats.Bombers`):
`TickBombers` launches every `BomberEvery` at the toughest enemy within `BomberReach`, `MoveBomber` steers,
`ExplodeMinion` bursts (MinionDef Damage 55, Range 70). Swarm Rounds, Royal Jelly and Brood Mother stay but have
`Unoffered` (never rolled; `Available` checks it). Preview `QUEENLOOT=1`; test `queenloot`; `balance` report includes
Hive Drones and Bomber Escort.

**Upgrade balance follow-up + Magnum Rounds (Oct 2026, protocol 102):** Piercing +2 pierce a stack and
`PierceReachPerStack` 0.075 per point (15% a stack); Swarm Rounds bite the enemy hit when nothing else is within
`SwarmAloneReach` (`SwarmAloneShare`, in Collide's splits loop; `BounceTarget` takes a reach); Twin Fang 40%; Dark
Matter +35% damage; Incendiary 40%; Shock 25%; Velocity +25% damage. New Common card **Magnum Rounds** (MaxStacks 4):
`Stats.ShotSizeMult` +15% a stack, applied in `MakeBullet` (host and clients) and the Revolver/bow radius overrides,
and folded into `AreaMult`; icon `magnum` in PixelArt; stat sheet shows "Shot size" when it isn't 100%. Protocol
bumped because the upgrade list's indices moved. Numbers in `Docs/UpgradeBalance.md`; checks in `upgradefix`.

**Upgrade balance I (Oct 2026):** Frost Lance Multishot +25% damage per extra (`FrostLanceGrow`); Radiant Orb light
orbs circle the orb while it's out (`LightOrbRing`, `LightOrbSpeed`) and Barrage adds one (`CompanionCount`: extra +
Fan; `RadiantSize` is the orb's radius, used by the renderer); Wolf Whistle darts only move the prey to an enemy
nearer `Player.AimAt` (`WolfDartHit`). The `balance` report places enemies by each weapon's base range and lists
weapons where a card is worth under 5% in every fight. Checks in `upgradefix`.

**Upgrade balance A-H (Oct 2026):** see `Docs/UpgradeBalance.md`. Commons +10% (Sharpened, Quick Trigger), Hair
Trigger +20%; Ricochet 40% a bounce (`RicochetMult`); Dark Matter +1 pierce; Brood Mother 40% of kills, 6 s turrets
(`BroodTurretTime`), one turret every 8 s while hitting a boss (`BroodBossEvery`, `Player.BroodCd`/`BroodDue`, spawned
in `TickPlayers`); Graviton +15% damage to slowed enemies (`Stats.SlowedBonus`); Executioner below 50%
(`ExecuteBelow`); Collapse hits the enemy shot at half (`CollapseCore`); Homing costs 5% a stack (`HomingDamageCost`
0.025). The `balance` report now uses per-weapon enemy health, a stand-in boss flagged `IsBoss`, and forces
`MatchState.Fighting` (turrets only shoot then).

**Saved-run fixes from the code review (Oct 2026), no protocol change:** a newcomer whose saved seat the host is
sitting in, with no other seat free, is now turned away instead of removing the host from their own lobby
(`Session.cs`, Hello); saves and the profile are written with `SaveStore.SafeWrite` (temp file, then `File.Replace`),
so a crash can't leave only the `.tmp` (the old code deleted the file before moving the temp in). Two new `saves`
checks.

**Bug fixes from a code review (Oct 2026), no protocol change:** a sweeping Beam Sentry no longer has its `Strafe`
flipped by `MoveEnemy` (its beam could turn back halfway); `ResetVariety` (Variety.cs, from `WaveCleared` and `Stop`)
drops pending Volatile bursts, bounced Mirrored shots and squad lists, so a burst can't go off in the intermission or
the next run; `DotDamage` (burn, bleed, poison, vines) now respects the boss immunities (`BossImmune`, shared with
`HurtEnemy`, plus `SingImmune`) and goes through `SoakBubble`. Three new `variety` checks; the Beam Sentry check waits
for the client to see the sweep (it was flaky in `all`).

**Upgrade fixes, step 2b (Oct 2026):** Ricochet works on every weapon: hits that can't bounce (`!CanBounce`: stand-in
bullets for swings/beams/cones/bursts, shots with `Pierce >= 50`, and piercing shots marked `Bullet.Bolts` — Railgun,
drawn Mender's Bow arrows, Bone Spear) add to `Player.BoltStore`, and every `BoltEvery` (0.25 s, `Player.BoltCd`) a
bounce bolt (`BoltShot`) carries `RicochetMult` (75%) of it to `BounceTarget` within 300 px, bouncing on with
Ricochet - 1. Piercing Rounds give +10% reach a stack (`PierceReach`, which `MeleeReach` now uses) to the Flamethrower, Frost
Lance (it ignored Piercing before), Fuma Shuriken, Repulsion Cannon, Radiant Orb, Chain Light, Spirit Fan, Ofuda, Arc
Lightning, Wisp Lantern and Imp Caller (their `WeaponRange` too). Checks in `upgradefix`. `upgradecov` now shows
Ricochet at +164% median (3 stacks) — strong; step 3 should look at it.

**Upgrade fixes, step 2a (Oct 2026):** Explosive, Collapse and Swarm Rounds were silently dropped for every hit
outside the bullet loop (beams, cones, orbs, wisps, chains): `Collide` cleared `splashes`/`splits` at its start,
wiping what those hits had queued. They're now cleared after being spent. The Repulsion Cannon, Ofuda ward bursts and
Imp Caller blasts now call `OnBulletHit`, so all weapon mods ride them. `upgradecov` after: every elemental/impact mod
works on every weapon (the Golem Staff only looks dead there because its boulder lands by charge, not at the cursor).
Still open: Ricochet (29 weapons can't bounce) and Piercing (17 already pierce or don't fire bullets).

**Upgrade fixes, step 1 (Oct 2026):** Velocity Rounds no longer carry Flak Cannon shells or Ofuda charms past the
cursor (flight time now divides by the faster speed; max range still grows). Incendiary Rounds are a pool
(`Enemy.BurnPool`/`PoolT`, burned off evenly over `IncendiaryTime` = 3 s after the last hit): each hit adds 25% of its
damage per stack, so fast and slow weapons gain alike (+26% a stack on Riot Repeater and Revolver; it used to keep
only the biggest hit's burn, so rapid-fire weapons got ~0). The Flamethrower's own burn still uses `BurnT`/`BurnDps`.
`upgradefix` test (last in the feature suite, so earlier tests keep their seeds). Next: step 2, weapon-specific
effects for Ricochet, Explosive, Swarm, Collapse and Piercing on weapons they skip (see `upgradecov`), or filtering
dead cards out of offers.

**Homing Rounds on Wisp Lantern / Imp Caller (Oct 2026):** sent wisps and imps used to ignore the mod (no homing, no
damage cost). Now each stack widens the cursor pick 50% (`SendPickRange`), a companion with no live target locks onto
the nearest enemy within 260 px (`MoveSent`), and wisp hits / sent-imp blasts pay `HomingCost`; card text per weapon
in `Upgrades.DescFor`; `fivemore` test.

**Legion nerf (Oct 2026):** Legion's own wolves and bats no longer frenzy (`Minion.NoFrenzy`); your other minions
still do. It added ~11,200 damage to one big target in 12 s (Bullet Storm ~6,200), now ~5,400. Measured by the new
`ultdps` report (`run.sh 1 1 ultdps`). No protocol change (host-only flag).

**Dev text editor (Oct 2026):** F4 / F1 → Tools: click any UI text to edit it; edits in `TextOverrides.txt` (numbers
stay the game's), folded into the code by `Tools/TextEdits/fold.py`; `textedit` test mode.

**Blood Mage class (Oct 2026, protocol 61):** 6th kit for all three roles: health as a resource (Space abilities cost
health, never below 1 HP; Blood Price up to +40% damage at low health; 3% lifesteal), 9 weapons, 9 Space abilities, 3
ultimates, 9 ability upgrades + class cards, icons, look; `blood` test suite; preview `BLOOD=n`.

**This session (Oct 2026), newest first:** **More variety between the bosses** (`Variety.cs` + `VarietyLook.cs`, from
`Docs/Mockups/enemy-variety.html`): four new `EnemyType`s after `BallLightning` (Beam Sentry 18, Scorcher 24, Reaver
28, Warder 36; added to `BuildWave`'s pool). The sentry's Phase is walk / plant / aim / sweep / rest with the beam
along `Facing` (Face() leaves it alone); the Scorcher drops `PatchKind.Embers` (players only, burnt in `TickVariety`);
the Reaver's cone and +50% while open (`ReaverIsOpen` in `HurtEnemy`); the Warder sets `BubbleHp`/`WardId` on allies,
popped in `VarietyKilled`. Elite traits are `Enemy.Traits` (`EliteTrait` flags, rolled in `SpawnEnemyAt`, synced with
`BubbleFrac` and `WardId`); the flat x1.35 elite fire rate is now Hasted (`TraitFireMult`, which also applies
Commanders). Bubbles soak in `HurtEnemy` (`SoakBubble`); Mirrored bounces in `Collide` (`MirrorBounces`, fired next
tick); Volatile and Splitting in `OnEnemyKilled`. Squads: `Squads` table, `BuildSquads` spends about 70% of a regular
wave (melee squads as often as `MeleeShare`, none twice, 80% cap) into `squadQueue`, spawned after every three singles
(`SpawnNextSquad` in `TickWaves`, `Remaining` counts them); `SquadId`/`SquadSlot`, escort rules in `SquadMove`
(formation march, Snipers behind their Shielder or firing line, a Shielder before planted sentries, Medics behind,
Splitters round their Nest) and `LungerWaits`. Bots avoid beams, Reaver cones and embers. `variety` test (in
features); Bestiary entries with demos (`Docs/checks/bestiary-variety.js`); protocol 101 after merging with the
Devourer rework and saved runs (crescents moved to bullet kind 52: the Devourer took 18 and 19, the last enemy kinds
in 10-19, so `IsEnemyShot` also takes `CrescentShot`; `PatchKind.Embers` comes after `Bile`) · **The Eclipse is a
five-stage finale** (`Eclipse.cs`: `EclipseState` on the boss, synced by `SyncEclipse`; Corona / Penumbra / Umbra /
Echoes / Last Light at 80 / 60 / 40 / 20%, Totality below 5%, 2.5 s stage changes; built from the mockup in
`Docs/Mockups/eclipse-5-stage.html`). The moon (`MoonA`/`MoonD`) soaks players' shots in `Collide`
(`FindEclipseMoon`/`MoonStops`); `InMoonShadow` is the shared shadow test for the Eclipse flash and Shadow nova
(`FlashT`/`FlashLeft`), the bots and the drawing. Solar flares (`EclipseFlare`, lanes hurt once each), closing umbra
rings (`EclipseRing`, two gaps), sunspots (`Sunspot`) and the Umbra's dark drain (`HurtNoIframes`: the drain and the
burning edge keep the hit immunity timer as it was), crescents as a new enemy bullet kind `Defs.CrescentShot` (18)
flying a circle through you and back (`AngVel` only, so nothing new to sync), the echoes (Colossus-style gap rings of
big shots, Lich-style twin beams `SweepOn`/`SweepA`, Herald strike rain via `AddHazard`, a Singularity-style pull +
`DiskOrbAt` disk), Last Light's burning edge (`Inset`, each game keeps its own player in via `KeepInEclipse` in
`KeepInArena`), Totality (`EclipseExposed` +50% in `HurtEnemy`, `NovaT` 12 s, the nova hits 70% max health and heals
2%). `e.Phase` mirrors the stage. Look: `EclipseLook.cs` (shadow, sunspots, flares, rings, beams, disk, echo ghost,
moon, corona swell; `DrawEclipseSky` from `DrawArenaRules` draws the Umbra's dark over bullets and the burning edge).
Stage banners say STAGE n / 5 (`StageShift.Of`), HUD notes `EclipseNote`, boss cues, bots (shadow spot before a flash,
back into the light, flares, rings, beams, disk, edge). `BossHpMult` 2.7 → 5.5: Boss Lab Good bots, trio 181-223 s;
solo 56 s to 300 s+ depending on the build, no downs. `eclipse` test (in features); preview `ECLIPSE=<health>
ECLIPSETURN=<turn> ECLIPSET=<s>`; Bestiary entry with six stage locks and a live demo of each
(`Docs/checks/bestiary-eclipse.js`); protocol 99 · Saved runs and records (owner's choices: save between waves, the
host keeps the save and seats are taken back by name, several slots, records now and unlocks later). `SaveData.cs`:
`RunSave` (plain `key value` text, upgrades/abilities/mastery by name; `SeatSave` per player) and `SaveStore`
(`Folder`, `List`/`Write`/`Delete`, temp-file writes). `Profile.cs`: `Profile` (`Current`, `Folder`, `profile.txt`;
runs, bests, totals, `BossesBeaten`, `RunsAs`/`WinsAs` by `ClassKey`, `ModWins`, `Unlocked`) and the empty
`Unlocks.All` registry (`UnlockDef` with an `Earned` condition; `CheckUnlocks`). `RunSaves.cs` (Game): `SaveRun`
(host, only in a clean intermission) from `TickWaves` as a wave starts and from `Stop`; `RunOver` deletes the save and
sends `Msg.Record` End; `BossRecord` from the kill loop when `OnEnemyKilled` gives the boss reward; `ReadRecord`
updates each game's own `Profile` (skipped with `devTouched`, from `DevUsed`, which now wraps every `Recorder?.Dev`,
or `botTouched`); `ApplyResume` after `HostStart` restores seats (`ResumeSeats`) and sends a `Record` Resume with the
path and route; the Start message carries a resumed flag (no Head Start again). `Unrecorded` keeps Boss Lab fights
out; `ProfileOverride` gives tests a profile per game. Session: `Resume` (lobby message carries its seats and each
entry's `Seat`), `HostResume`, seats by name (the owner bumps a stranger to another free seat, or out),
`Msg.SeatRequest`/`RequestSeat`; loadout and modifier changes are ignored while continuing; `StartMatch` sets
`StartWave` = saved wave + 1. UI `SavesUi.cs`: main menu **Continue a run (n)** and **Records**, the Continue list
(`DrawSaveRow`), `DrawRecords`, the lobby's `DrawResumeBanner`/`DrawResumeSeats`, `DrawUnlockToasts`; the pause menu
says runs are saved. `saves` test (in features); previews `continue`, `records`, `lobby` with `RESUME=1`; protocol 100
· The Devourer is a three-stage fight (`Devourer.cs`: `DevourState` on the boss, synced by `SyncDevour`; Appetite /
Gorging / Ravenous / last meal at 65 / 30 / 10%, built from the mockup in `Docs/Mockups/devourer-3-stage.html`; it
grows 50 → 58 → 64 px, `DevourRadius`). Its hunger is still `Hungry(e)` (`e.Phase` 1, now its Hungry turn), which
spits `EnemyType.Morsel`s (60 HP, `ParentId` = the boss, no XP; it crawls to them and each eaten heals `MorselEat` 1%;
walking over one squashes it, `StepOnMorsels`). Lunges (Snap, Frenzy) move the boss in `TickLunge`; Burrow hides it
(`DevourUnder`: shots pass in `Collide`, no contact or shove, not drawn; `DevourImmune` in `HurtEnemy`/`BlocksShot`).
Inhale: `DevourPull` pulls your own player in `LocalControl` (the boss shove is off while `DevourSwallowing`);
touching the mouth sets `Player.SwallowT` (replicated): `PinSwallowed` holds you in it, `Damage` ignores everything
but the stomach (`stomach` flag), enemy bullets skip you, you can't fire, you aren't drawn; allies' hits do ×2
(`DevourHit`) and 300 spit you out (`SpitOut` via `MovePlayer`; the client clears `SwallowT` on the teleport). Bile is
`PatchKind.Bile` (hurt in `TickBile`, dropped locally on a stage change by `ClearBile`); Vomit fires `Defs.BileShot`
(18), Jaws `Defs.ToothShot` (19). The Maw: `DevourBlocks` (in `BlocksShot`) notes which side a hit came from
(`MawHit`), `DevourHit` heals from the front, ×1.25 behind; it turns 60°/s, 35°/s solo; `Face()` skips the Devourer
(its `Facing` is its mouth). Last meal: `PullStrength` 55 and ten teeth (`ToothPos`). Look: `DevourerLook.cs`; HUD
notes `DevourNote`, banners GORGING / RAVENOUS, cues; bots avoid lunge lines, the cone, the eruption, teeth and bile,
shoot morsels first and hold fire on a hungry Devourer or into the Maw. `devourer` test (in features); preview
`DEVOUR=<health> DEVOURTURN=<turn> DEVOURT=<s>`. `BossHpMult` stays 2: Boss Lab Good bots solo 88 s (DPS) to timeouts
like the Herald's for solo Tank/Healer, duo 83 s, trio 113 s; protocol 99 · The Storm Herald is a three-stage fight
(`Herald.cs`: `HeraldState` on the boss, synced by `SyncHerald`; Gathering Storm / Gale / Supercell / last stand at 65
/ 30 / 10%, built from the mockup in `Docs/Mockups/storm-herald-3-stage.html`). Strikes, downbursts, the storm front
and strike rain reuse `Hazard`; lightning rods and their arcs live in the state (`HeraldRod`/`HeraldArc`, hurt as an
arc fires). The Gale's wind is derived on each game from the synced `Wind` (`HeraldWind`): `LocalControl` pushes your
own player, `MoveBullets` drifts every bullet. Ball lightning is a new `EnemyType.BallLightning` (moved by the host in
`TickHerald`, arcs to its neighbours, pops after 6 s). The Supercell's Conductor (`CondId`/`CondT`) marks players in
turn; the charge only hurts its carrier when it jumps to an ally. Last stand: four spokes from `Spin`. Look:
`HeraldLook.cs` (rain, wind, dark sky, rods, arcs, Conductor ring, spokes, storm cloud), stage-change banners GALE /
SUPERCELL, HUD notes, boss cues; bots dodge arcs, spokes and the Conductor and allow for the wind. `herald` test (in
features); preview `HERALD=<health> HERALDTURN=<turn> HERALDT=<s>`; Bestiary entry with stage locks
(`Docs/checks/bestiary-herald.js`). `BossHpMult` 4 → 3.5: Boss Lab 61-202 s solo, 98-137 s trio (the old fight: 56-213
s solo); protocol 98 · The Duelist in three stages (owner-approved mockup `Docs/Mockups/duelist-3-stage.html`):
`Duelist.cs` (`DuelState` on the boss, synced by `SyncDuel`: stage, turn, dash line, salute/stagger timers, the
circle, hanging blades) + `DuelistLook.cs`; masquerade decoys are `EnemyType.Decoy` with `ParentId` = the Duelist (1
HP, `DecoyShatter` on death) firing `Defs.HollowShot` (kind 17, harmless, skipped in `Collide`); the guard is in
`BlocksShot` and bounces bullets via `DuelGuardBounce` (queued, fired next tick); a perfect parry during the salute's
strike sets `StaggerT` (`DuelOpen`: x1.5 in `HurtEnemy`); the circle clamps players in `KeepInArena`
(`KeepInDuelRing`); stage show in `StageShiftLook.cs`, boss-bar notes `DuelNote`; `duelist` sim mode; preview
`DUEL=<health> DUELTURN=<turn>`; protocol 97 · The Lich is a three-stage fight (`Lich.cs` + `LichLook.cs`, from the
owner-approved mockup `Docs/Mockups/lich-3-stage.html`): `LichState` on the boss (synced by `SyncLich`; stage, turn,
window, sweep, blink, prisons, spirits), thresholds 65 / 30 / 10%, 2 s stage changes (`StartLichTransition`, banner in
StageShiftLook, notes in Hud). Six graves (`GravePos`). The Crypt: one phylactery at the grave farthest from players;
all phylacteries broken opens `LichWindow` 12 s, then they rise again (`RaisePhylacteries`); turns Fans / Rings /
Raise. Soul Harvest: three phylacteries (450 HP) joined by soul beams that hurt (`OnSegment`); Sweep
(`SweepOn`/`SweepA`), Prison (`BonePrison`, spikes fire inward), Spirits (`LichSpirit`). Undeath: no phylacteries; Hop
(fade 0.5 s, can't be hurt, ring of 20), Hands (`Hazard.Kind` = `HazardHand`, `HandBursts`), Storm. Last stand: the
Rite. `LichImmune` (phases `LichGuarded`/`LichFaded`/`LichShift`) in `BlocksShot` and `HurtEnemy`; the old T2 grunt
timer is gone; `lich` test (in features); preview `LICH=<health>` `LICHTURN=<turn>`; Bestiary text updated (its demo
still plays the fans and rings); protocol 96 ·**The Colossus becomes a three-stage fight** (protocol 95; from the
mockup `Docs/Mockups/colossus-3-stage.html`, with the stomp changed to rings of the Warden's push blocks): Awakening
(Stomps, Boulders, Fissures), Crumbling (four plates of two-way cover; Charge, which breaks a plate and dazes it for
+50% damage; Rockslide; Aftershocks), Molten Core (lava; Lava stomps, Meteors, Cave-in), kneeling last stand. Bots
read rings, landings, fissures, the charge lane and lava. `colossus` test suite (in features). Boss Lab, Good bots: 93
s solo, 118 s duo, 100 s trio, `BossHpMult` 3 → 2.3. Preview flags `COL=<health>` / `COLTURN=<turn>`. The Bestiary's
text is updated; its live preview still plays the old stomp. Three Healer abilities reworked (`wards` test, in
features): **Spirit Ward** no longer heals or speeds revives; it takes allies' damage (`WardSoak` in `Damage`, before
shields; `Zone.Soak`/`SoakMax`/`Soaked`, `WardSoakMax` 150 × potency; replicated as `GuardFrac`, drawn as a rim),
breaks when spent, and on breaking or ending heals allies in it for `WardRepay` 25% of what it took (`WardEnds` from
`TickZones`). **Healing Mist** no longer heals in place: `TickMists` sheds `Orb.Dew` drops (synced) every `DewEvery`
0.5 s within it, each healing `DewHeal` 10 × the zone's `HealMult` (Morning Dew) and lasting `DewLife` 6 s; they're
picked up like health orbs. **Arcane Ward** shields remember what they soak
(`Player.ArcaneOn`/`ArcaneStore`/`ArcaneBy`) and pay back `ArcaneRepay` 50% as healing when they break or run out
(`ArcanePayback`; recasting pays the old one out first); protocol 92 ·Healers alone: the aura heals the Healer
`AuraSelfHeal` 1 HP/s x `AuraHealMult` (replacing the flat 3 HP/s); a Healer's overhealing becomes a shield
(`OverhealShield` in `Heal`, cap 20%, 4 s, aura ticks excluded via `AuraSource`); Martyr with nobody down sets
`Player.WardT`/`WardFrac` (replicated, red ring) and `Damage` puts a warded player back up; a live Phoenix holds off
`CheckGameOver`; Haven and Spirit Ward heal x1.5 solo (`SoloZoneHeal`); `healersolo` test; protocol 93 · The
Singularity is a three-stage fight (`Singularity.cs`: `SingState` on the boss, synced by `SyncSing`; Event Horizon /
Accretion / Spaghettify / last stand at 65 / 30 / 10%, built from the mockup in
`Docs/Mockups/singularity-3-stage.html`). The accretion disk is computed, not bullets (`DiskOrb` from `DiskT` and the
`DiskGone` mask); the event horizon is its body (`SingRadius`); `SingShard` enemies pass their damage to the core;
`Msg.ReverseEnemyBullets` turns bullets round for the white hole; gravity wells spawn fresh each stage and don't
respawn within one (protocol 92). Look: `SingularityLook.cs`. Preview: `SING=<health> SINGTURN=<turn>`. Boss Lab:
83-95 s solo to trio at 3x. The last four untouched weapons reworked (`lastfour` test, in features): **Clot Bolt**
pools congeal after `PoolCongeal` 1 s (`Zone.Congealed`, `CongealPool` in BloodMage.cs from `TickZones`): enemies
inside are stuck `PoolStuck` 1 s (not bosses), and the owner takes `PoolGuard` 15% less inside (`Damage`); drawn
crusted (BloodMageLook). **Transfusion Darts** give a hurt ally `TransfuseHeal` 20 for `TransfuseCost` 4, and each
enemy hit steals `TransfuseSteal` 0.5 (`BloodShotHit`). **Rapier** thrusts skewer up to `RapierSkewerMax` 3 enemy
bullets (any but red) within `RapierSkewerWidth` of the line (`CutBullets` gained a `max`), each +1 `RapierStreak`.
**Katana**: the every-4th lunge is gone; `Player.SheathT` (replicated) builds while not firing (`TickNinjas`), and at
`KatanaSheath` 0.6 s the next cut is a quick-draw (`Swing.DrawCut`: a 220 px box, 22 px half-width, 2.5×), drawn as a
scabbard with a running glint; `WeaponRange` follows it; protocol 91 · Bosses get a per-boss health multiplier
(`BossHpMult` in Balance.cs, 1.2x Twin Reapers to 4x Warden / Storm Herald) tuned with Boss Lab runs (Good bots,
solo/duo/trio, waves 5-50) so fights average about 50-130 s; enemy bullets hit 15% harder (`Defs.EnemyBulletMult`).
Boss-killing tests got longer limits.Armory caught up with the second batch of weapon reworks (Scattergun, Flak
Cannon, Riot Repeater, Kusarigama, Bo Staff, Blood Lash, Prism Beam, Sprite Staff, Treant Rod, Totem Staff,
Lightbringer, Censer, Medicine Darts, Spirit Fan, Ofuda, Twin Rifle, Railgun, Burst SMG, Arcane Missiles, Wolf
Whistle, Wisp Lantern, Imp Caller, Twin Blades, Greatsword, Shuriken, Kunai, Blood Bolts, Bone Spear): the range
engine gained per-weapon hooks (`setup`, `tick`, `under`, `paint`), bullet options
`mult`/`on`/`heal`/`onAlly`/`land`/`end`/`burst`/`look`, swing `mult`/`on`, `s.cursor()`/`s.dash()`/`s.ray()`,
`shove()`, a teammate on the range (`ally: [x, y]`) for heal floats, a rooted Treant (`root`), sent wisps/imps, wolf
prey takedowns and enemy pellets (`foeShots`) for the Flak and Greatsword to stop; Incense and Ofuda Ward added to the
zones table; check `Docs/checks/armory-reworks2.js` · Armory brought up to date with the weapon reworks (Frost Lance,
Storm Hammer, Golem Staff, Bone Guard, Fuma Shuriken, Chain Light, Sanguine Orb, Fireball, Arc Lightning): new
range-demo options (`cones`, `strip`, hold-to-charge `release` with a Let go at control, `fuma`, `seek` + `coat`,
`shock`, `dart` cages, icicle swings); check `Docs/checks/armory-reworks.js`. How to play (`Menus.cs`) is wider (1200
px) with 14 controls (adds the end-of-run R / L / C and F1) and 14 tips in two columns (adds perfect parry, roles,
health orbs, mastery, paths, boss bodies, Practice Range), each with a glyph in `DrawTipGlyph`. The Warden's Lockdown
gates are 2 rows of push bullets deep, not 5 (`GateRows`, protocol 78). Radiant Orb (Elementalist Healer) is held, not
fired (`RadiantOrb.cs`): hold fire and `TickRadiantOrb` casts an orb from you that chases your cursor (750 px/s, at
most `Reach` 520 px out) and every 0.2 s hurts each enemy it touches (`Damage` 8, weapon mods via a pseudo bullet) and
heals each ally (4); let go, parry or go down and it fades; `Player.RadiantOn`/`RadiantPos` ride the snapshot,
`RadiantDisp` + `StepRadiantDisplay` draw your own without lag, `DrawRadiantOrbs`; extras still add Light Orbs;
`radiant` sim mode; preview `RADIANT=1`; Armory `s.orb`; protocol 76 · Six new run modifiers (`Modifiers.cs`, flags
64-2048): Head Start (level 3, two picks and a legendary queued in `Game.Start`), Practiced (`GainMastery` x2),
Bloodlust (Blood Moon's kill heal, no orbs, no regen), Gold Rush (`GrantGold` on every kill, `OpenShop` from
`WaveCleared` before each boss, `CloseShop` keeps gold), Tiny Arena (`TinyInset`/`TinyTarget` in `Paths.cs`, folded
into `ArenaMin`/`Walled`; walls drawn with `DrawNarrowWalls`), Nightfall (`NightF`, `DrawNight` in `PathsUi.cs`: a
darkness grid with a circle and an aim cone per player); the lobby's Modifiers tab is 3 columns now; `runmods` test;
preview `MODS=<flags>`; protocol 75 · Soul Link cast with no other player makes a **Soul Ghost** (`SoulGhost.cs`,
`MinionKind.SoulGhost`): linked like an ally (half damage via `GhostDamage`, the link's regen via `HealSoulGhost`, the
mastered link's 1 s `Minion.InvulnT`), half its healer's max health (the guards' `HasHealth` minions in `Stun.cs`),
stands beside you (`FollowOwner`) firing `Defs.HealerShot` light bolts, lasts the link; drawn by `DrawSoulGhost`,
tether in `DrawPlayerLinks`; the smart bot uses Soul Link alone when a target's near; `soulghost` sim mode; preview
`SOULGHOST=<seconds>`; protocol 74 · Session recordings save on quit: `OnApplicationQuit` → `Game.Stop("quit")`, a new
match over a running one ends it as "restarted", and `SessionRecorder.Tick` autosaves every 10 s (`AutosaveEvery`;
`session` test checks the mid-run file) · Knight Tank weapons reworked: the **Warhammer** is a charge weapon
(`Game.ChargeWeapon` shares the Mender's Bow draw/release; `BowCharge` sets damage 25-100% and stun 0.25-0.9 s; a 100
px wide box slam, `HammerHalfWidth`; drawn raised back with a charge ring and slam-strip preview, `DrawHammerCharge`),
the **Halberd** sweeps a thin 18° cone across 80° in six steps (`Swing.Sweep` groups + `Enemy.SweepMark`: each enemy
hit once a sweep; an extra adds a return sweep); `tankknight` test · The Knight Healer's Holy Mace is now the
**Quarterstaff** (`WeaponId.HolyMace` kept; 150° sweeps with knockback, `Swing.Deflect` → `DeflectBullets` bats enemy
bullets back as `HolyShot`s; heals on hit like the mace; `quarterstaff` test) · The Soldier Healer's Light Bolt is now
the **Repulsion Cannon** (`WeaponId.LightBolt` kept; `Repulsion.cs`: a cone blast that shoves enemies and knocks loose
`Orb.Tiny` health orbs, `FxKind.Repulse`; `repulsion` test; preview `REPULSE=0..1`); protocol 73 · Flame Burst
(Elementalist Tank) is now the **Flamethrower** (`WeaponId.Flamethrower`): a stream of `Defs.FlameShot` flames (2 per
0.06 s, ±11°, 3 damage to the first enemy then a third, `FlamePierced`/`Bullet.Pierced`, through everything; drawn
growing and reddening with age via `Bullet.Life0`) that set enemies burning (`Bullet.Ignite`, `FlameBurn` 5/s for 3 s,
applied in `OnBulletHit`); `flame` sim mode (in the feature suite); preview `FLAME=<seconds>`; Armory bullet option
`ignite`; protocol 72 · The Twin Reapers in three stages (owner-approved mockup): 2600 HP each; Twilight's tether only
burns on its own sweep, which follows its target; they fuse into the Dual Reaper (lasers, pillars, holes, chase, trap)
and split for Sundered (reaps, twin spiral, crossfire); no revive while fused; stage changes added to
`StageShiftLook.cs`; `reapers` sim mode (also in features); preview `REAPERS=<health>`; protocol 71 · Ringburst
replaced by **Searchlight** (the Warden's third legendary, `Stun.cs`): `Stats.Searchlight` stacks; `TickSearchlights`
chases `Player.LightPos` (replicated) toward `AimAt` at 1100 px/s and locks an enemy held in it for 2 s (`Enemy.LitT`,
stun 1 s, bosses slowed, 3 s rest); `LitMult` in `HurtEnemy` (+25%, +40% at 3 stacks, any player's hit);
`StepLightDisplay` (each screen chases its own cursor for its own light), `DrawPlayerSearchlights` (`WardenLook.cs`);
in `wardenloot`; preview `WARDENLOOT=1` shows it; protocol 70 · Leech Beam is now **Leech Tendrils**
(`WeaponId.LeechBeam`, Hemomancer Tank): hold fire and tendrils grasp up to 3 enemies in a 70° arc (`BloodMage.cs`:
`pendingGrasps`/`ResolveGrasps` from `Collide`, `Player.Grasp` holds the enemy ids and rides the snapshot,
`Enemy.GraspT` stuns like Glacial Tomb in `EnemyAI`, not bosses; `TickBloodMages` lets go when you stop firing); 12/s
per held enemy, 25% heals you; drawn by `DrawLeechTendrils`; `LeechGraspTest` in the `blood` and feature suites;
preview `LEECH=<held>`; Armory `s.grasp`, `Docs/checks/armory-leech.js`; protocol 69 · The Warden's legendaries:
Warden's Halo and Iron Volley replaced by **Warden's Guard** and **Stun Gun** (`Stun.cs`). Warden's Guard:
`Stats.Guards` companions drawn like the riot's `EnemyType.Guard` with a ring in the owner's colour,
(`MinionKind.WardenGuard`, `MinionMove.Escort` → `MoveGuard`: fights enemies within 240 px of its owner, else walks at
their side), the first minion with health (`Minion.Hp`/`MaxHp`, 150 + 15/wave; enemy bullets that touch it,
`GuardStopsBullet` in `Collide`, and enemy bodies, `GuardContact` in the enemy tick, hurt it; its health rides the
snapshot as `GuardFrac`), respawning `GuardRespawn` (15 s) after it falls (`Player.GuardCd`); drawn by
`DrawWardenGuard` (`WardenLook.cs`). Stun Gun: a legendary that `Grants` `AbilityId.StunGun` (Q/E/Shift); casting sets
`Player.StunShots` = 6 (9 mastered, replicated), `TickStunGun` fires `Defs.StunBolt` (the Warden's stun-shot look)
instead of the weapon, `StunBoltHit` stuns (FrozenT 1.5 s; bosses slowed), and `TickSlots` holds the slot's cooldown
until the last shot (`StunHeld`); HUD slot shows shots left, `DrawHeldStunGun`; `WeaponRange` follows it. `wardenloot`
sim mode (in the feature suite); preview `WARDENLOOT=1`; Armory demo type `stungun`; protocol 68 · Shielders' shields
wear down (`WearShield`: 60% of their health, worn by blocked shots and swings and 3%/s on their own; `Phase` 1-3 are
cracks, 4 = shattered, drawn by `DrawShielderShield`; `shielder` test) · The riot: 9 rioters a door; any left at 10 s
bring `EnemyType.Guard` (`CallGuards`, `GuardsBeatRioters`, `GuardMove`), who beat them and then go for players ·
Stage changes for the Warden and the Queen get a show (`StageShiftLook.cs`: sirens / wax spiral, closing rings,
shockwaves, a banner; cleared bullets pop; a burst as the stage starts) · The Warden's searchlight hunts 18 s;
protocol 67 · The Hive Queen is a 3-stage hive (owner-approved mockup): `Queen.cs` + `QueenLook.cs` (wax floor,
sealing cells, dive shadow, spray aim, bees, eggs, honey globs and puddles) + stage notes on the boss bar; her old
overlapping loop is gone (drones now get their own turn); `queen` sim mode (also in the feature suite), old Hive Queen
checks moved to the new loop; preview `QUEEN=<health fraction>`; protocol 66 · Classes renamed on screen: Mage →
**Elementalist**, Blood Mage → **Hemomancer** (`Defs.KitName`; enum names, files and methods unchanged) · Hemo Spray
replaced by **Implosion** (Hemomancer DPS, `WeaponId.Implosion`, `Defs.ImplodeShot`): click an enemy within 480 px (26
px slack) and it collapses (34), enemies within 55 px take 15 and are pulled 18 px in; extras/Barrage chain to another
enemy within 180 px (60%); no target = no shot and no cooldown spent; the cursor point now rides the input message
(`Player.AimAt`); target marker `DrawImplodeMark`, effect `DrawImplodeFx` (`BloodMageLook.cs`); preview
`IMPLODE=<0..1>`; Armory `s.implode`; protocol 65 · The Warden is a 3-stage prison riot (owner-approved mockup):
`Warden.cs` (logic) + `WardenLook.cs` (corner watchtower, searchlight, gates drawn as chevroned push bullets, riot
doors, shield, charge line, stun sparks, rioters) + gas look in `PathsUi.cs` + stage note on the boss bar; mechanics
take turns except the Lockdown's cross + gates + gas; `Player.StunT` and the `WardenState` ride the snapshot; new
`EnemyType.Rioter`, `Defs.StunShot`, `PatchKind.Gas`; `warden` sim mode (also in the feature suite); preview
`WARDEN=<health fraction>` holds it in a stage; protocol 64 · Vein Tether is a real beam (`BloodMage.cs`:
`pendingBeams`/`ResolveBeams` from `Collide`, `BeamEnd` raycast shared with drawing; `Player.FiringSeen` synced so
every screen draws everyone's beam, `DrawVeinBeams`; Armory `s.beam`, `Docs/checks/armory-vein.js`); protocol 63 ·
Bear Totem (summoner Tank) is now melee: `FireBearClaws` queues two quick claw swipes (110°, 85 px, 11 damage, 0.12 s
apart, raking opposite ways; `Swing.ClawSide`, drawn by `FxKind.Claw`) that don't cut bullets, and each hit bleeds
(`Swing.Bleed`, `ApplyBleed`/`TickBleed`: 30% of the hit per second for 3 s, stacking to five hits; replicated status
bit 16, drops and a blood-drop icon); `DotDamage` shared with burning; `bear` sim mode; protocol 62 · H hides/shows
the HUD (`hudShown`/`hudT`, `HudGroup` staggers four pieces with slide + scale + ease-out-back + fade;
`Ui.Alpha`/`Ui.Faded` make GUI.color-setting draws fade too; preview flag `HUDT=0..1`) · The Bazaar (a 10th path, 1
skull): kills on its waves give every player the same gold (`GoldPerXp` 0.3), and its prize is a shop before the boss
(`Shop.cs`, `ShopUi.cs`): per-player shelf of 4 upgrades + a legendary + a weapon mod priced by rarity, services
(restock, remove an upgrade, full heal, revive token), giving gold (reopens a done player's shop), no timer (closes
when everyone is Done; the host can close it after 60 s), leftover gold becomes each player's own XP; bots shop by
themselves; gold shows as a chip above the wave panel (`DrawGoldChip`; preview `GOLD=n`); preview mode `shop`
(`SHOPSEL`, `GIVE=1`, `REMOVE=1`) and `PATHOFFER=bazaar`; protocol 60 · HUD moved to the bottom (owner picked mockup C
of three): wave panel and your card flank the ability bar, teammates as slim rows, boss bars wide at the top · Session
recorder check (format 2): mode (run / practice), endless runs record their real end (they used to stay "victory" at
wave 50), path offers/choices/prizes, healing received by source, floor damage to enemies, dev tool use, each player's
final abilities and mastery; `report.py` shows paths, healing sources, dev-tool flags and leaves Practice Range
sessions out (`--practice` to include) · Divergent paths, all 4 steps (protocols 57-59): framework and the
Choose-your-path screen, Swarm Fields / Sniper Ridge / Elite Gauntlet, Burning Grounds and Sanctuary (hazard patches,
bots avoid fire and seek pools), the "no path" option, Frozen Lake (momentum movement in `LocalControl`: `iceVel`,
`Paths.IceGrip`) and Blood Moon (no orbs/regen, kills heal), Fogbound (client-side mist) and Narrow Pass (walls,
playable rect; bullets spark and stop at the walls); `paths` sim mode; the Sanctuary count test now counts spawn rolls
(swarmer pairs made it flaky) · `Docs/Paths.md` design with the owner's decisions · Run modifiers: lobby Modifiers
tab, six switches · Lobby: no RUN / PRACTICE label by Ready; background picker has a minimize button (fades after a
few seconds, hover to bring back) · Stat previews use a pixel arrow icon (`ui_arrow`) instead of "->" · Frost Lance
flies as an ice spear with a wider (20 px) spearhead hitbox (`DrawFrostLance`) · UI buttons restyled as icon cards
(style "F") · F1 dev menu tabbed and card-based; Field guide (Bestiary, Armory) in the pause menu · Docs: Armory build
script (icons from `PixelArt.cs`) and Playwright page checks · Audit: dead code removed, Game.cs and ArenaApp.cs
split, `Tools/RenderCheck`, `Tools/Preview`, `Tools/CompileCheck`, this HANDOFF.

Earlier: Parry gauge on the shield: a thin blue bar around the rim drains toward your aim (`DrawParryGauge`; faint
while lowered and refilling, your own only), the HUD parry tile is gone, and a break plays `FxKind.Shatter` (cracks,
then jagged pieces and sparks, seeded by position so everyone sees the same burst); protocol 56 · Ninja class (5th
kit) for all three roles: 9 weapons, 9 Space abilities, 3 ultimates, 9 ability upgrades + class cards, icons, look;
protocol 52 · YOUR STATS is a see-through sidebar down the whole left edge in the Tab menu and the upgrade picker
(`StatsSidebar`) · Class ability cards: very rare cards for your class's two other Space abilities, on Q/E/Shift
(`UpgradeDef.ClassAbility`, `Weight` 3; generated in `Upgrades.WithClassAbilities`; ability upgrades now need the
ability on any key, `Upgrades.HasAbility`); protocol 51 · Practice Range editor redesigned (numbered wave list, level
stepper, tiles sized to fit, hover info bar with the enemy drawn, tidy footer; `Ui.SmallButton` for tight rows) · Main
menu title is now "Cartesian Colosseum" (title only: the product name, window title and docs keep "Top-Down Arena" so
saved settings stay) on an animated x-axis; How to play redone with key caps and illustrated tips · Aim line runs to
the crosshair, capped at weapon range · lobby background picker moved bottom left as a card with a live thumbnail ·
Lobby loadout preview is a live mini-arena (real game offline, target dummies, ability demo on hover/pick) · Lobby
bottom bar: ready notes only with other players (no "Click Ready" prompt) · Your lobby background also shows dimmed
behind the arena; lobby's "Pick Practice Range" hint removed · Lobby: hosting/port line and invite addresses moved
into a popup behind a "!" button by the title · Lobby backgrounds (10, per player, still option) · lobby redesign +
Practice Range moved into the lobby, shared and co-editable · Tab menu during upgrade picks; mastery XP line on
ability tiles · aim line ends at weapon range (`Game.WeaponRange`, tested against real shots) · Lance: box hitbox
180×88 that slides out, 1 s stun · Practice Range (first as an F1 tool) · Shield Wall blocks red shots · boss contact
shove · Lance replaced the Knight Tank's Sword & Shield · Knight class · Mender's Bow arrows · Singularity rework ·
Boss Lab with human-like bots · bullet budget and homing changes · ability mastery.

Open balance notes from the last session analysis (Knight Tank Lance run, before the Lance box change):
waves 1-4 deal almost no damage (46 total vs 276 HP) and the wave-5 Warden is a sudden wall (406 damage);
Shielders are the Knight's worst enemy (43 s average lifetime, their shields block swings); Mortars, Shielders
and Snipers do the most damage per spawn; regular enemies' bodies can still hit you every frame you stand
on them (only bosses got the once-a-second rule). The owner hasn't yet picked which of these to act on.
