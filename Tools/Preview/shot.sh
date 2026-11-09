#!/bin/bash
# Renders a game screen to a PNG without Unity: Unity is replaced by stubs (Stubs.cs) that record the GL
# triangles and IMGUI draw calls, and shot.py composites them with Pillow.
#   Tools/Preview/shot.sh <out-name> <mode> [wave] [seconds] [players]
# Modes (see ShotModes.cs; flags in ShotScenes.cs): hud, dev, bg, lobby, lobbypr, practicehud, labsetup, labpanel, tab, picker, legendary, ...
# Env flags tweak the scene, e.g. KNIGHT=1 (Knights), POSE=1 (fixed pose), THRUST=0.3 (Lance jab frame),
# BOW=0.9, MASTERY=1, BG=3 (background number for 'bg'), MOUSE=x,y (hover for lobby modes), SOLO=1,
# menu: MENUTIPAT=<s> (clock for the tip box), MENUTIP=<0-13> (which tip), PLAY=1 (the Play picker; NOSAVES=1, MORE=1 extra saves, OLDER=1 the older runs, PAGE=n).
# records: SECTION=4 (Recent runs), RTAB=1 (Trends), RSEL=n (the open run), MOUSE=x,y (hover).
# DOWNED=down:<s since the hit>|revive:<0-1>|up:<s since standing> (you downed, being revived, getting up; mode hud, 2 players).
# RIOT=ready|wait,17|block,0.1 (Riot Gear: the plate, the recharge, a block that long ago; use with mode hud).
# NONSHOOT=1 (the Fuse Runner, Hookman and Sapper caught mid-move, with Tripwire Pods).
# SWORD=1 (the Swordsman and Shield Swordsman in each phase: closing in, wind-up, cut, recovery; SWORDBLOCK=1 flashes the shield).
# LUNGE=0.5 (the Lunger, Spike Block, Charger and Riot Shield part-way through their wind-up: the shared lane).
# AIM=0.7 (the aimed shooters winding up: Sniper, Shard, Medic, Splitter, Phantom, Grunt with their aim lines).
# TAGS=1 (a row of enemies on first sight, each with its name and job tag; uses a fresh profile).
# WAVECLEARED=1 WCWAVE=17 (the between-waves box after wave 17: the new-enemy chips for wave 18).
# HEMODPS=spray|debt|rush (the DPS Hemomancer's Arterial Spray, Blood Debt, Vein Rush; DEBTSETTLE=paid|forfeit, RUSHT=<s>; mode hud).
# ZOOM=1.8 (the scroll-wheel zoom on you, CameraZoom.cs).
# TWINNADES=<loaded>,<next tube 0/1>,<charge> (the Twin Rifle's grenade tubes and pips; with CAST=Airstrike CASTT=<s> for the Airstrike).
# JUGG=<s since cast> JUGGARMOR=<0-1> (the Tank Soldier's Juggernaut Suit: plates, armour bar, flak tracers; mode hud).
# NINJA=n / BLOOD=n (everyone plays that class, with weapon n), CAST="Crimson Pact" (cast an ability; CASTT=<seconds after>).
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
