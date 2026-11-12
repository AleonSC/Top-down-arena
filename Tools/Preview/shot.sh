#!/bin/bash
# Renders a game screen to a PNG without Unity: Unity is replaced by stubs (Stubs.cs) that record the GL
# triangles and IMGUI draw calls, and shot.py composites them with Pillow.
#   Tools/Preview/shot.sh <out-name> <mode> [wave] [seconds] [players]
# picker: GOLDINSTEAD=1 (on the Bazaar: the "Gold instead" key under the row).
# Modes (see ShotModes.cs; flags in ShotScenes.cs): hud, dev, bg, lobby, lobbypr, practicehud, labsetup, labpanel, tab, picker, legendary, ...
# Env flags tweak the scene, e.g. KNIGHT=1 (Knights), POSE=1 (fixed pose), THRUST=0.3 (Lance jab frame),
# DAZE=1 (three enemies dazed round you; mode hud), BOW=0.9, MASTERY=1, BG=3 (background number for 'bg'), MOUSE=x,y (hover for lobby modes), SOLO=1,
# menu: PLAY=1 (the Play picker; NOSAVES=1, MORE=1 extra saves, OLDER=1 the older runs, PAGE=n).
# records: SECTION=4 (Recent runs), RTAB=1 (Trends), RSEL=n (the open run), MOUSE=x,y (hover).
# DOWNED=down:<s since the hit>|revive:<0-1>|up:<s since standing> (you downed, being revived, getting up; mode hud, 2 players).
# QUICKMET=<n> (the lobby's Quick Play tab with n bosses beaten; mode lobby).
# TUTORIAL=<lesson 0-4>,<steps done> (the first-run tutorial prompt; mode hud).
# RIOT=ready|wait,17|block,0.1 (Riot Gear: the plate, the recharge, a block that long ago; use with mode hud).
# MOMENTUM=<0-5> (the DPS passive's stack pips round you; mode hud, 2 players).
# NONSHOOT=1 (the Fuse Runner, Hookman and Sapper caught mid-move, with Tripwire Pods).
# PORTALS=1 (spawn portals: opening, letting a burst out, a flank behind you with its arrow, closing; SpawnPortalLook.cs).
# SWORD=1 (the Swordsman and Shield Swordsman in each phase: closing in, wind-up, cut, recovery; SWORDBLOCK=1 flashes the shield).
# LUNGE=0.5 (the Lunger, Spike Block, Charger and Riot Shield part-way through their wind-up: the shared lane).
# TASER=0.3 (the Taser Guard charging, a Fork Bolt in flight over a field of bullets, and the hit on you 0.3 s ago; TASER=-1 for no hit; mode hud).
# SECURITY=0.2 (the Security Guard: locking on with its lane, just fired with a casing and a tracer, reloading, ready and empty; mode hud).
# AIM=0.7 (the aimed shooters winding up: Sniper, Shard, Medic, Splitter, Phantom, Grunt with their aim lines).
# TAGS=1 (a row of enemies on first sight, each with its name and job tag; uses a fresh profile).
# WAVECLEARED=1 WCWAVE=17 (the between-waves box after wave 17: the new-enemy chips for wave 18).
# HEMODPS=spray|debt|rush (the DPS Hemomancer's Arterial Spray, Blood Debt, Vein Rush; DEBTSETTLE=paid|forfeit, RUSHT=<s>; mode hud).
# DART=<s into the scene> (Poison Dart played through the game like the mockup: dart at 0.5 s, deaths at 2.2 and 3.9 s; DARTCAP=1 mastered; ShotPoisonDart.cs).
# PHALANX=<s since the cast> (Bone Phalanx played through the game; PHALANXCAP=1 mastered; ShotPhalanx.cs). FXOFF=1: Visual effects off (plain looks).
# KNIGHTDPS=steel|gauntlet|mercy|bridge KDT=<s> (the DPS Knight's abilities replaying the mockup's scenes through the game; KDW=<0-2> the weapon; crop (352,200)-(928,520) to set beside the mockup; ShotKnightDps.cs).
# WILDCARD=1 (lobby: you as a Wildcard, Twin Blades, Shadow Step, Blade Fan on Q, Death Lotus; the class wheel).
# pacman (mode): PACKEYS=l1.7,u0.6 (steer left then up), PACFRIGHT=5 (ghosts blue), PACDIE=0.7, PACOVER=1, PACPAUSE=1 (PacmanUi.cs).
# GASLOB=<s into the wind-up> (the Gas Grenadier's overhand lob: ring, arm, canister; mode hud; GasLobLook.cs).
# ZOOM=1.8 (the scroll-wheel zoom on you, CameraZoom.cs).
# NADET=<s> (the Twin Rifle's Rocket assist that long after a launch), NADELOAD=<s> (a tube loading; TwinNadeLook.cs);
#   with RIFLEFIRE=1 (holding fire: the Twin Rifle held wide, Spread arms) and CROSSAT=<px> (the cursor's distance).
# ROD=<s since the cast> (you take Lightning Rod, the tank Elementalist's second ultimate, and cast it that long before the shot; mode hud).
# TANKALT=<s> (the Tank Soldier's Alt fires: Double Barrel that long after firing, shells pips, Proximity Shells, Tear Gas; TankAltLook.cs).
# DOUBLEBARREL=<s> (the Soldier DPS Double Barrel that long into a fight: fire held at three dummies in a line; DBBOTH=1 Alt both barrels at 0.3 s; DBIDLE=1 not firing; mode hud; DoubleBarrelLook.cs).
# UNDERTOW=<meter>,<s held> (the Repulsion Cannon's Undertow meter, and its pull held that long on a pack of grunts; mode hud).
# TWINNADES=<loaded>,<next tube 0/1>,<charge> (the Twin Rifle's grenade tubes and pips; with CAST=Airstrike CASTT=<s> for the Airstrike).
# JUGG=<s since cast> JUGGARMOR=<0-1> (the Tank Soldier's Juggernaut Suit: plates, armour bar, flak tracers; mode hud).
# BLOODBATH=<s since the cast> (the Tank Hemomancer's Bloodbath pool, wading enemies and heal numbers; BATHMASTERED=1 for 220 px; mode hud, 2 players).
# BLOODWALL=<s> BWCLOCK=<s> (the tank Hemomancer laying a Blood Wall, ShotBloodWall.cs; mode hud, 2 players).
# CAST="Poison Dart" DARTKILL=<s> (its target dies that long after the cast, so the poison spreads; CASTT after it shows the globs).
# CLOT=<draw s> (the Clot Cannon charging, clots in flight, a wet pool and a congealed one; mode hud).
# LAG=pill|chart|guest LAGFROZEN=1 (the lag chart with made-up numbers, ShotLag.cs; mode hud, 3 players).
# FUMA=orbit|lash|cyclone|steal FUMAT=<clock s> (with NINJA=2: the Fuma Shuriken's Orbit stance; FumaLook.cs).
# NINJA=n / BLOOD=n (everyone plays that class, with weapon n), CAST="Blood Wall" (cast an ability; CASTT=<seconds after>).
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
