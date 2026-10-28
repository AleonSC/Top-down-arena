#!/bin/bash
# Renders a game screen to a PNG without Unity: Unity is replaced by stubs (Stubs.cs) that record the GL
# triangles and IMGUI draw calls, and shot.py composites them with Pillow.
#   Tools/Preview/shot.sh <out-name> <mode> [wave] [seconds] [players]
# Modes (see Shot.cs): hud, dev, bg, lobby, lobbypr, practicehud, labsetup, labpanel, tab, picker, legendary, ...
# Env flags tweak the scene, e.g. KNIGHT=1 (Knights), POSE=1 (fixed pose), THRUST=0.3 (Lance jab frame),
# BOW=0.9, MASTERY=1, BG=3 (background number for 'bg'), MOUSE=x,y (hover for lobby modes), SOLO=1,
# NINJA=n / BLOOD=n (everyone plays that class, with weapon n).
# Needs mono (mcs) and python3 with Pillow. Output: Tools/Preview/out/<out-name>.png
set -e
cd "$(dirname "$0")"
R=../../Assets/Scripts
mkdir -p out
mcs -langversion:7.2 -nowarn:414,169,649,219,618 -out:out/shot.exe Stubs.cs Shot.cs $R/Core/*.cs $R/Game/*.cs >/dev/null
cd out
mono shot.exe "$1" "${3:-6}" "${4:-12.2}" "${5:-1}" "$2" >/dev/null
python3 ../shot.py "$1" "$1.png"
echo "Tools/Preview/out/$1.png"
