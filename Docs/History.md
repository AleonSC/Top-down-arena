# Change history (newest first)

Moved out of `HANDOFF.md` so sessions don't read it by default. `git log` has the same story;
look here only when you need why something was built the way it was.

## Done to-do entries (moved from `AfterReset.md`, Nov 2026)

Every entry the owner ticked as built and checked in Unity, word for word, grouped by the to-do list's old sections.

### Bugs / polish
- [x] **Code cleanup: redundant code removed, shared helpers (Nov 2026, protocol 233; replays format 4; check in Unity).** Removed the retired Soul Link / Soul Ghost and ~30 never-called methods; `Targets.cs` holds the shared targeting helpers; the compile check now uses `Tools/Preview/Stubs.cs` only. Nothing should look, sound or play differently (render, sound and both suites checked); old replays no longer open. Details in HANDOFF.md §6.1.
- [x] **Lag chart (built, Nov 2026, protocol 231; check in Unity).** F5 in a match cycles off / pill / chart (top right); Shift+F5 freezes, Ctrl+F5 copies a summary; Settings → Gameplay has the same choice and **Log lag to a file** (a CSV a run in `<profile>/lag/`). Shows ping (now 4 pings a second), snapshot gaps and stalls, frame time / FPS / 1% low, KB/s in and out, every player's ping (`Msg.NetPings`), and wave / boss / bullet-storm ticks. [`LagChart.md`](LagChart.md). Check in Unity: the GL lines inside the chart (only seen in the preview tool so far), that the clipboard copy works, the CSV turns up in the profile folder, the chart against a boss bar or the Run stats screen, and a real guest over Tailscale (does it catch the lag your friend feels?). Camera lean and Scroll-wheel zoom now share a row in Settings → Gameplay to make room.
- [x] **Katana: two stances (built, Nov 2026, protocol 230; check in Unity).** From [`Mockups/katana-stances.html`](Mockups/katana-stances.html) (https://claude.ai/artifact/CV8cs8PEVMuhjqMcDxGX3h) with the owner's changes; design [`KatanaCombo.md`](KatanaCombo.md). Replaces swipe, swipe, lunge and the strike meter. The weapon-action key switches stance (1 s cooldown, 0.22 s without attacks, pushes enemies within 110 px back 45 px; nothing else). **Hayate**: dash-cut 150 px along your aim (28, untouchable 0.1 s), 0.55 s cooldown or none on a kill; Flow (a level per killing dash, +8% length and +4 a level, fades only in Hayate); every enemy hit fills the Kage meter (10). **Senbon**: swipes (16, 120°, 120 px) that mark what they don't kill (up to 3) and fill the Zanshin meter (10). **Hold fire 0.35 s with the meter full**: Kage Chain (6 nearest, 70 each, 75 ms a hop) or Zanshin (the marked freeze; hop through each and back, sheathe; 34 a mark, enemy shots within 380 px cut). **+1**: every dash and swipe strikes once more at 60%, one more Kage hop. Both meters, marks and Flow are kept on a switch. New replicated state: `Player.KatanaStance/Flow/Kage/Zan/Step/Dashes`, `Enemy.KatanaMarks`; `FxKind.KatanaStanceFx`, `KatanaHop`; `Sfx.KatanaStanceSwap`. Look (`KatanaLook.cs`): held pose per stance, the stance's meter arc (the other a faint inner arc), the hold arc, Flow diamonds, rose marks, the switch's burst and blade sweep, the hops' streaks; your own Zanshin dims the arena and letterboxes. Test: `run.sh 1 30 lastfour`; preview: `NINJA=1 SWING=0.08 SWINGW=Katana KATANA=senbon,2,4,10 KATANAMOVE=dash|swipe|switch|hop Tools/Preview/shot.sh x hud`. Check in Unity: the dash's feel (your own game moves you when the host's count changes, so a guest dashes a round trip late), the held pose per stance, the hops on a guest's screen, the meters' readability, Reduced effects. The Armory page and `WeaponDps.Generated.cs` are out of date for the Katana.
- [x] **Perfect parry: 0.30 s, a needle and a flash for everyone (built, Nov 2026, protocol 230; check in Unity).** From [`Mockups/parry-perfect-rework.html`](Mockups/parry-perfect-rework.html) (https://claude.ai/artifact/GKDpX5pcNRmnEPyQc2FDY4) with the owner's choices: `PerfectParry` 0.22 > 0.30 s (Riposte 0.42); shots go straight back (as before) as a fast white needle (`Defs.ParryNeedle`, 900 px/s, same damage and reach; Mirror Guard and Aeroaegis keep the arcane bolt); `FxKind.PerfectParry` in the parrier's colour (the shield's arc flashes white, ring and sparks) with `Sfx.PerfectParry` (`ParrySoundBank.cs`); your own picture holds 50 ms; and the world slows (enemies and every shot, not players) to 30% for 0.3 s on every screen (`WorldSlowT`, set by the host and from the effect on guests), at most once every 0.6 s. Test: `run.sh 1 30 parry`; preview: `NINJA=1 PERFECTPARRY=0.05 Tools/Preview/shot.sh x hud`. Check in Unity: the slow-down in co-op (a guest's own shots slow a moment after the host's), the flash on every class, whether 0.3 s is too long against bosses.
- [x] **Poison Dart replaces the Soldier DPS's Dash (built, Nov 2026, protocol 228; check in Unity).** Look A "Toxic Drip" of [`Mockups/poison-dart.html`](Mockups/poison-dart.html) (https://claude.ai/artifact/CgUNZEwM5y3GoiiUBNazNa), chosen and polished: glow, teardrops, bubbles, the dart left stuck in its target, a green health bar, a toxic burst and bubbling puddle on a poisoned death, wobbling globs that splat (preview: `DART=<s>` in Tools/Preview). `AbilityId.Dash` became `PoisonDart` (same number; old Dash mastery in saves is not carried over). 8 s: a dart at the enemy nearest the cursor hits for 20 and poisons for 18/s over 5 s; dying poisoned passes it to the 3 nearest unpoisoned enemies within 180 px at 80% (mastered: 5, full strength), full 5 s again, and they pass it on. Phase Step became Virulence (+30% venom, +60 px reach). First-guess numbers: check the feel. The Comet Streak (made for Dash) stays on your other dashes. Armory/Bestiary pages not updated (they still show Dash).
- [x] **Clot Cannon (built, Nov 2026, protocol 227; check in Unity).** The tank Hemomancer's Clot Bolt reworked from [`Mockups/clot-cannon.html`](Mockups/clot-cannon.html) (https://claude.ai/artifact/935GTGvNJLpEqEMFJ7mzkX), look A Gore Mortar ported (B Plasma Gloss and C Scab Vein are in the mockup if you'd rather). Hold to charge, let go to lob a clot over enemies: 160 px on a tap to 520 px at full (0.9 s), 34 splash within 70 px at any range, a 130 px pool for 4 s that slows 40% and congeals at 1 s (stuck 1 s); the 15% guard in your own pool is gone; each shot costs 6% of max health and the pools heal anyone in them 4 HP/s (protocol 232) (`Core/BloodMage.cs`, `Game/ClotCannonLook.cs`). Drops from the hand and the flying clot are only for show. Check in Unity: the arc of beads reads at normal zoom; the pool isn't too dark on every floor; the crust's timing. `WeaponId.ClotBolt` keeps its name. The Armory page still shows the old Clot Bolt.
- [x] **Blood Wall (built, Nov 2026, protocol 225; check in Unity).** Replaces the tank Hemomancer's Crimson Pact (`AbilityId.CrimsonPact` slot kept), look B "Thornvein" from [`Mockups/blood-wall.html`](Mockups/blood-wall.html) (https://claude.ai/artifact/CjMjW8trqMpV7VFRSpUweg). A toggle: a wall of `ZoneKind.BloodWall` nodes is laid every 12 px as you walk; it stops every enemy shot (red aimed too) and enemies (pushed back, 12 a second while against it); allies pass; each stretch stands 6 s. Drain 1%/s doubling every 4 s, can down you; healing (a healer's aura) stretches it. 8 s cooldown from when it ends. Your answers: bullets don't wear it; enemies take damage; no taunt. Decided for you: the ramp stays 1%/s doubling every 4 s (about 17 s alone); bots raise it with a target within 320 px above 70% health and stop at 40% (or when nothing's near). Blood Oath: +2 s standing, the drain doubles 1 s slower. Capstone: double contact damage and a 40% slow. The Crimson Pact taunt and burst code is gone (`FxKind.PactBurst` is now `WallSplash`). HUD: "ON" on the tile while it's up, the drain over the tank. Check in Unity: the wall's thickness at normal zoom, enemies that are pushed along it (they can slide round its ends), the drain number against the name tag, a guest's view (the tail from the last node to the tank is drawn locally), bots. The Armory and Bestiary pages still show Crimson Pact.
- [x] **Lightning Rod, a second ultimate for the Tank Elementalist, on the lobby's Ultimate row (built, Nov 2026, protocol 224; check in Unity).** From the second Tank Elementalist card of [`Mockups/ultimate-ideas.html`](https://claude.ai/artifact/9JuTEzwagoFYJwXfV57pwd); mockup of the build [`Mockups/lightning-rod.html`](Mockups/lightning-rod.html) (https://claude.ai/artifact/PtWmbs4u5J2SKFG8U1BeJj). **The choice** is the Juggernaut Suit's Ultimate row (entry below; merged in): Lightning Rod is key 1 of the Tank Elementalist (`UltKey = 1`). **Lightning Rod** (`Core/LightningRod.cs`, `Game/LightningRodLook.cs`): 70 s; 6 s of charge (`Player.RodT`, replicated), 80% less damage, every enemy (bosses too) targets you; each enemy shot that hits you calls a 30 bolt on its shooter (by your damage), arcing to the 2 nearest within 320 px for 15; a dead shooter's bolt lands where it stood; wet enemies take `WetArcs`; contact and melee hits (Swordsman, Reaver, Riot Shield bash, Devourer bite, Reaper reap) call one on the hitter. My calls: the cap is one bolt per enemy every 0.25 s (other pellets are still cut, arcs aren't capped); arcs reach 320 px; a shot touching you during your hit grace is grounded (used up, no damage, bolt still falls); a shot with no known shooter strikes the nearest enemy within 160 px of where it was fired; no Roar cut on top of the 80%. Capstone: 8 s and 3 arcs. Bots cast it when 3+ enemies have shots coming at them. All numbers are named constants. Check in Unity: the aura's strength next to other buffs (Aeroaegis hides it), the bolt's length from off-screen, the grey numbers beside you, how often bolts fall in a busy wave (too strong?), the blue taunt marks, the lobby keys' text size ("LIGHTNING ROD"), and the preview's dummies shooting during the demo. Previews: `ROD=2.5 Tools/Preview/shot.sh x hud`, `LOBBYULT=1 Tools/Preview/shot.sh x lobby`. Test suite `rod`. The Armory page is out of date (no Lightning Rod).
- [x] **Juggernaut Suit and the lobby's Ultimate row (built, Nov 2026, protocol 222; check in Unity).** From the second Tank > Soldier card of [`Mockups/ultimate-ideas.html`](https://claude.ai/artifact/9JuTEzwagoFYJwXfV57pwd); the build's own mockup is [`Mockups/juggernaut-suit.html`](Mockups/juggernaut-suit.html) (https://claude.ai/artifact/5kQgq2FwguQAfGCmgkUeJR). The Tank Soldier's second ultimate (`Core/Juggernaut.cs`, `Game/JuggernautLook.cs`): 10 s of 300 armour before health, 25% slower, a taunt, a 12-damage flak shard back at the shooter per bullet the armour takes; ends on time, armour 0, going down or the wave ending (chosen: as Last Stand, so no armour is carried into the break). Mastered 450 armour and 18 a shard. The armour is up from the press (the 0.6 s clamp is the look). Picked on the lobby's Ultimate row: key 0 stays Fortress, key 1 is the suit, the rest locked (`LobbyEntry.Ult`, `Msg.UltRequest`, `Player.UltPick`, saved in the seat's `class` line). Check in Unity: the plate size against the real soldier model (plates sit at 1.6x the swollen body radius); the name tag lift over the armour bar at every zoom; the tracers at normal zoom; that bosses really keep switching to the suit (they use `PickTarget` like everyone; some boss patterns may aim on their own); the 75 s cooldown and 300 armour against late waves; the lobby key text "JUGGERNAUT SUIT" in the real font. Previews: `JUGG=3 Tools/Preview/shot.sh x hud`, `JUGGARMOR=0.4`, and `LOADOUT=0,0,0,0,1 ULTDEMO=1 Tools/Preview/shot.sh x lobby`. The Armory page is now out of date (no Juggernaut Suit).
- [x] **Bone Phalanx, the Tank Summoner's second ultimate (built, Nov 2026, protocol 226; check in Unity).** Design from the first Tank > Summoner card of [`Mockups/ultimate-ideas.html`](Mockups/ultimate-ideas.html) (https://claude.ai/artifact/9JuTEzwagoFYJwXfV57pwd); look from [`Mockups/bone-phalanx.html`](Mockups/bone-phalanx.html) (https://claude.ai/artifact/4c2gZtdFuRmaJzVnD7epfY), a running copy of the rules with a timeline, three scenarios and the plain fallback. **The pick** uses the Ultimate row the Juggernaut Suit build added (`LobbyEntry.Ult`, `AbilityDef.UltKey`; Bone Phalanx is `UltKey = 1` for the Tank Summoner): key 0 Titan, still the default, key 1 Bone Phalanx. (This branch first built its own pick in parallel; at the merge it was dropped for main's.) **The ultimate** (75 s): gate 0.5 s, 8 skeletons step out middle first, march 260 px in 6 s on the cast heading, hold 1 s, crumble; shields stop front shots (red aimed too), enemies are shoved and speared (15 / 0.5 s), Heavy enemies slow the line to 45%, bosses, walls, props and pinned enemies stop it, 120 HP each, a broken one leaves a gap; ends on going down and between waves. Mastered: 10 skeletons, 180 HP. All numbers are named constants in `Core/BonePhalanx.cs` (first guesses). One zone with a health byte per skeleton rather than 8 minions (cheaper to replicate). Replay files moved to format 3. Check in Unity: the Tank Summoner's Ultimate row (key 1 lit, the detail box, the preview casting the line); the skeletons' size against real enemies (25 px lanes); whether the gate's soul green reads on every floor; shots at the lane edges (a shot exactly between two shields is judged by the lane it is in); how often 15-damage spears and 120 HP feel right against a late wave; that the line doesn't hitch on clients (its position comes with each snapshot, not smoothed); Visual effects off. Preview: `PHALANX=<s> [PHALANXCAP=1] [FXOFF=1] Tools/Preview/shot.sh x hud`; test suite `phalanx`. Out of date now: the Armory (no Bone Phalanx page). There was no "Pick ultimates for the locked slots" entry in this list to tick off.
- [x] **Bloodbath is now a channel (Nov 2026, protocol 229; check in Unity).** No 15% up front any more: while it runs it drains 4% of your max health a second (`BathDrain`), for up to 10 s. Press F again to stop it (no cooldown spent); it also stops at 15% health (`BathFloor`), when you go down (it used to keep going) or between waves. A stopped pool does nothing and dries up over 1.2 s; the dry-up used to happen during its last 1.2 s while still working. In Unity check: (1) the second F press ends it while the key shows its cooldown (the local press code had to let it through, `bathOut` in `Game.cs`); (2) while channelling, blood drips off you into the pool's middle and a red ring pulses round you, and the gold buff ring under you counts down the 10 s; (3) letting go or hitting 15% dries the pool at once (no tint, no heal numbers); (4) a bot with Bloodbath lets go when nothing's in the pool or it's under 25%. Preview: `BLOODBATH=3` (channelling) and `BLOODBATH=10.6` (drying). The mockup artifact is updated to match.
- [x] **Bloodbath, the Tank Hemomancer's second ultimate (built, Nov 2026, protocol 223; check in Unity).** Merged onto the Juggernaut Suit build's Ultimate row (entry above): both branches built the same pick, and the merge keeps that one (`AbilityDef.UltKey`, `Upgrades.AltUltimate`, `Player.UltPick`) with Bloodbath on the Tank Hemomancer's key 1. From the first Tank > Hemomancer card of [`Mockups/ultimate-ideas.html`](Mockups/ultimate-ideas.html) (https://claude.ai/artifact/9JuTEzwagoFYJwXfV57pwd); the look is [`Mockups/bloodbath.html`](Mockups/bloodbath.html) (https://claude.ai/artifact/SmVXr2cLz1BF87z6Cjmooi). Vein Tithe stays the default (key 0 of the lobby's Ultimate row); **Bloodbath** is key 1: 15% of max health up front (1 HP floor, still goes off), a 170 px pool for 10 s round where you cast (grows over 0.9 s, doesn't follow you), enemies in it 50% slower (Heavy 35%, bosses 20%: they now *can* be slowed by this, through `Enemy.WadeMult`) and bleeding 15/s while in it, each tick healing you 25% (no extra 3%), allies in it take 20% less; ends at 10 s or between waves, not on going down. Capstone: 220 px and 35% back. New: `LobbyEntry.Ult` / `Player.Ult` (0 default, 1 second; the host clamps it; Wildcard ignores it; bots use 0 unless you set a bot's keys), `Msg.UltRequest`, in `Hello`, the lobby, start message and saved seats (a sixth number on the save's `class` line). In Unity check: (1) the lobby's Ultimate row: as a Tank Hemomancer keys 0 and 1 light up, the picked one in inverse video, keys 2-3 padlocked; every other class shows one lit key; clicking key 1 shows Bloodbath in the detail box and the preview casts it (a 10 s pool, so the demo waits ~11.5 s between casts); changing class or role resets to key 0; a friend's pick shows on their side and in the match. (2) The pool: dark red with a wavy edge that wobbles slowly, ripples rolling out, a splash as it floods, enemies in it tinted red with drops falling, a thin red arc under allies standing in it, small pink "+n" to the right of you each 0.5 s, and the dry-up (brown, pulls back, cracks) in its last 1.2 s. Visual effects off: a plain disc, rim and time ring. Reduced effects: no wobble, fewer ripples and drops. (3) Heavy enemies and bosses visibly slower inside. (4) A bot given Bloodbath by hand casts it with 3+ enemies within 170 px and over 40% health. Preview: `BLOODBATH=3 Tools/Preview/shot.sh bath hud 6 12.2 2` (0.4 the flood, 9.4 the dry-up; `BATHMASTERED=1`). Tests: `Tools/SimTest/run.sh 1 1 bloodbath`. The Armory and Bestiary pages are not updated (the Armory doesn't list Bloodbath yet). There was no "Pick ultimates for the locked slots" entry in this file to tick off; the other ideas on the ultimate-ideas page still need picking for the remaining locked keys.
- [x] **Twin Rifle grenade tubes and Airstrike (built, Nov 2026, protocol 217; check in Unity).** From [`Mockups/twin-rifle-grenade-rework.html`](Mockups/twin-rifle-grenade-rework.html) (https://claude.ai/artifact/NqurerQMDKgB5MtzSyc8pw) and [`Mockups/airstrike-rework.html`](Mockups/airstrike-rework.html) (https://claude.ai/artifact/52bK9KqRg5TcvxKbTSqViz). The Twin Rifle's Alt fires grenades from a tube under each rifle: hits load them (40 a grenade, crossfire twice), two at most, one a press, left then right, and the turn order holds through a refill (`Core/TwinGrenades.cs`). Airstrike (Carpet Wall) replaces the Cluster Grenade (`Core/Airstrike.cs`, `Game/AirstrikeLook.cs`). Check in Unity: the tubes read on the guns at normal zoom; the jet's size and shadow offset; the flame strip isn't too busy; the numbers (60 a grenade, 55 a bomb, 16 s cooldown). Bots don't use the rifle's Alt. The Armory and Bestiary pages still show the old Cluster Grenade.
- [x] **Rapier Tempo and Flourish (built, Nov 2026, protocol 218; check in Unity).** From [`Mockups/rapier-tempo.html`](Mockups/rapier-tempo.html) (https://claude.ai/artifact/LAJY4Vkb2Bm6uBu4ohKZPo), with the slider's largest Tempo (20). The same-target damage streak is gone: every thrust deals a flat 18. Every enemy a thrust hits adds a Tempo stack (up to 20; with the +1 upgrade a two-enemy thrust adds two; skewered bullets add none, though they are still skewered). The wait between thrusts goes from 0.48 s at 0 to 0.16 s at 20 (fire-rate upgrades and Battle Fury speed it up further, floor 0.08 s). Tempo holds 1.2 s after the last hit, then drains 2 a second. The weapon-action key (Alt) spends 3+ Tempo on a Flourish: one alternating sweep and one piercing crescent wave (22, 380 px) per two stacks, 0.1 s apart. New `Rapier.cs`; `Player.RapierStreak`/`RapierTarget` replaced by `RapierTempo` and the replicated grace byte `RapierGraceLeft`; `DashFlourish` = 16 on `Msg.WeaponDash`. Look: one diamond per stack on the arc behind you (20, smaller than before), gold at full, a thin arc under them for the grace period and an orange top diamond while draining; the Flourish's sweeps are the Rapier's ordinary sweep look, alternating (`MeleeOwnerOf`), and the waves are the Knight's standard sword-wave bullets (not the mockup's gold-at-full crescents). Tests: `run.sh 1 30 lastfour`. Check in Unity: 20 diamonds against the model and HUD, the Flourish's sweeps (drawn by the generic sweep path for the first time with a Rapier), bots don't use the Flourish, a guest's view. The Armory page is out of date for the Rapier; the Needle entry below is superseded for the streak.
- [x] **One pair of arms, swings that return to the held pose (built, Nov 2026, no protocol change; check in Unity).** From [`Mockups/held-arms-handoff.html`](Mockups/held-arms-handoff.html) (https://claude.ai/artifact/1ocp6M9zvsi6fPngedEUCW). The model's own sleeves and gloves were still drawn under the arms of the Halberd, Warhammer, Greatsword, Rapier, Quarterstaff and Bo Staff looks; it now leaves them out (`CharacterModels.cs` `LookDrawsArms`, one list with the Twin Blades, Lightbringer, Lance and Censer). The **Katana** gets arms of its own: held two-handed, the Ninja's arms to charcoal gloves on the grip, in the held pose, swipes and lunge (a full strike meter keeps the model's arms and the sheathed blade). **No fading:** the held weapon is drawn whole or not at all (`MeleeSwingShowing`); the Rapier, Katana, Greatsword and Quarterstaff hold the end of the cut 0.06 s, then ease into the exact held pose over 0.16 s turning to your current aim (`MeleeRest`, `DrawSwingModel`); the Bo Staff's swings stay solid to the carry and match your aim and the +20 px reach, and the shove draws back into the carry; the Greatsword's wind-up starts from the held pose, the guard rises out of it in 0.08 s and the Rend's thrust settles back into it. Only a player's newest swing draws the weapon. **Kusarigama and Blood Lash (follow-up):** held with arms too (`DrawMeleeHeldIdle`): the sickle in the lead hand with its chain paid out in a cut and the weight hanging from the off hand (not while thrown or whirling); the whip's grip in the lead hand, the whip swaying slack until it cracks; both return to the held pose. **Pauldrons:** the Tank model's shoulder plates sat beside the Halberd's own turning pauldrons and read as a second pair of arms; the model now leaves them out for the Halberd, Lance, Twin Blades, Lightbringer and Censer (`LookDrawsPauldrons`). `HeldPhase` is now used only by the Katana's sheathed swings (full strike meter). Preview: `CLOTBURST=1` with `SWINGW=BloodLash`. Preview: `SWING=` now registers the swing before drawing (as the game sees it after one frame), and `KATANALUNGE=1` renders the Katana's lunge. Check in Unity: the Kusarigama's hanging weight and the Blood Lash's sway at game scale, the shoulders of the five pauldron weapons without the model's plates; the first frame of a swing still jumps from the held pose to the start of the arc (no wind-up except the Halberd's); fast chained swings; the Katana's arms against the Ninja model; a guest's view.
- [x] **Swordsman and Shield Swordsman (built, Nov 2026; protocol 220; check in Unity).** From [`Mockups/swordsman.html`](Mockups/swordsman.html): a melee enemy that cuts a cone (a curved band from its hand to the tip of a 140 px blade, 90° wide) after a 0.55 s wind-up that fills the cone red, and one with a shield that stops frontal shots (flashes on a block, drops for the cut and most of the recovery). Waves 7 and 10 (singles before wave 13, then in the "Sword line" and "Shield line" squads; `Core/Swordsman.cs`, `Game/SwordsmanLook.cs`; preview `SWORD=1`). Numbers (85 HP, damage 14, speeds 95/75) are first guesses. **No Bestiary or Armory entries yet** (the field-guide test skips them like the other TODOs there; ask if you want entries).
- [x] **DPS Knight abilities: Caught Steel, the Gauntlet, Misericorde, the Drawbridge (built, Nov 2026, protocol 221; check in Unity).** From [`Mockups/knight-dps-abilities.html`](Mockups/knight-dps-abilities.html) (https://claude.ai/artifact/LvoHD7caA1dQB7mwuJZKTX), looks Forge, Heraldic, Last Rites and Oak & Iron, ported shape for shape (`Game/KnightDpsLook.cs`, `Game/DrawbridgeLook.cs`) and checked frame by frame against the mockup with `KNIGHTDPS=steel|gauntlet|mercy|bridge KDT=<s> Tools/Preview/shot.sh x hud`. They replace Battle Fury, Lunge, Whirlwind and Blade Tempest (the `AbilityId` slots are kept; the old code, `FuryT`/`WhirlT`, the Blade Tempest beams and the Cyclone/Flurry/Bloodlust cards are gone; new cards Coup de Grace, Iron Grip, Forged Edge). **Caught Steel** (16 s): 6 s of catching bullets that skim past (14 px past your hit circle, up to 12), Space again or the time running out throws them (22 each, they cut enemy bullets), a hit drops a third; the cooldown starts at the throw. **The Gauntlet** (9 s): thrown at the enemy nearest your cursor along your aim (380 px, it flies over the pack, as in the mockup), Challenge 5 s, a charge carried by your own game (new `Msg.Carry`) bowling enemies aside, impact 30; killing it resets the cooldown. **Misericorde** (12 s): executes enemies under 25% (anyone's damage) and leaps on within 200 px; a wounded boss takes 120 and ends the chain; a press with nobody wounded costs nothing. **Drawbridge** (75 s): 500 x 100 px, slam at 1.05 s for 140 (edges 45 and a shove), 5 s as a walkway enemies can't step onto and whose rails stop every shot, red aimed ones too. Your open questions, answered as the mockup proposed: the release is a second press; bosses as above; the rails stop red aimed shots. Left out or changed from the mockup: the throw's 0.22 s wind-up (the throw is instant), the gauntlet drawn in your off hand, the mockup's whole-scene hit-pause (here your own screen holds), the release cut drawn as a crescent (your weapon doesn't swing it), Caught Steel's pips over your head rather than in a corner. The loadout preview wounds its dummies just before a Misericorde cast. Test: `run.sh 1 30 knightdps`. Check in Unity: the bridge's foreshortened fall and the slam's shake, the crest over small enemies, the Misericorde daggers in a crowd, the shards' orbit at game speed, a guest's carry (charge and leaps), bots with these abilities. The Armory and Bestiary pages are out of date for these four.
- [x] **Lance rework: the Breach, Hook & Haul, the Winged War Lance (built, Nov 2026, protocol 216; check in Unity).** Picked from [`Mockups/lance-rework.html`](Mockups/lance-rework.html) (https://claude.ai/artifact/SD5Jey54h5j6mCHWSh3Gjz): moveset Breach, alt Hook & Haul, model Winged War Lance, built from the mockup's own numbers. `Core/Lance.cs`, `Core/LanceMotion.cs`, `Game/LanceLook.cs`, `Game/LanceSoundBank.cs`. **Chain:** Jab (14, 178 × 32 px box, 30 px shove), Second Jab, Wide Sweep (16, eight cones that follow the lance across 150°, batting enemies to your left 85 px), Breaching Thrust (40, 0.2 s wind-up, 34 px step in, 205 × 88 px box, 160 px shove, 1 s stun); weapon Damage 14, Reach 178; one chain 84 damage in about 1.3 s, a bit under today's 60 a second before Impact. **Impact** (new, `Knock.cs` calls `LanceBump`/`LanceWallImpact`): a Lance-shoved enemy with 34 px or more of its push left that meets another: both take 10 and the second slides on with 70% of the push; into the arena wall, 18 and a 0.8 s stun (instead of the ordinary wall slam); orange number with an IMPACT/WALL tag (`Juice.cs` `ImpactNumber`). **Hook & Haul** (Left Alt, 4 s): the enemy nearest the cursor within 260 px is hooked for 10, stunned 0.8 s, hauled to 62 px in front; the next press is the Breaching Thrust (`Player.LancePrimed`, replicated: the fourth pip glows). **Look:** held like the Halberd/Twin Blades: the model leaves its arms and weapon out (`CharacterModels.cs` ownArms) and `LanceLook.cs` draws both arms, pauldrons, a heater shield on the left arm, the Winged War Lance couched on the right (pennant streaming), its trail, the jab wedge, the breach box, sparks, Impact bursts and wall cracks. Own sound bank (`render.sh Lance`) and hit-pause/shake (`LanceHitFeel`). Check in Unity: the pennant and shield at game scale, the sweep's trail fan, the step forward's timing, Impact against layout blocks (only the arena wall counts as a wall today), and the hook on a far target. The Armory page is now out of date for the Lance.
- [x] **DPS Hemomancer abilities rework: Arterial Spray, Blood Debt, Vein Rush (built, Nov 2026, protocol 210; check in Unity).** They replace Blood Boil, Frenzy Pact and Hemo Nova (the `AbilityId` slots are kept: `BloodBoil`, `FrenzyPact`, `HemoNova`). Looks picked from the mockups: Jet of [`Mockups/arterial-spray.html`](Mockups/arterial-spray.html) (https://claude.ai/artifact/G1jnzTvdtL7L5jPWQTAZq8), Ledger of [`Mockups/blood-debt.html`](Mockups/blood-debt.html) (https://claude.ai/artifact/LdKb6jrjKnnRMbyF7XkaPf), Needle of [`Mockups/vein-rush.html`](Mockups/vein-rush.html) (https://claude.ai/artifact/L2wfZgqBb993v2o7nQRqo6). **Arterial Spray** is a toggle like Blood Shell (press to start, press again to stop; there's no held-Space input), not held as the mockup had it: a 60° jet, 200 px, 30 damage a second plus a bleed and a 20% slow, pushes small enemies, drains 4%/s, sputters at 15%; capstone washes away bullets; card **High Pressure** (was Boiling Point). **Blood Debt**: health to 25% (5% if already that low), the rest owed for 8 s; kills repay 4% (Heavy, elite, boss 10%), boss damage repays 10% of the damage (instead of the mockup's 1% a hit, which fast weapons would abuse); lifesteal 15%; paid in full +2 s; capstone repays 50% more; card **Usury** (was Bloodrush). **Vein Rush**: 10%, 280 px in 0.22 s, untouchable, 25 + bleed and 2% back per enemy cut, a slowing trail (`ZoneKind.VeinTrail`, a row of small pools); capstone: the trail bursts at 1 s for 20; card **Crimson Tide** (now +25% reach, +1% refund). Numbers are first guesses. Check in Unity: the jet's drop density and spatter, whether the Ledger's "OWED" tally and stamps sit well by the bar, the Needle's afterimages at speed, Vein Rush near layout walls (the host cuts along the straight line from start to target, like the Lunge, even if a wall stopped you short), and the bots' use (they close the spray when nothing's in reach). Left out of the mockups: the Needle's hit-stop and screen flash, the Ledger's top-right ledger panel (the tally sits by the bar instead), the Jet's live DPS readout. **Looks redone (protocol 213)** as ports of the mockups' own particle systems, after the first build looked plain in Unity: the Jet's 420 drops a second with landing stains, soaking enemies and death bursts; the Ledger's card beside you (hatched bar, OWED / PAID / LEFT, coins, pops, tilted stamps, shards); the Needle's glowing streak, stretched figure, afterimages, slashes, homing refund drops, hit-stop and flash, and a continuous trail. Particle sizes are scaled up 1.6x (spray) and about 1.4x (Needle), because the mockups' canvases show a world unit about 1.7x bigger than the arena does. Check in Unity: the jet's density and stain build-up, the ledger card's spot beside you (it flips left near the right edge and below you near the top), the hit-stop on a rush through a crowd. Preview: `HEMODPS=spray|debt|rush [DEBTSETTLE=borrowed|paid|forfeit] [RUSHT=0.3] [RUSHMASTERED=1] Tools/Preview/shot.sh x hud`. The Armory pages for these three abilities are out of date.
- [x] **Exsanguinate rework: the Barbed Lash (built, Nov 2026, protocol 212; check in Unity).** From [`Mockups/exsanguinate-rework.html`](Mockups/exsanguinate-rework.html) (https://claude.ai/artifact/12cWV3Wqpg5F8DYBxZQCc8), take B. Whips crack out to 220 px and hook every enemy in it (stunned); the hook tears the enemy's veins out, they braid, and at 0.62-0.88 s a yank throws them in to touching you; 20 drained from each on arrival. Heavy enemies and bosses stay put and are still drained. **Protocol 212:** `FxKind.Exsanguinate` (cast -1, per enemy, impact -2). Check in Unity: the look (`Game/ExsanguinateLook.cs`), the impact's hit-pause and shake (Juice.cs), that the yank reads at 60 fps, and that enemies yanked across walls look right (it ignores walls, as the old pull did). Open: pull all the way in, or stop at Blood Lash range? The Bestiary/Armory pages still describe the old Exsanguinate.
- [x] **Lightbringer looks: Sundial, Sunrise, Spear, Hex plates (built, Nov 2026, protocol 215; check in Unity).** The four looks picked from [`Mockups/lightbringer-looks.html`](Mockups/lightbringer-looks.html) (https://claude.ai/artifact/6BwGmDMgd6vCunVzcBnvj9), ported shape for shape in `Game/LightbringerFxLook.cs`: Noon's ring with two echoes, a turning glyph dial that draws back into the blade, shards, pillar, glint and cracks (Radiant adds a green ring); Dawn's and Mercy's crescents (gold, three afterimages, green-edged with motes for Dawn, doubled with tiny hexagons for Mercy; the host marks them with the shot's `Look`, Dart or Crescent); the Sunlance as a spear forming over the off hand, flying, a beam pinching to a thread in a green spiral, a halo dropping onto the ally; Mercy's ward as six gold plates with a light running round, the plate facing a shot flashing white and cracking. New `FxKind.LightMark` (protocol 215): a heal burst or the ward snapping shut on an ally (delayed for Noon until the ring reaches them), and a shot striking the ward (its angle from `RiotImpactPoint`). The mockup's additive layers are drawn in the glow pass; with Visual effects off they're drawn in the ordinary pass. The plain blue shield ring no longer shows under a Lightbringer ward. Preview: `KNIGHT=1 SWINGW=Lightbringer LIGHTSTEP=3|4|cut|lance [CRES=mercy] [WARD=1 WARDHIT=0.1] SWING=<s> Tools/Preview/shot.sh x hud 6 12.2 2`. Check in Unity: the glow pass's brightness against the mockup, the Dawn crescent under enemy labels, the ward plates on bigger player models.
- [x] **Lightbringer rework: Dawn, Mercy, Noon with the Sunlance (built, Nov 2026, protocol 208; its sound bank 209; check in Unity).** From [`Mockups/lightbringer-rework.html`](Mockups/lightbringer-rework.html) (https://claude.ai/artifact/4Mu7FFXioeyoNb6Bv9Gh52) and [`LightbringerRework.md`](LightbringerRework.md). **Chain** (`Core/Lightbringer.cs`, motion `Core/LightbringerMotion.cs`): Dawn Cut (17, 100°, crescent 12 that heals the first hurt ally it passes for 6), Mercy Cut (17, crescent that wards the first ally it passes: 12 HP shield, 3 s), Noon Sunburst (22 within 140 px, 28 px shove, allies in it heal 10, you 5). Hits land when the sword gets there; a press counts 85% through; the chain starts over 0.8 s after a step ends; each step moves you 10-14 px forward. **Gauge:** +1 a cut hit, +2 a Sunburst hit; full, the next Noon is Radiant (220 px, 30 healing, a ward on everyone in it, spends the gauge). **Sunlance** (weapon-action key, 1.5 s): 4+ pips, the ally nearest the cursor within 450 px heals 4 a pip (downed: +25% revive), enemies on the line take 10 + 2 a pip, spends the gauge. **Protocol 208:** `Player.MercyT` replicated; `FxKind.LightStep`, `Sunlance`; `DashLance` on `Msg.WeaponDash`; the chain shares `Player.Combo` with the Twin Blades. **Look** (`Game/LightbringerLook.cs`): arms and a longsword with a sun-disc pommel, held at the ready when idle and swung through every step with a gold trail; the fuller glows with the gauge and the blade burns when full; Noon's ring, rays, pillar and cracks; the Sunlance's gathering off hand, tether and beam; three pips, a cooldown ring, a gold hexagon on warded players. Test: `run.sh 1 30 lightbringer`; preview `KNIGHT=1 SWINGW=Lightbringer SWING=0.2 LIGHTSTEP=1..4|lance Tools/Preview/shot.sh x hud 6 12.2 2`. Check in Unity: the arms against the Knight model, the forward steps, a guest's view, the sounds in the mix (its own bank, `Game/LightSoundBank.cs`: ten synthesized `Sfx` values, `LightCut1/2`, `LightNoon`, `LightRadiant`, `LightHit`, `LightHeal`, `LightWard`, `LanceGather`, `LanceRelease`, `LanceFizzle`; render with `Tools/SoundCheck/render.sh Light`), bots with the Lightbringer (they don't use the Sunlance). The Armory page and `WeaponDps.Generated.cs` are out of date for the Lightbringer.
- [x] **Twin Blades rework: the Crescent Waltz with Twin Fang (built, Nov 2026, protocol 203; check in Unity).** From [`Mockups/twin-blades-rework.html`](Mockups/twin-blades-rework.html) (https://claude.ai/artifact/5yhfDqZLxte8Cmz7TGq9XF), the Crescent Waltz chain with its own alt; the other two chains and the five alt ideas on that page were not built. **Chain** (`Core/TwinBlades.cs`, motion `Core/TwinBladesMotion.cs`): Rising Cut (16, 110 degrees), Falling Cut (16), Double Jab (two 115 px thrusts, 12 each), Pirouette (13 within 80 px, twice, cuts small bullets), Crescent Shear (44, 200 degrees, 18 px shove, plus a piercing crescent wave for 30). Each hit lands when the blade gets there in the motion. A press counts once a step is 70-85% through; the chain starts over 0.7 s after a step ends. Each step moves you 12-24 px forward (your own game, like the Katana's lunge step). **Twin Fang** (weapon-action key, 4 s): both sabres fly out on mirrored curves to the cursor (60-210 px), burst where they cross (25 within 55 px) and come back the other way, 18 each way per sabre; no swings until they're caught (0.95 s). **Gameplay changes:** weapon damage 15 > 16; the 10-hit priming and the dash-strike are gone; `+1` now adds a Shear wave (was an extra cut). **Protocol 203 (204 with the feel pass):** `Player.Combo` now carries the chain step (low 3 bits) and a step count (above them); `FxKind.TwinStep` and `TwinFang` added; `DashStrike` on `Msg.WeaponDash` now throws Twin Fang. **Look** (`Game/TwinBladesLook.cs`): the model leaves its arms and sword out; both arms, pauldrons, two curved moon-white sabres with gold knuckle bows and their shadows, held at the ready and swung through every step with a pale trail; the thrown sabres spin along the host's own path; five pips over the head (the fifth glows before the Shear) and a cooldown ring under your own feet. **Feel pass (built, Nov 2026, protocol 204):** ten new `Sfx` values in their own bank (`Game/TwinSoundBank.cs`, synthesized like the Katana's: `TwinCut1`/`TwinCut2` rising and falling ringing cuts, `TwinJab`, `TwinSpin` a 0.4 s whirl for the Pirouette, `TwinShear` from its wind-up through the crossing double ring, `TwinHit`/`TwinHitHeavy` a cut landing, `TwinThrow` a flick and a spinning whirr, `TwinCross` the clash where the sabres meet, `TwinCatch` two clicks; all but the catch use the Weapons volume). The host sends `FxKind.TwinHit` (draws nothing) when a step or sabre cuts something; it carries the hit sound, and your own game holds the picture and shakes (`Juice.cs` `TwinHitFeel`): cuts 0.025 s hold (0.12 s gap, so a chain gets one each) and 0.06 shake, jabs 0.018 / 0.05, the Pirouette a 0.04 kick only, the Shear 0.05 s + 0.01 per extra enemy (up to 3) and 0.18, a passing sabre 0.04 kick, Twin Fang's burst 0.04 s and 0.15. WAVs of every sound were rendered for a listen outside Unity. Test: `run.sh 1 30 missilesriot`; preview: `KNIGHT=1 SWINGW=TwinBlades SWING=0.2 TWINSTEP=1..5|fang Tools/Preview/shot.sh x hud`. Check in Unity: the arms against the Knight model, the forward steps (they ride `DashT`, so a dodge can't start during one), the Pirouette's arms circling while the body faces the aim, a guest's view, Reduced effects, the sounds in the mix (a cut and its hit play every 0.18 s in a chain), whether the holds stutter with Battle Fury's faster swings. The Armory page and `WeaponDps.Generated.cs` are out of date for the Twin Blades.
- [x] **Greatsword rework: three-hit chain and Guard & Rend (built, Nov 2026, protocol 197; check in Unity).** From [`Mockups/greatsword-combo.html`](Mockups/greatsword-combo.html) (https://claude.ai/artifact/1CZW4QkB9kv6kupYWqdQc6), as written. Chain: cut (40), return cut (40), overhead rend (90, 0.25 s wind-up, 40 px shove, the +1 projectile's sword wave at 50%); `Player.GreatStep`, restarts after 0.9 s. Alt (hold Left Alt): a 120 degree guard arc 72 px out stops enemy bullets (not red aimed shots), half speed, no swinging; each bullet adds Temper (max 6); letting go fires a piercing Rend for 30 + 15 per point; six points prime the next chain to start on the rend. Weapon base damage 60 > 40 (the chain averages about the same DPS). **Protocol 197 (198 with the sounds below; 200 once merged, after the Warhammer's 199):** `Player.GreatStep`, `GreatTemper`, `GreatGuard`, `GreatPrimed` are replicated; `DashGuardOn`/`DashGuardOff` ride `Msg.WeaponDash`; `FxKind.GreatRend` added. Look: the chain, crescents (each hangs the way its cut swept), Temper pips behind you, the guard arc and the Rend lance in the Moonlit Crescent look. **Wind-up and sounds (built, Nov 2026, protocol 198):** the overhead rend's wind-up is drawn (`DrawGreatWindup`: seen as `GreatStep` turning to 3, the blade swings back to the cut's start while a ring tightens; the held sword phases out) and five new `Sfx` values (`GreatWindup`, `GreatRaise`, `GreatBlock`, `GreatFull`, `GreatRend`; `Game/GreatswordSoundBank.cs`, synthesized like the Katana's; the wind-up and the Rend use the Weapons volume, the guard's the Effects volume; `GreatBlock` is raised at most every 0.07 s). Check in Unity: how the sounds sit in the mix, and that the wind-up pose lines up with the cut on a guest's screen. **Guard pose (built, Nov 2026, no protocol change):** while the key is held the sword is drawn braced along the aim (`DrawGreatGuardPose`), trembling a little, its edge lighting along its length as Temper builds and gold at six, then thrust forward for 0.22 s when you let go. **Rend hit-stop (built, Nov 2026, no protocol change):** your own Rend that catches an enemy holds the picture 0.04 to 0.08 s and shakes (0.12 to 0.32), harder with more Temper (`Juice.cs`). Nothing from the Greatsword is left unbuilt. Open questions from the mockup: does the guard block melee (it doesn't), can the rend wind-up be interrupted by stun (it can't). Check in Unity: the guard against bullet-heavy screens, a guest's view (guard raised by message), Reduced effects. The Armory page is out of date for the Greatsword.
- [x] **Kusarigama remake: sickle cut, weight throw and Whirl & Drag (built, Nov 2026, protocol 206; check in Unity).** From [`Mockups/kusarigama-combo.html`](Mockups/kusarigama-combo.html) (https://claude.ai/artifact/Wbq2o7Q7363nvb47kZXbLg), hook and reel dropped as asked. Fire: a sickle cut (16, 144 degrees, 96 px), then within 1 s the weight throw (34, 280 px, first enemy in the lane, stuns 0.4 s, dragged to you). Weapon-action key (hold): the weight circles you, pulls enemies within 200 px in and cuts them (12 a hit, you move at 55% speed, no swinging, 3 s at most); letting go snaps the chain in (30, 110 px, stuns 0.6 s) and starts a 3 s cooldown. `Player.KusariStep`, `KusariAim`, `KusariWhirl`, `KusariHeld`, `KusariAng` replicate; `HookId`/`HookT` now carry the throw. Not built, since you didn't answer: the chain cutting small bullets and a Tank damage cut while whirling. Look: `Game/KusarigamaLook.cs` (the weight, the pull ring, chevrons and blur); the sickle cut draws a lilac ribbon and leaves a crescent hanging for 0.5 s (`KusariCutLook`, visual only: it does not stop bullets), and the weight, chain and whirl were brightened after a first pass looked flat next to the mockup. The Armory and Bestiary pages still describe the old weapon.
- [x] **Quarterstaff rework: two hands, left/right swipes, Jab stacks, cone jab (built, Nov 2026, protocol 196; check in Unity).** From [`Mockups/quarterstaff-rework.html`](Mockups/quarterstaff-rework.html) (https://claude.ai/artifact/QqTBmHXxRJNBPxrXVy6vvE), look C (Beacon), one stack per enemy hit, Charges (one stack a jab), no decay, jab a little faster. `Core/Quarterstaff.cs`: every enemy a swipe hits adds a stack (`Player.StaffStacks`, replicated), limit 3 plus one per extra projectile, never fading (dropped when you swap weapons); the weapon-action key (held, repeats every 0.32 s) spends one on a jab (`DashJab`): 48 degree cone, 170 px, 30 damage, 110 px shove, bats bullets, heals 3 per enemy (3 enemies), holds the next swipe off 0.28 s. **Gameplay changes:** the swipe is now one 130 degree arc at 120 px (was 150 degrees at 105) and the follow-up strike with the other end is gone (Multishot now raises the stack limit); `ExtraDesc` and `Desc` rewritten. `Game/QuarterstaffLook.cs`: two gloved hands and arms, an iron shaft with steel bands, a lantern head whose crystal and circling pips follow the stacks, diamonds behind you, the jab's pale gold cone; the look alternates the swipe's direction like the Katana's. Test: `run.sh 1 30 quarterstaff`. Check in Unity: the held pose (the new staff against the cape and the model's own gloves), the arms at the end of a swipe, the pips against other HUD, a guest's view and key, the jab's feel at 0.32 s, Reduced effects. Not built: the Oak and Brass and Pilgrim's Ash looks, a wind-up pose before the swipe. The Armory page is out of date for the Quarterstaff, and `WeaponDps.Generated.cs` still has the old Quarterstaff number.
- [x] **Bo Staff held in two hands, Sage ribbon look (built, Nov 2026, no protocol change; check in Unity).** From [`Mockups/bo-staff-animations.html`](Mockups/bo-staff-animations.html) (https://claude.ai/artifact/B6cHUfcj3uthnR7sisqsLw), look Sage ribbon; Afterimages was not built. Drawing only, with Visual effects on (`Game/BoStaffLook.cs` `DrawBoStaffFx` from `DrawSlashLayer`; damage, reach, cones and timings are unchanged). The slash, jab and spin show the staff in two gloved hands with arms to shoulders, the grip sliding as it is struck, then a follow-through back to the carry; the Sage ribbon is a soft sweep from the tip in toward the staff, brightest at the head, fading in 0.14 s, only while it is struck. The jab adds a shock ring at the tip and speed lines behind you, the spin a ring on the floor as it ends. The shove's staff is now held crosswise in both hands (`DrawStaffShoveLayer`). **Not built from the mockup:** the wind-up (the host lands the hit as the effect starts, so the staff is already drawn back when the sweep begins, as with the Halberd's rest pose), the body turning in the spin and the 12 px jab step (the model is the game's own and does not move), the Today/New switch. The +20 px upgrade lengthens the staff's strike. Check in Unity: the arms and gloves against the Ninja model, the ribbon on busy screens, a guest's view, Reduced effects, chained swings (a new effect takes over from the old one's tail), the shove's long arms. The Armory page is out of date for the Bo Staff.
- [x] **Rapier in the Needle look (built, Nov 2026, protocol 195; check in Unity).** From [`Mockups/rapier-streak.html`](Mockups/rapier-streak.html) (https://claude.ai/artifact/3zG7qzNiKRCLsxRpVyFvsj), look Needle; the numbers are unchanged. **Protocol 195:** `Player.RapierStreak` is now replicated (`GameNet.cs`) so every screen can draw the streak. `MeleeLooks.cs`: the thrust is a thin lilac-to-white streak from the hand to the point (a little wider per stack), a star glint and a ring on the nearest enemy in its cone (`RapierThrustLook`), and a gold glow on the point at six; the figure reaches: a hand and arm that extend with the thrust, the thin blade flexing at the end, a swept hilt and the off hand behind your back (`DrawMeleeModel`); six small diamonds on an arc behind you fill as the streak builds and turn gold at six with a faint halo (`DrawRapierPips`, from `WorldRenderer.cs`, shown while a streak is going). `Juice.cs`: your own streak hits give a small kick (harder as it builds) and the sixth stack a 0.04 s hit-pause. Not built from the mockup: the skewered bullet sticking on the blade and spinning before it pops (the host's white spark is the only cue), the Quicksilver and Cadence looks. **Known bug, not fixed here:** with the +1 upgrade (a thrust hits two enemies) the streak resets on every thrust and never builds (`Knight.cs`, the `s.Rapier` block in the hit loop); a fix task is queued. Check in Unity: the pips against the character model and other HUD, the hand and blade against the Rapier's held model, a guest's view of the pips, Reduced effects. The Armory page is out of date for the Rapier.
- [x] **Rapier streak with the +1 upgrade (fixed, Nov 2026, no protocol change).** The streak was updated per enemy hit, so a thrust that hit two enemies reset it every time. It now follows the nearest (primary) enemy, updates once per thrust, and the same bonus applies to every enemy that thrust hits (`Knight.cs` `LandSwing`). Skewered bullets still add stacks. Test: `run.sh 1 30 lastfour` ("rapier +1"). Check in Unity: two enemies in line, thrust six times, the second takes the same damage as the first.
- [x] **Greatsword in the Moonlit Crescent look (built, Nov 2026, no protocol change; check in Unity).** From [`Mockups/greatsword-sweep.html`](Mockups/greatsword-sweep.html) (https://claude.ai/artifact/XC32DQ6v2oimt62QUVyNLW), look Moonlit Crescent; the numbers are unchanged. `MeleeLooks.cs`: the sweep is one long pale-blue crescent, thin at the tail and fat at the blade (`KatanaCrescent` with a 0.3 width and no air-shock arc), pale spark streaks, and a slash mark across each enemy it passes (`KatanaMarks`); the sword (`DrawMeleeModel`) is a wide blade with a fuller and a big gold guard in two gloved hands joined to your body by arms. `WorldEffects.cs` `DrawSteelWallFx`: the hanging crescent is a glassy arc that appears as the sweep passes, with a glint running along it, which brightens and ripples where a bullet died on it (found from the host's spark). `WorldShots.cs`: a Greatsword's sword beam is a bright crescent with a trail of fading ones. `Juice.cs`: your own cut that catches an enemy holds the picture 0.05 s and shakes (0.14). Not built from the mockup: the wind-up (the host's hit lands as the effect starts, so the sword has no pose before the cut), the Cold Steel and Gilded looks, the damage numbers (already in the game). Check in Unity: the big sword against the character models, the crescent against bullet-heavy screens, a guest's view, Reduced effects, the sword beam trail. The Armory page is out of date for the Greatsword.
- [x] **Storm Hammer: Thunderhead throw + Pulse slam on Alt (built, Nov 2026, protocol 207; check in Unity).** From [`Mockups/storm-hammer-throw.html`](Mockups/storm-hammer-throw.html) (https://claude.ai/artifact/AJEUXnuAAJbrmmPsQDPxqQ) and [`Mockups/storm-hammer-slam.html`](Mockups/storm-hammer-slam.html) (https://claude.ai/artifact/GRidKh45EZsAAjSAZQfnjC). `Core/StormHammer.cs`, `Game/StormHammerLook.cs`. The throw is now delayed 0.22 s (the hammer rises over the shoulder, snaps forward, then leaves the hand; the held one re-forms 0.5 to 0.75 s after the click); the thrown hammer is about 48 px across with hit radius 22 (was 11: a balance change, say if you want it back), a storm-cloud trail, forks to the floor and a sky bolt on the hit (`FxKind.Strike`). Alt (Left Alt, 6 s cooldown, fixed size, no charge): raise, slam 52 px ahead 0.58 s after the press, then a 280 px shockwave (560 px/s) hurts each enemy once for 40, shoves 40 px (not bosses or heavies) and electrifies it with one arc. All numbers are first guesses. Not built: the Tap/Full charge, the Skyforge, Tether, Fault line and Aftershock looks, and the second hand on the haft for the slam. With Visual effects off the old small held hammer is drawn. Check in Unity: the held pose against the character model's arms, the pose clock when the throw is spammed (a new HammerThrow effect starts each click), the slam's wave on busy floors, Reduced effects, a guest's view, bots (they throw but never slam).
- [x] **Warhammer in the Forge look (built, Nov 2026, no protocol change; check in Unity).** From [`Mockups/warhammer-slam.html`](Mockups/warhammer-slam.html) (https://claude.ai/artifact/4t6ykGbwxVXZoZEJP2V21A), look Forge; the numbers are unchanged (circle, charge, aftershock). `MeleeLooks.cs`: the hammer is held in two gloved hands joined to your body by arms in your colour (`HammerPose`), its head heats from steel to orange as you charge, with hot seams, a bigger glow and embers (`DrawHammerRaised`); the swing leaves the orange trail plus two lagging ghosts of the head; the impact is a white-orange flash, a crater glow that cools, the shock ring, a pillar for a charged slam, molten cracks that run out and cool from white to dark red over about 2 s (`HammerCracks`, so a hammer slam's effect now lives 2.1 s on screen) and embers floating up for up to 1.6 s. After the slam the hammer stays in the floor, then is drawn back up as the held weapon returns. `Juice.cs`: your own slam holds the picture 0.04-0.07 s and shakes (0.08-0.28 trauma), harder the more it was charged. Stun stars circle over each non-boss enemy the slam caught for the length of the stun (0.25-0.9 s by charge; `HammerStunStars`, worked out from the circle since the host's stun isn't sent to screens, so one the host skipped can still show stars). Damage numbers were already in the game (Settings > Damage numbers) and show on hammer hits. Not built from the mockup: the Quake and Shockwave looks. Check in Unity: the arms and gloves against the character models, the cracks against busy floors, Reduced effects, the aftershock's second crater, a guest's view. The Armory page is out of date for the Warhammer.
- [x] **Warhammer in the Quake look, held like the Halberd (built, Nov 2026, no protocol change; check in Unity).** Replaces the Forge look, from [`Mockups/warhammer-slam.html`](Mockups/warhammer-slam.html) (https://claude.ai/artifact/4t6ykGbwxVXZoZEJP2V21A), look Quake; the numbers are unchanged. **Held:** `DrawHammerIdle` (from the player loop in `WorldRenderer.cs`, beside the Halberd's) draws the hammer in two gloved hands joined to your body by arms in your colour whenever you are not charging and no slam is playing (`HammerBusy`); it is the pose a charge starts from and a slam settles back into, so it moves smoothly in and out of the animation with no pop (the model's own crude hammer is skipped with Visual effects on). After a slam the hammer stays in the floor, is drawn back up, then settles into the held pose by `HammerReturn` (0.85 s). **Look (`MeleeLooks.cs`):** a dull stone-grey head with a lit side and brass bands, nothing glowing; a pale dust ribbon behind the swing with two ghosts of the head; the impact is a brief flash, a ring of turning ticks, a dark crater with a lighter rim, dark cracks that stay about 2 s, dust puffs rolling out, stone chips thrown up that land with a small shadow (`HammerQuake`), and stun stars. Charging pulls dust toward the circle (`WorldShots.cs`) and makes your ring tremble. `Juice.cs`: your own slam holds the picture 0.04-0.07 s and shakes harder (0.1-0.36). Not built from the mockup: the Forge and Shockwave looks, floating numbers (the game already has damage numbers). Check in Unity: the hands and arms against the character models, the hand-off between idle, charge, slam and back, the crater and dust on busy floors, Reduced effects, the aftershock's second crater, a guest's view. The Armory page is out of date for the Warhammer.
- [x] **Halberd held in two hands, Cleaver look, with a wind-up (built, Nov 2026, no protocol change; check in Unity).** From [`Mockups/halberd-swing.html`](Mockups/halberd-swing.html) (https://claude.ai/artifact/N5ezBg3hUcGesvhRa89c1G). `Core/HalberdMotion.cs` is the whole swing timed from the click: 0.05 s pull back, the sweep (80 degrees in 0.21 s, easing in from behind the first step), with the return sweep a 0.08 s turn-over of the head between the two, then follow-through and recovery (0.58 s alone, 0.87 s with the return). **Gameplay change:** the six steps now land when the edge reaches them, so the first cut comes about 0.15 s after the click (was instant) and the last at 0.26 s (was 0.175 s); damage, reach, cone and the 60% return are the same. The effect is now sent at the click and lasts the whole swing (one `FxKind.Slash` with `Dur` = `HalberdMotion.Total`, no new message); the whoosh plays as the first step lands. `Game/HalberdLook.cs`: both gloved hands on the pole with arms to turning pauldrons, the pole sliding through the grip, a blade height with a shadow, the hot trail, three head ghosts, floor dust, and on each enemy orange sparks, a dust ring and a cut mark; your own first cut of a sweep holds the picture 0.04 s and shakes (0.12; Reduced effects and Screen shake off respect it). The held Halberd is drawn by this look when Visual effects are on (the model's small stick is left out); with them off the old stick and arc remain. Check in Unity: the model's own gloves peeking out under the new arms, the pole against the cape and body at the ready (it rests drawn back at 144 px), a guest's view (the swing starts when the effect arrives), swinging again before the recovery ends (a new swing takes over from the old one's pose), Reduced effects, and whether 0.15 s to the first cut feels right (if not, shorten `HalberdMotion.Pull` or the ease-in `WindA`). The Steel Arc and Gale looks were not built. Armory page is out of date for the Halberd.
- [x] **Katana reach and Moon Steel look (built, Nov 2026, no protocol change; check in Unity).** From [`Mockups/katana-reach.html`](Mockups/katana-reach.html) (https://claude.ai/artifact/CNA4mdw5EidAnV5kYbzUeY), look Moon Steel. Swipe reach 95 > 115 px (`Defs` Katana `Reach`) and the lunge line 260 > 300 px (`KatanaLungeReach`); the lunge step stays 110 px. Look (`MeleeLooks.cs`: `KatanaCrescent`, `KatanaSparks`, `KatanaSpear`): the swipe is a pale-blue crescent that is thick at the blade and thin behind it, its tail eaten away as the sweep ends, a white edge and an air-shock arc; hits throw orange and white streaks; the lunge is a hairline spear with shock rings at the tip and speed lines behind you. The Katana's energy colour is now cool (was rose). Three lagging blade ghosts follow each swipe, and every enemy the swipe or lunge passes through gets a bright slash mark timed to the blade (`KatanaMarks`, worked out from the swing's geometry and where enemies are drawn, so a mark can show on an enemy the host's hit just missed at the edge). The lunge step leaves five fading afterimages of you (`KatanaGhosts`, drawn from where the lunge started), and your own cuts shake the screen (lunge 0.14, swipe 0.07 trauma, `Juice.cs`; each swipe that cuts something also holds the picture 0.03 s, with its own 0.15 s gap so a chain gets one each; off with Screen shake off or Reduced effects). The blade curves toward the way it sweeps, so it flips with each swipe (`DrawMeleeModel` `bend`; the lunge's blade stays straight). Everything in the mockup is built. The Alt strike keeps its rose look. Check in Unity: the crescent's thickness at 115 px against the player, the streaks on a busy screen, Reduced effects, melee grace and bosses at the longer reach, and whether 115 / 300 is enough. The Armory and Bestiary pages are out of date for the Katana's reach.
- [x] **Katana feel pass: Steel sounds, lunge hit-pause, kill preview (built, Nov 2026, protocol 194; check in Unity).** The Steel set of [`Mockups/katana-sounds.html`](Mockups/katana-sounds.html) is in the game (`Game/KatanaSoundBank.cs`, 21 new `Sfx` values): swipes, lunge wind-up and thrust, a cut landing, meter full, no charge, aim, target change, stance, cut (and the empowered one), sheathe, blow, chain 1-5, empowered ready, streak lost. Your own lunge holds the picture 0.03 s (+0.01 per extra enemy cut, up to 3) when it hits (`KatanaLungeHold`, `Juice.cs`). While aiming, enemies the strike would kill have rose brackets and the rest faint white ones; the reticle is grey on a target that would live (`Game.KatanaWouldKill`: health against 120/240 times your damage; Mark for Death, armour and boss stages aren't counted). Details in [`KatanaCombo.md`](KatanaCombo.md). Needs a listen and a try in Unity: volumes against gunfire (the swipe ring and cut thud play every 0.26 s), whether the 0.4 s delay on the chain chimes feels right, the hit-pause length, and whether the faint white brackets read. Armory page for the Katana is out of date.
- [x] **Melee builds eat contact damage (built, Nov 2026, no protocol change; check in Unity).** Owner's feedback: the Katana (and melee in general) can't stay close enough to hit without being touched, worst on bosses. Now any melee swing that hits something gives 0.3 s of no contact damage (`Player.MeleeGraceT`, `Balance.MeleeContactGrace`, set in `LandSwing`, checked in the contact loop in `EnemyAI.cs`). Not built, if it's still rough: a longer Katana reach (95 px), a Katana lunge that ends short of a boss, or contact damage halved for melee weapons. Say if 0.3 s is too little or too much.
- [x] **Enemy roles: the late wave recipe and three enemies that don't shoot (built, Nov 2026, protocol 193; check in Unity).** From [`EnemyRoles.md`](EnemyRoles.md) and [`Mockups/non-shooter-enemies.html`](Mockups/non-shooter-enemies.html) (https://claude.ai/artifact/A9YbhUEfw8gXo2Ftz8csJw); I read "a, b, c" as Fuse Runner A, Hookman B, Sapper C. **Wave recipe** (`WaveRecipe.cs`, wave 13+): job shares (Pressure 45, Hunter 25, Zoner 12, Disruptor 8, Anchor 10), 2-3 headliners a wave, old types and squads retiring, squads half the wave; `wavemix` shows waves 14-39 at about 55% shooters (was 81-91%). **Fuse Runner** (wave 15, pairs): plants within 70 px, 0.8 s fuse, 90 px blast for 35 that hurts enemies too; shot during the fuse it blows at once. **Hookman** (17): 0.5 s blinking line, harpoon 320 px, yanks you 220 px (no damage); dodge, parry or a shot across the cable stops it; a miss leaves it open 1 s (+50%). **Sapper** (18): Tripwire Pods every 1.6 s (4 max), arm in 1 s, lasers between armed pods within 180 px, stepping close or crossing a laser: 0.3 s beep then 70 px blast for 28 (enemies too); shoot a pod to set it off; pods don't hold the wave open. New squads: Demolition (15), Whalers (17), Minefield (18). Numbers are first guesses. Check in Unity: the yank's feel (it's the Riot Shield's knockback), whether pods are readable in a busy fight, Fuse Runner pairs at 200 px/s, the headliner mix. Bots steer clear of a lit fuse, the Hookman's line, armed pods and their lasers (`BotBrain.cs`). Not built: the section 2 reworks (Charger, Phantom, Mortar, Scorcher, Splitter) and the other five new enemies. The Bestiary and Armory have no entries for the three (the field guide test skips them, as for the Spike Block).
- [x] **White blob on burning / heavily hit enemies (fixed, Nov 2026, protocol 192; check in Unity).** Burning, bleeding and poison no longer flash an enemy white every frame (the flash and the bulge now need a real hit; the snapshot sends a hit flag instead of flashing on any health drop). Steady fire softens the flash (`Enemy.FlashHeat`: down to a 30% tint, never past 70% on a boss) and the bulge (a third as big on bosses). Explosive and Collapse Rounds show at most one blast every 8 ticks on one spot (damage unchanged, `SplashFxNearby`), impact sparks thin out where hits pile up (`ImpactCrowd`), and enemy bullets now draw on top of every effect and glow (`DrawEnemyShots`). Check in Unity: a trio on a boss with Incendiary + Explosive Rounds, whether 30% flash under fire still reads as "I'm hitting it", and the bulge on bosses. Test: `run.sh 1 1 hitfx`.
- [x] **Tab screen as hologram panels (built, look B, no protocol change; check in Unity).** `TabHolo.cs` (new), `RunStatsUi.cs`, `HudTab.cs`: look B of [`Mockups/crt-tab-menu.html`](Mockups/crt-tab-menu.html) (https://claude.ai/artifact/A71BMR6JXvBph7kLERPhCW). Panels projected over the arena (`TabDark` 0.42) in the CRT colour: the level-up cards' Projection glass (`CardArt.Holo`, see-through) with scanlines, a drifting band, a magenta / cyan fringe and two dips every 5 s, the text drawn over those; a beam strip along the bottom with a faint cone; the panels switch on from a line when the screen opens and when the page changes (replaces the zoom-through). YOUR STATS down the left; Run so far (header with played / score / next boss tiles, eight total tiles, route, your fight and the party's share), Your build (header, weapon and abilities with MOVE TO keys, upgrades by rarity), Team (a panel per teammate with the live model on a hatched island). Tab keys are lobby keys; from the pause menu an X key closes it. The route's notes are shorter ("now · 3 of 4", "in 2 waves"). Check in Unity: the CRT font's widths (the preview's stand-in is wider: the route, the ability tiles' top row, the header's next-boss tile), the switch-on (GUI.matrix squash) with the live models, scrolling the upgrades, moving an ability, the tooltips over the glass, each CRT colour, Reduced effects, and opening it from the pause menu.
- [x] **Hologram health orbs (built, look C, Nov 2026, no protocol change; check in Unity).** `Game/OrbLook.cs` `DrawHoloOrb` from [`Mockups/digital-health-orbs.html`](Mockups/digital-health-orbs.html) (https://claude.ai/artifact/UFKb8r1pz6gDDjqVVV5kET): see-through wireframe sphere with scrolling latitude rings, a turning meridian, a white plus that glitches sideways now and then, a ring of ticks (not on tiny orbs) and the gold ring on big ones. Size, pulse and last-3-second blink are unchanged; Medkit orbs keep their own look. Pickup: the orb pops into square data bits that flicker away, a hex ring spreads (gold too for big) and a white plus rises off the player (`DataBurst`, particles 12-14, replaces the old green ring; Visual effects on only). Not built: following the floor colour (open question in the mockup). Armory page is not affected; Bestiary neither.
- [x] **Disk eject key on the menu's drive (built, key A + spring pop B, no protocol change; check in Unity).** `DiskEject.cs` from [`Mockups/disk-eject.html`](Mockups/disk-eject.html) (https://claude.ai/artifact/JNExXV67VaMoBeQQ5VW3AW). It replaces the "Switch profile" tab. Check: the key's hover, tooltip and press, the drain and the spring kick (the disk is drawn in a `GUI.BeginGroup` so only the part below the slit shows; verify the clip works with the IMGUI matrix), the fly to the cradle and the clamps closing, the sounds (`DiskClick`, `DiskEject`, `DiskFly`, `DiskLift`), skipping with a key, Reduced effects, that nothing clicks meanwhile.
- [x] **UI bug fixes from the UI audit (Nov 2026, no protocol change; check in Unity).** Mastery toasts ("Barrier · Mastery 3") now last their full 3 s: they were counted down on every OnGUI event, not once a frame (`Hud.cs` `DrawMasteryToasts`). A Wide Choice offer's 4th and 5th level-up cards now fly away as the tall cards they were drawn as (`PickOutro.cs` uses `OfferMod` like the picker), and the picker's backdrop no longer fades out while a challenge prize pick is still waiting. The HUD mode (H, or Settings > Gameplay) is now remembered between launches (`hud_mode`). The Wave cleared box's panels and dots now fade in with its text (`Ui.Alpha`). Check: level an ability to see a toast, take a pick from a Wide Choice offer, pick a HUD mode and restart, watch the Wave cleared box drop in.
- [x] **Wave cleared box in the CRT style (built, look A, no protocol change; check in Unity).** `WaveClearedUi.cs`: look A ("One tube") of [`Mockups/crt-wave-cleared.html`](Mockups/crt-wave-cleared.html) (https://claude.ai/artifact/B2qrLd1kKF6usdi9E93XgR): one CRT tube where the old card was (`RoundTube`, over a light dark layer, `WaveClearedDark` 0.35), in the CRT colour setting. Title in the CRT font with a blinking cursor (gold after a boss), clear time and score tiles, a row per player (yours lit like a selected key), award tags in inverse video (gold; Most healing and Reviver in the ink), a ready box (tick, A, or dashed), a dashed Squad row, the next wave in a dashed frame with the road to the boss, the lobby's Ready key (green with a tick when ready; a dim dashed AUTO-READY key with Auto-ready on), the Settings ON/OFF switch for Auto-ready (its label clicks too), the status in CRT caps and a segmented countdown. Reduced effects: no drop-in, glow, cursor blink or sweep. Check in Unity: the CRT font's widths (the preview's stand-in font is wider, so the status line and long award tags may have more room than the shots show; "MOST DAMAGE" + "MOST KILLS" should both fit), the tick glyph in the ready box, three players, each CRT colour, clicking Auto-ready by its label. Preview: `WAVECLEARED=1|auto|boss [WCT=<s>] Tools/Preview/shot.sh x hud 4 12.2 1`.
- [x] **Pushable crates and barrels (built, Nov 2026, no protocol change).** `Props.cs` `ShoveProps`/`PropDir`; mockup [`Mockups/pushable-props.html`](Mockups/pushable-props.html) (https://claude.ai/artifact/3f9V85K5pwwA4KG3K3jG7t), look B (scuff trail: two drag lines that fade over 3 s, `Prop.Scuffs`) chosen and built; A and C not used. Check in Unity: the scuff lines (width, alpha, Reduced effects), feel of the push speeds, a guest pushing on the host (the host sees them lean via smoothed motion), props against pillars and gates, enemies steering round crates. Numbers are first guesses.
- [x] **CRT looks for the partial HUD modes (built, Nov 2026, no protocol change).** Corner text is now look A (Phosphor Corners) and On the player look E (Callout Tag) of [`Mockups/crt-hud-modes.html`](Mockups/crt-hud-modes.html) (https://claude.ai/artifact/SqvMySBnczAodyFYWvHqCN); B, C, D and F were not chosen. Fixes: every number sits by its key, the ult shows its charge % (it showed seconds left), health is in the CRT colour (red and blinking under 25%), the wave sits beside enemies left. All in `Game/HudModes.cs`. Check in Unity: VT323 sizes against the widths (the preview uses another font), the tag switching sides near the left wall and the top, the tag hiding while you're downed, the other CRT colours, Reduced effects (no blink).
- [x] **HUD modes on H (built, Nov 2026, no protocol change).** H now cycles Full > Corner text > On the player > Hidden (`Game/HudModes.cs`; Settings > Gameplay has a four-key choice in place of the "Show the HUD" switch). Looks A and C of [`Mockups/hud-modes.html`](Mockups/hud-modes.html) (https://claude.ai/artifact/S6fHJiXUpS6rE6mpNU2Eft); B and D were not chosen. Both partial modes keep health, ability cooldown numbers (ready = the key alone) and enemies left; boss slim bars stay; teammate rows, wave/score, XP and chips go. "On the player" hides your own name tag (the health number sits there) and draws a three-quarter health arc. Check in Unity: text sizes in the real font, the arc against different player sizes, the fade between modes, the "Press H" reminder only showing in Hidden, a downed player in each mode, Reduced effects. Preview: `HUDMODE=0..3 Tools/Preview/shot.sh x hud`.
- [x] **Disk drive replaces the menu's x-axis; floppy slides in on picking a profile; the arc is a tapered stroke (built Nov 2026, no protocol change).** `DriveSlot.cs`, `ProfileTransition.cs` (now 5.4 s), `Menus.cs` (`DrawMenuArc`), from [`Mockups/floppy-slot.html`](Mockups/floppy-slot.html) (slot B, arc 3). New sounds `Sfx.Disk*`. Check in Unity: the disk's tip and slide into the slit (lip drawn over it), the readout text on the picker (it sits just above the shelf wall, which moved down 22 px), the drive on the menu with your name, the way back with Switch profile (the disk pops out), the sounds' mix, Reduced effects, and the move into the lobby and back (the drive fades where the axis was). The menu's title is still VECTOR COLOSSEUM (the mockup said Cartesian). The lobby's own axis is untouched; say if the lobby should lose it too.
- [x] **Earthquake is now the Burrower (built Nov 2026, protocol 189).** `Burrower.cs`, `Game/BurrowerLook.cs`, from the Burrower Visuals mockup (look A, dirt mound). Check in Unity: the telegraph cracks at the cursor, the breach (rock, dust, head with mandibles), the moving mound with its scar and dust, the snap-up on each bite (`FxKind.BurrowSnap`), the sink; Reduced effects (drops rock, puffs, dust rings); a guest's view. Not built: the screen shake on the breach, the Bestiary/Armory entries (out of date: the old Earthquake text), the Segmented worm and Armored ridge looks. Numbers are first guesses: slam 25, bite 30, run 3 s at 190 px/s, 10 s cooldown.
- [x] **Run results as CRT tubes (built, Nov 2026, no protocol change).** From [`Mockups/crt-run-failed.html`](Mockups/crt-run-failed.html), layout B (Rack) in Blue, with the main menu's keycaps in a row along the bottom. `HudSummary.cs`: a tube for the title, four for the totals, one per player (two or three players share the left column, narrower and without bars), and on the right the run's **damage per second** and **health** charts from the Recent runs page (`DrawDpsChart`, `DrawHealthChart`; `ChartTip` now takes `chartTipBounds`). The rank strip keeps its old look under the players; the rank report page is unchanged. A **Boss Hunt** now records a curve so its results graph too (`RunSaves.cs`; practice runs show "PRACTICE RUNS AREN'T GRAPHED"). Check in Unity: the ending animation (title gliding into its tube, tubes coming in, charts appearing once the tubes are in), VT323 row sizes (the preview's stand-in font is wider), hover tips on the charts, 2 and 3 players, Reduced effects, a guest's "Waiting for the host", the endless key after a win. The Run log tube in the mockup (first kill, ability used) was replaced by the charts; nothing new is tracked.
- [x] **Golem Wall, Basalt look (built Nov 2026, no protocol change).** `GolemWallLook.cs`, `Minions.cs` (no timer, replaced by a recast, wears from every shot), `Kits.cs` (Basalt Golem: radius 38, block radius 46, 200 health). Check in Unity: the golems' size against the player and walls, the rise, ember veins at each wear stage, the rubble, a guest's view (rise is drawn from when it is first seen), five golems at the capstone, whether 200 health feels right on late waves. Mockup: `Docs/Mockups/golem-wall-rework.html`. The Armory and Bestiary pages are out of date for this ability.
- [x] **Boss Hunt tab, Dossier layout (built Nov 2026, no protocol change).** `HuntUi.cs`: list of the ten bosses with status tags, a target scope with the live model, wave ladder, record tiles, rules. Check in Unity: row spacing at small window sizes, preview clipping in the scope, a guest's view, locked / coming-soon rows. Mockup: the Boss Hunt Tab Rework canvas (direction A). **Scrolling drum added (Nov 2026, no protocol change):** the list loops forever with the picked boss on top; wheel / PREV / NEXT / click. Check: the ease feels right, rows fade at both ends, the previews fade with them, START greys out while the top boss is locked, a guest's drum follows the host's pick.
- [x] **Difficulty tab in the CRT style (built, Nov 2026, no protocol change).** `DifficultyUi.cs`: the five levels are ink keys (inverse video when picked, skulls keep their colour), numbers have dim-ink names, phosphor sliders (`Ui.CrtSlider`, a gold tick at Normal) and framed text boxes, and the Blend / Layouts choices are `LobbyKit.Key`s. Check in Unity: dragging the sliders, typing in a box, a guest's read-only view, Reduced effects.
- [x] **Lobby declutter (built, Nov 2026, no protocol change).** From [`Mockups/lobby-declutter.html`](Mockups/lobby-declutter.html) (https://claude.ai/artifact/S5kKR6ctQmCUG5YTwGYh4h), title style B. Smaller text across the lobby (title, mode and tab keys, party cards, loadout keys). The loadout: the preview fills the left column (`ShowcaseRect` is now 262 x 404, so the Hangout grow starts from a taller window), under it the role / class title in look B (`DrawRoleClassTitle`: the role small in its colour with a fading rule, the class big, a short glowing rule; a Wildcard shows "ROLE" over "WILDCARD") and glowing stat bars (`LobbyKit.StatBar`: lit top, halo, bright end cap, slow pulse; flat with Reduced effects). Right column: Role, Class, Weapon + Ability, a new **Ultimate** row (the ultimate key in gold, three locked slots; click it to preview, as the old passive box did), then **one detail box** for whichever weapon, ability or ultimate was clicked last (`detailSel`, `DrawDetailBox`). The right quarter of each weapon key is an **ascension button** (rank mark over 0/3) that opens an ascension window over the panel (`ascOpen`, `DrawAscensionWindow`: three locked tiers, placeholder text, any click closes it); the old ascension row is gone. The old `DrawUltBox`, `DrawAscensionStrip` and `DrawSpecSheet` are deleted. Needs a try in Unity: key text at the smaller sizes in the real font ("CAVALRY CALL" and "WARHAMMER" shrink to the minimum), the taller preview's zoom and the Hangout grow, the Wildcard picks (their rows are about 4 px shorter), Esc does not close the ascension window (click does), and the ascension text is placeholder. Preview: `Tools/Preview/shot.sh x lobby`.
- [x] **Floating CRT cabinet, soft edges, quiet tabs (built, Nov 2026, protocol unchanged; check in Unity).** The pause menu, lobby Esc menu, Quit question, Settings, How to play and Profile cabinets now follow the "Floating cabinet", "Soft edges", "Light keys" and "Quiet tabs" toggles of [`Mockups/crt-pause-menu.html`](Mockups/crt-pause-menu.html) and [`Mockups/crt-profile.html`](Mockups/crt-profile.html). `CrtKit.Cabinet` no longer draws the edge-light strip, the comb or the opening's light sequence; it draws a glow, a rounded body with a 1 px blue outline, an inner shadow and 20 px bracket corners (top-left, bottom-right). The cabinet floats 24 px from the left, top and bottom (outer 410 x 672); everything inside is drawn in a group at its inner corner (`CabX/CabY/CabW/CabH` in `MenuDrawer.cs`), the slide uses `DrawerSlideW`, the side window is unchanged. Soft edges (`CrtKit.SoftEdges`, called from `ArenaApp.OnGUI` through `DrawCabEdges`, under the cursor): about 190 px of black falloff plus a 7 px rim and a 2 px line, fading with the menu, static also with Reduced effects. The profile: header 78 px, profile screen 126, option list 312 (rows 40), Back key 36 px; quiet tabs (`QuietTab` in `ProfileUi.cs`: 12 px bold caps at 48% ink, 85% on hover, the open one a lit pill; no screen behind them; no letter-spacing, IMGUI has none). Light keys: the pencil key and the Look window's EQUIPPED key were still heavy fills, now soft. Try in Unity: that the soft edges' rim and rounded cabinet corners look right at other resolutions and do not hide HUD you need (the arena dim stays), the Back key's well at the cabinet's bottom (36 px profile / 44 px others, its lower edge touches the cabinet's inner edge), that nothing clips in the inner group, the tab pill's size (32 px high here), and the glow near the screen's left edge. Left out: the rim's 30 px rounded corners (square here), letter-spacing. Previews: as the entries below.
- [x] **Profile as a CRT cabinet (built, Nov 2026, protocol unchanged; check in Unity).** Built from [`Mockups/crt-profile.html`](Mockups/crt-profile.html) (https://claude.ai/artifact/83nBL7SXAp58nDdWW2p8oE) in `Game/ProfileUi.cs` (cabinet), `CosmeticsUi.cs` (Look windows), `ProfileWindows.cs` (Rank and Unlocks windows), on the pause menu's machinery (`CrtWindows.cs`: `BeginDrawer`, `DrawerRow`, `OpenCabWindow(3)`, `WinKey`, `WinText`; the only change there is that `DrawCabWindow` draws the new window kind 3). Done: the "PROFILE_" header with boot log, a profile screen (rank badge with a pulsing glow, your name with a digital pencil key that still renames, "Title · Rank n" in the tier colour, XP bar and the real XP numbers), four tab keys, the tab's options as keys with the game's icons (Look shows what is equipped, Records a count, Unlocks "n of m"), a physical Back key. Each option opens in a CRT window that slides out and powers on the first time and only retypes after; its X key, Back and Esc close it first, then the profile (also in the pause and lobby menus, without Look). Switching tab retypes the list and swaps an open window to the tab's first option. Look: Frame, Trail, Background, Card background, Name effect, Aim line, Cursor, each with TRY IT ON (the old live previews, now clipped to the glass; Name effect and Card background show a big name), HOW OTHERS SEE YOU (`DrawLookCard`) and a list of nine rows at a time (wheel or drag the bar to scroll, the equipped row is scrolled into view): thumbnail, name, status, EQUIP key (gold EQUIPPED, dashed padlock "RANK n" key). Hovering a row, locked or not, tries it on in the preview and on the card; EQUIP keeps the old save path (`EquipCosmetic`). Rank: Progress (rank, title, XP, a block bar, the next five rewards), Reward ladder (the old ladder, restyled), Tiers (the five titles with their badges and rank ranges, you marked), XP sources (the real numbers from `Rank.cs`). Records: Overview, Bosses, Classes, Challenge rooms, Recent runs, the old pages in the window (Recent runs' arrows and tabs are digital keys now). Unlocks: a window per cosmetic kind (Frame ... Cursor, Emotes) with a check or padlock on each, plus the run-feat unlocks (`Unlocks.All`). Left out / changed: the old "Rank and runs" page is gone (its recent runs are in Records > Recent runs; it also had Equip buttons, equipping is now only in Look); long cosmetic names drop the kind's word ("Chevron Flow" for "Chevron Flow aim line") before they wrap; the badge is the game's `RankLook` art with a glow, not the mockup's drawn sheen; the Records and ladder pages keep their own insides (cards, graphs) in the new window; no Look tab inside a match, as before. Try in Unity: that the Look previews (GL) stay inside the glass and slide with the window, the Back / Esc order (window, then profile), renaming (pencil, Enter, Esc), hover try-on on every kind, scrolling the Name effect and Cursor lists, the opening from the main menu card and from the pause menu (it plays once, no replay when you pick or switch tabs), Reduced effects (no glow or typing), and font widths in the tab keys ("RECORDS", "UNLOCKS") and the "RANK 55" keys. Previews: `Tools/Preview/shot.sh x profile` with `TAB=0..3`, `SECTION=n` (the option), `RANK=n`, `TRAIL=id`, `MOUSE=x,y` (hover), `NOWIN=1`, `WINT=<s>`, `WINWIN=<s>`, `REDUCED=1`; `PAUSERECORDS=1 TAB=1 ... pause` for the in-match one.
- [x] **Lobby as one digital CRT screen (built, Nov 2026, no protocol change).** From look A of [`Mockups/crt-lobby-digital.html`](Mockups/crt-lobby-digital.html). The lobby is one blue phosphor screen (`CrtUi.Ink`; the New run boot now grows and settles on blue, so nothing changes colour at the hand-over) and every option is a digital key (`LobbyKit.Key`, `CrtKit.Frame`/`Dashed`): Back, `LOBBY_` (the underscore blinks), `!`, the three mode keys with their descriptions, Hangout, + Add a bot (and the bot cards' skill / MIX / x keys), Ready, Start (dim and dashed until it can start), the tab keys, role keys (`role_*` icons), class keys (`class_*` icon above the name), weapon, ability and the three ascension keys; locked slots are dashed with the real `ui_lock`. Long names shrink, then wrap, then are cut ("Hemomancer Healer", "Transfusion Darts"). Colour stays in the party cards (rim, frames, backgrounds, portraits, tags), the live preview, the stat bars, the passive ring and the icons. The ability cooldown tile (and charges / HP cost) sits at the bottom of the ability half of the detail card, beside the weapon's tiles. The Wildcard role keeps its picks (a row of weapon and ability keys per class, led by the class icon). The "!" popup, the locked tip, the Resume banner and the Seats tab are in the same style; the Perks, Difficulty, Modifiers, Boss Hunt and Practice bodies keep their own insides, in the new framed screen. Left out: no key text typing and no power-on of the lobby (the CRT boot and the entrance already open it), no cooldown ring on the ability tile (the tile text is bigger instead), the tab bodies are not turned into keys (they were placeholders in the mockup). Check in Unity: that the key text sizes look right in the real font (the preview measures with Arial-like widths, but Unity's text may differ a little), the New run boot into the blue lobby and the Esc-menu over it, Reduced effects (no glow, calm), the Hangout transition, a bot's card keys and its loadout (name box over the preview), and the Wildcard picks at 6 and 8 classes. Previews: `BOTS=1` / `BOTS=edit` in `lobby` mode adds a bot seat. Looks B and C were not chosen and are not saved. **Softer lobby (built, from [`Mockups/crt-lobby-softer.html`](Mockups/crt-lobby-softer.html), no protocol change):** (1) *Role gradient*: the loadout box and the Perks, Difficulty, Modifiers, Boss Hunt, Practice and Seats bodies (`LobbyKit.Panel`, `LobbyKit.Wash`) get a wash in the shown role's colour (a bot's while the host edits it): a dark diagonal from about 20% of the colour to near black plus a 32% glow from the top left, in 36 fills of exactly meeting strips; the rim leans toward the colour; the colour eases over about 0.7 s (`LobbyKit.WashTo`, snaps with Reduced effects). The strips approximate the mockup's ellipse with nested rectangles, so look at the glow's shape in Unity. (2) *Margin and soft edges*: nothing is nearer than 36 px (party column x=36..412, big box 440..1244 by 70..684, tabs at y=32), the 14 px lift is gone, and the glass has a 190 px falloff (`CrtEdgeShade`, up to 46% black at the edge) so content near the edge dims; the entrance, leave, return and launch slides and the card ring are re-tiled (`LobbySplit` 426). (3) *Quiet tabs* (`LobbyKit.QuietTab`): 12 px letter-spaced caps at 48% ink, 85% on hover, the open one a lit 30 px pill. (4) *Compact party column*: 52 px back key, 46 px `LOBBY_`, mode keys 52 px with their description under the name, 13 px Party line, 104 x 26 Hangout, party cards 84 (the first) and 80 px with 64 px portraits and 10 px tags, bot keys, open slots 80 px, Ready 48 and Start 44 px. Check in Unity: the real Rajdhani widths in the tabs, tags, mode descriptions and long names; the glow's shape and banding on a real screen; the edge falloff over the Hangout and Practice screens (is 46% at the edge too dark?); the slides; Reduced effects; hover and click on every tab. Previews: `LOBBYIN=0.9`, `LOBBYOUT=0.4`, `RETURN=0.6`, `LOADOUT=1,5,0,0` (a long name), `BOTS=edit`.
- [x] **Pause and Settings as a CRT cabinet (built, check in Unity).** Built from [`Mockups/crt-pause-menu.html`](Mockups/crt-pause-menu.html) in `Game/CrtWindows.cs` (cabinet, window, digital controls), `MenuDrawer.cs`, `KeybindsUi.cs`, `WindowAnim.cs`. Done: the pause menu, the lobby's Esc menu and the Quit question as the cabinet (header screen, option screen, Esc tag, red Quit); Settings (list switches to the four sections + physical Back key; main menu, lobby and pause); How to play (a window with topic tabs beside the pause/lobby list; from the main menu the cabinet lists the topics); each section a CRT window that slides out of the cabinet and powers on once, then only retypes; digital ON/OFF keys, segment keys, blocky sliders (- / + and drag, 5% steps), keybind keys ("PRESS KEY_"); the opening plays once (boot log, power-on, scan bar, keys typing) and stops after 3.5 s; Reduced effects / Visual effects off: no animation. `BeginDrawer`, `DrawerRow`, `SwitchRow`, `OpenSectionPanel`, `BeginPanelAnim` now produce the CRT look for the profile too (its panel powers on in place, no slide). Left out / changed: the old party-and-run-card drawer is now a "this run" CRT screen on the right that fades while a window is out; I added a small X key on windows and the profile; Settings opens with no window until you pick a section (as in the mockup); the Audio "Music player" tab, the Records pages and the profile's Look tab keep their old widgets inside the CRT window; no sounds were changed. Run stats still opens the Tab screen. Try in Unity: typed-text timing and glow, the key sizes and font widths (previews use stub text widths: the title cursor block and tab widths may sit differently), hover and click on a window while it slides, dragging the volume bars, Esc order (window, then list, then resume), the "Paused by X" / "Pausing..." sub-line, reduced effects, and the Quit question. Previews: `PAUSEAGE=<s>`, `PAUSESET=<0-3|list>`, `PAUSEHOW=<topic>`, `PAUSERECORDS=1`, `PAUSECONFIRM=1` with `Tools/Preview/shot.sh x pause`; `LOBBYMENU=1|settings|howto ... lobby`; the `settings` mode opens a window (`NOWIN=1` for just the list, `WINWIN=<s>`).
- [x] **Vein Tithe (replaces Blood Rite).** Built (Nov 2026, protocol 186) from [`Mockups/blood-rite-vein-tithe.html`](Mockups/blood-rite-vein-tithe.html) (https://claude.ai/artifact/7spUdvW2AiYZmuFq4VhZ5B). The tank Hemomancer's ultimate holds every enemy on screen for 30 s (can't move or shoot) for 0.25% of max health a second each (bosses 4x), and you deal 40% less; mastered is 40 s and 25% cheaper. Check in Unity: the artery and heart look (`Game/VeinTitheLook.cs`), whether the drain is right on a real tank (240 HP: 10 enemies cost 6 HP a second), whether bosses should be held at all, and that enemy bullets already in the air keep flying (they are not frozen). Unchanged from the mockup: enemies that spawn later aren't held.
- [x] **Wildcard role (4th role).** Built (Nov 2026, protocol 187) from [`Wildcard.md`](Wildcard.md). Unlocks after a run win as Tank, Healer and DPS (co-op counts, `Profile.WildcardUnlocked`); until then its role card is a padlock ("Win a run as a Tank, a Healer and a DPS to unlock") in the lobby and so also in the Practice Range. Pick any class's DPS weapon and DPS Space ability, plus a second DPS ability on Q, E or Shift (the lobby's Gear panel; the Tab menu moves it later). Same 5 ability slots. 130 HP, no passive, 0.9x damage and 1.25x ability cooldowns (`WildcardDmgMult`, `WildcardCdMult`, first guesses). Ultimate = the first ability's class's DPS ultimate. Hemomancer gear has its health cost but no Blood Price bonus. New `Msg.WildcardRequest`, `LobbyEntry.Wild`, `Player.StartBonus/WildKit`. Bots keep Tank/Healer/DPS unless you pick Wildcard for one by hand. Needs a try in Unity: the 6x3 Gear grid is tight (names at size 10 may clip), the tabs/keys strip, spec sheet height. Left out: its own role icon (borrows the DPS one), its own model (DPS build in purple trim), cards that need the second ability (only the first counts for `RequiresAbility`), a Wildcard lobby preview of the second ability. Armory/Bestiary untouched.
- [x] **Sanguine Rain rework (channel with a drain).** Built (Nov 2026, protocol 185) from [`Mockups/sanguine-rain.html`](Mockups/sanguine-rain.html) (https://claude.ai/artifact/4qb2DpM8A2Eiox3WrHDefs). The healer Hemomancer's ultimate is no longer lifesteal-for-allies: a 1.8 s siphon pulls 12% of the caster's max health into an orb (ribbons and streaking drops), the orb bursts and beads of blood arc to each ally, then a rain circle (80 px) trails each ally for 10 s (mastered 14 s) and heals them 3% of their max health every 0.5 s (mastered 4%) wherever they are. It drains the caster 3% of max health a second (blood beads flow down a tether from them), ends when the ability key is pressed again (no refund; the 80 s cooldown started at the cast), when the time is up, or at 15% health (`Core/SanguineRain.cs`, replicated `Player.RainClock`, `UltT` holds the time left; new `FxKind.RainRelease`, `Game/SanguineRainLook.cs`). Numbers are first guesses (`Rain*` constants). Alone, it just costs health. The old 20% damage lifesteal and its drops are gone. Needs a try in Unity (ring size 80 px, drop density with a full party, the orb height, whether 12% + 30% total cost is too much). Left out of the mockup: heal numbers over allies (their health bars show it) and the red screen vignette. Preview: `RAIN=<s> CASTCLEAN=1 Tools/Preview/shot.sh x hud`. Armory page for Sanguine Rain is out of date.
- [x] **Titan: Colossus (Runestone).** Built (Nov 2026, protocol 184): look C of [`Mockups/titan-colossus.html`](Mockups/titan-colossus.html) (https://claude.ai/artifact/K5urUkLLfurpzwtnRskZsU), 2 s longer than the mockup (17 s, capstone 27 s). The Titan minion is now 140 px (was 32), walks at 75 px/s, rises for 1.4 s (no block, no hits), then slams the nearest enemy after a 0.4 s wind-up: 90 damage, 190 px reach, every 1.4 s, 60 px knock-back (not bosses or Heavy). Its whole 140 px body blocks bullets (soak 1000, capstone 1.3x and 1.3x size). The last 1.2 s it crumbles into rubble. `Core/Titan.cs`, `Game/TitanLook.cs`; new `FxKind.TitanSlam` and replicated `Minion.Wind`; `Titan` colour is now cyan. Numbers are first guesses. Needs a try in Unity (body size against the arena, 140 px blocking, the wind-up telegraph, whether it gets stuck on walls). The lobby preview (`Showcase.cs`) waits 17 s + crumble between Titan casts and starts zoomed out. Left out of the mockup: the ability icon and the HUD timer/soak strip (the existing ability card and the bar over the titan stand in), the enemy-side collision (enemies can walk through it). The Armory page for Titan is out of date.
- [x] **Bo Staff rework (slash, jab, spin chain + shove).** Built (Nov 2026, protocol 183) from [`Mockups/bo-staff-rework.html`](Mockups/bo-staff-rework.html) (https://claude.ai/artifact/6JyEbi86yHR5gVFLorp8vP). Primary is a chain with **no timer** (`Player.BoStep`, replicated): cone slash (130°, 105 px, 16), jab (140 px line, 28 wide, 22), full spin (100 px, 30), then round again; holding the button plays it on by itself, and each step holds the next off for 0.32 / 0.30 / 0.80 s (the spin's includes its recovery). Every swing cuts small bullets (the mockup left that open; say if only the spin should). The weapon-action key now **shoves** instead of vaulting (the vault, its constants and its test are gone): a 100 px wide, 78 px deep bar pushes everything in it 70 px straight ahead for 6 damage (not bosses or heavies, which still take the damage), once a second, lands 0.1 s after the press, and starts the chain over. Extra-projectile upgrade: +20 px reach and +15% damage (`BoExtraDmg`) on every swing and the shove, +20 px on the push (reach alone failed the 'every weapon gets stronger' test). The swings use the existing glowing melee trail (jab as a thrust); new `FxKind.StaffShove` (`Game/BoStaffLook.cs`: staff pulled back, driven out, pressure arcs) and three pips over the player (`DrawBoStaffPips`). `Core/BoStaff.cs` holds the shove. Numbers are first guesses (`BoStepTime`, `BoShove*` in `Ninja.cs`). Needs a try in Unity: how the jab's reach and the spin's recovery feel held down, pip size, the shove's look next to the player model. The lobby's loadout preview plays the chain and presses Alt every 5.2 s, 3.3 s in, so the third chain is cut short by a shove (`Showcase.cs`); the stats show 2.1 swings/s (`FireInterval` 0.47, the chain's average step) and 48 damage a second (measured; only that line of `WeaponDps.Generated.cs` was updated). Left out of the mockup: the dummy-enemy walk-back, hit-stop. Raising the parry also resets the Bo Staff's chain to the slash and cancels a shove still drawing back (`ResetAttackChains`, `Katana.cs`). Armory page for the Bo Staff is out of date.
- [x] **Crimson Harvest (replaces Crimson Apotheosis).** Built (Nov 2026, protocol 182): the tendrils of look B (Whirlpool) and the ball of look A (Membrane) from [`Mockups/hemomancer-ult-rework.html`](Mockups/hemomancer-ult-rework.html) (https://claude.ai/artifact/UW8t3r8eW7qMWv53xbCAq5). The DPS Hemomancer's ultimate (`AbilityId.CrimsonApotheosis` kept, 65 s) now rips 20 damage and blood out of every enemy into a ball over you (Grunt 1 blood, Heavy 3, boss 8), holds it up to 5 s (mastered 8 s and 25% more blood) while you move at 60% speed, and a second press of the ability, or the timer, throws it at your cursor (up to 900 px): 60 + 12 per blood at the core, half at the edge of a 150 + 50·sqrt(blood) px blast, enemies flung (not bosses) and slowed 35% for 4.5 s (`CrimsonHarvest.cs`, `Game/CrimsonHarvestLook.cs`, new `FxKind.HarvestThrow`, replicated `Player.HarvestClock/HarvestBlood/HarvestHold`). I decided the open questions: the 1 HP floor and the doubled Blood Price are gone (Blood Price is the normal +40% and applies to the rip and the blast), the mockup's click to throw is the ability key pressed again, bosses give 8 blood, and nothing else adds blood. Numbers are first guesses. Needs a try in Unity (ball size against the arena, tendril density with 40 enemies, whether the aim ring reads, whether 5 s of hold feels right). Left out of the mockup: the HUD blood and timer bars (the amber ring round the ball is the timer), the ability tile icon, the floor stain's pale edge, and other players don't see your aim ring. Bots throw when the timer runs out. Armory page for Crimson Apotheosis is out of date. Preview: `HARVEST=<s since cast> [HARVESTBLOOD=26] Tools/Preview/shot.sh x hud`, or `HARVESTTHROW=<s since the throw>`.
- [x] **Dodge (default tiny dash).** Built (Nov 2026, protocol 180): Ctrl (rebindable, Settings → Keybinds → Movement) or a double tap of a movement key (0.25 s window, WASD or arrows) dashes 95 px in 0.11 s with 0.22 s of invulnerability (`Player.InvulnT`, so bullets and most hazards pass through), 1.1 s cooldown; with no direction held it steps back from your aim (`Core/Dodge.cs`). Your game moves you and sends the new `Msg.Dodge` so the host grants the invulnerability. Numbers are first guesses (`DodgeDist`, `DodgeTime`, `DodgeInvuln`, `DodgeCd`). Needs a try in Unity: the feel of the double tap while strafing, and that it can't be used while stunned, downed or mid-ability dash. Left out: a HUD cooldown (`Game.DodgeCooling` is ready), a dedicated look (a small ring only), bots don't dodge. Ctrl could clash with a browser/OS shortcut in WebGL.
- [x] **Cavalry Call (replaces Unbreakable).** Built (Nov 2026, protocol 176): the Gilded Charge look of [`Mockups/cavalry-call.html`](Mockups/cavalry-call.html) (https://claude.ai/artifact/DCGA4VR1DChKqwesTJyZ4a), with a 20% guard kept as you asked. `AbilityId.Unbreakable` is now `CavalryCall` (number kept), cooldown 18 s. Two lancers ride in at your sides at 0.45 s and charge 1,100 px along your aim from 0.85 s to 2.75 s (`Core/CavalryCall.cs`); each enemy a lance meets is hit once: 35 damage (+10 per Iron Will stack), shoved 150 px out of the lane, slowed 45% for 3 s (Heavy: 45 px shove, 20% slow; bosses: no shove, 20% slow). You take 20% less damage for 3 s (`Player.UnbreakT` reused; Iron Will is now +10% block and +10 damage per lance, and no longer heals). Mastered: four lancers (a second pair 114 px out) and a 5 s guard. New `FxKind.CavalryCall`, drawn by `Game/CavalryCallLook.cs` from its age alone. Numbers are first guesses. The healing from Unbreakable is gone (Battle Fury's lifesteal stays). Left out of the mockup: the gold slow ring on enemies (the normal slow look shows), the banner on the knight. Needs a try in Unity (lancer size next to the knight, lane width, dust density). Bots cast it whenever a target is within 700 px. Armory page for Unbreakable is out of date.
- [x] **Melee: held weapon phases out for the swing.** Built (Nov 2026, protocol unchanged): with Visual effects on, a melee weapon's held model fades out over 0.04 s when you swing and fades back in 0.14 s after the swinging model is gone (`HeldPhase`, `MeleeLooks.cs`; applied in `DrawPlayerModel`). Chained swings (Katana, Twin Blades, Halberd) keep it out until the last one ends. Windows per swing: sweep 0.39 s, spin 0.4 s, thrust 0.31 s, Lance 0.4 s, Warhammer 0.5 s are first guesses. Gloves stay. Needs a try in Unity (fade speeds, any gap or pop at the end of a swing).
- [x] **Consecrate: Holy Cross.** Built (Nov 2026, protocol 173): look B's glowing cross and halo with look C's echo trail from [`Mockups/consecrate-cross-v3.html`](Mockups/consecrate-cross-v3.html) (https://claude.ai/artifact/SAUdwzD8i7UbnPL81zXPbq). Consecrate no longer blesses the ground: a cross flies 600 px along your aim in 1.2 s, its arms growing from 50 to 190 px. Each ally it crosses (not you, not the downed) is healed 35 and gets Swiftness (+30% speed, 4 s, new replicated `Player.SwiftT`); each enemy takes 25, is slowed 40% for 3 s and is knocked back 110 px (bosses and Heavy enemies: 20% slow, no knockback). Once per target. Heal and damage scale with power (my choice, the mockup left it open). Mastered: two more crosses 22 degrees either side sharing their hits. Sacred Ground is now 40% more range and 2 s more Swiftness. Numbers are first guesses (`HolyCross.cs`). The cross is 600 px long where the mockup used 480 (the game's scale is larger). Needs a try in Unity (cross size against the arena, echo density, the Swiftness ring and streaks). Bots still cast it when an ally is hurt, aimed at their cursor. Armory page for Consecrate is out of date; `ZoneKind.Consecrate` and its drawing are now unused.
- [x] **New Swarmer (Chomper) + Spiker rename.** Built (Nov 2026, protocol 170): look B (Chomper) of [`Mockups/swarmer-looks.html`](Mockups/swarmer-looks.html) (https://claude.ai/artifact/JiWPF7iEoNsyXHEwuMDoex), made white with no eyes. The old Swarmer is now the **Spiker** (`EnemyType.Spiker`, same number, same zig-zag and stats). The new `EnemyType.Swarmer` is at the end of the enum: 14 health, speed 115, 6 per bite, radius 9, walks straight at you (shares the Brute's `v = dir`), drawn by `DrawChomper` in `WorldEnemies.cs` (the mouth gapes wider as it nears a player). I took the open question as yes: Swarmers hold wave 1 (packs of four, Rush squad is five), Spikers join from wave 2 (still in pairs, and in Scorched earth). Kill Rush, Defend and Swarm Fields use both. Numbers are first guesses. Old Practice plans that say `Swarmer` now mean the new one. Needs a try in Unity (mouth size, whether four in a pack is too many). The Bestiary and Armory pages are out of date (no Spiker or new Swarmer entry).
- [x] **Blood Shell (held bubble).** Built (Nov 2026, protocol 168): look B (Heartbeat) of [`Mockups/blood-shell.html`](Mockups/blood-shell.html) (https://claude.ai/artifact/TQg8fTNrMbdpe8whJK7Zkx). Press Space for a 96 px blood bubble (a toggle since protocol 201: press again to drop it; it also bleeds you 1.5 HP/s, `ShellDrain`): it stops every enemy bullet and keeps enemies out; allies can stand inside. Each bullet it stops costs you 7 health and heals every ally inside 4; it bursts at 1/3 health (flinging enemies back; the mastered cast also erases bullets within 160 px) or comes down when you press again, and the 9 s cooldown starts then. No upfront cost any more. Hardened Blood is now 25% less cost and 50% more healing per hit. The numbers are first guesses (`ShellCost`, `ShellHeal` in `BloodShell.cs`). Needs a try in Unity: I left out the ground stain from the mockup. Bots drop it when nothing is coming. The Armory page for Blood Shell is out of date.
- [x] **Records > Recent runs (phase 1).** Built (Nov 2026, protocol unchanged, 167): plan [`RecentRuns.md`](RecentRuns.md), mockup [`Mockups/recent-runs.html`](Mockups/recent-runs.html) (https://claude.ai/artifact/UE1ttduAmFgTuTaE7ZLeGr). A fifth Records row with a strip of your last 20 runs, tiles, and an Overview tab (damage per second, health, accuracy per wave) and a Trends tab (wave and damage by run). Every game samples each run every 10 s into `runs/<id>.run` beside the profile (`RunTimeline.cs`, `RunGraphs.cs`); only runs played from now on have graphs. Compiles, tests pass, shown in the preview (which draws no panel fills); not tried in Unity: line widths, the hover readout, the strip arrows. Phase 2 (damage by source, kills by enemy, damage taken from, ability timeline) needs a protocol bump; questions at the end of the plan are still open (hunts and rooms, teammates' lines, which charts).
- [x] **New run: CRT boot into a cyan CRT lobby.** Built (Nov 2026, protocol unchanged, 164): opening D (Terminal boot), cyan, curved glass, scanlines behind panels of [`Mockups/crt-new-run-lobby.html`](Mockups/crt-new-run-lobby.html) (https://claude.ai/artifact/BY2hFuhS9ZTxCV8CMHWdRT). Pressing New run (not Join or Continue) flares the saved-runs screen, fades the title, box and player card, grows the screen to the whole window (blue to cyan), as a full CRT the whole way (glow, scanlines, dark corners, glass highlight, rounded bezel: "glass first", [`Mockups/crt-new-run-transitions.html`](Mockups/crt-new-run-transitions.html) option A, so there is no flat fill and no pop when the lobby's glass takes over), types a link log (link, player, cat, opening lobby) and sweeps a scan line down while the lobby's pieces slide in: 7.1 s (the log types "Link", pauses a second with a blinking cursor, then "... Established."; same for Player ... name and Cat ... Petted, then Opening lobby), any key or click after 0.5 s skips (`LobbyCrt.cs`). Reduced effects or Visual effects off use the old entrance. The lobby itself now always has the cyan CRT screen behind the panels (opaque, so the menu background no longer shows in the lobby), faint phosphor lines, scanlines and a slow, faint rolling bar behind the panels only, and rounded glass corners, bezel and highlight over it, with no flicker. The main menu's CRT screens (title, header, saved runs, tip, Join) are blue now (`CrtUi.Ink`, `MenuIntro.CrtGreen`). Compiles and shown in the preview, not tried in Unity: check the scanline texture (stretched 1x720, bilinear), the bezel's 9-slice corners, that Hangout and Practice tabs look right under the glass, the 7.1 s length (long on purpose for the pauses; any key skips), and that the growing screen's bezel corners and the lobby's glass match at the hand-over. Lobby section headings (Party, Role, Class, Weapon, Ability, Invite friends) are cyan and glowing (`LobbyHeading`). Left out: the mockup's terminal font on them, and the green-to-cyan recolour of the main menu.
- [x] **Healing Dash (replaces Healing Mist).** Built (Nov 2026, protocol 166): look C (Footprints) of [`Mockups/ninja-healing-mist.html`](Mockups/ninja-healing-mist.html) (https://claude.ai/artifact/Q1oTWddLW8adMgeqNHFUDB), with small rings spreading out of each print after it lands (like the mist circles), the ability renamed (`AbilityId.HealingMist` is now `HealingDash`, number kept) and the sprint faster than the mockup. Sprint is +70% for 3 s (capstone 4.5 s, was the mist's 9 s), the Ninja drops a footprint every 64 px (`ZoneKind.Mist` reused; 40 px radius, 6 s, `Zone.Aim` = heading, side from the zone id) and anyone in one heals 12 HP/s, once however many overlap (`TickMists`, `Ninja.cs`). Morning Dew: +50% heal, +25% size. New replicated `Player.DashT` (`Player.Speed` uses it). The dew orbs (`Orb.Dew`, its drawing) and the following mist are gone. Numbers are first guesses; with the faster sprint the trail is long (about 1,400 px). Needs a try in Unity (footprint and ring size, the speed lines, whether 70% is too fast to steer). The wake direction uses the aim, not the move direction. Armory page for Healing Mist is out of date (and its icon key is now `Healing Dash`).
- [x] **World Tree: Heartwood + butterflies.** Built (Nov 2026, protocol 164): look A (Heartwood) of [`Mockups/world-tree.html`](Mockups/world-tree.html) (4 looks; A was picked). The tree's zone is smaller (100 px, was 170) and lasts 15 s (was 10; the capstone 20 s, was 16). The sprites are gone: every 1.9 s (1.4 s mastered) it sends one healing butterfly to each ally, the most hurt first and 0.16 s apart, that flutters to them in a curve and heals 14 on arrival (18 mastered); the zone's own 18 HP/s heal, slow and 20 DPS are unchanged. New `MinionKind.Butterfly` (target in `LinkTo`; `Core/WorldTree.cs`), look in `Game/WorldTreeLook.cs` (growing tree with leaf runes, roots, layered canopy with twinkling blooms and falling leaves, gold/green/cream flapping butterflies with a trail). Numbers are first guesses. Alone, nobody gets a butterfly (the caster is under the tree). Left out of the mockup: additive glow (stacked soft discs instead), the tinted glow on allies being served. Needs a try in Unity (tree size next to the 100 px ring, butterfly size 1.6x, the first wave at 0.9 s). Armory page for World Tree is out of date.
- [x] **Leap Slam: Bulwark look.** Built (Nov 2026, protocol 163): look C of [`Mockups/leap-slam-rework.html`](Mockups/leap-slam-rework.html) (https://claude.ai/artifact/H8BPouaNuNz69vqqR6xcwy). New `FxKind.LeapSlam` sent once at the cast (`LeapSlamLook.cs`): the knight swells to 1.55x and rises 56 px at the peak with the shadow on the floor, a hex landing marker closes in on the true radius, the landing throws three hex pressure rings, plates and sparks and leaves a hex rune (mastered: a dashed ring at 160 px), and each stunned enemy gets a hex cage with a draining ring instead of the ice block. Mechanics unchanged. Left out of the mockup: the shove-back on enemies and the hit-stop (the game has none). Needs a try in Unity (lift height, how the cage reads on bosses' escorts, shake). The Armory page for Leap Slam is out of date.
- [x] **Play transition: bay doors, Servo (version 2).** Built (Nov 2026, protocol unchanged, 162): version 2 of [`Mockups/menu-bay-doors.html`](Mockups/menu-bay-doors.html) (https://claude.ai/artifact/DbTe9qyEuqWm4uzZyQ6goY), 1 s each way (`MenuPlayLen`, `MenuServo.cs`). The header's letters flip like a split-flap board; the Play key splits and its halves slide into the box walls; Join game, Records and Settings drop through the floor one after another; the runs screen opens behind the doors; New run rises and the box gives a small thunk. Quit and Back are now one keycap (`KeyUi.Key` got a second legend, `text2`/`icon2`/`mix`/`dip`): only the legend, icon and lamp change. Ten new sounds (`Sfx.BayBeep` to `BayClack`, `SoundBank.cs`), cued off the same number. Back plays it all in reverse. The riveted bezel round the runs screen (`KeyUi.Bezel`, 7 px out, follows the screen as it opens) and screws in the box's corners (`KeyUi.Bolt`, menu and picker) are in. Left out of the mockup: the Play key's pulse ring during the move. Preview: `PLAY=1 PLAYK=0.5 Tools/Preview/shot.sh x menu` (`PLAYBACK=1` for the way back). Not tried in Unity: check the 1 s speed, the door seam, the header letters landing where the plain text sits (the swap at the end), and the sounds. The old scan wipe is gone.
- [x] **Vanish smoke + Blade Fan orbit.** Built (Nov 2026, protocol 162): look A (Smoke) of [`Mockups/ninja-vanish.html`](Mockups/ninja-vanish.html) (https://claude.ai/artifact/Ad3oBtXCZ15rMGbvxz2QD1) and mechanic B (Orbit guard) of [`Mockups/ninja-blade-fan.html`](Mockups/ninja-blade-fan.html) (https://claude.ai/artifact/36EtseK6FaBxvibUkNteei). I read "a and B" as Vanish A and Blade Fan B: say if you meant otherwise. **Blade Fan** now spins its 12 kunai around you for 4 s (60 px, 18 per cut, once per 0.5 s per enemy, erases enemy bullets they meet); pressing it again, or the 4 s running out, throws them out as the old ring. Cooldown 9 s -> 12 s (my proposal; the second press spends nothing). New replicated `Player.OrbitT/OrbitN`; `VanishFrom` is now replicated too (`Ninja.cs`, `NinjaLooks.cs`). **Vanish** keeps its mechanics; it gets puffs, wisps, a smoke bank and a dashed ring on the spot enemies aim at, and a puff when you return. The orbit follows you (the mockup kept it still). Compiles, ninja suite passes (its Healing Mist check already failed before this); not tried in Unity. Armory page for Blade Fan and Vanish are out of date.
- [x] **Death Lotus: Crimson Reaping look.** Built (Nov 2026, protocol 161): look D of [`Mockups/death-lotus-looks.html`](Mockups/death-lotus-looks.html) (https://claude.ai/artifact/UdxRXbXWaAMAQMzvGC968F), new `FxKind.DeathLotus` (`DeathLotusLook.cs`). The arena darkens, each target takes three white-hot crimson cuts and leaves a lotus stain, the camera kicks once at the cast. Damage is unchanged and still lands on the cast tick; the cuts are staggered 0.11 s apart (0.07 s at the capstone, so 14 cuts fit in the 1 s of invulnerability). The old pink slash chain and ring are gone. Needs a try in Unity (dark strength, cut size on big enemies, shake). Armory page for Death Lotus is out of date.
- [x] **Main menu: header screen + Quit key.** Built (Nov 2026, protocol unchanged): look E of [`Mockups/menu-screen-key-pairs.html`](Mockups/menu-screen-key-pairs.html) (https://claude.ai/artifact/8nLRRDmnCC6weMHEKwmQU5). A "MAIN MENU_" CRT screen and a Quit key (the existing button style, with a lamp) sit over Play and the trio, in the same spot as the picker's header; the loose power icon is gone. Play and the trio moved down 28 px and Play is 16 px shorter. Compiles; not tried in Unity (check the intro rise, the box spacing, and that Quit still closes the game).
- [x] **Bat Swarm look (Belfry burst).** Built (Nov 2026, protocol unchanged, 157): look B of [`Mockups/bat-swarm-rework.html`](Mockups/bat-swarm-rework.html) (https://claude.ai/artifact/1fuiPMYNRa95bKtW3SgrLs). A bell-toll ring on the cast, the bats circle you for 0.6 s then peel off one by one over 0.6 s (so the first bites land about 0.6 s later than before: say if that cost is too much), flapping dark wings with a speed streak while diving, a slash across the prey on every other bite, and feathers as they dissolve in the last second (`BatLook.cs`, `Minions.cs`; new host-only `Minion.Launch` and `BiteN`). Bats now face where they fly. Needs a try in Unity. Left out of the mockup: the hunted-enemy ring, hit numbers and the time rim. The Armory page for Bat Swarm is out of date.
- [x] **Hydra (replaces Sacrifice).** Built (Nov 2026, protocol 160): concept B of [`Mockups/summoner-sacrifice-rework.html`](Mockups/summoner-sacrifice-rework.html) (https://claude.ai/artifact/1kzJ9JwiEjfcHhPncsr5D6). The DPS Summoner's second Space ability is now Hydra (`AbilityId.Sacrifice` renamed, number kept): a three-headed hydra rises at your cursor for 8 s (cooldown 18 s) and each head spits a 10-damage venom bolt every 0.6 s at a different enemy (nearest, second nearest, ...); mastered, a fourth head. Companions are no longer touched. Numbers are first guesses (`Hydra.cs`); the bolts use the plain minion shot look and the heads have no recoil animation (clients can't see which head fired). Needs a try in Unity. The Armory page for Sacrifice is out of date and has no Hydra page.
- [x] **Alpha Call: Blood Scent.** Built (Nov 2026, protocol 159): look C of [`Mockups/alpha-call-rework.html`](Mockups/alpha-call-rework.html) (https://claude.ai/artifact/TAVmYkWVvmNrQnsLCCGTot). Your minions start at 2 stacks and each bite adds one (up to 5): +16% speed and +0.5x attack rate per stack (2 stacks is about the old flat frenzy, 5 is 3.5x bites). At 5 stacks the next bite lunges for triple damage in all and drops back to 2. Same 14 s cooldown, 6 s, capstone wolves join at 2 stacks. Only biting minions build stacks (shooters just sit at 2). Legion's frenzy on your other minions stays flat. Ember ring turns red and grows per stack, with a pip per stack above each minion (`DrawFrenzyMinions`, `AbilityLooks.cs`). Numbers are first guesses (`ScentSpeedPer`, `ScentRatePer` in `Minions.cs`). Needs a try in Unity. Left out of the mockup: the lunge's forward dash, enemy knockback and hit-stop. The Armory page for Alpha Call is out of date.
- [x] **Join game: address bar as a CRT screen + Connect key.** Built (Nov 2026, protocol unchanged): option 1 of [`Mockups/join-address-bar.html`](Mockups/join-address-bar.html) (https://claude.ai/artifact/YbV7LWWfZsAgFsv4JcEMy6). The address (with its HOST ADDRESS label) is a green CRT screen, Connect a keycap like the menu's (`KeyUi.Key`, Nov 2026), and the connecting status is a CRT screen with Cancel beside it (`Menus.cs`). Compiles and shown in the preview, not tried in Unity: check the typed text's colour and caret on the green screen (`GUI.contentColor` tint on `Ui.BareField`).
- [x] **Main menu buttons as keycaps.** Built (Nov 2026, protocol unchanged): style A of [`Mockups/mech-buttons.html`](Mockups/mech-buttons.html) (https://claude.ai/artifact/6FfvYNtc8mvF5AenSLZpVF). Play, Join game, Records, Settings, New run and Back are keycaps in a dark well with a corner lamp (lights under the mouse; Back's stays lit) that drop a few pixels when pressed (`KeyUi.cs`). The VHS hover still wraps them. Compiles and shown in the preview; not tried in Unity: check the press travel (the press is tracked from the mouse-down on the key), that the VHS hover and the press look right together, and the Play pulse ring around the key's well.
- [x] **Play picker: one scrolling saves list (Older runs removed).** Built (Nov 2026, protocol unchanged): the header screen ("YOUR RUNS" / "START A RUN", typed) and the physical Back key stay; under them one CRT screen lists every save, newest first (tall row for the newest, one-line rows for the rest), each with Continue and a DEL key (DEL, then REALLY?). Mouse wheel and a draggable scroll bar scroll it, with a "▼ n MORE" strip. The Older runs button, its screen and its grow transition, and the Delete this run button are gone (`DrawPlayPicker`, `DrawRunRow`, `SavesUi.cs`). Mockup: [`Mockups/crt-full-menu.html`](Mockups/crt-full-menu.html). Compiles and shown in the preview; not tried in Unity: check the wheel scrolls only while the mouse is over the list, DEL's second click, and that no click lands on a row hidden past the screen's edge. Gamepad and keyboard scrolling are not done.
- [x] **Soldier DPS gun animations (Twin Rifle, Railgun, Burst SMG).** Built (Nov 2026, no protocol change, 156): all three of [`Mockups/soldier-dps-gun-animations.html`](Mockups/soldier-dps-gun-animations.html) (https://claude.ai/artifact/75sL7drSpCfqpWaaUaTxjH), glowing shots kept, lobby preview matches (`DpsGunLooks.cs`; spray cone in `WorldRenderer.cs`). Needs a try in Unity. Left out: the mockup's gold crossfire ring and per-enemy pierce rings (`Bullet.Crossfire` is host-only, so clients can't see it), and the cone doesn't widen with the extra-projectile upgrade. Armory pages for these three are now out of date.
- [x] **Your look: Reward ladder first, two-line ladder rows.** Built (Nov 2026, protocol unchanged): the Reward ladder is the first row of the drawer (`SectionOrder`, `CosmeticsUi.cs`), and each ladder row is two lines, name on top with the kind under it, status on the right (look A of [`Mockups/reward-ladder-text.html`](Mockups/reward-ladder-text.html), https://claude.ai/artifact/8DzFHSAzyt9YBpRTFkyJhS), so names are no longer cut off. Rows are 52 px (were 42). Not compiled (no mono in the cloud container) and not tried in Unity.
- [x] **Sanctum: healing pulses, Bubble look.** Built (Nov 2026, protocol unchanged, 156): look B of [`Mockups/sanctum.html`](Mockups/sanctum.html) (https://claude.ai/artifact/TT6twY4kADa161ohhGM9aj).
      The dome (200 px, 300 with the capstone, 7 s, still stops bullets and prevents going down) no longer heals 20 HP/s: it sends 7 pulses from its centre, one every 0.9 s starting at 0.9 s,
      20 HP each and 30 for the last (150 total, was 140). A ring takes 0.55 s to reach the rim and each ally is healed as it passes them (`Core/Sanctum.cs`; `Game/SanctumLook.cs`).
      Left out of the mockup: ripples where bullets hit the shell. Needs a try in Unity. Armory page for Sanctum is out of date.
- [x] **Classic menu pointer (Corner + Lock-on).** Built (Nov 2026, protocol unchanged, 156): the Corner pointer and Lock-on morph from the Cursor Morph Lab mockups (https://claude.ai/artifact/ADXUL4hAKiFdKgqKJihnoN). Every rank cursor has its own Corner pointer too (`DrawLookPointer`, `DrawLookMorph`, `LookTints` in `CursorLooks.cs`): the bracket in the look's colour with a small flourish, then the crosshair swells out from the hotspot with an overshoot and a ripple (a generic Lock-on; the ticks only slam in for Classic). The system pointer is now always hidden. The pointer also shows (and morphs) over the pause menu, upgrade cards, shop and end screen. `DrawClassicMorph`, `StepCursorMorph` and `CursorMorphLen` (0.6 s) in `CursorLooks.cs`. Also fixed: the end screen and rank report had no cursor. Needs a try in Unity (size of the pointer, 0.6 s speed).
- [x] **Lumen Ward (Sanctify, reworked).** Built (Nov 2026, protocol 156): look A (Cathedral) of [`Mockups/sanctify-cone.html`](Mockups/sanctify-cone.html) (https://claude.ai/artifact/V38EdAFeoRynvKNiGrvooq), named Lumen Ward (`AbilityId.Sanctify` kept). A 740 px cone along your aim for 6 s, locked at the cast: heals 9 HP/s inside, burns enemy bullets in it; enemy bullets elsewhere slow to a stop (about 0.7 s, spreading at 1100 px/s) and fly again when it ends. Cooldown 20 s, no longer Instant for mastery XP. Capstone: enemies outside the light freeze too (not bosses). Numbers are my proposals. Left out of the mockup's look: the dark vignette outside the cone. Needs a try in Unity. README is updated; the Armory page for it is out of date.
- [x] **Melee grunts: Buzzsaw, zig-zag Swarmer, Spike Block.** Built (Nov 2026, protocol 157) from [`Mockups/melee-grunts.html`](Mockups/melee-grunts.html) (https://claude.ai/artifact/6eBLdvmwXbaWiwDVRPKpo4). The Buzzsaw (new, `EnemyType.Buzzsaw`) and Spike Block (new, `EnemyType.SpikeBlock`) join the melee mix from wave 5 (and Swarm Fields); the Swarmer stays, now zig-zagging with a thin spike on each face (the Buzzsaw is added beside it, not instead of it: say if you want the Swarmer gone). Numbers are first guesses (`MeleeGrunts.cs`). A Buzzsaw cuts your hit grace to 0.12 s so its touches come fast. The Swarmer's zig-zag makes it slower to arrive (about 0.6 of its speed toward you). Needs a try in Unity. Bestiary and Armory pages are out of date (no entries for the two new enemies).
- [x] **Artillery Assault and Bullet Rain.** Built (Nov 2026, protocol 155). **Artillery Assault** is the DPS Soldier's Bullet Storm renamed, with look A of
      [`Mockups/bullet-rain.html`](Mockups/bullet-rain.html) (heat aura, streaks, muzzle flash; `ArtilleryLook.cs`); mechanics unchanged. **Bullet Rain** replaces the
      Elementalist DPS's Meteor Storm: look D (Downpour) of [`Mockups/bullet-rain-elementalist.html`](Mockups/bullet-rain-elementalist.html). A `ZoneKind.BulletRain`
      zone (165 px radius, fixed where you cast it, 6 s or 9 s mastered) that does 45 damage a second to every enemy in it (`BulletRainLook.cs` draws it). Needs a try in
      Unity; the 45 a second is a first guess and the zone does not follow the cursor as the mockup did. Enemies in the rain are wet (3 s after they leave, with the 30% wet slow).
      `AbilityId.MeteorStorm` became `BulletRain`. Armory and Bestiary pages are out of date for both.
- [x] **Your look: side-menu icons + Reward ladder.** Built (Nov 2026, protocol unchanged, 152): the icons (`ui_frame`, `ui_trail`, `ui_scene`, `ui_card`, `ui_name`, `ui_aim`, `ui_emote` in `PixelArt.cs`) and look 1 of [`Mockups/reward-ladder-next.html`](Mockups/reward-ladder-next.html) (https://claude.ai/artifact/XySVp6TrCVv4ruoSvgm8oh): the next reward pinned in a box on the left (live preview, XP to go), the ladder as a rail on the right (shields on a line, tier banners, a gold "you" marker; it opens at your rank). Compile check, core and feature suites pass. Needs a try in Unity.
- [x] **Lifeline look.** Built (Nov 2026, protocol 153): look E (Heartbeat Halo) of [`Mockups/lifeline.html`](Mockups/lifeline.html) (https://claude.ai/artifact/KTCFvJN7mTMWBGzBJ1sbu7), under Visual effects (`LifelineLook.cs`, new `FxKind.Lifeline`). Needs a try in Unity, with an ally down, to see the halo and column on everyone. The downed ally is revived at the cast (as before), so the mockup's raise-on-landing is just the halo dropping in. The Armory and Bestiary are unaffected.
- [x] **Menu title typing sounds.** Built (Nov 2026, protocol unchanged, 151): look B (Mechanical keyboard) of [`Mockups/menu-typing-sounds.html`](Mockups/menu-typing-sounds.html) (https://claude.ai/artifact/TQwbAXCgovn2mq9gH7V5mK). A click-and-thock per letter while the intro types the title (four takes), a deeper one for the space, and a bigger Enter thock on the last letter (`TickTitleTyping`, `MenuIntro.cs`; `Sfx.TitleKey/TitleSpace/TitleEnter`). Plays in the profile picker to menu move too (it types the title as well). Own screen only; silent when the intro is skipped or with reduced effects. Needs a try in Unity.
- [x] **Cursor cosmetics.** Built (Nov 2026, protocol unchanged, 153): the ten looks of [`Mockups/cursors.html`](Mockups/cursors.html) (https://claude.ai/artifact/1RkWbWDHf2rd3vmaSydA4e), under **Cursor** in Your look. Classic (the old crosshair) is free; the others unlock at ranks 3, 4, 11, 15, 17, 21, 26, 31, 38 and 46. Only your own crosshair is drawn, so nothing goes on the wire. Left out: additive glow; a still fallback with Visual effects off is not built (the mockup note says so, remove it or build it). Needs a try in Unity. The Armory and Bestiary are unaffected.
- [x] **Aim line cosmetics.** Built (Nov 2026, protocol unchanged, 147): the ten looks of [`Mockups/aim-lines.html`](Mockups/aim-lines.html) (https://claude.ai/artifact/DqF7ZeYmB5eJkJizFEkubX), under **Aim line** in Your look. Classic is free; the other nine unlock at ranks 5, 11, 14, 17, 21, 26, 29, 34 and 46. Only your own line is drawn, so nothing goes on the wire. Left out: the Ruler's numbers (the arena draws shapes only, no text) and additive glow on Ember, Storm and Prism. Needs a try in Unity. The Armory and Bestiary are unaffected.
- [x] **Main menu VHS hover.** Built (Nov 2026, protocol unchanged, 147): look C (Rewind) of [`Mockups/menu-vhs-hover.html`](Mockups/menu-vhs-hover.html) (https://claude.ai/artifact/PsNUAeijJ2QycuxskywvLi). Hover Play, Join game, Records or Settings for the tape look; click snaps it into focus (0.3 s double image and white flash) until you leave and re-enter (`MenuVhs.cs`). Left out of the mockup: the blur of the focus pull (IMGUI has no blur, the double image stands in). Needs a try in Unity. The Play picker (Back, Continue, New run, Older runs, Delete, and the older-runs list's Continue) has it too (Nov 2026); say if the pause menu should. Also fixed a sometimes-delayed click: the snap's double image drew the button three times on repaint, which shifted IMGUI control ids, so a click right after hover could miss (`Ui.NoHit`).
- [x] **Spirit Chalk: no bullet block by default, slow fade, Warding Ink card.** Built (Nov 2026, protocol 150 after merging; a card was added and card ids shifted). Ink now lasts 10 s (`ChalkLife`)
      and lets bullets through; the new Rare card **Warding Ink** (Spirit Chalk only; `Upgrades.RequiresWeapon`) makes it destroy bullets, each taking 1.5 s (`ChalkWear`) off the dot it hit.
      Look C (dust) built: the ribbon dims to a ghost and grains lift off it, a spark puff marks each Warding Ink block (`ChalkLook.cs`). From [`Mockups/chalk-fade.html`](Mockups/chalk-fade.html)
      (look C picked). Needs a try in Unity. Armory page for Spirit Chalk is out of date.
- [x] **Run replays with a timeline (F1 → Replays).** Built (Nov 2026, protocol unchanged, 150): timeline look C of [`Mockups/dev-timeline.html`](Mockups/dev-timeline.html) (https://claude.ai/artifact/LFGSc7YWDfNEu29cSi9aPu) and the tab of [`Mockups/dev-recording.html`](Mockups/dev-recording.html) (https://claude.ai/artifact/4jXeaJ9ddAMPZTf4RcNpgV). Record is a saved switch (off by default); replays save to `replays/` as `.replay`; Open / Rename / Delete in the tab; the bar has pause, frame steps, wave jumps, scrubbing and speeds 0.1x to 10x either way. Watch and scrub only: no taking over from a point (the game's random numbers aren't seeded and the host state isn't saved; that would be a second pass). Opens from the main menu only. Needs a try in Unity (the bar covers the bottom HUD; say if you want it smaller or hideable).
- [x] **Play to Continue was too abrupt.** Built (Nov 2026, protocol unchanged): look D (Scan wipe) of [`Mockups/play-transition.html`](Mockups/play-transition.html) (https://claude.ai/artifact/KoZjXL3aurKkt4Dhx6GTaB), slowed to 0.85 s (`MenuPlayLen` in `Menus.cs`). A teal line sweeps left to right from the menu buttons to the picker, and right to left on Back or Esc. No clicks during the sweep; it snaps with reduced effects. Needs a try in Unity.
- [x] **Menu tip box (no more record line).** Built (Nov 2026, protocol unchanged, 147): look C with D's corner outlines of [`Mockups/menu-tip-box.html`](Mockups/menu-tip-box.html)
      (https://claude.ai/artifact/1fYyGEDjRqhw9XfqR2nMxB). The chip under the title is gone, record line and all; a separate box sits under the menu card with the tip's drawing,
      the text, a count and Prev / Next. It changes every 8 s (or at once on a click) and fades out while the Join address row is open (`DrawMenuTipBox`, `Menus.cs`).
      The gap under the title axis is now empty; say if you want the card or title moved up. Needs a try in Unity.
- [x] **Tank gun animations (Scattergun, Flak Cannon, Riot Repeater).** Built (Nov 2026, no protocol change): glowing shots kept, lobby preview matches. Mockup:
      [`Mockups/tank-gun-animations.html`](Mockups/tank-gun-animations.html) (https://claude.ai/artifact/QeTMr1Gr6n1KoJKymyutE3). Needs a try in Unity. Armory pages are now out of date for these three.
- [x] **Pause menu: models show up before the rest of the menu.** Done (Nov 2026): models fade with their panel.
- [x] **HUD panels get the upgrade-card gradient.** Built (Nov 2026, protocol unchanged, 147): `Ui.HudPanel` now shades every HUD panel like `CardArt.Body` (slate when plain, tinted by the accent otherwise). Needs a try in Unity.
- [x] **HUD animations: rim runner.** Built (Nov 2026, protocol unchanged, 147): look D of [`Mockups/hud-animations.html`](Mockups/hud-animations.html) (https://claude.ai/artifact/FtY5EZaHeJZgckp88Pb9SN). Recharging draws the tile's border as a loading ring, a ready tile has a spark circling it, using it snuffs the rim with a burst at the key cap (`DrawTileRim`, `HudTiles.cs`). Reduced effects: the ring only. Needs a try in Unity.

### Balance / design
- [x] **Hangout: scrolling map, PvP and shooting.** Built (Nov 2026, protocol 148): layout B of [`Mockups/hangout-pvp.html`](Mockups/hangout-pvp.html) (https://claude.ai/artifact/Xn5oUe7SyRfa7pNCxk6WLT)
      without the background slider (you dropped it), the PvP checkbox under the minimap, and look A (Grow) of [`Mockups/hangout-transition.html`](Mockups/hangout-transition.html)
      (https://claude.ai/artifact/3FWZesgUXiUERrtzBmmikm). 2400 x 1600 map. Shots use your weapon's rate, speed and reach only: no abilities, pellets, beams or swings. Needs a try in Unity.
      Differences from the mockup: nobody is drawn faded when PvP is off (everyone starts off, so everything would be ghosted); instead health bars show on fighters. Say if you want the faded look.
      Abilities (protocol 149): only the first build's three, on Space or right click with PvP on: DPS Dash, Tank Barrier (4 s, deletes other players' shots, since the Hangout has no enemies), Healer Burst heal (+60 in 230 px, revives at once). Needs a try in Unity.
- [x] **Elementalist DPS abilities: Meteor and Blizzard visuals, Arcane Surge becomes Tsunami.** Built (Nov 2026, protocol 147): look A for all three, from
      [`Mockups/elementalist-dps-abilities.html`](Mockups/elementalist-dps-abilities.html) (https://claude.ai/artifact/R7eis4QntDJvmvq8mpvhb9). Meteor gets a falling
      fireball and a flash/ring/sparks/scorch impact (`FxKind.Meteor`), Blizzard a whiteout of wind-driven snow. **Tsunami** (`Core/Tsunami.cs`): a wave 340 px wide
      along your aim, 12 damage, **wet** for 8 s (14 mastered), slowed 30% (15% on bosses and heavy enemies); every lightning hit (Arc Lightning, Storm Hammer pulses,
      Chain Light, Nimbus) arcs to every wet enemy on the map for 40% of that hit, at most once per 0.4 s per enemy. Mastered: the wave rolls back and wets what it
      missed. Storm Hammer pulses count as half a hammer hit (their own arcs are only an eighth, which would have made wet arcs about 2 damage). Needs a try in Unity.
      Armory and Bestiary pages are out of date for Arcane Surge, Meteor and Blizzard. Say if you want fire (Fireball, Meteor) to dry enemies off.
- [x] **Elementalist tank reworks: Aeroaegis, Frost Nova, Flame Wall.** Built (Nov 2026, protocol unchanged), picks B, A and B of
      [`Mockups/aeroaegis.html`](Mockups/aeroaegis.html) (https://claude.ai/artifact/N3c3iaVGPCbmaTwdkmMbFT),
      [`Mockups/frost-nova-rework.html`](Mockups/frost-nova-rework.html) (https://claude.ai/artifact/EmEnaEkh7bnKo6Y8twYRRS) and
      [`Mockups/flame-wall-rework.html`](Mockups/flame-wall-rework.html) (https://claude.ai/artifact/F2KPGcHLYYcnByVSCiuFEL). The lobby preview
      runs the real game, so it matches. Needs a try in Unity. Armory pages are now out of date for all three.
      - **Aeroaegis** (was Arcane Aegis, `Core/Aegis.cs`, `Game/AeroaegisLook.cs`): the vortex (56 px) goes to the player your cursor is nearest
        (you if no one is closer; downed allies skipped). Enemies are pressed out to its rim and slid round it. I kept the bullet reflect and its
        mastery (4.5 s, double damage), now at the vortex's radius. `AbilityId.ArcaneAegis` is unchanged. Say if bullets should pass instead.
      - **Frost Nova** (`Game/FrostNovaLook.cs`): its Ring effect now lasts 3.3 s so the frosted ground stays; the old ring draws with effects off.
        Left out: the mockup's 0.35 s wind-up (the cast is instant), the ice bursting into shards when it thaws, and a freeze ring that tracks
        other freezes (the ring counts 3 s from when the enemy is first seen frozen).
      - **Flame Wall** (`Game/FlameWallLook.cs`): magma ring; the mockup's fireball arc from the staff and spark bursts on blocked shots are left out
        (the cast is instant; blocked shots flare the rock just before they hit).
- [x] **Ping system.** Built (Nov 2026, protocol 145; `Msg.PingMark`, since `Msg.Ping` was already the latency check). Mockup:
      [`Mockups/ping-system.html`](Mockups/ping-system.html) (https://claude.ai/artifact/UHdFiTLxtxuzmZLQZQbH3Z). I picked the wheel (tap = last pick, hold = wheel),
      the player's slot colour, and a separate key (middle mouse, rebindable). The arena is one fixed screen, so the mockup's off-screen arrows and
      minimap weren't built. Left out for now: muting a player's pings, ping sounds, a Loot mark on the item itself. Say if you want any. Needs a try in Unity.
      Bestiary/Armory pages are unaffected.
- [x] **"Wave cleared" box with awards and a ready system.** Built (Nov 2026, protocol 146): look 1 of [`Mockups/wave-cleared-ready.html`](Mockups/wave-cleared-ready.html)
      (https://claude.ai/artifact/U4U1dESwvCGWizAUC9ivBk): squad tally with an Awards column, the next wave's enemy types and the road to the boss,
      Ready (Y) and an Auto-ready switch (off by default). Everyone ready = a 3 s countdown; everyone on auto-ready = the usual 4 s; otherwise it waits.
      The next-wave panel lists enemy *types* (the counts are random per wave), so no counts like the mockup.
- [x] **In-run text chat box.** Built (Nov 2026, protocol 143), look C (Bubbles) of [`Mockups/chat-box.html`](Mockups/chat-box.html) (https://claude.ai/artifact/KL9YxpHUagLVPpcBNU1tB8).
      Match only; no system lines (joins, revives) yet. Say if you want them or the Hangout to have chat too.
- [x] **Air trail behind fast movers.** Built (Nov 2026, look A Ribbons, protocol unchanged): `AirTrailLooks.cs`, from [`Mockups/air-trail.html`](Mockups/air-trail.html)
      (https://claude.ai/artifact/7n5yGrXprCX62ViuzaxePi). Players, enemies and minions; none under 110 px/s, full at 330. Tune `AirStart`/`AirFull` if it's too shy or too loud.
- [x] **Defend the Crystal should use mostly melee enemies.** Done (Nov 2026): melee, a gun 15% of the time.
- [x] **Revolver and Mender's Bow animations.** Built: look A for both (Revolver "Smoke & brass", Bow "Flex & snap"), Nov 2026
      (`Game/GunBowLooks.cs`). Mockup: [`Mockups/revolver-bow-animations.html`](Mockups/revolver-bow-animations.html)
      (https://claude.ai/artifact/WRUe5JpFtw1zhdMPZtiVY4). Kept from today's look: the cylinder pips and the bow's draw ring and aim cone.
- [x] **Healer Summoner left-click attacks.** Picks (Nov 2026), mockup [`Mockups/healer-summoner-attacks.html`](Mockups/healer-summoner-attacks.html)
      (https://claude.ai/artifact/V7omfhCLTvzB8i3riqqfA8):
      - [x] **Sprite Staff: Firefly Hop.** Built (protocol 130): a firefly seeds what it hits, then hops (up to 2, 300 px) to the
            nearest enemy that isn't seeded. 7 damage every 0.5 s: ~14 a second on one enemy, ~42 across three.
      - [x] **Treant Rod: Root Lash**, and the tether to the Treant is now a root too. Built: 250 px line, 15 damage every 0.55 s
            (~27 a second), heals allies it touches 10. Homing Rounds widen it. Code names (`Vine*`, `TickVines`) are unchanged.
      - [x] **Treant Rod: sounds and seeds (built, Nov 2026, protocol 196; check in Unity).** From [`Mockups/treant-rod-sounds-seeds.html`](Mockups/treant-rod-sounds-seeds.html) (https://claude.ai/artifact/2Z2P1N6zZ2j3Y5nqGUh6UN): the lash is Leaf Rustle (`Sfx.SwingRoot`), the Treant flicks a seed (`Defs.TreantSeed`, new bullet kind, drawn as the Thorn Dart) at the nearest enemy within 320 px, 3 damage every 0.4 s, with Pod Pop (`ShotSeed`) on firing and Tick (`ShotSeedHit`) on landing. Seeds get no weapon mods; range grows with the Treant. Browser mockup sounds are approximations: listen in Unity (`Tools/SoundCheck/render.sh Swing`/`Shot` now works again) and tell me if any is too loud/quiet. The Armory and Bestiary pages are out of date for the Treant Rod.
      - [x] **Totem Staff replaced by Spirit Chalk**, and **Prism Beam replaced by Nimbus** (Healer Elementalist). Built (Nov 2026, protocol 131; smoothed and old code removed at 133)
            from [`Mockups/totem-staff-rework.html`](Mockups/totem-staff-rework.html) (https://claude.ai/artifact/3cp8ynYASdq6fySbRoaeJZ).
            Spirit Chalk also traps: **draw a loop round non-boss enemies and they can't leave it for 3.5 s.** The old weapons'
            leftover code (the Prism Beam's split beams, the totem zone, charge and timer) was removed on request (Nov 2026).
- [x] **Healer Soldier: Haven replaced by the Adrenaline Shot.** Built (Nov 2026, protocol 144): look C (Heartbeat), capstone C (half-length dose to the nearest
      other ally within 250 px), from [`Mockups/adrenaline-shot.html`](Mockups/adrenaline-shot.html) (https://claude.ai/artifact/UmTvWZEiBvZ95Qt1a6pQJL).
      Instant, 14 s cooldown, goes to the player closest to your cursor (you, alone): 8 s of +30% move speed, +40% fire rate, 30% less damage.
      Hallowed Ground became **Long Dose** (+3 s, 2 stacks). Haven's leftovers are still in the code, waiting for your OK to delete: `ZoneKind.Haven` and its
      `ZoneDef` (they keep the zone numbers in place), the revive-boost zone code in `Minions.cs` and `SoloZoneHeal`.
- [x] **Healer rework: Soul Lantern (Summoner) and Deployable Medkit (Soldier).** Built (Nov 2026, protocol 139): Soul Lantern look B
      (Wisp Cage) and Deployable Medkit look A (Field Kit), from [`Mockups/soul-lantern.html`](Mockups/soul-lantern.html)
      (https://claude.ai/artifact/1LBQNmz4VkR48go2Un1xSk) and [`Mockups/deployable-medkit.html`](Mockups/deployable-medkit.html)
      (https://claude.ai/artifact/6mZaE1GtNnM1PsK4YhDRwa). Soul Link left the Soldier; the Medkit took its slot (and enum number).
      Kindred Spirits now speeds the lantern's heal. Medkit orbs use the normal orb magnet (110 px, hurt players only).
      The dormant Soul Ghost code (`SoulGhost.cs`, `MinionKind.SoulGhost`, `Player.LinkT`/`LinkTo`, the old tether drawing) was
      removed in Nov 2026 (protocol 233). The old zone lantern (`ZoneKind.Lantern`) is still dormant.
      Bestiary/Armory pages still show the old abilities until you ask.
- [x] **Milestone upgrades.** Built (Nov 2026, protocol 141): every 10 levels an extra pick from their own gold pool (Scholar, Second Helping,
      Wide Choice, Reroll Cache, Fortune, Iron Constitution, Quickening), plus the very rare **Milestone Compass** (5% a stack, max 3, a lucky
      milestone pick on any level-up). Mockup: [`Mockups/milestone-upgrades.html`](Mockups/milestone-upgrades.html)
      (https://claude.ai/artifact/G9mB2sSNavM6WPVZvzWXQL). Numbers are placeholders to tune. Milestone picks can't be rerolled, and a lucky one
      uses the same "MILESTONE!" title. Bestiary/Armory pages don't list these yet.
- [x] **Level-up cards redrawn** like the milestone cards (rarity chip on top, cutout icon tile, stack circles, rarity glow and gradient body, slow bob,
      the pick preview on the card), two-click Reroll, uppercase REROLL / CHOOSE UPGRADE. Mockup: [`Mockups/upgrade-card-layout.html`](Mockups/upgrade-card-layout.html).

### Visuals
- [x] **Tank Soldier Fortress dome.** Built (Nov 2026, protocol 146): look B, Bulwark wall (`Game/DomeLook.cs`, `Core/Dome.cs`), 10 s (14 s mastered), no taunt or damage cut. Only players and player bullets cross; enemies (bosses too) and their shots are held out, and nothing hostile damages a player inside. Mockup: [`Mockups/fortress-dome.html`](Mockups/fortress-dome.html) (https://claude.ai/artifact/VapgPykPS23u6D1UnMjHvv). Bestiary/Armory pages still show the old Fortress until you ask.
- [x] **Profile picker <-> main menu transition, with sound.** Built (Nov 2026, no protocol bump): look C, Plot points, at half speed
      (`Game/ProfileTransition.cs`), from [`Mockups/profile-transitions.html`](Mockups/profile-transitions.html)
      (https://claude.ai/artifact/3pyKAd8sSKGrPhRcXE7tND). Max 3 profiles; the picker always shows four boxes (empty slots say "Empty",
      New profile grays out when full). It replaces the menu's plotted intro after a pick (the intro now runs inside the move).
- [x] **Healer Soldier Mend Pulse animations.** Built (Nov 2026): look B, Heartbeat (`Game/MendPulseLook.cs`, no protocol bump), from
      [`Mockups/mend-pulse.html`](Mockups/mend-pulse.html) (https://claude.ai/artifact/4RHDSKaLNNGErr8MhprTWx). Left out: the capstone's bullet
      pops (bullets are erased host-side with no event the client sees).
- [x] **Tank Soldier Roar and Bulldoze animations.** Built (Nov 2026): the "Proposed" side of [`Mockups/tank-abilities.html`](Mockups/tank-abilities.html)
      (https://claude.ai/artifact/NosUu8ukpJzDsBes8FiAYM), `Game/TankLooks.cs`, no protocol bump (protocol 142). Timed from each cast's `FxKind.Callout`,
      so nothing is synced; the old plain rings are skipped with Visual effects on. Differences from the mockup: no windup before the cast (the
      ability fires instantly, so Roar's pull-in is 0.08 s, and Bulldoze's lane shows as it starts), enemies don't turn to face you, no hit-stop
      on other players' casts, and an enemy a Bulldoze kills is gone before the charge reaches it, so it shows no contact sparks.
      Preview: `CAST=Roar CASTT=0.25 Tools/Preview/shot.sh x hud 6 9 2`, `CAST=Bulldoze CASTT=0.2 ...`, or `LOADOUT=0,0,0,1 ABILITY=1 SHOWT=1.55 shot.sh x lobby`.
- [x] **Tank Soldier Barrier animations.** Built (Nov 2026): look C, Bulwark Plates (`Game/BarrierLook.cs`, no protocol bump). Reflect tint left out (host-only flag). Mockup: [`Mockups/barrier-animations.html`](Mockups/barrier-animations.html)
      (https://claude.ai/artifact/CpWoeYAWoSirm4mkf7XHx4): A today, B Force Field (hex dome), C Bulwark Plates (8 plates,
      one knocked off per boss hit), D Soap Bubble. Drawing only; a reflect-specific tint would need `BarrierReflect`
      replicated (protocol bump).
- [x] **Arcane Missiles reworked as Ice Shards.** Built (Nov 2026), look A from [`Mockups/ice-shards.html`](Mockups/ice-shards.html)
      (https://claude.ai/artifact/6D2aFuh1WxeedCRGpd7iSx). Same mechanics (orbit, launch, homing); renamed, frost-blue kites that
      shatter on a hit. Internal ids (`WeaponId.ArcaneMissiles`, `Defs.ArcaneShot`) unchanged; no protocol change. The Armory
      page still says Arcane Missiles until you ask for it to be updated.
- [x] **Radiant Orb trail.** Built (Nov 2026): style B, Stardust (`Game/RadiantOrbLooks.cs`), from
      [`Mockups/radiant-orb-trails.html`](Mockups/radiant-orb-trails.html). Light Orbs get it at half size. Drawing only.
- [x] **Arc Lightning: cone + Lichtenberg look.** Built (Nov 2026, protocol 175, no change): the strip is now a cone (24 px wide at the staff, 120 px at the far end, `Game.ArcTaper`/`ArcNear`), and that cone is what hits; Barrage's extra cones fan out from the staff. Look F (Lichtenberg) of [`Mockups/arc-lightning-2.html`](Mockups/arc-lightning-2.html) (https://claude.ai/artifact/LKmsYerhGbrtkx92cwFTq1) replaces Thunderhead (`Game/ArcLightningLooks.cs`): violet tendrils creep along the dashed cone while you charge; on release a fractal tree whips down it in 0.26 s with a branch to each enemy, cools to violet and stays as a scar for about a second (the effect now lasts 1.4 s). Needs a try in Unity (tree density/brightness, up to ~350 line segments per strip so check Barrage's frame cost, the cone against your aim). Looks round two had eight options (E to L); the rest are unbuilt. The Armory page for Arc Lightning is out of date.
- [x] **Chain Light looks.** Built (Nov 2026): style A, Radiance (`Game/ChainLightLooks.cs`), in the arena and the
      lobby preview, from [`Mockups/chain-light.html`](Mockups/chain-light.html).
- [x] **Soldier DPS ability visuals: Dash, Cluster Grenade, Mark for Death.** Built (Nov 2026, no protocol change,
      `Game/SoldierLooks.cs`) from [`Mockups/soldier-dps-abilities.html`](Mockups/soldier-dps-abilities.html)
      (https://claude.ai/artifact/5ZoTSdqs2X2kqCzhZwdRRo): Dash B (Comet Streak), Grenade A (Frag Burst), Mark A (Reticle Lock)
      with B's skull over it. Left out for now: the dash's squashed body, and other players' dash streaks (only your own dash
      has ever been replicated); the shards are for looks (the real shrapnel's angles are the host's).
- [x] **Character models for every role and class (18), with weapon models.** Built (Nov 2026) from
      [`Mockups/character-models.html`](Mockups/character-models.html), colour-by-class (`Game/CharacterModels.cs`),
      plus a bigger role-coloured glow on the floor round every player (`Juice.cs`, `GlowPlayer`). The weapon models are
      a first pass: redraw any of them in `DrawHeld`.
- [x] **Melee attack animations.** Built (Nov 2026): style E, the weapon models with the energy arc
      (`Game/MeleeLooks.cs`), from [`Mockups/melee-attacks.html`](Mockups/melee-attacks.html).
- [x] **Repulsion Cannon visuals.** Built: look D, the cannon blast (`RepulseLook.cs`), Nov 2026. Mockup: [`Mockups/repulsion-cannon.html`](Mockups/repulsion-cannon.html)
      (https://claude.ai/artifact/QutT2bQZmuigHQpx9cX5h3): today's look and four options, A Shockwave, B Light
      burst, C Force ripples (bends the floor grid), D Cannon blast (smoke, sparks, small kick), at the real 70° / 240 px
      cone, with optional hit feedback (flash, slide streak, orb). Drawing only (`DrawRepulseFx`), no protocol change.
      Not built from D: the dust puffs behind hit enemies (needs hit positions in the effect).
- [x] **Frost Lance visuals.** Built: look A, the crystal spear (`FrostLook.cs`), Nov 2026. Mockup: [`Mockups/frost-lance.html`](Mockups/frost-lance.html)
      (https://claude.ai/artifact/3iseS4yjtmVDriXUxqJoxF): today's look and four options, A Crystal spear, B Frost
      growth, C Glacier, D Aurora lance, at the real 230 px reach and 0.15 s splinter delay, with optional frost on
      hit enemies. Drawing only (`DrawIcicleFx` / `DrawSplinterFx`), no protocol change. Not built from A: the facet
      sparks on hit enemies (needs a per-enemy hit timer).
- [x] **Redesign the main menu buttons.** Built: design D with the pen plot intro (Nov 2026).
      Mockups done, waiting for a pick: four layouts in [`Mockups/menu-redesign.html`](Mockups/menu-redesign.html)
      (https://claude.ai/artifact/6T2r1GZSBsajVK8pxaAo8s), and three plotted intros for the menu in
      [`Mockups/menu-intro.html`](Mockups/menu-intro.html) (https://claude.ai/artifact/TdwEQNxJ8STpVk2MRPqLuk).
      Owner likes **D (Big play)**: its intros and the Host game → lobby move are in
      [`Mockups/menu-d.html`](Mockups/menu-d.html) (https://claude.ai/artifact/VskdxmJhgTzmpeUnjjM2R5). With D the
      port moves to Settings (Network, 7777 by default) and the name is edited on the bottom-left chip.
- [x] **Knockback visual effect.** Done (Nov 2026): pushes slide (0.18 s) with a streak, dust and an impact puff.
- [x] **Load-in animations for the How to Play and Field guide windows.** Done (Nov 2026): How to play slides in;
      the Field guide rises in.

### Rank (cosmetic account rank)
Design doc: [`Rank.md`](Rank.md); mockups: [`Mockups/rank-screens.html`](Mockups/rank-screens.html)
(https://claude.ai/artifact/6jPzqJeT4sFbfWhm1Ewkf9). **Badge style B (shield) picked.** XP from every run with a
bonus for harder modifiers; ranks unlock cosmetics only (titles, trails, frames, menu backgrounds).
- [x] **Built** (Nov 2026, protocol 135): `Rank.cs` + profile XP + `rank` tests, the results strip, the lobby badge and
      frame, the menu chip, Records > Rank, the Ember and Gold stardust trails, the Steel and Gold frames, and the Dusk
      Grid background. See "As built" in the doc.
- [x] **Light cycle (Tron) trail.** Built (Nov 2026, protocol 139): Grid wake with Derezz's breakup, cyan only, rank 45 (`Game/LightCycleTrail.cs`); the orange and magenta equip options were not built. Mockup (Nov 2026): [`Mockups/tron-trail.html`](Mockups/tron-trail.html)
      (https://claude.ai/artifact/Ctzk9h9coiwQf4NvEY48hN). A wall of light that snaps to right-angle turns, in four styles
      (A Light wall, B Derezz, C Grid wake, D Twin rail) and three colours (cyan, orange, magenta), proposed at rank 45. Pick a
      style (my pick: B) and whether the colours are equip options.
- [x] **More rank frames (borders).** Built (Nov 2026, protocol 138): all five, drawn by `Game/FrameLooks.cs` on the party card, the menu chip and the cosmetics look card. Mockup (Nov 2026): [`Mockups/rank-frames.html`](Mockups/rank-frames.html)
      (https://claude.ai/artifact/CRkVFKgeCQEWGGFahRY3c3). Today's Steel (12) and Gold (30) plus five new animated ones:
      Circuit 16, Ember Edge 20, Void Pulse 24, Legend Foil 40, Prism 50. Frames are numbered in rank order in the lobby (Gold 2 to 5, hence the bump); with Visual effects off they fall back to a static rim.
- [x] **Lobby Hangout.** Built (Nov 2026, protocol 137): a Hangout tab in the lobby where every player's character walks around
      with WASD, wearing their trail, frame and rank (`Game/LobbyRoom.cs`, `Core/LobbyAvatars.cs`, `Docs/Hangout.md`). No mockup
      first, at the owner's request. Left for later: emotes, a ready pad, a weapon preview spot.
- [x] **More rank trails.** Built (Nov 2026, protocol 136): Frost 14, Storm 18, Void 22 and Prism 35, from
      [`Mockups/rank-trails.html`](Mockups/rank-trails.html) (https://claude.ai/artifact/L5DppgBwZDXzgAaPo2cf2o). Drawing only
      (`TrailPiece` in `Game/RankTrails.cs`, shared with the cosmetics window samples). Trails are numbered in rank order in the
      lobby, so Gold went from 2 to 5, hence the bump. The reward ladder panel goes to three columns. Armory and Bestiary untouched. Left for later: tuning the XP numbers from real play.

### Bigger features (design docs ready)
- [x] **Zombie enemies for Zombie Infection** (built, with the dark green arena). Mockup: `Docs/Mockups/zombie-enemies.html` (published:
      https://claude.ai/artifact/9Dvd5zEz49owzvonTP3hnb). Four green zombies with green particle effects: Shambler
      (wide triangle, slow melee), Runner (narrow long triangle, fast melee, packs of 3), Spitter (pentagon, lobs globs
      that leave acid puddles), Crawler (trapezoid, grabs and roots the player), Bloater (swelling circle, bursts into a slowing cloud), Shrieker
      (diamond, screams to frenzy the horde), Mender (cross, heals and raises the dead faster). Waiting for the owner's go-ahead or
      changes; then build them as new EnemyTypes replacing the stand-ins.
- [x] **Wave editor** (option B, timeline) in F1 → Tools. Generation script done (`Tools/WaveGen/build.sh`, `Data/Waves`). Next: let
      hunts/approaches/paths use designed waves (the Warden Hunt's nine first).
- [x] Zombie Infection modifier, basic version (hordes, cap, the dead rise). Next: zombie looks/types, infected
      co-op players: [`ZombieInfection.md`](ZombieInfection.md)
- [x] **Riot Gear, the first perk (and the perk framework).** Built (Nov 2026, protocol 154; plan in [`RiotGear.md`](RiotGear.md)): the lobby's Perks tab (look A of
      [`Mockups/riot-gear-perk-menu.html`](Mockups/riot-gear-perk-menu.html), https://claude.ai/artifact/WrbA5TsYMgcn6tfe7Rd8ad), the Plate look and HUD chip (look 2 of
      [`Mockups/riot-gear-effects.html`](Mockups/riot-gear-effects.html), https://claude.ai/artifact/241kgcxDtwMbJVEkRijuuG), perks synced through the lobby, the match start,
      the snapshot and saved runs. Blocks one hit, then a 30 s recharge, 0.5 s of invulnerability after a block; burn, gas, acid and lava ticks go through. Needs a try in Unity:
      the Perks tab (clicks, hover tips, seat icons), the plate's look and timing, the HUD chip's place above the XP strip, and how a block feels. The Armory and Bestiary are not
      updated. Replay files are now format 2 (older replays no longer open). Left for later: Contraband and the other bosses' perks, the second/third perk unlocks per boss, the
      no-perks record variant, and a "perks used" line on the run records. Say if reviving should refill the shield (it doesn't now: the timer runs on).
- [x] **Contraband, the second Warden perk.** Built (Nov 2026, protocol unchanged, 154; [`Contraband.md`](Contraband.md)): win the Warden Hunt on Hard or Nightmare to unlock it. The first wave after each boss
      (6, 11, 16...) always has a treasure chest when anyone in the party has it, and your own item from any chest is rare or better. Needs a try in Unity (the chest turning up, the Perks tab card).
      Left out: a marker or call-out for the cycle chest (say if you want one). The Armory and Bestiary are not updated.
- [x] Boss Hunt for the Warden (10 waves, its own enemies). Next: factions + starting builds for later bosses, then Perks:
      [`BossHuntsAndPerks.md`](BossHuntsAndPerks.md)

## Audit findings (October 2026), moved from `HANDOFF.md` §6 (Nov 2026)

#### 6.1 Dead code — already removed

The codebase is kept tidy as features change, so very little was dead. Removed (with the owner's OK): `Game.HavenHeal`
(unused constant), `NetPeer.ConnectionCount` (never called), the Boss Lab seat picker's "no corner" option and a stale
"F1 > Practice Range" line in saved practice files (leftovers of the old F1 Practice Range). Scans covered unused
methods, fields that are written but never read, and unused enum values; nothing else qualified. The **simple test
bot** in `LocalControl` (`Bot` without a `BotLevel`) is **not** dead: most sim tests rely on its predictability (and
the `--bot` launch option uses it). F1 "Bot plays for you" uses the smart bot since Oct 2026.

**Second pass (Nov 2026, protocol 233, replay format 4):** removed the retired Soul Link and Soul Ghost (`SoulGhost.cs`,
`MinionKind.SoulGhost`, `Player.LinkT`/`LinkTo`, `Minion.InvulnT`, the tether drawing), about 30 never-called methods
and constants (old looks such as `DrawStormHammerOld`, UI pieces such as `FancyButton` and `DrawTileRim`, `BathCut`,
`PodWires`, `WardenGate.Rolled`, ...) and `Tools/CompileCheck/UnityStubs.cs` (the compile check builds against
`Tools/Preview/Stubs.cs`). Shared helpers replaced copies: `Targets.cs` (`Targetable`, `BossTarget`, the living-player
picks), `MenuMusic.LoopAndNormalize` and `SoundBank.Partials`. Checked unchanged: RenderCheck (skipping the removed
minion), every sound clip and the music bit for bit, both suites. Still dormant, left for the owner: `ZoneKind.Lantern`.

#### 6.2 Where coding is slow: one feature, many places (highest value)

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

#### 6.3 File organisation

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

#### 6.4 Networking

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

#### 6.5 Gameplay performance

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

#### 6.6 Balance tooling

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

#### 6.7 Suggested order

Low-risk, high-value first: (5) and (6) file splits → (7) test suite layout → (1) weapon numbers in
`WeaponDef` → (8) symmetric serialisation → (2)/(3) registries → (14) balance file. Do each as its own commit,
running both test suites and the compile check after each.

#### 6.8 Work plan (the owner's order, October 2026)

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
   with the owner, see [`Docs/Paths.md`](Paths.md) (decisions, the first pool of 9 paths, screens, engineering
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

**Boss approaches** ([`Docs/Approaches.md`](Approaches.md)): step 1 (framework + the Warden) is built. Next: the
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

## Earlier changes

**Katana feel pass (Nov 2026), protocol 194:** the Steel set of `Docs/Mockups/katana-sounds.html` (`Game/KatanaSoundBank.cs`, new `Sfx`
values, `Swing.Sound`, `FxKind.KatanaHitCue`), a hit-pause on your own lunge (`KatanaLungeHold`) and kill-preview brackets while aiming
(`Game.KatanaWouldKill`). See `Docs/KatanaCombo.md`. Test: `lastfour`.

**Glacial Tomb look (Nov 2026), protocol 185 (no change):** look C (Crystal Reliquary) of `Docs/Mockups/glacial-tomb.html` is in the game as `Game/GlacialTombLook.cs` (see `HANDOFF.md`). The cast's Ring effect now lasts `Game.TombFxTime` (freeze + 1 s) instead of 0.8 s so the look can tell it and run the thaw; nothing else in `Core/` changed. Mockups A and B were not built.

**Parry cancels attack chains (Nov 2026), protocol 183 (no change):** raising the parry calls `ResetAttackChains` (`Core/Katana.cs`,
from `TickParry`): the Katana goes back to swipe 1, its wait is cleared, and a lunge still pulling back never lands (`KatanaLunges`
is taken back by one; `LocalKatana` reads that as a cancel). Earned builds are untouched. Test: `lastfour`.

**Katana chain streak (Nov 2026), protocol 181:** `Docs/KatanaCombo.md` (last section), mockups `katana-chain.html` and
`katana-streak-examples.html`. `Player.KatanaChain` (replicated): an Alt strike that kills its target keeps the meter full and adds
a chain; a survived target ends the streak; no drain while the chain is above 0; at `KatanaChainNeed` 5 the next strike is empowered
(`KatanaEmpDmg` 240 / `KatanaEmpRoute` 120). The strike gained a `KatanaHold` 0.4 s held frame; its look is now one age-driven
`FxKind.KatanaStrike` plus a `FxKind.KatanaCut` per enemy cut (`Game/KatanaLook.cs`: stance, dim, beam, afterimages, flash, letterbox,
halves, shake). Counter: blade notches. Test: `lastfour` (chain, survived target, no drain on a streak, empowered).

**Katana combo rework (Nov 2026), protocol 179:** `Docs/KatanaCombo.md`, mockups `Docs/Mockups/katana-combo.html` and
`katana-look-glow.html`. The sheathe and quick-draw (`SheathT`, `KatanaSheath`, `KatanaDraw*`) are gone. The Katana is
swipe, swipe, lunge (`Player.KatanaStep`, replicated; `Core/Katana.cs`): each step sets its own `FireCd`, a tap during a
step is queued (`TickKatanaFire`), and `KatanaChainWindow` 0.7 s without the next step starts over. The lunge is a 260 px
box Swing (`DrawCut`, drawn as the thrust look) landing 0.15 s after it starts. Every enemy a step hits adds to
`Player.KatanaMeter` (replicated, `Swing.Meter`), which drains at 25/s after 1.2 s without a hit. The Alt strike reuses
`Msg.WeaponDash` with `DashKatana` (no new message): holding the weapon-action key at a full meter aims (`LocalKatana`,
cursor-nearest enemy, `Game.KatanaAiming/KatanaMark`), letting go sends the enemy's position, the host spends the meter,
sets `InvulnT` and, `KatanaWind + KatanaCut` later, deals 120 to the target and 60 along the line
(`TickKatanaStrikes`), broadcasting the new `FxKind.KatanaStrike` (`Game/KatanaLook.cs`). Your own game holds you still for the
0.35 s stance, then slides you to 52 px behind the target in 0.14 s (`DashT`). The lunge steps you 110 px forward (`Player.KatanaLunges`, protocol 179: your own game moves you when it changes). Not built from the design doc: a time slow while aiming (none, to keep co-op fair). Test: `lastfour` (chain, queue, reset, meter,
drain, strike).

**Tank gun animations (Nov 2026), protocol 145 (no change):** the owner's go-ahead on
`Docs/Mockups/tank-gun-animations.html` (https://claude.ai/artifact/QeTMr1Gr6n1KoJKymyutE3), "make sure lobby previews match".
`Game/TankGunLooks.cs`, with Visual effects on, drawing only. Scattergun: flash cone, sparks, 0.14 s recoil, then a pump (the
front hand slides back and forward, 0.14 to 0.34 s) with a red shell dropping out at 0.24 s. Flak Cannon: big flash and
smoke, 0.3 s breech recoil, the orange bands glowing hot for 0.8 s, the shell lifted on an arc with a ground shadow (its
glow rides with it), and an airburst of flash, orange disc, two rings, shrapnel and smoke (`IsFlakBoom`: the Boom in the
shell's colour). Riot Repeater: small flash, 0.09 s bolt kick, a brass casing per round, one magazine rib lighting, and
the rounds as glowing orbs with a white core and a tapered tracer. `CharacterModels.cs` takes the pose from
`TankGunPose`; `ShotLooks.cs` maps `TankShot` to the Riot Repeater as well and `FlakShell` to the Flak Cannon. The lobby's
preview runs the same Juice pass, so it matches with no extra code (`TANKGUN=0|1|2` in `Tools/Preview` shows it).
The Armory pages are not updated.

**Arc Lightning, style A "Thunderhead" (Nov 2026), protocol 129 (no change):** the owner's pick from
`Docs/Mockups/arc-lightning.html`, "make sure the lobby preview matches". `Game/ArcLightningLooks.cs`, with Visual effects
on: `DrawArcFieldFx` and `DrawArcCharge` hand the strip and the charge to the glow pass (`DrawArcLightningGlow`): a halo
and static off the staff growing with the charge, your own strip as a dashed outline with a pulse and a crackling marker
(Barrage's strips too), and on release the spine bolt (80 ms out, re-rolled every 33 ms, the same on every screen),
forks to the edges, a flash at the end and a crackle on each enemy in it for the stun. The lobby's preview ran the glow
pass already, so it shows the release; it never drew this weapon's charge, so `DrawShowcase` now calls `DrawArcCharge`
(which the glow pass covers with effects on). Preview: `SHOTW=ArcLightning` with `SHOTT=<s>` or `SHOTC=<0..1>`; `juice.sh`
checks a real cast. Effects off: unchanged (render check IDENTICAL).

**Chain Light, style A "Radiance" (Nov 2026), protocol 127 (no change):** the owner's pick from
`Docs/Mockups/chain-light.html`, "make sure the lobby preview matches it". `Game/ChainLightLooks.cs`, with Visual effects
on: `DrawLightConeFx` and `DrawBoltFx` hand Chain Light's cone (`FxKind.LightCone`, only Chain Light's) and its bolts (the
only bolts in the light's warm `HolyShot` colour) to the glow pass, where `DrawChainLightGlow` draws the mockup's fan
(blooming 0.12 s, fading 0.33 s; white at your hand, the light a quarter out, clear at the reach; soft edges from 80% to
115% of its half angle; colour per vertex), nine flickering god-rays, motes, and per enemy a soft beam, a white core, a
halo and a ring. The lobby's loadout preview runs the glow pass (clipped), so it shows the same. Side by side with the
mockup at the game's scale (preview `SHOTW=ChainLight SHOTT=<s>`) it matches; `juice.sh` checks a real cast. Effects
off: unchanged (render check IDENTICAL).

**Every custom shot checked against the projectiles mockup (Nov 2026), protocol 127 (no change):** each weapon fired by
the game's own code (preview flag `SHOTW=<WeaponId>`: three volleys at 0.06 / 0.18 / 0.3 s) beside the matching
`Docs/Mockups/projectiles.html` projectile drawn by its own `draw()` at the game's scale. Close already: Twin Rifle (after
its fix), Railgun, Pistol, and the bespoke Fireball and Shuriken. Fixed: the **Scattergun**'s pellets each had the big
shared glow (a column of blobs; now the mockup's 7 px pin of light); the **Prism Beam** gets the mockup's fading trail
(`TaperTrail`, shared with the tracer) and 16 px glow; **Arcane Missiles** glow at the exhaust (12 px orange) instead of
the body; the **Golem Staff**'s boulder no longer glows (it's a rock); **Blood Bolts** are the mockup's teardrop with a
dotted trail and a highlight, unglowing. `ShotGlow` holds each shot's glow. Effects off: unchanged (render check IDENTICAL).

**Nebula moves visibly again (Nov 2026), protocol 128 (no change):** owner: the background is no longer animated.
It was, but the Nebula's clouds drift ~6 px/s, which the old stacked discs' sweeping ring edges made easy to see and
the smooth glows hide (two frames a second apart: 11% of pixels changed before, 0.2% after). The clouds now loop wider
and faster and swell and fade (25%). SoftGlow also draws 8 bands of 32 instead of up to 12 of 40, about a third of the
vertices.

**Smooth menu-background glows; no arena vignette (Nov 2026), protocol 128 (no change):** owner: the backgrounds'
gradients showed hard steps behind the lobby (the picker's small thumbnail hid them), and remove the vignette.
`SoftGlow` (MenuBackgrounds.cs) stacked 3-6 flat discs, so every disc edge was a visible ring. It now draws bands of
shaded triangles with a smoothstep falloff (`GlowFalloff`), blending continuously from the centre to nothing. Nebula,
Spiral, Synthwave, Orbits, Ember Storm, Frost Peaks, Event Horizon and the rest that use it are affected. `DrawMood`
(Juice.cs) no longer draws the dark band round the arena's edges (or the Warden searchlight's darker one); the path/boss
tint and the red hurt corners stay, and so does the game-over/victory vignette in RunEnding.cs.
**Tracer rounds as in the mockup (Nov 2026), protocol 127 (no change):** owner: not sure the Twin Rifle's tracers glow
properly. Compared with `Docs/Mockups/projectiles.html` at the same scale: the game drew a 25 px streak of two flat-ended,
flat-coloured lines and the general bullet glow (0.47), where the mockup has a streak as long as the shot's last 10
frames, tapering 3 -> 0.6 px and fading 0.8 -> 0, a white 3 px head and a 14 px glow at 0.8. `ShotLooks` now draws the
mockup's streak (colour per vertex, never longer than the shot has flown: `Life0 - Life`) and `ShotGlow` gives tracers
(Twin Rifle, Burst SMG) the mockup's glow in the glow pass. Effects off: unchanged (render check IDENTICAL).

**Melee: one effect per Halberd sweep; glow in the lobby preview (Nov 2026), protocol 127 (no change):** owner: the
Halberd looked like the old design again, and the lobby previews lost their gradients. The Halberd sent one Slash per
step (six), which MeleeLooks stitched together with neighbour-matching code (`StepNeighbours`, `NewerStepThan`); fragile,
and unlike every other weapon. Now its first step sends one Slash for the whole sweep (`Swing.FxAim`/`FxHalf`; the
other steps have `NoFx`), and MeleeLooks plays it as an ordinary sweep over the steps' 0.21 s at a steady speed, in step
with the hits (a second pass from an extra projectile sweeps back); the step code is gone. With effects off it's the old
arc. The lobby's loadout preview never ran the glow pass (its comment said glows and gradients couldn't be clipped), so
when the melee energy moved into the glow pass the preview lost it: `VTri` now clips like `Tri` (`ClippedVTri`, colours
blended along each cut) and the preview runs `DrawGlowPass` (melee energy, bullet and blast glows) clipped to its window.
The preview tool's UI layer (GL drawn in menus) now uses the GPU-like rasterizer too. `juice.sh` fires real Greatsword and
Halberd swings through the game's code and checks every frame, and that a Halberd sweep is one effect. Then (owner: "the warhammer renders the new and old animations"): a Warhammer slam
sends a blast (`FxKind.Boom`) beside its box (`Jab`), and the blast still drew its old ring and glow under the new
slam. The blast's colour is now a named constant (`Game.HammerBoomColor`) that also marks it as the hammer's;
`HammerBoomDrawnByMelee` leaves it out (ring and glow) with Visual effects on. `juice.sh` checks a real slam.

**Colour space and glows (Nov 2026), protocol 127 (no change):** owner: the game's glows and gradients don't match the
mockups or the preview images (the lobby background previews, the bullet glow in Settings). Cause: ProjectSettings
aren't in git, and a new Unity 6 project defaults to the Linear colour space; the game draws with raw `GL.Color` vertex
colours, which Unity doesn't convert there, so colours and see-through layers came out lighter and greyer (the owner's
melee screenshot had a mauve-grey arena) and glows blended differently. `ArenaEditor.EnsureSetup` now sets
`PlayerSettings.colorSpace = Gamma` (and logs it); `ArenaApp.Awake` turns on 4x MSAA and logs if the colour space is still
Linear. The Settings effects preview draws real additive glows (`GlowDisc` in the glow material) instead of flat discs.
Preview: `LINEAR=1` simulates a Linear project (`raster.py`), which reproduces the washed-out look.

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
