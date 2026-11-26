#!/bin/bash
# Renders a game screen to a PNG without Unity: Unity is replaced by stubs (Stubs.cs) that record the GL
# triangles and IMGUI draw calls, and shot.py composites them with Pillow.
#   Tools/Preview/shot.sh <out-name> <mode> [wave] [seconds] [players]
# picker: GOLDINSTEAD=1 (on the Bazaar: the "Gold instead" key under the row).
# Modes (see ShotModes.cs; flags in ShotScenes.cs): hud, dev, bg, lobby, tab, picker, legendary, ...
# Env flags tweak the scene, e.g. KNIGHT=1 (Knights), POSE=1 (fixed pose), THRUST=0.3 (Lance jab frame),
# DAZE=1 (three enemies dazed round you; mode hud), BOW=0.9, MASTERY=1, BG=3 (background number for 'bg'), MOUSE=x,y (hover for lobby modes), SOLO=1,
# menu: PLAY=1 (the Play picker; NOSAVES=1, MORE=1 extra saves, OLDER=1 the older runs, PAGE=n), MENUT=<s> (the clock: 3 = rooms built), BG=28 (Holo Floor).
# records: SECTION=4 (Recent runs), RTAB=1 (Trends), RSEL=n (the open run), MOUSE=x,y (hover).
# DOWNED=down:<s since the hit>|revive:<0-1>|up:<s since standing> (you downed, being revived, getting up; mode hud, 2 players).
# DRILL=<steps done>,<seconds>[,done[,hits]] (a Training drill: Move and Aim, DRILLID=1 Dodge (DRILLHIT=1 just hit), DRILLID=2 Wall Kick, DRILLID=3 Shoot, DRILLID=4 Parry, DRILLID=5 Read the Tells; the drill card, the result card; mode hud).
# QUICKMET=<n> (the lobby's Quick Play tab with n bosses beaten; mode lobby).
# TUTORIAL=<lesson 0-4>,<steps done> (the first-run tutorial prompt; mode hud).
# RAILGUN=<s> (the Railgun's scripted shot: RAILHOLD=<s> held, default 1.6; RAILAIM=<deg>; RAILHEAT=<0-100>; RAILDUMP=1 Heat Dump instead; mode hud).
# NEEDLES=<s> (the Healer Ninja's Acupuncture Needles: healing needles through a grunt into a teammate, Bloodletting in two grunts; mode hud, 3 players, ZOOM=2.2).
# OFUDA=<s> (the Healer Ninja's Ofuda: talismans bending onto a hurt teammate, a Blast Ofuda stuck to a grunt and one on the ground; mode hud, 3 players, ZOOM=1.6).
# ARCLOCK=<s> (Arc Lightning's lock-on: hold from 0, let go at 1.5 s; ARCFORK=1 the Forked Volley instead; mode hud).
# OATHPICK=1|0 (Guardian's Oath waiting for the click: cursor on an ally, or off; mode hud, 3 players).
# SPRINT=<s> (the Dodge's held sprint that long in, running right with its fading wake; mode hud).
# RIOT=ready|wait,17|block,0.1 (Riot Gear: the plate, the recharge, a block that long ago; use with mode hud).
# MOMENTUM=<0-5> (the DPS passive's stack pips round you; mode hud, 2 players).
# IRONHIDE=<shield 0-75>[,<wait s>] (the Tank's Ironhide plates; mode hud, 1 player, ZOOM=3).
# HEALAURA=1|2 (the Healer aura, HealerAuraLook.cs: a Tank near the Healer (2: near the rim) and a DPS outside; mode hud, 3 players, ZOOM=2.2).
# NONSHOOT=1 (the Hook Thrower caught mid-move: aiming, harpoon out, spinning, open after a miss).
# PORTALS=1 (spawn portals: opening, letting a burst out, a flank behind you with its arrow, closing; SpawnPortalLook.cs; PORTALSURGE=1 in a surge, PORTALSEAL=<0-1> a sealed flash that far through).
# CULT=1 (the Twin Reapers' cult mid-move: Wisps, Acolyte, Gravedigger, Shade, Bound pairs (tether, revive), Scythe Wraith, Lamplighters, Mourner).
# HOLDCELL=<s> [HOLDCELLS=2] | HOLDLOCK=1 | HOLDNET=<s> (the Warden's net and Holding Cells, ShotHoldingCell.cs; mode hud, wave 10, 2 players).
# DEVOURAT=x,y[,deg] (pin the Devourer there facing deg, default 90, and clear shots: its Lamprey body, DevourerBodyLook.cs; with DEVOUR=<health> [DEVOURTURN=<turn>] ZOOM=3 CAMAT=640,345 shot.sh x hud -50 9 1).
# HERALDSHEET=1 (the Storm Herald's body in its four phases, HeraldBodyLook.cs; ZOOM=1.38 CAMAT=640,360 shot.sh x hud 3 1 1); HERALDSHEET=poses:<1-12> one of its poses (ZOOM=2.2 CAMAT=herald). CAMAT=herald: a zoom centred on the Herald.
# WARDENSHEET=all|<1-15> (the Warden's poses, WardenBodyLook.cs: all fifteen in a grid, or one in the middle; NINJA=0 ZOOM=2 shot.sh x hud 3 1 1).
# TUSKER=<s since the cast> [TUSKERCAP=1] (Tidal Tusker, ShotTusker.cs; ZOOM=1.6 CAMAT=640,450 shot.sh x hud 1 6 1).
# FORBIDDEN=<s since the cast> [FPNEXT=<1-5>] (Forbidden Path, ShotForbiddenPath.cs; ZOOM=2.2 CAMAT=630,460 shot.sh x hud 1 6 1).
# CROWD=<n> (a Warden mob of n round you, for a crowd's draw cost; NINJA=0, e.g. shot.sh x hud 3 1 2). VTXTRACE=1 (any shot: print the world frame's top vertex sources; run mono shot.exe in out/ to see it). VTXDEPTH=<n>: that many stack frames a source (default 5).
# PERFRUN=<secs>[,boss] PERFIDLE=<secs> [PERFMAP=<pieces>] (draw cost: a Quick Play breach wave drawn every frame, printing vertices and draw ms; PERFMAP: a grown map; ShotPerf.cs; e.g. shot.sh x hud 7 1 2; with SURVIVAL=840 SURVMARKS=55: Survival's room).
# SWORD=1 (the Acolyte's sickle cut in each phase: closing in, wind-up, cut, recovery).
# LUNGE=0.5 (the Lunger, Shiv, Scythe Wraith and Riot Shield part-way through their wind-up: the shared lane).
# TASER=0.3 (the Taser Guard charging, a Fork Bolt in flight over a field of bullets, and the hit on you 0.3 s ago; TASER=-1 for no hit; mode hud).
# SECURITY=0.2 (the Security Guard: locking on with its lane, just fired with a casing and a tracer, reloading, ready and empty; mode hud).
# SECSHIRT=1 (a riot Guard, then Security Guards walking, locking on, just fired, reloading, close up; SECSHIRT=2 Taser Guards idle and charging, SECSHIRT=3 Riot Shields, SECSHIRT=4 Gas Grenadiers; mode hud, KNIGHT=1 ZOOM=2.6).
# AIM=0.7 (the aimed shooters winding up: Sniper, Shard, Shade, Taser Guard, Grunt with their aim lines).
# TAGS=1 (a row of enemies on first sight, each with its name and job tag; uses a fresh profile).
# WAVECLEARED=1 WCWAVE=17 (the between-waves box after wave 17: the new-enemy chips for wave 18).
# STAMPEDE=<s since the cast> [STAMPEDEMASTERED=1] (the Tank Summoner's Stampede: the gate, the herd charging, fading; use 0.2, 0.7, 1.2; mode hud).
# SENBON=<needles>[,<s since the last cast>[,<enemies>]] [SENBONCAP=1] (the DPS Ninja's Senbon Seekers: the ring of needles, new ones coming out under 0.35 s, needles curving in on enemies 170 px off; ZOOM=2, mode hud; ShotSenbon.cs).
# LANDSHARK=<s since the cast> [LANDSHARKMASTERED=1] (the DPS Summoner's Land Shark: the puddle, the fin, the leap and bite, the drain; use 0.6, 1.0, 1.1, 1.25; mode hud).
# HEMODPS=spray|debt|rush (the DPS Hemomancer's Arterial Spray, Blood Debt, Vein Rush; DEBTSETTLE=paid|forfeit, RUSHT=<s>; mode hud).
# SANCTUM=<s into the scene> (the Healer Knight's Sanctum Glass like the mock-up: raised at 0.5 s, shots from 0.4 s, a hurt friend; SANCTUMCAP=1 mastered; mode hud, 2 players; ShotSanctumGlass.cs).
# GALEWALL=<s into the scene> (Gale Wall like the mockup: cast at 0.5 s, shots from 0.4 s; GALEBLAST=<s> unleashes it then; GALECAP=1 mastered; ShotGaleWall.cs).
# QUEENHOLD=swell,<0-1>|lay,<s>|dive,<s> (with FLANKERS=1: the Brood Mother swelling before her eggs, laying them, in a royal dive).
# QUEENSTAGE=<0-3> (with FLANKERS=1: the Hive Queen's Brood Mother body in Swarm, Honeycomb, Frenzy, Last Stand; ZOOM=2.6 CAMAT=800,215, mode hud 3 1 1).
# WWARD=<s into the scene> (Water Ward: a cast every 3 s from 0.2 s; grunts walk in from WWENEMY s (default 6.5, -1 none); WWSHOOT=1 shots from above from 7 s; ShotWaterWard.cs).
# BOWFOCUS=rest|draw|bar|focus|miss|swing|scatter,<s> (the Mender's Bow: Focus bar, Focus arrow, the arrow swung from the quiver, Scatter; mode hud, 1 player, ZOOM=2; ShotBowFocus.cs).
# GUSTGATE=<s into the scene> (Gust Gate: the ring cast at 0.2 s, an ally running through it from 0.5 s, a grunt inside pushed out; mode hud, 2 players, ZOOM=2.2; ShotGustGate.cs).
# ENGINEER=pylon|breach|real|bots ENGT=<s> (the Engineer mockup in the real arena; breach needs EXPANDMAP=0, BREACHSHAPE=stash|cross|..., BREACHLOOT=0|1|4; ENGMODEL=0-2 the Engineer look; ShotEngineer.cs). Mode englook: ENGSHEET=0 looks, 1 kit (EngineerModel.cs).
# ARCANIST=tank|healer|dps (the Arcanist's kit drawn by the game: shards, Kinetic Grasp and Grand Sigil; Glyph Stylus and Sanctum Sigil; Dancing Blades, Runic Array and Telekinetic Crush; ShotArcanist.cs).
# COVER=<s into the scene> (Deployable Cover like the mockup: covers thrown at 0.5, 1.5 and 2.5 s; COVERWEAR=1 wears two; COVERBREAK=<s> breaks the first then; ShotCover.cs).
# CLAYMORE=<s into the scene> (Claymore: claymores planted at 0.2, 0.5 and 0.8 s, arming 0.6 s; CLAYTRIP=<s> walks a grunt into the first one's cone then; CLAYCAP=1 mastered; ShotClaymore.cs).
# DECOY=<s into the scene> (Decoy Dummy: dummies thrown at 0.3, 1.0 and 1.7 s, grunts shooting the nearest; DECOYWEAR=1 wears the second; DECOYPOP=<s> pops the first then; ShotDecoy.cs).
# GRAZE=<s into the scene> (Graze: shots skimming past you above and below from 0.3 s, the streaks and the ultimate's tile flash; ShotGraze.cs; ZOOM=2).
# CAUTER=1 (Cauterize on four bleeding Brutes at four stages of its look; ZOOM=2.4; ShotCauterize.cs).
# DART=<s into the scene> (Poison Dart played through the game like the mockup: dart at 0.5 s, deaths at 2.2 and 3.9 s; DARTCAP=1 mastered; ShotPoisonDart.cs).
# MOMENT=multi|clear|level|lowhp|marks MOMENTN=<n> MOMENTT=<s> MULTISTYLE=1 (the juice moments: multikill call, last kill, level shock, heartbeat, floor marks; ShotJuice.cs).
# SHOCK=<s since the cast> (the Shockwave ability played through the game; SHOCKCAP=1 mastered; ShotShockwave.cs).
# FLAMEWALL=<s since the cast> (Flame Wall cast at your cursor, shots flying in; ShotFlameWall.cs).
# TURRET=<s> / NOVA=<s> since the cast (the Turret and Nova abilities played through the game; ABILCAP=1 mastered, TURRETEND=1 its last seconds; ShotSentryNova.cs).
# WOLFPACK=pack|alpha|merge WOLFT=<s> (the Wolf Whistle's pack hunting three grunts: four wolves with two hurt and kill pips, an alpha of three, or five merging at 0.6 s; ShotWolfPack.cs).
# BONEGUARD=<s into the scene> (the Bone Guard's Marrow Tether, then its Ossuary Wall Alt at 1.6 s; ShotBoneGuard.cs).
# PHALANX=<s since the cast> (Bone Phalanx played through the game; PHALANXCAP=1 mastered; ShotPhalanx.cs). FXOFF=1: Visual effects off (plain looks).
# KNIGHTDPS=caltrops|gauntlet|dive|bridge KDT=<s> (the DPS Knight's abilities replaying the mockup's scenes through the game; KDW=<0-2> the weapon; crop (352,200)-(928,520) to set beside the mockup; ShotKnightDps.cs).
# WILDCARD=1 (lobby: you as a Wildcard, Twin Blades, Shadow Step, Blade Fan on Q, Death Lotus; the class wheel).
# NIMBUS=<s since a strike, -1 none> [NIMBUSSTORM=<s into the Eye of the Storm> NIMBUSBOLT=1] (the Nimbus, Anvil look; mode hud).
# pacman (mode): PACKEYS=l1.7,u0.6 (steer left then up), PACFRIGHT=5 (ghosts blue), PACDIE=0.7, PACOVER=1, PACPAUSE=1 (PacmanUi.cs).
# MECH=Easy|Normal|Hard|Nightmare (the run's mechanics tier, Docs/DifficultyTiers.md; e.g. the Warden's bar has no Last Stand cell on Easy).
# WARDENTRANS=<to 1-3>,<s> (the Warden's 5 s phase change into Lockdown/Riot/Last Stand that far in; a negative s: in that phase; WARDENHP=<health>[,<armor>]; KNIGHT=1 ZOOM=2.6 CAMAT=800,470 shot.sh x hud 3 1 1).
# RIOTERS=1 (a pack of Rioters with a Guard and a Thug; mode hud, ZOOM=3 for a close look).
# THUGRAGE=1 (four Thugs at full, 2/3, 1/3 and 5% health: the red halo growing; NINJA=0 ZOOM=3).
# PRISONERS=1 (Thugs and Shivs, both Shiv kinds walking, winding up and lunging; mode hud, ZOOM=3).
# TURNKEY=1 (the Turnkey running, planting, building; Floor Mines, Shackle Traps, a Mop Slick; NINJA=0 ZOOM=2.2; TurnkeyLook.cs).
# TKDOORS=1 [TKT=<s>] [TKCYCLE=1] [TKCLOSE=1] [TKBEEP=1] (Cell Doors in the outer wall: calm, due, opening, jammed, being built; the traps; EXPANDMAP=0 MAPVIEW=2,2,1 NINJA=0 ZOOM=2 shot.sh x hud 3; ShotCellDoor.cs).
# WARDENCREW=1 (Guard, Riot Shield, Gas Grenadier, Taser Guard, Security Guard in a row; WARDENCREW=2 mid-move; NINJA=0 ZOOM=3.2; OfficerLook.cs).
# GUARD=1 (the riot Guard walking, drawing back and swinging forehand, then backhand; mode hud, KNIGHT=1 ZOOM=2.6; GuardSwing.cs). GUARD=2: four Guards walking in at 200/150/110/70 px (the ready tell; ZOOM=2.2).
# THUG=1 (the Thug walking, early and late in its punch wind-up, then mid-jab; mode hud, KNIGHT=1 ZOOM=2.6; ThugPunch.cs).
# GASLOB=<s into the wind-up> (the Gas Grenadier's overhand lob: ring, arm, canister; mode hud; GasLobLook.cs).
# GAUNTLET=<clock s> (with GRIDCORE=1 EXPANDMAP=0: the Relic Gauntlet, RelicGauntlet.cs, at that laser clock; GAUNTLETAT=<0-1800> where you stand, GAUNTLETSPOT=<n>; ZOOM=0.45 sees it whole; clip.sh ... GAUNTLET=0:7:0.1 animates it).
# ZOOM=1.8 (the scroll-wheel zoom on you, CameraZoom.cs).
# FPV=<yaw deg>|big[,<pitch deg>] (the first-person view looking that way, FirstPerson.cs; 0 east, 90 south; big: at the biggest enemy; FPVKICK=<1-0>[,<swings>] the weapon mid-kick; FPVWEAPON=<WeaponId> hold that weapon).
# MAPVIEW=<from>,<to>,<t> (the Map key's switch: 0 minimap, 1 full map, 2 off, t of the way; MapView.cs), MAPCYCLE=<s> (a scripted M, M, M for clip.sh); with EXPANDMAP=6 ZOOM=1.5.
# NADET=<s> (the Twin Rifle's Rocket assist that long after a launch), NADELOAD=<s> (a tube loading; TwinNadeLook.cs);
#   with RIFLEFIRE=1 (holding fire: the Twin Rifle held wide, Spread arms) and CROSSAT=<px> (the cursor's distance).
# BALLISTA=<s since the cast> [BALLISTACAP=1 mastered] [BALLISTARIDE=<s>, -1 nobody] (the DPS Knight's Ballista played through the game, shot.sh x hud 1 6 1: falls 0-0.6, you climb on at 0.8 and fire a bolt every 0.9 s; use 0.3, 1, 3.2, 8.2; ShotBallista.cs).
# DEPLOYTURRET=<s since the cast> [DEPLOYCAP=1 mastered] (the DPS Soldier's Deployable Turret played through the game, shot.sh x hud 1 6 1: drop 0-0.55, unfolds to 1.1, fires to 11.1 (spool full by ~2.3), overheats, blows at 11.9; use 0.3, 0.8, 3, 6, 10, 11.5, 12; FXOFF=1 plain; ShotDeployTurret.cs).
# CANNON=<s since the cast> [CANNONCAP=1 mastered] (the DPS Knight's Cannon Call played through the game, shot.sh x hud 1 6 1: flare 0.08-0.3, ball drops 0.35-1.25, slam at 1.25, crater to 4.25; use 0.2, 0.9, 1.15, 1.3, 1.5, 2.5; ShotCannonCall.cs).
# THORNBLOOM=<s since the cast> [THORNCAP=1 mastered] (the DPS Hemomancer's Thorn Bloom played through the game, shot.sh x hud 1 6 1: needles fly 0-0.2, tendrils whip on to ~0.9; use 0.05, 0.15, 0.3, 0.5, 0.8; ShotThornBloom.cs).
# BAMBOO=<s since the cast> [BAMBOOCAP=1 mastered] (the Tank Ninja's Bamboo Grove played through the game, ZOOM=2 shot.sh x hud 1 6 1: wind-up 0-0.35, rings burst 0.35-0.7, fence to 3.9; use 0.2, 0.42, 0.6, 1.0, 2.5; ShotBambooGrove.cs).
# BELL=<s since the cast> [BELLCAP=1 mastered] (the Tank Knight's Siege Bell played through the game, ZOOM=1.3 shot.sh x hud 1 6 1: bell down 0-0.22, tolls 0.3/0.8/1.3, shockwave 1.3-1.75, bell gone 2.45, stuns to ~3.8; use 0.4, 1.0, 1.45, 1.6, 2.4; ShotSiegeBell.cs).
# CHAKRA=<s since the cast> [CHAKRACAP=1 mastered] (the Healer Ninja's Chakra Reserves on a hurt teammate, ZOOM=2 shot.sh x hud 1 6 2: thread 0-0.22, lotus to 0.95, Surge to 5; use 0.1, 0.3, 0.6, 2.0; ShotChakra.cs).
# ROD=<s since the cast> (you take Lightning Rod, the tank Elementalist's second ultimate, and cast it that long before the shot; mode hud).
# TANKALT=<s> (the Tank Soldier's Alt fires: Double Barrel that long after firing, shells pips, Proximity Shells, Tear Gas; TankAltLook.cs).
# HORSE=<s since the cast> [HORSEALL=1] [HORSEWEAR=<0-1>] [HORSEEND=broke|sally] (the Tank Knight's Trojan Horse rolling out, teammates inside shooting arrows; HORSEALL: you too, enemies go for it; mode hud, 3 players, ZOOM=2; ShotTrojanHorse.cs).
# HONEYBEE=<s> (the Summoner DPS Honeybee Staff that long into a scripted loop: clicks that swarm a dummy at 0-0.8 s, the honey spray held 1.0-1.9 s, a harvest at ~2.5 s; mode hud; HoneybeeLook.cs).
# BONESPEAR=<s> (the Hemomancer DPS Blood Brawler that long into its loop: Pulse 0 s, Reel 0.45, Heart Lift 0.95, Marrow Burst 1.5, Alt (Sympathy) 2.4, Pulse 2.7 and Reel 3.1 echoing; BSAIM=<deg> tilts it; BSHAND=1 centres on the bleeding hand; BloodBrawlerLook.cs).
# EMBOLUS=<s> (the Hemomancer DPS Embolus Spears that long into the mockup's loop: throws at 0 and 0.3 s, recall at 1.7 s, throws at 2.6 and 2.9 s, Alt burst at 4.1 s; ESAIM=<deg> tilts the aim; EmbolusLook.cs).
# NODACHI=<s> (the Tank Ninja Nodachi that long after Fire is first held: NODGUARD=1 starts in Mamori with shots flying in, NODALT=<s> presses Alt then; NodachiLook.cs).
# NUNCHAKU=<s> (the Ninja DPS Nunchaku, Twin Storm, that long into the mockup's loop: Fire held 0-1.85 s for the combo, Alt at 2.1 s (the Twin Windmill swats shots from the right), the Twin Counter at 3.7 s; NunchakuLook.cs).
# BULWARK=<s> BBMODE=chain|guard|brace|bash|melee (the Tank Knight's Bulwark Barrier that long into a scene: Fire held (the chain, then the Shield Throw from about 1 s), shots at the plate, braced soaking them, a full-Ram Shield Bash at 0.3 s, or braced with Brutes walking into the plate; mode hud; BulwarkBarrierLook.cs).
# FROSTLANCE=<s> (the Tank Elementalist's Frost Lance that long into its chain, Fire held: Shiver Crescent at 0, Backhand Rime 0.34 s, Frost Waltz 0.68 s, Crystal Fan 1.08 s, Avalanche Slam 1.5 s; FLALT=1: Absolute Zero at 0; FLCHILL=<n> chill on the grunts; mode hud; FrostLanceLook.cs).
# BLOODBLAST=<s> (the Tank Hemomancer's Blood Blast chain that long in; BBSTEP=n start step, BBALT=1 Crimson Tide, BBSTACKS=n BBKILL=<s> the Blood Vortex; mode hud, ZOOM=1.8).
# BUMBLE=<s> (the DPS Engineer's Bumble Remote that long into a fight: the Bumble Bot at the cursor by a pack, stinging it, Fire held from BUMFIRE s (0.6); BUMALT=<s>: the Swarm Burst pressed then; mode hud; BumbleRemoteLook.cs).
# WYVERN=<s> (the Tank Summoner's Wyvern Staff that long into a loop: hovering with its gale until 1.0 s, then Fire held; WYALT=1: the Wyrm Slam at 1.0 s instead; mode hud).
# TESSEN=<s> (the Healer Ninja's Twin Tessen that long into the Crane Dance, Fire held: the Crane Bloom about 1.25 s, its wave out by 1.45 s; TTALT=<s>: Alt held that long then let go, the Kamikaze Gust; TTBREEZE=100: a Typhoon; mode hud; TwinTessenLook.cs).
# HEARTFLAIL=<s> (the Healer Hemomancer's Heart Flail that long in; HFMODE=swing|spin|throw|burst: a click, Fire held, Alt (Open Heart), the beat at 180 for a Cardiac Burst; mode hud; HeartFlailLook.cs).
# WINDCLASH=<s> (the Tank Elementalist's Wind Clash that long into its combo, Fire held: crescents at 0 and 0.34 s, lance at 0.68 s, ring at 1.12 s; WCALT=1: the Tempest at 0 with enemies shooting; mode hud; WindClashLook.cs).
# STONESPEAR=<s> (the Elementalist DPS Stonespear that long into the mockup's loop: slam at 0, throws at 1.3 and 1.85 s, Alt volley at 2.6 s; SSMOVE=1 walking down (the spears trail); SSTECT=1 with Tectonic; mode hud; StonespearLook.cs).
# GRENADE=<s> (the Tank Soldier's Grenade Launcher that long into a fight: fire held at a pack GLAT px ahead; GLSMOKE=1 Alt then smoke canisters from 0.6 s; GLSWAP=1 Alt only; GLIDLE=1; mode hud; GrenadeLauncherLook.cs).
# DOUBLEBARREL=<s> (the Soldier DPS Double Barrel that long into a fight: fire held at three dummies in a line; DBBOTH=1 Alt both barrels at 0.3 s; DBIDLE=1 not firing; mode hud; DoubleBarrelLook.cs).
# STATICSHOCK=<s> (the Healer Elementalist's Static Shock that long into a fight: fire held at a pack, a teammate in the cone gets Overcharge; SSALT=1 the Arc Cannon at 0.1 s through a teammate; SSHEAT=<s> every enemy already that long in the cone (the ramp); mode hud, 3 players; StaticShockLook.cs).
# HIVECENSER=<s> (the Healer Summoner's Hive Censer that long into a scripted pour: on a pack until 1.2 s, by a teammate (chitin) until 2.0 s, then the pack again; HCALT=1 Hive Call at 2.4 s; HiveCenserLook.cs; shot.sh x hud 3 1 3).
# TWINPISTOLS=<s> (the Healer Soldier Twin Pistols that long into a fight: fire held at a pack 260 px ahead; TPLASER=1 Alt (Laser Sights) at 0.1 s; TPIDLE=1 not firing; TwinPistolsLook.cs).
# WHIP=<s> (the Revolver's Fan stance that long into a fight: grunts and enemy shots in front, a click (the whip) at 0.1 s; WHIPIDLE=1 no click; WHIPFAN=1 a second click (the fan) at 0.3 s; mode hud; PistolWhipLook.cs).
# UNDERTOW=<meter>,<s held> (the Repulsion Cannon's Undertow meter, and its pull held that long on a pack of grunts; mode hud).
# TWINNADES=<loaded>,<next tube 0/1>,<charge> (the Twin Rifle's grenade tubes and pips; with CAST=Airstrike CASTT=<s> for the Airstrike).
# JUGG=<s since cast> JUGGARMOR=<0-1> (the Tank Soldier's Juggernaut Suit: plates, armour bar, flak tracers; mode hud).
# ARMORDROP=<s since the cast> [ARMORCAP=1] [ARMORHIT=<armor left>] (the Tank Soldier's Armor Drop: crate, vests, armor shell and bar; mode hud).
# VORTEX=<s since cast> VXMASTERED=1 (the Tank Elementalist's Vortex: wind-up, storm, grunts swept round, shots swept away; mode hud; VortexLook.cs).
# RAVAGER=<s since cast> [RAVAGERCLICK=0] [RAVAGERSTILL=1] (the DPS Hemomancer's Blood Ravager walking right through grunts, clicking over the last 0.4 s; ShotBloodRavager.cs; ZOOM=2.2, mode hud).
# BOIL=<s since cast> [BOILHP=<0-1>] [BOILEND=1] (the DPS Hemomancer's Blood Boil circling and firing at grunts, health eased down to BOILHP; BOILEND=1 ends it 0.4 s before, for the steam; ShotBloodBoil.cs; ZOOM=1.8, mode hud).
# BOLT=<s since cast> (the DPS Elementalist's Living Bolt: 0.3 the channel, 4 electrified with three dashes' residue, 9.5 the flicker; mode hud; LivingBoltLook.cs).
# GOLEM=<s since cast> GOLCOATS=<n> GOLPUNCH=<n.swing> GOLFX=shed|collapse|clot GOLFXT=<s> (the Tank Hemomancer's Blood Golem: gathering, standing, punching, shedding, bursting; mode hud; BloodGolemLook.cs).
# COLOSSUS=<s since cast> COLWIND=<0-1> COLEND=<s> (the Tank Elementalist's Stone Colossus: rising, standing, slamming, crumbling; mode hud; StoneColossusLook.cs).
# BLOODBATH=<s since the cast> (the Tank Hemomancer's Bloodbath pool, wading enemies and heal numbers; BATHMASTERED=1 for 220 px; mode hud, 2 players).
# NEEDLESWARM=<s held> NSALT=<s> (the DPS Hemomancer's Needle Swarm: Fire held on a grunt, tendrils growing and wrapping, shots breaking needles; NSALT the Alt's fan; ShotNeedleSwarm.cs; mode hud).
# BLOODWALL=<s> BWCLOCK=<s> (the tank Hemomancer laying a Blood Wall, ShotBloodWall.cs; mode hud, 2 players).
# CAST="Poison Dart" DARTKILL=<s> (its target dies that long after the cast, so the poison spreads; CASTT after it shows the globs).
# CLOT=<draw s> (the Gore Mortar charging, clots in flight, a wet pool and a congealed one; mode hud).
# LAG=pill|chart|guest LAGFROZEN=1 (the lag chart with made-up numbers, ShotLag.cs; mode hud, 3 players).
# FUMA=orbit|lash|cyclone|steal FUMAT=<clock s> (with NINJA=2: the Fuma Shuriken's Orbit stance; FumaLook.cs).
# WARDENINTRO=<s> (the Warden's entrance that far in: the cell closing, the padlock, the break, the scan band; WardenIntro.cs; e.g. shot.sh x hud 5 3 2).
# NINJA=n / BLOOD=n (everyone plays that class, with weapon n), CAST="Blood Wall" (cast an ability; CASTT=<seconds after>).
# WALLTOUCH=1 [WALLT=<s>] (you touching the nearest long wall, crackling; with LAYOUT=3; WallTouchLook.cs).
# Needs mono (mcs) and python3 with Pillow. Output: Tools/Preview/out/<out-name>.png
set -e
cd "$(dirname "$0")"
R=../../Assets/Scripts
mkdir -p out
mcs -langversion:7.2 -nowarn:414,169,649,219,618 -out:out/shot.exe Stubs.cs Shot*.cs $R/Core/*.cs $R/Game/*.cs >/dev/null
cd out
mono shot.exe "$1" "${3:-6}" "${4:-12.2}" "${5:-1}" "$2" >/dev/null
python3 ../shot.py "$1" "$1.png"
echo "Tools/Preview/out/$1.png"
